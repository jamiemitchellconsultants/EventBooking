using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Access;

/// <summary>Defines staff access profile for the current use case.</summary>
public sealed class StaffAccessProfile
{
    private StaffAccessProfile()
    {
    }

    /// <summary>Defines staff user id for the current use case.</summary>
    public Guid StaffUserId { get; private set; }

    /// <summary>Defines is manager for the current use case.</summary>
    public bool IsManager { get; private set; }

    /// <summary>Defines is coordinator for the current use case.</summary>
    public bool IsCoordinator { get; private set; }

    /// <summary>Defines is admin for the current use case.</summary>
    public bool IsAdmin { get; private set; }

    /// <summary>Defines is appointment staff for the current use case.</summary>
    public bool IsAppointmentStaff { get; private set; }

    /// <summary>Defines appointment type id for the current use case.</summary>
    public Guid? AppointmentTypeId { get; private set; }

    /// <summary>Defines version for the current use case.</summary>
    public long Version { get; private set; }

    /// <summary>Defines roles for the current use case.</summary>
    public IReadOnlySet<Role> Roles => RoleSet();

    /// <summary>Defines create for the current use case.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="roles">The roles.</param>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public static StaffAccessProfile Create(
        Guid staffUserId,
        IReadOnlyCollection<Role> roles,
        Guid? appointmentTypeId)
    {
        Guard.Against(staffUserId == Guid.Empty, "staffUserId must not be empty.");
        var roleSet = Validate(roles, appointmentTypeId);

        var profile = new StaffAccessProfile
        {
            StaffUserId = staffUserId,
            Version = 1,
        };
        profile.Apply(roleSet, appointmentTypeId);
        return profile;
    }

    /// <summary>Defines create for the current use case.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="role">The role.</param>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public static StaffAccessProfile Create(
        Guid staffUserId,
        Role role,
        Guid? appointmentTypeId) =>
        Create(staffUserId, [role], appointmentTypeId);

    /// <summary>Whether the given role set requires an appointment-type scope
    /// (Manager and AppointmentStaff are scoped roles; Admin and Coordinator are not).</summary>
    /// <param name="roles">The roles.</param>
    public static bool NeedsScope(IReadOnlyCollection<Role> roles) =>
        roles.Contains(Role.Manager) || roles.Contains(Role.AppointmentStaff);

    /// <summary>Defines has role for the current use case.</summary>
    /// <param name="role">The role.</param>
    public bool HasRole(Role role) => role switch
    {
        Role.Manager => IsManager,
        Role.Coordinator => IsCoordinator,
        Role.Admin => IsAdmin,
        Role.AppointmentStaff => IsAppointmentStaff,
        _ => false,
    };

    /// <summary>Defines is valid for the current use case.</summary>
    public bool IsValid()
    {
        try
        {
            _ = Validate(RoleSet(), AppointmentTypeId);
            return StaffUserId != Guid.Empty && Version > 0;
        }
        catch (DomainException)
        {
            return false;
        }
    }

    /// <summary>Defines replace for the current use case.</summary>
    /// <param name="roles">The roles.</param>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public void Replace(IReadOnlyCollection<Role> roles, Guid? appointmentTypeId)
    {
        var roleSet = Validate(roles, appointmentTypeId);
        Apply(roleSet, appointmentTypeId);
        Version++;
    }

    /// <summary>Defines remove manager role for the current use case.</summary>
    public bool RemoveManagerRole()
    {
        Guard.Against(!IsManager, "The profile is not a Manager.");
        IsManager = false;
        if (!IsAppointmentStaff)
        {
            AppointmentTypeId = null;
        }

        Version++;
        return RoleSet().Count == 0;
    }

    private static HashSet<Role> Validate(
        IReadOnlyCollection<Role>? roles,
        Guid? appointmentTypeId)
    {
        Guard.Against(roles is null || roles.Count == 0, "At least one role is required.");
        var roleSet = roles!.ToHashSet();
        Guard.Against(
            roleSet.Any(role => !Enum.IsDefined(role)),
            "Every role must be recognised.");

        if (roleSet.Contains(Role.Admin))
        {
            Guard.Against(roleSet.Count != 1, "Admin cannot be combined with another role.");
            Guard.Against(appointmentTypeId is not null, "Admin cannot have an appointment type.");
            return roleSet;
        }

        Guard.Against(
            !NeedsScope(roleSet) && appointmentTypeId is not null,
            "Only Manager or AppointmentStaff can have an appointment type.");

        if (appointmentTypeId is not null)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId.Value);
        }

        return roleSet;
    }

    private void Apply(IReadOnlySet<Role> roles, Guid? appointmentTypeId)
    {
        IsManager = roles.Contains(Role.Manager);
        IsCoordinator = roles.Contains(Role.Coordinator);
        IsAdmin = roles.Contains(Role.Admin);
        IsAppointmentStaff = roles.Contains(Role.AppointmentStaff);
        AppointmentTypeId = appointmentTypeId;
    }

    private HashSet<Role> RoleSet()
    {
        var roles = new HashSet<Role>();
        if (IsManager) roles.Add(Role.Manager);
        if (IsCoordinator) roles.Add(Role.Coordinator);
        if (IsAdmin) roles.Add(Role.Admin);
        if (IsAppointmentStaff) roles.Add(Role.AppointmentStaff);
        return roles;
    }
}
