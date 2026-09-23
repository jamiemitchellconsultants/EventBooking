namespace EventBooking.Web.Services;

/// <summary>Supplies browser action gating in the configured transitional-location time zone.</summary>
public sealed class TransitionalLocationPageClock
{
    private readonly TimeZoneInfo _zone;
    private readonly Func<DateTimeOffset> _utcNow;

    /// <summary>Creates a clock for one IANA or operating-system time-zone identifier.</summary>
    /// <param name="timeZoneId">The configured transitional-location time-zone identifier.</param>
    /// <param name="utcNow">An optional UTC source used by deterministic tests.</param>
    public TransitionalLocationPageClock(string timeZoneId, Func<DateTimeOffset>? utcNow = null)
    {
        _zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Gets the current instant represented in the transitional-location time zone.</summary>
    public DateTimeOffset NowAtTransitionalLocation => TimeZoneInfo.ConvertTime(_utcNow(), _zone);
}
