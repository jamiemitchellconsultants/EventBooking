using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Bookings;

/// <summary>Confirms one offered event for the invite the book token names.</summary>
/// <param name="BookToken">The attendee's single-use confirmation link.</param>
/// <param name="EventId">The offered event being confirmed.</param>
public sealed record ConfirmBookingCommand(string BookToken, Guid EventId);

/// <summary>The created booking and its management link.</summary>
/// <param name="BookingId">The newly created active booking identifier.</param>
/// <param name="ManageToken">The raw management token returned once to the attendee.</param>
public sealed record ConfirmBookingOutcome(Guid BookingId, string ManageToken);

/// <summary>
/// Confirms one offered event on the anonymous pipeline. Locks in canonical order —
/// attendee, invite, event, then the required capacity rows — after a port read of the
/// attendee id, and charges exactly the required types all-or-nothing.
/// </summary>
/// <param name="invites">The invites.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="events">The events.</param>
/// <param name="capacities">The capacities.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="appointments">The appointments.</param>
/// <param name="locations">The locations.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="emails">The emails.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
/// <param name="zones">The zone abstraction the window's start instant is read in.</param>
public sealed class ConfirmBookingHandler(
    IInviteRepository invites,
    IAttendeeRepository attendees,
    IEventRepository events,
    IEventCapacityRepository capacities,
    IBookingRepository bookings,
    IBookingAppointmentRepository appointments,
    ILocationRepository locations,
    ITokenService tokens,
    IEmailDeliveryRepository emails,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock,
    IEventWindowZones zones)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<ConfirmBookingOutcome>> HandleAsync(
        ConfirmBookingCommand command, CancellationToken ct)
    {
        if (!tokens.TryRead(command.BookToken, out var reference)
            || reference.Purpose != TokenPurpose.Book
            || reference.Version < Invite.InitialTokenVersion)
            return Result<ConfirmBookingOutcome>.Failure(
                Error.Validation("This link cannot be used to confirm a booking."));

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        // Port read: the attendee id without a lock, so the locks below follow the
        // canonical order. Everything is re-validated after locking.
        var port = await invites.GetAsync(reference.EntityId, ct);
        if (port is null)
            return Result<ConfirmBookingOutcome>.Failure(Error.NotFound("No such invite."));

        var attendee = await attendees.LockForUpdateAsync(port.AttendeeId, ct);
        if (attendee is null)
            return Result<ConfirmBookingOutcome>.Failure(Error.NotFound("No such attendee."));

        var invite = await invites.LockForUpdateAsync(reference.EntityId, ct);
        if (invite is null)
            return Result<ConfirmBookingOutcome>.Failure(Error.NotFound("No such invite."));
        if (invite.AttendeeId != attendee.Id)
            return Result<ConfirmBookingOutcome>.Failure(
                Error.Conflict("This link does not belong to this attendee."));
        if (invite.TokenVersion != reference.Version)
            return Result<ConfirmBookingOutcome>.Failure(
                Error.Conflict("This link has been replaced."));
        if (invite.Status == InviteStatus.Used)
        {
            var existing = await bookings.GetByInviteIdAsync(invite.Id, ct);
            await transaction.RollbackAsync(ct);
            return Result<ConfirmBookingOutcome>.Failure(Error.AlreadyConfirmed(
                $"This invite already confirmed booking {existing?.Id}.", existing?.Id ?? Guid.Empty));
        }

        if (invite.Status != InviteStatus.Pending)
            return Result<ConfirmBookingOutcome>.Failure(
                Error.Conflict($"The invite is {invite.Status} and can no longer be used."));
        if (!invite.Offers(command.EventId))
            return Result<ConfirmBookingOutcome>.Failure(
                Error.Validation("The chosen event is not one of this invite's options."));

        var eventItem = await events.LockForUpdateAsync(command.EventId, ct);
        if (eventItem is null)
            return Result<ConfirmBookingOutcome>.Failure(Error.NotFound("No such event."));
        var location = await locations.GetAsync(eventItem.LocationId, ct);
        if (location is not null && eventItem.Window.HasStarted(zones, location.TimeZoneId, clock.UtcNow))
            return Result<ConfirmBookingOutcome>.Failure(
                Error.WindowStarted("This event has started and can no longer be booked."));

        var required = invite.RequiredAppointmentTypeIds;
        var locked = await capacities.LockForUpdateAsync(eventItem.Id, required, ct);
        if (locked.Count != required.Count)
            return Result<ConfirmBookingOutcome>.Failure(
                Error.Validation("The event no longer lists every required appointment type."));

        try
        {
            eventItem.ChargeRequiredTypes(required);
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(ct);
            return Result<ConfirmBookingOutcome>.Failure(Error.CapacityExhausted(ex.Message));
        }

        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, clock.UtcNow);
        bookings.Add(booking);
        foreach (var typeId in required)
            appointments.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
        invite.MarkUsed();
        if (attendee.Status != AttendeeStatus.Booked)
            attendee.MarkBooked(clock.UtcNow);

        audit.Record(AuditEntityTypes.Booking, booking.Id, AuditAction.BookingCreated,
            ActorType.AttendeeToken, invite.Id.ToString(), $"event {eventItem.Id}");
        foreach (var typeId in required)
            audit.Record(AuditEntityTypes.Event, eventItem.Id, AuditAction.CapacityDecremented,
                ActorType.AttendeeToken, invite.Id.ToString(), $"type {typeId}");
        emails.Add(EmailLog.RecordPending(Guid.NewGuid(), attendee.Id,
            EmailTemplate.BookingConfirmation, clock.UtcNow, bookingId: booking.Id));

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (UniqueConstraintViolationException)
        {
            // Two legacy pending invites for one attendee racing disjoint events: the attendee
            // lock serialized them but each passed every check, so the backstop decides and the
            // loser reports the same conflict a pre-check would have given.
            await transaction.RollbackAsync(ct);
            return Result<ConfirmBookingOutcome>.Failure(
                Error.Conflict("This attendee is already booked."));
        }

        return Result<ConfirmBookingOutcome>.Success(new ConfirmBookingOutcome(
            booking.Id, tokens.Issue(TokenPurpose.Manage, booking.Id, booking.ManageTokenVersion)));
    }
}
