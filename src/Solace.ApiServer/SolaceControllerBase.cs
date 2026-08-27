using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Solace.ApiServer.Utils;
using System.Security.Claims;

namespace Solace.ApiServer;

[ApiController]
internal abstract class SolaceControllerBase : ControllerBase
{
    private static Config Config => Program.config;

    // todo: JsonHttpResult<EarthApiResponse>
    protected static ContentHttpResult EarthJson(object results)
        => JsonCamelCase(new EarthApiResponse(results));

    // todo: JsonHttpResult<EarthApiResponse>
    protected static ContentHttpResult EarthJson(object? results, EarthApiResponse.UpdatesResponse? updates)
        => JsonCamelCase(new EarthApiResponse(results, updates));

    protected static ContentHttpResult JsonCamelCase(object value)
        => TypedResults.Content(Common.Json.Serialize(value), "application/json");

    protected static ContentHttpResult JsonPascalCase(object value)
        => TypedResults.Content(JsonSerializer.Serialize(value), "application/json");

    protected bool TryGetAccountId(out Guid accountId)
    {
        string? playerIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(playerIdString))
        {
            accountId = default;
            return false;
        }

        return Guid.TryParse(playerIdString, out accountId);
    }
}
