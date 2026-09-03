using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Solace.AuthServer.Features.XboxLive;
using Solace.Common.Asp;
using Solace.Common.Asp.Auth;
using Solace.Db.Earth;

namespace Solace.IntegrationTests;

// todo: validate DisplayClaims
public sealed partial class LoginTests
{
    [Test]
    public async Task Login_XboxLive_TitleMtg_Default(CancellationToken cancellationToken)
    {
        using var response = await _authServerClient.GetAsync("/title.mgt.xboxlive.com/titles/default/endpoints?type=1", cancellationToken);

        await Assert.That(response).IsOk();

        var result = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);

        await Assert.That(result).IsNotNull();

        var expectedResult = JsonSerializer.Deserialize<JsonObject>($$"""
            {
                "EndPoints": [
                    {
                        "Protocol": "http",
                        "Host": "localhost",
                        "Port": {{_authServerClient.BaseAddress!.Port}},
                        "HostType": "fqdn",
                        "RelyingParty": "http://xboxlive.com",
                        "TokenType": "JWT"
                    },
                    {
                        "Protocol": "https",
                        "Host": "xboxlive.com",
                        "HostType": "fqdn",
                        "RelyingParty": "http://xboxlive.com",
                        "TokenType": "JWT"
                    }
                ]
            }    
            """);

        await Assert.That(JsonNode.DeepEquals(result, expectedResult)).IsTrue();
    }

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

        // todo: test both RelyingParties
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

    [Test]
    public async Task Login_XboxLive_TitleMgt_2037747551(CancellationToken cancellationToken)
    {
        await using var earthDb = EarthDbContext.CreateFromConnection(_earthConnectionString);

        var cryptoSecrets = await earthDb.GetOrInitializeSecretsAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "/title.mgt.xboxlive.com/titles/2037747551/endpoints");
        AddXBLAuth(request, cryptoSecrets);

        using var response = await _authServerClient.SendAsync(request, cancellationToken);

        await Assert.That(response).IsOk();

        var result = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);

        await Assert.That(result).IsNotNull();

        var expectedResult = JsonSerializer.Deserialize<JsonObject>("""
            {
                "EndPoints": [
                    {
                        "Protocol": "https",
                        "Host": "*.playfabapi.com",
                        "HostType": "wildcard",
                        "RelyingParty": "https://b980a380.minecraft.playfabapi.com/",
                        "TokenType": "JWT"
                    },
                    {
                        "Protocol": "https",
                        "Host": "*.commerce.gameservices.com",
                        "HostType": "wildcard",
                        "RelyingParty": "https://minecraft.commerce.microsoftstudios.com/",
                        "TokenType": "JWT"
                    },
                    {
                        "Protocol": "http",
                        "Host": "*",
                        "HostType": "wildcard"
                    },
                    {
                        "Protocol": "https",
                        "Host": "*",
                        "HostType": "wildcard"
                    }
                ]
            }
            """);

        await Assert.That(JsonNode.DeepEquals(result, expectedResult)).IsTrue();
    }

    [Test]
    public async Task Login_XboxLive_GetProfile(CancellationToken cancellationToken)
    {
        await using var earthDb = EarthDbContext.CreateFromConnection(_earthConnectionString);

        var cryptoSecrets = await earthDb.GetOrInitializeSecretsAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "/accounts.xboxlive.com/users/current/profile");
        AddXBLAuth(request, cryptoSecrets);

        using var response = await _authServerClient.SendAsync(request, cancellationToken);

        await Assert.That(response).IsOk();

        var result = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);

        await Assert.That(result).IsNotNull();

        await Assert.That(result["gamerTag"]).IsJsonValue();
        await Assert.That(result["touAcceptanceDate"]).IsJsonValue();
        await Assert.That(result["dateOfBirth"]).IsJsonValue();
        await Assert.That(result["dateCreated"]).IsJsonValue();
        await Assert.That(result["userHash"]).IsJsonValue();
        await Assert.That(result["userXuid"]).IsJsonValue();
    }

    [Test]
    public async Task Login_XboxLive_GetProfileSettings(CancellationToken cancellationToken)
    {
        await using var earthDb = EarthDbContext.CreateFromConnection(_earthConnectionString);

        var cryptoSecrets = await earthDb.GetOrInitializeSecretsAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "/profile.xboxlive.com/users/me/profile/settings?settings=GameDisplayPicRaw,Gamerscore,Gamertag,FirstName,LastName");
        AddXBLAuth(request, cryptoSecrets);

        using var response = await _authServerClient.SendAsync(request, cancellationToken);

        await Assert.That(response).IsOk();

        var result = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);

        await Assert.That(result).IsNotNull();

        var profile = (result["profileUsers"] as JsonArray)?[0] as JsonObject;

        await Assert.That(profile).IsNotNull();

        await Assert.That(profile["id"].GetValue<string>).IsEqualTo(_profileId.ToString());

        var settings = profile["settings"] as JsonArray;

        await Assert.That(settings).IsNotNull();

        var gamerTag = settings.FirstOrDefault(node =>
            node is JsonObject nodeObject &&
            nodeObject["id"] is JsonValue id &&
            id.GetValue<string>() is "Gamertag");

        await Assert.That(gamerTag).IsNotNull();

        await Assert.That(gamerTag["value"]!.GetValue<string>()).IsEqualTo(ProfileUsername);
    }

    private void AddXBLAuth(HttpRequestMessage request, CryptoSecrets cryptoSecrets)
    {
        var jwt = JwtUtils.Sign(new XapiToken(_profileId, ProfileUsername), cryptoSecrets.LiveXapiTokenSecret, ValidityDatePair.Create(5));

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("XBL3.0", $"x={_profileId};{jwt}");
    }
}
