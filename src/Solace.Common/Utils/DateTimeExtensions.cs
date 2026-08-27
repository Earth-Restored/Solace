namespace Solace.Common.Utils;

public static class DateTimeExtensions
{
    extension(DateTime dateTime)
    {
        public long ToUnixTimeMilliseconds()
            => new DateTimeOffset(dateTime).ToUnixTimeMilliseconds();
    }
}
