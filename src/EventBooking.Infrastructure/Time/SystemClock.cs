using EventBooking.Application.Abstractions;

namespace EventBooking.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    private readonly TimeZoneInfo _transitionalLocation;

    public SystemClock(ClockOptions options)
    {
        // Resolved once, at startup: a bad configuration value should stop the host coming up
        // rather than fail the first time somebody reads the date.
        _transitionalLocation = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);
    }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc/>
    public DateTimeOffset NowAtTransitionalLocation => TimeZoneInfo.ConvertTime(UtcNow, _transitionalLocation);

    public DateOnly TodayAtTransitionalLocation => DateOnly.FromDateTime(NowAtTransitionalLocation.DateTime);

    public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => LocalDateOf(instant, _transitionalLocation);

    /// <inheritdoc/>
    public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, _transitionalLocation);

    public static DateOnly LocalDateOf(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
}
