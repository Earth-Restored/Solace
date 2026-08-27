using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Solace.ApiServer.Controllers.EarthApi;

[AllowAnonymous]
[ApiVersion("1.1")]
[Route("1")]
internal sealed class SummaryController : SolaceControllerBase
{
    [HttpGet("summary")]
    public ContentHttpResult Get()
        => EarthJson(new Dictionary<string, object?>
        {
            ["status"] = "ok",
            ["updates"] = new Dictionary<string, object>()
        });
}
