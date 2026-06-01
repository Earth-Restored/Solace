using Asp.Versioning;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Utils;

namespace Solace.ApiServer.Controllers.EarthApi;

[ApiVersion("1.1")]
[Route("cdn/tile/16/{_}/{tilePos1}_{tilePos2}_16.png")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
internal sealed class CdnTileController : SolaceControllerBase
{
    [HttpGet]
    public async Task<Results<EmptyHttpResult, NotFound>> GetTile(int _, int tilePos1, int tilePos2, CancellationToken cancellationToken) // _ used because we dont care :|
    {
        Response.Headers.ContentType = "image/png";
        if (!await TileUtils.TryWriteTile(tilePos1, tilePos2, Response.Body, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var cd = new System.Net.Mime.ContentDisposition { FileName = tilePos1 + "_" + tilePos2 + "_16.png", Inline = true };
        Response.Headers.Append("Content-Disposition", cd.ToString());

        return TypedResults.Empty;
    }
}
