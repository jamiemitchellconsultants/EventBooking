using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventWindowTests
{
    private static EventWindow Window(int day, int hour, int durationMinutes = 240) =>
        new(new DateOnly(2026, 9, day), new TimeOnly(hour, 0), durationMinutes);

    [Fact]
    public void EndTimeFollowsTheStatedDuration()
    {
        Assert.Equal(new TimeOnly(13, 0), Window(10, 9).EndTime);
        Assert.Equal(new TimeOnly(17, 0), Window(10, 13).EndTime);
    }

    [Fact]
    public void TheDurationIsStatedByTheCaller()
    {
        Assert.Equal(90, Window(10, 9, 90).DurationMinutes);
        Assert.Equal(new TimeOnly(10, 30), Window(10, 9, 90).EndTime);
    }

    [Fact]
    public void TwoWindowsWithTheSameDateAndStartAreEqual()
    {
        Assert.Equal(Window(10, 9), Window(10, 9));
        Assert.NotEqual(Window(10, 9), Window(10, 13));
    }

    [Fact]
    public void WindowsSortByDateThenStartTime()
    {
        var unsorted = new List<EventWindow> { Window(12, 9), Window(10, 13), Window(10, 9) };

        unsorted.Sort();

        Assert.Equal(new List<EventWindow> { Window(10, 9), Window(10, 13), Window(12, 9) }, unsorted);
    }

    [Fact]
    public void AStartTimeThatWouldRunPastMidnightIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(21, 0), 240));
        Assert.Equal("The window must end on the local date it starts.", ex.Message);
    }

    [Fact]
    public void StartsAfterComparesOnDateOnly()
    {
        Assert.True(Window(10, 9).StartsAfter(new DateOnly(2026, 9, 9)));
        Assert.False(Window(10, 9).StartsAfter(new DateOnly(2026, 9, 10)));
        Assert.False(Window(10, 9).StartsAfter(new DateOnly(2026, 9, 11)));
    }

    [Fact]
    public void ToStringRendersTheWindowForEmailAndAuditText()
    {
        Assert.Equal("2026-09-10 09:00-13:00", Window(10, 9).ToString());
    }
}
