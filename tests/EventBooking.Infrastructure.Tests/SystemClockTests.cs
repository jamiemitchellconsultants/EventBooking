using EventBooking.Infrastructure.Time;

namespace EventBooking.Infrastructure.Tests;

public class SystemClockTests
{
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    [Fact]
    public void TheLocalDateFollowsTheHeadOfficeZoneNotUtc()
    {
        // 23:30 UTC on 9 September is already 00:30 on 10 September in British Summer Time.
        var instant = new DateTimeOffset(2026, 9, 9, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 9, 10), SystemClock.LocalDateOf(instant, London));
    }

    [Fact]
    public void InWinterTheZoneMatchesUtc()
    {
        var instant = new DateTimeOffset(2026, 1, 9, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 1, 9), SystemClock.LocalDateOf(instant, London));
    }

    [Fact]
    public void TheClockReportsAUtcInstant()
    {
        var clock = new SystemClock(new HeadOfficeOptions("Europe/London"));

        Assert.Equal(TimeSpan.Zero, clock.UtcNow.Offset);
        Assert.InRange(
            clock.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public void AnUnknownTimeZoneFailsAtConstructionNotAtUseTime()
    {
        Assert.ThrowsAny<Exception>(() => new SystemClock(new HeadOfficeOptions("Mars/Olympus_Mons")));
    }

    /// <summary>Verifies a UTC instant renders with the head-office offset in British Summer Time.</summary>
    [Fact]
    public void InstantAtHeadOfficeConvertsAUtcInstantToLondonLocalTime()
    {
        var clock = new SystemClock(new HeadOfficeOptions("Europe/London"));
        var instant = new DateTimeOffset(2026, 9, 15, 8, 30, 0, TimeSpan.Zero);

        var local = clock.InstantAtHeadOffice(instant);

        Assert.Equal(TimeSpan.FromHours(1), local.Offset);
        Assert.Equal(new TimeOnly(9, 30), TimeOnly.FromDateTime(local.DateTime));
    }

    /// <summary>Verifies the London local date can run ahead of the UTC date late in the evening.</summary>
    [Fact]
    public void InstantAtHeadOfficeLocalDateCanDifferFromTheUtcDate()
    {
        var clock = new SystemClock(new HeadOfficeOptions("Europe/London"));
        // 23:30 UTC on 15 September is already 00:30 on 16 September in British Summer Time.
        var instant = new DateTimeOffset(2026, 9, 15, 23, 30, 0, TimeSpan.Zero);

        var local = clock.InstantAtHeadOffice(instant);

        Assert.Equal(new DateOnly(2026, 9, 16), DateOnly.FromDateTime(local.DateTime));
    }

    /// <summary>Verifies conversion re-expresses an instant rather than shifting it.</summary>
    [Fact]
    public void InstantAtHeadOfficePreservesTheSameInstant()
    {
        var clock = new SystemClock(new HeadOfficeOptions("Europe/London"));
        var instant = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.FromHours(-4));

        Assert.Equal(instant.UtcDateTime, clock.InstantAtHeadOffice(instant).UtcDateTime);
    }

    /// <summary>Verifies a winter instant carries the zero London offset.</summary>
    [Fact]
    public void InstantAtHeadOfficeUsesTheZeroOffsetInLondonWinter()
    {
        var clock = new SystemClock(new HeadOfficeOptions("Europe/London"));
        var instant = new DateTimeOffset(2026, 1, 15, 8, 30, 0, TimeSpan.Zero);

        var local = clock.InstantAtHeadOffice(instant);

        Assert.Equal(TimeSpan.Zero, local.Offset);
        Assert.Equal(new TimeOnly(8, 30), TimeOnly.FromDateTime(local.DateTime));
    }
}
