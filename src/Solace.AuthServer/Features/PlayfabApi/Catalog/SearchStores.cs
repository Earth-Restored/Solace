using System.Diagnostics;
using System.Text.Json;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Solace.Common.Asp.Json;

namespace Solace.AuthServer.Features.PlayfabApi.Catalog;

[Handler]
[MapPost("Catalog/SearchStores")]
[MapGroup<PlayfabApiGroup>]
public static partial class SearchStores
{
    [ForcePascalCase]
    public sealed record Response(
        object[] Stores,
        string ConfigurationName,
        int? Count
    );

    private static async ValueTask<Ok<OkResponse<Response>>> HandleAsync(
        SearchRequest _,
        CancellationToken cancellationToken
    )
        => TypedResults.Ok(new OkResponse<Response>(
            200,
            "OK",
            new Response(
                [],
                "DEFAULT",
                0
            )
        ));
}