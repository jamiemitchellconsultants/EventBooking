using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Access;

/// <summary>Defines staff access context for the current use case.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Roles">The roles.</param>
/// <param name="AppointmentTypeId">The appointment type id.</param>
public sealed record StaffAccessContext(
    Guid StaffUserId,
    IReadOnlySet<Role> Roles,
    Guid? AppointmentTypeId);

/// <summary>Defines istaff access authorizer for the current use case.</summary>
public interface IStaffAccessAuthorizer
{
    /// <summary>Provides authorize async within this contract.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="capability">The capability.</param>
    /// <param name="requiredAppointmentTypeId">The required appointment type id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Result<StaffAccessContext>> AuthorizeAsync(
        Guid staffUserId,
        StaffCapability capability,
        Guid? requiredAppointmentTypeId,
        CancellationToken cancellationToken);
}

/// <summary>Defines staff access authorizer for the current use case.</summary>
/// <param name="profiles">The profiles.</param>
public sealed class StaffAccessAuthorizer(IStaffAccessProfileRepository profiles)
    : IStaffAccessAuthorizer
{
    private static readonly Error Denied = Error.Forbidden("This staff profile cannot perform this operation.");

    /// <summary>Defines authorize async for the current use case.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="capability">The capability.</param>
    /// <param name="requiredAppointmentTypeId">The required appointment type id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<StaffAccessContext>> AuthorizeAsync(
        Guid staffUserId,
        StaffCapability capability,
        Guid? requiredAppointmentTypeId,
        CancellationToken cancellationToken)
    {
        var profile = await profiles.GetAsync(staffUserId, cancellationToken);
        if (profile is null || !profile.IsValid() || !IsAllowed(profile, capability))
        {
            return Result<StaffAccessContext>.Failure(Denied);
        }

        // A Manager or AppointmentStaff profile with no appointment-type scope grants no
        // capabilities on its own: the null scope denies every capability unless another role
        // on the same profile grants it.
        if (StaffAccessProfile.NeedsScope(profile.Roles)
            && profile.AppointmentTypeId is null
            && !IsAllowed(profile.IsAdmin, profile.IsCoordinator, false, false, capability))
        {
            return Result<StaffAccessContext>.Failure(Denied);
        }

        var scopedCapability = capability is
            StaffCapability.ManageEventNegotiation or
            StaffCapability.ViewEventOperations or
            StaffCapability.ConductAppointments;

        if (scopedCapability
            && StaffAccessProfile.NeedsScope(profile.Roles)
            && profile.AppointmentTypeId is null)
        {
            return Result<StaffAccessContext>.Failure(Denied);
        }

        if (requiredAppointmentTypeId is not null
            && scopedCapability
            && profile.AppointmentTypeId != requiredAppointmentTypeId)
        {
            return Result<StaffAccessContext>.Failure(Denied);
        }

        return Result<StaffAccessContext>.Success(new StaffAccessContext(
            profile.StaffUserId,
            profile.Roles,
            profile.AppointmentTypeId));
    }

    private static bool IsAllowed(StaffAccessProfile profile, StaffCapability capability) =>
        IsAllowed(profile.IsAdmin, profile.IsCoordinator, profile.IsManager, profile.IsAppointmentStaff, capability);

    private static bool IsAllowed(bool isAdmin, bool isCoordinator, bool isManager, bool isAppointmentStaff, StaffCapability capability)
    {
        // Explicit attendee-data deny for Admin is retained even though valid profiles make Admin
        // exclusive. It fails closed if invalid data reaches this method in a future refactor.
        if (isAdmin && capability is
            StaffCapability.ManageAttendees or
            StaffCapability.ViewAttendeeDashboards or
            StaffCapability.ViewAttendeeAudit)
        {
            return false;
        }

        return capability switch
        {
            StaffCapability.ManageSettings => isAdmin,
            StaffCapability.ManageReferenceData => isAdmin,
            StaffCapability.ManageStaffAccess => isAdmin,
            StaffCapability.ManageAttendees => isCoordinator,
            StaffCapability.ViewAttendeeDashboards => isCoordinator,
            StaffCapability.ViewAttendeeAudit => isCoordinator,
            StaffCapability.ViewEventAudit => isAdmin || isCoordinator,
            StaffCapability.ManageEventNegotiation => isManager,
            StaffCapability.ViewEventOperations =>
                isAdmin || isCoordinator || isManager || isAppointmentStaff,
            StaffCapability.CancelEvent =>
                isAdmin || isCoordinator || isManager,
            StaffCapability.ConductAppointments => isManager || isAppointmentStaff,
            _ => false,
        };
    }
}
