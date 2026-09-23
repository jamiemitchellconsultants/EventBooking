namespace EventBooking.Web.Services;

/// <summary>
/// Formats UTC audit and delivery instants in the configured head-office local time zone.
/// </summary>
public sealed class HeadOfficeTimePresentation
{
    private readonly TimeZoneInfo _timeZone;
    private readonly string _timeZoneId;

    /// <summary>
    /// Creates presentation formatting for the configured IANA or operating-system time-zone identifier.
    /// </summary>
    /// <param name="timeZoneId">The configured head-office time-zone identifier.</param>
    public HeadOfficeTimePresentation(string timeZoneId)
    {
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        _timeZoneId = timeZoneId;
    }

    /// <summary>
    /// Converts a stored instant to head-office local time and includes its offset and configured zone identifier.
    /// </summary>
    /// <param name="timestamp">The stored instant to present without changing its point in time.</param>
    /// <returns>A local date and time with an unambiguous offset and zone identifier.</returns>
    public string Format(DateTimeOffset timestamp) =>
        $"{TimeZoneInfo.ConvertTime(timestamp, _timeZone):yyyy-MM-dd HH:mm zzz} ({_timeZoneId})";
}
