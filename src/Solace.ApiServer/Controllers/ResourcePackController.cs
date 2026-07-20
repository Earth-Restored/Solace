using Asp.Versioning;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Types;
using Solace.ApiServer.Utils;
using Solace.Common;

namespace Solace.ApiServer.Controllers;

//Wheres the resource pack?
[ApiVersion("1.1")]
[Route("api/v{version:apiVersion}/resourcepacks/2020.1217.02/default")]
internal sealed class ResourcePackController : ControllerBase
{
    internal sealed record ResourcePackResponse(int Order, int[] ParsedResourcePackVersion, string RelativePath, string ResourcePackVersion, string ResourcePackId);
    
    [HttpGet]
    public ContentResult Get()
    {
        var resp = Json.Serialize(new EarthApiResponse(new ResourcePackResponse[]{
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
