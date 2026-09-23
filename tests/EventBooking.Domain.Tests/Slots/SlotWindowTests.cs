using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Domain.Tests.Slots;

public class SlotWindowTests
{
    private static SlotWindow Window(int day, int hour) =>
        new(new DateOnly(2026, 9, day), new TimeOnly(hour, 0));

    [Fact]
    public void EndTimeIsFourHoursAfterTheStart()
    {
        Assert.Equal(new TimeOnly(13, 0), Window(10, 9).EndTime);
        Assert.Equal(new TimeOnly(17, 0), Window(10, 13).EndTime);
    }

    [Fact]
    public void DurationIsAlwaysFourHours()
    {
        Assert.Equal(TimeSpan.FromHours(4), SlotWindow.Duration);
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
        var unsorted = new List<SlotWindow> { Window(12, 9), Window(10, 13), Window(10, 9) };

        unsorted.Sort();

        Assert.Equal(new List<SlotWindow> { Window(10, 9), Window(10, 13), Window(12, 9) }, unsorted);
    }

    [Fact]
    public void AStartTimeThatWouldRunPastMidnightIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(21, 0)));
        Assert.Equal("startTime must leave room for the full 4-hour window on the same day.", ex.Message);
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
