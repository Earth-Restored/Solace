using Asp.Versioning;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Utils;
using Solace.Common;
using Solace.StaticData;

namespace Solace.ApiServer.Controllers;

[ApiVersion("1.1")]
[Route("api/v{version:apiVersion}/resourcepacks/2020.1217.02/default")]
[ApiController]
internal sealed partial class ResourcePackController : ControllerBase
{
    internal sealed record ResourcePackResponse(int Order, int[] ParsedResourcePackVersion, string RelativePath, string ResourcePackVersion, string ResourcePackId);

    private readonly StaticDataProvider _staticData;
    private readonly ILogger<ResourcePackController> _logger;

    public ResourcePackController(StaticDataProvider staticData, ILogger<ResourcePackController> logger)
    {
        _staticData = staticData;
        _logger = logger;
    }

    [HttpGet]
    public Results<ContentHttpResult, NotFound> Get()
    {
        if (_staticData.Resourcepacks.GenoaResourcepackName is null)
        {
            LogResourcepackNotFound();
            return TypedResults.NotFound();
        }

        var resp = Json.Serialize(new EarthApiResponse(new ResourcePackResponse[]{
            new(
                0,
                [2020, 1214, 4],
                $"availableresourcepack/resourcepacks/{_staticData.Resourcepacks.GenoaResourcepackName}",
                "2020.1214.04",
                _staticData.Resourcepacks.GenoaResourcepackName
            )
        }));

        return TypedResults.Content(resp, "application/json");
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Resource pack file not found")]
    private partial void LogResourcepackNotFound();
}
