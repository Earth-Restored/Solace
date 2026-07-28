using System.Diagnostics;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.ObjectStore.Client;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Data;
using Solace.WebPortal.Data;

namespace Solace.WebPortal.Features.Data;

[Handler]
[MapGet("size")]
[MapGroup<DataGroup>]
[Authorize(Policy = Permissions.ViewData)]
public static partial class GetSize
{
    public sealed record Query;

    private static async ValueTask<GetSizeResponse> HandleAsync(
        Query _,
        EarthDbContext earthDb,
        ApplicationDbContext webPortalDb,
        ObjectStoreClient objectStore,
        CancellationToken cancellationToken
    )
    {
        var earthDbSize = await GetDatabaseSize(earthDb, cancellationToken);
        var webPortalDbSize = await GetDatabaseSize(webPortalDb, cancellationToken);
        var objectStoreSize = await objectStore.GetTotalSizeAsync(cancellationToken);

        return new GetSizeResponse(earthDbSize, webPortalDbSize, objectStoreSize);
    }

    private static async Task<long> GetDatabaseSize(DbContext dbContext, CancellationToken cancellationToken)
    {
        using var command = dbContext.Database.GetDbConnection().CreateCommand();

        command.CommandText = "SELECT pg_database_size(current_database());";

        if (command.Connection is { State: not System.Data.ConnectionState.Open })
        {
            await command.Connection.OpenAsync(cancellationToken);
        }

        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
