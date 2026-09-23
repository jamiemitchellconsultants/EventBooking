namespace EventBooking.Infrastructure.Time;

/// <summary>
/// The one time zone every date rule uses until Task 4 gives each <c>Location</c> its own zone.
/// An identifier the host operating system recognises.
/// </summary>
/// <param name="TimeZoneId">The IANA time-zone identifier.</param>
public sealed record ClockOptions(string TimeZoneId);
