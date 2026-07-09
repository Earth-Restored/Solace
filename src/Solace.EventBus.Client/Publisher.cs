using Solace.EventBus;

namespace Solace.EventBus.Client;

public sealed class Publisher : IAsyncDisposable
{
    private readonly EventBusService.EventBusServiceClient _client;

    internal Publisher(EventBusService.EventBusServiceClient client)
    {
        _client = client;
    }

    public Task<bool> PublishAsync(string queueName, string type, string data)
        => _client.PublishAsync(new PublishRequest { QueueName = queueName, Type = type, Data = data, })
            .ResponseAsync
            .ContinueWith(task => task.Result.Success, TaskContinuationOptions.ExecuteSynchronously);

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}