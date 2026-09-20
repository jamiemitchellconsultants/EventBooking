# 00b — Vocabulary edits 17 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — src/EventBooking.Application/Slots/CancelConfirmedSlotHandler.cs — 1/1

<!-- vocabulary-file: {"id":87,"oldPath":"src/EventBooking.Application/Slots/CancelConfirmedSlotHandler.cs","newPath":"src/EventBooking.Application/Events/CancelEventHandler.cs","beforeSha":"dab740fb627391cbe1ba02cb1f53c57e5e7454e9a97891b35030d1f4939e7747","afterSha":"1bb5f009b9ed991f568e41b7467cdef71213e1584b968ceed0ffdbc6135e7a0c","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Slots;

/// <summary>Requests cancellation of a confirmed slot and, when authorized, its bookings.</summary>
/// <param name="StaffUserId">The staff identity performing the cancellation.</param>
/// <param name="ConfirmedSlotId">The confirmed slot to cancel.</param>
/// <param name="ConfirmCascade">Whether cancellation of active bookings is authorized.</param>
public sealed record CancelConfirmedSlotCommand(
    Guid StaffUserId,
    Guid ConfirmedSlotId,
    bool ConfirmCascade);

/// <summary>Reports the durable booking and reinvite changes made by slot cancellation.</summary>
/// <param name="BookingsVoided">The number of active bookings transitioned to cancelled.</param>
/// <param name="CandidatesReinvited">The number of affected candidates issued a replacement invite.</param>
public sealed record CancelSlotOutcome(int BookingsVoided, int CandidatesReinvited);

/// <summary>Cancels a confirmed slot while serializing the candidate lifecycle before slot state.</summary>
/// <param name="slots">Loads and locks confirmed-slot rows.</param>
/// <param name="bookings">Reads the affected bookings and candidate-ID worklist.</param>
/// <param name="invites">Locks pending invites before booking rows.</param>
/// <param name="candidates">Locks candidate lifecycle roots and returns tracked candidates.</param>
/// 
/// <param name="bookingCanceller">Releases booking capacity under the slot lock.</param>
/// <param name="issuer">Creates replacement invites after cancellation.</param>
/// <param name="deliveries">Stages and dispatches cancellation and replacement notifications.</param>
/// <param name="audit">Records cancellation and capacity changes.</param>
/// <param name="unitOfWork">Owns the transaction enclosing the lifecycle transitions.</param>
/// <param name="access">The access.</param>
/// <param name="slotFinder">The slot finder.</param>
/// <param name="appointments">The appointments.</param>
/// <param name="clock">The clock.</param>
public sealed class CancelConfirmedSlotHandler(
    IConfirmedSlotRepository slots,
    IBookingRepository bookings,
    IInviteRepository invites,
    ICandidateRepository candidates,
    IStaffAccessAuthorizer access,
    BookingCanceller bookingCanceller,
    InviteIssuer issuer,
    EligibleSlotFinder slotFinder,
    IBookingAppointmentRepository appointments,
    EmailDeliveryService deliveries,
    IAuditLogger audit,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Cancels the slot and its current active bookings, preserving notification and reinvite
    /// behavior while taking candidate lifecycle locks before the confirmed-slot lock.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<CancelSlotOutcome>> HandleAsync(
        CancelConfirmedSlotCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.CancelConfirmedSlot,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<CancelSlotOutcome>.Failure(authorized.Error);
        }

        // This read is only a candidate-id worklist. It is deliberately non-authoritative and
        // no returned booking entity is ever mutated; each identifier is re-read under the
        // candidate lifecycle lock below.
        var affectedCandidateIds = (await bookings.ListActiveCandidateIdsForSlotAsync(
                command.ConfirmedSlotId,
                cancellationToken))
            .Distinct()
            .OrderBy(candidateId => candidateId)
            .ToArray();

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var lockedCandidates = new Dictionary<Guid, Candidate>();
        var lockedPending = new Dictionary<Guid, IReadOnlyList<Invite>>();
        var lockedOriginals = new Dictionary<Guid, Booking?>();
        var lockedRecoveries = new Dictionary<Guid, Booking?>();

        // Every candidate lifecycle transition uses Candidate -> Invite -> Booking (original,
        // then active recovery). Candidate identifiers are sorted so a slot cancellation
        // touching multiple candidates cannot deadlock another slot cancellation that touches
        // the same set in a different order.
        foreach (var candidateId in affectedCandidateIds)
        {
            var candidate = await candidates.LockForUpdateAsync(candidateId, cancellationToken);
            if (candidate is null)
            {
                continue;
            }

            lockedCandidates[candidate.Id] = candidate;
            lockedPending[candidate.Id] = await invites.LockPendingListForCandidateAsync(
                candidate.Id,
                cancellationToken);
            var original = await bookings.LockActiveOriginalForCandidateAsync(
                candidate.Id,
                cancellationToken);
            lockedOriginals[candidate.Id] = original;
            lockedRecoveries[candidate.Id] = original is null
                ? null
                : await bookings.LockActiveRecoveryAsync(original.Id, cancellationToken);
        }

        var slot = await slots.LockForUpdateAsync(command.ConfirmedSlotId, cancellationToken);
        if (slot is null)
        {
            return Result<CancelSlotOutcome>.Failure(Error.NotFound("No such slot."));
        }

        if (slot.Status == ConfirmedSlotStatus.Cancelled)
        {
            return Result<CancelSlotOutcome>.Failure(
                Error.Conflict("This slot has already been cancelled."));
        }

        // A slot can no longer be cancelled once its date has started.
        if (slot.Window.Date < clock.TodayAtHeadOffice)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelSlotOutcome>.Failure(Error.Conflict(
                "This appointment has already taken place and can no longer be cancelled."));
        }

        // Re-read after all lifecycle locks and the slot guard. Only the entities returned by the
        // candidate/booking lock calls are eligible for mutation, preventing a stale pre-lock
        // booking instance from being changed.
        var authoritativeBookings = await bookings.ListActiveForSlotAsync(slot.Id, cancellationToken);
        var affected = new List<AffectedJourneyBooking>(authoritativeBookings.Count);
        foreach (var authoritativeBooking in authoritativeBookings)
        {
            if (!lockedCandidates.TryGetValue(authoritativeBooking.CandidateId, out var candidate))
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<CancelSlotOutcome>.Failure(Error.Conflict(
                    "The slot changed while it was being cancelled. Please retry."));
            }

            Booking? locked = null;
            if (lockedOriginals[candidate.Id]?.Id == authoritativeBooking.Id)
            {
                locked = lockedOriginals[candidate.Id];
            }
            else if (lockedRecoveries[candidate.Id]?.Id == authoritativeBooking.Id)
            {
                locked = lockedRecoveries[candidate.Id];
            }

            if (locked is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<CancelSlotOutcome>.Failure(Error.Conflict(
                    "The slot changed while it was being cancelled. Please retry."));
            }

            affected.Add(new AffectedJourneyBooking(candidate, locked));
        }

        affected.Sort((left, right) => left.Booking.Id.CompareTo(right.Booking.Id));

        if (!command.ConfirmCascade && affected.Count > 0)
        {
            return Result<CancelSlotOutcome>.Failure(Error.Conflict(
                $"Cancelling this slot will cancel {affected.Count} confirmed bookings. "
                + "Affected candidates will be notified and re-invited. Confirm to proceed."));
        }

        var actorId = command.StaffUserId.ToString();
        var reinvited = 0;
        var dispatches = new List<SlotCancellationDispatch>();

        try
        {
            // Cancel first: the re-invites below must not be able to offer this slot back.
            slot.Cancel();

            audit.Record(
                AuditEntityTypes.ConfirmedSlot,
                slot.Id,
                AuditAction.SlotCancelled,
                ActorType.Staff,
                actorId,
                $"{affected.Count} bookings voided");

            foreach (var (candidate, booking) in affected)
            {
                if (booking.IsOriginal)
                {
                    var released = await bookingCanceller.CancelLockedAsync(
                        booking,
                        slot,
                        ActorType.Staff,
                        actorId,
                        cancellationToken);
                    if (released.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelSlotOutcome>.Failure(released.Error);
                    }

                    candidate.ResetToNotYetInvited();

                    var issueResult = await issuer.IssueInitialAsync(
                        candidate, 0, ActorType.Staff, actorId, isReinvite: false, cancellationToken);
                    if (issueResult.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelSlotOutcome>.Failure(issueResult.Error);
                    }

                    var issued = issueResult.Value;
                    if (issued.Invited)
                    {
                        reinvited++;
                    }

                    var cancellation = deliveries.StagePending(
                        candidate.Id,
                        EmailTemplate.SlotCancelledRebookingNeeded,
                        bookingId: booking.Id,
                        confirmedSlotId: slot.Id);
                    deliveries.ClaimForDispatch(cancellation);
                    dispatches.Add(new SlotCancellationDispatch(
                        candidate,
                        released.Value,
                        slot,
                        cancellation.Id,
                        issued.DispatchPlan));
                }
                else
                {
                    var releasedRecovery = await bookingCanceller.CancelLockedAsync(
                        booking,
                        slot,
                        ActorType.Staff,
                        actorId,
                        cancellationToken);
                    if (releasedRecovery.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelSlotOutcome>.Failure(releasedRecovery.Error);
                    }

                    var replacementPlan = await TryIssueReplacementRecoveryAsync(
                        candidate,
                        booking,
                        lockedPending[candidate.Id],
                        actorId,
                        cancellationToken);
                    if (replacementPlan.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelSlotOutcome>.Failure(replacementPlan.Error);
                    }

                    if (replacementPlan.Value is not null)
                    {
                        reinvited++;
                    }

                    var recoveryCancellation = deliveries.StagePending(
                        candidate.Id,
                        EmailTemplate.SlotCancelledRebookingNeeded,
                        bookingId: booking.Id,
                        confirmedSlotId: slot.Id);
                    deliveries.ClaimForDispatch(recoveryCancellation);
                    dispatches.Add(new SlotCancellationDispatch(
                        candidate,
                        releasedRecovery.Value,
                        slot,
                        recoveryCancellation.Id,
                        replacementPlan.Value));
                }
            }
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelSlotOutcome>.Failure(Error.Conflict(ex.Message));
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
                CandidateEmailComposer.SlotCancelled(
                    dispatch.Candidate,
                    dispatch.AppointmentTypeIds,
                    dispatch.Slot,
                    replacementSent),
                cancellationToken);
        }

        return Result<CancelSlotOutcome>.Success(new CancelSlotOutcome(affected.Count, reinvited));
    }

    /// <summary>
    /// Issues a replacement recovery Invite for the cancelled recovery's still-outstanding types,
    /// or returns no plan when three options are unavailable so the Coordinator can act later.
    /// </summary>
    private async Task<Result<EmailDispatchPlan?>> TryIssueReplacementRecoveryAsync(
        Candidate candidate,
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
            candidate.RequiredAppointmentTypeIds,
            RecoveryConfirmationValidator.BuildAttempts(journey, rows),
            covered);

        if (selected.Count == 0)
        {
            return Result<EmailDispatchPlan?>.Success(null);
        }

        var options = await slotFinder.FindAsync(
            selected,
            Invite.RequiredOptionCount,
            [],
            cancellationToken);
        if (options.Count < Invite.RequiredOptionCount)
        {
            return Result<EmailDispatchPlan?>.Success(null);
        }

        var replacement = await issuer.IssueRecoveryAsync(
            candidate,
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

/// <summary>One locked candidate and the locked journey Booking cancelled with the slot.</summary>
/// <param name="Candidate">The lifecycle-locked candidate.</param>
/// <param name="Booking">The locked original or recovery Booking on the cancelled slot.</param>
internal sealed record AffectedJourneyBooking(Candidate Candidate, Booking Booking);

internal sealed record SlotCancellationDispatch(
    Candidate Candidate,
    IReadOnlyList<Guid> AppointmentTypeIds,
    ConfirmedSlot Slot,
    Guid CancellationDeliveryId,
    EmailDispatchPlan? InvitePlan);
`````

## after — src/EventBooking.Application/Events/CancelEventHandler.cs — 1/1

<!-- vocabulary-file: {"id":87,"oldPath":"src/EventBooking.Application/Slots/CancelConfirmedSlotHandler.cs","newPath":"src/EventBooking.Application/Events/CancelEventHandler.cs","beforeSha":"dab740fb627391cbe1ba02cb1f53c57e5e7454e9a97891b35030d1f4939e7747","afterSha":"1bb5f009b9ed991f568e41b7467cdef71213e1584b968ceed0ffdbc6135e7a0c","side":"after","part":1,"parts":1} -->

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

## before — src/EventBooking.Application/Slots/ConfirmedSlotImportParser.cs — 1/1

<!-- vocabulary-file: {"id":88,"oldPath":"src/EventBooking.Application/Slots/ConfirmedSlotImportParser.cs","newPath":"src/EventBooking.Application/Events/EventImportParser.cs","beforeSha":"57e01370a4a8ce0696b410dde59506705c4cee1053606b025e9fec4a7b301e88","afterSha":"16dabdd84801aa973eea0cf51817b9ce52338b0b71c9e6c52dc5b5c4cb0e3b53","side":"before","part":1,"parts":1} -->

`````csharp
using System.Globalization;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Slots;

/// <summary>Defines confirmed slot import row for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Window">The window.</param>
/// <param name="HeadcountsByAppointmentType">The headcounts by appointment type.</param>
public sealed record ConfirmedSlotImportRow(
    int LineNumber, SlotWindow Window, IReadOnlyDictionary<Guid, int> HeadcountsByAppointmentType);

/// <summary>Defines confirmed slot import error for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Message">The message.</param>
public sealed record ConfirmedSlotImportError(int LineNumber, string Message);

/// <summary>Defines confirmed slot import parse result for the current use case.</summary>
/// <param name="Rows">The rows.</param>
/// <param name="Errors">The errors.</param>
public sealed record ConfirmedSlotImportParseResult(
    IReadOnlyList<ConfirmedSlotImportRow> Rows,
    IReadOnlyList<ConfirmedSlotImportError> Errors);

/// <summary>
/// Structural validation only, mirroring CandidateCsvParser (Task 29): header, field count,
/// parseable date/time, a positive integer per fixed AppointmentType column, duplicate windows
/// within the file, and a row-count ceiling. Nothing here reads the database.
/// </summary>
public static class ConfirmedSlotImportParser
{
    /// <summary>Defines required header for the current use case.</summary>
    public const string RequiredHeader = "date,startTime,DAT,MED,UNI";
    /// <summary>Defines max rows for the current use case.</summary>
    public const int MaxRows = 200;

    private static readonly (string Code, Guid Id)[] TypeColumns =
    [
        ("DAT", AppointmentTypeIds.DrugAndAlcoholTesting),
        ("MED", AppointmentTypeIds.MedicalCheckUp),
        ("UNI", AppointmentTypeIds.UniformFitting),
    ];

    /// <summary>Defines parse for the current use case.</summary>
    /// <param name="content">The content.</param>
    /// <param name="today">The today.</param>
    public static ConfirmedSlotImportParseResult Parse(string? content, DateOnly? today = null)
    {
        var rows = new List<ConfirmedSlotImportRow>();
        var errors = new List<ConfirmedSlotImportError>();

        if (string.IsNullOrWhiteSpace(content))
        {
            errors.Add(new ConfirmedSlotImportError(0, "The file is empty."));
            return new ConfirmedSlotImportParseResult(rows, errors);
        }

        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        if (!string.Equals(lines[0].Trim(), RequiredHeader, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new ConfirmedSlotImportError(1, $"The header line must read exactly: {RequiredHeader}"));
            return new ConfirmedSlotImportParseResult(rows, errors);
        }

        var dataLineNumbers = new List<int>();
        for (var index = 1; index < lines.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(lines[index]))
            {
                dataLineNumbers.Add(index);
            }
        }

        if (dataLineNumbers.Count > MaxRows)
        {
            errors.Add(new ConfirmedSlotImportError(0, $"A file may contain at most 200 rows."));
            return new ConfirmedSlotImportParseResult(rows, errors);
        }

        var seenWindows = new Dictionary<(DateOnly Date, TimeOnly StartTime), int>();

        foreach (var index in dataLineNumbers)
        {
            var lineNumber = index + 1;
            var fields = lines[index].Split(',');

            if (fields.Length != 5)
            {
                errors.Add(new ConfirmedSlotImportError(
                    lineNumber, "Expected 5 comma-separated fields: date,startTime,DAT,MED,UNI."));
                continue;
            }

            if (!DateOnly.TryParseExact(
                    fields[0].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                errors.Add(new ConfirmedSlotImportError(lineNumber, $"{fields[0].Trim()} is not a valid date (expected yyyy-MM-dd)."));
                continue;
            }

            if (!TimeOnly.TryParseExact(
                    fields[1].Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTime))
            {
                errors.Add(new ConfirmedSlotImportError(lineNumber, $"{fields[1].Trim()} is not a valid startTime (expected HH:mm)."));
                continue;
            }

            if (!TryReadHeadcounts(fields, lineNumber, errors, out var headcounts))
            {
                continue;
            }

            var window = (date, startTime);

            SlotWindow slotWindow;
            try
            {
                slotWindow = new SlotWindow(date, startTime);
            }
            catch (DomainException ex)
            {
                errors.Add(new ConfirmedSlotImportError(lineNumber, ex.Message));
                continue;
            }

            // Imported slots follow the same future-date rule as slot proposals.
            if (today.HasValue && !slotWindow.StartsAfter(today.Value))
            {
                errors.Add(new ConfirmedSlotImportError(
                    lineNumber, "The slot date must be in the future."));
                continue;
            }

            if (seenWindows.TryGetValue(window, out var firstLine))
            {
                errors.Add(new ConfirmedSlotImportError(
                    lineNumber, $"Duplicate slot window — already used on line {firstLine}."));
                continue;
            }

            seenWindows.Add(window, lineNumber);
            rows.Add(new ConfirmedSlotImportRow(lineNumber, slotWindow, headcounts));
        }

        if (errors.Count == 0 && rows.Count == 0)
        {
            errors.Add(new ConfirmedSlotImportError(1, "The file contains no slot rows."));
        }

        return new ConfirmedSlotImportParseResult(rows, errors);
    }

    private static bool TryReadHeadcounts(
        string[] fields,
        int lineNumber,
        List<ConfirmedSlotImportError> errors,
        out IReadOnlyDictionary<Guid, int> headcounts)
    {
        var result = new Dictionary<Guid, int>();
        headcounts = result;

        for (var column = 0; column < TypeColumns.Length; column++)
        {
            var (code, id) = TypeColumns[column];
            var raw = fields[column + 2].Trim();

            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var headcount)
                || headcount <= 0)
            {
                errors.Add(new ConfirmedSlotImportError(
                    lineNumber, $"{code} headcount must be a positive whole number."));
                return false;
            }

            result[id] = headcount;
        }

        return true;
    }
}
`````

## after — src/EventBooking.Application/Events/EventImportParser.cs — 1/1

<!-- vocabulary-file: {"id":88,"oldPath":"src/EventBooking.Application/Slots/ConfirmedSlotImportParser.cs","newPath":"src/EventBooking.Application/Events/EventImportParser.cs","beforeSha":"57e01370a4a8ce0696b410dde59506705c4cee1053606b025e9fec4a7b301e88","afterSha":"16dabdd84801aa973eea0cf51817b9ce52338b0b71c9e6c52dc5b5c4cb0e3b53","side":"after","part":1,"parts":1} -->

`````csharp
using System.Globalization;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Events;

/// <summary>Defines event import row for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Window">The window.</param>
/// <param name="HeadcountsByAppointmentType">The headcounts by appointment type.</param>
public sealed record EventImportRow(
    int LineNumber, EventWindow Window, IReadOnlyDictionary<Guid, int> HeadcountsByAppointmentType);

/// <summary>Defines event import error for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Message">The message.</param>
public sealed record EventImportError(int LineNumber, string Message);

/// <summary>Defines event import parse result for the current use case.</summary>
/// <param name="Rows">The rows.</param>
/// <param name="Errors">The errors.</param>
public sealed record EventImportParseResult(
    IReadOnlyList<EventImportRow> Rows,
    IReadOnlyList<EventImportError> Errors);

/// <summary>
/// Structural validation only, mirroring AttendeeCsvParser (Task 29): header, field count,
/// parseable date/time, a positive integer per fixed AppointmentType column, duplicate windows
/// within the file, and a row-count ceiling. Nothing here reads the database.
/// </summary>
public static class EventImportParser
{
    /// <summary>Defines required header for the current use case.</summary>
    public const string RequiredHeader = "date,startTime,DAT,MED,UNI";
    /// <summary>Defines max rows for the current use case.</summary>
    public const int MaxRows = 200;

    private static readonly (string Code, Guid Id)[] TypeColumns =
    [
        ("DAT", AppointmentTypeIds.DrugAndAlcoholTesting),
        ("MED", AppointmentTypeIds.MedicalCheckUp),
        ("UNI", AppointmentTypeIds.UniformFitting),
    ];

    /// <summary>Defines parse for the current use case.</summary>
    /// <param name="content">The content.</param>
    /// <param name="today">The today.</param>
    public static EventImportParseResult Parse(string? content, DateOnly? today = null)
    {
        var rows = new List<EventImportRow>();
        var errors = new List<EventImportError>();

        if (string.IsNullOrWhiteSpace(content))
        {
            errors.Add(new EventImportError(0, "The file is empty."));
            return new EventImportParseResult(rows, errors);
        }

        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        if (!string.Equals(lines[0].Trim(), RequiredHeader, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new EventImportError(1, $"The header line must read exactly: {RequiredHeader}"));
            return new EventImportParseResult(rows, errors);
        }

        var dataLineNumbers = new List<int>();
        for (var index = 1; index < lines.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(lines[index]))
            {
                dataLineNumbers.Add(index);
            }
        }

        if (dataLineNumbers.Count > MaxRows)
        {
            errors.Add(new EventImportError(0, $"A file may contain at most 200 rows."));
            return new EventImportParseResult(rows, errors);
        }

        var seenWindows = new Dictionary<(DateOnly Date, TimeOnly StartTime), int>();

        foreach (var index in dataLineNumbers)
        {
            var lineNumber = index + 1;
            var fields = lines[index].Split(',');

            if (fields.Length != 5)
            {
                errors.Add(new EventImportError(
                    lineNumber, "Expected 5 comma-separated fields: date,startTime,DAT,MED,UNI."));
                continue;
            }

            if (!DateOnly.TryParseExact(
                    fields[0].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                errors.Add(new EventImportError(lineNumber, $"{fields[0].Trim()} is not a valid date (expected yyyy-MM-dd)."));
                continue;
            }

            if (!TimeOnly.TryParseExact(
                    fields[1].Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTime))
            {
                errors.Add(new EventImportError(lineNumber, $"{fields[1].Trim()} is not a valid startTime (expected HH:mm)."));
                continue;
            }

            if (!TryReadHeadcounts(fields, lineNumber, errors, out var headcounts))
            {
                continue;
            }

            var window = (date, startTime);

            EventWindow eventWindow;
            try
            {
                eventWindow = new EventWindow(date, startTime);
            }
            catch (DomainException ex)
            {
                errors.Add(new EventImportError(lineNumber, ex.Message));
                continue;
            }

            // Imported events follow the same future-date rule as event proposals.
            if (today.HasValue && !eventWindow.StartsAfter(today.Value))
            {
                errors.Add(new EventImportError(
                    lineNumber, "The event date must be in the future."));
                continue;
            }

            if (seenWindows.TryGetValue(window, out var firstLine))
            {
                errors.Add(new EventImportError(
                    lineNumber, $"Duplicate event window — already used on line {firstLine}."));
                continue;
            }

            seenWindows.Add(window, lineNumber);
            rows.Add(new EventImportRow(lineNumber, eventWindow, headcounts));
        }

        if (errors.Count == 0 && rows.Count == 0)
        {
            errors.Add(new EventImportError(1, "The file contains no event rows."));
        }

        return new EventImportParseResult(rows, errors);
    }

    private static bool TryReadHeadcounts(
        string[] fields,
        int lineNumber,
        List<EventImportError> errors,
        out IReadOnlyDictionary<Guid, int> headcounts)
    {
        var result = new Dictionary<Guid, int>();
        headcounts = result;

        for (var column = 0; column < TypeColumns.Length; column++)
        {
            var (code, id) = TypeColumns[column];
            var raw = fields[column + 2].Trim();

            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var headcount)
                || headcount <= 0)
            {
                errors.Add(new EventImportError(
                    lineNumber, $"{code} headcount must be a positive whole number."));
                return false;
            }

            result[id] = headcount;
        }

        return true;
    }
}
`````

## before — src/EventBooking.Application/Slots/GetManagerSlotBoardHandler.cs — 1/1

<!-- vocabulary-file: {"id":89,"oldPath":"src/EventBooking.Application/Slots/GetManagerSlotBoardHandler.cs","newPath":"src/EventBooking.Application/Events/GetManagerEventBoardHandler.cs","beforeSha":"eda6266bc12628b1cf21f1b0627e0f65ee118adc0b9b204083955bc290ddec21","afterSha":"27bda208456d9147df58f444c691c1ecfeb3ab24f1a6b4de84d12706ef22ecf3","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Slots;

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

/// <summary>Defines manager confirmed slot view for the current use case.</summary>
/// <param name="ConfirmedSlotId">The confirmed slot id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="MyHeadcount">The my headcount.</param>
/// <param name="MyRemainingCapacity">The my remaining capacity.</param>
public sealed record ManagerConfirmedSlotView(
    Guid ConfirmedSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int MyHeadcount,
    int MyRemainingCapacity);

/// <summary>Defines manager slot board for the current use case.</summary>
/// <param name="OpenProposals">The open proposals.</param>
/// <param name="ConfirmedSlots">The confirmed slots.</param>
public sealed record ManagerSlotBoard(
    IReadOnlyList<OpenProposalView> OpenProposals,
    IReadOnlyList<ManagerConfirmedSlotView> ConfirmedSlots);

/// <summary>Defines get manager slot board query for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
public sealed record GetManagerSlotBoardQuery(Guid ManagerUserId);

/// <summary>Defines get manager slot board handler for the current use case.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="confirmedSlots">The confirmed slots.</param>
/// <param name="access">The access.</param>
/// <param name="clock">The clock.</param>
public sealed class GetManagerSlotBoardHandler(
    ISlotProposalRepository proposals,
    IConfirmedSlotRepository confirmedSlots,
    IStaffAccessAuthorizer access,
    IClock clock)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<ManagerSlotBoard>> HandleAsync(
        GetManagerSlotBoardQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.ManagerUserId,
            StaffCapability.ManageSlotNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<ManagerSlotBoard>.Failure(authorized.Error);
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

        var confirmed = (await confirmedSlots.ListActiveAsync(clock.TodayAtHeadOffice, cancellationToken))
            .OrderBy(s => s.Window)
            .Select(s =>
            {
                var capacity = s.CapacityFor(myType);
                return new ManagerConfirmedSlotView(
                    s.Id,
                    s.Window.Date,
                    s.Window.StartTime,
                    s.Window.EndTime,
                    capacity.TotalHeadcount,
                    capacity.RemainingCapacity);
            })
            .ToList();

        return Result<ManagerSlotBoard>.Success(new ManagerSlotBoard(open, confirmed));
    }
}
`````
