using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Solace.AuthServer.Utils;
using Solace.Db.Earth;
using Solace.EventBus.Client;
using Solace.StaticData;
using Solace.WebPortal.Common;

namespace Solace.WebPortal.Features.Seasons.Data;

[Handler]
[MapPost("data/update")]
[MapGroup<SeasonsGroup>]
[Authorize(Policy = Permissions.EditSeasonsAndChallenges)]
public static partial class UpdateData
{
    public sealed record Command;

    private static async ValueTask<Ok> HandleAsync(
        Command? _,
        EarthDbContext earthDb,
        EventBusClient eventBus,
        StaticDataProvider staticData,
        CancellationToken cancellationToken
    )
    {
        await DataSeedUtils.SeedSeasonsDataAsync(earthDb, staticData.Seasons, update: true, force: false, cancellationToken);

        await eventBus.PublishAsync("seasons", "seasons_data_updated", string.Empty, cancellationToken);
        return TypedResults.Ok();
    }
}
