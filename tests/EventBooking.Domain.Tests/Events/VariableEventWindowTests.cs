using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

/// <summary>
/// Task 4: the fixed four-hour window becomes an explicit duration in 15-minute steps that must
/// end on the local date it starts (design 01 — Time; boundary values in design 08).
/// </summary>
public class VariableEventWindowTests
{
    private static EventWindow Window(int durationMinutes, int hour = 9, int minute = 0) =>
        new(new DateOnly(2026, 9, 14), new TimeOnly(hour, minute), durationMinutes);

    [Theory]
    [InlineData(15)]
    [InlineData(90)]
    [InlineData(240)]
    [InlineData(720)]
    public void AnyQuarterHourDurationUpToTwelveHoursIsAccepted(int durationMinutes)
    {
        Assert.Equal(durationMinutes, Window(durationMinutes).DurationMinutes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(725)]
    [InlineData(735)]
    public void ADurationOutsideTheQuarterHourRangeIsRefused(int durationMinutes)
    {
        Assert.Throws<DomainException>(() => Window(durationMinutes));
    }

    [Fact]
    public void TheEndTimeIsDerivedFromTheDuration()
    {
        Assert.Equal(new TimeOnly(11, 0), Window(90, 9, 30).EndTime);
        Assert.Equal(new TimeOnly(17, 0), Window(240, 13).EndTime);
    }

    [Fact]
    public void AWindowThatWouldCrossLocalMidnightIsRefused()
    {
        Assert.Throws<DomainException>(() => Window(90, 23));
        Assert.Throws<DomainException>(() => Window(90, 22, 30));
    }

    [Fact]
    public void AWindowThatEndsBeforeLocalMidnightIsAccepted()
    {
        Assert.Equal(new TimeOnly(23, 45), Window(90, 22, 15).EndTime);
    }

    [Fact]
    public void WindowsWithTheSameDateAndStartButDifferentDurationsDiffer()
    {
        Assert.NotEqual(Window(90), Window(120));
    }

    [Fact]
    public void OrderingStaysByDateThenStartTime()
    {
        var early = new EventWindow(new DateOnly(2026, 9, 14), new TimeOnly(9, 0), 480);
        var late = new EventWindow(new DateOnly(2026, 9, 14), new TimeOnly(9, 30), 15);

        Assert.True(early.CompareTo(late) < 0);
    }
}
