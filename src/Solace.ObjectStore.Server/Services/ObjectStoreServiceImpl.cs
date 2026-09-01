using System.Buffers;
using System.IO.Pipelines;
using Google.Protobuf;
using Grpc.Core;
using Solace.Common.Utils;

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

    public override async Task<GetTotalSizeResponse> GetTotalSize(GetTotalSizeRequest request, ServerCallContext context)
    {
        var size = _dataStore.GetTotalSize(context.CancellationToken);

        return new GetTotalSizeResponse() { TotalSize = size, };
    }

    public override async Task<DeleteAllResponse> DeleteAll(DeleteAllRequest request, ServerCallContext context)
    {
        _dataStore.DeleteAll();

        return new DeleteAllResponse();
    }

    public override async Task<StoreObjectResponse> StoreObject(IAsyncStreamReader<StoreObjectRequest> requestStream, ServerCallContext context)
    {
        var pipe = new Pipe();

        Task<Guid>? storeTask = null;

        try
        {
            using var writerStream = pipe.Writer.AsStream();
            await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
            {
                if (storeTask is null)
                {
                    Guid? requestId;
                    if (request is { IdLow: 0, IdHigh: 0, })
                    {
                        requestId = null;
                    }
                    else
                    {
                        requestId = Guid.FromLowHigh(request.IdLow, request.IdHigh);
                    }

                    storeTask = _dataStore.StoreAsync(requestId, pipe.Reader.AsStream(), context.CancellationToken);
                }

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

        Guid id;
        try
        {
            if (storeTask is null)
            {
                id = await _dataStore.StoreAsync(null, Stream.Null, context.CancellationToken);
            }
            else
            {
                id = await storeTask;
            }
        }
        catch (DataStore.DataStoreException exception)
        {
            LogFailedToSstoreObjectToDataStore(exception);
            throw new RpcException(new Status(StatusCode.Internal, "Object store failed to write object: " + exception.Message));
        }
        catch (Exception exception)
        {
            LogUnexpectedFailureWhileStoringObject(exception);
            throw new RpcException(new Status(StatusCode.Internal, "Object store failed while storing object."));
        }

        LogStoreObjectSuccess(id);

        var (idLow, idHigh) = id.ToLowHigh();

        return new StoreObjectResponse
        {
            IdLow = idLow,
            IdHigh = idHigh,
        };
    }

    public override async Task GetObject(GetObjectRequest request, IServerStreamWriter<GetObjectResponse> responseStream, ServerCallContext context)
    {
        var id = Guid.FromLowHigh(request.IdLow, request.IdHigh);

        LogGetObject(id);

        var @object = await _dataStore.LoadAsync(id, context.CancellationToken);

        using var objectStream = @object.Stream;

        if (objectStream is null)
        {
            LogGetObjectObjectNotFound(id);
            context.Status = new Status(StatusCode.NotFound, $"Object with Id '{id}' does not exist.");
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
        var id = Guid.FromLowHigh(request.IdLow, request.IdHigh);

        LogDeleteObject(id);

        var exists = await _dataStore.DeleteAsync(id, context.CancellationToken);

        return new DeleteObjectResponse
        {
            Success = exists,
        };
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Failed to store object to data store")]
    private partial void LogFailedToSstoreObjectToDataStore(Exception exception);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Unexpected failure while storing object")]
    private partial void LogUnexpectedFailureWhileStoringObject(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stored new object '{Id}'")]
    private partial void LogStoreObjectSuccess(Guid Id);

    [LoggerMessage(Level = LogLevel.Error, Message = "Object store failed mid-stream")]
    private partial void LogStoreObjectStreamWriteFail(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Request for object '{Id}'")]
    private partial void LogGetObject(Guid Id);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Requested object '{Id}' does not exist")]
    private partial void LogGetObjectObjectNotFound(Guid Id);

    [LoggerMessage(Level = LogLevel.Information, Message = "Request to delete object '{Id}'")]
    private partial void LogDeleteObject(Guid Id);
}