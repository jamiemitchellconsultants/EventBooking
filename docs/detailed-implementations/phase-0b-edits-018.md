# 00b — Vocabulary edits 18 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — src/EventBooking.Application/Events/GetManagerEventBoardHandler.cs — 1/1

<!-- vocabulary-file: {"id":89,"oldPath":"src/EventBooking.Application/Slots/GetManagerSlotBoardHandler.cs","newPath":"src/EventBooking.Application/Events/GetManagerEventBoardHandler.cs","beforeSha":"eda6266bc12628b1cf21f1b0627e0f65ee118adc0b9b204083955bc290ddec21","afterSha":"27bda208456d9147df58f444c691c1ecfeb3ab24f1a6b4de84d12706ef22ecf3","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Events;

/// <summary>Defines open proposal view for the current use case.</summary>
/// <param name="ProposalId">The proposal id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="AcceptedByAppointmentTypeNames">The accepted by appointment type names.</param>
/// <param name="MyAcceptedHeadcount">The my accepted headcount.</param>
/// <param name="AcceptedByMe">The accepted by me.</param>
/// <param name="CreatedByMe">The created by me.</param>
public sealed record OpenProposalView(
    Guid ProposalId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<string> AcceptedByAppointmentTypeNames,
    int? MyAcceptedHeadcount,
    bool AcceptedByMe,
    bool CreatedByMe);

/// <summary>Defines manager event view for the current use case.</summary>
/// <param name="EventId">The event id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="MyHeadcount">The my headcount.</param>
/// <param name="MyRemainingCapacity">The my remaining capacity.</param>
public sealed record ManagerEventView(
    Guid EventId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int MyHeadcount,
    int MyRemainingCapacity);

/// <summary>Defines manager event board for the current use case.</summary>
/// <param name="OpenProposals">The open proposals.</param>
/// <param name="Events">The events.</param>
public sealed record ManagerEventBoard(
    IReadOnlyList<OpenProposalView> OpenProposals,
    IReadOnlyList<ManagerEventView> Events);

/// <summary>Defines get manager event board query for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
public sealed record GetManagerEventBoardQuery(Guid ManagerUserId);

/// <summary>Defines get manager event board handler for the current use case.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="events">The events.</param>
/// <param name="access">The access.</param>
/// <param name="clock">The clock.</param>
public sealed class GetManagerEventBoardHandler(
    IEventProposalRepository proposals,
    IEventRepository events,
    IStaffAccessAuthorizer access,
    IClock clock)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<ManagerEventBoard>> HandleAsync(
        GetManagerEventBoardQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.ManagerUserId,
            StaffCapability.ManageEventNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<ManagerEventBoard>.Failure(authorized.Error);
        }

        var myType = authorized.Value.AppointmentTypeId!.Value;

        var open = (await proposals.ListOpenAsync(cancellationToken))
            .OrderBy(proposal => proposal.Window)
            .Select(proposal =>
            {
                var myAcceptance = proposal.Acceptances.SingleOrDefault(
                    acceptance =>
                        acceptance.AppointmentTypeId == myType
                        && acceptance.ManagerUserId == query.ManagerUserId);

                return new OpenProposalView(
                    proposal.Id,
                    proposal.Window.Date,
                    proposal.Window.StartTime,
                    proposal.Window.EndTime,
                    proposal.Acceptances
                        .Select(acceptance =>
                            AppointmentTypeIds.NameOf(acceptance.AppointmentTypeId))
                        .OrderBy(name => name, StringComparer.Ordinal)
                        .ToList(),
                    myAcceptance?.Headcount,
                    myAcceptance is not null,
                    proposal.CreatedByManagerUserId == query.ManagerUserId);
            })
            .ToList();

        var confirmed = (await events.ListActiveAsync(clock.TodayAtTransitionalLocation, cancellationToken))
            .OrderBy(s => s.Window)
            .Select(s =>
            {
                var capacity = s.CapacityFor(myType);
                return new ManagerEventView(
                    s.Id,
                    s.Window.Date,
                    s.Window.StartTime,
                    s.Window.EndTime,
                    capacity.TotalHeadcount,
                    capacity.RemainingCapacity);
            })
            .ToList();

        return Result<ManagerEventBoard>.Success(new ManagerEventBoard(open, confirmed));
    }
}
`````

## before — src/EventBooking.Application/Slots/ImportConfirmedSlotsHandler.cs — 1/1

<!-- vocabulary-file: {"id":90,"oldPath":"src/EventBooking.Application/Slots/ImportConfirmedSlotsHandler.cs","newPath":"src/EventBooking.Application/Events/ImportEventsHandler.cs","beforeSha":"e3185882c4e4da885498241da3a82990a9092201518af303ef3df46243f9d1cf","afterSha":"6d7fa7ef147a1c6c253e8e2db69532db4c83063489df0a5329759e080a4a54fd","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Slots;

/// <param name="StaffUserId">The staff identity performing the import.</param>
/// <param name="CsvContent">The raw CSV file content.</param>
/// <param name="AllowPastDates">Whether historical slot dates are accepted. Only the demo
/// seeder sets this: its agreed slots are deliberately historical, while the user-facing
/// import requires future dates like slot proposals do.</param>
public sealed record ImportConfirmedSlotsCommand(Guid StaffUserId, string? CsvContent, bool AllowPastDates = false);

/// <summary>Defines confirmed slot import outcome for the current use case.</summary>
/// <param name="Accepted">The accepted.</param>
/// <param name="ImportedCount">The imported count.</param>
/// <param name="Errors">The errors.</param>
public sealed record ConfirmedSlotImportOutcome(
    bool Accepted, int ImportedCount, IReadOnlyList<ConfirmedSlotImportError> Errors);

/// <summary>Defines import confirmed slots handler for the current use case.</summary>
/// <param name="confirmedSlots">The confirmed slots.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
public sealed class ImportConfirmedSlotsHandler(
    IConfirmedSlotRepository confirmedSlots,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<ConfirmedSlotImportOutcome>> HandleAsync(
        ImportConfirmedSlotsCommand command, CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ImportConfirmedSlots,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<ConfirmedSlotImportOutcome>.Failure(authorized.Error);
        }

        var parsed = ConfirmedSlotImportParser.Parse(
            command.CsvContent,
            command.AllowPastDates ? null : clock.TodayAtHeadOffice);
        if (parsed.Errors.Count > 0)
        {
            return Result<ConfirmedSlotImportOutcome>.Success(
                new ConfirmedSlotImportOutcome(false, 0, parsed.Errors));
        }

        foreach (var row in parsed.Rows)
        {
            var slot = ConfirmedSlot.CreateImported(Guid.NewGuid(), row.Window, row.HeadcountsByAppointmentType);
            confirmedSlots.Add(slot);

            audit.Record(
                AuditEntityTypes.ConfirmedSlot,
                slot.Id,
                AuditAction.SlotImported,
                ActorType.Staff,
                command.StaffUserId.ToString(),
                $"Imported from CSV line {row.LineNumber}.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ConfirmedSlotImportOutcome>.Success(
            new ConfirmedSlotImportOutcome(true, parsed.Rows.Count, []));
    }
}
`````

## after — src/EventBooking.Application/Events/ImportEventsHandler.cs — 1/1

<!-- vocabulary-file: {"id":90,"oldPath":"src/EventBooking.Application/Slots/ImportConfirmedSlotsHandler.cs","newPath":"src/EventBooking.Application/Events/ImportEventsHandler.cs","beforeSha":"e3185882c4e4da885498241da3a82990a9092201518af303ef3df46243f9d1cf","afterSha":"6d7fa7ef147a1c6c253e8e2db69532db4c83063489df0a5329759e080a4a54fd","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Events;

/// <param name="StaffUserId">The staff identity performing the import.</param>
/// <param name="CsvContent">The raw CSV file content.</param>
/// <param name="AllowPastDates">Whether historical event dates are accepted. Only the demo
/// seeder sets this: its agreed events are deliberately historical, while the user-facing
/// import requires future dates like event proposals do.</param>
public sealed record ImportEventsCommand(Guid StaffUserId, string? CsvContent, bool AllowPastDates = false);

/// <summary>Defines event import outcome for the current use case.</summary>
/// <param name="Accepted">The accepted.</param>
/// <param name="ImportedCount">The imported count.</param>
/// <param name="Errors">The errors.</param>
public sealed record EventImportOutcome(
    bool Accepted, int ImportedCount, IReadOnlyList<EventImportError> Errors);

/// <summary>Defines import events handler for the current use case.</summary>
/// <param name="events">The events.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
public sealed class ImportEventsHandler(
    IEventRepository events,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<EventImportOutcome>> HandleAsync(
        ImportEventsCommand command, CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ImportEvents,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<EventImportOutcome>.Failure(authorized.Error);
        }

        var parsed = EventImportParser.Parse(
            command.CsvContent,
            command.AllowPastDates ? null : clock.TodayAtTransitionalLocation);
        if (parsed.Errors.Count > 0)
        {
            return Result<EventImportOutcome>.Success(
                new EventImportOutcome(false, 0, parsed.Errors));
        }

        foreach (var row in parsed.Rows)
        {
            var eventItem = Event.CreateImported(Guid.NewGuid(), row.Window, row.HeadcountsByAppointmentType);
            events.Add(eventItem);

            audit.Record(
                AuditEntityTypes.Event,
                eventItem.Id,
                AuditAction.EventImported,
                ActorType.Staff,
                command.StaffUserId.ToString(),
                $"Imported from CSV line {row.LineNumber}.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<EventImportOutcome>.Success(
            new EventImportOutcome(true, parsed.Rows.Count, []));
    }
}
`````

## before — src/EventBooking.Application/Slots/ProposeSlotHandler.cs — 1/1

<!-- vocabulary-file: {"id":91,"oldPath":"src/EventBooking.Application/Slots/ProposeSlotHandler.cs","newPath":"src/EventBooking.Application/Events/ProposeEventHandler.cs","beforeSha":"bf9be5d3612db3db5ed1cdf07fb73921293a119049fcb4dc738df0b58c5ed7b1","afterSha":"e9a8f03cde4775f50395e3e7766157d53255e0370b9c3847d7f7124b6010e31e","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Slots;

/// <summary>Defines propose slot command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
public sealed record ProposeSlotCommand(Guid ManagerUserId, DateOnly Date, TimeOnly StartTime);

/// <summary>Creates one future open proposal inside a transaction protected by a database backstop.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
public sealed class ProposeSlotHandler(
    ISlotProposalRepository proposals,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock)
{
    /// <summary>Creates the requested proposal or returns a stable conflict for its open window.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<Guid>> HandleAsync(
        ProposeSlotCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageSlotNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<Guid>.Failure(authorized.Error);
        }

        SlotWindow window;
        try
        {
            window = new SlotWindow(command.Date, command.StartTime);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Failure(Error.Validation(ex.Message));
        }

        if (!window.StartsAfter(clock.TodayAtHeadOffice))
        {
            return Result<Guid>.Failure(Error.Validation("A slot must be proposed for a future date."));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var open = await proposals.ListOpenAsync(cancellationToken);
        if (open.Any(p => p.Window == window))
        {
            return Result<Guid>.Failure(Error.Conflict("An open proposal already exists for that window."));
        }

        var id = Guid.NewGuid();

        SlotProposal proposal;
        try
        {
            proposal = SlotProposal.Create(id, window, command.ManagerUserId);
            proposals.Add(proposal);

            audit.Record(
                AuditEntityTypes.SlotProposal,
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

<!-- vocabulary-file: {"id":91,"oldPath":"src/EventBooking.Application/Slots/ProposeSlotHandler.cs","newPath":"src/EventBooking.Application/Events/ProposeEventHandler.cs","beforeSha":"bf9be5d3612db3db5ed1cdf07fb73921293a119049fcb4dc738df0b58c5ed7b1","afterSha":"e9a8f03cde4775f50395e3e7766157d53255e0370b9c3847d7f7124b6010e31e","side":"after","part":1,"parts":1} -->

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
            window = new EventWindow(command.Date, command.StartTime);
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

## before — src/EventBooking.Application/Slots/WithdrawAcceptanceHandler.cs — 1/1

<!-- vocabulary-file: {"id":92,"oldPath":"src/EventBooking.Application/Slots/WithdrawAcceptanceHandler.cs","newPath":"src/EventBooking.Application/Events/WithdrawAcceptanceHandler.cs","beforeSha":"7daf22bdf4b0f57422b0a957391697ee57f6ff48b69e30b5c366ed3227cca919","afterSha":"e9a1a0a9103b8bed7de1d29cb40edd15df3d028075becb98aa5c00de7a29cdf4","side":"before","part":1,"parts":1} -->

`````csharp
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
`````

## after — src/EventBooking.Application/Events/WithdrawAcceptanceHandler.cs — 1/1

<!-- vocabulary-file: {"id":92,"oldPath":"src/EventBooking.Application/Slots/WithdrawAcceptanceHandler.cs","newPath":"src/EventBooking.Application/Events/WithdrawAcceptanceHandler.cs","beforeSha":"7daf22bdf4b0f57422b0a957391697ee57f6ff48b69e30b5c366ed3227cca919","afterSha":"e9a1a0a9103b8bed7de1d29cb40edd15df3d028075becb98aa5c00de7a29cdf4","side":"after","part":1,"parts":1} -->

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

## before — src/EventBooking.Application/Slots/WithdrawProposalHandler.cs — 1/1

<!-- vocabulary-file: {"id":93,"oldPath":"src/EventBooking.Application/Slots/WithdrawProposalHandler.cs","newPath":"src/EventBooking.Application/Events/WithdrawProposalHandler.cs","beforeSha":"cb9011b43cfe5b9304e8a25bfca3701b4e66bbee9942b09f5ac04b4236da06d5","afterSha":"448b6ac7e1267f6b805afebc256d75d37e73b297538289fdeaf32cbb373bcaf6","side":"before","part":1,"parts":1} -->

`````csharp
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
`````

## after — src/EventBooking.Application/Events/WithdrawProposalHandler.cs — 1/1

<!-- vocabulary-file: {"id":93,"oldPath":"src/EventBooking.Application/Slots/WithdrawProposalHandler.cs","newPath":"src/EventBooking.Application/Events/WithdrawProposalHandler.cs","beforeSha":"cb9011b43cfe5b9304e8a25bfca3701b4e66bbee9942b09f5ac04b4236da06d5","afterSha":"448b6ac7e1267f6b805afebc256d75d37e73b297538289fdeaf32cbb373bcaf6","side":"after","part":1,"parts":1} -->

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

## before — src/EventBooking.Domain/Audit/ActorType.cs — 1/1

<!-- vocabulary-file: {"id":94,"oldPath":"src/EventBooking.Domain/Audit/ActorType.cs","newPath":"src/EventBooking.Domain/Audit/ActorType.cs","beforeSha":"dd7eb788431c077b73349fa10db7c5716d12dd495eb8687fa3173548cb9a4e8b","afterSha":"27461b60fab320aa22d085fe8c9f4e5707b9c66c4b0451585900137602cc9a1d","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Audit;

/// <summary>Defines actor type for the current use case.</summary>
public enum ActorType
{
    /// <summary>Defines staff for the current use case.</summary>
    Staff = 1,
    /// <summary>Defines candidate token for the current use case.</summary>
    CandidateToken = 2,
    /// <summary>Defines system for the current use case.</summary>
    System = 3,
}
`````

## after — src/EventBooking.Domain/Audit/ActorType.cs — 1/1

<!-- vocabulary-file: {"id":94,"oldPath":"src/EventBooking.Domain/Audit/ActorType.cs","newPath":"src/EventBooking.Domain/Audit/ActorType.cs","beforeSha":"dd7eb788431c077b73349fa10db7c5716d12dd495eb8687fa3173548cb9a4e8b","afterSha":"27461b60fab320aa22d085fe8c9f4e5707b9c66c4b0451585900137602cc9a1d","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Audit;

/// <summary>Defines actor type for the current use case.</summary>
public enum ActorType
{
    /// <summary>Defines staff for the current use case.</summary>
    Staff = 1,
    /// <summary>Defines attendee token for the current use case.</summary>
    AttendeeToken = 2,
    /// <summary>Defines system for the current use case.</summary>
    System = 3,
}
`````

## before — src/EventBooking.Domain/Audit/AuditAction.cs — 1/1

<!-- vocabulary-file: {"id":95,"oldPath":"src/EventBooking.Domain/Audit/AuditAction.cs","newPath":"src/EventBooking.Domain/Audit/AuditAction.cs","beforeSha":"b8f36678c71b37fba9a50f1077087a7752cc79591b62e0e6421bdbe5d5463555","afterSha":"01a641eb364fc18ca072722f09966f130d08519e33654c74ec1116948d4c6499","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Audit;

/// <summary>Defines audit action for the current use case.</summary>
public enum AuditAction
{
    /// <summary>Defines proposal created for the current use case.</summary>
    ProposalCreated = 1,
    /// <summary>Defines proposal withdrawn for the current use case.</summary>
    ProposalWithdrawn = 2,
    /// <summary>Defines acceptance recorded for the current use case.</summary>
    AcceptanceRecorded = 3,
    /// <summary>Defines acceptance withdrawn for the current use case.</summary>
    AcceptanceWithdrawn = 4,
    /// <summary>Defines slot confirmed for the current use case.</summary>
    SlotConfirmed = 5,
    /// <summary>Defines slot cancelled for the current use case.</summary>
    SlotCancelled = 6,
    /// <summary>Defines capacity decremented for the current use case.</summary>
    CapacityDecremented = 7,
    /// <summary>Defines capacity incremented for the current use case.</summary>
    CapacityIncremented = 8,
    /// <summary>Defines invite created for the current use case.</summary>
    InviteCreated = 9,
    /// <summary>Defines invite sent for the current use case.</summary>
    InviteSent = 10,
    /// <summary>Defines invite expired for the current use case.</summary>
    InviteExpired = 11,
    /// <summary>Defines invite option replaced for the current use case.</summary>
    InviteOptionReplaced = 12,
    /// <summary>Defines booking created for the current use case.</summary>
    BookingCreated = 13,
    /// <summary>Defines booking cancelled for the current use case.</summary>
    BookingCancelled = 14,
    /// <summary>Defines capacity adjusted for the current use case.</summary>
    CapacityAdjusted = 15,
    /// <summary>Defines slot imported for the current use case.</summary>
    SlotImported = 16,
    /// <summary>Defines staff access changed for the current use case.</summary>
    StaffAccessChanged = 17,
    /// <summary>Defines staff access removed for the current use case.</summary>
    StaffAccessRemoved = 18,
    /// <summary>Records an Expected appointment moving to CheckedIn.</summary>
    AppointmentCheckedIn = 19,
    /// <summary>Records a CheckedIn appointment moving to Completed.</summary>
    AppointmentCompleted = 20,
    /// <summary>Records an Expected appointment moving to NoShow.</summary>
    AppointmentMarkedNoShow = 21,
    /// <summary>Records one approved reverse appointment transition.</summary>
    AppointmentStatusCorrected = 22,
    /// <summary>Records the initial Employee Group assignment of a Candidate.</summary>
    EmployeeGroupAssigned = 23,
    /// <summary>Records a Candidate Employee Group change and its derived requirements.</summary>
    EmployeeGroupChanged = 24,
    /// <summary>Records a Coordinator issuing a recovery Invite for missed appointments.</summary>
    RecoveryInviteCreated = 25,
    /// <summary>Records a Coordinator cancelling a pending recovery Invite.</summary>
    RecoveryInviteCancelled = 26,
    /// <summary>Records a recovery Booking linked to its original journey root.</summary>
    RecoveryBookingCreated = 27,
    /// <summary>Records a recovery Booking concluded after terminal appointment outcomes.</summary>
    RecoveryBookingConcluded = 28,
    /// <summary>Records an identity-provider-driven role change applied by the claims sync.</summary>
    StaffRolesSynced = 29,
    /// <summary>Records a Coordinator deleting a Candidate and cascading onto their active bookings.</summary>
    CandidateDeleted = 30,
}
`````

## after — src/EventBooking.Domain/Audit/AuditAction.cs — 1/1

<!-- vocabulary-file: {"id":95,"oldPath":"src/EventBooking.Domain/Audit/AuditAction.cs","newPath":"src/EventBooking.Domain/Audit/AuditAction.cs","beforeSha":"b8f36678c71b37fba9a50f1077087a7752cc79591b62e0e6421bdbe5d5463555","afterSha":"01a641eb364fc18ca072722f09966f130d08519e33654c74ec1116948d4c6499","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Audit;

/// <summary>Defines audit action for the current use case.</summary>
public enum AuditAction
{
    /// <summary>Defines proposal created for the current use case.</summary>
    ProposalCreated = 1,
    /// <summary>Defines proposal withdrawn for the current use case.</summary>
    ProposalWithdrawn = 2,
    /// <summary>Defines acceptance recorded for the current use case.</summary>
    AcceptanceRecorded = 3,
    /// <summary>Defines acceptance withdrawn for the current use case.</summary>
    AcceptanceWithdrawn = 4,
    /// <summary>Defines event confirmed for the current use case.</summary>
    EventConfirmed = 5,
    /// <summary>Defines event cancelled for the current use case.</summary>
    EventCancelled = 6,
    /// <summary>Defines capacity decremented for the current use case.</summary>
    CapacityDecremented = 7,
    /// <summary>Defines capacity incremented for the current use case.</summary>
    CapacityIncremented = 8,
    /// <summary>Defines invite created for the current use case.</summary>
    InviteCreated = 9,
    /// <summary>Defines invite sent for the current use case.</summary>
    InviteSent = 10,
    /// <summary>Defines invite expired for the current use case.</summary>
    InviteExpired = 11,
    /// <summary>Defines invite option replaced for the current use case.</summary>
    InviteOptionReplaced = 12,
    /// <summary>Defines booking created for the current use case.</summary>
    BookingCreated = 13,
    /// <summary>Defines booking cancelled for the current use case.</summary>
    BookingCancelled = 14,
    /// <summary>Defines capacity adjusted for the current use case.</summary>
    CapacityAdjusted = 15,
    /// <summary>Defines event imported for the current use case.</summary>
    EventImported = 16,
    /// <summary>Defines staff access changed for the current use case.</summary>
    StaffAccessChanged = 17,
    /// <summary>Defines staff access removed for the current use case.</summary>
    StaffAccessRemoved = 18,
    /// <summary>Records an Expected appointment moving to CheckedIn.</summary>
    AppointmentCheckedIn = 19,
    /// <summary>Records a CheckedIn appointment moving to Completed.</summary>
    AppointmentCompleted = 20,
    /// <summary>Records an Expected appointment moving to NoShow.</summary>
    AppointmentMarkedNoShow = 21,
    /// <summary>Records one approved reverse appointment transition.</summary>
    AppointmentStatusCorrected = 22,
    /// <summary>Records the initial Attendee Group assignment of a Attendee.</summary>
    AttendeeGroupAssigned = 23,
    /// <summary>Records a Attendee Attendee Group change and its derived requirements.</summary>
    AttendeeGroupReassigned = 24,
    /// <summary>Records a Coordinator issuing a recovery Invite for missed appointments.</summary>
    RecoveryInviteCreated = 25,
    /// <summary>Records a Coordinator cancelling a pending recovery Invite.</summary>
    RecoveryInviteCancelled = 26,
    /// <summary>Records a recovery Booking linked to its original journey root.</summary>
    RecoveryBookingCreated = 27,
    /// <summary>Records a recovery Booking concluded after terminal appointment outcomes.</summary>
    RecoveryBookingConcluded = 28,
    /// <summary>Records an identity-provider-driven role change applied by the claims sync.</summary>
    StaffRolesSynced = 29,
    /// <summary>Records a Coordinator deleting a Attendee and cascading onto their active bookings.</summary>
    AttendeeDeleted = 30,
}
`````

## before — src/EventBooking.Domain/Audit/AuditEntityTypes.cs — 1/1

<!-- vocabulary-file: {"id":96,"oldPath":"src/EventBooking.Domain/Audit/AuditEntityTypes.cs","newPath":"src/EventBooking.Domain/Audit/AuditEntityTypes.cs","beforeSha":"130b7421be70c59502be69107d039acc45d1502c3a600ff779d43cf433c8b2af","afterSha":"1c9066132ad85c4bd8ce04efc13128d1f29689436e5ac05107f55ec4f88cd85f","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Audit;

/// <summary>Defines audit entity types for the current use case.</summary>
public static class AuditEntityTypes
{
    /// <summary>Defines slot proposal for the current use case.</summary>
    public const string SlotProposal = "SlotProposal";
    /// <summary>Defines confirmed slot for the current use case.</summary>
    public const string ConfirmedSlot = "ConfirmedSlot";
    /// <summary>Defines invite for the current use case.</summary>
    public const string Invite = "Invite";
    /// <summary>Defines booking for the current use case.</summary>
    public const string Booking = "Booking";
    /// <summary>Defines staff access profile for the current use case.</summary>
    public const string StaffAccessProfile = "StaffAccessProfile";
    /// <summary>Audit entity name for one independently progressing booking appointment.</summary>
    public const string BookingAppointment = "BookingAppointment";
    /// <summary>Audit entity name for one invited person and their derived requirements.</summary>
    public const string Candidate = "Candidate";

    /// <summary>Defines all for the current use case.</summary>
    public static readonly IReadOnlyList<string> All =
        [SlotProposal, ConfirmedSlot, Invite, Booking, StaffAccessProfile, BookingAppointment, Candidate];
}
`````

## after — src/EventBooking.Domain/Audit/AuditEntityTypes.cs — 1/1

<!-- vocabulary-file: {"id":96,"oldPath":"src/EventBooking.Domain/Audit/AuditEntityTypes.cs","newPath":"src/EventBooking.Domain/Audit/AuditEntityTypes.cs","beforeSha":"130b7421be70c59502be69107d039acc45d1502c3a600ff779d43cf433c8b2af","afterSha":"1c9066132ad85c4bd8ce04efc13128d1f29689436e5ac05107f55ec4f88cd85f","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Audit;

/// <summary>Defines audit entity types for the current use case.</summary>
public static class AuditEntityTypes
{
    /// <summary>Defines event proposal for the current use case.</summary>
    public const string EventProposal = "EventProposal";
    /// <summary>Defines event for the current use case.</summary>
    public const string Event = "Event";
    /// <summary>Defines invite for the current use case.</summary>
    public const string Invite = "Invite";
    /// <summary>Defines booking for the current use case.</summary>
    public const string Booking = "Booking";
    /// <summary>Defines staff access profile for the current use case.</summary>
    public const string StaffAccessProfile = "StaffAccessProfile";
    /// <summary>Audit entity name for one independently progressing booking appointment.</summary>
    public const string BookingAppointment = "BookingAppointment";
    /// <summary>Audit entity name for one invited person and their derived requirements.</summary>
    public const string Attendee = "Attendee";

    /// <summary>Defines all for the current use case.</summary>
    public static readonly IReadOnlyList<string> All =
        [EventProposal, Event, Invite, Booking, StaffAccessProfile, BookingAppointment, Attendee];
}
`````

## before — src/EventBooking.Domain/Audit/AuditLog.cs — 1/1

<!-- vocabulary-file: {"id":97,"oldPath":"src/EventBooking.Domain/Audit/AuditLog.cs","newPath":"src/EventBooking.Domain/Audit/AuditLog.cs","beforeSha":"8db55c9761fb0e487e9eded75f1950834bb3b30d614c001536aec9a88c117810","afterSha":"abcc480ed03191855926d1777d3d61b31c6f983a4dbedc01ee27afe136117136","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Audit;

/// <summary>An append-only record of one state change. Never updated, never deleted.</summary>
public sealed class AuditLog
{
    private AuditLog()
    {
        EntityType = string.Empty;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines entity type for the current use case.</summary>
    public string EntityType { get; private set; }

    /// <summary>Defines entity id for the current use case.</summary>
    public Guid EntityId { get; private set; }

    /// <summary>Defines action for the current use case.</summary>
    public AuditAction Action { get; private set; }

    /// <summary>Defines actor type for the current use case.</summary>
    public ActorType ActorType { get; private set; }

    /// <summary>The Entra object id for staff, the invite or booking id for a candidate token,
    /// null for the system.</summary>
    public string? ActorId { get; private set; }

    /// <summary>Defines timestamp for the current use case.</summary>
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>Defines details for the current use case.</summary>
    public string? Details { get; private set; }

    /// <summary>Defines record for the current use case.</summary>
    /// <param name="id">The id.</param>
    /// <param name="entityType">The entity type.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="action">The action.</param>
    /// <param name="actorType">The actor type.</param>
    /// <param name="actorId">The actor id.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <param name="details">The details.</param>
    public static AuditLog Record(
        Guid id,
        string entityType,
        Guid entityId,
        AuditAction action,
        ActorType actorType,
        string? actorId,
        DateTimeOffset timestamp,
        string? details)
    {
        Guard.Against(
            !AuditEntityTypes.All.Contains(entityType),
            $"{entityType} is not an audited entity type.");
        Guard.Against(entityId == Guid.Empty, "entityId must not be empty.");

        if (actorType != ActorType.System)
        {
            Guard.NotBlank(actorId, "actorId");
        }

        return new AuditLog
        {
            Id = id,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ActorType = actorType,
            ActorId = actorId,
            Timestamp = timestamp,
            Details = details,
        };
    }
}
`````

## after — src/EventBooking.Domain/Audit/AuditLog.cs — 1/1

<!-- vocabulary-file: {"id":97,"oldPath":"src/EventBooking.Domain/Audit/AuditLog.cs","newPath":"src/EventBooking.Domain/Audit/AuditLog.cs","beforeSha":"8db55c9761fb0e487e9eded75f1950834bb3b30d614c001536aec9a88c117810","afterSha":"abcc480ed03191855926d1777d3d61b31c6f983a4dbedc01ee27afe136117136","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Audit;

/// <summary>An append-only record of one state change. Never updated, never deleted.</summary>
public sealed class AuditLog
{
    private AuditLog()
    {
        EntityType = string.Empty;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines entity type for the current use case.</summary>
    public string EntityType { get; private set; }

    /// <summary>Defines entity id for the current use case.</summary>
    public Guid EntityId { get; private set; }

    /// <summary>Defines action for the current use case.</summary>
    public AuditAction Action { get; private set; }

    /// <summary>Defines actor type for the current use case.</summary>
    public ActorType ActorType { get; private set; }

    /// <summary>The Entra object id for staff, the invite or booking id for a attendee token,
    /// null for the system.</summary>
    public string? ActorId { get; private set; }

    /// <summary>Defines timestamp for the current use case.</summary>
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>Defines details for the current use case.</summary>
    public string? Details { get; private set; }

    /// <summary>Defines record for the current use case.</summary>
    /// <param name="id">The id.</param>
    /// <param name="entityType">The entity type.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="action">The action.</param>
    /// <param name="actorType">The actor type.</param>
    /// <param name="actorId">The actor id.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <param name="details">The details.</param>
    public static AuditLog Record(
        Guid id,
        string entityType,
        Guid entityId,
        AuditAction action,
        ActorType actorType,
        string? actorId,
        DateTimeOffset timestamp,
        string? details)
    {
        Guard.Against(
            !AuditEntityTypes.All.Contains(entityType),
            $"{entityType} is not an audited entity type.");
        Guard.Against(entityId == Guid.Empty, "entityId must not be empty.");

        if (actorType != ActorType.System)
        {
            Guard.NotBlank(actorId, "actorId");
        }

        return new AuditLog
        {
            Id = id,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ActorType = actorType,
            ActorId = actorId,
            Timestamp = timestamp,
            Details = details,
        };
    }
}
`````
