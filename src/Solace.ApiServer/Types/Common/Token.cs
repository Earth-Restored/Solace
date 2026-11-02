namespace Solace.ApiServer.Types.Common;

internal sealed record Token(
    TokenType ClientType,
    Dictionary<string, object> ClientProperties,
    Rewards Rewards,
    TokenLifetime Lifetime
);
