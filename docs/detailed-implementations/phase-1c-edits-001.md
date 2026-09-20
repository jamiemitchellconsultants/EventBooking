# 01c — Negotiation across any number of types, edits 1 (Task 6)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — src/EventBooking.Application/Events/AcceptProposalHandler.cs — 1/1

<!-- retirement-file: {"id":0,"file":"src/EventBooking.Application/Events/AcceptProposalHandler.cs","beforeSha":"1114eb286da051e3655baf73c94558fa3e4826bfda94cb73646dff92f2ff1de0","afterSha":"004ef2c00f51dc7bd48137df1f99b60454912684284fd11ad8ce4f88b2e5e41e","side":"before","part":1,"parts":1} -->

`````csharp
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
`````

## after — src/EventBooking.Application/Events/AcceptProposalHandler.cs — 1/1

<!-- retirement-file: {"id":0,"file":"src/EventBooking.Application/Events/AcceptProposalHandler.cs","beforeSha":"1114eb286da051e3655baf73c94558fa3e4826bfda94cb73646dff92f2ff1de0","afterSha":"004ef2c00f51dc7bd48137df1f99b60454912684284fd11ad8ce4f88b2e5e41e","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## before — src/EventBooking.Application/Events/ProposeEventHandler.cs — 1/1

<!-- retirement-file: {"id":1,"file":"src/EventBooking.Application/Events/ProposeEventHandler.cs","beforeSha":"05566bb17f0a18d98096e6e9a354fbb44dcc33359e4dadc2dfe34f6a560b216d","afterSha":"0ffbdb7ef1635117bd7d62ec315fe58a9eec80b3df106a1d510e9df66cf18317","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Events;

/// <summary>Defines propose event command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
public sealed record ProposeEventCommand(Guid ManagerUserId, DateOnly Date, TimeOnly StartTime);

/// <summary>Creates one future open proposal inside a transaction protected by a database backstop.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
public sealed class ProposeEventHandler(
    IEventProposalRepository proposals,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock)
{
    /// <summary>Creates the requested proposal or returns a stable conflict for its open window.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<Guid>> HandleAsync(
        ProposeEventCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageEventNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<Guid>.Failure(authorized.Error);
        }

        EventWindow window;
        try
        {
            window = new EventWindow(command.Date, command.StartTime, 240);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Failure(Error.Validation(ex.Message));
        }

        if (!window.StartsAfter(clock.TodayAtTransitionalLocation))
        {
            return Result<Guid>.Failure(Error.Validation("A event must be proposed for a future date."));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var open = await proposals.ListOpenAsync(cancellationToken);
        if (open.Any(p => p.Window == window))
        {
            return Result<Guid>.Failure(Error.Conflict("An open proposal already exists for that window."));
        }

        var id = Guid.NewGuid();

        EventProposal proposal;
        try
        {
            proposal = EventProposal.Create(id, window, command.ManagerUserId);
            proposals.Add(proposal);

            audit.Record(
                AuditEntityTypes.EventProposal,
                id,
                AuditAction.ProposalCreated,
                ActorType.Staff,
                command.ManagerUserId.ToString(),
                window.ToString());

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<Guid>.Failure(Error.Validation(ex.Message));
        }
        catch (UniqueConstraintViolationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<Guid>.Failure(Error.Conflict("An open proposal already exists for that window."));
        }

        return Result<Guid>.Success(id);
    }
}
`````

## after — src/EventBooking.Application/Events/ProposeEventHandler.cs — 1/1

<!-- retirement-file: {"id":1,"file":"src/EventBooking.Application/Events/ProposeEventHandler.cs","beforeSha":"05566bb17f0a18d98096e6e9a354fbb44dcc33359e4dadc2dfe34f6a560b216d","afterSha":"0ffbdb7ef1635117bd7d62ec315fe58a9eec80b3df106a1d510e9df66cf18317","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Events;

/// <summary>Defines propose event command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="Headcount">The proposing Manager's own headcount (FR-2.1).</param>
public sealed record ProposeEventCommand(
    Guid ManagerUserId, DateOnly Date, TimeOnly StartTime, int Headcount = 1);

/// <summary>Creates one future open proposal inside a transaction protected by a database backstop.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
/// <param name="zones">The zone abstraction that interprets the window at its location.</param>
public sealed class ProposeEventHandler(
    IEventProposalRepository proposals,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock,
    IEventWindowZones zones)
{
    /// <summary>Creates the requested proposal or returns a stable conflict for its open window.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<Guid>> HandleAsync(
        ProposeEventCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageEventNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<Guid>.Failure(authorized.Error);
        }


        // A scoped role with no scope is granted nothing (FR-10.7): a proposal must name the
        // proposing appointment type.
        if (authorized.Value.AppointmentTypeId is not { } proposerAppointmentTypeId)
        {
            return Result<Guid>.Failure(Error.Forbidden("Proposing needs an assigned appointment type."));
        }

        EventWindow window;
        try
        {
            window = new EventWindow(command.Date, command.StartTime, 240);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Failure(Error.Validation(ex.Message));
        }

        if (!window.StartsAfter(clock.TodayAtTransitionalLocation))
        {
            return Result<Guid>.Failure(Error.Validation("A event must be proposed for a future date."));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var open = await proposals.ListOpenAsync(cancellationToken);
        if (open.Any(p => p.Window == window))
        {
            return Result<Guid>.Failure(Error.Conflict("An open proposal already exists for that window."));
        }

        var id = Guid.NewGuid();

        EventProposal proposal;
        try
        {
            // The listed set is still the predecessor's three types, and the location is the
            // transitional one, until Task 13 puts both on the command, the API and the MCP tool.
            proposal = EventProposal.Propose(
                id,
                TransitionalLocation.Id,
                locationIsActive: true,
                TransitionalLocation.TimeZoneId,
                window,
                zones,
                clock.UtcNow,
                [.. AppointmentTypeIds.All.Select(typeId => new ProposableAppointmentType(
                    typeId, AppointmentTypeIds.CodeOf(typeId), true, true))],
                proposerAppointmentTypeId,
                command.ManagerUserId,
                command.Headcount);
            proposals.Add(proposal);

            audit.Record(
                AuditEntityTypes.EventProposal,
                id,
                AuditAction.ProposalCreated,
                ActorType.Staff,
                command.ManagerUserId.ToString(),
                window.ToString());

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (ProposalValidationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<Guid>.Failure(Error.Validation(string.Join("; ", ex.Failures)));
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<Guid>.Failure(Error.Validation(ex.Message));
        }
        catch (UniqueConstraintViolationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<Guid>.Failure(Error.Conflict("An open proposal already exists for that window."));
        }

        return Result<Guid>.Success(id);
    }
}
`````

## after — src/EventBooking.Application/Events/TransitionalLocation.cs — 1/1

<!-- retirement-file: {"id":2,"file":"src/EventBooking.Application/Events/TransitionalLocation.cs","beforeSha":null,"afterSha":"e40528a61591c18dc408494650bf994da8801211ad84b297293dfb3b515dd246","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Events;

/// <summary>
/// Proposals name the location that would host them from Task 6, but nothing carries a location
/// into the handlers until Task 13 puts it on the command, the API and the MCP tool. Until then
/// every proposal is made at this one well-known location, exactly as the predecessor's single
/// site behaved. Delete this class in Task 13.
/// </summary>
public static class TransitionalLocation
{
    /// <summary>The single location every proposal is made at until Task 13.</summary>
    public static Guid Id { get; } = Guid.Parse("10000000-0000-0000-0000-000000000001");

    /// <summary>The zone that location is read in, matching the transitional clock.</summary>
    public const string TimeZoneId = "Europe/London";
}
`````

## before — src/EventBooking.Application/Events/WithdrawAcceptanceHandler.cs — 1/1

<!-- retirement-file: {"id":3,"file":"src/EventBooking.Application/Events/WithdrawAcceptanceHandler.cs","beforeSha":"e9a1a0a9103b8bed7de1d29cb40edd15df3d028075becb98aa5c00de7a29cdf4","afterSha":"28b0155e6513f58c3400cac3ac9d44a56770911df55a02c060d09624fb641f16","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Events;

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
    IEventProposalRepository proposals,
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
            StaffCapability.ManageEventNegotiation,
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
        if (proposal.Status != EventProposalStatus.Open)
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
            AuditEntityTypes.EventProposal,
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
`````

## after — src/EventBooking.Application/Events/WithdrawAcceptanceHandler.cs — 1/1

<!-- retirement-file: {"id":3,"file":"src/EventBooking.Application/Events/WithdrawAcceptanceHandler.cs","beforeSha":"e9a1a0a9103b8bed7de1d29cb40edd15df3d028075becb98aa5c00de7a29cdf4","afterSha":"28b0155e6513f58c3400cac3ac9d44a56770911df55a02c060d09624fb641f16","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Events;

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
    IEventProposalRepository proposals,
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
            StaffCapability.ManageEventNegotiation,
            null,
            cancellationToken);
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

        // A confirmed proposal is a conflict rather than a validation error: nothing about the
        // request is malformed, the world moved on.
        if (proposal.Status != EventProposalStatus.Open)
        {
            return Result.Failure(
                Error.Conflict("An acceptance can only be withdrawn while the proposal is still open."));
        }

        try
        {
            proposal.WithdrawAcceptance(actingType);
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
            command.ManagerUserId.ToString(),
            null);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
`````

## before — src/EventBooking.Application/Events/WithdrawProposalHandler.cs — 1/1

<!-- retirement-file: {"id":4,"file":"src/EventBooking.Application/Events/WithdrawProposalHandler.cs","beforeSha":"448b6ac7e1267f6b805afebc256d75d37e73b297538289fdeaf32cbb373bcaf6","afterSha":"e6ab77ef956b68238754e2edfe09389a5adfb2e4a124bb86f027e656d3278025","side":"before","part":1,"parts":1} -->

`````csharp
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

        if (proposal.Status != EventProposalStatus.Open)
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
`````

## after — src/EventBooking.Application/Events/WithdrawProposalHandler.cs — 1/1

<!-- retirement-file: {"id":4,"file":"src/EventBooking.Application/Events/WithdrawProposalHandler.cs","beforeSha":"448b6ac7e1267f6b805afebc256d75d37e73b297538289fdeaf32cbb373bcaf6","afterSha":"e6ab77ef956b68238754e2edfe09389a5adfb2e4a124bb86f027e656d3278025","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## before — src/EventBooking.Domain/Events/Event.cs — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Domain/Events/Event.cs","beforeSha":"7176938fa57aa67da2992f8f04353eb77bc09dd70592e56fde0881e6ef098316","afterSha":"02141017b7ff494efab3e36265b50eadf42a22282b49a3efcc4b7e5a2f5c2023","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Events;

/// <summary>Defines event for the current use case.</summary>
public sealed class Event
{
    private readonly List<EventCapacity> _capacities = [];

    private Event()
    {
        // Required by the persistence layer's constructor binding.
        Window = null!;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines proposal id for the current use case.</summary>
    public Guid ProposalId { get; private set; }

    /// <summary>Defines window for the current use case.</summary>
    public EventWindow Window { get; private set; }

    /// <summary>Defines status for the current use case.</summary>
    public EventStatus Status { get; private set; } = EventStatus.Active;

    /// <summary>Defines capacities for the current use case.</summary>
    public IReadOnlyList<EventCapacity> Capacities => _capacities;

    /// <summary>
    /// The only way a eventItem is created. Marks the proposal confirmed in the same call, so
    /// a proposal can never back a second eventItem.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="proposal">The proposal.</param>
    public static Event CreateFrom(Guid id, EventProposal proposal)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(proposal is null, "proposal must be supplied.");

        proposal!.MarkConfirmed();

        var eventItem = new Event
        {
            Id = id,
            ProposalId = proposal.Id,
            Window = proposal.Window,
            Status = EventStatus.Active,
        };

        foreach (var acceptance in proposal.Acceptances.OrderBy(a => a.AppointmentTypeId))
        {
            eventItem._capacities.Add(
                EventCapacity.Initialise(id, acceptance.AppointmentTypeId, acceptance.Headcount));
        }

        return eventItem;
    }

    /// <summary>Defines capacity for for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public EventCapacity CapacityFor(Guid appointmentTypeId)
    {
        AppointmentTypeIds.EnsureKnown(appointmentTypeId);

        var capacity = _capacities.SingleOrDefault(c => c.AppointmentTypeId == appointmentTypeId);
        Guard.Against(capacity is null, $"This event has no capacity counter for {appointmentTypeId}.");

        return capacity!;
    }

    /// <summary>Defines has spare capacity for all for the current use case.</summary>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public bool HasSpareCapacityForAll(IEnumerable<Guid> appointmentTypeIds) =>
        Status == EventStatus.Active
        && appointmentTypeIds.All(id => CapacityFor(id).HasSpare);

    /// <summary>Defines cancel for the current use case.</summary>
    public void Cancel()
    {
        Guard.Against(Status == EventStatus.Cancelled, "This event has already been cancelled.");
        Status = EventStatus.Cancelled;
    }
}
`````

## after — src/EventBooking.Domain/Events/Event.cs — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Domain/Events/Event.cs","beforeSha":"7176938fa57aa67da2992f8f04353eb77bc09dd70592e56fde0881e6ef098316","afterSha":"02141017b7ff494efab3e36265b50eadf42a22282b49a3efcc4b7e5a2f5c2023","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Events;

/// <summary>Defines event for the current use case.</summary>
public sealed class Event
{
    private readonly List<EventCapacity> _capacities = [];

    private Event()
    {
        // Required by the persistence layer's constructor binding.
        Window = null!;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines proposal id for the current use case.</summary>
    public Guid ProposalId { get; private set; }

    /// <summary>The location hosting the event, carried from its proposal.</summary>
    public Guid LocationId { get; private set; }

    /// <summary>Defines window for the current use case.</summary>
    public EventWindow Window { get; private set; }

    /// <summary>Defines status for the current use case.</summary>
    public EventStatus Status { get; private set; } = EventStatus.Active;

    /// <summary>Defines capacities for the current use case.</summary>
    public IReadOnlyList<EventCapacity> Capacities => _capacities;

    /// <summary>
    /// The only way a eventItem is created. Marks the proposal confirmed in the same call, so
    /// a proposal can never back a second eventItem.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="proposal">The proposal.</param>
    public static Event CreateFrom(Guid id, EventProposal proposal)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(proposal is null, "proposal must be supplied.");

        proposal!.MarkConfirmed();

        var eventItem = new Event
        {
            Id = id,
            ProposalId = proposal.Id,
            LocationId = proposal.LocationId,
            Window = proposal.Window,
            Status = EventStatus.Active,
        };

        foreach (var acceptance in proposal.Acceptances.OrderBy(a => a.AppointmentTypeId))
        {
            eventItem._capacities.Add(
                EventCapacity.Initialise(id, acceptance.AppointmentTypeId, acceptance.Headcount));
        }

        return eventItem;
    }

    /// <summary>Defines capacity for for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public EventCapacity CapacityFor(Guid appointmentTypeId)
    {
        var capacity = _capacities.SingleOrDefault(c => c.AppointmentTypeId == appointmentTypeId);
        Guard.Against(capacity is null, $"This event has no capacity counter for {appointmentTypeId}.");

        return capacity!;
    }

    /// <summary>Defines has spare capacity for all for the current use case.</summary>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public bool HasSpareCapacityForAll(IEnumerable<Guid> appointmentTypeIds) =>
        Status == EventStatus.Active
        && appointmentTypeIds.All(id => CapacityFor(id).HasSpare);

    /// <summary>Defines cancel for the current use case.</summary>
    public void Cancel()
    {
        Guard.Against(Status == EventStatus.Cancelled, "This event has already been cancelled.");
        Status = EventStatus.Cancelled;
    }
}
`````

## before — src/EventBooking.Domain/Events/EventProposal.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Domain/Events/EventProposal.cs","beforeSha":"42f672ffcf6626ea4644697aa738b01434f818e701891cf8d0ad1f1e591b141e","afterSha":"1bdb31907a4b28d4932f594c175ccc29ef6f3cd5818bb9572525b28fd6705815","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Domain.Events;

/// <summary>Defines event proposal for the current use case.</summary>
public sealed class EventProposal
{
    private readonly List<ProposalAcceptance> _acceptances = [];

    private EventProposal()
    {
        // Required by the persistence layer's constructor binding.
        Window = null!;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines window for the current use case.</summary>
    public EventWindow Window { get; private set; }

    /// <summary>Defines status for the current use case.</summary>
    public EventProposalStatus Status { get; private set; } = EventProposalStatus.Open;

    /// <summary>Defines created by manager user id for the current use case.</summary>
    public Guid CreatedByManagerUserId { get; private set; }

    /// <summary>Defines acceptances for the current use case.</summary>
    public IReadOnlyList<ProposalAcceptance> Acceptances => _acceptances;

    /// <summary>Defines create for the current use case.</summary>
    /// <param name="id">The id.</param>
    /// <param name="window">The window.</param>
    /// <param name="createdByManagerUserId">The created by manager user id.</param>
    public static EventProposal Create(Guid id, EventWindow window, Guid createdByManagerUserId)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(window is null, "window must be supplied.");
        Guard.Against(createdByManagerUserId == Guid.Empty, "createdByManagerUserId must not be empty.");

        return new EventProposal
        {
            Id = id,
            Window = window!,
            Status = EventProposalStatus.Open,
            CreatedByManagerUserId = createdByManagerUserId,
        };
    }

    /// <summary>Withdraws an open proposal. Any Manager in scope or Admin may act, not just the creator.</summary>
    /// <param name="managerUserId">The manager user id.</param>
    public void Withdraw(Guid managerUserId)
    {
        Guard.Against(Status != EventProposalStatus.Open, "Only an open proposal can be withdrawn.");
        Guard.Against(managerUserId == Guid.Empty, "managerUserId must not be empty.");

        Status = EventProposalStatus.Withdrawn;
    }

    /// <summary>Defines accept for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    /// <param name="managerUserId">The manager user id.</param>
    /// <param name="headcount">The headcount.</param>
    public bool Accept(Guid appointmentTypeId, Guid managerUserId, int headcount)
    {
        Guard.Against(Status != EventProposalStatus.Open, "Only an open proposal can be accepted.");
        AppointmentTypeIds.EnsureKnown(appointmentTypeId);

        var existing = _acceptances.SingleOrDefault(
            acceptance => acceptance.AppointmentTypeId == appointmentTypeId);

        if (existing is null)
        {
            _acceptances.Add(
                ProposalAcceptance.Record(Id, appointmentTypeId, managerUserId, headcount));
            return true;
        }

        return existing.ChangeHeadcount(headcount);
    }

    /// <summary>Defines withdraw acceptance for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    /// <param name="managerUserId">The manager user id.</param>
    public void WithdrawAcceptance(Guid appointmentTypeId, Guid managerUserId)
    {
        Guard.Against(
            Status != EventProposalStatus.Open,
            "An acceptance can only be withdrawn while the proposal is still open.");
        AppointmentTypeIds.EnsureKnown(appointmentTypeId);

        var acceptance = _acceptances.SingleOrDefault(a => a.AppointmentTypeId == appointmentTypeId);
        Guard.Against(acceptance is null, "This appointment type has not accepted the proposal.");

        _acceptances.Remove(acceptance!);
    }

    /// <summary>Defines is accepted by for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public bool IsAcceptedBy(Guid appointmentTypeId) =>
        _acceptances.Any(a => a.AppointmentTypeId == appointmentTypeId);

    /// <summary>Defines is fully accepted for the current use case.</summary>
    public bool IsFullyAccepted =>
        Status == EventProposalStatus.Open
        && AppointmentTypeIds.All.All(IsAcceptedBy);

    internal void MarkConfirmed()
    {
        Guard.Against(!IsFullyAccepted, "A proposal can only be confirmed once all 3 managers have accepted it.");
        Status = EventProposalStatus.Confirmed;
    }
}
`````
