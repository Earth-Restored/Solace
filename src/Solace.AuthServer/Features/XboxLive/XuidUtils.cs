using System.Text.RegularExpressions;

namespace Solace.AuthServer.Features.XboxLive;

public static partial class XuidUtils
{
    [GeneratedRegex(@"^xuid\((?<xuid>.*)\)$", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    public static partial Regex GetXuidRegex();
}
