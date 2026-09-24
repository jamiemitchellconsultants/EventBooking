using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Negotiation;

/// <summary>Proposes a new event window at a location for a set of appointment types.</summary>
/// <param name="StaffUserId">The proposing manager.</param>
/// <param name="LocationId">The location that would host the event.</param>
/// <param name="Date">The window's local date at the location.</param>
/// <param name="StartTime">The window's local start time.</param>
/// <param name="DurationMinutes">The window length in minutes.</param>
/// <param name="ListedAppointmentTypeIds">The appointment types the event would offer.</param>
/// <param name="ProposerHeadcount">The proposer's own headcount.</param>
public sealed record ProposeEventCommand(
    Guid StaffUserId,
    Guid LocationId,
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMinutes,
    IReadOnlyList<Guid> ListedAppointmentTypeIds,
    int ProposerHeadcount = 1);

/// <summary>The proposal that was created, and the event when it confirmed immediately.</summary>
/// <param name="ProposalId">The new proposal identifier.</param>
/// <param name="Status">The proposal status after proposing.</param>
/// <param name="EventId">The event identifier, or null while the proposal stays open.</param>
public sealed record ProposeEventOutcome(Guid ProposalId, string Status, Guid? EventId);

/// <summary>Creates one proposal, confirming immediately when a single type is listed.</summary>
/// <param name="proposals">The event proposal repository.</param>
/// <param name="locations">The location repository.</param>
/// <param name="types">The appointment type repository.</param>
/// <param name="profiles">The staff access profile repository.</param>
/// <param name="access">The staff access authorizer.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit logger.</param>
/// <param name="clock">The clock.</param>
/// <param name="zones">The zone abstraction that interprets the window at its location.</param>
/// <param name="events">The event repository.</param>
public sealed class ProposeEventHandler(
    IEventProposalRepository proposals,
    ILocationRepository locations,
    IAppointmentTypeRepository types,
    IStaffAccessProfileRepository profiles,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock,
    IEventWindowZones zones,
    IEventRepository events)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<ProposeEventOutcome>> HandleAsync(
        ProposeEventCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId, StaffCapability.ManageEventNegotiation, null, ct);
        if (authorized.IsFailure) return Result<ProposeEventOutcome>.Failure(authorized.Error);
        if (authorized.Value.AppointmentTypeId is not { } proposerTypeId)
            return Result<ProposeEventOutcome>.Failure(
                Error.Forbidden("Proposing needs an assigned appointment type."));

        EventWindow window;
        try
        {
            window = new EventWindow(command.Date, command.StartTime, command.DurationMinutes);
        }
        catch (DomainException ex)
        {
            return Result<ProposeEventOutcome>.Failure(Error.Validation(ex.Message));
        }

        var location = await locations.GetAsync(command.LocationId, ct);
        if (location is null) return Result<ProposeEventOutcome>.Failure(Error.NotFound("No such location."));

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var open = await proposals.ListOpenAsync(ct);
        if (open.Any(p =>
                p.LocationId == location.Id
                && p.Window.Date == window.Date
                && p.Window.StartTime == window.StartTime))
        {
            return Result<ProposeEventOutcome>.Failure(Error.Conflict("An open proposal already exists for that window."));
        }

        var listed = await types.ListAsync(ct);
        var listedById = listed.ToDictionary(t => t.Id);
        var managerTypeIds = (await profiles.ListAsync(ct))
            .Where(p => p.IsManager && p.AppointmentTypeId is not null)
            .Select(p => p.AppointmentTypeId!.Value)
            .ToHashSet();

        var requested = (command.ListedAppointmentTypeIds ?? []).Distinct().ToList();
        var offer = requested
            .Select(id => listedById.TryGetValue(id, out var t)
                ? new ProposableAppointmentType(id, t.Code, t.IsActive, managerTypeIds.Contains(id))
                : new ProposableAppointmentType(id, id.ToString(), false, false))
            .ToList();

        // One transaction, not two: a single-type proposal is fully accepted by the
        // proposer's own acceptance, so the event is created before the commit and the
        // audit sequence reads ProposalCreated, AcceptanceRecorded, EventConfirmed.
        try
        {
            var proposal = EventProposal.Propose(
                Guid.NewGuid(), location.Id, location.IsActive, location.TimeZoneId,
                window, zones, clock.UtcNow, offer, proposerTypeId, command.StaffUserId, command.ProposerHeadcount);
            proposals.Add(proposal);
            audit.Record(AuditEntityTypes.EventProposal, proposal.Id, AuditAction.ProposalCreated,
                ActorType.Staff, command.StaffUserId.ToString(), $"types {offer.Count}; zone {location.TimeZoneId}");
            var proposerCode = listedById.TryGetValue(proposerTypeId, out var proposerType)
                ? proposerType.Code
                : proposerTypeId.ToString();
            audit.Record(AuditEntityTypes.EventProposal, proposal.Id, AuditAction.AcceptanceRecorded,
                ActorType.Staff, command.StaffUserId.ToString(),
                $"{proposerCode} headcount {command.ProposerHeadcount}");

            Guid? eventId = null;
            if (proposal.IsFullyAccepted)
            {
                var created = Event.CreateFrom(Guid.NewGuid(), proposal);
                await events.AddAsync(created, ct);
                eventId = created.Id;
                audit.Record(AuditEntityTypes.Event, created.Id, AuditAction.EventConfirmed,
                    ActorType.Staff, command.StaffUserId.ToString(), window.ToString());
            }

            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result<ProposeEventOutcome>.Success(new ProposeEventOutcome(
                proposal.Id, proposal.Status.ToString(), eventId));
        }
        catch (ProposalValidationException ex)
        {
            await transaction.RollbackAsync(ct);
            return Result<ProposeEventOutcome>.Failure(Error.Validation(string.Join("; ", ex.Failures)));
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(ct);
            return Result<ProposeEventOutcome>.Failure(Error.Validation(ex.Message));
        }
        catch (UniqueConstraintViolationException)
        {
            await transaction.RollbackAsync(ct);
            return Result<ProposeEventOutcome>.Failure(Error.Conflict("An open proposal already exists for that window."));
        }
    }
}
