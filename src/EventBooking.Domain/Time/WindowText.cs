namespace EventBooking.Domain.Time;

/// <summary>The one window formatter shared by emails and attendee pages.</summary>
public static class WindowText
{
    /// <summary>Formats a window in the location's zone with its abbreviation.</summary>
    /// <param name="date">The date.</param>
    /// <param name="start">The start time.</param>
    /// <param name="end">The end time.</param>
    /// <param name="locationName">The location name.</param>
    /// <param name="abbreviation">The zone abbreviation.</param>
    public static string Format(
        DateOnly date, TimeOnly start, TimeOnly end, string locationName, string abbreviation) =>
        $"{date:ddd dd MMM yyyy}, {start:HH:mm}-{end:HH:mm} {abbreviation} at {locationName}";
}
