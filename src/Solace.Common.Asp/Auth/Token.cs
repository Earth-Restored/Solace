namespace Solace.Common.Asp.Auth;

public sealed record Token<TData>(
    DateTimeOffset Issued,
    DateTimeOffset Expires,
    bool? Expired,
    TData Data
) where TData : ITokenData<TData>;