namespace Solace.AuthServer.Features.Common;

public sealed record Token<TData>(
    DateTimeOffset Issued,
    DateTimeOffset Expires,
    bool? Expired,
    TData Data
) where TData : ITokenData<TData>;