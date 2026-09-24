using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public static class TestContractFactory
{
    public static EventTimeDto EventTime(string _)
    {
        var start = new DateTimeOffset(2026, 10, 14, 9, 30, 0, TimeSpan.FromHours(1));
        return new EventTimeDto(
            new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 90,
            start, start.AddMinutes(90), start.ToUniversalTime(),
            start.AddMinutes(90).ToUniversalTime(), "Europe/London", "BST");
    }
}
