using EventBooking.Domain.Events;
using EventBooking.Domain.Time;

namespace EventBooking.Domain.Tests.Events;

/// <summary>
/// Task 4: every "has it started / has it ended / is it today" rule is answered in the
/// <c>Location</c>'s zone (design 01 — Time). The domain asks an abstraction; NodaTime answers it
/// in Infrastructure.
/// </summary>
public class EventWindowZoneRuleTests
{
    // Tokyo is nine hours ahead of UTC with no daylight saving, so a UTC instant late on one day is
    // already the next local day: the rules cannot be satisfied by reading UTC.
    private const string Tokyo = "Asia/Tokyo";

    private static readonly EventWindow Window =
        new(new DateOnly(2026, 9, 14), new TimeOnly(9, 30), 90);

    private static readonly FixedOffsetZones Zones = new(TimeSpan.FromHours(9));

    private static DateTimeOffset Utc(int day, int hour, int minute = 0) =>
        new(new DateTime(2026, 9, day, hour, minute, 0, DateTimeKind.Utc));

    [Fact]
    public void TheStartAndEndInstantsComeFromTheZone()
    {
        Assert.Equal(Utc(14, 0, 30), Window.StartInstant(Zones, Tokyo));
        Assert.Equal(Utc(14, 2, 0), Window.EndInstant(Zones, Tokyo));
    }

    [Fact]
    public void TheWindowHasStartedFromItsStartInstantOnwards()
    {
        Assert.False(Window.HasStarted(Zones, Tokyo, Utc(14, 0, 29)));
        Assert.True(Window.HasStarted(Zones, Tokyo, Utc(14, 0, 30)));
        Assert.True(Window.HasStarted(Zones, Tokyo, Utc(14, 5)));
    }

    [Fact]
    public void TheWindowHasEndedFromItsEndInstantOnwards()
    {
        Assert.False(Window.HasEnded(Zones, Tokyo, Utc(14, 1, 59)));
        Assert.True(Window.HasEnded(Zones, Tokyo, Utc(14, 2)));
    }

    [Fact]
    public void TheEventDateIsTheLocalDateNotTheUtcDate()
    {
        // 23:30 UTC on the 13th is 08:30 on the 14th in Tokyo: the event's own date.
        Assert.True(Window.IsOnEventDate(Zones, Tokyo, Utc(13, 23, 30)));
        Assert.False(Window.IsOnEventDate(Zones, Tokyo, Utc(14, 15, 30)));
    }

    [Fact]
    public void AWindowWhoseStartOrEndHasNoUniqueInstantIsRejected()
    {
        var gapAtStart = new AwkwardZones(LocalTimeValidity.Gap, LocalTimeValidity.Unique);
        var ambiguousAtEnd = new AwkwardZones(LocalTimeValidity.Unique, LocalTimeValidity.Ambiguous);

        Assert.Equal(EventWindowZoneProblem.StartHasNoUniqueInstant, Window.ProblemIn(gapAtStart, Tokyo));
        Assert.Equal(EventWindowZoneProblem.EndHasNoUniqueInstant, Window.ProblemIn(ambiguousAtEnd, Tokyo));
        Assert.Equal(EventWindowZoneProblem.None, Window.ProblemIn(Zones, Tokyo));
    }

    [Fact]
    public void AnUnknownZoneIsItsOwnProblem()
    {
        Assert.Equal(EventWindowZoneProblem.UnknownZone, Window.ProblemIn(Zones, "Mars/Olympus_Mons"));
    }

    private sealed class FixedOffsetZones(TimeSpan offset) : IEventWindowZones
    {
        public bool IsKnownZone(string timeZoneId) => timeZoneId == Tokyo;

        public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            LocalTimeValidity.Unique;

        public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            new DateTimeOffset(date.ToDateTime(time), offset).ToUniversalTime();

        public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
            DateOnly.FromDateTime(instant.ToOffset(offset).DateTime);

        public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) => "JST";
    }

    private sealed class AwkwardZones(LocalTimeValidity start, LocalTimeValidity end) : IEventWindowZones
    {
        public bool IsKnownZone(string timeZoneId) => timeZoneId == Tokyo;

        public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            time == Window.StartTime ? start : end;

        public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            new(date.ToDateTime(time), TimeSpan.Zero);

        public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) => "JST";
    }
}
