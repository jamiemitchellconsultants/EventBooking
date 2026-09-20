# 00b — Vocabulary edits 9 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — src/EventBooking.Application/Appointments/UpdateBookingAppointmentStatusHandler.cs — 1/1

<!-- vocabulary-file: {"id":49,"oldPath":"src/EventBooking.Application/Appointments/UpdateBookingAppointmentStatusHandler.cs","newPath":"src/EventBooking.Application/Appointments/UpdateBookingAppointmentStatusHandler.cs","beforeSha":"2dabc48919b5f8ca9b834e29818ebbd7f343f7282dd0ea9e4e132366f8f7700f","afterSha":"fcc3f522a5eedf80ef1f31bf00938b11316db90cd263b73d6f47f6a7e7254dea","side":"after","part":1,"parts":1} -->

`````csharp
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
            return Result<BookingAppointmentUpdateView>.Failure(Error.Conflict(
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
`````

## before — src/EventBooking.Application/Bookings/BookingCanceller.cs — 1/1

<!-- vocabulary-file: {"id":50,"oldPath":"src/EventBooking.Application/Bookings/BookingCanceller.cs","newPath":"src/EventBooking.Application/Bookings/BookingCanceller.cs","beforeSha":"5395fd970f149470bc7b00257fa24e3e7173abc56909f456e26618f2690c11f7","afterSha":"3a047eb886c4d90accf2c7b3492809be5320f8df2e3d2e6724a58b6409ae7d05","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Bookings;

/// <summary>
/// Voids one booking and returns the capacity it held, under a row lock on the capacity rows.
/// Shared by candidate deletion (Task 32), candidate cancellation (Task 41) and slot cancellation
/// (Task 42) — all three release capacity in exactly the same way, and a second implementation
/// would be a second chance to get the locking wrong.
/// </summary>
/// <param name="appointments">The appointments.</param>
/// <param name="capacities">The capacities.</param>
/// <param name="audit">The audit.</param>
public sealed class BookingCanceller(
    IBookingAppointmentRepository appointments,
    ISlotCapacityRepository capacities,
    IAuditLogger audit)
{
    /// <summary>Cancels one locked Booking and returns capacity for its own Appointment snapshot.</summary>
    /// <param name="booking">The booking.</param>
    /// <param name="slot">The slot.</param>
    /// <param name="actorType">The actor type.</param>
    /// <param name="actorId">The actor id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<Guid>>> CancelLockedAsync(
        Booking booking,
        ConfirmedSlot slot,
        ActorType actorType,
        string? actorId,
        CancellationToken cancellationToken)
    {
        var snapshot = (await appointments.ListForBookingAsync(booking.Id, cancellationToken))
            .Select(appointment => appointment.AppointmentTypeId)
            .Distinct()
            .Order()
            .ToList();

        if (snapshot.Count == 0)
        {
            return Result<IReadOnlyList<Guid>>.Failure(
                Error.Conflict("The booking has no appointments to release."));
        }

        IReadOnlyList<SlotCapacity> locked;
        try
        {
            foreach (var appointmentTypeId in snapshot)
            {
                slot.CapacityFor(appointmentTypeId);
            }

            locked = (await capacities.LockForUpdateAsync(slot.Id, snapshot, cancellationToken))
                .OrderBy(capacity => capacity.AppointmentTypeId)
                .ToList();
        }
        catch (DomainException ex)
        {
            return Result<IReadOnlyList<Guid>>.Failure(Error.Conflict(ex.Message));
        }

        if (locked.Count != snapshot.Count)
        {
            return Result<IReadOnlyList<Guid>>.Failure(
                Error.Conflict("The slot is missing capacity counters for the booking."));
        }

        booking.Cancel();

        foreach (var capacity in locked)
        {
            capacity.Increment();

            audit.Record(
                AuditEntityTypes.ConfirmedSlot,
                slot.Id,
                AuditAction.CapacityIncremented,
                actorType,
                actorId,
                $"{capacity.AppointmentTypeId} now {capacity.RemainingCapacity}");
        }

        audit.Record(
            AuditEntityTypes.Booking,
            booking.Id,
            AuditAction.BookingCancelled,
            actorType,
            actorId,
            null);

        return Result<IReadOnlyList<Guid>>.Success(snapshot);
    }
}
`````

## after — src/EventBooking.Application/Bookings/BookingCanceller.cs — 1/1

<!-- vocabulary-file: {"id":50,"oldPath":"src/EventBooking.Application/Bookings/BookingCanceller.cs","newPath":"src/EventBooking.Application/Bookings/BookingCanceller.cs","beforeSha":"5395fd970f149470bc7b00257fa24e3e7173abc56909f456e26618f2690c11f7","afterSha":"3a047eb886c4d90accf2c7b3492809be5320f8df2e3d2e6724a58b6409ae7d05","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Bookings;

/// <summary>
/// Voids one booking and returns the capacity it held, under a row lock on the capacity rows.
/// Shared by attendee deletion (Task 32), attendee cancellation (Task 41) and event cancellation
/// (Task 42) — all three release capacity in exactly the same way, and a second implementation
/// would be a second chance to get the locking wrong.
/// </summary>
/// <param name="appointments">The appointments.</param>
/// <param name="capacities">The capacities.</param>
/// <param name="audit">The audit.</param>
public sealed class BookingCanceller(
    IBookingAppointmentRepository appointments,
    IEventCapacityRepository capacities,
    IAuditLogger audit)
{
    /// <summary>Cancels one locked Booking and returns capacity for its own Appointment snapshot.</summary>
    /// <param name="booking">The booking.</param>
    /// <param name="eventItem">The eventItem.</param>
    /// <param name="actorType">The actor type.</param>
    /// <param name="actorId">The actor id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<Guid>>> CancelLockedAsync(
        Booking booking,
        Event eventItem,
        ActorType actorType,
        string? actorId,
        CancellationToken cancellationToken)
    {
        var snapshot = (await appointments.ListForBookingAsync(booking.Id, cancellationToken))
            .Select(appointment => appointment.AppointmentTypeId)
            .Distinct()
            .Order()
            .ToList();

        if (snapshot.Count == 0)
        {
            return Result<IReadOnlyList<Guid>>.Failure(
                Error.Conflict("The booking has no appointments to release."));
        }

        IReadOnlyList<EventCapacity> locked;
        try
        {
            foreach (var appointmentTypeId in snapshot)
            {
                eventItem.CapacityFor(appointmentTypeId);
            }

            locked = (await capacities.LockForUpdateAsync(eventItem.Id, snapshot, cancellationToken))
                .OrderBy(capacity => capacity.AppointmentTypeId)
                .ToList();
        }
        catch (DomainException ex)
        {
            return Result<IReadOnlyList<Guid>>.Failure(Error.Conflict(ex.Message));
        }

        if (locked.Count != snapshot.Count)
        {
            return Result<IReadOnlyList<Guid>>.Failure(
                Error.Conflict("The eventItem is missing capacity counters for the booking."));
        }

        booking.Cancel();

        foreach (var capacity in locked)
        {
            capacity.Increment();

            audit.Record(
                AuditEntityTypes.Event,
                eventItem.Id,
                AuditAction.CapacityIncremented,
                actorType,
                actorId,
                $"{capacity.AppointmentTypeId} now {capacity.RemainingCapacity}");
        }

        audit.Record(
            AuditEntityTypes.Booking,
            booking.Id,
            AuditAction.BookingCancelled,
            actorType,
            actorId,
            null);

        return Result<IReadOnlyList<Guid>>.Success(snapshot);
    }
}
`````

## before — src/EventBooking.Application/Bookings/CancelBookingHandler.cs — 1/1

<!-- vocabulary-file: {"id":51,"oldPath":"src/EventBooking.Application/Bookings/CancelBookingHandler.cs","newPath":"src/EventBooking.Application/Bookings/CancelBookingHandler.cs","beforeSha":"03e21506cf30c0bccfabd4acd19e1973edad1bd141c7d1cc754188dc0f3774f8","afterSha":"ee1db20bb72d8f194ee311d3e3d68995460f9004cc9e40b6bbd90eb01b9545aa","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Bookings;

/// <summary>Defines cancel booking command for the current use case.</summary>
/// <param name="ManageToken">The manage token.</param>
/// <param name="Rebook">The rebook.</param>
public sealed record CancelBookingCommand(string? ManageToken, bool Rebook);

/// <summary>Reports cancellation and any replacement-invite delivery outcome.</summary>
/// <param name="Reinvited">Whether a replacement invite was created.</param>
/// <param name="InviteCreated">The explicit replacement-invite creation state.</param>
/// <param name="DeliveryStatus">The provider outcome, or <c>Unavailable</c> when no invite exists.</param>
/// <param name="DeliveryId">The durable replacement delivery identifier, when one was staged.</param>
public sealed record CancelBookingOutcome(
    bool Reinvited,
    bool InviteCreated = false,
    string? DeliveryStatus = null,
    Guid? DeliveryId = null);

/// <summary>Cancels or rebooks under the candidate lifecycle lock before releasing slot capacity.</summary>
/// <param name="deliveries">Stages and dispatches replacement invites after commit.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="slots">The slots.</param>
/// <param name="candidates">The candidates.</param>
/// <param name="invites">The invites.</param>
/// <param name="bookingCanceller">The booking canceller.</param>
/// <param name="issuer">The issuer.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="clock">The clock.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class CancelBookingHandler(
    IBookingRepository bookings,
    IConfirmedSlotRepository slots,
    ICandidateRepository candidates,
    IInviteRepository invites,
    BookingCanceller bookingCanceller,
    InviteIssuer issuer,
    EmailDeliveryService deliveries,
    ITokenService tokens,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    /// <summary>Cancels the managed booking and optionally issues a replacement invite atomically.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<CancelBookingOutcome>> HandleAsync(
        CancelBookingCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ManageToken is null || !tokens.TryRead(command.ManageToken, out _))
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var confirmedSlotId = await bookings.GetConfirmedSlotIdByManageTokenHashAsync(
            tokens.Hash(command.ManageToken),
            cancellationToken);
        var candidateId = await bookings.GetCandidateIdByManageTokenHashAsync(
            tokens.Hash(command.ManageToken),
            cancellationToken);
        if (confirmedSlotId is null || candidateId is null)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var candidate = await candidates.LockForUpdateAsync(candidateId.Value, cancellationToken);
        if (candidate is null)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var pending = await invites.LockPendingListForCandidateAsync(candidate.Id, cancellationToken);

        var booking = await bookings.LockByManageTokenHashForUpdateAsync(
            tokens.Hash(command.ManageToken), cancellationToken);

        if (booking is null || booking.Status != BookingStatus.Active)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        if (booking.CandidateId != candidate.Id)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        Booking? activeRecovery = null;
        if (booking.IsOriginal)
        {
            activeRecovery = await bookings.LockActiveRecoveryAsync(booking.Id, cancellationToken);
        }

        var slotIds = new List<Guid> { confirmedSlotId.Value };
        if (activeRecovery is not null && activeRecovery.ConfirmedSlotId != confirmedSlotId.Value)
        {
            slotIds.Add(activeRecovery.ConfirmedSlotId);
        }

        slotIds.Sort();
        var lockedSlots = new Dictionary<Guid, ConfirmedSlot>();
        foreach (var slotId in slotIds)
        {
            var locked = await slots.LockForUpdateAsync(slotId, cancellationToken);
            if (locked is null)
            {
                return Result<CancelBookingOutcome>.Failure(
                    Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
            }

            lockedSlots[slotId] = locked;
        }

        var slot = lockedSlots[confirmedSlotId.Value];

        // A booking can no longer be cancelled once its slot date has started.
        if (lockedSlots.Values.Any(locked => locked.Window.Date < clock.TodayAtHeadOffice))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelBookingOutcome>.Failure(Error.Conflict(
                "This appointment has already taken place and can no longer be cancelled."));
        }

        var reinvited = false;
        InviteIssueResult? issued = null;

        try
        {
            if (!booking.IsOriginal)
            {
                var releasedRecovery = await bookingCanceller.CancelLockedAsync(
                    booking,
                    slot,
                    ActorType.CandidateToken,
                    booking.Id.ToString(),
                    cancellationToken);
                if (releasedRecovery.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<CancelBookingOutcome>.Failure(releasedRecovery.Error);
                }
            }
            else
            {
                foreach (var pendingRecovery in pending.Where(invite => invite.RecoveryOfBookingId.HasValue && invite.Status == Domain.Invites.InviteStatus.Pending))
                {
                    pendingRecovery.MarkSuperseded();
                }

                if (activeRecovery is not null)
                {
                    var releasedActiveRecovery = await bookingCanceller.CancelLockedAsync(
                        activeRecovery,
                        lockedSlots[activeRecovery.ConfirmedSlotId],
                        ActorType.CandidateToken,
                        booking.Id.ToString(),
                        cancellationToken);
                    if (releasedActiveRecovery.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelBookingOutcome>.Failure(releasedActiveRecovery.Error);
                    }
                }

                var released = await bookingCanceller.CancelLockedAsync(
                    booking,
                    slot,
                    ActorType.CandidateToken,
                    booking.Id.ToString(),
                    cancellationToken);
                if (released.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<CancelBookingOutcome>.Failure(released.Error);
                }

                candidate.ResetToNotYetInvited();

                if (command.Rebook)
                {
                    var issueResult = await issuer.IssueInitialAsync(
                        candidate,
                        0,
                        ActorType.CandidateToken,
                        booking.Id.ToString(),
                        isReinvite: false,
                        cancellationToken);
                    if (issueResult.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelBookingOutcome>.Failure(issueResult.Error);
                    }

                    issued = issueResult.Value;
                    reinvited = issued.Invited;
                }
            }
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelBookingOutcome>.Failure(Error.Conflict(ex.Message));
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

        if (issued?.DispatchPlan is { } plan)
        {
            var status = await deliveries.DispatchClaimedAsync(
                plan.DeliveryId, plan.Message, cancellationToken, plan.OnSent);

            return Result<CancelBookingOutcome>.Success(
                new CancelBookingOutcome(
                    reinvited,
                    InviteCreated: reinvited,
                    status.ToString(),
                    plan.DeliveryId));
        }

        return Result<CancelBookingOutcome>.Success(
            new CancelBookingOutcome(reinvited, InviteCreated: reinvited, "Unavailable"));
    }
}
`````

## after — src/EventBooking.Application/Bookings/CancelBookingHandler.cs — 1/1

<!-- vocabulary-file: {"id":51,"oldPath":"src/EventBooking.Application/Bookings/CancelBookingHandler.cs","newPath":"src/EventBooking.Application/Bookings/CancelBookingHandler.cs","beforeSha":"03e21506cf30c0bccfabd4acd19e1973edad1bd141c7d1cc754188dc0f3774f8","afterSha":"ee1db20bb72d8f194ee311d3e3d68995460f9004cc9e40b6bbd90eb01b9545aa","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Bookings;

/// <summary>Defines cancel booking command for the current use case.</summary>
/// <param name="ManageToken">The manage token.</param>
/// <param name="Rebook">The rebook.</param>
public sealed record CancelBookingCommand(string? ManageToken, bool Rebook);

/// <summary>Reports cancellation and any replacement-invite delivery outcome.</summary>
/// <param name="Reinvited">Whether a replacement invite was created.</param>
/// <param name="InviteCreated">The explicit replacement-invite creation state.</param>
/// <param name="DeliveryStatus">The provider outcome, or <c>Unavailable</c> when no invite exists.</param>
/// <param name="DeliveryId">The durable replacement delivery identifier, when one was staged.</param>
public sealed record CancelBookingOutcome(
    bool Reinvited,
    bool InviteCreated = false,
    string? DeliveryStatus = null,
    Guid? DeliveryId = null);

/// <summary>Cancels or rebooks under the attendee lifecycle lock before releasing event capacity.</summary>
/// <param name="deliveries">Stages and dispatches replacement invites after commit.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="events">The events.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="invites">The invites.</param>
/// <param name="bookingCanceller">The booking canceller.</param>
/// <param name="issuer">The issuer.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="clock">The clock.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class CancelBookingHandler(
    IBookingRepository bookings,
    IEventRepository events,
    IAttendeeRepository attendees,
    IInviteRepository invites,
    BookingCanceller bookingCanceller,
    InviteIssuer issuer,
    EmailDeliveryService deliveries,
    ITokenService tokens,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    /// <summary>Cancels the managed booking and optionally issues a replacement invite atomically.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<CancelBookingOutcome>> HandleAsync(
        CancelBookingCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ManageToken is null || !tokens.TryRead(command.ManageToken, out _))
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var eventId = await bookings.GetEventIdByManageTokenHashAsync(
            tokens.Hash(command.ManageToken),
            cancellationToken);
        var attendeeId = await bookings.GetAttendeeIdByManageTokenHashAsync(
            tokens.Hash(command.ManageToken),
            cancellationToken);
        if (eventId is null || attendeeId is null)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var attendee = await attendees.LockForUpdateAsync(attendeeId.Value, cancellationToken);
        if (attendee is null)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var pending = await invites.LockPendingListForAttendeeAsync(attendee.Id, cancellationToken);

        var booking = await bookings.LockByManageTokenHashForUpdateAsync(
            tokens.Hash(command.ManageToken), cancellationToken);

        if (booking is null || booking.Status != BookingStatus.Active)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        if (booking.AttendeeId != attendee.Id)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        Booking? activeRecovery = null;
        if (booking.IsOriginal)
        {
            activeRecovery = await bookings.LockActiveRecoveryAsync(booking.Id, cancellationToken);
        }

        var eventIds = new List<Guid> { eventId.Value };
        if (activeRecovery is not null && activeRecovery.EventId != eventId.Value)
        {
            eventIds.Add(activeRecovery.EventId);
        }

        eventIds.Sort();
        var lockedEvents = new Dictionary<Guid, Event>();
        foreach (var lockedEventId in eventIds)
        {
            var locked = await events.LockForUpdateAsync(lockedEventId, cancellationToken);
            if (locked is null)
            {
                return Result<CancelBookingOutcome>.Failure(
                    Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
            }

            lockedEvents[lockedEventId] = locked;
        }

        var eventItem = lockedEvents[eventId.Value];

        // A booking can no longer be cancelled once its event date has started.
        if (lockedEvents.Values.Any(locked => locked.Window.Date < clock.TodayAtTransitionalLocation))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelBookingOutcome>.Failure(Error.Conflict(
                "This appointment has already taken place and can no longer be cancelled."));
        }

        var reinvited = false;
        InviteIssueResult? issued = null;

        try
        {
            if (!booking.IsOriginal)
            {
                var releasedRecovery = await bookingCanceller.CancelLockedAsync(
                    booking,
                    eventItem,
                    ActorType.AttendeeToken,
                    booking.Id.ToString(),
                    cancellationToken);
                if (releasedRecovery.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<CancelBookingOutcome>.Failure(releasedRecovery.Error);
                }
            }
            else
            {
                foreach (var pendingRecovery in pending.Where(invite => invite.RecoveryOfBookingId.HasValue && invite.Status == Domain.Invites.InviteStatus.Pending))
                {
                    pendingRecovery.MarkSuperseded();
                }

                if (activeRecovery is not null)
                {
                    var releasedActiveRecovery = await bookingCanceller.CancelLockedAsync(
                        activeRecovery,
                        lockedEvents[activeRecovery.EventId],
                        ActorType.AttendeeToken,
                        booking.Id.ToString(),
                        cancellationToken);
                    if (releasedActiveRecovery.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelBookingOutcome>.Failure(releasedActiveRecovery.Error);
                    }
                }

                var released = await bookingCanceller.CancelLockedAsync(
                    booking,
                    eventItem,
                    ActorType.AttendeeToken,
                    booking.Id.ToString(),
                    cancellationToken);
                if (released.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<CancelBookingOutcome>.Failure(released.Error);
                }

                attendee.ResetToNotYetInvited();

                if (command.Rebook)
                {
                    var issueResult = await issuer.IssueInitialAsync(
                        attendee,
                        0,
                        ActorType.AttendeeToken,
                        booking.Id.ToString(),
                        isReinvite: false,
                        cancellationToken);
                    if (issueResult.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelBookingOutcome>.Failure(issueResult.Error);
                    }

                    issued = issueResult.Value;
                    reinvited = issued.Invited;
                }
            }
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelBookingOutcome>.Failure(Error.Conflict(ex.Message));
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

        if (issued?.DispatchPlan is { } plan)
        {
            var status = await deliveries.DispatchClaimedAsync(
                plan.DeliveryId, plan.Message, cancellationToken, plan.OnSent);

            return Result<CancelBookingOutcome>.Success(
                new CancelBookingOutcome(
                    reinvited,
                    InviteCreated: reinvited,
                    status.ToString(),
                    plan.DeliveryId));
        }

        return Result<CancelBookingOutcome>.Success(
            new CancelBookingOutcome(reinvited, InviteCreated: reinvited, "Unavailable"));
    }
}
`````

## before — src/EventBooking.Application/Bookings/CancelCandidateBookingHandler.cs — 1/1

<!-- vocabulary-file: {"id":52,"oldPath":"src/EventBooking.Application/Bookings/CancelCandidateBookingHandler.cs","newPath":"src/EventBooking.Application/Bookings/CancelAttendeeBookingHandler.cs","beforeSha":"e498dd164fd2e1e3152da9c5631412636fcc307381e27a363c418c5792b91781","afterSha":"3f12a48e14049a0bcd029180acd43fc71e25fee1a91f1d77c3d5efba889072b6","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Bookings;

/// <summary>Requests staff cancellation of one candidate's booking.</summary>
/// <param name="StaffUserId">The coordinator performing the cancellation; recorded as the audit actor.</param>
/// <param name="CandidateId">The candidate the booking must belong to.</param>
/// <param name="BookingId">The booking to cancel.</param>
/// <param name="Rebook">
/// Whether to issue a replacement invite. Applies only to an original booking; rebooking a
/// cancelled recovery booking is the recovery path's job and is refused here.
/// </param>
public sealed record CancelCandidateBookingCommand(
    Guid StaffUserId,
    Guid CandidateId,
    Guid BookingId,
    bool Rebook);

/// <summary>
/// Cancels one candidate booking on a coordinator's behalf, reusing the candidate self-service
/// cancellation path so capacity release, recovery cascade, and audit semantics stay identical.
/// </summary>
/// <param name="access">Authorizes candidate management before anything is read or locked.</param>
/// <param name="bookings">Locks and re-reads the targeted booking and any active recovery.</param>
/// <param name="slots">Locks every confirmed slot whose capacity is released.</param>
/// <param name="candidates">Locks the candidate lifecycle root.</param>
/// <param name="invites">Locks pending invites so recovery invites can be superseded.</param>
/// <param name="bookingCanceller">Performs the cancellation, capacity release, and audit write.</param>
/// <param name="issuer">Issues the optional replacement invite.</param>
/// <param name="deliveries">Dispatches a staged replacement invite after commit.</param>
/// <param name="unitOfWork">Owns the transaction enclosing the lifecycle transitions.</param>
/// <param name="clock">The clock.</param>
public sealed class CancelCandidateBookingHandler(
    IStaffAccessAuthorizer access,
    IBookingRepository bookings,
    IConfirmedSlotRepository slots,
    ICandidateRepository candidates,
    IInviteRepository invites,
    BookingCanceller bookingCanceller,
    InviteIssuer issuer,
    EmailDeliveryService deliveries,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    private const string NoSuchBooking = "No such active booking for this candidate.";

    /// <summary>
    /// Cancels the targeted booking under the candidate lifecycle lock order, cascading onto an
    /// active recovery when the target is the original, and optionally re-inviting the candidate.
    /// </summary>
    /// <param name="command">The coordinator's cancellation request.</param>
    /// <param name="cancellationToken">Cancels the authorization, locks, and dispatch.</param>
    /// <returns>
    /// The cancellation outcome, a forbidden failure when the caller lacks candidate management,
    /// a not-found failure for an unknown or already-inactive booking, or a conflict when a
    /// replacement invite is requested for a recovery booking.
    /// </returns>
    public async Task<Result<CancelBookingOutcome>> HandleAsync(
        CancelCandidateBookingCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageCandidates,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<CancelBookingOutcome>.Failure(authorized.Error);
        }

        var actorId = command.StaffUserId.ToString();

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var candidate = await candidates.LockForUpdateAsync(command.CandidateId, cancellationToken);
        if (candidate is null)
        {
            return Result<CancelBookingOutcome>.Failure(Error.NotFound(NoSuchBooking));
        }

        var pending = await invites.LockPendingListForCandidateAsync(candidate.Id, cancellationToken);

        var booking = await bookings.LockByIdForCandidateAsync(
            command.BookingId, candidate.Id, cancellationToken);
        if (booking is null || booking.Status != BookingStatus.Active)
        {
            return Result<CancelBookingOutcome>.Failure(Error.NotFound(NoSuchBooking));
        }

        if (!booking.IsOriginal && command.Rebook)
        {
            return Result<CancelBookingOutcome>.Failure(Error.Conflict(
                "A recovery booking cannot be cancelled and rebooked. "
                + "Cancel it, then arrange the missed appointments again."));
        }

        Booking? activeRecovery = null;
        if (booking.IsOriginal)
        {
            activeRecovery = await bookings.LockActiveRecoveryAsync(booking.Id, cancellationToken);
        }

        var slotIds = new List<Guid> { booking.ConfirmedSlotId };
        if (activeRecovery is not null && activeRecovery.ConfirmedSlotId != booking.ConfirmedSlotId)
        {
            slotIds.Add(activeRecovery.ConfirmedSlotId);
        }

        slotIds.Sort();
        var lockedSlots = new Dictionary<Guid, ConfirmedSlot>();
        foreach (var slotId in slotIds)
        {
            var locked = await slots.LockForUpdateAsync(slotId, cancellationToken);
            if (locked is null)
            {
                return Result<CancelBookingOutcome>.Failure(Error.NotFound(NoSuchBooking));
            }

            lockedSlots[slotId] = locked;
        }

        var slot = lockedSlots[booking.ConfirmedSlotId];

        // A booking can no longer be cancelled once its slot date has started.
        if (lockedSlots.Values.Any(locked => locked.Window.Date < clock.TodayAtHeadOffice))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelBookingOutcome>.Failure(Error.Conflict(
                "This appointment has already taken place and can no longer be cancelled."));
        }

        var reinvited = false;
        InviteIssueResult? issued = null;

        try
        {
            if (!booking.IsOriginal)
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
                    return Result<CancelBookingOutcome>.Failure(releasedRecovery.Error);
                }
            }
            else
            {
                foreach (var pendingRecovery in pending.Where(invite => invite.RecoveryOfBookingId.HasValue && invite.Status == Domain.Invites.InviteStatus.Pending))
                {
                    pendingRecovery.MarkSuperseded();
                }

                if (activeRecovery is not null)
                {
                    var releasedActiveRecovery = await bookingCanceller.CancelLockedAsync(
                        activeRecovery,
                        lockedSlots[activeRecovery.ConfirmedSlotId],
                        ActorType.Staff,
                        actorId,
                        cancellationToken);
                    if (releasedActiveRecovery.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelBookingOutcome>.Failure(releasedActiveRecovery.Error);
                    }
                }

                var released = await bookingCanceller.CancelLockedAsync(
                    booking,
                    slot,
                    ActorType.Staff,
                    actorId,
                    cancellationToken);
                if (released.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<CancelBookingOutcome>.Failure(released.Error);
                }

                candidate.ResetToNotYetInvited();

                if (command.Rebook)
                {
                    var issueResult = await issuer.IssueInitialAsync(
                        candidate,
                        0,
                        ActorType.Staff,
                        actorId,
                        isReinvite: false,
                        cancellationToken);
                    if (issueResult.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelBookingOutcome>.Failure(issueResult.Error);
                    }

                    issued = issueResult.Value;
                    reinvited = issued.Invited;
                }
            }
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelBookingOutcome>.Failure(Error.Conflict(ex.Message));
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

        if (issued?.DispatchPlan is { } plan)
        {
            var status = await deliveries.DispatchClaimedAsync(
                plan.DeliveryId, plan.Message, cancellationToken, plan.OnSent);

            return Result<CancelBookingOutcome>.Success(
                new CancelBookingOutcome(
                    reinvited,
                    InviteCreated: reinvited,
                    status.ToString(),
                    plan.DeliveryId));
        }

        return Result<CancelBookingOutcome>.Success(
            new CancelBookingOutcome(reinvited, InviteCreated: reinvited, "Unavailable"));
    }
}
`````
