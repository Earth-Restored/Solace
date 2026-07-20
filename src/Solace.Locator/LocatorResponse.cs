#if USE_SHARED_LIBS
using System.Runtime.Loader;
#endif
namespace Solace.Locator;

internal sealed record LocatorResponse(
    Dictionary<string, LocatorResponse.Environment> ServiceEnvironments,
    Dictionary<string, List<string>> SupportedEnvironments
)
{
    internal sealed record Environment(string ServiceUri, string CdnUri, string PlayfabTitleId);
}
