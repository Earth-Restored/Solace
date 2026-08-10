using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using BitcoderCZ.Utils;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.EntityFrameworkCore;
using Solace.BuildplateImporter;
using Solace.Db.Earth;
using Solace.EventBus.Client;
using Solace.ObjectStore.Client;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Buildplates;
using Solace.WebPortal.Data;
using Solace.WebPortal.Utils;

namespace Solace.WebPortal.Features.Buildplates;

[Handler]
[MapGet("{buildplateId}/preview")]
[MapGroup<BuildplatesGroup>]
public sealed partial class GetPreview(
    ApplicationDbContext appDb,
    EarthDbContext earthDb,
    EventBusClient eventBusClient,
    ObjectStoreClient objectStoreClient,
    BuildplatePreviewGenerationSemaphore semaphore,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    ILogger<GetPreview> logger
)
{
    public sealed record Query(
        Guid BuildplateId,
        bool IsTemplate,
        Guid? PlayerId,
        bool ForceRefresh = false
    );

    private async ValueTask<IAsyncEnumerable<BuildplatePreviewResponse>> HandleAsync(
        Query query,
        CancellationToken cancellationToken)
        => StreamPreviewAsync(query, cancellationToken);

    private async IAsyncEnumerable<BuildplatePreviewResponse> StreamPreviewAsync(
          Query query,
          [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("BuildplatePreview:Enabled", true))
        {
            yield break;
        }

        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser is null)
        {
            yield break;
            // return TypedResults.Unauthorized();
        }

        if (!httpUser.HasPermission(Permissions.CreateProfile) && !httpUser.HasPermission(Permissions.ViewBuildplates) && !httpUser.HasPermission(Permissions.ViewPlayers))
        {
            yield break;
            // return TypedResults.Forbid();
        }

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            httpContext.Response.Headers["X-Accel-Buffering"] = "no";
            httpContext.Response.Headers["Cache-Control"] = "no-cache";
        }

        var channel = Channel.CreateUnbounded<BuildplatePreviewResponse>();

        _ = Task.Run(async () =>
        {
            try
            {
                var progress = new Progress<ProgressReport>(report =>
                {
                    channel.Writer.TryWrite(new BuildplatePreviewResponse(report.PercentComplete, report.StatusMessage, null));
                });

                (byte[] Data, Vector3 BoundsMin, Vector3 BoundsMax)? previewData = null;
                if (query.IsTemplate)
                {
                    var dbBuildplatePreview = await appDb.BuildplatePreviews
                        .AsNoTracking()
                        .FirstOrDefaultAsync(preview => preview.PlayerId == Guid.Empty && preview.BuildplateId == query.BuildplateId, cancellationToken: cancellationToken);

                    if (dbBuildplatePreview is null || query.ForceRefresh)
                    {
                        if (dbBuildplatePreview is not null)
                        {
                            appDb.BuildplatePreviews.Remove(dbBuildplatePreview);
                            await appDb.SaveChangesAsync(cancellationToken);
                        }

                        channel.Writer.TryWrite(new BuildplatePreviewResponse(0.00, "Queued for generation, please wait", null));

                        await semaphore.Semaphore.WaitAsync(cancellationToken);
                        try
                        {
                            await using var importer = new Importer(earthDb, eventBusClient, objectStoreClient, logger)
                            {
                                OwnsEarthDb = true,
                                OwnsEventBusClient = false,
                                OwnsObjectStoreClient = false,
                            };

                            var resourcePackManager = await ResourcePackManagerSingleton.GetResourcePackManagerAsync(configuration["StaticDataPath"]!);

                            previewData = await importer.GenerateTemplateWebPortalPreviewAsync(query.BuildplateId, appDb, resourcePackManager, progress: progress, cancellationToken);
                        }
                        finally
                        {
                            semaphore.Semaphore.Release();
                        }
                    }
                    else
                    {
                        progress?.Complete();
                        previewData = (dbBuildplatePreview.PreviewData, dbBuildplatePreview.BoundsMin, dbBuildplatePreview.BoundsMax);
                    }
                }
                else
                {
                    var dbBuildplatePreview = await appDb.BuildplatePreviews
                        .AsNoTracking()
                        .FirstOrDefaultAsync(preview => preview.PlayerId == query.PlayerId && preview.BuildplateId == query.BuildplateId, cancellationToken: cancellationToken);

                    if (dbBuildplatePreview is null || query.ForceRefresh)
                    {
                        if (dbBuildplatePreview is not null)
                        {
                            appDb.BuildplatePreviews.Remove(dbBuildplatePreview);
                            await appDb.SaveChangesAsync(cancellationToken);
                        }

                        channel.Writer.TryWrite(new BuildplatePreviewResponse(0.00, "Queued for generation, please wait", null));

                        await semaphore.Semaphore.WaitAsync(cancellationToken);
                        try
                        {
                            await using var importer = new Importer(earthDb, eventBusClient, objectStoreClient, logger)
                            {
                                OwnsEarthDb = true,
                                OwnsEventBusClient = false,
                                OwnsObjectStoreClient = false,
                            };

                            var resourcePackManager = await ResourcePackManagerSingleton.GetResourcePackManagerAsync(configuration["StaticDataPath"]!);

                            previewData = await importer.GetPlayerBuildplateWebPortalPreviewAsync(query.PlayerId!.Value, query.BuildplateId, appDb, resourcePackManager, progress: progress, cancellationToken);
                        }
                        finally
                        {
                            semaphore.Semaphore.Release();
                        }
                    }
                    else
                    {
                        progress?.Complete();
                        previewData = (dbBuildplatePreview.PreviewData, dbBuildplatePreview.BoundsMin, dbBuildplatePreview.BoundsMax);
                    }
                }

                if (previewData is not null)
                {
                    var uri = DataToUri(previewData.Value.Data.AsSpan());
                    channel.Writer.TryWrite(new BuildplatePreviewResponse(1.0, "Done", new(uri, new(previewData.Value.BoundsMin, previewData.Value.BoundsMax))));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                LogGenerationFailed(exception, query.BuildplateId);
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);

        await foreach (var message in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }
    }

    private static unsafe string DataToUri(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return "data:application/octet-stream;base64,";
        }

        const string Prefix = "data:application/octet-stream;base64,";

        var base64Length = (data.Length + 2) / 3 * 4;
        var totalLength = Prefix.Length + base64Length;

        fixed (byte* ptr = data)
        {
            var state = ((nuint)ptr, data.Length);

            return string.Create(totalLength, state, static (span, state) =>
            {
                Prefix.AsSpan().CopyTo(span);

                var (dataPtr, dataLength) = state;

                var byteSpan = new ReadOnlySpan<byte>((void*)dataPtr, dataLength);

                Convert.TryToBase64Chars(byteSpan, span[Prefix.Length..], out _);
            });
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Preview mesh generation failed for Buildplate ID {Id}")]
    private partial void LogGenerationFailed(Exception exception, Guid Id);
}
