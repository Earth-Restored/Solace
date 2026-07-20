using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Solace.ApiServer.Controllers;

[AllowAnonymous]
[ApiVersion("1.1")]
[Route("1")]
internal sealed class SummaryController : SolaceControllerBase
{
    [HttpGet("summary")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Endpoints cannot be static")]
    public ContentHttpResult Get()
        => EarthJson(new Dictionary<string, object?>
        {
            ["status"] = "ok",
            ["updates"] = new Dictionary<string, object>()
        });
}
