using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Negotiation;

/// <summary>Withdraws the calling manager's acceptance while the proposal is still open.</summary>
/// <param name="StaffUserId">The withdrawing manager.</param>
/// <param name="ProposalId">The proposal identifier.</param>
public sealed record WithdrawAcceptanceCommand(Guid StaffUserId, Guid ProposalId);

/// <summary>Withdraws an acceptance while holding the affected proposal row lock.</summary>
/// <param name="proposals">The event proposal repository.</param>
/// <param name="access">The staff access authorizer.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit logger.</param>
public sealed class WithdrawAcceptanceHandler(
    IEventProposalRepository proposals,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result> HandleAsync(
        WithdrawAcceptanceCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId, StaffCapability.ManageEventNegotiation, null, ct);
        if (authorized.IsFailure) return Result.Failure(authorized.Error);

        // A scoped role with no scope is granted nothing (FR-10.7), and negotiation is
        // judged on the caller's own appointment type.
        if (authorized.Value.AppointmentTypeId is not { } actingType)
            return Result.Failure(
                Error.Forbidden("Withdrawing needs an assigned appointment type."));

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var proposal = await proposals.LockForUpdateAsync(command.ProposalId, ct);
        if (proposal is null) return Result.Failure(Error.NotFound("No such proposal."));

        // The status is not checked by hand: the aggregate decides, and it reports the
        // status it actually has. Checking here would be a second copy of the rule that
        // could disagree with the first.
        try
        {
            proposal.WithdrawAcceptance(actingType);
        }
        // Ordered, not interchangeable: ProposalNotOpenException derives from
        // DomainException, so the general catch first would swallow it and turn a
        // conflict into a validation error.
        catch (ProposalNotOpenException ex)
        {
            return Result.Failure(Error.ProposalNotOpen(
                $"The proposal is {ex.CurrentStatus} and can no longer be changed."));
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }

        audit.Record(
            AuditEntityTypes.EventProposal,
            proposal.Id,
            AuditAction.AcceptanceWithdrawn,
            ActorType.Staff,
            command.StaffUserId.ToString(),
            null);

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result.Success();
    }
}
