using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Events;

/// <summary>Defines accept proposal command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="ProposalId">The proposal id.</param>
/// <param name="Headcount">The headcount.</param>
public sealed record AcceptProposalCommand(Guid ManagerUserId, Guid ProposalId, int Headcount);

/// <summary>The event identifier is null unless this accept was the third.</summary>
/// <param name="ProposalId">The proposal id.</param>
/// <param name="EventId">The event id.</param>
public sealed record AcceptProposalOutcome(Guid ProposalId, Guid? EventId);

/// <summary>Records an acceptance under the proposal row lock and confirms at most one eventItem.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="events">The events.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
public sealed class AcceptProposalHandler(
    IEventProposalRepository proposals,
    IEventRepository events,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Records or revises the appointment-type acceptance while preserving confirmation atomicity.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<AcceptProposalOutcome>> HandleAsync(
        AcceptProposalCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageEventNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<AcceptProposalOutcome>.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var proposal = await proposals.LockForUpdateAsync(command.ProposalId, cancellationToken);
        if (proposal is null)
        {
            return Result<AcceptProposalOutcome>.Failure(Error.NotFound("No such proposal."));
        }

        var appointmentTypeId = authorized.Value.AppointmentTypeId!.Value;

        var previousHeadcount = proposal.Acceptances
            .SingleOrDefault(acceptance => acceptance.AppointmentTypeId == appointmentTypeId)
            ?.Headcount;

        bool changed;
        try
        {
            changed = proposal.Accept(
                appointmentTypeId,
                command.ManagerUserId,
                command.Headcount);
        }
        catch (ProposalNotOpenException ex)
        {
            // FR-2.11: nothing about the request is malformed; the proposal moved on.
            return Result<AcceptProposalOutcome>.Failure(
                Error.Conflict($"The proposal is {ex.CurrentStatus} and can no longer be changed."));
        }
        catch (DomainException ex)
        {
            return Result<AcceptProposalOutcome>.Failure(Error.Validation(ex.Message));
        }

        if (!changed)
        {
            await transaction.CommitAsync(cancellationToken);
            return Result<AcceptProposalOutcome>.Success(
                new AcceptProposalOutcome(proposal.Id, null));
        }

        var appointmentTypeName = AppointmentTypeIdsName(appointmentTypeId);
        var auditDetails = previousHeadcount is null
            ? $"{appointmentTypeName} headcount {command.Headcount}"
            : $"{appointmentTypeName} headcount {previousHeadcount} -> {command.Headcount}";

        audit.Record(
            AuditEntityTypes.EventProposal,
            proposal.Id,
            AuditAction.AcceptanceRecorded,
            ActorType.Staff,
            command.ManagerUserId.ToString(),
            auditDetails);

        Guid? eventId = null;

        if (proposal.IsFullyAccepted)
        {
            var createdEventId = Guid.NewGuid();

            Event eventItem;
            try
            {
                eventItem = Event.CreateFrom(createdEventId, proposal);
            }
            catch (DomainException ex)
            {
                return Result<AcceptProposalOutcome>.Failure(Error.Validation(ex.Message));
            }

            events.Add(eventItem);
            eventId = createdEventId;

            audit.Record(
                AuditEntityTypes.Event,
                createdEventId,
                AuditAction.EventConfirmed,
                ActorType.Staff,
                command.ManagerUserId.ToString(),
                eventItem.Window.ToString());
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<AcceptProposalOutcome>.Success(
            new AcceptProposalOutcome(proposal.Id, eventId));
    }

    private static string AppointmentTypeIdsName(Guid appointmentTypeId) =>
        Domain.AppointmentTypes.AppointmentTypeIds.NameOf(appointmentTypeId);
}
