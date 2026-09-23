namespace EventBooking.Domain.Time;

/// <summary>
/// Whether one local wall-clock time names exactly one instant in a zone. A daylight-saving jump
/// forward leaves a gap where it names none; a jump back leaves an overlap where it names two.
/// </summary>
public enum LocalTimeValidity
{
    /// <summary>The local time names exactly one instant.</summary>
    Unique,

    /// <summary>The local time falls in a daylight-saving gap and names no instant.</summary>
    Gap,

    /// <summary>The local time falls in a daylight-saving overlap and names two instants.</summary>
    Ambiguous,
}

/// <summary>Why an <c>EventWindow</c> cannot be interpreted in a given zone.</summary>
public enum EventWindowZoneProblem
{
    /// <summary>The window names one unambiguous span of time in that zone.</summary>
    None,

    /// <summary>The zone identifier is not one the host recognises.</summary>
    UnknownZone,

    /// <summary>The start falls in a daylight-saving gap or overlap.</summary>
    StartHasNoUniqueInstant,

    /// <summary>The end falls in a daylight-saving gap or overlap.</summary>
    EndHasNoUniqueInstant,
}

/// <summary>
/// The time-zone questions the domain has to ask to interpret an <c>EventWindow</c> at its
/// <c>Location</c>. The domain owns the questions because its rules depend on the answers; the
/// IANA database that answers them lives in infrastructure.
/// </summary>
public interface IEventWindowZones
{
    /// <summary>Whether the identifier names a zone this host can resolve.</summary>
    /// <param name="timeZoneId">The IANA time-zone identifier.</param>
    bool IsKnownZone(string timeZoneId);

    /// <summary>Whether a local date and time names exactly one instant in the zone.</summary>
    /// <param name="date">The local date.</param>
    /// <param name="time">The local time of day.</param>
    /// <param name="timeZoneId">The IANA time-zone identifier.</param>
    LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId);

    /// <summary>Converts a local date and time in the zone to the instant it names.</summary>
    /// <param name="date">The local date.</param>
    /// <param name="time">The local time of day.</param>
    /// <param name="timeZoneId">The IANA time-zone identifier.</param>
    DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId);

    /// <summary>The calendar date an instant falls on, in the zone.</summary>
    /// <param name="instant">The instant.</param>
    /// <param name="timeZoneId">The IANA time-zone identifier.</param>
    DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId);

    /// <summary>The zone abbreviation in force at an instant, for display.</summary>
    /// <param name="instant">The instant.</param>
    /// <param name="timeZoneId">The IANA time-zone identifier.</param>
    string AbbreviationOf(DateTimeOffset instant, string timeZoneId);
}
