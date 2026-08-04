namespace Solace.EventBus.Client;

public readonly union MessagePayload(string, ReadOnlyMemory<byte>, Stream);
