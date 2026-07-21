using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Solace.DB.Earth;
using Solace.DB.Earth.Models.Player;
using Solace.StaticData;

namespace Solace.ApiServer.Utils;

internal sealed class Rewards
{
    private int _rubies;
    private int _experiencePoints;

    private int? _level;
    private readonly Dictionary<Guid, int> _items = [];
    private readonly HashSet<Guid> _buildplates = [];
    private readonly HashSet<string> _challenges = [];

    public Rewards()
    {
        // empty
    }

    public Rewards SetLevel(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);

        _level = level;
        return this;
    }

    public Rewards AddItem(Guid id, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count > 0)
        {
            _items[id] = _items.GetValueOrDefault(id, 0) + count;
        }

        return this;
    }

    public Rewards AddBuildplate(Guid id)
    {
        _buildplates.Add(id);
        return this;
    }

    public Rewards AddChallenge(string id)
    {
        _challenges.Add(id);
        return this;
    }

    public Rewards AddRubies(int rubies)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rubies);

        _rubies += rubies;
        return this;
    }

    public Rewards AddExperiencePoints(int experiencePoints)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(experiencePoints);

        _experiencePoints += experiencePoints;
        return this;
    }

    public async Task ToRedeemQueryAsync(EarthDbContext earthDb, ResultsEF.Builder results, Guid accountId, DateTimeOffset currentTime, StaticData.StaticDataProvider staticData, CancellationToken cancellationToken = default)
    {
        var checkLevelUp = false;
        if (_rubies > 0 || _experiencePoints > 0)
        {
            var profile = await earthDb.Profiles
                .AsTracking()
                .FirstAsync(profile => profile.Id == accountId, cancellationToken: cancellationToken);

            if (_rubies > 0)
            {
                profile.Rubies.Earned += _rubies;
            }

            if (_experiencePoints > 0)
            {
                profile.Experience += _experiencePoints;
            }

            if (_experiencePoints > 0)
            {
                checkLevelUp = true;
            }

            await earthDb.SaveChangesAsync(cancellationToken);

            results.Profile();
        }

        if (_items.Count > 0)
        {
            foreach (var (itemId, quantity) in _items)
            {
                if (quantity > 0)
                {
                    Catalog.ItemsCatalogR.Item? item = staticData.Catalog.ItemsCatalog.GetItem(itemId);
                    Debug.Assert(item is not null);

                    if (item.Stackable)
                    {
                        await InventoryUtils.AddStackableItemsAsync(earthDb, results, accountId, itemId, quantity, cancellationToken);
                    }
                    else
                    {
                        await InventoryUtils.AddInstanceItemsAsync(earthDb, results, accountId, itemId, quantity, cancellationToken);
                    }

                    if (await JournalUtils.AddCollectedItemAsync(earthDb, results, accountId, itemId, currentTime, quantity, cancellationToken) is 0)
                    {
                        if (item.JournalEntry is not null)
                        {
                            await TokenUtils.AddTokenAsync(earthDb, results, new JournalItemUnlockedTokenEF(accountId, itemId), cancellationToken);
                        }
                    }
                }
            }

            await earthDb.SaveChangesAsync(cancellationToken);

            results.Inventory();
            results.Journal();
        }

        if (_buildplates.Count > 0)
        {
            // TODO
        }

        if (_challenges.Count > 0)
        {
            // TODO
        }

        if (checkLevelUp)
        {
            await LevelUtils.CheckAndHandlePlayerLevelUpAsync(earthDb, results, accountId, currentTime, staticData);
        }
    }

    public Types.Common.Rewards ToApiResponse()
        => new Types.Common.Rewards(
            _rubies,
            _experiencePoints,
            _level,
            [.. _items.Select(item => new Types.Common.Rewards.Item(item.Key, item.Value))],
            [.. _buildplates],
            [.. _challenges.Select(challenge => new Types.Common.Rewards.Challenge(challenge))],
            [],
            []
        );

    public static Rewards FromDBRewardsModel(DB.Earth.Models.Common.Rewards rewardsModel)
    {
        var rewards = new Rewards();
        rewards.AddRubies(rewardsModel.Rubies);
        rewards.AddExperiencePoints(rewardsModel.ExperiencePoints);
        if (rewardsModel.Level is not null)
        {
            rewards.SetLevel(rewardsModel.Level.Value);
        }

        foreach (var (id, count) in rewardsModel.Items)
        {
            rewards.AddItem(id, count);
        }

        foreach (var id in rewardsModel.Buildplates)
        {
            rewards.AddBuildplate(id);
        }

        foreach (var id in rewardsModel.Challenges)
        {
            rewards.AddChallenge(id);
        }

        return rewards;
    }

    public DB.Earth.Models.Common.Rewards ToDBRewardsModel()
        => new DB.Earth.Models.Common.Rewards(
            _rubies,
            _experiencePoints,
            _level,
            _items.ToDictionary(item => item.Key, item => item.Value),
            [.. _buildplates],
            [.. _challenges]
        );
}
