using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Player;

namespace Solace.ApiServer.Utils;

internal static class InventoryUtils
{
    public static async Task<bool> TakeStackableItemsAsync(EarthDbContext earthDb, ResultsEF.Builder results, Guid accountId, Guid itemId, int count, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count is 0)
        {
            return true;
        }

        var rowsAffected = await earthDb.StackableItems
            .Where(si => si.AccountId == accountId && si.ItemId == itemId && si.Count >= count)
            .ExecuteUpdateAsync(setters => setters.SetProperty(si => si.Count, si => si.Count - count), cancellationToken);

        results.Inventory(rowsAffected > 0);

        return rowsAffected > 0;
    }

    public static async Task<IEnumerable<NonStackableItemInstanceEF>> TakeInstanceItemsAsync(EarthDbContext earthDb, ResultsEF.Builder results, Guid accountId, Guid itemId, IEnumerable<Guid> instanceIds, CancellationToken cancellationToken = default)
    {
        if (instanceIds.TryGetNonEnumeratedCount(out var count) && count is 0)
        {
            return [];
        }

        var itemsToRemove = await earthDb.NonStackableItems
            .Where(x => x.AccountId == accountId && x.ItemId == itemId && instanceIds.Contains(x.InstanceId))
            .ToListAsync(cancellationToken);

        earthDb.NonStackableItems.RemoveRange(itemsToRemove);
        await earthDb.SaveChangesAsync(cancellationToken);

        results.Inventory(itemsToRemove.Count > 0);

        return itemsToRemove;
    }

    public static async Task AddStackableItemsAsync(EarthDbContext earthDb, ResultsEF.Builder results, Guid accountId, Guid itemId, int count, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count is 0)
        {
            return;
        }

        var rowsAffected = await earthDb.StackableItems
            .Where(x => x.AccountId == accountId && x.ItemId == itemId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Count, x => x.Count + count), cancellationToken);

        if (rowsAffected is 0)
        {
            try
            {
                var newItem = new StackableItemEF(accountId, itemId, count);

                earthDb.StackableItems.Add(newItem);
                await earthDb.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                await earthDb.StackableItems
                    .Where(x => x.AccountId == accountId && x.ItemId == itemId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Count, x => x.Count + count), cancellationToken);
            }
        }

        results.Inventory();
    }

    public static async Task AddInstanceItemsAsync(EarthDbContext earthDb, ResultsEF.Builder results, Guid accountId, Guid itemId, int count, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count is 0)
        {
            return;
        }

        var instances = Enumerable.Range(0, count).Select(index => new NonStackableItemInstanceEF(accountId, itemId, Guid.NewGuid(), 0));

        await earthDb.NonStackableItems.AddRangeAsync(instances, cancellationToken);
        await earthDb.SaveChangesAsync(cancellationToken);

        results.Inventory();
    }

    public static async Task AddInstanceItemsAsync(EarthDbContext earthDb, ResultsEF.Builder results, IEnumerable<NonStackableItemInstanceEF> instances, CancellationToken cancellationToken = default)
    {
        if (instances.TryGetNonEnumeratedCount(out var count) && count is 0)
        {
            return;
        }

        await earthDb.NonStackableItems.AddRangeAsync(instances, cancellationToken);
        await earthDb.SaveChangesAsync(cancellationToken);

        results.Inventory();
    }
}