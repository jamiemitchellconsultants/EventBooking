using EventBooking.Domain.Common;

namespace EventBooking.Domain.AppointmentTypes;

/// <summary>
/// One kind of appointment, run by at most one current Manager across every location. Admin-managed
/// reference data: created, renamed and deactivated, never deleted (FR-1.3, FR-1.6, FR-1.7).
/// </summary>
public sealed class AppointmentType
{
    /// <summary>The longest code an appointment type may have (design 08).</summary>
    public const int MaximumCodeLength = 50;

    /// <summary>The longest name an appointment type may have (design 08).</summary>
    public const int MaximumNameLength = 100;

    private AppointmentType()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    /// <summary>The stable reference-data identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>The immutable canonical uppercase snake-case code.</summary>
    public string Code { get; private set; }

    /// <summary>The staff-facing display name.</summary>
    public string Name { get; private set; }

    /// <summary>Whether new proposals and groups may name this type.</summary>
    public bool IsActive { get; private set; }

    /// <summary>The optimistic-concurrency token.</summary>
    public long Version { get; private set; }

    /// <summary>Creates an active appointment type.</summary>
    /// <param name="id">The new identifier.</param>
    /// <param name="code">The code, in any case.</param>
    /// <param name="name">The display name.</param>
    public static AppointmentType Create(Guid id, string? code, string? name)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        var canonicalCode = ReferenceDataCode.Parse(code, MaximumCodeLength, "code");
        var displayName = BoundedName(name);

        return new AppointmentType
        {
            Id = id,
            Code = canonicalCode,
            Name = displayName,
            IsActive = true,
            Version = 1,
        };
    }

    /// <summary>Renames the type. Always allowed; the code never changes.</summary>
    /// <param name="name">The new display name.</param>
    public void Rename(string? name)
    {
        Name = BoundedName(name);
        Version++;
    }

    /// <summary>Deactivates the type, refusing while it is in live use (FR-1.6).</summary>
    /// <param name="usage">What the type currently carries.</param>
    public void Deactivate(AppointmentTypeUsage usage)
    {
        if (usage.Any)
        {
            throw new ReferenceDataInUseException(
                "The appointment type cannot be deactivated while it is listed or mapped.",
                usage.AsBlocking());
        }

        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        Version++;
    }

    /// <summary>Returns the type to use.</summary>
    public void Reactivate()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        Version++;
    }

    /// <summary>
    /// The predecessor's three seeded types, retained only until Phase 3 rewires seeding and the
    /// handlers onto Admin-managed types.
    /// </summary>
    public static IReadOnlyList<AppointmentType> CreateFixedSet() =>
        AppointmentTypeIds.All
            .Select(id => new AppointmentType
            {
                Id = id,
                Code = AppointmentTypeIds.CodeOf(id),
                Name = AppointmentTypeIds.NameOf(id),
                IsActive = true,
                Version = 1,
            })
            .ToList();

    private static string BoundedName(string? name)
    {
        var displayName = Guard.NotBlank(name, "name");
        Guard.Against(
            displayName.Length > MaximumNameLength,
            $"name must be at most {MaximumNameLength} characters.");
        return displayName;
    }
}
