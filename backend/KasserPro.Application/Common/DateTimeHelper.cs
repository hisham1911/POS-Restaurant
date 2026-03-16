namespace KasserPro.Application.Common;

/// <summary>
/// Converts user-provided Egypt local dates to UTC boundaries for database queries.
/// Egypt Standard Time is UTC+2 (no DST since 2014).
/// </summary>
public static class DateTimeHelper
{
    private static readonly TimeZoneInfo EgyptTimeZone = GetEgyptTimeZone();

    private static TimeZoneInfo GetEgyptTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
    }

    /// <summary>
    /// Converts a local date range (Egypt time) to UTC boundaries.
    /// Returns (utcFrom, utcTo) where utcTo is exclusive (start of next day).
    /// </summary>
    public static (DateTime UtcFrom, DateTime UtcTo) ToUtcRange(DateTime fromDate, DateTime toDate)
    {
        var localFrom = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Unspecified);
        var localTo = DateTime.SpecifyKind(toDate.Date.AddDays(1), DateTimeKind.Unspecified);

        return (
            TimeZoneInfo.ConvertTimeToUtc(localFrom, EgyptTimeZone),
            TimeZoneInfo.ConvertTimeToUtc(localTo, EgyptTimeZone)
        );
    }

    /// <summary>
    /// Converts a single date (Egypt time) to UTC boundaries for that day.
    /// </summary>
    public static (DateTime UtcFrom, DateTime UtcTo) ToUtcDayRange(DateTime date)
    {
        return ToUtcRange(date, date);
    }
}
