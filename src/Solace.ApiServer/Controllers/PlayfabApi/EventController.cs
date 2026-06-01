using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Models.Playfab;
using Solace.Common.Utils;

namespace Solace.ApiServer.Controllers.PlayfabApi;

[Route("Event")]
[Route("20CA2.playfabapi.com/Event")]
internal sealed class EventController : SolaceControllerBase
{
    [HttpPost("WriteTelemetryEvents")]
    public ContentHttpResult WriteTelemetryEvents()
    {
        return JsonPascalCase(new PlayfabOkResponse(
            200,
            "OK",
            new Dictionary<string, object>()
            {
                ["AssignedEventIds"] = Array.Empty<string>(),
            }
        ));
    }
}
