using System.Diagnostics;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Solace.Common.Asp.Json;

namespace Solace.AuthServer.Features.PlayfabApi.Catalog;

[Handler]
[MapPost("Catalog/GetPublishedItem")]
[MapGroup<PlayfabApiGroup>]
public sealed partial class GetPublishedItem(
    CatalogService catalog,
    IHttpContextAccessor httpContextAccessor
)
{
    [ForcePascalCase]
    public sealed record Query(
        string? ItemId
    );

    [ForcePascalCase]
    public sealed record Response(
        CatalogItem Item
    );

    private async ValueTask<Results<Ok<OkResponse<Response>>, Ok<ErrorResponse>, NotFound>> HandleAsync(
        Query query,
        CancellationToken cancellationToken
    )
    {
        var httpContext = httpContextAccessor.HttpContext;
        Debug.Assert(httpContext is not null);

        if (!Guid.TryParse(query.ItemId, out var itemId))
        {
            return TypedResults.Ok(new ErrorResponse(
                400,
                "BadRequest",
                "InvalidParams",
                1000,
                "Invalid input parameters",
                new(StringComparer.Ordinal)
                {
                    ["ItemId"] = ["The ItemId field is required."]
                }
            ));
        }

        if (!catalog.TryGetItem(itemId, out var item))
        {
            // TODO: fake not found
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new OkResponse<Response>(
            200,
            "OK",
            new Response(
                catalog.FixItemUrls(item, httpContext.Request)
            )
        ));
    }
}