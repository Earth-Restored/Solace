using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Solace.Common.Asp.Json;

namespace Solace.AuthServer.Features.PlayfabApi.Inventory;

[Handler]
[MapPost("inventory/GetVirtualCurrencies")]
[MapGroup<PlayfabApiGroup>]
public static partial class GetVirtualCurrencies
{
    public sealed record Query;

    [ForcePascalCase]
    public sealed record Response(
        Currency[] Currencies,
        object[] Items
    );

    [ForcePascalCase]
    public sealed record Currency(
        Guid CurrencyId,
        long Amount,
        long ChangedAmount
    );

    private static async ValueTask<Ok<OkResponse<Response>>> HandleAsync(
        [AsParameters] Query _, // allow empty body
        CancellationToken cancellationToken
    )
        => TypedResults.Ok(new OkResponse<Response>(
            200,
            "OK",
            new Response(
                [
                    new Currency(
                        PlayfabApiUtils.MinecoinCurrencyId,
                        0,
                        0
                    )
                ],
                []
            )
        ));
}