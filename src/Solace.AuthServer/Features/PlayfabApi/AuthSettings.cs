namespace Solace.AuthServer.Features.PlayfabApi;

public sealed class AuthSettings
{
    public required int EntityTokenValidityMinutes { get; init; }

    public required int SessionTicketValidityMinutes { get; init; }
}
