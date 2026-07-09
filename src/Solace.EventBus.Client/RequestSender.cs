namespace Solace.EventBus.Client;

public sealed class RequestSender : IAsyncDisposable
{
    private readonly EventBusService.EventBusServiceClient _client;
    internal RequestSender(EventBusService.EventBusServiceClient client)
    {
        _client = client;
    }

    public Task<string?> RequestAsync(string queueName, string type, string data)
        => _client.RequestAsync(new RequestMessage { QueueName = queueName, Type = type, Data = data, })
            .ResponseAsync
            .ContinueWith(task =>
            {
                return task.Result.Status switch
                {
                    ResponseMessageStatus.Success => task.Result.Data,
                    ResponseMessageStatus.NoHandlers => null,
#pragma warning disable CA2201 // Do not raise reserved exception types
                    _ => throw new Exception(task.Result.ErrorMessage),
#pragma warning restore CA2201 // Do not raise reserved exception types
                };
            }, TaskContinuationOptions.ExecuteSynchronously);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
