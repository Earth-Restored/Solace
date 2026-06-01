using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Utils;
using Solace.Common;

namespace Solace.ApiServer.Controllers.EarthApi;

[AllowAnonymous]
[ApiVersion("1.1")]
[Route("1")]
internal sealed class SummaryController : ControllerBase
{
    [HttpGet("summary")]
    public IActionResult Get()
        => Content(Json.Serialize(new EarthApiResponse(new Dictionary<string, object?>
        {
            ["status"] = "ok",
            ["updates"] = new Dictionary<string, object>()
        })), "application/json");
}
