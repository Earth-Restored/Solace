using System.Globalization;

namespace Solace.ApiServer.Utils;

public static class TimeFormatter
{
    private static readonly string JSON_DATE_FORMAT = "yyyy-MM-ddTHH:mm:ssZ";

    public static string FormatTime(long time)
        => FormatTime(DateTimeOffset.FromUnixTimeMilliseconds(time));

    public static string FormatTime(DateTimeOffset dateTime)
        => dateTime.UtcDateTime.ToString(JSON_DATE_FORMAT, CultureInfo.InvariantCulture);

    public static string FormatDuration(long duration)
        => FormatDuration(TimeSpan.FromMilliseconds(duration));

    public static string FormatDuration(TimeSpan timeSpan)
        => $"{(int)timeSpan.TotalHours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";

    public static DateTimeOffset ParseTime(string time)
        => DateTimeOffset.Parse(time, CultureInfo.InvariantCulture);

    public static TimeSpan ParseDuration(string duration)
    {
        var parts = duration.Split(':');
        if (parts.Length < 3)
        {
            throw new ArgumentException("Invalid duration format", nameof(duration));
        }

        var hours = long.Parse(parts[0], CultureInfo.InvariantCulture);
        var minutes = long.Parse(parts[1], CultureInfo.InvariantCulture);
        var seconds = long.Parse(parts[2], CultureInfo.InvariantCulture);

        return TimeSpan.FromSeconds(hours * 3600 + minutes * 60 + seconds);
    }
}
