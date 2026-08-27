using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Solace.ApiServer.Models;
using Solace.Common;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using BitcoderCZ.Utils;

namespace Solace.ApiServer.Utils;

internal static class JwtUtils
{
    private static readonly JwtSecurityTokenHandler jwtHandler = new JwtSecurityTokenHandler();

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
            new Claim("iat", validity.Issued.ToUnixTimeSeconds().ToString()),
            new Claim("nbf", validity.Issued.ToUnixTimeSeconds().ToString()),
            new Claim("exp", validity.Expires.ToUnixTimeSeconds().ToString()),
            new Claim("data", Json.Serialize(data)),
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

    public static Token<TData>? Verify<TData>(string token, ImmutableArray<byte> secret, bool allowExpired = false)
        where TData : ITokenData<TData>
        => Verify<TData>(token, ImmutableCollectionsMarshal.AsArray(secret)!, allowExpired);

    public static Token<TData>? Verify<TData>(string token, byte[] secret, bool allowExpired = false)
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
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = !allowExpired,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
            }, out _).Claims.ToDictionary(claim => claim.Type, claim => claim.Value);

            if (!claims.TryGetValue("iat", out string? iat) || !claims.TryGetValue("exp", out string? exp) || !claims.TryGetValue("data", out string? dataJson))
            {
                return null;
            }

            if (!long.TryParse(iat, out long issuedSeconds) || !long.TryParse(exp, out long expiresSeconds))
            {
                return null;
            }

            var expires = DateTimeOffset.FromUnixTimeSeconds(expiresSeconds);

            var data = Json.Deserialize<TData>(dataJson);
            if (data is null)
            {
                return null;
            }

            return new Token<TData>(DateTimeOffset.FromUnixTimeSeconds(issuedSeconds), expires, allowExpired && expires < DateTimeOffset.UtcNow, data);
        }
        catch (SecurityTokenException ex)
        {
            Log.Debug($"JWT verification failed: {ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            Log.Debug($"JWT data deserialization failed: {ex.Message}");
            return null;
        }
    }
}
