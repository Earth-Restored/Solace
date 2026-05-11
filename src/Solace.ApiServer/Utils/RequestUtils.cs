namespace Solace.ApiServer.Utils;

internal static class RequestUtils
{
    public const string TimestampKey = "RequestStartedOn";

    public static long GetTimestamp(this HttpContext context)
        => ((DateTimeOffset)context.Items[TimestampKey]!).ToUnixTimeMilliseconds();
}
