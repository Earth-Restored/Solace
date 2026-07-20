using System.Text.Json.Serialization;
using Solace.AuthServer.Features.Common;
using Solace.Common.Asp.Auth;

namespace Solace.AuthServer.Features.XboxLive;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(DeviceToken), "device")]
[JsonDerivedType(typeof(TitleToken), "title")]
[JsonDerivedType(typeof(UserToken), "user")]
public abstract class AuthToken : ITokenData<AuthToken>
{
}