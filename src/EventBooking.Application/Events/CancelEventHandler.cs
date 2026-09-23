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
/// <param name="eligibility">The event eligibility query.</param>
/// <param name="settings">The system settings repository.</param>
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
    IInviteIssuer issuer,
    IEventEligibilityQuery eligibility,
    ISystemSettingsRepository settings)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<CancelEventOutcome>> HandleAsync(
        CancelEventCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId, StaffCapability.CancelEvent, null, ct);
        if (authorized.IsFailure) return Result<CancelEventOutcome>.Failure(authorized.Error);

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        if (!command.Confirm)
        {
            var preview = await events.LockForUpdateAsync(command.EventId, ct);
            if (preview is null)
                return Result<CancelEventOutcome>.Failure(Error.NotFound("No such event."));
            if (authorized.Value.AppointmentTypeId is { } previewScopedType
                && !preview.Capacities.Any(c => c.AppointmentTypeId == previewScopedType))
                return Result<CancelEventOutcome>.Failure(
                    Error.Forbidden("This event does not list the manager's appointment type."));

            var previewActive = await bookings.ListActiveForEventAsync(command.EventId, ct);
            await transaction.CommitAsync(ct);
            return Result<CancelEventOutcome>.Success(new CancelEventOutcome(previewActive.Count, 0, 0));
        }

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

        var lockedInvites = new List<(Attendee Attendee, Booking Snapshot, Domain.Invites.Invite? Invite)>(
            lockedAttendees.Count);
        foreach (var (attendee, snapshot) in lockedAttendees)
        {
            var invite = await invites.LockForUpdateAsync(snapshot.InviteId, ct);
            lockedInvites.Add((attendee, snapshot, invite));
        }

        var targets = new List<CancelTarget>(lockedInvites.Count);
        foreach (var (attendee, snapshot, invite) in lockedInvites)
        {
            var booking = await bookings.LockByIdForAttendeeAsync(snapshot.Id, attendee.Id, ct);
            if (booking is null || booking.Status != BookingStatus.Active) continue;
            targets.Add(new CancelTarget(
                attendee,
                booking,
                invite?.RequiredAppointmentTypeIds ?? attendee.RequiredAppointmentTypeIds,
                invite?.LocationIds));
        }

        var eventItem = await events.LockForUpdateAsync(command.EventId, ct);
        if (eventItem is null)
            return Result<CancelEventOutcome>.Failure(Error.NotFound("No such event."));

        if (authorized.Value.AppointmentTypeId is { } scopedType
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

        var configuration = await settings.GetAsync(ct);
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
            var fresh = await eligibility.FindEligibleEventsAsync(
                requiredAppointmentTypeIds: target.Required,
                locationIds: originalLocations,
                excludeEventIds: [eventItem.Id],
                count: configuration.InviteOptionCount,
                asOf: clock.UtcNow,
                cancellationToken: ct);
            if (fresh.Count >= configuration.InviteOptionCount)
            {
                var issued = await issuer.IssueRecoveryAsync(target.Attendee, target.Booking.Id, target.Required,
                    originalLocations, fresh.Take(configuration.InviteOptionCount).ToList(),
                    ActorType.Staff, command.StaffUserId.ToString(), ct);
                if (issued.IsSuccess)
                {
                    reinvited++;
                    emails.Add(EmailLog.RecordPending(Guid.NewGuid(), target.Attendee.Id,
                        EmailTemplate.EventCancelledRebookingNeeded, clock.UtcNow,
                        bookingId: target.Booking.Id, eventId: eventItem.Id));
                    audit.Record(AuditEntityTypes.Booking, target.Booking.Id, AuditAction.BookingCancelled,
                        ActorType.Staff, command.StaffUserId.ToString(), "replacement created");
                    continue;
                }
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
        Attendee Attendee, Booking Booking, IReadOnlyList<Guid> Required, IReadOnlyList<Guid>? LocationIds);
}
