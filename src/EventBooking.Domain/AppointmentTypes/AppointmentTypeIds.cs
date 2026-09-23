using EventBooking.Domain.Common;

namespace EventBooking.Domain.AppointmentTypes;

/// <summary>
/// The 3 appointment types are fixed by the spec. Their identifiers are constants rather than
/// generated values so that seed data, tests and migrations all agree without a lookup.
/// </summary>
public static class AppointmentTypeIds
{
    /// <summary>Defines drug and alcohol testing for the current use case.</summary>
    public static readonly Guid DrugAndAlcoholTesting = Guid.Parse("a0000001-0000-0000-0000-000000000001");
    /// <summary>Defines medical check up for the current use case.</summary>
    public static readonly Guid MedicalCheckUp = Guid.Parse("a0000002-0000-0000-0000-000000000002");
    /// <summary>Defines uniform fitting for the current use case.</summary>
    public static readonly Guid UniformFitting = Guid.Parse("a0000003-0000-0000-0000-000000000003");

    /// <summary>Defines all for the current use case.</summary>
    public static readonly IReadOnlyList<Guid> All =
    [
        DrugAndAlcoholTesting,
        MedicalCheckUp,
        UniformFitting,
    ];

    private static readonly Dictionary<string, Guid> CodeToId = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DAT"] = DrugAndAlcoholTesting,
        ["MED"] = MedicalCheckUp,
        ["UNI"] = UniformFitting,
    };

    private static readonly Dictionary<Guid, string> IdToCode = new()
    {
        [DrugAndAlcoholTesting] = "DAT",
        [MedicalCheckUp] = "MED",
        [UniformFitting] = "UNI",
    };

    private static readonly Dictionary<Guid, string> IdToName = new()
    {
        [DrugAndAlcoholTesting] = "Drug & Alcohol Testing",
        [MedicalCheckUp] = "Medical Check-up",
        [UniformFitting] = "Uniform Fitting",
    };

    /// <summary>Defines codes for the current use case.</summary>
    public static IReadOnlyCollection<string> Codes => CodeToId.Keys;

    /// <summary>Defines try from code for the current use case.</summary>
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

    /// <summary>Defines code of for the current use case.</summary>
    /// <param name="id">The id.</param>
    public static string CodeOf(Guid id) =>
        IdToCode.TryGetValue(id, out var code)
            ? code
            : throw new DomainException($"{id} is not one of the 3 appointment types.");

    /// <summary>Defines name of for the current use case.</summary>
    /// <param name="id">The id.</param>
    public static string NameOf(Guid id) =>
        IdToName.TryGetValue(id, out var name)
            ? name
            : throw new DomainException($"{id} is not one of the 3 appointment types.");

    /// <summary>Defines ensure known for the current use case.</summary>
    /// <param name="id">The id.</param>
    public static void EnsureKnown(Guid id)
    {
        Guard.Against(!IdToName.ContainsKey(id), $"{id} is not one of the 3 appointment types.");
    }
}
