using System.Net;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Types;
using Solace.ApiServer.Utils;
using Solace.Common;

namespace Solace.ApiServer.Controllers.EarthApi;

[ApiController]
[ApiVersion("1.0")]
[ApiVersion("1.1")]
internal sealed partial class LocatorController : ControllerBase
{
    private readonly ILogger<LocatorController> _logger;

    public LocatorController(ILogger<LocatorController> logger)
    {
        _logger = logger;
    }

    [HttpGet("player/environment")]
    [HttpGet("api/v1.1/player/environment")]
    public ContentResult Get()
    {
        string protocol = Request.IsHttps ? "https://" : "http://";
        string baseServerIP = $"{protocol}{Request.Host.Value}";
        
        LogIssuedLocator(HttpContext.Connection.RemoteIpAddress, baseServerIP);

        string resp = Json.Serialize(new EarthApiResponse(new LocatorResponse(new()
        {
            ["production"] = new LocatorResponse.Environment(baseServerIP, baseServerIP + "/cdn", "20CA2"),
        },
        new()
        {
            ["2020.1217.02"] = ["production"],
            ["2020.1210.01"] = ["production"],
        }
        )));

        return Content(resp, "application/json");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{RemoteIp} has issued locator, replying with {ServerIp}")]
    private partial void LogIssuedLocator(IPAddress? RemoteIp, string ServerIp);
}