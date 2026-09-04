using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Solace.AuthServer.Features.Common;
using Solace.AuthServer.Features.PlayfabApi;
using Solace.Common.Asp;
using Solace.Common.Asp.Auth;
using Solace.Db.Earth;

namespace Solace.IntegrationTests;

public sealed partial class LoginTests
{
    [Test]
    public async Task Login_Playfab_LoginWithXbox(CancellationToken cancellationToken)
    {
        await using var earthDb = EarthDbContext.CreateFromConnection(_fixture.EarthConnectionString);

        var cryptoSecrets = await earthDb.GetOrInitializeSecretsAsync();

        var xboxToken = JwtUtils.Sign(new PlayfabXboxToken(_fixture.ProfileId), cryptoSecrets.LivePlayfabTokenSecret, ValidityDatePair.Create(5));

        using var respones = await _fixture.AuthServerClient.PostAsync("/20CA2.playfabapi.com/Client/LoginWithXbox?sdk=XPlatCppSdk-3.11.190520", new StringContent($$"""
            {
                "CreateAccount": true,
                "EncryptedRequest": null,
                "InfoRequestParameters": {
                    "GetCharacterInventories": false,
                    "GetCharacterList": false,
                    "GetPlayerProfile": true,
                    "GetPlayerStatistics": false,
                    "GetTitleData": false,
                    "GetUserAccountInfo": true,
                    "GetUserData": false,
                    "GetUserInventory": false,
                    "GetUserReadOnlyData": false,
                    "GetUserVirtualCurrency": false,
                    "PlayerStatisticNames": null,
                    "ProfileConstraints": null,
                    "TitleDataKeys": null,
                    "UserDataKeys": null,
                    "UserReadOnlyDataKeys": null
                },
                "PlayerSecret": null,
                "TitleId": "20CA2",
                "XboxToken": "XBL3.0 x={{_fixture.ProfileId}};{{xboxToken}}"
            }
            """, Encoding.UTF8, "application/json"), cancellationToken);

        var data = await ValidatePlayfabResponse(respones, cancellationToken);

        var sessionTicket = data["SessionTicket"]?.GetValue<string>();

        await Assert.That(sessionTicket).IsNotNull();

        var dashIndex = -1;

        for (var i = 0; i < 5; i++)
        {
            dashIndex = sessionTicket.IndexOf('-', dashIndex + 1);
        }

        await Assert.That(sessionTicket[..dashIndex]).WhenParsedInto<Guid>().IsEqualTo(_fixture.ProfileId);

        await Assert.That(JwtUtils.Verify<PlayfabSessionTicket>(sessionTicket[(dashIndex + 1)..], cryptoSecrets.PlayfabSessionTicketSecret, NullLogger.Instance, allowExpired: false)).IsNotNull();

        var entityTokenObject = data["EntityToken"] as JsonObject;

        await Assert.That(entityTokenObject).IsNotNull();

        var entityToken = entityTokenObject["EntityToken"]?.GetValue<string>();

        await Assert.That(entityToken).IsNotNull();

        await Assert.That(JwtUtils.Verify<EntityToken>(entityToken, cryptoSecrets.PlayfabEntityTokenSecret, NullLogger.Instance, allowExpired: false)).IsNotNull();

        var entityTokenExpiration = entityTokenObject["TokenExpiration"]?.GetValue<string>();

        await Assert.That(entityTokenExpiration).WhenParsedInto<DateTime>().IsAfter(DateTime.UtcNow);
    }

    [Test]
    public async Task Login_Playfab_GetEntityToken(CancellationToken cancellationToken)
    {
        await using var earthDb = EarthDbContext.CreateFromConnection(_fixture.EarthConnectionString);

        var cryptoSecrets = await earthDb.GetOrInitializeSecretsAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/20CA2.playfabapi.com/Authentication/GetEntityToken?sdk=XPlatCppSdk-3.11.190520");
        AddPlayfabAuth(request, cryptoSecrets);
        request.Content = new StringContent($$"""
            {
                "Entity": {
                    "Id": "{{_fixture.ProfileId}}",
                    "Type": "master_player_account"
                }
            }
            """, Encoding.UTF8, "application/json");

        using var response = await _fixture.AuthServerClient.SendAsync(request, cancellationToken);

        var result = await ValidatePlayfabResponse(response, cancellationToken);

        await Assert.That(result).IsNotNull();

        var notAfter = result["TokenExpiration"]?.GetValue<DateTime>();

        await Assert.That(notAfter)
            .IsNotNull()
            .And.IsAfter(DateTime.UtcNow);

        var token = result["EntityToken"]?.GetValue<string>();

        await Assert.That(token).IsNotNullOrWhiteSpace();

        await Assert.That(JwtUtils.Verify<EntityToken>(token!, cryptoSecrets.PlayfabEntityTokenSecret, NullLogger.Instance, allowExpired: false)).IsNotNull();
    }

    [Test]
    public async Task Login_Playfab_GetUserPublisherData(CancellationToken cancellationToken)
    {
        await using var earthDb = EarthDbContext.CreateFromConnection(_fixture.EarthConnectionString);

        var cryptoSecrets = await earthDb.GetOrInitializeSecretsAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/20CA2.playfabapi.com/Client/GetUserPublisherData");

        var jwt = JwtUtils.Sign(new PlayfabSessionTicket(_fixture.ProfileId), cryptoSecrets.PlayfabSessionTicketSecret, ValidityDatePair.Create(5));

        request.Headers.Add("X-Authorization", $"{_fixture.ProfileId}-{jwt}");

        request.Content = new StringContent($$"""
            {
                "Entity": {
                    "Id": "{{_fixture.ProfileId}}",
                    "Type": "master_player_account"
                },
                "Keys": [
                    "PlayFabCommerceEnabled"
                ]
            }
            """, Encoding.UTF8, "application/json");

        using var response = await _fixture.AuthServerClient.SendAsync(request, cancellationToken);

        var result = await ValidatePlayfabResponse(response, cancellationToken);

        await Assert.That(result).IsNotNull();

        await Assert.That(result["DataVersion"]?.GetValue<int>()).IsNotNull();

        var playfabCommerce = result["Data"]?["PlayFabCommerceEnabled"] as JsonObject;

        await Assert.That(playfabCommerce).IsNotNull();

        await Assert.That(playfabCommerce["Value"]?.GetValue<string>()).IsEqualTo("true");

        await Assert.That(playfabCommerce["Permission"]?.GetValue<string>()).IsEqualTo("Public");
    }

    private void AddPlayfabAuth(HttpRequestMessage request, CryptoSecrets cryptoSecrets)
    {
        var jwt = JwtUtils.Sign(new EntityToken(_fixture.ProfileId, "title_player_account"), cryptoSecrets.PlayfabEntityTokenSecret, ValidityDatePair.Create(5));

        request.Headers.Add("X-EntityToken", jwt);
    }

    private static async Task<JsonObject> ValidatePlayfabResponse(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);

        await Assert.That(json).IsNotNull();

        await Assert.That(json["code"]?.GetValue<int>()).IsEqualTo(200);
        await Assert.That(json["status"]?.GetValue<string>()).IsEqualTo("OK");

        var data = json["data"];

        await Assert.That(data).IsJsonObject();

        return (JsonObject)data!;
    }
}
