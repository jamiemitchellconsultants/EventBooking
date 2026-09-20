# 01d — Capacity generalised to N rows, edits 1 (Task 7)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — src/EventBooking.Application/Events/AdjustEventCapacityHandler.cs — 1/1

<!-- retirement-file: {"id":0,"file":"src/EventBooking.Application/Events/AdjustEventCapacityHandler.cs","beforeSha":"64702f7961dc4241dc95379abb9c31b210809571ce20428de60dc25ef8723774","afterSha":"6a0142625d81ba88d2daae76373354a76b213c351caf070487fa20897609ed82","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Events;

/// <summary>Defines adjust event capacity command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="EventId">The event id.</param>
/// <param name="TotalHeadcount">The total headcount.</param>
public sealed record AdjustEventCapacityCommand(
    Guid ManagerUserId,
    Guid EventId,
    int TotalHeadcount);

/// <summary>Defines adjust event capacity outcome for the current use case.</summary>
/// <param name="EventId">The event id.</param>
/// <param name="TotalHeadcount">The total headcount.</param>
/// <param name="RemainingCapacity">The remaining capacity.</param>
public sealed record AdjustEventCapacityOutcome(
    Guid EventId,
    int TotalHeadcount,
    int RemainingCapacity);

/// <summary>Defines adjust event capacity handler for the current use case.</summary>
/// <param name="events">The events.</param>
/// <param name="capacities">The capacities.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
public sealed class AdjustEventCapacityHandler(
    IEventRepository events,
    IEventCapacityRepository capacities,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<AdjustEventCapacityOutcome>> HandleAsync(
        AdjustEventCapacityCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageEventNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(authorized.Error);
        }

        if (command.TotalHeadcount <= 0)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.Validation("totalHeadcount must be greater than zero."));
        }

        await using var transaction =
            await unitOfWork.BeginTransactionAsync(cancellationToken);

        var appointmentTypeId = authorized.Value.AppointmentTypeId!.Value;
        var locked = await capacities.LockForUpdateAsync(
            command.EventId,
            [appointmentTypeId],
            cancellationToken);
        var capacity = locked.SingleOrDefault();

        if (capacity is null)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.NotFound("No such event or capacity for the manager's appointment type."));
        }

        var eventItem = await events.GetAsync(command.EventId, cancellationToken);
        if (eventItem is null)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.NotFound("No such eventItem."));
        }

        if (eventItem.Status != EventStatus.Active)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.Conflict("A cancelled event cannot have its capacity adjusted."));
        }

        if (command.TotalHeadcount < capacity.OccupiedCapacity)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.Conflict(
                    "Headcount cannot be lower than the active-booking count of "
                    + $"{capacity.OccupiedCapacity}."));
        }

        var previousTotal = capacity.TotalHeadcount;
        var previousRemaining = capacity.RemainingCapacity;

        bool changed;
        try
        {
            changed = capacity.AdjustTotalHeadcount(command.TotalHeadcount);
        }
        catch (DomainException exception)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.Validation(exception.Message));
        }

        if (!changed)
        {
            await transaction.CommitAsync(cancellationToken);
            return Success(eventItem.Id, capacity);
        }

        audit.Record(
            AuditEntityTypes.Event,
            eventItem.Id,
            AuditAction.CapacityAdjusted,
            ActorType.Staff,
            command.ManagerUserId.ToString(),
            $"{AppointmentTypeName(appointmentTypeId)} total "
            + $"{previousTotal} -> {capacity.TotalHeadcount}; remaining "
            + $"{previousRemaining} -> {capacity.RemainingCapacity}");

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Success(eventItem.Id, capacity);
    }

    private static Result<AdjustEventCapacityOutcome> Success(
        Guid eventId,
        EventCapacity capacity) =>
        Result<AdjustEventCapacityOutcome>.Success(
            new AdjustEventCapacityOutcome(
                eventId,
                capacity.TotalHeadcount,
                capacity.RemainingCapacity));

    private static string AppointmentTypeName(Guid appointmentTypeId) =>
        Domain.AppointmentTypes.AppointmentTypeIds.NameOf(appointmentTypeId);
}
`````

## after — src/EventBooking.Application/Events/AdjustEventCapacityHandler.cs — 1/1

<!-- retirement-file: {"id":0,"file":"src/EventBooking.Application/Events/AdjustEventCapacityHandler.cs","beforeSha":"64702f7961dc4241dc95379abb9c31b210809571ce20428de60dc25ef8723774","afterSha":"6a0142625d81ba88d2daae76373354a76b213c351caf070487fa20897609ed82","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Events;

/// <summary>Defines adjust event capacity command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="EventId">The event id.</param>
/// <param name="TotalHeadcount">The total headcount.</param>
public sealed record AdjustEventCapacityCommand(
    Guid ManagerUserId,
    Guid EventId,
    int TotalHeadcount);

/// <summary>Defines adjust event capacity outcome for the current use case.</summary>
/// <param name="EventId">The event id.</param>
/// <param name="TotalHeadcount">The total headcount.</param>
/// <param name="RemainingCapacity">The remaining capacity.</param>
public sealed record AdjustEventCapacityOutcome(
    Guid EventId,
    int TotalHeadcount,
    int RemainingCapacity);

/// <summary>Defines adjust event capacity handler for the current use case.</summary>
/// <param name="events">The events.</param>
/// <param name="capacities">The capacities.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
public sealed class AdjustEventCapacityHandler(
    IEventRepository events,
    IEventCapacityRepository capacities,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<AdjustEventCapacityOutcome>> HandleAsync(
        AdjustEventCapacityCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageEventNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(authorized.Error);
        }

        if (command.TotalHeadcount <= 0)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.Validation("totalHeadcount must be greater than zero."));
        }

        await using var transaction =
            await unitOfWork.BeginTransactionAsync(cancellationToken);

        var appointmentTypeId = authorized.Value.AppointmentTypeId!.Value;
        var locked = await capacities.LockForUpdateAsync(
            command.EventId,
            [appointmentTypeId],
            cancellationToken);
        var capacity = locked.SingleOrDefault();

        if (capacity is null)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.NotFound("No such event or capacity for the manager's appointment type."));
        }

        var eventItem = await events.GetAsync(command.EventId, cancellationToken);
        if (eventItem is null)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.NotFound("No such eventItem."));
        }

        if (eventItem.Status != EventStatus.Active)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.Conflict("A cancelled event cannot have its capacity adjusted."));
        }

        var previousTotal = capacity.TotalHeadcount;
        var previousRemaining = capacity.RemainingCapacity;

        CapacityAdjustment adjustment;
        try
        {
            // The occupied count on the locked row is this type's active-booking count: every
            // active booking requiring the type holds exactly one place on it.
            adjustment = capacity.AdjustTotalHeadcount(
                command.TotalHeadcount, capacity.OccupiedCapacity);
        }
        catch (DomainException exception)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.Validation(exception.Message));
        }

        if (adjustment.Status == CapacityAdjustmentStatus.BelowActiveBookings)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.Conflict(
                    "Headcount cannot be lower than the active-booking count of "
                    + $"{adjustment.MinimumTotalHeadcount}."));
        }

        if (!adjustment.Changed)
        {
            await transaction.CommitAsync(cancellationToken);
            return Success(eventItem.Id, capacity);
        }

        audit.Record(
            AuditEntityTypes.Event,
            eventItem.Id,
            AuditAction.CapacityAdjusted,
            ActorType.Staff,
            command.ManagerUserId.ToString(),
            $"{AppointmentTypeName(appointmentTypeId)} total "
            + $"{previousTotal} -> {capacity.TotalHeadcount}; remaining "
            + $"{previousRemaining} -> {capacity.RemainingCapacity}");

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Success(eventItem.Id, capacity);
    }

    private static Result<AdjustEventCapacityOutcome> Success(
        Guid eventId,
        EventCapacity capacity) =>
        Result<AdjustEventCapacityOutcome>.Success(
            new AdjustEventCapacityOutcome(
                eventId,
                capacity.TotalHeadcount,
                capacity.RemainingCapacity));

    private static string AppointmentTypeName(Guid appointmentTypeId) =>
        Domain.AppointmentTypes.AppointmentTypeIds.NameOf(appointmentTypeId);
}
`````

## before — src/EventBooking.Application/Events/CancelEventHandler.cs — 1/1

<!-- retirement-file: {"id":1,"file":"src/EventBooking.Application/Events/CancelEventHandler.cs","beforeSha":"1bb5f009b9ed991f568e41b7467cdef71213e1584b968ceed0ffdbc6135e7a0c","afterSha":"c12e5cbf90e47ea06f7bdda8fb65d06961229072e9ce73c64463ae17839c18f2","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Events;

/// <summary>Requests cancellation of a event and, when authorized, its bookings.</summary>
/// <param name="StaffUserId">The staff identity performing the cancellation.</param>
/// <param name="EventId">The event to cancel.</param>
/// <param name="ConfirmCascade">Whether cancellation of active bookings is authorized.</param>
public sealed record CancelEventCommand(
    Guid StaffUserId,
    Guid EventId,
    bool ConfirmCascade);

/// <summary>Reports the durable booking and reinvite changes made by event cancellation.</summary>
/// <param name="BookingsVoided">The number of active bookings transitioned to cancelled.</param>
/// <param name="AttendeesReinvited">The number of affected attendees issued a replacement invite.</param>
public sealed record CancelEventOutcome(int BookingsVoided, int AttendeesReinvited);

/// <summary>Cancels a event while serializing the attendee lifecycle before event state.</summary>
/// <param name="events">Loads and locks event rows.</param>
/// <param name="bookings">Reads the affected bookings and attendee-ID worklist.</param>
/// <param name="invites">Locks pending invites before booking rows.</param>
/// <param name="attendees">Locks attendee lifecycle roots and returns tracked attendees.</param>
/// 
/// <param name="bookingCanceller">Releases booking capacity under the event lock.</param>
/// <param name="issuer">Creates replacement invites after cancellation.</param>
/// <param name="deliveries">Stages and dispatches cancellation and replacement notifications.</param>
/// <param name="audit">Records cancellation and capacity changes.</param>
/// <param name="unitOfWork">Owns the transaction enclosing the lifecycle transitions.</param>
/// <param name="access">The access.</param>
/// <param name="eventFinder">The event finder.</param>
/// <param name="appointments">The appointments.</param>
/// <param name="clock">The clock.</param>
public sealed class CancelEventHandler(
    IEventRepository events,
    IBookingRepository bookings,
    IInviteRepository invites,
    IAttendeeRepository attendees,
    IStaffAccessAuthorizer access,
    BookingCanceller bookingCanceller,
    InviteIssuer issuer,
    EligibleEventFinder eventFinder,
    IBookingAppointmentRepository appointments,
    EmailDeliveryService deliveries,
    IAuditLogger audit,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Cancels the event and its current active bookings, preserving notification and reinvite
    /// behavior while taking attendee lifecycle locks before the event lock.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<CancelEventOutcome>> HandleAsync(
        CancelEventCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.CancelEvent,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<CancelEventOutcome>.Failure(authorized.Error);
        }

        // This read is only a attendee-id worklist. It is deliberately non-authoritative and
        // no returned booking entity is ever mutated; each identifier is re-read under the
        // attendee lifecycle lock below.
        var affectedAttendeeIds = (await bookings.ListActiveAttendeeIdsForEventAsync(
                command.EventId,
                cancellationToken))
            .Distinct()
            .OrderBy(attendeeId => attendeeId)
            .ToArray();

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var lockedAttendees = new Dictionary<Guid, Attendee>();
        var lockedPending = new Dictionary<Guid, IReadOnlyList<Invite>>();
        var lockedOriginals = new Dictionary<Guid, Booking?>();
        var lockedRecoveries = new Dictionary<Guid, Booking?>();

        // Every attendee lifecycle transition uses Attendee -> Invite -> Booking (original,
        // then active recovery). Attendee identifiers are sorted so a event cancellation
        // touching multiple attendees cannot deadlock another event cancellation that touches
        // the same set in a different order.
        foreach (var attendeeId in affectedAttendeeIds)
        {
            var attendee = await attendees.LockForUpdateAsync(attendeeId, cancellationToken);
            if (attendee is null)
            {
                continue;
            }

            lockedAttendees[attendee.Id] = attendee;
            lockedPending[attendee.Id] = await invites.LockPendingListForAttendeeAsync(
                attendee.Id,
                cancellationToken);
            var original = await bookings.LockActiveOriginalForAttendeeAsync(
                attendee.Id,
                cancellationToken);
            lockedOriginals[attendee.Id] = original;
            lockedRecoveries[attendee.Id] = original is null
                ? null
                : await bookings.LockActiveRecoveryAsync(original.Id, cancellationToken);
        }

        var eventItem = await events.LockForUpdateAsync(command.EventId, cancellationToken);
        if (eventItem is null)
        {
            return Result<CancelEventOutcome>.Failure(Error.NotFound("No such eventItem."));
        }

        if (eventItem.Status == EventStatus.Cancelled)
        {
            return Result<CancelEventOutcome>.Failure(
                Error.Conflict("This event has already been cancelled."));
        }

        // A event can no longer be cancelled once its date has started.
        if (eventItem.Window.Date < clock.TodayAtTransitionalLocation)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelEventOutcome>.Failure(Error.Conflict(
                "This appointment has already taken place and can no longer be cancelled."));
        }

        // Re-read after all lifecycle locks and the event guard. Only the entities returned by the
        // attendee/booking lock calls are eligible for mutation, preventing a stale pre-lock
        // booking instance from being changed.
        var authoritativeBookings = await bookings.ListActiveForEventAsync(eventItem.Id, cancellationToken);
        var affected = new List<AffectedJourneyBooking>(authoritativeBookings.Count);
        foreach (var authoritativeBooking in authoritativeBookings)
        {
            if (!lockedAttendees.TryGetValue(authoritativeBooking.AttendeeId, out var attendee))
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<CancelEventOutcome>.Failure(Error.Conflict(
                    "The event changed while it was being cancelled. Please retry."));
            }

            Booking? locked = null;
            if (lockedOriginals[attendee.Id]?.Id == authoritativeBooking.Id)
            {
                locked = lockedOriginals[attendee.Id];
            }
            else if (lockedRecoveries[attendee.Id]?.Id == authoritativeBooking.Id)
            {
                locked = lockedRecoveries[attendee.Id];
            }

            if (locked is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<CancelEventOutcome>.Failure(Error.Conflict(
                    "The event changed while it was being cancelled. Please retry."));
            }

            affected.Add(new AffectedJourneyBooking(attendee, locked));
        }

        affected.Sort((left, right) => left.Booking.Id.CompareTo(right.Booking.Id));

        if (!command.ConfirmCascade && affected.Count > 0)
        {
            return Result<CancelEventOutcome>.Failure(Error.Conflict(
                $"Cancelling this event will cancel {affected.Count} confirmed bookings. "
                + "Affected attendees will be notified and re-invited. Confirm to proceed."));
        }

        var actorId = command.StaffUserId.ToString();
        var reinvited = 0;
        var dispatches = new List<EventCancellationDispatch>();

        try
        {
            // Cancel first: the re-invites below must not be able to offer this event back.
            eventItem.Cancel();

            audit.Record(
                AuditEntityTypes.Event,
                eventItem.Id,
                AuditAction.EventCancelled,
                ActorType.Staff,
                actorId,
                $"{affected.Count} bookings voided");

            foreach (var (attendee, booking) in affected)
            {
                if (booking.IsOriginal)
                {
                    var released = await bookingCanceller.CancelLockedAsync(
                        booking,
                        eventItem,
                        ActorType.Staff,
                        actorId,
                        cancellationToken);
                    if (released.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelEventOutcome>.Failure(released.Error);
                    }

                    attendee.ResetToNotYetInvited();

                    var issueResult = await issuer.IssueInitialAsync(
                        attendee, 0, ActorType.Staff, actorId, isReinvite: false, cancellationToken);
                    if (issueResult.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelEventOutcome>.Failure(issueResult.Error);
                    }

                    var issued = issueResult.Value;
                    if (issued.Invited)
                    {
                        reinvited++;
                    }

                    var cancellation = deliveries.StagePending(
                        attendee.Id,
                        EmailTemplate.EventCancelledRebookingNeeded,
                        bookingId: booking.Id,
                        eventId: eventItem.Id);
                    deliveries.ClaimForDispatch(cancellation);
                    dispatches.Add(new EventCancellationDispatch(
                        attendee,
                        released.Value,
                        eventItem,
                        cancellation.Id,
                        issued.DispatchPlan));
                }
                else
                {
                    var releasedRecovery = await bookingCanceller.CancelLockedAsync(
                        booking,
                        eventItem,
                        ActorType.Staff,
                        actorId,
                        cancellationToken);
                    if (releasedRecovery.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelEventOutcome>.Failure(releasedRecovery.Error);
                    }

                    var replacementPlan = await TryIssueReplacementRecoveryAsync(
                        attendee,
                        booking,
                        lockedPending[attendee.Id],
                        actorId,
                        cancellationToken);
                    if (replacementPlan.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelEventOutcome>.Failure(replacementPlan.Error);
                    }

                    if (replacementPlan.Value is not null)
                    {
                        reinvited++;
                    }

                    var recoveryCancellation = deliveries.StagePending(
                        attendee.Id,
                        EmailTemplate.EventCancelledRebookingNeeded,
                        bookingId: booking.Id,
                        eventId: eventItem.Id);
                    deliveries.ClaimForDispatch(recoveryCancellation);
                    dispatches.Add(new EventCancellationDispatch(
                        attendee,
                        releasedRecovery.Value,
                        eventItem,
                        recoveryCancellation.Id,
                        replacementPlan.Value));
                }
            }
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelEventOutcome>.Failure(Error.Conflict(ex.Message));
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        foreach (var dispatch in dispatches)
        {
            var replacementSent = false;
            if (dispatch.InvitePlan is { } invitePlan)
            {
                var inviteStatus = await deliveries.DispatchClaimedAsync(
                    invitePlan.DeliveryId, invitePlan.Message, cancellationToken, invitePlan.OnSent);
                replacementSent = inviteStatus == EmailStatus.Sent;
            }

            await deliveries.DispatchClaimedAsync(
                dispatch.CancellationDeliveryId,
                AttendeeEmailComposer.EventCancelled(
                    dispatch.Attendee,
                    dispatch.AppointmentTypeIds,
                    dispatch.Event,
                    replacementSent),
                cancellationToken);
        }

        return Result<CancelEventOutcome>.Success(new CancelEventOutcome(affected.Count, reinvited));
    }

    /// <summary>
    /// Issues a replacement recovery Invite for the cancelled recovery's still-outstanding types,
    /// or returns no plan when three options are unavailable so the Coordinator can act later.
    /// </summary>
    private async Task<Result<EmailDispatchPlan?>> TryIssueReplacementRecoveryAsync(
        Attendee attendee,
        Booking booking,
        IReadOnlyList<Invite> pending,
        string actorId,
        CancellationToken cancellationToken)
    {
        var rootId = booking.RecoveryOfBookingId!.Value;
        var journey = await bookings.ListJourneyAsync(rootId, cancellationToken);
        var rows = await appointments.ListForBookingsAsync(
            journey.Select(entry => entry.Id).ToList(),
            cancellationToken);
        var covered = pending
            .Where(invite => invite.RecoveryOfBookingId.HasValue)
            .SelectMany(invite => invite.RequiredAppointmentTypeIds)
            .Distinct()
            .ToList();
        var selected = new RecoveryRequirementSelector().Select(
            attendee.RequiredAppointmentTypeIds,
            RecoveryConfirmationValidator.BuildAttempts(journey, rows),
            covered);

        if (selected.Count == 0)
        {
            return Result<EmailDispatchPlan?>.Success(null);
        }

        var options = await eventFinder.FindAsync(
            selected,
            Invite.RequiredOptionCount,
            [],
            cancellationToken);
        if (options.Count < Invite.RequiredOptionCount)
        {
            return Result<EmailDispatchPlan?>.Success(null);
        }

        var replacement = await issuer.IssueRecoveryAsync(
            attendee,
            rootId,
            selected,
            options,
            ActorType.Staff,
            actorId,
            cancellationToken);
        if (replacement.IsFailure)
        {
            return Result<EmailDispatchPlan?>.Failure(replacement.Error);
        }

        return Result<EmailDispatchPlan?>.Success(replacement.Value.DispatchPlan);
    }
}

/// <summary>One locked attendee and the locked journey Booking cancelled with the eventItem.</summary>
/// <param name="Attendee">The lifecycle-locked attendee.</param>
/// <param name="Booking">The locked original or recovery Booking on the cancelled eventItem.</param>
internal sealed record AffectedJourneyBooking(Attendee Attendee, Booking Booking);

internal sealed record EventCancellationDispatch(
    Attendee Attendee,
    IReadOnlyList<Guid> AppointmentTypeIds,
    Event Event,
    Guid CancellationDeliveryId,
    EmailDispatchPlan? InvitePlan);
`````

## after — src/EventBooking.Application/Events/CancelEventHandler.cs — 1/1

<!-- retirement-file: {"id":1,"file":"src/EventBooking.Application/Events/CancelEventHandler.cs","beforeSha":"1bb5f009b9ed991f568e41b7467cdef71213e1584b968ceed0ffdbc6135e7a0c","afterSha":"c12e5cbf90e47ea06f7bdda8fb65d06961229072e9ce73c64463ae17839c18f2","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Events;

/// <summary>Requests cancellation of a event and, when authorized, its bookings.</summary>
/// <param name="StaffUserId">The staff identity performing the cancellation.</param>
/// <param name="EventId">The event to cancel.</param>
/// <param name="ConfirmCascade">Whether cancellation of active bookings is authorized.</param>
public sealed record CancelEventCommand(
    Guid StaffUserId,
    Guid EventId,
    bool ConfirmCascade);

/// <summary>Reports the durable booking and reinvite changes made by event cancellation.</summary>
/// <param name="BookingsVoided">The number of active bookings transitioned to cancelled.</param>
/// <param name="AttendeesReinvited">The number of affected attendees issued a replacement invite.</param>
public sealed record CancelEventOutcome(int BookingsVoided, int AttendeesReinvited);

/// <summary>Cancels a event while serializing the attendee lifecycle before event state.</summary>
/// <param name="events">Loads and locks event rows.</param>
/// <param name="bookings">Reads the affected bookings and attendee-ID worklist.</param>
/// <param name="invites">Locks pending invites before booking rows.</param>
/// <param name="attendees">Locks attendee lifecycle roots and returns tracked attendees.</param>
/// 
/// <param name="bookingCanceller">Releases booking capacity under the event lock.</param>
/// <param name="issuer">Creates replacement invites after cancellation.</param>
/// <param name="deliveries">Stages and dispatches cancellation and replacement notifications.</param>
/// <param name="audit">Records cancellation and capacity changes.</param>
/// <param name="unitOfWork">Owns the transaction enclosing the lifecycle transitions.</param>
/// <param name="access">The access.</param>
/// <param name="eventFinder">The event finder.</param>
/// <param name="appointments">The appointments.</param>
/// <param name="clock">The clock.</param>
/// <param name="zones">The zone abstraction the window's start instant is read in.</param>
public sealed class CancelEventHandler(
    IEventRepository events,
    IBookingRepository bookings,
    IInviteRepository invites,
    IAttendeeRepository attendees,
    IStaffAccessAuthorizer access,
    BookingCanceller bookingCanceller,
    InviteIssuer issuer,
    EligibleEventFinder eventFinder,
    IBookingAppointmentRepository appointments,
    EmailDeliveryService deliveries,
    IAuditLogger audit,
    IClock clock,
    IEventWindowZones zones,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Cancels the event and its current active bookings, preserving notification and reinvite
    /// behavior while taking attendee lifecycle locks before the event lock.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<CancelEventOutcome>> HandleAsync(
        CancelEventCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.CancelEvent,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<CancelEventOutcome>.Failure(authorized.Error);
        }

        // This read is only a attendee-id worklist. It is deliberately non-authoritative and
        // no returned booking entity is ever mutated; each identifier is re-read under the
        // attendee lifecycle lock below.
        var affectedAttendeeIds = (await bookings.ListActiveAttendeeIdsForEventAsync(
                command.EventId,
                cancellationToken))
            .Distinct()
            .OrderBy(attendeeId => attendeeId)
            .ToArray();

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var lockedAttendees = new Dictionary<Guid, Attendee>();
        var lockedPending = new Dictionary<Guid, IReadOnlyList<Invite>>();
        var lockedOriginals = new Dictionary<Guid, Booking?>();
        var lockedRecoveries = new Dictionary<Guid, Booking?>();

        // Every attendee lifecycle transition uses Attendee -> Invite -> Booking (original,
        // then active recovery). Attendee identifiers are sorted so a event cancellation
        // touching multiple attendees cannot deadlock another event cancellation that touches
        // the same set in a different order.
        foreach (var attendeeId in affectedAttendeeIds)
        {
            var attendee = await attendees.LockForUpdateAsync(attendeeId, cancellationToken);
            if (attendee is null)
            {
                continue;
            }

            lockedAttendees[attendee.Id] = attendee;
            lockedPending[attendee.Id] = await invites.LockPendingListForAttendeeAsync(
                attendee.Id,
                cancellationToken);
            var original = await bookings.LockActiveOriginalForAttendeeAsync(
                attendee.Id,
                cancellationToken);
            lockedOriginals[attendee.Id] = original;
            lockedRecoveries[attendee.Id] = original is null
                ? null
                : await bookings.LockActiveRecoveryAsync(original.Id, cancellationToken);
        }

        var eventItem = await events.LockForUpdateAsync(command.EventId, cancellationToken);
        if (eventItem is null)
        {
            return Result<CancelEventOutcome>.Failure(Error.NotFound("No such eventItem."));
        }

        if (eventItem.Status == EventStatus.Cancelled)
        {
            return Result<CancelEventOutcome>.Failure(
                Error.Conflict("This event has already been cancelled."));
        }

        // An event can no longer be cancelled once its window has started (Task 7). The zone is
        // still the transitional location's until Phase 3 gives the handler the event's own.
        if (eventItem.Window.HasStarted(
                zones, TransitionalLocation.TimeZoneId, clock.UtcNow))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelEventOutcome>.Failure(Error.Conflict(
                "This appointment has already taken place and can no longer be cancelled."));
        }

        // Re-read after all lifecycle locks and the event guard. Only the entities returned by the
        // attendee/booking lock calls are eligible for mutation, preventing a stale pre-lock
        // booking instance from being changed.
        var authoritativeBookings = await bookings.ListActiveForEventAsync(eventItem.Id, cancellationToken);
        var affected = new List<AffectedJourneyBooking>(authoritativeBookings.Count);
        foreach (var authoritativeBooking in authoritativeBookings)
        {
            if (!lockedAttendees.TryGetValue(authoritativeBooking.AttendeeId, out var attendee))
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<CancelEventOutcome>.Failure(Error.Conflict(
                    "The event changed while it was being cancelled. Please retry."));
            }

            Booking? locked = null;
            if (lockedOriginals[attendee.Id]?.Id == authoritativeBooking.Id)
            {
                locked = lockedOriginals[attendee.Id];
            }
            else if (lockedRecoveries[attendee.Id]?.Id == authoritativeBooking.Id)
            {
                locked = lockedRecoveries[attendee.Id];
            }

            if (locked is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<CancelEventOutcome>.Failure(Error.Conflict(
                    "The event changed while it was being cancelled. Please retry."));
            }

            affected.Add(new AffectedJourneyBooking(attendee, locked));
        }

        affected.Sort((left, right) => left.Booking.Id.CompareTo(right.Booking.Id));

        if (!command.ConfirmCascade && affected.Count > 0)
        {
            return Result<CancelEventOutcome>.Failure(Error.Conflict(
                $"Cancelling this event will cancel {affected.Count} confirmed bookings. "
                + "Affected attendees will be notified and re-invited. Confirm to proceed."));
        }

        var actorId = command.StaffUserId.ToString();
        var reinvited = 0;
        var dispatches = new List<EventCancellationDispatch>();

        try
        {
            // Cancel first: the re-invites below must not be able to offer this event back.
            eventItem.Cancel(zones, TransitionalLocation.TimeZoneId, clock.UtcNow);

            audit.Record(
                AuditEntityTypes.Event,
                eventItem.Id,
                AuditAction.EventCancelled,
                ActorType.Staff,
                actorId,
                $"{affected.Count} bookings voided");

            foreach (var (attendee, booking) in affected)
            {
                if (booking.IsOriginal)
                {
                    var released = await bookingCanceller.CancelLockedAsync(
                        booking,
                        eventItem,
                        ActorType.Staff,
                        actorId,
                        cancellationToken);
                    if (released.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelEventOutcome>.Failure(released.Error);
                    }

                    attendee.ResetToNotYetInvited();

                    var issueResult = await issuer.IssueInitialAsync(
                        attendee, 0, ActorType.Staff, actorId, isReinvite: false, cancellationToken);
                    if (issueResult.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelEventOutcome>.Failure(issueResult.Error);
                    }

                    var issued = issueResult.Value;
                    if (issued.Invited)
                    {
                        reinvited++;
                    }

                    var cancellation = deliveries.StagePending(
                        attendee.Id,
                        EmailTemplate.EventCancelledRebookingNeeded,
                        bookingId: booking.Id,
                        eventId: eventItem.Id);
                    deliveries.ClaimForDispatch(cancellation);
                    dispatches.Add(new EventCancellationDispatch(
                        attendee,
                        released.Value,
                        eventItem,
                        cancellation.Id,
                        issued.DispatchPlan));
                }
                else
                {
                    var releasedRecovery = await bookingCanceller.CancelLockedAsync(
                        booking,
                        eventItem,
                        ActorType.Staff,
                        actorId,
                        cancellationToken);
                    if (releasedRecovery.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelEventOutcome>.Failure(releasedRecovery.Error);
                    }

                    var replacementPlan = await TryIssueReplacementRecoveryAsync(
                        attendee,
                        booking,
                        lockedPending[attendee.Id],
                        actorId,
                        cancellationToken);
                    if (replacementPlan.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelEventOutcome>.Failure(replacementPlan.Error);
                    }

                    if (replacementPlan.Value is not null)
                    {
                        reinvited++;
                    }

                    var recoveryCancellation = deliveries.StagePending(
                        attendee.Id,
                        EmailTemplate.EventCancelledRebookingNeeded,
                        bookingId: booking.Id,
                        eventId: eventItem.Id);
                    deliveries.ClaimForDispatch(recoveryCancellation);
                    dispatches.Add(new EventCancellationDispatch(
                        attendee,
                        releasedRecovery.Value,
                        eventItem,
                        recoveryCancellation.Id,
                        replacementPlan.Value));
                }
            }
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelEventOutcome>.Failure(Error.Conflict(ex.Message));
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        foreach (var dispatch in dispatches)
        {
            var replacementSent = false;
            if (dispatch.InvitePlan is { } invitePlan)
            {
                var inviteStatus = await deliveries.DispatchClaimedAsync(
                    invitePlan.DeliveryId, invitePlan.Message, cancellationToken, invitePlan.OnSent);
                replacementSent = inviteStatus == EmailStatus.Sent;
            }

            await deliveries.DispatchClaimedAsync(
                dispatch.CancellationDeliveryId,
                AttendeeEmailComposer.EventCancelled(
                    dispatch.Attendee,
                    dispatch.AppointmentTypeIds,
                    dispatch.Event,
                    replacementSent),
                cancellationToken);
        }

        return Result<CancelEventOutcome>.Success(new CancelEventOutcome(affected.Count, reinvited));
    }

    /// <summary>
    /// Issues a replacement recovery Invite for the cancelled recovery's still-outstanding types,
    /// or returns no plan when three options are unavailable so the Coordinator can act later.
    /// </summary>
    private async Task<Result<EmailDispatchPlan?>> TryIssueReplacementRecoveryAsync(
        Attendee attendee,
        Booking booking,
        IReadOnlyList<Invite> pending,
        string actorId,
        CancellationToken cancellationToken)
    {
        var rootId = booking.RecoveryOfBookingId!.Value;
        var journey = await bookings.ListJourneyAsync(rootId, cancellationToken);
        var rows = await appointments.ListForBookingsAsync(
            journey.Select(entry => entry.Id).ToList(),
            cancellationToken);
        var covered = pending
            .Where(invite => invite.RecoveryOfBookingId.HasValue)
            .SelectMany(invite => invite.RequiredAppointmentTypeIds)
            .Distinct()
            .ToList();
        var selected = new RecoveryRequirementSelector().Select(
            attendee.RequiredAppointmentTypeIds,
            RecoveryConfirmationValidator.BuildAttempts(journey, rows),
            covered);

        if (selected.Count == 0)
        {
            return Result<EmailDispatchPlan?>.Success(null);
        }

        var options = await eventFinder.FindAsync(
            selected,
            Invite.RequiredOptionCount,
            [],
            cancellationToken);
        if (options.Count < Invite.RequiredOptionCount)
        {
            return Result<EmailDispatchPlan?>.Success(null);
        }

        var replacement = await issuer.IssueRecoveryAsync(
            attendee,
            rootId,
            selected,
            options,
            ActorType.Staff,
            actorId,
            cancellationToken);
        if (replacement.IsFailure)
        {
            return Result<EmailDispatchPlan?>.Failure(replacement.Error);
        }

        return Result<EmailDispatchPlan?>.Success(replacement.Value.DispatchPlan);
    }
}

/// <summary>One locked attendee and the locked journey Booking cancelled with the eventItem.</summary>
/// <param name="Attendee">The lifecycle-locked attendee.</param>
/// <param name="Booking">The locked original or recovery Booking on the cancelled eventItem.</param>
internal sealed record AffectedJourneyBooking(Attendee Attendee, Booking Booking);

internal sealed record EventCancellationDispatch(
    Attendee Attendee,
    IReadOnlyList<Guid> AppointmentTypeIds,
    Event Event,
    Guid CancellationDeliveryId,
    EmailDispatchPlan? InvitePlan);
`````

## after — src/EventBooking.Domain/Events/CapacityAdjustment.cs — 1/1

<!-- retirement-file: {"id":2,"file":"src/EventBooking.Domain/Events/CapacityAdjustment.cs","beforeSha":null,"afterSha":"5eb72984b3a5aa57851e9ddc19c673e5878fd71ffc7d179fe689b76d42ee46cd","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Events;

/// <summary>What a headcount adjustment did, or why it was refused (FR-3.5, FR-3.6).</summary>
public enum CapacityAdjustmentStatus
{
    /// <summary>The total and the remaining count both moved by the same delta.</summary>
    Adjusted,

    /// <summary>The submitted total equalled the current one: nothing changed, so nothing is audited.</summary>
    Unchanged,

    /// <summary>The submitted total was below the type's active-booking count; nothing changed.</summary>
    BelowActiveBookings,
}

/// <summary>
/// The outcome of a headcount adjustment. A refusal carries the minimum the row would accept and
/// the current server values, which is what FR-3.6 requires the Manager to be told.
/// </summary>
/// <param name="Status">What the adjustment did, or why it was refused.</param>
/// <param name="MinimumTotalHeadcount">The lowest total this row would accept.</param>
/// <param name="TotalHeadcount">The total after the call.</param>
/// <param name="RemainingCapacity">The remaining count after the call.</param>
public readonly record struct CapacityAdjustment(
    CapacityAdjustmentStatus Status,
    int MinimumTotalHeadcount,
    int TotalHeadcount,
    int RemainingCapacity)
{
    /// <summary>Whether anything moved, and so whether there is anything to audit.</summary>
    public bool Changed => Status == CapacityAdjustmentStatus.Adjusted;
}
`````

## before — src/EventBooking.Domain/Events/Event.cs — 1/1

<!-- retirement-file: {"id":3,"file":"src/EventBooking.Domain/Events/Event.cs","beforeSha":"02141017b7ff494efab3e36265b50eadf42a22282b49a3efcc4b7e5a2f5c2023","afterSha":"cc6ee56361e57c11a94f530ba83e67d9f083d353147d3409c48ff26d5bbdd3c6","side":"before","part":1,"parts":1} -->

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
