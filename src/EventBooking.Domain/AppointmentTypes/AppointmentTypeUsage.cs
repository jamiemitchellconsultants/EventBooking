namespace EventBooking.Domain.AppointmentTypes;

/// <summary>
/// How much live use an appointment type carries. The caller counts; the aggregate decides.
/// </summary>
/// <param name="OpenProposals">Open proposals listing the type.</param>
/// <param name="FutureEvents">Active events listing the type whose window has not passed.</param>
/// <param name="ActiveGroups">Active attendee groups mapping the type.</param>
public readonly record struct AppointmentTypeUsage(int OpenProposals, int FutureEvents, int ActiveGroups)
{
    /// <summary>A type nothing depends on.</summary>
    public static AppointmentTypeUsage None => new(0, 0, 0);

    /// <summary>Whether anything live depends on this type.</summary>
    public bool Any => OpenProposals > 0 || FutureEvents > 0 || ActiveGroups > 0;

    /// <summary>The blocking counts, for a refusal a screen can render.</summary>
    public IReadOnlyDictionary<string, int> AsBlocking() =>
        new Dictionary<string, int>
        {
            ["openProposals"] = OpenProposals,
            ["futureEvents"] = FutureEvents,
            ["activeGroups"] = ActiveGroups,
        };
}
