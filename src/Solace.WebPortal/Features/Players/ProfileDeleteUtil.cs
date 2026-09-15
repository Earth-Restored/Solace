using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.ObjectStore.Client;

namespace Solace.WebPortal.Features.Players;

public static class ProfileDeleteUtil
{
    public static async Task<bool> DeleteProfile(Guid profileId, EarthDbContext earthDb, ObjectStoreClient objectStore, CancellationToken cancellationToken = default)
    {
        var buildplateObjects = await earthDb.PlayerBuildplates
           .AsNoTracking()
           .Where(bp => bp.ProfileId == profileId)
           .Select(bp => new { bp.ServerDataObjectId, bp.PreviewObjectId, })
           .ToListAsync(cancellationToken);

        var sharedBuildplateObjects = await earthDb.SharedBuildplates
           .AsNoTracking()
           .Where(bp => bp.ProfileId == profileId)
           .Select(bp => new { bp.ServerDataObjectId, })
           .ToListAsync(cancellationToken);

        var rowsDeleted = await earthDb.Profiles
            .Where(account => account.Id == profileId)
            .ExecuteDeleteAsync(cancellationToken);

        if (rowsDeleted is 0)
        {
            return false;
        }

        foreach (var bp in buildplateObjects)
        {
            await objectStore.DeleteAsync(bp.ServerDataObjectId, cancellationToken);

            await objectStore.DeleteAsync(bp.PreviewObjectId, cancellationToken);
        }

        foreach (var bp in sharedBuildplateObjects)
        {
            await objectStore.DeleteAsync(bp.ServerDataObjectId, cancellationToken);
        }

        return true;
    }
}