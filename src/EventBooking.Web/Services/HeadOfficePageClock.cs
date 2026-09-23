namespace EventBooking.Web.Services;

/// <summary>Supplies browser action gating in the configured head-office time zone.</summary>
public sealed class HeadOfficePageClock
{
    private readonly TimeZoneInfo _zone;
    private readonly Func<DateTimeOffset> _utcNow;

    /// <summary>Creates a clock for one IANA or operating-system time-zone identifier.</summary>
    /// <param name="timeZoneId">The configured head-office time-zone identifier.</param>
    /// <param name="utcNow">An optional UTC source used by deterministic tests.</param>
    public HeadOfficePageClock(string timeZoneId, Func<DateTimeOffset>? utcNow = null)
    {
        _zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Gets the current instant represented in the head-office time zone.</summary>
    public DateTimeOffset NowAtHeadOffice => TimeZoneInfo.ConvertTime(_utcNow(), _zone);
}
