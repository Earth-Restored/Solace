using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace Solace.ObjectStore.Client;

public sealed class ObjectStoreClient : IAsyncDisposable
{
    public sealed class ConnectException : ObjectStoreClientException
    {
        public ConnectException()
            : base()
        {
        }

        public ConnectException(string? message)
            : base(message)
        {
        }

        public ConnectException(string? message, Exception? cause)
            : base(message, cause)
        {
        }
    }

    private readonly GrpcChannel _channel;
    private readonly ObjectStoreService.ObjectStoreServiceClient _client;

    public static async Task<ObjectStoreClient> ConnectAsync(string connectionString, ILogger logger)
    {
        try
        {
            var channel = GrpcChannel.ForAddress(connectionString);
            var client = new ObjectStoreService.ObjectStoreServiceClient(channel);
            return new ObjectStoreClient(channel, client);
        }
        catch (Exception ex)
        {
            logger.LogError(connectionString);
            logger.LogError(ex, "MEOW");
            return null!;
        }

    }

    public ObjectStoreClient(GrpcChannel channel, ObjectStoreService.ObjectStoreServiceClient client)
    {
        _channel = channel;
        _client = client;
    }

    public async Task<string?> StoreAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        using var call = _client.StoreObject(cancellationToken: cancellationToken);

        await call.RequestStream.WriteAsync(new StoreObjectRequest
        {
            ChunkData = ByteString.CopyFrom(data.Span),
        }, cancellationToken);

        await call.RequestStream.CompleteAsync();

        var response = await call;

        return response.Id;
    }

    public async Task<string?> StoreAsync(Stream data, CancellationToken cancellationToken = default)
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

            return response.Id;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: false);
        }
    }

    public async Task<Stream?> GetStreamAsync(string id, CancellationToken cancellationToken = default)
    {
        var call = _client.GetObject(new GetObjectRequest { Id = id }, cancellationToken: cancellationToken);

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
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
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

    public async Task<byte[]?> GetArrayAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            MemoryStream? memoryStream = null;
            using var call = _client.GetObject(new GetObjectRequest { Id = id }, cancellationToken: cancellationToken);

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
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Memory<byte>?> GetMemoryAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            MemoryStream? memoryStream = null;
            using var call = _client.GetObject(new GetObjectRequest { Id = id }, cancellationToken: cancellationToken);

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
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await _client.DeleteObjectAsync(new DeleteObjectRequest { Id = id, }, cancellationToken: cancellationToken);

        return response.Success;
    }

    public async ValueTask DisposeAsync()
        => _channel.Dispose();

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

            int bytesToRead = Math.Min(buffer.Length, _currentChunk.Length);
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