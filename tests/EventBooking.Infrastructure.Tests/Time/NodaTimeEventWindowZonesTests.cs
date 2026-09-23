using EventBooking.Domain.Events;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Time;

namespace EventBooking.Infrastructure.Tests.Time;

/// <summary>
/// Task 4: the IANA database answers the domain's zone questions. These cases use real daylight
/// saving transitions, so they fail if the provider or the resolution policy changes.
/// </summary>
public class NodaTimeEventWindowZonesTests
{
    private readonly NodaTimeEventWindowZones _zones = new();

    [Theory]
    [InlineData("Europe/London", true)]
    [InlineData("Europe/Dublin", true)]
    [InlineData("Asia/Tokyo", true)]
    [InlineData("Mars/Olympus_Mons", false)]
    [InlineData("", false)]
    public void OnlyRealZonesAreKnown(string timeZoneId, bool known)
    {
        Assert.Equal(known, _zones.IsKnownZone(timeZoneId));
    }

    [Fact]
    public void TheSpringForwardGapNamesNoInstant()
    {
        // British Summer Time begins at 01:00 on 29 March 2026: 01:30 never happens.
        Assert.Equal(LocalTimeValidity.Gap,
            _zones.ValidityOf(new DateOnly(2026, 3, 29), new TimeOnly(1, 30), "Europe/London"));
    }

    [Fact]
    public void TheAutumnOverlapNamesTwoInstants()
    {
        // Clocks go back at 02:00 on 25 October 2026: 01:30 happens twice.
        Assert.Equal(LocalTimeValidity.Ambiguous,
            _zones.ValidityOf(new DateOnly(2026, 10, 25), new TimeOnly(1, 30), "Europe/London"));
    }

    [Fact]
    public void AWindowEndingInTheGapIsRejectedEvenWhenItsStartIsFine()
    {
        var window = new EventWindow(new DateOnly(2026, 3, 29), new TimeOnly(0, 30), 60);

        Assert.Equal(EventWindowZoneProblem.EndHasNoUniqueInstant,
            window.ProblemIn(_zones, "Europe/London"));
    }

    [Fact]
    public void AnOrdinaryWindowResolvesToItsInstants()
    {
        var window = new EventWindow(new DateOnly(2026, 9, 14), new TimeOnly(9, 30), 90);

        Assert.Equal(EventWindowZoneProblem.None, window.ProblemIn(_zones, "Europe/London"));
        Assert.Equal(
            new DateTimeOffset(2026, 9, 14, 8, 30, 0, TimeSpan.Zero),
            window.StartInstant(_zones, "Europe/London").ToUniversalTime());
        Assert.Equal(
            new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero),
            window.EndInstant(_zones, "Europe/London").ToUniversalTime());
    }

    [Fact]
    public void TheSameLocalTimeInTwoZonesIsTwoDifferentInstants()
    {
        // London and Dublin share an offset, so they cannot show this; Tokyo can.
        var london = _zones.InstantOf(new DateOnly(2026, 9, 14), new TimeOnly(9, 0), "Europe/London");
        var tokyo = _zones.InstantOf(new DateOnly(2026, 9, 14), new TimeOnly(9, 0), "Asia/Tokyo");

        Assert.Equal(TimeSpan.FromHours(8), london.ToUniversalTime() - tokyo.ToUniversalTime());
    }

    [Fact]
    public void TheLocalDateOfAnInstantFollowsItsZone()
    {
        // 16:00 UTC is still the 13th in London (17:00 BST) but already the 14th in Tokyo (01:00).
        var instant = new DateTimeOffset(2026, 9, 13, 16, 0, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 9, 13), _zones.LocalDateOf(instant, "Europe/London"));
        Assert.Equal(new DateOnly(2026, 9, 14), _zones.LocalDateOf(instant, "Asia/Tokyo"));
    }

    [Fact]
    public void TheAbbreviationFollowsTheSeason()
    {
        Assert.Equal("BST", _zones.AbbreviationOf(
            new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero), "Europe/London"));
        Assert.Equal("GMT", _zones.AbbreviationOf(
            new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero), "Europe/London"));
    }

    [Fact]
    public void ResolvingATimeThatNamesNoUniqueInstantIsRefused()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _zones.InstantOf(new DateOnly(2026, 3, 29), new TimeOnly(1, 30), "Europe/London"));
    }
}
