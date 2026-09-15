using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Security.Claims;

namespace Solace.ApiServer;

[ApiController]
internal abstract class SolaceControllerBase : ControllerBase
{
    protected static ContentHttpResult JsonCamelCase(object value)
        => TypedResults.Content(Common.Json.Serialize(value), "application/json");

    protected static ContentHttpResult JsonPascalCase(object value)
        => TypedResults.Content(JsonSerializer.Serialize(value), "application/json");

    protected bool TryGetProfileId(out Guid accountId)
    {
        var playerIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(playerIdString))
        {
            accountId = default;
            return false;
        }

        return Guid.TryParse(playerIdString, out accountId);
    }
}
