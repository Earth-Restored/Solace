using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Solace.ApiServer.Controllers;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}")]
internal sealed class TutorialController : SolaceControllerBase
{
    // todo: surely only 1 is needed
    [HttpGet("player/tutorial")]
    [HttpGet("player/tutorials")]
    [HttpGet("player/oobe")]
    [HttpGet("player/outofboxexperience")]
    [HttpGet("tutorial")]
    [HttpGet("tutorials")]
    [HttpGet("oobe")]
    [HttpGet("outofboxexperience")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Endpoints cannot be static")]
    public ContentHttpResult GetTutorialState()
        => EarthJson(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["completed"] = new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["map_permission"] = true,
                ["tappable"] = true,
                ["adventure"] = true,
                ["adventure_crystal_activation"] = true,
                ["adventure_preview"] = true,
                ["ar_placement"] = true,
                ["ar_gameplay"] = true,
                ["journal"] = true,
                ["challenge"] = true,
                ["challenges"] = true,
                ["freedom"] = true
            },
            ["available"] = Array.Empty<string>()
        });

    // todo: surely only 1 is needed
    [HttpPost("player/tutorial")]
    [HttpPost("player/tutorials")]
    [HttpPost("player/tutorial/{tutorialId}")]
    [HttpPost("player/oobe")]
    [HttpPost("player/oobe/{tutorialId}")]
    [HttpPost("player/outofboxexperience")]
    [HttpPost("player/outofboxexperience/{tutorialId}")]
    [HttpPost("tutorial")]
    [HttpPost("tutorials")]
    [HttpPost("tutorial/{tutorialId}")]
    [HttpPost("oobe")]
    [HttpPost("oobe/{tutorialId}")]
    [HttpPost("outofboxexperience")]
    [HttpPost("outofboxexperience/{tutorialId}")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Endpoints cannot be static")]
    public ContentHttpResult CompleteTutorial(string? tutorialId = null)
        => EarthJson(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tutorialId"] = tutorialId,
            ["completed"] = true,
            ["updates"] = null
        });
}
