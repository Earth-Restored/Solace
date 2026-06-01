using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Utils;
using Solace.Common;

namespace Solace.ApiServer.Controllers.EarthApi;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}")]
internal sealed class TutorialController : ControllerBase
{
    [HttpGet("player/tutorial")]
    [HttpGet("player/tutorials")]
    [HttpGet("player/oobe")]
    [HttpGet("player/outofboxexperience")]
    [HttpGet("tutorial")]
    [HttpGet("tutorials")]
    [HttpGet("oobe")]
    [HttpGet("outofboxexperience")]
    public IActionResult GetTutorialState()
        => Content(Json.Serialize(new EarthApiResponse(new Dictionary<string, object>
        {
            ["completed"] = new Dictionary<string, bool>
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
        })), "application/json");

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
    public IActionResult CompleteTutorial(string? tutorialId = null)
        => Content(Json.Serialize(new EarthApiResponse(new Dictionary<string, object?>
        {
            ["tutorialId"] = tutorialId,
            ["completed"] = true,
            ["updates"] = null
        })), "application/json");
}
