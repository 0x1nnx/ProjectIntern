using System.Globalization;

namespace ProjectIntern.GCommon;

public static class WorkingHoursParser
{
    private static readonly string[] TimeFormats = { "HH:mm", "HH:mm:ss" };

    public static TimeOnly? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!TimeOnly.TryParseExact(value, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly time))
            throw new InvalidOperationException($"Invalid time format: {value}");

        return time;
    }
}
