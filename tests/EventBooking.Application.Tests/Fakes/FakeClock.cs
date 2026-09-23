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
    public DateTimeOffset NowAtTransitionalLocation => UtcNow;

    public DateOnly TodayAtTransitionalLocation => DateAtTransitionalLocation(UtcNow);

    public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => DateOnly.FromDateTime(instant.UtcDateTime);

    /// <inheritdoc/>
    // This fake treats transitional location as UTC, exactly as NowAtTransitionalLocation and DateAtTransitionalLocation do, so
    // application-layer tests stay deterministic without a real time-zone database.
    public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant.ToUniversalTime();

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}
