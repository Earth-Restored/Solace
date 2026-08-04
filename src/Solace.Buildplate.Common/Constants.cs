using System.Text.RegularExpressions;

namespace Solace.Buildplate.Common;

public static partial class Constants
{
    public static readonly Version GameVersion = new(1, 20, 5);

    [GeneratedRegex(@"Done \((?<time>.*?)\)! For help, type ""help""", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    public static partial Regex GetServerStartedRegex();
}
