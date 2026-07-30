using System.Diagnostics;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Common.Constants;
using Solace.Common.Utils;
using Solace.Db.Earth;
using Solace.StaticData;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Players;

namespace Solace.WebPortal.Features.Players;

[Handler]
[MapPut("{profileId}")]
[MapGroup<PlayersGroup>]
[Authorize(Policy = Permissions.ManagePlayers)]
public static partial class UpdatePlayer
{
    public sealed record Command([property: FromRoute] Guid ProfileId, [property: FromBody] UpdatePlayerCommand Body);

    private static async ValueTask<Results<Ok, BadRequest<string>, NotFound>> HandleAsync(
        Command command,
        EarthDbContext earthDb,
        StaticDataProvider staticData,
        CancellationToken cancellationToken
    )
    {
        if (command.Body.Username is { } username)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Length < AccountConstants.UsernameLengthMin || username.Length > AccountConstants.UsernameLengthMax)
            {
                return TypedResults.BadRequest($"Username must be {AccountConstants.UsernameLengthMin}-{AccountConstants.UsernameLengthMax} characters long.");
            }

            if (!AccountConstants.GetUsernameRegex().IsMatch(username))
            {
                return TypedResults.BadRequest($"Username must contain only: {AccountConstants.UsernameAllowedCharacters}");
            }

            if (await earthDb.Profiles
                .AnyAsync(account => account.Username == username, cancellationToken))
            {
                return TypedResults.BadRequest("Account with the specified username already exists");
            }
        }

        if (command.Body.Health < 0)
        {
            return TypedResults.BadRequest("Health cannot be negative.");
        }

        if (command.Body is { PurchasedRubies: < 0 } or { EarnedRubies: < 0 })
        {
            return TypedResults.BadRequest("Rubies cannot be negative.");
        }

        var profile = await earthDb.Profiles
            .AsTracking()
            .Include(profile => profile.Boosts)
            .FirstOrDefaultAsync(profile => profile.Id == command.ProfileId, cancellationToken);

        if (profile is null)
        {
            return TypedResults.NotFound();
        }

        Debug.Assert(profile.Boosts is not null);

        if ((long)(command.Body.PurchasedRubies ?? profile.Rubies.Purchased) + (command.Body.EarnedRubies ?? profile.Rubies.Earned) > int.MaxValue)
        {
            return TypedResults.BadRequest("Too many rubies.");
        }

        if (command.Body.Username is not null)
        {
            profile.Username = command.Body.Username;
        }

        if (command.Body.Health is { } health)
        {
            var maxHealth = BoostUtils.GetMaxPlayerHealth(profile.Boosts, DateTimeOffset.UtcNow, staticData.Catalog.ItemsCatalog);

            if (health > maxHealth)
            {
                return TypedResults.BadRequest($"Health cannot be larger than {maxHealth}.");
            }

            profile.Health = health;
        }

        if (command.Body.PurchasedRubies is { } purchasedRubies)
        {
            profile.Rubies.Purchased = purchasedRubies;
        }

        if (command.Body.EarnedRubies is { } earnedRubies)
        {
            profile.Rubies.Earned = earnedRubies;
        }

        await earthDb.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok();
    }
}
