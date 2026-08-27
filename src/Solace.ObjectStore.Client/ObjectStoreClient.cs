using System.Buffers;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Solace.Common.Utils;

namespace Solace.ObjectStore.Client;

public sealed class ObjectStoreClient : IAsyncDisposable
{
    private readonly GrpcChannel _channel;
    private readonly ObjectStoreService.ObjectStoreServiceClient _client;

    public static async Task<ObjectStoreClient> ConnectAsync(string connectionString, ILogger logger)
    {
        _ = logger;

        var channel = GrpcChannel.ForAddress(connectionString);
        var client = new ObjectStoreService.ObjectStoreServiceClient(channel);

        return new ObjectStoreClient(channel, client, logger);
    }

    public ObjectStoreClient(GrpcChannel channel, ObjectStoreService.ObjectStoreServiceClient client, ILogger logger)
    {
        _ = logger;
        _channel = channel;
        _client = client;
    }

    public async Task<long> GetTotalSizeAsync(CancellationToken cancellationToken = default)
    {
        var response = await _client.GetTotalSizeAsync(new GetTotalSizeRequest(), cancellationToken: cancellationToken);

        return response.TotalSize;
    }

    [Obsolete("Make sure to only call from the DeleteAll endpoint", false)]
    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
        => await _client.DeleteAllAsync(new DeleteAllRequest(), cancellationToken: cancellationToken);

    public async Task<Guid?> StoreAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        using var call = _client.StoreObject(cancellationToken: cancellationToken);

        await call.RequestStream.WriteAsync(new StoreObjectRequest
        {
            ChunkData = ByteString.CopyFrom(data.Span),
        }, cancellationToken);

        await call.RequestStream.CompleteAsync();

        var response = await call;

        return Guid.FromLowHigh(response.IdLow, response.IdHigh);
    }

    public async Task<Guid?> StoreAsync(Stream data, CancellationToken cancellationToken = default)
    {
        using var call = _client.StoreObject(cancellationToken: cancellationToken);

        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        int bytesRead;
        try
        {
            while ((bytesRead = await data.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await call.RequestStream.WriteAsync(new StoreObjectRequest
                {
                    ChunkData = ByteString.CopyFrom(buffer, 0, bytesRead),
                }, cancellationToken);
            }

            await call.RequestStream.CompleteAsync();

            var response = await call;

            return Guid.FromLowHigh(response.IdLow, response.IdHigh);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: false);
        }
    }

    public async Task<Guid?> UpdateAsync(Guid id, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        var idLowHigh = id.ToLowHigh();

        using var call = _client.StoreObject(cancellationToken: cancellationToken);

        await call.RequestStream.WriteAsync(new StoreObjectRequest
        {
            IdLow = idLowHigh.Low,
            IdHigh = idLowHigh.High,
            ChunkData = ByteString.CopyFrom(data.Span),
        }, cancellationToken);

        await call.RequestStream.CompleteAsync();

        var response = await call;

        return Guid.FromLowHigh(response.IdLow, response.IdHigh);
    }

    public async Task<Guid?> UpdateAsync(Guid id, Stream data, CancellationToken cancellationToken = default)
    {
        var idLowHigh = id.ToLowHigh();

        using var call = _client.StoreObject(cancellationToken: cancellationToken);

        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        int bytesRead;
        try
        {
            while ((bytesRead = await data.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await call.RequestStream.WriteAsync(new StoreObjectRequest
                {
                    IdLow = idLowHigh.Low,
                    IdHigh = idLowHigh.High,
                    ChunkData = ByteString.CopyFrom(buffer, 0, bytesRead),
                }, cancellationToken);
            }

            await call.RequestStream.CompleteAsync();

            var response = await call;

            return Guid.FromLowHigh(response.IdLow, response.IdHigh);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: false);
        }
    }

    public async Task<Stream?> GetStreamAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var idLowHigh = id.ToLowHigh();
        var call = _client.GetObject(new GetObjectRequest { IdLow = idLowHigh.Low, IdHigh = idLowHigh.High, }, cancellationToken: cancellationToken);

        try
        {
            if (!await call.ResponseStream.MoveNext(cancellationToken))
            {
                // 0 bytes
                call.Dispose();
                return Stream.Null;
            }

            return new GetObjectStreamWrapper(call, call.ResponseStream.Current, cancellationToken);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            call.Dispose();
            return null;
        }
        catch
        {
            call.Dispose();
            throw;
        }
    }

    public async Task<byte[]?> GetArrayAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            MemoryStream? memoryStream = null;
            var idLowHigh = id.ToLowHigh();
            using var call = _client.GetObject(new GetObjectRequest { IdLow = idLowHigh.Low, IdHigh = idLowHigh.High, }, cancellationToken: cancellationToken);

            await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                memoryStream ??= new MemoryStream((int)response.TotalLength);
                response.ChunkData.WriteTo(memoryStream);
            }

            if (memoryStream is null)
            {
                return [];
            }

            return memoryStream.ToArray();
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Memory<byte>?> GetMemoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            MemoryStream? memoryStream = null;
            var idLowHigh = id.ToLowHigh();
            using var call = _client.GetObject(new GetObjectRequest { IdLow = idLowHigh.Low, IdHigh = idLowHigh.High, }, cancellationToken: cancellationToken);

            await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                memoryStream ??= new MemoryStream((int)response.TotalLength);
                response.ChunkData.WriteTo(memoryStream);
            }

            if (memoryStream is null)
            {
                return Memory<byte>.Empty;
            }

            _ = memoryStream.TryGetBuffer(out var buffer);

            return buffer.AsMemory();
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var idLowHigh = id.ToLowHigh();
        var response = await _client.DeleteObjectAsync(new DeleteObjectRequest { IdLow = idLowHigh.Low, IdHigh = idLowHigh.High, }, cancellationToken: cancellationToken);

        return response.Success;
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.ShutdownAsync();
        _channel.Dispose();
    }

    private sealed class GetObjectStreamWrapper : Stream
    {
        private readonly AsyncServerStreamingCall<GetObjectResponse> _call;
        private readonly CancellationToken _externalCancellationToken;
        private readonly long _length;
        private ReadOnlyMemory<byte> _currentChunk;
        private long _position;
        private bool _isDone;

        public GetObjectStreamWrapper(AsyncServerStreamingCall<GetObjectResponse> call, GetObjectResponse firstResponse, CancellationToken cancellationToken)
        {
            _call = call;
            _currentChunk = firstResponse.ChunkData.Memory;
            _externalCancellationToken = cancellationToken;

            _length = firstResponse.TotalLength;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.IsEmpty)
            {
                return 0;
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_externalCancellationToken, cancellationToken);

            while (_currentChunk.IsEmpty)
            {
                if (_isDone)
                {
                    return 0;
                }

                if (await _call.ResponseStream.MoveNext(linkedCts.Token))
                {
                    _currentChunk = _call.ResponseStream.Current.ChunkData.Memory;
                }
                else
                {
                    _isDone = true;
                    return 0; // end of stream
                }
            }

            var bytesToRead = int.Min(buffer.Length, _currentChunk.Length);
            _currentChunk.Span[..bytesToRead].CopyTo(buffer.Span);
            _currentChunk = _currentChunk[bytesToRead..];

            _position += bytesToRead;

            return bytesToRead;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => await ReadAsync(buffer.AsMemory(offset, count), cancellationToken);

        public override int Read(byte[] buffer, int offset, int count)
            => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _call.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            _call.Dispose();
            await base.DisposeAsync();
        }
    }
}