using System.Buffers;
using System.IO.Pipelines;
using Google.Protobuf;
using Grpc.Core;

namespace Solace.ObjectStore.Server.Services;

internal sealed partial class ObjectStoreServiceImpl : ObjectStoreService.ObjectStoreServiceBase
{
    private readonly DataStore _dataStore;

    private readonly ILogger<ObjectStoreServiceImpl> _logger;

    public ObjectStoreServiceImpl(DataStore dataStore, ILogger<ObjectStoreServiceImpl> logger)
    {
        _dataStore = dataStore;
        _logger = logger;
    }

    public override async Task<StoreObjectResponse> StoreObject(IAsyncStreamReader<StoreObjectRequest> requestStream, ServerCallContext context)
    {
        var pipe = new Pipe();

        var storeTask = _dataStore.StoreAsync(pipe.Reader.AsStream(), context.CancellationToken);

        try
        {
            using var writerStream = pipe.Writer.AsStream();
            await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
            {
                await pipe.Writer.WriteAsync(request.ChunkData.Memory, context.CancellationToken);
            }

            await pipe.Writer.FlushAsync();
            await pipe.Writer.CompleteAsync();
        }
        catch (Exception exception)
        {
            LogStoreObjectStreamWriteFail(exception);
            await pipe.Writer.CompleteAsync(exception);
            throw new RpcException(new Status(StatusCode.Internal, "Object upload failed mid-stream."));
        }

        string id;
        try
        {
            id = await storeTask;
        }
        catch (DataStore.DataStoreException ex)
        {
            _logger.LogCritical(ex, "Failed to store object to data store: {Message}", ex.Message);
            throw new RpcException(new Status(StatusCode.Internal, "Object store failed to write object: " + ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Unexpected failure while storing object");
            throw new RpcException(new Status(StatusCode.Internal, "Object store failed while storing object."));
        }

        LogStoreObjectSuccess(id);

        return new StoreObjectResponse
        {
            Id = id,
        };
    }

    public override async Task GetObject(GetObjectRequest request, IServerStreamWriter<GetObjectResponse> responseStream, ServerCallContext context)
    {
        LogGetObject(request.Id);

        var @object = await _dataStore.LoadAsync(request.Id, context.CancellationToken);

        using var objectStream = @object.Stream;

        if (objectStream is null)
        {
            LogGetObjectObjectNotFound(request.Id);
            context.Status = new Status(StatusCode.NotFound, $"Object with Id '{request.Id}' does not exist.");
            return;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        int bytesRead;

        try
        {
            while ((bytesRead = await objectStream.ReadAsync(buffer, context.CancellationToken)) > 0)
            {
                if (context.CancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var response = new GetObjectResponse
                {
                    TotalLength = @object.Length,
                    ChunkData = ByteString.CopyFrom(buffer, 0, bytesRead),
                };

                await responseStream.WriteAsync(response);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: false);
        }
    }

    public override async Task<DeleteObjectResponse> DeleteObject(DeleteObjectRequest request, ServerCallContext context)
    {
        LogDeleteObject(request.Id);

        await _dataStore.DeleteAsync(request.Id, context.CancellationToken);

        return new DeleteObjectResponse
        {
            Success = true,
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Stored new object '{Id}'")]
    private partial void LogStoreObjectSuccess(string Id);

    [LoggerMessage(Level = LogLevel.Error, Message = "Object store failed mid-stream")]
    private partial void LogStoreObjectStreamWriteFail(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Request for object '{Id}'")]
    private partial void LogGetObject(string Id);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Requested object '{Id}' does not exist")]
    private partial void LogGetObjectObjectNotFound(string Id);

    [LoggerMessage(Level = LogLevel.Information, Message = "Request to delete object '{Id}'")]
    private partial void LogDeleteObject(string Id);
}