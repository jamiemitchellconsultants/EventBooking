namespace EventBooking.Domain.Locations;

/// <summary>
/// How much live scheduling a location carries. The caller counts; the aggregate decides.
/// </summary>
/// <param name="OpenProposals">Open proposals hosted at the location.</param>
/// <param name="FutureEvents">Active events at the location whose window has not passed.</param>
public readonly record struct LocationUsage(int OpenProposals, int FutureEvents)
{
    /// <summary>A location nothing is scheduled against.</summary>
    public static LocationUsage None => new(0, 0);

    /// <summary>Whether anything live depends on this location.</summary>
    public bool Any => OpenProposals > 0 || FutureEvents > 0;

    /// <summary>The blocking counts, for a refusal a screen can render.</summary>
    public IReadOnlyDictionary<string, int> AsBlocking() =>
        new Dictionary<string, int>
        {
            ["openProposals"] = OpenProposals,
            ["futureEvents"] = FutureEvents,
        };
}
