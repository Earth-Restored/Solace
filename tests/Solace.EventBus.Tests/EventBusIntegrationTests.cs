using Microsoft.Extensions.Logging.Abstractions;
using Solace.EventBus.Client;

namespace Solace.EventBus.Tests;

public sealed class EventBusIntegrationTests
{
    [Test]
    public async Task PublishAndSubscribe_StringMessage_ReceivedBySubscriber()
    {
        await using var server = await TestEventBusServer.StartAsync();
        await using var client = await EventBusClient.ConnectAsync(server.Address, NullLogger.Instance);

        var tcs = new TaskCompletionSource<SubscriberEvent>();

        await using var subscriber = await client.AddSubscriberAsync("test_string_queue", (evt, ct) =>
        {
            tcs.TrySetResult(evt);
            return Task.CompletedTask;
        }, ex => Task.CompletedTask);

        await Task.Delay(100);

        var publishResult = await client.PublishAsync("test_string_queue", "greeting", "Hello Server!");
        await Assert.That(publishResult).IsTrue();

        var receivedEvent = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(receivedEvent.Type).IsEqualTo("greeting");
        await Assert.That((string)receivedEvent.Data.Value!).IsEqualTo("Hello Server!");
    }

    [Test]
    public async Task PublishAndSubscribe_BinaryMessage_ReceivedBySubscriber()
    {
        await using var server = await TestEventBusServer.StartAsync();
        await using var client = await EventBusClient.ConnectAsync(server.Address, NullLogger.Instance);

        var tcs = new TaskCompletionSource<SubscriberEvent>();

        await using var subscriber = await client.AddSubscriberAsync("test_binary_queue", (evt, ct) =>
        {
            tcs.TrySetResult(evt);
            return Task.CompletedTask;
        }, ex => Task.CompletedTask);

        await Task.Delay(100);

        byte[] payload = [10, 20, 30, 40,];
        var publishResult = await client.PublishAsync("test_binary_queue", "binary_data", payload);
        await Assert.That(publishResult).IsTrue();

        var receivedEvent = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(receivedEvent.Type).IsEqualTo("binary_data");
        var receivedBytes = ((ReadOnlyMemory<byte>)receivedEvent.Data.Value!).ToArray();
        await Assert.That(receivedBytes).IsEquivalentTo(payload);
    }

    [Test]
    public async Task PublishAndSubscribe_StreamMessage_ReceivedBySubscriber()
    {
        await using var server = await TestEventBusServer.StartAsync();
        await using var client = await EventBusClient.ConnectAsync(server.Address, NullLogger.Instance);

        var tcs = new TaskCompletionSource<SubscriberEvent>();

        await using var subscriber = await client.AddSubscriberAsync("test_stream_queue", (evt, ct) =>
        {
            tcs.TrySetResult(evt);
            return Task.CompletedTask;
        }, ex => Task.CompletedTask);

        await Task.Delay(100);

        var streamData = new byte[50_000];
        new Random(42).NextBytes(streamData);

        var publishResult = await client.PublishAsync("test_stream_queue", "stream_type", new MemoryStream(streamData));
        await Assert.That(publishResult).IsTrue();

        var receivedEvent = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(receivedEvent.Type).IsEqualTo("stream_type");
        var receivedStream = (Stream)receivedEvent.Data.Value!;
        using var memoryStream = new MemoryStream();
        await receivedStream.CopyToAsync(memoryStream);

        await Assert.That(memoryStream.ToArray()).IsEquivalentTo(streamData);
    }

    [Test]
    public async Task RequestResponse_StringRequest_ReturnsHandlerResponse()
    {
        await using var server = await TestEventBusServer.StartAsync();
        await using var client = await EventBusClient.ConnectAsync(server.Address, NullLogger.Instance);

        await using var handler = await client.AddRequestHandlerAsync("rpc_queue", (req, ct) =>
        {
            var reqData = (string)req.Data.Value!;
            var reply = $"echo:{reqData}";
            return Task.FromResult<MessagePayload?>(new MessagePayload(reply));
        }, ex => Task.CompletedTask);

        await Task.Delay(100);

        var responsePayload = await client.RequestAsync("rpc_queue", "ping", "hello_rpc");

        await Assert.That(responsePayload).IsNotNull();
        await Assert.That((string)responsePayload!.Value.Value!).IsEqualTo("echo:hello_rpc");
    }

    [Test]
    public async Task RequestResponse_BinaryRequest_ReturnsHandlerResponse()
    {
        await using var server = await TestEventBusServer.StartAsync();
        await using var client = await EventBusClient.ConnectAsync(server.Address, NullLogger.Instance);

        await using var handler = await client.AddRequestHandlerAsync("rpc_binary_queue", (req, ct) =>
        {
            byte[] responseBytes = [99, 88, 77,];
            return Task.FromResult<MessagePayload?>(new MessagePayload(responseBytes));
        }, ex => Task.CompletedTask);

        await Task.Delay(100);

        var responsePayload = await client.RequestAsync("rpc_binary_queue", "binary_req", [1, 2, 3,]);

        await Assert.That(responsePayload).IsNotNull();
        var bytes = ((ReadOnlyMemory<byte>)responsePayload!.Value.Value!).ToArray();
        await Assert.That(bytes).IsEquivalentTo(new byte[] { 99, 88, 77, });
    }

    [Test]
    public async Task RequestResponse_StreamResponse_ReturnsHandlerStream()
    {
        await using var server = await TestEventBusServer.StartAsync();
        await using var client = await EventBusClient.ConnectAsync(server.Address, NullLogger.Instance);

        var expectedData = new byte[40_000];
        new Random(123).NextBytes(expectedData);

        await using var handler = await client.AddRequestHandlerAsync("rpc_stream_queue", (req, ct) =>
        {
            return Task.FromResult<MessagePayload?>(new MessagePayload(new MemoryStream(expectedData)));
        }, ex => Task.CompletedTask);

        await Task.Delay(100);

        var requestStream = new MemoryStream([1, 2, 3,]);
        var responsePayload = await client.RequestAsync("rpc_stream_queue", "stream_req", requestStream);

        await Assert.That(responsePayload).IsNotNull();
        var responseStream = (Stream)responsePayload!.Value.Value!;
        using var ms = new MemoryStream();
        await responseStream.CopyToAsync(ms);

        await Assert.That(ms.ToArray()).IsEquivalentTo(expectedData);
    }

    [Test]
    public async Task RequestResponse_NoHandlers_ReturnsNull()
    {
        await using var server = await TestEventBusServer.StartAsync();
        await using var client = await EventBusClient.ConnectAsync(server.Address, NullLogger.Instance);

        var responsePayload = await client.RequestAsync("non_existent_queue", "type", "data");

        await Assert.That(responsePayload).IsNull();
    }

    [Test]
    public async Task RequestResponse_HandlerThrowsException_PropagatesToRequester()
    {
        await using var server = await TestEventBusServer.StartAsync();
        await using var client = await EventBusClient.ConnectAsync(server.Address, NullLogger.Instance);

        var errorTcs = new TaskCompletionSource<Exception>();

        await using var handler = await client.AddRequestHandlerAsync("error_queue", (req, ct) =>
        {
            throw new InvalidOperationException("Handler processing failed");
        }, ex =>
        {
            if (ex is not null)
            {
                errorTcs.TrySetResult(ex);
            }

            return Task.CompletedTask;
        });

        await Task.Delay(100);

        var action = () => client.RequestAsync("error_queue", "ping", "data");
        await Assert.That(action).Throws<Exception>();

        var caughtHandlerException = await errorTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(caughtHandlerException.Message).IsEqualTo("Handler processing failed");
    }

    [Test]
    public async Task RequestResponse_StreamHandlerThrowsException_PropagatesToRequester()
    {
        await using var server = await TestEventBusServer.StartAsync();
        await using var client = await EventBusClient.ConnectAsync(server.Address, NullLogger.Instance);

        var errorTcs = new TaskCompletionSource<Exception>();

        await using var handler = await client.AddRequestHandlerAsync("error_stream_queue", (req, ct) =>
        {
            throw new InvalidOperationException("Stream handler failed");
        }, ex =>
        {
            if (ex is not null)
            {
                errorTcs.TrySetResult(ex);
            }

            return Task.CompletedTask;
        });

        await Task.Delay(100);

        using var requestStream = new MemoryStream([1, 2, 3,]);
        var action = () => client.RequestAsync("error_stream_queue", "stream_req", requestStream);
        await Assert.That(action).Throws<Exception>();

        var caughtHandlerException = await errorTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(caughtHandlerException.Message).IsEqualTo("Stream handler failed");
    }
}
