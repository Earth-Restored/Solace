using Solace.Common.Asp.Auth;

namespace Solace.AuthServer.Features.XboxLive;

public sealed class TitleToken : AuthToken, ITokenData<TitleToken>
{
    public required string Tid { get; init; }
}