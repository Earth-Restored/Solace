#if USE_SHARED_LIBS
using System.Runtime.Loader;
#endif
namespace Solace.Locator;

internal sealed record EarthApiResponse(LocatorResponse Result, object Updates);
