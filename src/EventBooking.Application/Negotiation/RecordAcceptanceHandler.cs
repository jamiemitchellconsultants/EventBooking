using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Negotiation;

/// <summary>Records or revises the calling manager's acceptance of a proposal.</summary>
/// <param name="StaffUserId">The accepting manager.</param>
/// <param name="ProposalId">The proposal identifier.</param>
/// <param name="Headcount">The manager's headcount for their appointment type.</param>
public sealed record RecordAcceptanceCommand(Guid StaffUserId, Guid ProposalId, int Headcount);

/// <summary>The proposal status after accepting, and the event when this was the final accept.</summary>
/// <param name="ProposalId">The proposal identifier.</param>
/// <param name="Status">The proposal status after accepting.</param>
/// <param name="EventId">The event identifier, or null while the proposal stays open.</param>
/// <param name="Changed">Whether the headcount actually changed.</param>
public sealed record RecordAcceptanceOutcome(Guid ProposalId, string Status, Guid? EventId, bool Changed);

/// <summary>Records an acceptance under the proposal row lock and confirms at most one event.</summary>
/// <param name="proposals">The event proposal repository.</param>
/// <param name="events">The event repository.</param>
/// <param name="access">The staff access authorizer.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit logger.</param>
/// <param name="types">The appointment type repository.</param>
public sealed class RecordAcceptanceHandler(
    IEventProposalRepository proposals,
    IEventRepository events,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IAppointmentTypeRepository types)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<RecordAcceptanceOutcome>> HandleAsync(
        RecordAcceptanceCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId, StaffCapability.ManageEventNegotiation, null, ct);
        if (authorized.IsFailure) return Result<RecordAcceptanceOutcome>.Failure(authorized.Error);
        if (authorized.Value.AppointmentTypeId is not { } actingType)
            return Result<RecordAcceptanceOutcome>.Failure(
                Error.Forbidden("Accepting needs an assigned appointment type."));

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var proposal = await proposals.LockForUpdateAsync(command.ProposalId, ct);
        if (proposal is null)
            return Result<RecordAcceptanceOutcome>.Failure(Error.NotFound("No such proposal."));

        var previous = proposal.Acceptances
            .SingleOrDefault(a => a.AppointmentTypeId == actingType)?.Headcount;

        bool changed;
        try
        {
            changed = proposal.Accept(actingType, command.StaffUserId, command.Headcount);
        }
        catch (ProposalNotOpenException ex)
        {
            return Result<RecordAcceptanceOutcome>.Failure(Error.ProposalNotOpen(
                $"The proposal is {ex.CurrentStatus} and can no longer be changed."));
        }
        catch (DomainException ex)
        {
            return Result<RecordAcceptanceOutcome>.Failure(Error.Validation(ex.Message));
        }

        if (!changed)
        {
            await transaction.CommitAsync(ct);
            return Result<RecordAcceptanceOutcome>.Success(new RecordAcceptanceOutcome(
                proposal.Id, proposal.Status.ToString(), null, false));
        }

        var typeCode = await TypeCodeAsync(actingType, ct);
        audit.Record(AuditEntityTypes.EventProposal, proposal.Id, AuditAction.AcceptanceRecorded,
            ActorType.Staff, command.StaffUserId.ToString(),
            previous is null ? $"{typeCode} headcount {command.Headcount}"
                : $"{typeCode} headcount {previous} -> {command.Headcount}");

        Guid? eventId = null;
        try
        {
            if (proposal.IsFullyAccepted)
            {
                var created = Event.CreateFrom(Guid.NewGuid(), proposal);
                await events.AddAsync(created, ct);
                eventId = created.Id;
                audit.Record(AuditEntityTypes.Event, created.Id, AuditAction.EventConfirmed,
                    ActorType.Staff, command.StaffUserId.ToString(), proposal.Window.ToString());
            }

            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(ct);
            return Result<RecordAcceptanceOutcome>.Failure(Error.Validation(ex.Message));
        }
        catch (UniqueConstraintViolationException)
        {
            await transaction.RollbackAsync(ct);
            return Result<RecordAcceptanceOutcome>.Failure(
                Error.Conflict("The proposal was confirmed by a concurrent acceptance."));
        }

        return Result<RecordAcceptanceOutcome>.Success(new RecordAcceptanceOutcome(
            proposal.Id, proposal.Status.ToString(), eventId, true));
    }

    private async Task<string> TypeCodeAsync(Guid actingType, CancellationToken ct) =>
        (await types.GetAsync(actingType, ct))?.Code ?? actingType.ToString();
}
