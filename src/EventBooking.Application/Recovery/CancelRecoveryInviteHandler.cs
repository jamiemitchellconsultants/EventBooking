using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Recovery;

/// <summary>Cancels one pending recovery invite without touching capacity or appointments.</summary>
/// <param name="StaffUserId">The coordinator cancelling the recovery invite.</param>
/// <param name="AttendeeId">The attendee the invite must belong to.</param>
/// <param name="InviteId">The pending recovery invite to cancel.</param>
public sealed record CancelRecoveryInviteCommand(Guid StaffUserId, Guid AttendeeId, Guid InviteId);

/// <summary>Cancels one pending recovery invite without changing capacity or attendee status.</summary>
/// <param name="attendees">The attendees.</param>
/// <param name="invites">The invites.</param>
/// <param name="access">The staff access authorizer.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
public sealed class CancelRecoveryInviteHandler(
    IAttendeeRepository attendees,
    IInviteRepository invites,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result> HandleAsync(
        CancelRecoveryInviteCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId, StaffCapability.ManageAttendees, null, ct);
        if (authorized.IsFailure) return Result.Failure(authorized.Error);

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        // The attendee row is taken first. Task 15 puts Invite above Attendee in the
        // ladder, so locking the invite first and the attendee second is a descent and
        // trips the guard. Reading the invite unlocked to learn its attendee, then
        // locking downwards, is the order every other lifecycle handler already uses.
        var invite = await invites.GetAsync(command.InviteId, ct);
        if (invite is null || invite.AttendeeId != command.AttendeeId)
            return Result.Failure(Error.NotFound("No such invite."));

        var attendee = await attendees.LockForUpdateAsync(invite.AttendeeId, ct);
        if (attendee is null) return Result.Failure(Error.NotFound("No such attendee."));

        var locked = await invites.LockForUpdateAsync(command.InviteId, ct);
        if (locked is null || locked.AttendeeId != attendee.Id)
            return Result.Failure(Error.NotFound("No such invite."));

        // Re-read under the lock: the unlocked read above established lock order only,
        // and the invite may have been answered in between.
        if (locked.RecoveryOfBookingId is null)
            return Result.Failure(
                Error.Validation("Only a recovery invite can be cancelled."));
        if (locked.Status != InviteStatus.Pending)
            return Result.Failure(
                Error.Conflict($"The invite is {locked.Status} and can no longer be cancelled."));

        try
        {
            locked.CancelRecovery();
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }

        audit.Record(
            AuditEntityTypes.Invite,
            locked.Id,
            AuditAction.RecoveryInviteCancelled,
            ActorType.Staff,
            command.StaffUserId.ToString(),
            null);

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result.Success();
    }
}
