using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Slots;

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
    ISlotProposalRepository proposals,
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
            StaffCapability.ManageSlotNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            var admin = await access.AuthorizeAsync(
                command.ManagerUserId,
                StaffCapability.ManageSettings,
                null,
                cancellationToken);
            if (admin.IsFailure)
            {
                return Result.Failure(authorized.Error);
            }
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var proposal = await proposals.LockForUpdateAsync(command.ProposalId, cancellationToken);
        if (proposal is null)
        {
            return Result.Failure(Error.NotFound("No such proposal."));
        }

        if (proposal.Status != SlotProposalStatus.Open)
        {
            return Result.Failure(Error.Conflict("Only an open proposal can be withdrawn."));
        }

        try
        {
            proposal.Withdraw(command.ManagerUserId);
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }

        audit.Record(
            AuditEntityTypes.SlotProposal,
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
