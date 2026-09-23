using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EventBooking.Application.Access;

/// <summary>Mirrors a staff identity's identity-provider-asserted roles onto its
/// <c>StaffAccessProfile</c>, deriving dependent scope changes and never overwriting a previously-valid
/// profile with an invalid attendee shape.</summary>
/// <param name="profiles">The profiles.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="logger">The logger.</param>
public sealed class SyncStaffAccessProfileRolesHandler(
    IStaffAccessProfileRepository profiles,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    ILogger<SyncStaffAccessProfileRolesHandler> logger)
{
    /// <summary>Applies one sync pass and returns the resulting profile, or null when the identity
    /// has (or ends up with) no profile at all.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="claimedRoles">The claimed roles.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<StaffAccessProfile?> SyncAsync(
        Guid staffUserId,
        IReadOnlySet<Role> claimedRoles,
        CancellationToken cancellationToken)
    {
        // Cheap unlocked read first: the overwhelming majority of calls (every authenticated
        // page/session bootstrap) find nothing has changed since the last sync, and should not
        // pay for a transaction plus a table-wide advisory lock to discover that.
        var existing = await profiles.GetAsync(staffUserId, cancellationToken);
        if (existing is not null && existing.Roles.ToHashSet().SetEquals(claimedRoles))
        {
            return existing;
        }

        if (existing is null && claimedRoles.Count == 0)
        {
            return null;
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var locked = await profiles.LockAllAsync(cancellationToken);
        var current = locked.SingleOrDefault(value => value.StaffUserId == staffUserId);

        if (current is not null && current.Roles.ToHashSet().SetEquals(claimedRoles))
        {
            await transaction.CommitAsync(cancellationToken);
            return current;
        }

        if (current is null && claimedRoles.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        if (current is not null
            && current.IsAdmin
            && !claimedRoles.Contains(Role.Admin)
            && locked.Count(profile => profile.IsAdmin) == 1)
        {
            logger.LogWarning(
                "Refused to sync {StaffUserId} off the Admin role: they are the last remaining Admin.",
                staffUserId);
            await transaction.RollbackAsync(cancellationToken);
            return current;
        }

        var previous = current is null ? null : AuditStateOf(current);
        if (claimedRoles.Count == 0)
        {
            profiles.Remove(current!);
            Record(staffUserId, previous, new AuditState([], null));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var desiredScope = StaffAccessProfile.NeedsScope(claimedRoles)
            ? current?.AppointmentTypeId
            : null;

        try
        {
            if (current is null)
            {
                current = StaffAccessProfile.Create(staffUserId, claimedRoles, desiredScope);
                profiles.Add(current);
            }
            else
            {
                current.Replace(claimedRoles, desiredScope);
            }
        }
        catch (DomainException)
        {
            logger.LogWarning(
                "Rejected role sync for {StaffUserId}: claimed roles {ClaimedRoles} are not a valid combination.",
                staffUserId,
                string.Join(",", claimedRoles.OrderBy(role => role)));
            await transaction.RollbackAsync(cancellationToken);
            return current;
        }

        Record(staffUserId, previous, AuditStateOf(current));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return current;
    }

    private void Record(Guid staffUserId, AuditState? previous, AuditState? current) =>
        audit.Record(
            AuditEntityTypes.StaffAccessProfile,
            staffUserId,
            AuditAction.StaffRolesSynced,
            ActorType.System,
            actorId: null,
            JsonSerializer.Serialize(new { previous, current }));

    private static AuditState AuditStateOf(StaffAccessProfile profile) => new(
        profile.Roles.OrderBy(role => role).Select(role => role.ToString()).ToList(),
        profile.AppointmentTypeId);

    private sealed record AuditState(IReadOnlyList<string> Roles, Guid? AppointmentTypeId);
}
