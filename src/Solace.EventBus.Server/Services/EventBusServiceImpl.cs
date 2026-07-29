using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.ObjectPool;
using Solace.Common.ObjectPool;
using Solace.Common.Utils;

namespace Solace.EventBus.Server.Services;

internal sealed partial class EventBusServiceImpl : EventBusService.EventBusServiceBase
{
    // EventBusServiceImpl is not registered as singleton - created per request
    internal sealed class State
    {
        public ConcurrentDictionary<string, ConcurrentDictionary<Guid, SafeStreamWriter<EventMessage>>> Subscribers { get; } = [];
        public ConcurrentDictionary<string, ImmutableArray<HandlerConnection>> Handlers { get; } = [];
        public Lock HandlersLock { get; } = new();

        public ObjectPool<List<Task>> TaskListPool { get; } = ObjectPool.Create(new ListPooledObjectPolicy<Task>() { InitialCapacity = 4, });

        public ObjectPool<HashSet<HandlerConnection>> HashSetHandlerConnectionPool { get; } = ObjectPool.Create(new HashSetPooledObjectPolicy<HandlerConnection>() { InitialCapacity = 4, });

        private ulong _requestCounter;

        public ulong GetAndIncrementRequestCounter()
            => Interlocked.Increment(ref _requestCounter);
    }

    internal sealed class HandlerConnection : IDisposable
    {
        public required SafeStreamWriter<ServerMessage> Writer { get; init; }

        public ConcurrentDictionary<ulong, TaskCompletionSource<HandlerResponse>> PendingRequests { get; } = [];

        public void Dispose()
            => Writer.Dispose();
    }

    private readonly State _state;

    private readonly ILogger<EventBusServiceImpl> _logger;

    public EventBusServiceImpl(State state, ILogger<EventBusServiceImpl> logger)
    {
        _logger = logger;
        _state = state;
    }

    public override async Task<PublishResponse> Publish(PublishRequest request, ServerCallContext context)
    {
        LogPublishingMessage(_logger, request.QueueName, request.Type);

        if (_state.Subscribers.TryGetValue(request.QueueName, out var queueSubscribers))
        {
            var tasks = _state.TaskListPool.Get();

            try
            {
                var message = new EventMessage { Type = request.Type, Data = request.Data, Timestamp = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow), };

                foreach (var kvp in queueSubscribers)
                {
                    tasks.Add(SendEventAsync(_logger, queueSubscribers, kvp, message));
                }

                await Task.WhenAll(tasks);
            }
            finally
            {
                _state.TaskListPool.Return(tasks);
            }

            static async Task SendEventAsync(ILogger logger, ConcurrentDictionary<Guid, SafeStreamWriter<EventMessage>> queue, KeyValuePair<Guid, SafeStreamWriter<EventMessage>> kvp, EventMessage message)
            {
                try
                {
                    await kvp.Value.WriteAsync(message);
                }
                catch (Exception exception)
                {
                    LogSubscriberWriteFailed(logger, kvp.Key, exception);
                    queue.TryRemove(kvp.Key, out _);
                }
            }
        }

        return new PublishResponse { Success = true, };
    }

    public override async Task Subscribe(SubscribeRequest request, IServerStreamWriter<EventMessage> responseStream, ServerCallContext context)
    {
        var id = Guid.NewGuid();
        LogSubscriberConnected(_logger, request.QueueName, id);

        using var safeWriter = new SafeStreamWriter<EventMessage>(responseStream);
        _state.Subscribers.GetOrAdd(request.QueueName, static _ => new())[id] = safeWriter;

        await context.CancellationToken.AsTask()
            .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        LogSubscriberDisconnected(_logger, request.QueueName, id);

        if (_state.Subscribers.TryGetValue(request.QueueName, out var queue))
        {
            queue.TryRemove(id, out _);

            if (queue.IsEmpty)
            {
                _state.Subscribers.TryRemove(new(request.QueueName, queue));
            }
        }
    }

    public override async Task<ResponseMessage> Request(RequestMessage request, ServerCallContext context)
    {
        HashSet<HandlerConnection>? triedHandlers = null;

        while (!context.CancellationToken.IsCancellationRequested)
        {
            HandlerConnection? targetHandler = null;

            if (_state.Handlers.TryGetValue(request.QueueName, out var registeredHandlers) && !registeredHandlers.IsDefaultOrEmpty)
            {
                if (triedHandlers is null || triedHandlers.Count == 0)
                {
                    targetHandler = registeredHandlers[Random.Shared.Next(registeredHandlers.Length)];
                }
                else
                {
                    int startIndex = Random.Shared.Next(registeredHandlers.Length);
                    for (int i = 0; i < registeredHandlers.Length; i++)
                    {
                        var handler = registeredHandlers[(startIndex + i) % registeredHandlers.Length];
                        if (!triedHandlers.Contains(handler))
                        {
                            targetHandler = handler;
                            break;
                        }
                    }
                }
            }

            if (targetHandler is null)
            {
                if (triedHandlers is null or { Count: 0, })
                {
                    LogNoActiveHandlers(_logger, request.QueueName);
                }

                if (triedHandlers is not null)
                {
                    _state.HashSetHandlerConnectionPool.Return(triedHandlers);
                }

                return new ResponseMessage { Status = ResponseMessage.Types.Status.NoHandlers, ErrorMessage = "No active handlers." };
            }

            var correlationId = _state.GetAndIncrementRequestCounter();

            var tcs = new TaskCompletionSource<HandlerResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            targetHandler.PendingRequests[correlationId] = tcs;

            try
            {
                LogSendingRequestToHandler(_logger, request.QueueName, correlationId);

                await targetHandler.Writer.WriteAsync(new ServerMessage
                {
                    CorrelationId = correlationId,
                    Type = request.Type,
                    Data = request.Data,
                    Timestamp = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
                });

                var response = await tcs.Task.WaitAsync(context.CancellationToken);

                if (response.Status is HandlerResponse.Types.Status.NotHandled)
                {
                    triedHandlers ??= _state.HashSetHandlerConnectionPool.Get();
                    triedHandlers.Add(targetHandler);
                    continue;
                }

                if (triedHandlers is not null)
                {
                    _state.HashSetHandlerConnectionPool.Return(triedHandlers);
                }

                return new ResponseMessage
                {
                    Status = response.Status switch
                    {
                        HandlerResponse.Types.Status.Success => ResponseMessage.Types.Status.Success,
                        HandlerResponse.Types.Status.Error => ResponseMessage.Types.Status.HandlerError,
                        _ => throw new UnreachableException(),
                    },
                    Data = response.Data,
                };
            }
            catch (OperationCanceledException)
            {
                LogRequestCancelled(_logger, correlationId);

                if (triedHandlers is not null)
                {
                    _state.HashSetHandlerConnectionPool.Return(triedHandlers);
                }

                throw new RpcException(Status.DefaultCancelled);
            }
            catch (Exception exception)
            {
                LogRequestFailed(_logger, correlationId, exception.Message, exception);

                triedHandlers ??= _state.HashSetHandlerConnectionPool.Get();
                triedHandlers.Add(targetHandler);
                continue;
            }
            finally
            {
                targetHandler.PendingRequests.TryRemove(correlationId, out _);
            }
        }

        throw new RpcException(Status.DefaultCancelled);
    }

    public override async Task HandleRequests(IAsyncStreamReader<ClientMessage> requestStream, IServerStreamWriter<ServerMessage> responseStream, ServerCallContext context)
    {
        var registeredQueues = new HashSet<string>(StringComparer.Ordinal);
        using var connection = new HandlerConnection
        {
            Writer = new SafeStreamWriter<ServerMessage>(responseStream)
        };

        try
        {
            while (await requestStream.MoveNext(context.CancellationToken))
            {
                var message = requestStream.Current;

                if (message.PayloadCase == ClientMessage.PayloadOneofCase.RegisterQueue)
                {
                    var queue = message.RegisterQueue;

                    lock (_state.HandlersLock)
                    {
                        var current = _state.Handlers.GetValueOrDefault(queue, []);
                        if (!current.Contains(connection))
                        {
                            _state.Handlers[queue] = current.Add(connection);
                            LogRegisteringHandlerQueue(_logger, queue);
                        }
                    }

                    registeredQueues.Add(queue);
                }
                else if (message.PayloadCase == ClientMessage.PayloadOneofCase.Response)
                {
                    if (connection.PendingRequests.TryRemove(message.Response.CorrelationId, out var tcs))
                    {
                        tcs.SetResult(message.Response);
                    }
                    else
                    {
                        LogUnknownCorrelationId(_logger, message.Response.CorrelationId);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            LogHandlerStreamCancelled(_logger);
        }
        catch (Exception exception)
        {
            LogHandlerStreamError(_logger, exception);
            throw;
        }
        finally
        {
            LogCleaningUpHandlerConnection(_logger, registeredQueues.Count);

            lock (_state.HandlersLock)
            {
                foreach (var queue in registeredQueues)
                {
                    if (_state.Handlers.TryGetValue(queue, out var current))
                    {
                        var updated = current.Remove(connection);

                        if (updated.IsEmpty)
                        {
                            _state.Handlers.TryRemove(queue, out _);
                        }
                        else
                        {
                            _state.Handlers[queue] = updated;
                        }
                    }
                }
            }

            foreach (var tcs in connection.PendingRequests.Values)
            {
                tcs.TrySetException(new RpcException(new Status(StatusCode.Unavailable, "Handler lost connection.")));
            }
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Publishing message to queue '{QueueName}' with type '{MessageType}'")]
    private static partial void LogPublishingMessage(ILogger logger, string queueName, string messageType);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Failed to write to subscriber '{SubscriberId}'. Removing from subscribers queue")]
    private static partial void LogSubscriberWriteFailed(ILogger logger, Guid subscriberId, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Subscriber '{SubscriberId}' connected to queue '{QueueName}'")]
    private static partial void LogSubscriberConnected(ILogger logger, string queueName, Guid subscriberId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Subscriber '{SubscriberId}' disconnected from queue '{QueueName}'")]
    private static partial void LogSubscriberDisconnected(ILogger logger, string queueName, Guid subscriberId);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "No active handlers registered for queue '{QueueName}'")]
    private static partial void LogNoActiveHandlers(ILogger logger, string queueName);

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug, Message = "Dispatching request to handler for queue '{QueueName}' (CorrelationId: {CorrelationId})")]
    private static partial void LogSendingRequestToHandler(ILogger logger, string queueName, ulong correlationId);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Request with CorrelationId '{CorrelationId}' was cancelled")]
    private static partial void LogRequestCancelled(ILogger logger, ulong correlationId);

    [LoggerMessage(EventId = 8, Level = LogLevel.Error, Message = "Request failed for CorrelationId '{CorrelationId}': {ErrorMessage}")]
    private static partial void LogRequestFailed(ILogger logger, ulong correlationId, string errorMessage, Exception exception);

    [LoggerMessage(EventId = 9, Level = LogLevel.Information, Message = "Registering handler connection for queue '{QueueName}'")]
    private static partial void LogRegisteringHandlerQueue(ILogger logger, string queueName);

    [LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "Received response with unknown or expired CorrelationId '{CorrelationId}'")]
    private static partial void LogUnknownCorrelationId(ILogger logger, ulong correlationId);

    [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "Handler request stream was cancelled")]
    private static partial void LogHandlerStreamCancelled(ILogger logger);

    [LoggerMessage(EventId = 12, Level = LogLevel.Error, Message = "Error encountered in handler request stream processing")]
    private static partial void LogHandlerStreamError(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 13, Level = LogLevel.Information, Message = "Cleaning up handler connection. Removing from {QueueCount} registered queues")]
    private static partial void LogCleaningUpHandlerConnection(ILogger logger, int queueCount);
}