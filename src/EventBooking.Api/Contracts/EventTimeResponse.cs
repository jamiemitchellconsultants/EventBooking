using EventBooking.Domain.Time;

namespace EventBooking.Api.Contracts;

/// <summary>
/// The one representation of an EventWindow on the wire (design 05, "Times"). Every response
/// carrying a window uses this record, so no two endpoints can disagree about what 09:30
/// means. The end is derived from the duration in the location's own wall clock, never stored.
/// </summary>
/// <param name="Date">The local calendar date at the location.</param>
/// <param name="StartTime">The local start time of day.</param>
/// <param name="DurationMinutes">The window length in minutes.</param>
/// <param name="StartLocal">The start instant carrying the location's offset.</param>
/// <param name="EndLocal">The end instant carrying the location's offset.</param>
/// <param name="StartUtc">The start instant in UTC.</param>
/// <param name="EndUtc">The end instant in UTC.</param>
/// <param name="TimeZoneId">The location's IANA zone identifier.</param>
/// <param name="ZoneAbbreviation">The abbreviation in force at the start, for display.</param>
public sealed record EventTimeResponse(
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMinutes,
    DateTimeOffset StartLocal,
    DateTimeOffset EndLocal,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string TimeZoneId,
    string ZoneAbbreviation)
{
    /// <summary>Derives every member from a stored window and its location's zone.</summary>
    /// <param name="date">The local calendar date.</param>
    /// <param name="startTime">The local start time of day.</param>
    /// <param name="durationMinutes">The window length in minutes.</param>
    /// <param name="timeZoneId">The location's IANA zone identifier.</param>
    /// <param name="zones">The zone resolver.</param>
    /// <returns>The wire representation.</returns>
    public static EventTimeResponse From(
        DateOnly date, TimeOnly startTime, int durationMinutes, string timeZoneId,
        IEventWindowZones zones)
    {
        ArgumentNullException.ThrowIfNull(zones);

        // The attendee-facing window is wall clock, so the end is the local end time rather
        // than the start instant plus the duration. The two differ only across a daylight
        // saving change, and the EventWindow invariant already refuses a window whose end has
        // no unique instant, so this cannot silently produce a wrong answer.
        var endTime = startTime.Add(TimeSpan.FromMinutes(durationMinutes));
        var startLocal = zones.InstantOf(date, startTime, timeZoneId);
        var endLocal = zones.InstantOf(date, endTime, timeZoneId);

        return new EventTimeResponse(
            date,
            startTime,
            durationMinutes,
            startLocal,
            endLocal,
            startLocal.ToUniversalTime(),
            endLocal.ToUniversalTime(),
            timeZoneId,
            zones.AbbreviationOf(startLocal, timeZoneId));
    }
}
