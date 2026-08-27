using Asp.Versioning;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Solace.ApiServer.Types;
using Solace.ApiServer.Utils;
using Solace.Common;

namespace Solace.ApiServer.Controllers.EarthApi;

//Wheres the resource pack?
[ApiVersion("1.1")]
[Route("api/v{version:apiVersion}/resourcepacks/2020.1217.02/default")]
internal sealed class ResourcePackController : ControllerBase
{
    [HttpGet]
    public ContentResult Get()
    {
        string resp = Json.Serialize(new EarthApiResponse(new ResourcePackResponse[]{
            new ResourcePackResponse(
                0,
                [2020, 1214, 4],
                "availableresourcepack/resourcepacks/dba38e59-091a-4826-b76a-a08d7de5a9e2-1301b0c257a311678123b9e7325d0d6c61db3c35",
                "2020.1214.04",
                "dba38e59-091a-4826-b76a-a08d7de5a9e2"
            )
        }));
        return Content(resp, "application/json");
    }
}

//Heres the resource pack!
[Route("cdn/availableresourcepack/resourcepacks/dba38e59-091a-4826-b76a-a08d7de5a9e2-1301b0c257a311678123b9e7325d0d6c61db3c35")]
public class ResourcePackCdnController : ControllerBase
{
    private readonly StaticData.StaticData _staticData;

    public ResourcePackCdnController(StaticData.StaticData staticData)
    {
        _staticData = staticData;
    }

    [HttpGet, HttpHead]
    public async Task<Results<BadRequest, PhysicalFileHttpResult>> Get()
    {
        string resourcePackFilePath = Path.Combine(_staticData.Directory, "resourcepacks", "vanilla.zip"); //resource packs are distributed as renamed zip files containing an MCpack

        if (!System.IO.File.Exists(resourcePackFilePath))
        {
            Log.Error("[Resourcepacks] Error! Resource pack file not found.");
            return TypedResults.BadRequest(); //we cannot serve you.
        }

        string downloadName = "dba38e59-091a-4826-b76a-a08d7de5a9e2-1301b0c257a311678123b9e7325d0d6c61db3c35";

        return TypedResults.PhysicalFile(
            path: resourcePackFilePath,
            contentType: "application/octet-stream",
            fileDownloadName: downloadName,
            enableRangeProcessing: true
        );
    }
}
