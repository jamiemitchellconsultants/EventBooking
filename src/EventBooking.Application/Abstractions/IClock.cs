namespace EventBooking.Application.Abstractions;

/// <summary>
/// The only source of "now" in the system. Production code never reads the ambient clock directly:
/// invite expiry, retry sweeps and event eligibility all have to be steerable from a test.
/// </summary>
public interface IClock
{
    /// <summary>Provides utc now within this contract.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Gets the current instant converted to the configured transitional-location time zone.</summary>
    DateTimeOffset NowAtTransitionalLocation { get; }

    /// <summary>Today's date at transitional location. The system is single-site by design.</summary>
    DateOnly TodayAtTransitionalLocation { get; }

    /// <summary>Converts an instant to the calendar date at transitional location.</summary>
    /// <param name="instant">The instant.</param>
    DateOnly DateAtTransitionalLocation(DateTimeOffset instant);

    /// <summary>Converts the given instant to transitional-location local time, preserving time of day.</summary>
    /// <param name="instant">The instant to convert.</param>
    /// <returns>The same instant expressed with the transitional-location time-zone offset.</returns>
    DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant);
}
