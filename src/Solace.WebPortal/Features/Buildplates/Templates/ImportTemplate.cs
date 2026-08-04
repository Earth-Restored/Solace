using System.IO.Compression;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Solace.BuildplateImporter;
using Solace.Db.Earth;
using Solace.EventBus.Client;
using Solace.ObjectStore.Client;
using Solace.WebPortal.Common;

namespace Solace.WebPortal.Features.Buildplates.Templates;

[Handler]
[MapPost("import")]
[MapGroup<TemplatesGroup>]
[Authorize(Policy = Permissions.ManageBuildplates)]
public sealed partial class ImportTemplate(
    EarthDbContext earthDb,
    EventBusClient eventBus,
    ObjectStoreClient objectStore,
    IConfiguration configuration,
    ILogger<ImportTemplate> logger
)
{
    public sealed record Command
    {
        public required IFormFile File { get; init; }
    }

    private async ValueTask<Results<Ok, InternalServerError<string>>> HandleAsync(
        Command command,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var importer = new Importer(earthDb, eventBus, objectStore, logger)
            {
                OwnsEarthDb = true,
                OwnsEventBusClient = false,
                OwnsObjectStoreClient = false,
            };

            var rawName = Path.GetFileNameWithoutExtension(command.File.FileName);
            var displayName = string.IsNullOrWhiteSpace(rawName) ? "imported buildplate" : rawName;

            var fixUpBuildplates = configuration.GetValue<bool>("FixUpBuildplatesOnImport", false);

            using var memoryStream = new MemoryStream();
            await command.File.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            if (Path.GetExtension(command.File.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                MCeToJava.Converter.InitRegistry(logger);
                var mceBuildplate = await MCeToJava.Utils.JsonUtils.DeserializeJsonAsync<MCeToJava.Models.MCE.Buildplate>(memoryStream);

                if (mceBuildplate is null)
                {
                    return TypedResults.InternalServerError("Failed to parse the buildplate JSON.");
                }

                var options = new MCeToJava.Converter.Options(
                    logger,
                    MCeToJava.Models.ConvertTarget.Vienna,
                    "minecraft:plains",
                    displayName
                );

                var worldData = await MCeToJava.Converter.Convert(mceBuildplate, null, options);

                using var zipStream = new MemoryStream();
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    foreach (var (fileName, fileContents) in worldData.Files)
                    {
                        var entry = archive.CreateEntry(fileName);
                        using var entryStream = entry.Open();
                        await entryStream.WriteAsync(fileContents, cancellationToken);
                    }
                }

                zipStream.Position = 0;
                if (!await importer.ImportTemplateAsync(Guid.CreateVersion7(), displayName, zipStream, fixUpBuildplates, cancellationToken))
                {
                    return TypedResults.InternalServerError("Unknown error occured.");
                }
            }
            else
            {
                if (!await importer.ImportTemplateAsync(Guid.CreateVersion7(), displayName, memoryStream, fixUpBuildplates, cancellationToken))
                {
                    return TypedResults.InternalServerError("Unknown error occured.");
                }
            }

            return TypedResults.Ok();
        }
        catch (Exception exception)
        {
            LogFailedToImport(exception);
            return TypedResults.InternalServerError("Unknown error occured.");
        }
    }

    [LoggerMessage(Level = LogLevel.Error)]
    private partial void LogFailedToImport(Exception exception);
}
