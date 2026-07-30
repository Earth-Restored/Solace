using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.Extensions.Options;
using Solace.WebPortal.Common;

namespace Solace.WebPortal.Features.Home;

[Handler]
[MapGet("/api/config/public-endpoints")]
public static partial class GetPublicEndpoints
{
    public sealed record Query;

    private static async ValueTask<PublicEndpointInfo> HandleAsync(
        Query _,
        IOptions<PublicEndpointInfo> endpointInfoOptions,
        CancellationToken cancellationToken
    )
        => endpointInfoOptions.Value;
}
