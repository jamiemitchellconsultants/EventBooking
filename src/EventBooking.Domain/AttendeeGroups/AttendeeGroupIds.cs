using EventBooking.Domain.Common;

namespace EventBooking.Domain.AttendeeGroups;

/// <summary>Stable identifiers and canonical codes for the five approved Attendee Groups.</summary>
public static class AttendeeGroupIds
{
    /// <summary>Gets the stable identifier for Cabin Crew.</summary>
    public static readonly Guid CabinCrew = Guid.Parse("e0000001-0000-0000-0000-000000000001");

    /// <summary>Gets the stable identifier for Pilots.</summary>
    public static readonly Guid Pilots = Guid.Parse("e0000002-0000-0000-0000-000000000002");

    /// <summary>Gets the stable identifier for Ground Operations Agent.</summary>
    public static readonly Guid GroundOperationsAgent = Guid.Parse("e0000003-0000-0000-0000-000000000003");

    /// <summary>Gets the stable identifier for Engineering.</summary>
    public static readonly Guid Engineering = Guid.Parse("e0000004-0000-0000-0000-000000000004");

    /// <summary>Gets the stable identifier for Ground Transport Services.</summary>
    public static readonly Guid GroundTransportServices = Guid.Parse("e0000005-0000-0000-0000-000000000005");

    private static readonly Dictionary<string, Guid> CodeToId = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CABIN_CREW"] = CabinCrew,
        ["PILOTS"] = Pilots,
        ["GROUND_OPERATIONS_AGENT"] = GroundOperationsAgent,
        ["ENGINEERING"] = Engineering,
        ["GROUND_TRANSPORT_SERVICES"] = GroundTransportServices,
    };

    private static readonly Dictionary<Guid, string> IdToCode = new()
    {
        [CabinCrew] = "CABIN_CREW",
        [Pilots] = "PILOTS",
        [GroundOperationsAgent] = "GROUND_OPERATIONS_AGENT",
        [Engineering] = "ENGINEERING",
        [GroundTransportServices] = "GROUND_TRANSPORT_SERVICES",
    };

    /// <summary>Gets every approved Attendee Group identifier.</summary>
    public static IReadOnlyCollection<Guid> All { get; } =
    [
        CabinCrew,
        Pilots,
        GroundOperationsAgent,
        Engineering,
        GroundTransportServices,
    ];

    /// <summary>Gets every approved canonical Attendee Group code.</summary>
    public static IReadOnlyCollection<string> Codes => CodeToId.Keys;

    /// <summary>Tries to resolve a trimmed case-insensitive canonical-code input to its identifier.</summary>
    /// <param name="code">The code.</param>
    /// <param name="id">The id.</param>
    public static bool TryFromCode(string? code, out Guid id)
    {
        id = Guid.Empty;

        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        return CodeToId.TryGetValue(code.Trim(), out id);
    }

    /// <summary>Gets the canonical code for a known Attendee Group identifier.</summary>
    /// <param name="id">The id.</param>
    public static string CodeOf(Guid id) =>
        IdToCode.TryGetValue(id, out var code)
            ? code
            : throw new DomainException($"{id} is not one of the 5 attendee groups.");

    /// <summary>Ensures the identifier names a known Attendee Group.</summary>
    /// <param name="id">The id.</param>
    public static void EnsureKnown(Guid id)
    {
        Guard.Against(!IdToCode.ContainsKey(id), $"{id} is not one of the 5 attendee groups.");
    }
}
