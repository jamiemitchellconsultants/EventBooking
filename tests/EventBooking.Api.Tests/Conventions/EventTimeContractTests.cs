using EventBooking.Api.Contracts;
using EventBooking.Infrastructure.Time;

namespace EventBooking.Api.Tests.Conventions;

/// <summary>
/// The one event-time representation, proved in two zones. London and Tokyo, not London and
/// Dublin: contradiction #10 — two zones sharing an offset cannot demonstrate that the
/// conversion is zone-dependent at all.
/// </summary>
public sealed class EventTimeContractTests
{
    private static readonly NodaTimeEventWindowZones Zones = new();

    [Fact]
    public void ALondonSummerWindowCarriesItsOffsetAndAbbreviation()
    {
        var time = EventTimeResponse.From(
            new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 90, "Europe/London", Zones);

        Assert.Equal(new DateOnly(2026, 10, 14), time.Date);
        Assert.Equal(new TimeOnly(9, 30), time.StartTime);
        Assert.Equal(90, time.DurationMinutes);
        Assert.Equal(TimeSpan.FromHours(1), time.StartLocal.Offset);
        Assert.Equal(new TimeOnly(11, 0), TimeOnly.FromTimeSpan(time.EndLocal.TimeOfDay));
        Assert.Equal(TimeSpan.Zero, time.StartUtc.Offset);
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 8, 30, 0, TimeSpan.Zero), time.StartUtc);
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero), time.EndUtc);
        Assert.Equal("Europe/London", time.TimeZoneId);
        Assert.Equal("BST", time.ZoneAbbreviation);
    }

    [Fact]
    public void TheSameWallClockInTokyoIsADifferentInstant()
    {
        var london = EventTimeResponse.From(
            new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 90, "Europe/London", Zones);
        var tokyo = EventTimeResponse.From(
            new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 90, "Asia/Tokyo", Zones);

        Assert.Equal(london.Date, tokyo.Date);
        Assert.Equal(london.StartTime, tokyo.StartTime);
        Assert.NotEqual(london.StartUtc, tokyo.StartUtc);
        Assert.Equal(TimeSpan.FromHours(9), tokyo.StartLocal.Offset);
    }

    [Fact]
    public void AWinterLondonWindowCarriesTheWinterAbbreviation()
    {
        var time = EventTimeResponse.From(
            new DateOnly(2026, 12, 2), new TimeOnly(9, 30), 60, "Europe/London", Zones);

        Assert.Equal("GMT", time.ZoneAbbreviation);
        Assert.Equal(TimeSpan.Zero, time.StartLocal.Offset);
    }

    /// <summary>The end is derived, never stored, so it moves with the duration alone.</summary>
    [Fact]
    public void TheEndIsDerivedFromTheDuration()
    {
        var shorter = EventTimeResponse.From(
            new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 15, "Europe/London", Zones);
        var longer = EventTimeResponse.From(
            new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 720, "Europe/London", Zones);

        Assert.Equal(shorter.StartUtc, longer.StartUtc);
        Assert.Equal(TimeSpan.FromMinutes(15), shorter.EndUtc - shorter.StartUtc);
        Assert.Equal(TimeSpan.FromMinutes(720), longer.EndUtc - longer.StartUtc);
    }
}
