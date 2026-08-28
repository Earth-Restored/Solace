using System.Reflection;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Solace.WebPortal.Common.Features.About;

namespace Solace.WebPortal.Features.About;

[Handler]
[MapGet("/api/about/info")]
public static partial class GetInfo
{
    public sealed record Query;

    private static async ValueTask<ServerInfo> HandleAsync(
        Query _,
        CancellationToken cancellationToken
    )
        => new(Assembly.GetExecutingAssembly().GetName().Version!);
}
