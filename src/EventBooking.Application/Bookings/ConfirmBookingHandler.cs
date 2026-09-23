using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Bookings;

/// <summary>Defines confirm booking command for the current use case.</summary>
/// <param name="Token">The token.</param>
/// <param name="EventId">The event id.</param>
public sealed record ConfirmBookingCommand(string? Token, Guid EventId);

/// <summary>Returns the durable booking link and actual confirmation-email outcome.</summary>
/// <param name="BookingId">The newly created active booking identifier.</param>
/// <param name="Date">The event's transitional-location date.</param>
/// <param name="StartTime">The event's start time.</param>
/// <param name="EndTime">The derived four-hour end time.</param>
/// <param name="ManageToken">The raw management token returned once to the attendee.</param>
/// <param name="DeliveryStatus">The post-commit provider outcome.</param>
/// <param name="DeliveryId">The durable confirmation-delivery identifier.</param>
public sealed record ConfirmBookingOutcome(
    Guid BookingId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string ManageToken,
    string DeliveryStatus = "Pending",
    Guid? DeliveryId = null);

/// <summary>
/// Confirms one offered event while serializing the attendee lifecycle and capacity rows,
/// atomically creating one operational appointment per attendee requirement.
/// </summary>
/// <param name="bookings">Persists the new booking row.</param>
/// <param name="appointments">Snapshots one operational appointment per attendee requirement.</param>
/// <param name="deliveries">Stages and dispatches the post-commit confirmation email.</param>
/// <param name="invites">The invites.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="events">The events.</param>
/// <param name="capacities">The capacities.</param>
/// <param name="eventFinder">The event finder.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="clock">The clock.</param>
/// <param name="portal">The portal.</param>
public sealed class ConfirmBookingHandler(
    IInviteRepository invites,
    IAttendeeRepository attendees,
    IEventRepository events,
    IBookingRepository bookings,
    IBookingAppointmentRepository appointments,
    IEventCapacityRepository capacities,
    EligibleEventFinder eventFinder,
    ITokenService tokens,
    EmailDeliveryService deliveries,
    IAuditLogger audit,
    IUnitOfWork unitOfWork,
    IClock clock,
    AttendeePortalOptions portal)
{
    private const string FilledUpMessage =
        "That time filled up while you were choosing. Please pick from the updated options.";

    /// <summary>
    /// Confirms a attendee's offered future event while holding the eventItem, invite and capacity locks,
    /// snapshotting one Expected operational appointment per attendee requirement in the same save.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<ConfirmBookingOutcome>> HandleAsync(
        ConfirmBookingCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Token is null || !tokens.TryRead(command.Token, out _))
        {
            return Result<ConfirmBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        // This pre-read locates only the attendee row that defines the lock order. Invite state
        // is re-read under lock below and this value must not be used as authority.
        var preflightInvite = await invites.GetByTokenHashAsync(tokens.Hash(command.Token), cancellationToken);
        if (preflightInvite is null)
        {
            return Result<ConfirmBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // Lock order for every attendee lifecycle transition is Attendee -> Invite -> Booking
        // -> Event -> EventCapacity. The attendee lock also serializes disjoint, legacy
        // tokens that could otherwise book different events at the same time.
        var attendee = await attendees.LockForUpdateAsync(preflightInvite.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            return Result<ConfirmBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var invite = await invites.LockByTokenHashForUpdateAsync(
            tokens.Hash(command.Token),
            cancellationToken);
        if (invite is null || !invite.IsUsableAt(clock.UtcNow))
        {
            return Result<ConfirmBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        if (invite.AttendeeId != attendee.Id)
        {
            return Result<ConfirmBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var isRecovery = invite.RecoveryOfBookingId.HasValue;
        IReadOnlyList<Guid> required;
        Booking? original = null;

        if (!isRecovery)
        {
            var existingBooking = await bookings.LockActiveForAttendeeAsync(attendee.Id, cancellationToken);
            if (existingBooking is not null)
            {
                return Result<ConfirmBookingOutcome>.Failure(Error.Conflict("This attendee is already booked."));
            }

            if (!attendee.RequiredAppointmentTypeIds
                .Order()
                .SequenceEqual(invite.RequiredAppointmentTypeIds.Order()))
            {
                invite.MarkSuperseded();
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Result<ConfirmBookingOutcome>.Failure(
                    Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
            }

            required = invite.RequiredAppointmentTypeIds;
        }
        else
        {
            original = await bookings.LockActiveOriginalForAttendeeAsync(attendee.Id, cancellationToken);
            var activeRecovery = original is null
                ? null
                : await bookings.LockActiveRecoveryAsync(original.Id, cancellationToken);
            if (original is null
                || original.Id != invite.RecoveryOfBookingId
                || activeRecovery is not null)
            {
                return await StaleRecoveryAsync(invite, transaction, cancellationToken);
            }

            var journey = await bookings.ListJourneyAsync(original.Id, cancellationToken);
            var rows = await appointments.ListForBookingsAsync(
                journey.Select(entry => entry.Id).ToList(),
                cancellationToken);
            var validated = new RecoveryConfirmationValidator().Validate(
                invite,
                attendee.RequiredAppointmentTypeIds,
                RecoveryConfirmationValidator.BuildAttempts(journey, rows),
                []);
            if (validated.IsFailure)
            {
                return await StaleRecoveryAsync(invite, transaction, cancellationToken);
            }

            required = validated.Value;
        }

        if (!invite.Offers(command.EventId))
        {
            return Result<ConfirmBookingOutcome>.Failure(
                Error.Conflict("That time is not one of your options."));
        }

        var eventItem = await events.LockForUpdateAsync(command.EventId, cancellationToken);
        if (eventItem is null)
        {
            return Result<ConfirmBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var actorId = invite.Id.ToString();

        var locked = await capacities.LockForUpdateAsync(
            command.EventId,
            required,
            cancellationToken);

        var stillAvailable =
            eventItem.Status == EventStatus.Active
            && eventItem.Window.StartsAfter(clock.TodayAtTransitionalLocation)
            && locked.Count == required.Count
            && locked.All(c => c.HasSpare);

        if (!stillAvailable)
        {
            await DropAndReplaceOptionAsync(invite, attendee, required, eventItem.Id, actorId, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<ConfirmBookingOutcome>.Failure(Error.Conflict(FilledUpMessage));
        }

        var bookingId = Guid.NewGuid();
        var manageToken = tokens.Issue(bookingId);

        Booking booking;
        try
        {
            booking = isRecovery
                ? Booking.CreateRecovery(
                    bookingId, invite, original!, eventItem.Id, manageToken.TokenHash, clock.UtcNow)
                : Booking.Create(bookingId, invite, eventItem.Id, manageToken.TokenHash, clock.UtcNow);

            foreach (var capacity in locked)
            {
                capacity.Decrement();

                audit.Record(
                    AuditEntityTypes.Event,
                    eventItem.Id,
                    AuditAction.CapacityDecremented,
                    ActorType.AttendeeToken,
                    actorId,
                    $"{capacity.AppointmentTypeId} now {capacity.RemainingCapacity}");
            }

            invite.MarkUsed();
            if (!isRecovery)
            {
                attendee.MarkBooked();
            }
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ConfirmBookingOutcome>.Failure(Error.Conflict(ex.Message));
        }

        bookings.Add(booking);

        foreach (var appointmentTypeId in required)
        {
            appointments.Add(BookingAppointment.Create(
                Guid.NewGuid(), booking.Id, appointmentTypeId));
        }

        audit.Record(
            AuditEntityTypes.Booking,
            bookingId,
            isRecovery ? AuditAction.RecoveryBookingCreated : AuditAction.BookingCreated,
            ActorType.AttendeeToken,
            actorId,
            isRecovery ? $"root {original!.Id} {eventItem.Window}" : eventItem.Window.ToString());

        var delivery = deliveries.StagePending(
            attendee.Id,
            EmailTemplate.BookingConfirmation,
            bookingId: bookingId);
        deliveries.ClaimForDispatch(delivery);
        var message = AttendeeEmailComposer.BookingConfirmation(
            attendee, required, eventItem, $"{portal.BaseUrl}/manage/{manageToken.Token}", portal);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ConfirmBookingOutcome>.Failure(Error.Conflict("This attendee is already booked."));
        }

        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // The provider call is deliberately after the commit: a mail failure must not undo a good booking.
        var deliveryStatus = await deliveries.DispatchClaimedAsync(delivery.Id, message, cancellationToken);

        return Result<ConfirmBookingOutcome>.Success(
            new ConfirmBookingOutcome(
                bookingId,
                eventItem.Window.Date,
                eventItem.Window.StartTime,
                eventItem.Window.EndTime,
                manageToken.Token,
                deliveryStatus.ToString(),
                delivery.Id));
    }

    /// <summary>
    /// Supersedes a recovery Invite whose snapshot no longer matches locked journey state,
    /// keeping the attendee-facing invalid-link response free of internal eligibility detail.
    /// </summary>
    private async Task<Result<ConfirmBookingOutcome>> StaleRecoveryAsync(
        Domain.Invites.Invite invite,
        ITransactionScope transaction,
        CancellationToken cancellationToken)
    {
        invite.MarkSuperseded();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<ConfirmBookingOutcome>.Failure(
            Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
    }

    /// <summary>
    /// Drops an option that filled up, replacing it when capacity exists elsewhere. When no
    /// replacement exists and the invite is left short, flags the attendee for coordinator
    /// follow-up (Issue #242) instead of leaving them with a silently shrinking choice.
    /// </summary>
    private async Task DropAndReplaceOptionAsync(
        Domain.Invites.Invite invite,
        Attendee attendee,
        IReadOnlyList<Guid> requiredAppointmentTypeIds,
        Guid lostEventId,
        string actorId,
        CancellationToken cancellationToken)
    {
        invite.RemoveOption(lostEventId);

        var replacement = await eventFinder.FindAsync(
            requiredAppointmentTypeIds,
            1,
            invite.OfferedEventIds.Append(lostEventId).ToList(),
            cancellationToken);

        if (replacement.Count == 1)
        {
            invite.AddOption(replacement[0].Id);

            audit.Record(
                AuditEntityTypes.Invite,
                invite.Id,
                AuditAction.InviteOptionReplaced,
                ActorType.AttendeeToken,
                actorId,
                $"{lostEventId} replaced by {replacement[0].Id}");

            return;
        }

        audit.Record(
            AuditEntityTypes.Invite,
            invite.Id,
            AuditAction.InviteOptionReplaced,
            ActorType.AttendeeToken,
            actorId,
            $"{lostEventId} dropped, no replacement available");

        if (invite.OfferedEventIds.Count < Domain.Invites.Invite.RequiredOptionCount
            && attendee.Status == AttendeeStatus.Invited)
        {
            attendee.MarkNoResponse();

            audit.Record(
                AuditEntityTypes.Invite,
                invite.Id,
                AuditAction.InviteOptionReplaced,
                ActorType.AttendeeToken,
                actorId,
                $"only {invite.OfferedEventIds.Count} live option(s) remain, attendee flagged for follow-up");
        }
    }
}
