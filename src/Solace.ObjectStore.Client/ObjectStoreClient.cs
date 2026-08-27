using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Solace.ObjectStore.Client;

public sealed class ObjectStoreClient : IAsyncDisposable
{
    public sealed class ConnectException : ObjectStoreClientException
    {
        public ConnectException(string? message)
            : base(message)
        {
        }

        public ConnectException(string? message, Exception? cause)
            : base(message, cause)
        {
        }
    }

    private readonly string _host;
    private readonly int _port;
    private readonly Channel<Command> _commandQueue;
    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource _initialConnectTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _processingTask;

    public static async Task<ObjectStoreClient> ConnectAsync(string connectionString)
    {
        string[] parts = connectionString.Split(':', 2);
        string host = parts[0];
        if (!int.TryParse(parts.Length > 1 ? parts[1] : "5396", out int port) || port is <= 0 or > 65535)
        {
            throw new ArgumentException("Invalid port number in connection string.");
        }

        var client = new ObjectStoreClient(host, port);
        await client._initialConnectTcs.Task;
        return client;
    }

    private ObjectStoreClient(string host, int port)
    {
        _host = host;
        _port = port;
        _commandQueue = Channel.CreateUnbounded<Command>();
        _processingTask = Task.Run(ProcessConnectionAsync);
    }

    public async Task<string?> StoreAsync(ReadOnlyMemory<byte> data)
    {
        var result = await EnqueueCommand(CommandType.Store, data);
        return (string?)result;
    }

    public async Task<byte[]?> GetAsync(string id)
    {
        var result = await EnqueueCommand(CommandType.Get, id);
        return (byte[]?)result;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await EnqueueCommand(CommandType.Delete, id);
        return (bool)result!;
    }

    private Task<object?> EnqueueCommand(CommandType type, object data)
    {
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_commandQueue.Writer.TryWrite(new Command(type, data, tcs)))
        {
            tcs.SetException(new ObjectDisposedException(nameof(ObjectStoreClient)));
        }

        return tcs.Task;
    }

    private async Task ProcessConnectionAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            Socket? socket = null;
            NetworkStream? stream = null;

            try
            {
                socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(_host, _port, _cts.Token);

                _initialConnectTcs.TrySetResult();

                stream = new NetworkStream(socket, ownsSocket: true);
                await RunMultiplexedLoopsAsync(stream);

                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (_initialConnectTcs.TrySetException(new ConnectException($"Could not connect to {_host}:{_port}", ex)))
                {
                    return;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), _cts.Token);
                }
                catch
                {
                    break;
                }
            }
            finally
            {
                stream?.Dispose();
                socket?.Dispose();
            }
        }
    }

    private async Task RunMultiplexedLoopsAsync(Stream stream)
    {
        var reader = PipeReader.Create(stream);
        var writer = PipeWriter.Create(stream);
        var pendingResponses = Channel.CreateUnbounded<Command>();

        using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);

        var readTask = ReadLoopAsync(reader, pendingResponses.Reader, loopCts.Token);
        var writeTask = WriteLoopAsync(writer, pendingResponses.Writer, loopCts.Token);

        Task completedTask = await Task.WhenAny(readTask, writeTask);
        loopCts.Cancel();

        try
        {
            await Task.WhenAll(readTask, writeTask);
        }
        catch
        {
        }

        pendingResponses.Writer.TryComplete();
        var dropEx = new ConnectException("Connection dropped before response was received.");
        await foreach (var cmd in pendingResponses.Reader.ReadAllAsync())
        {
            cmd.Tcs.TrySetException(dropEx);
        }

        await completedTask; // Rethrow inner exception to trigger reconnect
    }

    private async Task WriteLoopAsync(PipeWriter writer, ChannelWriter<Command> pendingResponses, CancellationToken token)
    {
        try
        {
            await foreach (var command in _commandQueue.Reader.ReadAllAsync(token))
            {
                pendingResponses.TryWrite(command);
                await WriteCommandAsync(writer, command, token);
            }
        }
        finally
        {
            await writer.CompleteAsync();
        }
    }

    private static async Task ReadLoopAsync(PipeReader reader, ChannelReader<Command> pendingResponses, CancellationToken token)
    {
        Range[] partsArray = ArrayPool<Range>.Shared.Rent(2);
        try
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync(token);
                ReadOnlySequence<byte> buffer = result.Buffer;

                if (TryReadMessage(ref buffer, out ReadOnlySequence<byte> line))
                {
                    if (!pendingResponses.TryRead(out var command))
                    {
                        throw new InvalidOperationException("Received unsolicited response from server.");
                    }

                    var message = Encoding.ASCII.GetString(line).AsSpan().Trim('\r');
                    var parts = partsArray.AsSpan(0, 2);
                    var partsLength = message.Split(parts, ' ');
                    var partsLocal = parts[..partsLength];

                    if (message[partsLocal[0]] is "ERR")
                    {
                        command.Tcs.TrySetResult(command.Type is CommandType.Delete ? false : null);
                        reader.AdvanceTo(buffer.Start);
                        continue;
                    }

                    if (message[partsLocal[0]] is "OK")
                    {
                        if (command.Type is CommandType.Delete)
                        {
                            command.Tcs.TrySetResult(true);
                            reader.AdvanceTo(buffer.Start);
                            continue;
                        }

                        if (command.Type is CommandType.Store)
                        {
                            command.Tcs.TrySetResult(partsLocal.Length > 1 ? message[partsLocal[1]].ToString() : null);
                            reader.AdvanceTo(buffer.Start);
                            continue;
                        }

                        if (command.Type is CommandType.Get && partsLocal.Length is 2 && int.TryParse(message[partsLocal[1]], out int length))
                        {
                            reader.AdvanceTo(buffer.Start);
                            await ReadBinaryPayloadAsync(reader, length, command, token);
                            continue;
                        }
                    }

                    throw new InvalidDataException("Invalid server response format.");
                }

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    throw new EndOfStreamException("Server closed the connection.");
                }
            }
        }
        finally
        {
            ArrayPool<Range>.Shared.Return(partsArray);
            await reader.CompleteAsync();
        }
    }

    private static async Task WriteCommandAsync(PipeWriter writer, Command command, CancellationToken token)
    {
        switch (command.Type)
        {
            case CommandType.Store:
                var memory = (ReadOnlyMemory<byte>)command.Data;
                var header = Encoding.ASCII.GetBytes($"STORE {memory.Length}\n");
                writer.Write(header);
                writer.Write(memory.Span);
                await writer.FlushAsync(token);
                break;
            case CommandType.Get:
                await writer.WriteAsync(Encoding.ASCII.GetBytes($"GET {(string)command.Data}\n"), token);
                break;
            case CommandType.Delete:
                await writer.WriteAsync(Encoding.ASCII.GetBytes($"DEL {(string)command.Data}\n"), token);
                break;
        }
    }

    private static async Task ReadBinaryPayloadAsync(PipeReader reader, int length, Command command, CancellationToken token)
    {
        if (length is 0)
        {
            command.Tcs.TrySetResult(Array.Empty<byte>());
            return;
        }

        while (true)
        {
            ReadResult result = await reader.ReadAsync(token);
            ReadOnlySequence<byte> buffer = result.Buffer;

            if (buffer.Length >= length)
            {
                byte[] data = buffer.Slice(0, length).ToArray();
                command.Tcs.TrySetResult(data);

                reader.AdvanceTo(buffer.GetPosition(length));
                return;
            }

            reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
            {
                throw new EndOfStreamException("Incomplete binary payload received.");
            }
        }
    }

    private static bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
    {
        SequencePosition? position = buffer.PositionOf((byte)'\n');
        if (position is null)
        {
            line = default;
            return false;
        }

        line = buffer.Slice(0, position.Value);
        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _commandQueue.Writer.TryComplete();

        try
        {
            await _processingTask.WaitAsync(TimeSpan.FromSeconds(3));
        }
        catch
        {
        }

        var ex = new ObjectDisposedException(nameof(ObjectStoreClient));
        
        while (_commandQueue.Reader.TryRead(out var cmd))
        {
            cmd.Tcs.TrySetException(ex);
        }
    }

    private enum CommandType
    {
        Store,
        Get,
        Delete,
    }

    private readonly record struct Command(CommandType Type, object Data, TaskCompletionSource<object?> Tcs);
}