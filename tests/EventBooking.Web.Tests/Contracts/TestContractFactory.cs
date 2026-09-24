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

    public static EventTimeDto EventTimeAt(DateOnly date, TimeOnly time, int durationMinutes = 240)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        var local = date.ToDateTime(time);
        var offset = zone.GetUtcOffset(local);
        var start = new DateTimeOffset(local, offset);
        var abbreviation = zone.IsDaylightSavingTime(local) ? "BST" : "GMT";
        return new EventTimeDto(
            date, time, durationMinutes, start, start.AddMinutes(durationMinutes),
            start.ToUniversalTime(), start.AddMinutes(durationMinutes).ToUniversalTime(),
            "Europe/London", abbreviation);
    }
}
