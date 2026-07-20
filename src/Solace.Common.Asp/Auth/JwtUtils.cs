using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Solace.Common;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using BitcoderCZ.Utils;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Solace.Common.Asp.Auth;

public static partial class JwtUtils
{
    private static readonly JwtSecurityTokenHandler jwtHandler = new JwtSecurityTokenHandler();

    private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private const string KeyId = "solace-kid-v1";

    public static string Sign<TData>(Token<TData> token, byte[] secret)
        where TData : ITokenData<TData>
        => SignInternal<TData>(token, secret, new ValidityDatePair(token.Issued, token.Expires));

    public static string Sign<TData>(TData data, byte[] secret, ValidityDatePair validity)
        where TData : ITokenData<TData>
        => SignInternal<TData>(data, secret, validity);

    public static string Sign<TData>(Token<TData> token, ImmutableArray<byte> secret)
        where TData : ITokenData<TData>
        => SignInternal<TData>(token, ImmutableCollectionsMarshal.AsArray(secret)!, new ValidityDatePair(token.Issued, token.Expires));

    public static string Sign<TData>(TData data, ImmutableArray<byte> secret, ValidityDatePair validity)
        where TData : ITokenData<TData>
        => SignInternal<TData>(data, ImmutableCollectionsMarshal.AsArray(secret)!, validity);

    private static string SignInternal<TData>(object dataOrToken, byte[] secret, ValidityDatePair validity)
        where TData : ITokenData<TData>
    {
        ThrowHelper.ThrowIfNull(dataOrToken);
        ThrowHelper.ThrowIfNull(secret);

        TData data = dataOrToken switch
        {
            Token<TData> token => token.Data,
            TData tokenData => tokenData,
            _ => throw new UnreachableException(),
        };

        Claim[] payload =
        [
            new Claim("iat", validity.Issued.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new Claim("nbf", validity.Issued.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new Claim("exp", validity.Expires.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new Claim("data", JsonSerializer.Serialize(data, jsonOptions)),
        ];

        var signingKey = new SymmetricSecurityKey(secret)
        {
            KeyId = KeyId,
        };

        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        return jwtHandler.WriteToken(new JwtSecurityToken(
            new JwtHeader(credentials),
            new JwtPayload(payload)
        ));
    }

    public static Token<TData>? Verify<TData>(string token, ImmutableArray<byte> secret, ILogger logger, bool allowExpired = false)
        where TData : ITokenData<TData>
        => Verify<TData>(token, ImmutableCollectionsMarshal.AsArray(secret)!, logger, allowExpired);

    private static Token<TData>? Verify<TData>(string token, byte[] secret, ILogger logger, bool allowExpired = false)
        where TData : ITokenData<TData>
    {
        ThrowHelper.ThrowIfNull(token);
        ThrowHelper.ThrowIfNull(secret);

        try
        {
            var signingKey = new SymmetricSecurityKey(secret)
            {
                KeyId = KeyId,
            };

            var claims = jwtHandler.ValidateToken(token, new TokenValidationParameters()
            {
#pragma warning disable CA5404 // Do not disable token validation checks todo
                ValidateIssuer = false,
                ValidateAudience = false,
#pragma warning restore CA5404 // Do not disable token validation checks
                ValidateLifetime = !allowExpired,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
            }, out _).Claims.ToDictionary(claim => claim.Type, claim => claim.Value);

            if (!claims.TryGetValue("iat", out var iat) || !claims.TryGetValue("exp", out var exp) || !claims.TryGetValue("data", out var dataJson))
            {
                return null;
            }

            if (!long.TryParse(iat, CultureInfo.InvariantCulture, out var issuedSeconds) || !long.TryParse(exp, CultureInfo.InvariantCulture, out var expiresSeconds))
            {
                return null;
            }

            var expires = DateTimeOffset.FromUnixTimeSeconds(expiresSeconds);

            var data = JsonSerializer.Deserialize<TData>(dataJson);
            if (data is null)
            {
                return null;
            }

            return new Token<TData>(DateTimeOffset.FromUnixTimeSeconds(issuedSeconds), expires, allowExpired && expires < DateTimeOffset.UtcNow, data);
        }
        catch (SecurityTokenArgumentException exception)
        {
            LogJwtVerificationFail(logger, exception);
            return null;
        }
        catch (SecurityTokenException exception)
        {
            LogJwtVerificationFail(logger, exception);
            return null;
        }
        catch (JsonException exception)
        {
            LogJwtDataDeserializationFail(logger, exception);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "JWT verification failed")]
    private static partial void LogJwtVerificationFail(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "JWT data deserialization failed")]
    private static partial void LogJwtDataDeserializationFail(ILogger logger, Exception exception);
}
