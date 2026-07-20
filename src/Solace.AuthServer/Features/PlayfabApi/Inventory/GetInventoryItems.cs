using System.Text.Json.Serialization;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Solace.Common.Asp.Json;

namespace Solace.AuthServer.Features.PlayfabApi.Inventory;

[Handler]
[MapPost("inventory/GetInventoryItems")]
[MapGroup<PlayfabApiGroup>]
public static partial class GetInventoryItems
{
    [ForcePascalCase]
    public sealed record Query(
        ReceiptData ReceiptData
    );

    [ForcePascalCase]
    public sealed record ReceiptData(
        string DeviceId
    );

    [ForcePascalCase]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public sealed record Response(
        object[] Items,
        string ETag,
        object[] ItemMetadata,
        object[] Subscriptions,
        string? Receipt
    );

    private static async ValueTask<Ok<OkResponse<Response>>> HandleAsync(
        Query _,
        CancellationToken cancellationToken
    )
        => TypedResults.Ok(new OkResponse<Response>(
            200,
            "OK",
            new Response(
                [],
                "1/MQ==",
                [],
                [],
                null
            )
        ));
}