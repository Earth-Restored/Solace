namespace Solace.ApiServer.Types.Common;

internal sealed record Token(
    TokenType ClientType,
    Dictionary<string, string> ClientProperties,
    Rewards Rewards,
    TokenLifetime Lifetime
);
