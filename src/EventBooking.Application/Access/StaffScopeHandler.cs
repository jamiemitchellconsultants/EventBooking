using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;

namespace EventBooking.Application.Access;

/// <summary>Requests setting one profile's appointment-type scope, displacing any holder.</summary>
/// <param name="StaffUserId">The acting administrator.</param>
/// <param name="TargetStaffUserId">The profile whose scope changes.</param>
/// <param name="AppointmentTypeId">The scope to set, or null to clear.</param>
/// <param name="ExpectedVersion">The target version the writer refreshed to.</param>
public sealed record SetStaffScopeCommand(
    Guid StaffUserId, Guid TargetStaffUserId, Guid? AppointmentTypeId, long ExpectedVersion);

/// <summary>The scope change plus the displaced Manager's display name when any.</summary>
/// <param name="TargetStaffUserId">The profile whose scope changed.</param>
/// <param name="AppointmentTypeId">The scope now set, or null when cleared.</param>
/// <param name="DisplacedManagerDisplayName">The displaced holder's name, when any.</param>
public sealed record SetStaffScopeOutcome(
    Guid TargetStaffUserId, Guid? AppointmentTypeId, string? DisplacedManagerDisplayName);

/// <summary>Sets one profile's appointment-type scope, atomically displacing any holder.</summary>
/// <param name="profiles">The profiles.</param>
/// <param name="identities">The identities.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="access">The access.</param>
public sealed class StaffScopeHandler(
    IStaffAccessProfileRepository profiles,
    IStaffIdentityRepository identities,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IStaffAccessAuthorizer access)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<SetStaffScopeOutcome>> HandleAsync(
        SetStaffScopeCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId, StaffCapability.ManageStaffAccess, null, ct);
        if (authorized.IsFailure) return Result<SetStaffScopeOutcome>.Failure(authorized.Error);

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var target = await profiles.GetAsync(command.TargetStaffUserId, ct);
        if (target is null)
            return Result<SetStaffScopeOutcome>.Failure(Error.NotFound("No such staff profile."));
        if (target.Version != command.ExpectedVersion)
            return Result<SetStaffScopeOutcome>.Failure(
                Error.VersionConflict("The staff profile changed under you.", target.Version));
        if (command.AppointmentTypeId is not null && !target.IsManager)
            return Result<SetStaffScopeOutcome>.Failure(
                Error.Validation("Only a Manager profile can hold an appointment-type scope."));

        string? displacedName = null;
        if (command.AppointmentTypeId is { } typeId)
        {
            var holder = (await profiles.LockAllAsync(ct))
                .SingleOrDefault(p => p.AppointmentTypeId == typeId
                    && p.StaffUserId != target.StaffUserId
                    && p.IsManager);
            if (holder is not null)
            {
                var identity = (await identities.ListAsync(ct))
                    .SingleOrDefault(i => i.StaffUserId == holder.StaffUserId);
                displacedName = identity?.DisplayName ?? identity?.StaffId.Value;
                if (holder.RemoveManagerRole())
                    profiles.Remove(holder);
                else
                    holder.Replace(RolesOf(holder), null);
            }
        }

        try
        {
            target.Replace(RolesOf(target), command.AppointmentTypeId);
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(ct);
            return Result<SetStaffScopeOutcome>.Failure(Error.Validation(ex.Message));
        }

        audit.Record(AuditEntityTypes.StaffAccessProfile, target.StaffUserId,
            AuditAction.StaffAccessChanged, ActorType.Staff, command.StaffUserId.ToString(),
            command.AppointmentTypeId is null
                ? "scope cleared"
                : $"scope {command.AppointmentTypeId}");
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result<SetStaffScopeOutcome>.Success(new SetStaffScopeOutcome(
            target.StaffUserId, command.AppointmentTypeId, displacedName));
    }

    private static List<Role> RolesOf(StaffAccessProfile profile)
    {
        var roles = new List<Role>();
        if (profile.IsManager) roles.Add(Role.Manager);
        if (profile.IsCoordinator) roles.Add(Role.Coordinator);
        if (profile.IsAdmin) roles.Add(Role.Admin);
        if (profile.IsAppointmentStaff) roles.Add(Role.AppointmentStaff);
        return roles;
    }
}
