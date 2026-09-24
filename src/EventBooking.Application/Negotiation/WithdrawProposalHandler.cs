using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Negotiation;

/// <summary>Withdraws an open proposal for the proposing appointment type.</summary>
/// <param name="StaffUserId">The withdrawing manager.</param>
/// <param name="ProposalId">The proposal identifier.</param>
public sealed record WithdrawProposalCommand(Guid StaffUserId, Guid ProposalId);

/// <summary>Withdraws an open proposal while holding its lifecycle row lock.</summary>
/// <param name="proposals">The event proposal repository.</param>
/// <param name="access">The staff access authorizer.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit logger.</param>
public sealed class WithdrawProposalHandler(
    IEventProposalRepository proposals,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result> HandleAsync(
        WithdrawProposalCommand command, CancellationToken ct)
    {
        // The predecessor let an Admin withdraw any proposal. FR-2.9 judges withdrawal by
        // the proposing appointment type, and Admin holds no negotiation capability at
        // all, so that fallback is gone (Task 6).
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId, StaffCapability.ManageEventNegotiation, null, ct);
        if (authorized.IsFailure) return Result.Failure(authorized.Error);

        if (authorized.Value.AppointmentTypeId is not { } actingType)
            return Result.Failure(
                Error.Forbidden("Withdrawing needs an assigned appointment type."));

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var proposal = await proposals.LockForUpdateAsync(command.ProposalId, ct);
        if (proposal is null) return Result.Failure(Error.NotFound("No such proposal."));

        try
        {
            // FR-2.9: the proposing appointment type withdraws, whoever currently holds
            // it — so a successor Manager inherits the right along with the type.
            proposal.Withdraw(actingType);
        }
        catch (ProposalNotOpenException ex)
        {
            return Result.Failure(Error.ProposalNotOpen(
                $"The proposal is {ex.CurrentStatus} and can no longer be changed."));
        }
        // Withdraw's only other refusal is the proposing-type guard, and being the wrong
        // type is a permission answer rather than a malformed request.
        catch (DomainException)
        {
            return Result.Failure(
                Error.Forbidden("Only the proposing appointment type may withdraw the proposal."));
        }

        audit.Record(
            AuditEntityTypes.EventProposal,
            proposal.Id,
            AuditAction.ProposalWithdrawn,
            ActorType.Staff,
            command.StaffUserId.ToString(),
            null);

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result.Success();
    }
}
