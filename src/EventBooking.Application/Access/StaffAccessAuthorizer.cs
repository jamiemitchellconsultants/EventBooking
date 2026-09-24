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

        var scopedCapability = capability is
            StaffCapability.ManageEventNegotiation or
            StaffCapability.ViewEventOperations or
            StaffCapability.ConductAppointments;

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

    internal static bool IsAllowed(StaffAccessProfile profile, StaffCapability capability)
    {
        // Explicit attendee-data deny for Admin is retained even though valid profiles make Admin
        // exclusive. It fails closed if invalid data reaches this method in a future refactor,
        // and even if a refactor mislabels a table row.
        if (profile.IsAdmin && capability is
            StaffCapability.ManageAttendees or
            StaffCapability.ViewAttendeeDashboards or
            StaffCapability.ViewAttendeeAudit)
        {
            return false;
        }

        // A scoped role with a null scope contributes no grant at all (FR-10.7): the
        // Manager or AppointmentStaff role is set aside, and the capability is granted only
        // when another held role grants it through an unscoped table row.
        var granting = profile.Roles;
        if (profile.AppointmentTypeId is null)
        {
            granting = granting
                .Where(role => role is not (Role.Manager or Role.AppointmentStaff))
                .ToHashSet();
        }

        var name = capability.ToString();
        return CapabilityMatrix.Grants.Any(grant =>
            grant.Capability == name
            && granting.Contains(Enum.Parse<Role>(grant.Role))
            && (!grant.NeedsScope || profile.AppointmentTypeId is not null));
    }
}
