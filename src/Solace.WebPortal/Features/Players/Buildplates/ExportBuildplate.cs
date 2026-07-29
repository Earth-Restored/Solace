using System.IO.Compression;
using System.Text;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Buildplate.Model;
using Solace.BuildplateImporter;
using Solace.Db.Earth;
using Solace.EventBus.Client;
using Solace.ObjectStore.Client;
using Solace.WebPortal.Common;

namespace Solace.WebPortal.Features.Players.Buildplates;

[Handler]
[MapGet("{buildplateId}/export")]
[MapGroup<BuildplatesGroup>]
[Authorize(Policy = Permissions.ViewPlayers)]
public sealed partial class ExportBuildplate(
    EventBusClient eventBusClient,
    ObjectStoreClient objectStoreClient,
    EarthDbContext earthDb,
    ILogger<ExportBuildplate> logger
)
{
    public sealed record Query(
        [property: FromRoute] Guid PlayerId,
        [property: FromRoute] Guid BuildplateId,
        [property: FromQuery] string Format
    );

    private async ValueTask<Results<NotFound, InternalServerError, BadRequest<string>, FileStreamHttpResult>> HandleAsync(
        Query query,
        CancellationToken cancellationToken)
    {
        var buildplate = await earthDb.PlayerBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(buildplate => buildplate.AccountId == query.PlayerId && buildplate.Id == query.BuildplateId, cancellationToken);

        if (buildplate is null)
        {
            return TypedResults.NotFound();
        }

        var fileStream = new MemoryStream();
        string extension;
        string contentType;

        switch (query.Format)
        {
            case "Solace":
                {
                    extension = "zip";
                    contentType = "application/zip";

                    using var data = await objectStoreClient.GetStreamAsync(buildplate.ServerDataObjectId, cancellationToken);

                    if (data is null)
                    {
                        return TypedResults.InternalServerError();
                    }

                    var stream = new MemoryStream((int)data.Length);
                    await data.CopyToAsync(stream, cancellationToken);
                    stream.Position = 0;

                    using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
                    {
                        if (zip.GetEntry(WorldData.MetadataFileName) is null)
                        {
                            var metadataEntry = zip.CreateEntry(WorldData.MetadataFileName);

                            var metadata = new BuildplateMetadataV1(buildplate.Size, buildplate.Offset, buildplate.Night);

                            using var metadataStream = metadataEntry.Open();
                            WorldData.WriteMetadata(metadataStream, metadata);
                        }
                    }

                    fileStream = stream;
                }

                break;
            case "Earth":
                {
                    extension = "json";
                    contentType = "application/json";

                    var data = await objectStoreClient.GetStreamAsync(buildplate.PreviewObjectId, cancellationToken);

                    if (data is null)
                    {
                        // try to regenerate
                        await using var importer = new Importer(earthDb, eventBusClient, objectStoreClient, logger)
                        {
                            OwnsEarthDb = true,
                            OwnsEventBusClient = false,
                            OwnsObjectStoreClient = false,
                        };

                        data = await importer.RegenerateTemplatePreviewAsync(buildplate.Id, cancellationToken);
                        if (data is null)
                        {
                            return TypedResults.InternalServerError();
                        }
                    }

                    using var dataReader = new StreamReader(data, encoding: Encoding.UTF8, leaveOpen: false);

                    var buildplateJson = new MCeToJava.Models.MCE.Buildplate(
                        buildplate.Id,
                        "",
                        DateTime.UtcNow,
                        false,
                        false,
                        1, // todo
                        1,
                        buildplate.TemplateId ?? Guid.Empty,
                        MCeToJava.Models.MCE.Buildplate.Gamemode.Survival,
                        await dataReader.ReadToEndAsync(cancellationToken),
                        new BitcoderCZ.Maths.Vectors.int3(0, buildplate.Offset, 0),
                        new MCeToJava.Models.MCE.Buildplate.Flat2(buildplate.Size, buildplate.Size),
                        buildplate.BlocksPerMeter,
                        MCeToJava.Models.MCE.Buildplate.Orientation.Horizontal,
                        0
                    );

                    fileStream = new MemoryStream();

                    Solace.Common.Json.SerializeIndented(fileStream, buildplateJson);
                }

                break;
            default:
                return TypedResults.BadRequest("Unknown format.");
        }

        fileStream.Position = 0;

        var fileName = $"{buildplate.Name.Replace(" ", "_")}_export.{extension}";

        return TypedResults.File(fileStream, contentType: contentType, fileDownloadName: fileName);
    }
}
