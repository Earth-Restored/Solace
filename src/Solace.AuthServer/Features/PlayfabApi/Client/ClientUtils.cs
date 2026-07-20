using System.Text.RegularExpressions;

namespace Solace.AuthServer.Features.PlayfabApi.Client;

public static partial class ClientUtils
{
    [GeneratedRegex("^[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}-(.*)$")]
    public static partial Regex GetAuthRegex();
}