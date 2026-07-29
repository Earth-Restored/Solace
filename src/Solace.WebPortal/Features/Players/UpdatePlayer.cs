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
[MapPut("{id}")]
[MapGroup<PlayersGroup>]
[Authorize(Policy = Permissions.ManagePlayers)]
public static partial class UpdatePlayer
{
    public sealed record Command([property: FromRoute] Guid Id, [property: FromBody] UpdatePlayerCommand Body);

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

            if (await earthDb.Accounts
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

        var account = await earthDb.Accounts
            .AsTracking()
            .Include(account => account.Profile)
            .Include(account => account.Boosts)
            .FirstOrDefaultAsync(account => account.Id == command.Id, cancellationToken);

        if (account is null)
        {
            return TypedResults.NotFound();
        }

        Debug.Assert(account.Profile is not null);
        Debug.Assert(account.Boosts is not null);

        if ((long)(command.Body.PurchasedRubies ?? account.Profile.Rubies.Purchased) + (command.Body.EarnedRubies ?? account.Profile.Rubies.Earned) > int.MaxValue)
        {
            return TypedResults.BadRequest("Too many rubies.");
        }

        if (command.Body.Username is not null)
        {
            account.Username = command.Body.Username;
        }

        if (command.Body.Health is { } health)
        {
            var maxHealth = BoostUtils.GetMaxPlayerHealth(account.Boosts, DateTimeOffset.UtcNow, staticData.Catalog.ItemsCatalog);

            if (health > maxHealth)
            {
                return TypedResults.BadRequest($"Health cannot be larger than {maxHealth}.");
            }

            account.Profile.Health = health;
        }

        if (command.Body.PurchasedRubies is { } purchasedRubies)
        {
            account.Profile.Rubies.Purchased = purchasedRubies;
        }

        if (command.Body.EarnedRubies is { } earnedRubies)
        {
            account.Profile.Rubies.Earned = earnedRubies;
        }

        await earthDb.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok();
    }
}
