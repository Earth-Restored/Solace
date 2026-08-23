using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Solace.BuildplateImporter;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Player;
using Solace.ObjectStore.Client;

namespace Solace.ApiServer.Utils;

internal sealed class Rewards
{
    private int _rubies;
    private int _experiencePoints;

    private int? _level;
    private readonly Dictionary<Guid, int> _items = [];
    private readonly HashSet<Guid> _buildplates = [];
    private readonly HashSet<string> _challenges = [with(StringComparer.Ordinal)];

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

    public async Task ToRedeemQueryAsync(EarthDbContext earthDb, ResultsEF.Builder results, ObjectStoreClient objectStore, Guid profileId, DateTimeOffset currentTime, StaticData.StaticDataProvider staticData, CancellationToken cancellationToken = default)
    {
        var checkLevelUp = false;
        if (_rubies > 0 || _experiencePoints > 0)
        {
            var profile = await earthDb.Profiles
                .AsTracking()
                .FirstAsync(profile => profile.Id == profileId, cancellationToken: cancellationToken);

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
                    var item = staticData.Catalog.ItemsCatalog.GetItem(itemId);
                    Debug.Assert(item is not null);

                    if (item.Stackable)
                    {
                        await InventoryUtils.AddStackableItemsAsync(earthDb, results, profileId, itemId, quantity, cancellationToken);
                    }
                    else
                    {
                        await InventoryUtils.AddInstanceItemsAsync(earthDb, results, profileId, itemId, quantity, cancellationToken);
                    }

                    if (await JournalUtils.AddCollectedItemAsync(earthDb, results, profileId, itemId, currentTime, quantity, cancellationToken) is 0)
                    {
                        if (item.JournalEntry is not null)
                        {
                            await TokenUtils.AddTokenAsync(earthDb, results, new JournalItemUnlockedTokenEF(profileId, itemId), cancellationToken);
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
            await using var importer = new Importer(earthDb, null, objectStore, NullLogger.Instance)
            {
                OwnsEarthDb = false,
                OwnsEventBusClient = false,
                OwnsObjectStoreClient = false,
            };

            foreach (var buildplateId in _buildplates)
            {
                if (await earthDb.PlayerBuildplates.AnyAsync(buildplate => buildplate.ProfileId == profileId && buildplate.TemplateId == buildplateId, cancellationToken))
                {
                    continue;
                }

                await importer.AddBuidplateToPlayer(buildplateId, profileId, cancellationToken);

                results.Buildplates();
            }
        }

        if (_challenges.Count > 0)
        {
            // TODO
        }

        if (checkLevelUp)
        {
            await LevelUtils.CheckAndHandlePlayerLevelUpAsync(earthDb, results, profileId, currentTime, staticData);
        }
    }

    public Types.Common.Rewards ToApiResponse()
        => new(
            _rubies,
            _experiencePoints,
            _level,
            [.. _items.Select(item => new Types.Common.Rewards.Item(item.Key, item.Value))],
            [.. _buildplates],
            [.. _challenges.Select(challenge => new Types.Common.Rewards.Challenge(challenge))],
            [],
            []
        );

    public static Rewards FromDBRewardsModel(Db.Earth.Models.Common.Rewards rewardsModel)
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

    public Db.Earth.Models.Common.Rewards ToDBRewardsModel()
        => new(
            _rubies,
            _experiencePoints,
            _level,
            _items.ToDictionary(item => item.Key, item => item.Value),
            [.. _buildplates],
            [.. _challenges]
        );
}
