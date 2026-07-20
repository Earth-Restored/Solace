using Microsoft.EntityFrameworkCore;
using Solace.Common.Utils;
using Solace.DB;
using Solace.DB.Models.Player;

namespace Solace.ApiServer.Utils;

internal static class HotbarUtils
{
    public static async Task LimitToInventoryAsync(EarthDbContext earthDb, Guid accountId, HotbarEF hotbar, CancellationToken cancellationToken = default)
    {
        var hotbarItemIds = hotbar.Items
            .Where(i => i is not null)
            .Select(i => i!.Uuid)
            .Distinct()
            .ToList();

        if (hotbarItemIds is [])
        {
            return;
        }

        var inventoryStackableCounts = await earthDb.StackableItems
            .AsNoTracking()
            .Where(x => x.AccountId == accountId && hotbarItemIds.Contains(x.ItemId))
            .ToDictionaryAsync(x => x.ItemId, x => x.Count, cancellationToken);

        var inventoryInstances = await earthDb.NonStackableItems
            .AsNoTracking()
            .Where(x => x.AccountId == accountId && hotbarItemIds.Contains(x.ItemId))
            .GroupBy(x => x.ItemId)
            .ToDictionaryAsync(group => group.Key, group => group.Select(x => x.InstanceId).ToHashSet(), cancellationToken);

        Dictionary<Guid, int> usedStackableItemCounts = [];
        Dictionary<Guid, HashSet<Guid>> usedNonStackableItemInstances = [];

        for (var index = 0; index < hotbar.Items.Length; index++)
        {
            var item = hotbar.Items[index];
            if (item is null)
            {
                continue;
            }

            if (item.InstanceId is not null)
            {
                if (inventoryInstances.TryGetValue(item.Uuid, out var instances) && instances.Contains(item.InstanceId.Value))
                {
                    var usedItemInstances = usedNonStackableItemInstances.ComputeIfAbsent(item.Uuid, uuid => [])!;

                    if (!usedItemInstances.Add(item.InstanceId.Value))
                    {
                        item = null;
                    }
                }
                else
                {
                    item = null;
                }
            }
            else
            {
                var inventoryCount = inventoryStackableCounts.GetValueOrDefault(item.Uuid, 0);

                var usedCount = usedStackableItemCounts.GetValueOrDefault(item.Uuid);
                var availableCount = inventoryCount - usedCount;

                if (availableCount > 0)
                {
                    if (availableCount < item.Count)
                    {
                        item = new HotbarEF.Item(item.Uuid, availableCount, null);
                    }

                    usedCount += item.Count;
                    usedStackableItemCounts[item.Uuid] = usedCount;
                }
                else
                {
                    item = null;
                }
            }

            hotbar.Items[index] = item;
        }
    }
}
