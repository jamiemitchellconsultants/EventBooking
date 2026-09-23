using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Access;

/// <summary>Describes the authenticated caller's staff number, roles, and shared scope.</summary>
/// <param name="StaffId">The staff id.</param>
/// <param name="Roles">The roles.</param>
/// <param name="AppointmentTypeId">The appointment type id.</param>
/// <param name="AppointmentTypeName">The appointment type name.</param>
public sealed record MeView(
    StaffId? StaffId,
    IReadOnlyList<Role> Roles,
    Guid? AppointmentTypeId,
    string? AppointmentTypeName);

/// <summary>Builds the current caller's view after synchronising provider-owned roles.</summary>
/// <param name="appointmentTypes">The appointment types.</param>
/// <param name="sync">The sync.</param>
public sealed class MeHandler(
    IAppointmentTypeRepository appointmentTypes,
    SyncStaffAccessProfileRolesHandler sync)
{
    /// <summary>Synchronises claimed roles, then returns roles and EventBooking-owned scope.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="staffId">The staff id.</param>
    /// <param name="claimedRoles">The claimed roles.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<MeView> GetAsync(
        Guid staffUserId,
        StaffId? staffId,
        IReadOnlySet<Role> claimedRoles,
        CancellationToken cancellationToken)
    {
        var profile = await sync.SyncAsync(staffUserId, claimedRoles, cancellationToken);
        if (profile is null || !profile.IsValid())
        {
            return new MeView(staffId, [], null, null);
        }

        string? appointmentTypeName = null;
        if (profile.AppointmentTypeId is not null)
        {
            var type = await appointmentTypes.GetAsync(
                profile.AppointmentTypeId.Value, cancellationToken);
            appointmentTypeName = type?.Name;
        }

        return new MeView(
            staffId,
            profile.Roles.OrderBy(role => role).ToList(),
            profile.AppointmentTypeId,
            appointmentTypeName);
    }
}
