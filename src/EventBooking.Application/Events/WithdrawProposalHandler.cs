using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Events;

/// <summary>Defines withdraw proposal command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="ProposalId">The proposal id.</param>
public sealed record WithdrawProposalCommand(Guid ManagerUserId, Guid ProposalId);

/// <summary>Withdraws an open proposal while holding its lifecycle row lock.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
public sealed class WithdrawProposalHandler(
    IEventProposalRepository proposals,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Withdraws an open proposal when the caller is its appointment-type Manager or an Admin.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> HandleAsync(
        WithdrawProposalCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageEventNegotiation,
            null,
            cancellationToken);
        // The predecessor let an Admin withdraw any proposal. FR-2.9 judges withdrawal by the
        // proposing appointment type, and the design's capability matrix gives Admin no
        // negotiation capability at all, so that fallback is gone.
        if (authorized.IsFailure)
        {
            return Result.Failure(authorized.Error);
        }


        // A scoped role with no scope is granted nothing (FR-10.7), and negotiation is judged on
        // the caller's own appointment type.
        if (authorized.Value.AppointmentTypeId is not { } actingType)
        {
            return Result.Failure(Error.Forbidden("This action needs an assigned appointment type."));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var proposal = await proposals.LockForUpdateAsync(command.ProposalId, cancellationToken);
        if (proposal is null)
        {
            return Result.Failure(Error.NotFound("No such proposal."));
        }

        if (proposal.Status != EventProposalStatus.Open)
        {
            return Result.Failure(Error.Conflict("Only an open proposal can be withdrawn."));
        }

        try
        {
            // FR-2.9: the proposing appointment type withdraws, whoever currently holds it.
            proposal.Withdraw(actingType);
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }

        audit.Record(
            AuditEntityTypes.EventProposal,
            proposal.Id,
            AuditAction.ProposalWithdrawn,
            ActorType.Staff,
            command.ManagerUserId.ToString(),
            null);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
