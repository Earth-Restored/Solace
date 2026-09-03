using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Solace.AuthServer.Features.XboxLive;
using Solace.Common.Asp;
using Solace.Common.Asp.Auth;
using Solace.Db.Earth;

namespace Solace.IntegrationTests;

public sealed partial class LoginTests
{
    [Test]
    public async Task Login_XboxLive_UserAuth(CancellationToken cancellationToken)
    {
        await using var earthDb = EarthDbContext.CreateFromConnection(_earthConnectionString);

        var cryptoSecrets = await earthDb.GetOrInitializeSecretsAsync();

        var ticket = JwtUtils.Sign(new AuthServer.Features.Common.XboxTicketToken(_profileId, ProfileUsername), cryptoSecrets.LoginXboxTokenSecret, ValidityDatePair.Create(5));

        // currently, only RpsTicket is used by the server
        using var response = await _authServerClient.PostAsync("/user.auth.xboxlive.com/user/authenticate", new StringContent($$"""
            {
                "Properties": {
                    "AuthMethod": "RPS",
                    "ProofKey": {
                    "alg": "ES256",
                    "crv": "P-256",
                    "kty": "EC",
                    "use": "sig",
                    "x": "xxx",
                    "y": "xxx"
                    },
                    "RpsTicket": "{{ticket}}",
                    "SiteName": "user.auth.xboxlive.com"
                },
                "RelyingParty": "http://auth.xboxlive.com",
                "TokenType": "JWT"
            }
            """, Encoding.UTF8, "application/json"), cancellationToken);

        await Assert.That(response).IsOk();

        var responseJson = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);

        await Assert.That(responseJson).IsNotNull();

        var notAfter = responseJson["NotAfter"]?.GetValue<DateTime>();

        await Assert.That(notAfter)
            .IsNotNull()
            .And.IsAfter(DateTime.UtcNow);

        var token = responseJson["Token"]?.GetValue<string>();

        await Assert.That(token).IsNotNullOrWhiteSpace();

        await Assert.That(JwtUtils.Verify<AuthToken>(token!, cryptoSecrets.LiveAuthTokenSecret, NullLogger.Instance, allowExpired: false)).IsNotNull();
    }

    [Test]
    public async Task Login_XboxLive_DeviceAuth(CancellationToken cancellationToken)
    {
        await using var earthDb = EarthDbContext.CreateFromConnection(_earthConnectionString);

        var cryptoSecrets = await earthDb.GetOrInitializeSecretsAsync();

        var ticket = JwtUtils.Sign(new AuthServer.Features.Common.XboxTicketToken(_profileId, ProfileUsername), cryptoSecrets.LoginXboxTokenSecret, ValidityDatePair.Create(5));

        // currently, only RpsTicket is used by the server
        using var response = await _authServerClient.PostAsync("/device.auth.xboxlive.com/device/authenticate", new StringContent($$"""
            {
                "Properties": {
                    "AuthMethod": "RPS",
                    "ProofKey": {
                    "alg": "ES256",
                    "crv": "P-256",
                    "kty": "EC",
                    "use": "sig",
                    "x": "xxx",
                    "y": "xxx"
                    },
                    "RpsTicket": "{{ticket}}",
                    "SiteName": "user.auth.xboxlive.com"
                },
                "RelyingParty": "http://auth.xboxlive.com",
                "TokenType": "JWT"
            }
            """, Encoding.UTF8, "application/json"), cancellationToken);

        await Assert.That(response).IsOk();

        var responseJson = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);

        await Assert.That(responseJson).IsNotNull();

        var notAfter = responseJson["NotAfter"]?.GetValue<DateTime>();

        await Assert.That(notAfter)
            .IsNotNull()
            .And.IsAfter(DateTime.UtcNow);

        var token = responseJson["Token"]?.GetValue<string>();

        await Assert.That(token).IsNotNullOrWhiteSpace();

        await Assert.That(JwtUtils.Verify<AuthToken>(token!, cryptoSecrets.LiveAuthTokenSecret, NullLogger.Instance, allowExpired: false)).IsNotNull();
    }

    [Test]
    public async Task Login_XboxLive_TitleAuth(CancellationToken cancellationToken)
    {
        await using var earthDb = EarthDbContext.CreateFromConnection(_earthConnectionString);

        var cryptoSecrets = await earthDb.GetOrInitializeSecretsAsync();

        var deviceToken = JwtUtils.Sign(new DeviceToken()
        {
            Did = "F700F376F3793B3A", // TODO: implement
        }, cryptoSecrets.LiveAuthTokenSecret, ValidityDatePair.Create(5));

        var ticket = JwtUtils.Sign(new AuthServer.Features.Common.XboxTicketToken(_profileId, ProfileUsername), cryptoSecrets.LoginXboxTokenSecret, ValidityDatePair.Create(5));

        // currently, only RpsTicket and DeviceToken is used by the server
        using var response = await _authServerClient.PostAsync("/title.auth.xboxlive.com/title/authenticate", new StringContent($$"""
            {
                "Properties": {
                    "AuthMethod": "RPS",
                    "DeviceToken": "{{deviceToken}}",
                    "ProofKey": {
                    "alg": "ES256",
                    "crv": "P-256",
                    "kty": "EC",
                    "use": "sig",
                    "x": "xxx",
                    "y": "xxx"
                    },
                    "RpsTicket": "{{ticket}}",
                    "SiteName": "user.auth.xboxlive.com"
                },
                "RelyingParty": "http://auth.xboxlive.com",
                "TokenType": "JWT"
            }
            """, Encoding.UTF8, "application/json"), cancellationToken);

        await Assert.That(response).IsOk();

        var responseJson = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);

        await Assert.That(responseJson).IsNotNull();

        var notAfter = responseJson["NotAfter"]?.GetValue<DateTime>();

        await Assert.That(notAfter)
            .IsNotNull()
            .And.IsAfter(DateTime.UtcNow);

        var token = responseJson["Token"]?.GetValue<string>();

        await Assert.That(token).IsNotNullOrWhiteSpace();

        await Assert.That(JwtUtils.Verify<TitleToken>(token!, cryptoSecrets.LiveAuthTokenSecret, NullLogger.Instance, allowExpired: false)).IsNotNull();
    }

    [Test]
    public async Task Login_XboxLive_XstsAuth(CancellationToken cancellationToken)
    {
        await using var earthDb = EarthDbContext.CreateFromConnection(_earthConnectionString);

        var cryptoSecrets = await earthDb.GetOrInitializeSecretsAsync();

        var titleToken = JwtUtils.Sign<AuthToken>(new TitleToken()
        {
            Tid = "2037747551",
        }, cryptoSecrets.LiveAuthTokenSecret, ValidityDatePair.Create(5));

        var userToken = JwtUtils.Sign<AuthToken>(new UserToken()
        {
            Xid = _profileId,
            Uhs = _profileId,

            UserId = _profileId,
            Username = ProfileUsername,
        }, cryptoSecrets.LiveAuthTokenSecret, ValidityDatePair.Create(5));

        var deviceToken = JwtUtils.Sign<AuthToken>(new DeviceToken()
        {
            Did = "F700F376F3793B3A", // TODO: implement
        }, cryptoSecrets.LiveAuthTokenSecret, ValidityDatePair.Create(5));

        using var response = await _authServerClient.PostAsync("/xsts.auth.xboxlive.com/xsts/authorize", new StringContent($$"""
            {
                "Properties": {
                    "DeviceToken": "{{deviceToken}}",
                    "SandboxId": "RETAIL",
                    "TitleToken": "{{titleToken}}",
                    "UserTokens": [
                        "{{userToken}}"
                    ]
                },
                "RelyingParty": "http://xboxlive.com",
                "TokenType": "JWT"
            }
            """, Encoding.UTF8, "application/json"), cancellationToken);

        await Assert.That(response).IsOk();

        var responseJson = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);

        await Assert.That(responseJson).IsNotNull();

        var notAfter = responseJson["NotAfter"]?.GetValue<DateTime>();

        await Assert.That(notAfter)
            .IsNotNull()
            .And.IsAfter(DateTime.UtcNow);

        var token = responseJson["Token"]?.GetValue<string>();

        await Assert.That(token).IsNotNullOrWhiteSpace();

        await Assert.That(JwtUtils.Verify<XapiToken>(token!, cryptoSecrets.LiveXapiTokenSecret, NullLogger.Instance, allowExpired: false)).IsNotNull();
    }
}
