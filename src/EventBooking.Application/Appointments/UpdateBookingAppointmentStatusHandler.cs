using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Appointments;

/// <summary>Requests one scoped appointment transition using the caller's expected version.</summary>
public sealed record UpdateBookingAppointmentStatusCommand
{
    /// <summary>Gets the authenticated staff identity requesting the transition.</summary>
    public required Guid StaffUserId { get; init; }
    /// <summary>Gets the stable booking-appointment identifier.</summary>
    public required Guid BookingAppointmentId { get; init; }
    /// <summary>Gets the requested independent operational status.</summary>
    public required BookingAppointmentStatus Status { get; init; }
    /// <summary>Gets the positive version last observed by the caller.</summary>
    public required long ExpectedVersion { get; init; }
}

/// <summary>Returns only the changed appointment row state needed by the workspace.</summary>
public sealed record BookingAppointmentUpdateView
{
    /// <summary>Gets the stable booking-appointment identifier.</summary>
    public required Guid BookingAppointmentId { get; init; }
    /// <summary>Gets this appointment's current independent operational status.</summary>
    public required BookingAppointmentStatus Status { get; init; }
    /// <summary>Gets when staff checked the attendee in, or null until check-in.</summary>
    public required DateTimeOffset? CheckedInAt { get; init; }
    /// <summary>Gets when staff recorded completion or no-show, or null before an outcome.</summary>
    public required DateTimeOffset? OutcomeAt { get; init; }
    /// <summary>Gets the current positive concurrency version.</summary>
    public required long Version { get; init; }
}

/// <summary>Authorizes, locks, validates, applies, and audits one appointment transition.</summary>
/// <param name="access">The access.</param>
/// <param name="appointments">The appointments.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="invites">The invites.</param>
/// <param name="events">The events.</param>
/// <param name="outcomes">The outcomes.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="clock">The clock.</param>
public sealed class UpdateBookingAppointmentStatusHandler(
    IStaffAccessAuthorizer access,
    IBookingAppointmentRepository appointments,
    IBookingRepository bookings,
    IAttendeeRepository attendees,
    IInviteRepository invites,
    IEventRepository events,
    RecoveryBookingOutcomeCoordinator outcomes,
    IAuditLogger audit,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private static readonly Error MissingAppointment =
        Error.NotFound("No such booking appointment.");

    /// <summary>Applies one scoped status transition with idempotency and version protection.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<BookingAppointmentUpdateView>> HandleAsync(
        UpdateBookingAppointmentStatusCommand command,
        CancellationToken cancellationToken)
    {
        var authorization = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ConductAppointments,
            requiredAppointmentTypeId: null,
            cancellationToken);
        if (authorization.IsFailure)
        {
            return Result<BookingAppointmentUpdateView>.Failure(authorization.Error);
        }

        if (authorization.Value.AppointmentTypeId is not Guid appointmentTypeId)
        {
            return Result<BookingAppointmentUpdateView>.Failure(
                Error.Forbidden("This staff profile cannot perform this operation."));
        }

        if (command.ExpectedVersion <= 0 || !Enum.IsDefined(command.Status))
        {
            return Result<BookingAppointmentUpdateView>.Failure(
                Error.Validation("A recognised status and positive expectedVersion are required."));
        }

        var locator = await appointments.FindLocatorInScopeAsync(
            command.BookingAppointmentId, appointmentTypeId, cancellationToken);
        if (locator is null)
        {
            return Result<BookingAppointmentUpdateView>.Failure(MissingAppointment);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // Attendee-first lifecycle order: Attendee, ordered pending Invites, original
        // Booking, addressed Booking, Confirmed Event, then ordered Booking Appointments.
        // The locator above only established this order; every relationship is re-read here.
        var attendee = await attendees.LockForUpdateAsync(locator.AttendeeId, cancellationToken);
        var pending = await invites.LockPendingListForAttendeeAsync(locator.AttendeeId, cancellationToken);
        var original = await bookings.LockForUpdateAsync(locator.OriginalBookingId, cancellationToken);
        var booking = await bookings.LockForUpdateAsync(locator.BookingId, cancellationToken);
        var eventItem = await events.LockForUpdateAsync(locator.EventId, cancellationToken);
        var lockedAppointments = await appointments.LockForBookingAsync(
            locator.BookingId, cancellationToken);
        var appointment = lockedAppointments.SingleOrDefault(value =>
            value.Id == command.BookingAppointmentId && value.AppointmentTypeId == appointmentTypeId);

        if (attendee is null || original is null || booking is null || appointment is null || eventItem is null)
        {
            return Result<BookingAppointmentUpdateView>.Failure(MissingAppointment);
        }

        if (booking.AttendeeId != attendee.Id
            || original.AttendeeId != attendee.Id
            || appointment.BookingId != booking.Id
            || booking.EventId != eventItem.Id
            || (booking.IsOriginal ? booking.Id != original.Id : booking.RecoveryOfBookingId != original.Id))
        {
            return Result<BookingAppointmentUpdateView>.Failure(
                Error.Conflict("The booking appointment no longer matches its active requirement."));
        }

        if (booking.Status == BookingStatus.Cancelled || eventItem.Status != EventStatus.Active)
        {
            return Result<BookingAppointmentUpdateView>.Failure(
                Error.Conflict("Cancelled bookings and events cannot be updated."));
        }

        if (appointment.Status == command.Status)
        {
            return Result<BookingAppointmentUpdateView>.Success(View(appointment));
        }

        if (appointment.Version != command.ExpectedVersion)
        {
            return Result<BookingAppointmentUpdateView>.Failure(Error.AppointmentVersionConflict(
                $"This appointment is now {appointment.Status} at version {appointment.Version}. Refresh and try again."));
        }

        var laterRecoveryExists = booking is { IsOriginal: false } || command.Status is
            BookingAppointmentStatus.Expected or BookingAppointmentStatus.CheckedIn
            ? await LaterRecoveryExistsAsync(
                original, booking, pending, appointmentTypeId, cancellationToken)
            : false;

        if (appointment.Status == BookingAppointmentStatus.NoShow
            && command.Status == BookingAppointmentStatus.Expected
            && laterRecoveryExists)
        {
            return Result<BookingAppointmentUpdateView>.Failure(Error.RecoveryActive(
                "A later recovery covers this appointment type. Cancel the recovery first, then correct the no-show."));
        }

        var localNow = clock.NowAtTransitionalLocation;
        var localDate = DateOnly.FromDateTime(localNow.DateTime);
        var localTime = TimeOnly.FromDateTime(localNow.DateTime);
        var checkInAllowed = eventItem.Window.Date == localDate;
        var noShowAllowed = eventItem.Window.Date < localDate
            || (eventItem.Window.Date == localDate && localTime >= eventItem.Window.EndTime);
        var previous = appointment.Status;

        try
        {
            appointment.TransitionTo(
                command.Status,
                command.StaffUserId,
                clock.UtcNow,
                checkInAllowed,
                noShowAllowed);
        }
        catch (DomainException exception)
        {
            return Result<BookingAppointmentUpdateView>.Failure(
                Error.Conflict(exception.Message));
        }

        var changed = false;
        if (!booking.IsOriginal)
        {
            try
            {
                changed = outcomes.Synchronize(booking, lockedAppointments, laterRecoveryExists);
            }
            catch (DomainException exception)
            {
                return Result<BookingAppointmentUpdateView>.Failure(
                    Error.Conflict(exception.Message));
            }
        }

        var reopened = changed && booking.Status == BookingStatus.Active;
        audit.Record(
            AuditEntityTypes.BookingAppointment,
            appointment.Id,
            ActionFor(previous, appointment.Status),
            ActorType.Staff,
            command.StaffUserId.ToString(),
            $"status:{previous}->{appointment.Status};appointmentTypeId:{appointmentTypeId};eventId:{eventItem.Id}"
                + (reopened ? $";booking:{BookingStatus.Concluded}->{BookingStatus.Active}" : null));

        if (changed && booking.Status == BookingStatus.Concluded)
        {
            audit.Record(
                AuditEntityTypes.Booking,
                booking.Id,
                AuditAction.RecoveryBookingConcluded,
                ActorType.Staff,
                command.StaffUserId.ToString(),
                $"root {booking.RecoveryOfBookingId} {eventItem.Window}");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<BookingAppointmentUpdateView>.Success(View(appointment));
    }

    /// <summary>
    /// Reports whether a later pending recovery Invite or non-cancelled recovery Booking
    /// covers the appointment type, making a correction stale or a reopen unsafe.
    /// </summary>
    private async Task<bool> LaterRecoveryExistsAsync(
        Booking original,
        Booking addressed,
        IReadOnlyList<Invite> pending,
        Guid appointmentTypeId,
        CancellationToken cancellationToken)
    {
        if (pending.Any(invite =>
                invite.RecoveryOfBookingId.HasValue
                && invite.RequiredAppointmentTypeIds.Contains(appointmentTypeId)))
        {
            return true;
        }

        var journey = await bookings.ListJourneyAsync(original.Id, cancellationToken);
        var rows = await appointments.ListForBookingsAsync(
            journey.Select(entry => entry.Id).ToList(),
            cancellationToken);
        var typesByBooking = rows
            .GroupBy(row => row.BookingId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => row.AppointmentTypeId).ToHashSet());

        return journey.Any(entry =>
            !entry.IsOriginal
            && entry.Id != addressed.Id
            && entry.Status != BookingStatus.Cancelled
            && entry.CreatedAt > addressed.CreatedAt
            && typesByBooking.TryGetValue(entry.Id, out var types)
            && types.Contains(appointmentTypeId));
    }

    private static AuditAction ActionFor(
        BookingAppointmentStatus previous,
        BookingAppointmentStatus current) => (previous, current) switch
    {
        (BookingAppointmentStatus.Expected, BookingAppointmentStatus.CheckedIn) =>
            AuditAction.AppointmentCheckedIn,
        (BookingAppointmentStatus.CheckedIn, BookingAppointmentStatus.Completed) =>
            AuditAction.AppointmentCompleted,
        (BookingAppointmentStatus.Expected, BookingAppointmentStatus.NoShow) =>
            AuditAction.AppointmentMarkedNoShow,
        _ => AuditAction.AppointmentStatusCorrected,
    };

    private static BookingAppointmentUpdateView View(BookingAppointment appointment) => new()
    {
        BookingAppointmentId = appointment.Id,
        Status = appointment.Status,
        CheckedInAt = appointment.CheckedInAt,
        OutcomeAt = appointment.OutcomeAt,
        Version = appointment.Version,
    };
}
