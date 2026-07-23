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

namespace Solace.WebPortal.Features.Buildplates.Templates;

[Handler]
[MapGet("{id}/export")]
[MapGroup<TemplatesGroup>]
[Authorize(Policy = Permissions.ViewBuildplates)]
public sealed partial class ExportTemplate(
    EventBusClient eventBusClient,
    ObjectStoreClient objectStoreClient,
    EarthDbContext earthDb,
    ILogger<ExportTemplate> logger
)
{
    public sealed record Query(
        [property: FromRoute] Guid Id,
        [property: FromQuery] string Format
    );

    private async ValueTask<Results<NotFound, InternalServerError, BadRequest<string>, FileStreamHttpResult>> HandleAsync(
        Query query,
        CancellationToken cancellationToken)
    {
        var template = await earthDb.TemplateBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(template => template.Id == query.Id, cancellationToken);

        if (template is null)
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

                    using var data = await objectStoreClient.GetStreamAsync(template.ServerDataObjectId, cancellationToken);

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

                            var metadata = new BuildplateMetadataV1(template.Size, template.Offset, template.Night);

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

                    var data = await objectStoreClient.GetStreamAsync(template.PreviewObjectId, cancellationToken);

                    if (data is null)
                    {
                        // try to regenerate
                        await using var importer = new Importer(earthDb, eventBusClient, objectStoreClient, logger)
                        {
                            OwnsEarthDb = true,
                            OwnsEventBusClient = false,
                            OwnsObjectStoreClient = false,
                        };

                        data = await importer.RegenerateTemplatePreviewAsync(template.Id, cancellationToken);
                        if (data is null)
                        {
                            return TypedResults.InternalServerError();
                        }
                    }

                    using var dataReader = new StreamReader(data, encoding: Encoding.UTF8, leaveOpen: false);

                    var buildplate = new MCeToJava.Models.MCE.Buildplate(
                        template.Id,
                        "",
                        DateTime.UtcNow,
                        false,
                        false,
                        1, // todo
                        1,
                        template.Id,
                        MCeToJava.Models.MCE.Buildplate.Gamemode.Survival,
                        await dataReader.ReadToEndAsync(cancellationToken),
                        new BitcoderCZ.Maths.Vectors.int3(0, template.Offset, 0),
                        new MCeToJava.Models.MCE.Buildplate.Flat2(template.Size, template.Size),
                        template.BlocksPerMeter,
                        MCeToJava.Models.MCE.Buildplate.Orientation.Horizontal,
                        0
                    );

                    fileStream = new MemoryStream();

                    Solace.Common.Json.SerializeIndented(fileStream, buildplate);
                }

                break;
            default:
                return TypedResults.BadRequest("Unknown format.");
        }

        fileStream.Position = 0;

        var fileName = $"{template.Name.Replace(" ", "_")}_export.{extension}";

        return TypedResults.File(fileStream, contentType: contentType, fileDownloadName: fileName);
    }
}
