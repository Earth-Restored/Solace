namespace Solace.ApiServer.Utils;

internal static class RequestExtensions
{
    public const string TimestampKey = "RequestStartedOn";

    extension(HttpContext context)
    {
        public long GetTimestamp()
            => ((DateTimeOffset)context.Items[TimestampKey]!).ToUnixTimeMilliseconds();
    }
}
