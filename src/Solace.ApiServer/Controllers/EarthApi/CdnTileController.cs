using Asp.Versioning;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Utils;
using Solace.DB;
using Solace.EventBus.Client;
using Solace.ObjectStore.Client;

namespace Solace.ApiServer.Controllers.EarthApi;

[ApiVersion("1.1")]
[Route("cdn/tile/16/{_}/{tilePos1}_{tilePos2}_16.png")]
[ResponseCache(Duration = 11200)]
internal sealed class CdnTileController : SolaceControllerBase
{
    private readonly EarthDbContext _earthDb;
    private readonly EventBusClient _eventBus;
    private readonly ObjectStoreClient _objectStore;

    public CdnTileController(EarthDbContext earthDb, EventBusClient eventBus, ObjectStoreClient objectStore)
    {
        _earthDb = earthDb;
        _eventBus = eventBus;
        _objectStore = objectStore;
    }

    [HttpGet]
    public async Task<Results<EmptyHttpResult, NotFound>> GetTile(int _, int tilePos1, int tilePos2, CancellationToken cancellationToken) // _ used because we dont care :|
    {
        if (!await TileUtils.TryWriteTile(tilePos1, tilePos2, Response.Body, _earthDb, _eventBus, _objectStore, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var cd = new System.Net.Mime.ContentDisposition { FileName = $"{tilePos1}_{tilePos2}_16.png", Inline = true };
        Response.Headers.Append("Content-Disposition", cd.ToString());
        Response.Headers.ContentType = "application/octet-stream";

        return TypedResults.Empty;
    }
}
