using System.Text.RegularExpressions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.AttendeeGroups;

/// <summary>One change-controlled employment category that determines attendee requirements.</summary>
public sealed class AttendeeGroup
{
    private static readonly Regex CanonicalCodeExpression =
        new("^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$", RegexOptions.CultureInvariant);

    private readonly List<AttendeeGroupRequirement> _requirements = [];

    private AttendeeGroup()
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

    /// <summary>Gets whether new and changed Attendees may be assigned this group.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the optimistic-concurrency token.</summary>
    public long Version { get; private set; }

    /// <summary>Gets the fixed Appointment Type mappings owned by this group.</summary>
    public IReadOnlyList<AttendeeGroupRequirement> Requirements => _requirements;

    /// <summary>Gets the mapped Appointment Type identifiers in stable identifier order.</summary>
    public IReadOnlyList<Guid> RequiredAppointmentTypeIds =>
        _requirements.Select(requirement => requirement.AppointmentTypeId).Order().ToList();

    /// <summary>Defines one validated reference-data row and its complete mapping.</summary>
    /// <param name="id">The id.</param>
    /// <param name="code">The code.</param>
    /// <param name="name">The name.</param>
    /// <param name="isActive">The is active.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public static AttendeeGroup Define(
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
        Guard.Against(!isActive, "An inactive attendee group cannot be assignment authority.");

        var displayName = Guard.NotBlank(name, "name");
        var mapping = appointmentTypeIds.ToList();

        Guard.Against(mapping.Count == 0, "An attendee group must map at least one appointment type.");
        Guard.Against(
            mapping.Distinct().Count() != mapping.Count,
            "An attendee group cannot map the same appointment type twice.");

        foreach (var appointmentTypeId in mapping)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

        var group = new AttendeeGroup
        {
            Id = id,
            Code = code!,
            Name = displayName,
            IsActive = isActive,
        };

        foreach (var appointmentTypeId in mapping.Order())
        {
            group._requirements.Add(AttendeeGroupRequirement.For(id, appointmentTypeId));
        }

        return group;
    }

    /// <summary>The longest code an attendee group may have (design 08).</summary>
    public const int MaximumCodeLength = 50;

    /// <summary>The longest name an attendee group may have (design 08).</summary>
    public const int MaximumNameLength = 200;

    /// <summary>
    /// Creates an active Admin-managed group whose mapping names only active appointment types
    /// (FR-1.4).
    /// </summary>
    /// <param name="id">The new identifier.</param>
    /// <param name="code">The code, in any case.</param>
    /// <param name="name">The display name.</param>
    /// <param name="appointmentTypeIds">The appointment types every member requires.</param>
    /// <param name="activeAppointmentTypeIds">Every appointment type currently active.</param>
    public static AttendeeGroup Create(
        Guid id,
        string? code,
        string? name,
        IEnumerable<Guid> appointmentTypeIds,
        IReadOnlyCollection<Guid> activeAppointmentTypeIds)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        ArgumentNullException.ThrowIfNull(activeAppointmentTypeIds);

        var canonicalCode = ReferenceDataCode.Parse(code, MaximumCodeLength, "code");
        var displayName = BoundedName(name);
        var mapping = ValidatedMapping(appointmentTypeIds, activeAppointmentTypeIds);

        var group = new AttendeeGroup
        {
            Id = id,
            Code = canonicalCode,
            Name = displayName,
            IsActive = true,
            Version = 1,
        };

        foreach (var appointmentTypeId in mapping)
        {
            group._requirements.Add(AttendeeGroupRequirement.For(id, appointmentTypeId));
        }

        return group;
    }

    /// <summary>Renames the group. Always allowed; the code never changes.</summary>
    /// <param name="name">The new display name.</param>
    public void Rename(string? name)
    {
        Name = BoundedName(name);
        Version++;
    }

    /// <summary>
    /// Replaces the mapping set. A change that leaves the set identical is always allowed and
    /// changes nothing; a real change is refused while any member holds an active original booking,
    /// because their booking appointments were created from the old snapshot (FR-1.5).
    /// </summary>
    /// <param name="appointmentTypeIds">The appointment types every member should require.</param>
    /// <param name="activeAppointmentTypeIds">Every appointment type currently active.</param>
    /// <param name="blockingMembers">Members holding an active original booking.</param>
    /// <returns>Whether the mapping actually changed.</returns>
    public bool ReplaceRequirements(
        IEnumerable<Guid> appointmentTypeIds,
        IReadOnlyCollection<Guid> activeAppointmentTypeIds,
        int blockingMembers)
    {
        ArgumentNullException.ThrowIfNull(activeAppointmentTypeIds);
        Guard.Against(blockingMembers < 0, "blockingMembers must not be negative.");

        var mapping = ValidatedMapping(appointmentTypeIds, activeAppointmentTypeIds);
        if (mapping.SequenceEqual(RequiredAppointmentTypeIds))
        {
            return false;
        }

        if (blockingMembers > 0)
        {
            throw new ReferenceDataInUseException(
                "The requirement set cannot change while members hold an active booking.",
                new Dictionary<string, int> { ["blockingMembers"] = blockingMembers });
        }

        _requirements.Clear();
        foreach (var appointmentTypeId in mapping)
        {
            _requirements.Add(AttendeeGroupRequirement.For(Id, appointmentTypeId));
        }

        Version++;
        return true;
    }

    /// <summary>Deactivates the group, refusing while it still has members (FR-1.6).</summary>
    /// <param name="memberCount">Attendees currently assigned to the group.</param>
    public void Deactivate(int memberCount)
    {
        Guard.Against(memberCount < 0, "memberCount must not be negative.");

        if (memberCount > 0)
        {
            throw new ReferenceDataInUseException(
                "The attendee group cannot be deactivated while it has members.",
                new Dictionary<string, int> { ["members"] = memberCount });
        }

        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        Version++;
    }

    /// <summary>Returns the group to use as assignment authority.</summary>
    public void Reactivate()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        Version++;
    }

    private static string BoundedName(string? name)
    {
        var displayName = Guard.NotBlank(name, "name");
        Guard.Against(
            displayName.Length > MaximumNameLength,
            $"name must be at most {MaximumNameLength} characters.");
        return displayName;
    }

    private static List<Guid> ValidatedMapping(
        IEnumerable<Guid> appointmentTypeIds, IReadOnlyCollection<Guid> activeAppointmentTypeIds)
    {
        var mapping = appointmentTypeIds.ToList();

        Guard.Against(mapping.Count == 0, "An attendee group must map at least one appointment type.");
        Guard.Against(
            mapping.Distinct().Count() != mapping.Count,
            "An attendee group cannot map the same appointment type twice.");
        Guard.Against(
            mapping.Any(id => !activeAppointmentTypeIds.Contains(id)),
            "An attendee group can only map active appointment types.");

        return [.. mapping.Order()];
    }
}
