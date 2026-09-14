using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Solace.Db.Earth;
using Solace.Db.Playfab;
using Solace.ObjectStore.Client;
using Solace.WebPortal.Common.Features.Data;
using Solace.WebPortal.Data;

namespace Solace.WebPortal.Features.Data;

public sealed partial class DataArchiveService(
    EarthDbContext earthDb,
    PlayfabDbContext playfabDb,
    ApplicationDbContext webPortalDb,
    ObjectStoreClient objectStore,
    ILogger<DataArchiveService> logger)
{
    private const string FormatVersion = "1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<Stream> ExportAsync(CancellationToken cancellationToken)
    {
        var temporaryDirectory = Directory.CreateTempSubdirectory("solace-export-");
        try
        {
            var databases = new Dictionary<string, (DbContext Context, string Name)>(StringComparer.Ordinal)
            {
                ["Earth"] = (earthDb, await GetDatabaseName(earthDb)),
                ["Playfab"] = (playfabDb, await GetDatabaseName(playfabDb)),
                ["WebPortal"] = (webPortalDb, await GetDatabaseName(webPortalDb)),
            };

            var objectIds = new List<Guid>();
            await foreach (var objectId in objectStore.ListIdsAsync(cancellationToken))
            {
                objectIds.Add(objectId);
            }

            foreach (var (key, database) in databases)
            {
                await RunPostgresToolAsync("pg_dump", database.Context.Database.GetConnectionString()!, ["--format=custom", "--file", Path.Combine(temporaryDirectory.FullName, $"{key.ToLowerInvariant()}.dump")], cancellationToken);
            }

            var manifest = new DataArchiveManifest(
                FormatVersion,
                Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0",
                DateTimeOffset.UtcNow,
                [.. objectIds.Select(id => id.ToString("D"))],
                databases.ToDictionary(pair => pair.Key, pair => pair.Value.Name, StringComparer.Ordinal));

            var archive = new MemoryStream();
            using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, leaveOpen: true))
            {
                var manifestEntry = zip.CreateEntry("manifest.json", CompressionLevel.Fastest);
                await using (var manifestStream = manifestEntry.Open())
                {
                    await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, cancellationToken);
                }

                foreach (var key in databases.Keys)
                {
                    var entry = zip.CreateEntry($"{key.ToLowerInvariant()}.dump", CompressionLevel.Fastest);
                    await using var destination = entry.Open();
                    await using var source = File.OpenRead(Path.Combine(temporaryDirectory.FullName, $"{key.ToLowerInvariant()}.dump"));
                    await source.CopyToAsync(destination, cancellationToken);
                }

                foreach (var objectId in objectIds)
                {
                    await using var source = await objectStore.GetStreamAsync(objectId, cancellationToken)
                        ?? throw new InvalidOperationException($"Object store object '{objectId}' disappeared during export.");
                    var entry = zip.CreateEntry($"objects/{objectId:D}", CompressionLevel.Fastest);
                    await using var destination = entry.Open();
                    await source.CopyToAsync(destination, cancellationToken);
                }
            }

            archive.Position = 0;
            return archive;
        }
        finally
        {
            temporaryDirectory.Delete(recursive: true);
        }
    }

    public async Task ImportAsync(Stream archiveStream, DataConflictResolution conflictResolution, CancellationToken cancellationToken)
    {
        var temporaryDirectory = Directory.CreateTempSubdirectory($"solace-import-{Guid.NewGuid()}");
        var stagingDatabases = new List<string>();

        try
        {
            var archivePath = Path.Combine(temporaryDirectory.FullName, "archive.zip");
            await using (var archiveFile = File.Create(archivePath))
            {
                await archiveStream.CopyToAsync(archiveFile, cancellationToken);
            }

            ZipFile.ExtractToDirectory(archivePath, temporaryDirectory.FullName);
            var manifest = await ReadManifestAsync(Path.Combine(temporaryDirectory.FullName, "manifest.json"), cancellationToken);
            ValidateManifest(manifest, temporaryDirectory.FullName);

            var contexts = new (string Key, DbContext Live, Func<string, DbContext> CreateStaging)[]
            {
                ("Earth", earthDb, EarthDbContext.CreateFromConnection),
                ("Playfab", playfabDb, PlayfabDbContext.CreateFromConnection),
                ("WebPortal", webPortalDb, connectionString => ApplicationDbContext.CreateFromConnection(connectionString, true)),
            };

            foreach (var (key, live, createStaging) in contexts)
            {
                var stagingName = $"solace_import_{Guid.NewGuid():N}";
                stagingDatabases.Add(stagingName);
                var stagingConnection = await CreateStagingDatabaseAsync(live.Database.GetDbConnection().ConnectionString, stagingName, cancellationToken);
                await RunPostgresToolAsync("pg_restore", stagingConnection, ["--exit-on-error", Path.Combine(temporaryDirectory.FullName, $"{key.ToLowerInvariant()}.dump")], cancellationToken);

                await using var staging = createStaging(stagingConnection);
                await staging.Database.MigrateAsync(cancellationToken);
                await MergeDatabaseAsync(staging, live, conflictResolution, cancellationToken);
            }

            await RestoreObjectsAsync(temporaryDirectory.FullName, manifest, conflictResolution, cancellationToken);
        }
        finally
        {
            foreach (var stagingDatabase in stagingDatabases)
            {
                try
                {
                    await DropDatabaseAsync(earthDb.Database.GetDbConnection().ConnectionString, stagingDatabase, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    LogFailedToDropDatabase(exception, stagingDatabase);
                }
            }

            temporaryDirectory.Delete(recursive: true);
        }
    }

    private static async Task<string> GetDatabaseName(DbContext context)
        => context.Database.GetDbConnection().Database;

    private static async Task<DataArchiveManifest> ReadManifestAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);

        return await JsonSerializer.DeserializeAsync<DataArchiveManifest>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("The archive manifest is empty.");
    }

    private static void ValidateManifest(DataArchiveManifest manifest, string directory)
    {
        if (manifest.FormatVersion != FormatVersion)
        {
            throw new InvalidDataException($"Unsupported archive format version '{manifest.FormatVersion}'.");
        }

        foreach (var name in (ReadOnlySpan<string>)["earth.dump", "playfab.dump", "webportal.dump",])
        {
            if (!File.Exists(Path.Combine(directory, name)))
            {
                throw new InvalidDataException($"The archive is missing '{name}'.");
            }
        }
    }

    private static async Task<string> CreateStagingDatabaseAsync(string connectionString, string database, CancellationToken cancellationToken)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres" };

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand($"""
            CREATE DATABASE {QuoteIdentifier(database)}
            """, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        builder.Database = database;

        return builder.ConnectionString;
    }

    private static async Task DropDatabaseAsync(string connectionString, string database, CancellationToken cancellationToken)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres" };

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand($"""
            DROP DATABASE IF EXISTS {QuoteIdentifier(database)} WITH (FORCE)
            """, connection);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task MergeDatabaseAsync(DbContext source, DbContext destination, DataConflictResolution resolution, CancellationToken cancellationToken)
    {
        await using var sourceConnection = (NpgsqlConnection)source.Database.GetDbConnection();
        await using var destinationConnection = (NpgsqlConnection)destination.Database.GetDbConnection();
        await sourceConnection.OpenAsync(cancellationToken);
        await destinationConnection.OpenAsync(cancellationToken);
        await using var transaction = await destinationConnection.BeginTransactionAsync(cancellationToken);

        var tables = await GetTablesAsync(sourceConnection, cancellationToken);
        foreach (var table in tables)
        {
            var columns = await GetColumnsAsync(sourceConnection, table, cancellationToken);
            if (columns.Count is 0)
            {
                continue;
            }

            var primaryKeys = await GetPrimaryKeyColumnsAsync(sourceConnection, table, cancellationToken);

            var insertSql = $"""
                INSERT INTO {QuoteIdentifier(table)} ({string.Join(", ", columns.Select(QuoteIdentifier))}) VALUES ({string.Join(", ", columns.Select((_, index) => $"@p{index}"))})
                """;

            if (resolution is DataConflictResolution.Ignore)
            {
                insertSql += " ON CONFLICT DO NOTHING";
            }
            else if (primaryKeys.Count > 0)
            {
                var conflictTarget = string.Join(", ", primaryKeys.Select(QuoteIdentifier));
                var updateSet = string.Join(", ", columns.Select(column => $"{QuoteIdentifier(column)} = EXCLUDED.{QuoteIdentifier(column)}"));
                insertSql += $" ON CONFLICT ({conflictTarget}) DO UPDATE SET {updateSet}";
            }

            await using var select = new NpgsqlCommand($"SELECT {string.Join(", ", columns.Select(QuoteIdentifier))} FROM {QuoteIdentifier(table)}", sourceConnection);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                await using var insert = new NpgsqlCommand(insertSql, destinationConnection, transaction);
                for (var index = 0; index < columns.Count; index++)
                {
                    var parameter = insert.Parameters.AddWithValue($"p{index}", reader.GetValue(index));
                    parameter.DataTypeName = reader.GetDataTypeName(index);
                }

                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<List<string>> GetTablesAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE' ORDER BY table_name
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var tables = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static async Task<List<string>> GetColumnsAsync(NpgsqlConnection connection, string table, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = $1 AND is_generated = 'NEVER' AND (identity_generation IS NULL OR identity_generation = 'BY DEFAULT') ORDER BY ordinal_position
            """, connection);
        command.Parameters.AddWithValue(table);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private async Task RestoreObjectsAsync(string directory, DataArchiveManifest manifest, DataConflictResolution resolution, CancellationToken cancellationToken)
    {
        foreach (var objectIdText in manifest.ObjectIds)
        {
            if (!Guid.TryParse(objectIdText, out var objectId))
            {
                throw new InvalidDataException($"Invalid object ID '{objectIdText}'.");
            }

            var path = Path.Combine(directory, "objects", objectId.ToString("D"));
            if (!File.Exists(path))
            {
                throw new InvalidDataException($"The archive is missing object '{objectId}'.");
            }

            if (resolution is DataConflictResolution.Ignore && await objectStore.ExistsAsync(objectId, cancellationToken))
            {
                continue;
            }

            await using var stream = File.OpenRead(path);
            await objectStore.UpdateAsync(objectId, stream, cancellationToken);
        }
    }

    private static async Task RunPostgresToolAsync(string tool, string databaseConnectionString, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(databaseConnectionString);
        var startInfo = new ProcessStartInfo(tool,
        [
            "--host", connectionString.Host ?? "localhost",
            "--port", connectionString.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--username", connectionString.Username ?? "postgres",
            "--dbname", connectionString.Database!,
            .. arguments,
        ])
        {
            FileName = tool,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.Environment["PGPASSWORD"] = connectionString.Password;

        var output = await Process.RunAndCaptureTextAsync(startInfo, cancellationToken);
        if (output.ExitStatus.ExitCode is not 0)
        {
            throw new InvalidOperationException($"{tool} failed for database '{connectionString.Database}': {output.StandardError}");
        }
    }

    private static async Task<List<string>> GetPrimaryKeyColumnsAsync(NpgsqlConnection connection, string table, CancellationToken cancellationToken)
    {
        const string sql = """
        SELECT kcu.column_name
        FROM information_schema.table_constraints tc
        JOIN information_schema.key_column_usage kcu
            ON tc.constraint_name = kcu.constraint_name
            AND tc.table_schema = kcu.table_schema
        WHERE tc.constraint_type = 'PRIMARY KEY'
          AND tc.table_schema = 'public'
          AND tc.table_name = $1
        ORDER BY kcu.ordinal_position
        """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(table);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var primaryKeys = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            primaryKeys.Add(reader.GetString(0));
        }

        return primaryKeys;
    }

    private static string QuoteIdentifier(string identifier)
        => $"\"{identifier.Replace("\"", "\"\"")}\"";

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to drop staging database {DatabaseName}")]
    private partial void LogFailedToDropDatabase(Exception exception, string DatabaseName);
}
