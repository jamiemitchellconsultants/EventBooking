namespace EventBooking.Application.Abstractions;

/// <summary>
/// The only source of "now" in the system. Production code never reads the ambient clock directly:
/// invite expiry, retry sweeps and slot eligibility all have to be steerable from a test.
/// </summary>
public interface IClock
{
    /// <summary>Provides utc now within this contract.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Gets the current instant converted to the configured head-office time zone.</summary>
    DateTimeOffset NowAtHeadOffice { get; }

    /// <summary>Today's date at head office. The system is single-site by design.</summary>
    DateOnly TodayAtHeadOffice { get; }

    /// <summary>Converts an instant to the calendar date at head office.</summary>
    /// <param name="instant">The instant.</param>
    DateOnly DateAtHeadOffice(DateTimeOffset instant);

    /// <summary>Converts the given instant to head-office local time, preserving time of day.</summary>
    /// <param name="instant">The instant to convert.</param>
    /// <returns>The same instant expressed with the head-office time-zone offset.</returns>
    DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant);
}
