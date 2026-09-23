using EventBooking.Application.Abstractions;

namespace EventBooking.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    private readonly TimeZoneInfo _headOffice;

    public SystemClock(HeadOfficeOptions options)
    {
        // Resolved once, at startup: a bad configuration value should stop the host coming up
        // rather than fail the first time somebody reads the date.
        _headOffice = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);
    }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc/>
    public DateTimeOffset NowAtHeadOffice => TimeZoneInfo.ConvertTime(UtcNow, _headOffice);

    public DateOnly TodayAtHeadOffice => DateOnly.FromDateTime(NowAtHeadOffice.DateTime);

    public DateOnly DateAtHeadOffice(DateTimeOffset instant) => LocalDateOf(instant, _headOffice);

    /// <inheritdoc/>
    public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, _headOffice);

    public static DateOnly LocalDateOf(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
}
