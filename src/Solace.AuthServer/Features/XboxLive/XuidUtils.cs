using System.Text.RegularExpressions;

namespace Solace.AuthServer.Features.XboxLive;

public static partial class XuidUtils
{
    [GeneratedRegex(@"^xuid\((.*)\)$")]
    public static partial Regex GetXuidRegex();
}
