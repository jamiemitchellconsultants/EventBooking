// tests/EventBooking.Infrastructure.Tests/SystemClockTests.cs (complete)
using EventBooking.Infrastructure.Time;

namespace EventBooking.Infrastructure.Tests;

public sealed class SystemClockTests
{
    [Fact]
    public void ClockReportsAUtcInstant()
    {
        var before = DateTimeOffset.UtcNow;
        var actual = new SystemClock().UtcNow;
        var after = DateTimeOffset.UtcNow;
        Assert.Equal(TimeSpan.Zero, actual.Offset);
        Assert.InRange(actual, before, after);
    }
}
