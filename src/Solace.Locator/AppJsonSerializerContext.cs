#if USE_SHARED_LIBS
using System.Runtime.Loader;
#endif
using System.Text.Json.Serialization;

namespace Solace.Locator;

[JsonSerializable(typeof(EarthApiResponse))]
[JsonSerializable(typeof(LocatorResponse))]
[JsonSerializable(typeof(Dictionary<string, LocatorResponse.Environment>))]
[JsonSerializable(typeof(Dictionary<string, List<string>>))]
internal sealed partial class AppJsonSerializerContext : JsonSerializerContext
{
}