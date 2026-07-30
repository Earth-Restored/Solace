using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Solace.Db;

public static class PostgresDatabaseFacadeExtensions
{
    private const long MigrationLockKey = 8127391104;

    extension(DatabaseFacade database)
    {
        public async Task MigrateAsyncWithLock(CancellationToken cancellationToken = default)
        {
            if (!database.IsNpgsql())
            {
                throw new InvalidOperationException($"MigrateAsyncWithLock can only be used with PostgreSQL. The current provider is '{database.ProviderName}'.");
            }

            var databaseCreator = database.GetService<IRelationalDatabaseCreator>();
            if (!await databaseCreator.ExistsAsync(cancellationToken))
            {
                try
                {
                    await databaseCreator.CreateAsync(cancellationToken);
                }
                catch (PostgresException ex) when (ex.SqlState == "42P04")
                {
                    // duplicate database, ignore
                }
            }

            var connection = database.GetDbConnection();
            var wasClosed = connection.State is ConnectionState.Closed;

            if (wasClosed)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using (var lockCommand = connection.CreateCommand())
                {
                    lockCommand.CommandText = "SELECT pg_advisory_lock(@lockId);";

                    var param = lockCommand.CreateParameter();
                    param.ParameterName = "lockId";
                    param.Value = MigrationLockKey;
                    lockCommand.Parameters.Add(param);

                    await lockCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                try
                {
                    await database.MigrateAsync(cancellationToken);
                }
                finally
                {
                    await using (var unlockCommand = connection.CreateCommand())
                    {
                        unlockCommand.CommandText = "SELECT pg_advisory_unlock(@lockId);";

                        var param = unlockCommand.CreateParameter();
                        param.ParameterName = "lockId";
                        param.Value = MigrationLockKey;
                        unlockCommand.Parameters.Add(param);

                        await unlockCommand.ExecuteNonQueryAsync(cancellationToken);
                    }
                }
            }
            finally
            {
                if (wasClosed)
                {
                    await connection.CloseAsync();
                }
            }
        }
    }
}
