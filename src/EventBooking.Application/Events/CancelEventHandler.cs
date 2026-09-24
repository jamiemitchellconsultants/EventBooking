using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Domain.Access;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Events;

/// <summary>Cancels an event in two steps: preview counts, then confirm the cascade.</summary>
/// <param name="StaffUserId">The staff identity performing the cancellation.</param>
/// <param name="EventId">The event to cancel.</param>
/// <param name="Confirm">Whether this call carries the confirmation.</param>
public sealed record CancelEventCommand(Guid StaffUserId, Guid EventId, bool Confirm);

/// <summary>How many bookings were cancelled, rebooked, or left awaiting availability.</summary>
/// <param name="CancelledCount">Bookings moved to cancelled (preview: bookings that would be).</param>
/// <param name="ReinvitedCount">Cancelled attendees issued a replacement invite.</param>
/// <param name="AwaitingAvailabilityCount">Cancelled attendees with no replacement.</param>
public sealed record CancelEventOutcome(
    int CancelledCount, int ReinvitedCount, int AwaitingAvailabilityCount);

/// <summary>
/// Cancels an event and rebooks its attendees from their original location sets, in attendee-id
/// order. Locks level by level — attendees, invites, bookings, event, capacities — so the
/// cascade never descends and never deadlocks against attendee-first work.
/// </summary>
/// <param name="events">The events.</param>
/// <param name="capacities">The capacities.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="invites">The invites.</param>
/// <param name="locations">The locations.</param>
/// <param name="emails">The emails.</param>
/// <param name="access">The staff access authorizer.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
/// <param name="zones">The zone abstraction the window's start instant is read in.</param>
/// <param name="issuer">The invite issuer.</param>
public sealed class CancelEventHandler(
    IEventRepository events,
    IEventCapacityRepository capacities,
    IBookingRepository bookings,
    IAttendeeRepository attendees,
    IInviteRepository invites,
    ILocationRepository locations,
    IEmailDeliveryRepository emails,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock,
    IEventWindowZones zones,
    IInviteIssuer issuer)
{
    // Each retry needs another booking to have committed in the snapshot-to-lock gap, so a
    // stale snapshot cannot repeat more often than the event has bookings racing it.
    private const int MaxAttempts = 10;

    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<CancelEventOutcome>> HandleAsync(
        CancelEventCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId, StaffCapability.CancelEvent, null, ct);
        if (authorized.IsFailure) return Result<CancelEventOutcome>.Failure(authorized.Error);

        if (!command.Confirm) return await PreviewAsync(command, authorized.Value, ct);

        // The snapshot below is taken before the event lock, because the ladder puts the event
        // below every attendee. A booking confirmed in that gap makes the snapshot stale; the
        // attempt then rolls back and starts over rather than surfacing a refusal the caller
        // can do nothing about. Each retry needs a booking to have committed in the gap, and
        // once the event lock is held no more can be, so the loop converges.
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var result = await CancelAsync(command, authorized.Value, ct);
            if (result is not null) return result;
        }

        return Result<CancelEventOutcome>.Failure(
            Error.Conflict("The event's bookings kept changing while cancelling. Please retry."));
    }

    private async Task<Result<CancelEventOutcome>> PreviewAsync(
        CancelEventCommand command, StaffAccessContext authorized, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        {
            var preview = await events.LockForUpdateAsync(command.EventId, ct);
            if (preview is null)
                return Result<CancelEventOutcome>.Failure(Error.NotFound("No such event."));
            if (authorized.AppointmentTypeId is { } previewScopedType
                && !preview.Capacities.Any(c => c.AppointmentTypeId == previewScopedType))
                return Result<CancelEventOutcome>.Failure(
                    Error.Forbidden("This event does not list the manager's appointment type."));

            if (preview.Status == EventStatus.Cancelled)
                return Result<CancelEventOutcome>.Failure(
                    Error.Conflict("This event has already been cancelled."));

            var previewActive = await bookings.ListActiveForEventAsync(command.EventId, ct);
            await transaction.CommitAsync(ct);
            return Result<CancelEventOutcome>.Success(new CancelEventOutcome(previewActive.Count, 0, 0));
        }
    }

    /// <summary>Runs one cancellation attempt; null means the snapshot went stale and it should be retried.</summary>
    private async Task<Result<CancelEventOutcome>?> CancelAsync(
        CancelEventCommand command, StaffAccessContext authorized, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);


        // Snapshot without locks, then each booking's locks in canonical order with
        // re-validation under lock: every attendee first, then every booking, then every
        // invite, then the event, then capacity rows. Taking the event first would descend
        // back to the attendee level and deadlock against attendee-first work.
        var active = await bookings.ListActiveForEventAsync(command.EventId, ct);
        var ordered = active.OrderBy(b => b.AttendeeId).ThenBy(b => b.Id).ToList();
        var lockedAttendees = new List<(Attendee Attendee, Booking Snapshot)>(ordered.Count);
        foreach (var snapshot in ordered)
        {
            var attendee = await attendees.LockForUpdateAsync(snapshot.AttendeeId, ct);
            if (attendee is null) continue;
            lockedAttendees.Add((attendee, snapshot));
        }

        var lockedInvites = new List<(Attendee Attendee, Booking Snapshot, Domain.Invites.Invite? Invite, Domain.Invites.Invite? Pending)>(
            lockedAttendees.Count);
        foreach (var (attendee, snapshot) in lockedAttendees)
        {
            var invite = await invites.LockForUpdateAsync(snapshot.InviteId, ct);
            var pending = await invites.LockPendingForAttendeeAsync(attendee.Id, ct);
            lockedInvites.Add((attendee, snapshot, invite, pending));
        }

        var targets = new List<CancelTarget>(lockedInvites.Count);
        foreach (var (attendee, snapshot, invite, pending) in lockedInvites)
        {
            var booking = await bookings.LockByIdForAttendeeAsync(snapshot.Id, attendee.Id, ct);
            if (booking is null || booking.Status != BookingStatus.Active) continue;
            targets.Add(new CancelTarget(
                attendee,
                booking,
                invite?.RequiredAppointmentTypeIds ?? attendee.RequiredAppointmentTypeIds,
                invite?.LocationIds,
                pending));
        }

        var eventItem = await events.LockForUpdateAsync(command.EventId, ct);
        if (eventItem is null)
            return Result<CancelEventOutcome>.Failure(Error.NotFound("No such event."));

        if (eventItem.Status == EventStatus.Cancelled)
            return Result<CancelEventOutcome>.Failure(
                Error.Conflict("This event has already been cancelled."));

        // Bookings are confirmed under the event lock, so once it is held the active set can
        // no longer grow: any booking that was not in the pre-lock snapshot means the
        // snapshot is stale and cancelling now would strand it.
        var known = ordered.Select(b => b.Id).ToHashSet();
        var current = await bookings.ListActiveForEventAsync(command.EventId, ct);
        if (current.Any(b => !known.Contains(b.Id)))
        {
            await transaction.RollbackAsync(ct);
            return null;
        }

        if (authorized.AppointmentTypeId is { } scopedType
            && !eventItem.Capacities.Any(c => c.AppointmentTypeId == scopedType))
            return Result<CancelEventOutcome>.Failure(
                Error.Forbidden("This event does not list the manager's appointment type."));

        var location = await locations.GetAsync(eventItem.LocationId, ct);
        if (location is null)
            return Result<CancelEventOutcome>.Failure(Error.NotFound("No such location."));
        try
        {
            eventItem.Cancel(zones, location.TimeZoneId, clock.UtcNow);
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(ct);
            return Result<CancelEventOutcome>.Failure(Error.WindowStarted(ex.Message));
        }

        audit.Record(AuditEntityTypes.Event, eventItem.Id, AuditAction.EventCancelled,
            ActorType.Staff, command.StaffUserId.ToString(), $"{active.Count} bookings");

        var cancelled = 0;
        var reinvited = 0;
        var awaiting = 0;

        foreach (var target in targets)
        {
            await capacities.LockForUpdateAsync(eventItem.Id, target.Required, ct);

            target.Booking.Cancel();
            eventItem.ReleaseTypes(target.Required);
            target.Attendee.ResetToNotYetInvited(clock.UtcNow);
            cancelled++;

            var originalLocations = target.LocationIds ?? [eventItem.LocationId];

            // A fresh initial invite: a recovery invite cannot be rooted at the booking that
            // was just cancelled. The cancelled event is excluded because its cancellation is
            // still unsaved and the eligibility query cannot see it.
            var issued = await issuer.IssueInitialAsync(target.Attendee, originalLocations,
                EmailTemplate.EventCancelledRebookingNeeded, ActorType.Staff,
                command.StaffUserId.ToString(), ct, [eventItem.Id],
                target.Pending is null ? [] : [target.Pending]);
            if (issued.IsSuccess)
            {
                reinvited++;
                audit.Record(AuditEntityTypes.Booking, target.Booking.Id, AuditAction.BookingCancelled,
                    ActorType.Staff, command.StaffUserId.ToString(), "replacement created");
                continue;
            }

            awaiting++;
            target.Attendee.MarkAwaitingAvailability(clock.UtcNow);
            emails.Add(EmailLog.RecordPending(Guid.NewGuid(), target.Attendee.Id,
                EmailTemplate.EventCancelledRebookingNeeded, clock.UtcNow,
                bookingId: target.Booking.Id, eventId: eventItem.Id));
            audit.Record(AuditEntityTypes.Booking, target.Booking.Id, AuditAction.BookingCancelled,
                ActorType.Staff, command.StaffUserId.ToString(), "no replacement");
        }

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result<CancelEventOutcome>.Success(new CancelEventOutcome(cancelled, reinvited, awaiting));
    }

    private sealed record CancelTarget(
        Attendee Attendee, Booking Booking, IReadOnlyList<Guid> Required, IReadOnlyList<Guid>? LocationIds,
        Domain.Invites.Invite? Pending);
}
