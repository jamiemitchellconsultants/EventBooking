using EventBooking.Application.Abstractions;

namespace EventBooking.Application.Tests.Fakes;

public sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset? utcNow = null)
    {
        UtcNow = utcNow ?? new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
    }

    public DateTimeOffset UtcNow { get; set; }

    /// <inheritdoc/>
    public DateTimeOffset NowAtHeadOffice => UtcNow;

    public DateOnly TodayAtHeadOffice => DateAtHeadOffice(UtcNow);

    public DateOnly DateAtHeadOffice(DateTimeOffset instant) => DateOnly.FromDateTime(instant.UtcDateTime);

    /// <inheritdoc/>
    // This fake treats head office as UTC, exactly as NowAtHeadOffice and DateAtHeadOffice do, so
    // application-layer tests stay deterministic without a real time-zone database.
    public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) => instant.ToUniversalTime();

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}
