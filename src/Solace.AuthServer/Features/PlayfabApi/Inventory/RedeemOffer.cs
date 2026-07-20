using System.Text.Json.Serialization;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Solace.Common.Asp.Json;

namespace Solace.AuthServer.Features.PlayfabApi.Inventory;

[Handler]
[MapPost("inventory/redeem")]
[MapGroup<PlayfabApiGroup>]
public static partial class RedeemOffer
{
    [ForcePascalCase]
    public sealed record Command(
        MarketplaceData MarketplaceData,
        string TargetMarketplace
    );

    [ForcePascalCase]
    public sealed record MarketplaceData(
        string XboxToken,
        [property: JsonPropertyName("userId")] string UserId
    );

    [ForcePascalCase]
    public sealed record Response(
        RedeemedOffer[] Succeeded,
        object[] Failed
    );

    [ForcePascalCase]
    public sealed record RedeemedOffer(
        string OfferId,
        DateTime RedeemTimeStamp,
        Guid MarketplaceTransactionId,
        object[] Items
    );

    private static async ValueTask<Ok<OkResponse<Response>>> HandleAsync(
        Command _,
        CancellationToken cancellationToken
    )
        => TypedResults.Ok(new OkResponse<Response>(
            200,
            "OK",
            new Response(
                [],
                []
            )
        ));
}