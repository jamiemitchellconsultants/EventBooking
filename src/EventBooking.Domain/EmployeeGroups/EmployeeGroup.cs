using System.Text.RegularExpressions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.EmployeeGroups;

/// <summary>One change-controlled employment category that determines candidate requirements.</summary>
public sealed class EmployeeGroup
{
    private static readonly Regex CanonicalCodeExpression =
        new("^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$", RegexOptions.CultureInvariant);

    private readonly List<EmployeeGroupRequirement> _requirements = [];

    private EmployeeGroup()
    {
        // Materializes persisted rows, including inactive ones that Define would reject.
        Code = string.Empty;
        Name = string.Empty;
    }

    /// <summary>Gets the stable reference-data identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the immutable canonical uppercase snake-case code.</summary>
    public string Code { get; private set; }

    /// <summary>Gets the canonical Coordinator-facing display name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets whether new and changed Candidates may be assigned this group.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the fixed Appointment Type mappings owned by this group.</summary>
    public IReadOnlyList<EmployeeGroupRequirement> Requirements => _requirements;

    /// <summary>Gets the mapped Appointment Type identifiers in stable identifier order.</summary>
    public IReadOnlyList<Guid> RequiredAppointmentTypeIds =>
        _requirements.Select(requirement => requirement.AppointmentTypeId).Order().ToList();

    /// <summary>Defines one validated reference-data row and its complete mapping.</summary>
    /// <param name="id">The id.</param>
    /// <param name="code">The code.</param>
    /// <param name="name">The name.</param>
    /// <param name="isActive">The is active.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public static EmployeeGroup Define(
        Guid id,
        string? code,
        string? name,
        bool isActive,
        IEnumerable<Guid> appointmentTypeIds)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(
            code is null || !CanonicalCodeExpression.IsMatch(code),
            "code must be canonical uppercase snake case.");
        Guard.Against(!isActive, "An inactive employee group cannot be assignment authority.");

        var displayName = Guard.NotBlank(name, "name");
        var mapping = appointmentTypeIds.ToList();

        Guard.Against(mapping.Count == 0, "An employee group must map at least one appointment type.");
        Guard.Against(
            mapping.Distinct().Count() != mapping.Count,
            "An employee group cannot map the same appointment type twice.");

        foreach (var appointmentTypeId in mapping)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

        var group = new EmployeeGroup
        {
            Id = id,
            Code = code!,
            Name = displayName,
            IsActive = isActive,
        };

        foreach (var appointmentTypeId in mapping.Order())
        {
            group._requirements.Add(EmployeeGroupRequirement.For(id, appointmentTypeId));
        }

        return group;
    }
}
