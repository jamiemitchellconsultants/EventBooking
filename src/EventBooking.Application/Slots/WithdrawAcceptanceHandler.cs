using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Slots;

/// <summary>Defines withdraw acceptance command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="ProposalId">The proposal id.</param>
public sealed record WithdrawAcceptanceCommand(Guid ManagerUserId, Guid ProposalId);

/// <summary>Withdraws an acceptance while holding the affected proposal row lock.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
public sealed class WithdrawAcceptanceHandler(
    ISlotProposalRepository proposals,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Removes the caller's appointment-type acceptance or returns the relevant stable failure.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> HandleAsync(
        WithdrawAcceptanceCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageSlotNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var proposal = await proposals.LockForUpdateAsync(command.ProposalId, cancellationToken);
        if (proposal is null)
        {
            return Result.Failure(Error.NotFound("No such proposal."));
        }

        // A confirmed proposal is a conflict rather than a validation error: nothing about the
        // request is malformed, the world moved on.
        if (proposal.Status != SlotProposalStatus.Open)
        {
            return Result.Failure(
                Error.Conflict("An acceptance can only be withdrawn while the proposal is still open."));
        }

        try
        {
            proposal.WithdrawAcceptance(authorized.Value.AppointmentTypeId!.Value, command.ManagerUserId);
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }

        audit.Record(
            AuditEntityTypes.SlotProposal,
            proposal.Id,
            AuditAction.AcceptanceWithdrawn,
            ActorType.Staff,
            command.ManagerUserId.ToString(),
            null);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
