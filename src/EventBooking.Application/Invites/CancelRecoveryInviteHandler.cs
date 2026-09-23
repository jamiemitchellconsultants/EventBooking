using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Invites;

/// <summary>Cancels one pending recovery Invite without touching capacity or appointments.</summary>
/// <param name="StaffUserId">The Coordinator cancelling the recovery Invite.</param>
/// <param name="CandidateId">The candidate route the Invite must belong to.</param>
/// <param name="InviteId">The pending recovery Invite to cancel.</param>
public sealed record CancelRecoveryInviteCommand(Guid StaffUserId, Guid CandidateId, Guid InviteId);

/// <summary>Cancels one Pending recovery Invite without changing capacity or Candidate status.</summary>
/// <param name="candidates">The candidates.</param>
/// <param name="access">The access.</param>
/// <param name="invites">The invites.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class CancelRecoveryInviteHandler(
    ICandidateRepository candidates,
    IStaffAccessAuthorizer access,
    IInviteRepository invites,
    IBookingRepository bookings,
    IAuditLogger audit,
    IUnitOfWork unitOfWork)
{
    /// <summary>Cancels one Pending recovery Invite without changing capacity or Candidate status.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> HandleAsync(
        CancelRecoveryInviteCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageCandidates,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var candidate = await candidates.LockForUpdateAsync(command.CandidateId, cancellationToken);
        if (candidate is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("No such candidate."));
        }

        var invite = await invites.LockForUpdateAsync(command.InviteId, cancellationToken);
        if (invite is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("No such invite."));
        }

        if (invite.RecoveryOfBookingId is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("Only a recovery invite can be cancelled."));
        }

        if (invite.CandidateId != command.CandidateId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("This invite does not belong to this candidate."));
        }

        var root = await bookings.LockForUpdateAsync(invite.RecoveryOfBookingId.Value, cancellationToken);
        if (root is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("The original booking no longer exists."));
        }

        if (root.CandidateId != command.CandidateId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("This invite does not belong to this candidate."));
        }

        if (invite.Status != InviteStatus.Pending)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("This recovery invite is no longer pending."));
        }

        try
        {
            invite.RotateTokenHash(Guid.NewGuid().ToString("N"));
            invite.CancelRecovery();
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Validation(ex.Message));
        }

        audit.Record(
            AuditEntityTypes.Invite,
            invite.Id,
            AuditAction.RecoveryInviteCancelled,
            ActorType.Staff,
            command.StaffUserId.ToString(),
            $"root {root.Id}");

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result.Success();
    }
}
