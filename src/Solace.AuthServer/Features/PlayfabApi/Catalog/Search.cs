using System.Diagnostics;
using System.Text.Json.Serialization;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using OData2Linq;
using Solace.Common.Asp.Json;

namespace Solace.AuthServer.Features.PlayfabApi.Catalog;

[Handler]
[MapPost("Catalog/Search")]
[MapGroup<PlayfabApiGroup>]
public sealed partial class Search(
    CatalogService catalog,
    IHttpContextAccessor httpContextAccessor,
    ILogger<Search> logger
)
{
    [ForcePascalCase]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public sealed record Response(
        IEnumerable<CatalogItem> Items,
        string ConfigurationName,
        int? Count
    );

    private async ValueTask<Ok<OkResponse<Response>>> HandleAsync(
        SearchRequest query,
        CancellationToken cancellationToken
    )
    {
        var httpContext = httpContextAccessor.HttpContext;
        Debug.Assert(httpContext is not null);

        IEnumerable<CatalogItem> items;
        int? count = null;
        try
        {
            // doesn't make sense but the client requests it like this, somehow works on the original server
            var filter = query.Filter
                .Replace("platforms/any(tp: tp eq 'android.googleplay' and tp eq 'title.earth')", "platforms/any(tp: tp eq 'android.googleplay') and platforms/any(tp: tp eq 'title.earth')");

            var itemsQueryOData = catalog.CreateItemsQuery();

            itemsQueryOData = itemsQueryOData.Filter(filter);

            if (query.OrderBy is { } orderBy)
            {
                itemsQueryOData = itemsQueryOData.OrderBy(orderBy);
            }

            var itemsQuery = itemsQueryOData.ToOriginalQuery();

            if (query.Count)
            {
                count = itemsQuery.Count();
            }

            if (query.Skip is { } skip)
            {
                itemsQuery = itemsQuery.Skip(skip);
            }

            if (query.Top is { } top)
            {
                itemsQuery = itemsQuery.Take(top);
            }

            items = itemsQuery
                .ToArray()
                .Select(item => catalog.FixItemUrls(item, httpContext.Request));
        }
        catch (Exception exception)
        {
            LogSearchError(exception);
            items = [];
        }

        var response = new Response(
            items,
            "DEFAULT",
            count
        );

        httpContext.Response.Headers.Append("access-control-allow-credentials", "true");
        httpContext.Response.Headers.Append("access-control-allow-headers", "Content-Type, Content-Encoding, X-Authentication, X-Authorization, X-PlayFabSDK, X-ReportErrorAsSuccess, X-SecretKey, X-EntityToken, Authorization, x-ms-app, x-ms-client-request-id, x-ms-user-id, traceparent, tracestate, Request-Id");
        httpContext.Response.Headers.Append("access-control-allow-methods", "GET, POST");
        httpContext.Response.Headers.Append("access-control-allow-origin", "*");

        return TypedResults.Ok(new OkResponse<Response>(
            200,
            "OK",
            response
        ));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "An error occured while searching for items")]
    private partial void LogSearchError(Exception exception);
}