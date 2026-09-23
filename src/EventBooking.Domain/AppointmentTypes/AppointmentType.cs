namespace EventBooking.Domain.AppointmentTypes;

/// <summary>Defines appointment type for the current use case.</summary>
public sealed class AppointmentType
{
    private AppointmentType()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    private AppointmentType(Guid id, string code, string name)
    {
        Id = id;
        Code = code;
        Name = name;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }
    /// <summary>Defines code for the current use case.</summary>
    public string Code { get; private set; }
    /// <summary>Defines name for the current use case.</summary>
    public string Name { get; private set; }

    /// <summary>Defines create fixed set for the current use case.</summary>
    public static IReadOnlyList<AppointmentType> CreateFixedSet() =>
        AppointmentTypeIds.All
            .Select(id => new AppointmentType(
                id,
                AppointmentTypeIds.CodeOf(id),
                AppointmentTypeIds.NameOf(id)))
            .ToList();
}
