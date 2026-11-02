using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Common;
using Solace.Db.Earth.Models.Player;

namespace Solace.IntegrationTests;

public sealed partial class SolaceIntegrationTests
{
    [Test]
    public async Task Tokens_Get_ReturnsTokens(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        using var response = await client.GetAsync("/1/api/v1.1/player/tokens", cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonObject;
        await Assert.That(result).IsNotNull();

        var tokens = (result!["tokens"] ?? result["Tokens"]) as JsonObject;
        await Assert.That(tokens).IsNotNull();
    }

    [Test]
    public async Task Tokens_Redeem_InvalidTokenId_ReturnsBadRequest(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        var randomTokenId = Guid.NewGuid();

        using var response = await client.PostAsync($"/1/api/v1.1/player/tokens/{randomTokenId}/redeem", null, cancellationToken);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Tokens_Redeem_ValidLevelUpToken_GivesRewardsAndDeletesToken(CancellationToken cancellationToken)
    {
        Guid testTokenId;
        await using (var earthDb = EarthDbContext.CreateFromConnection(_fixture.EarthConnectionString))
        {
            var rewards = new Rewards(
                Rubies: 5,
                ExperiencePoints: 0,
                Level: 2,
                Items: [],
                Buildplates: [],
                Challenges: [],
                PersonaItems: []
            );

            var token = new LevelUpTokenEF(_fixture.ProfileId, 2, rewards);
            earthDb.Tokens.Add(token);
            await earthDb.SaveChangesAsync(cancellationToken);

            testTokenId = token.TokenId;
        }

        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        using var response = await client.PostAsync($"/1/api/v1.1/player/tokens/{testTokenId}/redeem", null, cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonObject;
        await Assert.That(result).IsNotNull();

        // verify that token was removed
        await using (var earthDb = EarthDbContext.CreateFromConnection(_fixture.EarthConnectionString))
        {
            var exists = await earthDb.Tokens.AnyAsync(t => t.TokenId == testTokenId, cancellationToken);
            await Assert.That(exists).IsFalse();
        }
    }
}
