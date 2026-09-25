using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.EventGroups;
using EventBooking.Domain.Events;
using EventBooking.Domain.Invites;
using EventBooking.Domain.SelfRegistrations;
using EventBooking.Domain.Time;

namespace EventBooking.Application.SelfRegistrations;

/// <summary>Confirms one pending request through its token.</summary>
/// <param name="ConfirmationToken">The signed confirmation token.</param>
public sealed record ConfirmSelfRegistrationCommand(string? ConfirmationToken);

/// <summary>The created booking and its management link.</summary>
/// <param name="BookingId">The newly created active booking identifier.</param>
/// <param name="ManageToken">The raw management token returned once to the registrant.</param>
public sealed record ConfirmSelfRegistrationOutcome(Guid BookingId, string ManageToken);

/// <summary>
/// Confirms one pending request on the anonymous pipeline. Locks in canonical order —
/// event group, attendee, booking, event, then the required capacity rows — after a port
/// read of the registration, and charges exactly the required types all-or-nothing.
/// </summary>
/// <param name="groups">The event groups.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="invites">The invites.</param>
/// <param name="attendeeGroups">The attendee groups.</param>
/// <param name="events">The events.</param>
/// <param name="capacities">The capacities.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="appointments">The appointments.</param>
/// <param name="locations">The locations.</param>
/// <param name="settings">The system settings.</param>
/// <param name="emails">The emails.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="unitOfWork">The unitOfWork.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
/// <param name="zones">The zones.</param>
/// <param name="correlation">The correlation.</param>
public sealed class ConfirmSelfRegistrationHandler(
    IEventGroupRepository groups,
    IAttendeeRepository attendees,
    IInviteRepository invites,
    IAttendeeGroupRepository attendeeGroups,
    IEventRepository events,
    IEventCapacityRepository capacities,
    IBookingRepository bookings,
    IBookingAppointmentRepository appointments,
    ILocationRepository locations,
    ISystemSettingsRepository settings,
    IEmailDeliveryRepository emails,
    ITokenService tokens,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock,
    IEventWindowZones zones,
    ICorrelationContext correlation)
{
    private const string InvalidLinkMessage = "This confirmation link is invalid.";
    private const string ExpiredLinkMessage = "This confirmation link has expired.";

    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<ConfirmSelfRegistrationOutcome>> HandleAsync(
        ConfirmSelfRegistrationCommand command, CancellationToken ct)
    {
        if (!SelfRegistrationToken.TryRead(
                tokens, command.ConfirmationToken, out var requestId, out var tokenVersion))
            return Result<ConfirmSelfRegistrationOutcome>.Failure(
                Error.TokenInvalid(InvalidLinkMessage));

        // Port read: the registration without a lock, so the locks below follow the
        // canonical order. Everything is re-validated after locking.
        var port = await groups.GetRegistrationAsync(requestId, ct);
        if (port is null)
            return Result<ConfirmSelfRegistrationOutcome>.Failure(
                Error.TokenInvalid(InvalidLinkMessage));

        var early = await RefusalAsync(port, tokenVersion, ct);
        if (early is not null) return early;

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        var group = await groups.LockForUpdateAsync(port.EventGroupId, ct);
        if (group is null)
            return Result<ConfirmSelfRegistrationOutcome>.Failure(
                Error.TokenInvalid(InvalidLinkMessage));

        // Re-read without tracking: the port read above may predate a concurrent
        // confirmation that the group lock just serialized against.
        var registration = await groups.GetRegistrationUntrackedAsync(requestId, ct);
        if (registration is null)
            return Result<ConfirmSelfRegistrationOutcome>.Failure(
                Error.TokenInvalid(InvalidLinkMessage));

        var late = await RefusalAsync(registration, tokenVersion, ct);
        if (late is not null)
        {
            await transaction.RollbackAsync(ct);
            return late;
        }

        var attendeeGroup = await attendeeGroups.GetAsync(registration.AttendeeGroupId, ct);
        if (attendeeGroup is null || !attendeeGroup.IsActive)
            return Result<ConfirmSelfRegistrationOutcome>.Failure(
                SelfRegistrationErrors.GroupNotSelectable());

        if (!group.IsOpen
            || group.Events.SingleOrDefault(x => x.EventId == registration.EventId) is not { IsOpen: true })
            return Result<ConfirmSelfRegistrationOutcome>.Failure(SelfRegistrationErrors.NotOpen());

        var live = await LiveRequirementsAsync(group, ct);
        if (live.IsFailure)
            return Result<ConfirmSelfRegistrationOutcome>.Failure(live.Error);

        var attendee = await ResolveAttendeeAsync(registration, attendeeGroup, ct);
        if (attendee is null)
            return Result<ConfirmSelfRegistrationOutcome>.Failure(
                Error.NotFound("No such attendee."));

        if (await bookings.LockActiveForAttendeeAsync(attendee.Id, ct) is not null)
            return Result<ConfirmSelfRegistrationOutcome>.Failure(
                Error.Conflict("This attendee is already booked."));

        var eventItem = await events.LockForUpdateAsync(registration.EventId, ct);
        if (eventItem is null)
            return Result<ConfirmSelfRegistrationOutcome>.Failure(Error.NotFound("No such event."));
        var location = await locations.GetAsync(eventItem.LocationId, ct);
        if (eventItem.Status != EventStatus.Active || location is null
            || eventItem.Window.HasStarted(zones, location.TimeZoneId, clock.UtcNow))
            return Result<ConfirmSelfRegistrationOutcome>.Failure(
                SelfRegistrationErrors.EventUnavailable());

        try
        {
            EventGroupTypeSet.Validate(
                live.Value,
                [eventItem.Capacities.Select(c => c.AppointmentTypeId).ToArray()]);
        }
        catch (DomainException ex)
        {
            return Result<ConfirmSelfRegistrationOutcome>.Failure(Error.Validation(ex.Message));
        }

        var required = attendeeGroup.RequiredAppointmentTypeIds;
        var locked = await capacities.LockForUpdateAsync(eventItem.Id, required, ct);
        if (locked.Count != required.Count)
            return Result<ConfirmSelfRegistrationOutcome>.Failure(
                Error.Validation("The event no longer lists every required appointment type."));

        try
        {
            eventItem.ChargeRequiredTypes(required);
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(ct);
            return Result<ConfirmSelfRegistrationOutcome>.Failure(Error.CapacityExhausted(ex.Message));
        }

        var now = clock.UtcNow;
        var systemSettings = await settings.GetAsync(ct);
        // The internal invite records the single confirmed option: it offers exactly the
        // event being booked, so the booking factory's pending-and-offers checks hold.
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, now.AddDays(systemSettings.InviteExpiryDays),
            [location.Id], [eventItem.Id], required, retryCount: 0,
            systemSettings.InviteExpiryDays, systemSettings.MaxAutoRetryCount,
            inviteOptionCount: 1);
        invites.Add(invite);

        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, now);
        bookings.Add(booking);
        foreach (var typeId in required)
            appointments.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
        invite.MarkUsed();
        if (attendee.Status == AttendeeStatus.NotYetInvited)
            attendee.MarkInvited(now);
        if (attendee.Status != AttendeeStatus.Booked)
            attendee.MarkBooked(now);

        registration.Confirm(tokenVersion, now);

        emails.Add(SelfRegistrationDelivery.StageConfirmation(
            Guid.NewGuid(), attendee.Id, booking.Id, now, correlation.CorrelationId));

        var details = $"correlation {correlation.CorrelationId}; event {eventItem.Id}";
        audit.Record(AuditEntityTypes.SelfRegistration, registration.RequestId,
            AuditAction.SelfRegistrationConfirmed, ActorType.Anonymous,
            correlation.CorrelationId, details);
        audit.Record(AuditEntityTypes.Booking, booking.Id, AuditAction.BookingCreated,
            ActorType.Anonymous, correlation.CorrelationId, details);
        foreach (var typeId in required)
            audit.Record(AuditEntityTypes.Event, eventItem.Id, AuditAction.CapacityDecremented,
                ActorType.Anonymous, correlation.CorrelationId, $"type {typeId}");

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (UniqueConstraintViolationException)
        {
            await transaction.RollbackAsync(ct);
            return Result<ConfirmSelfRegistrationOutcome>.Failure(
                Error.Conflict("This attendee is already booked."));
        }

        return Result<ConfirmSelfRegistrationOutcome>.Success(new ConfirmSelfRegistrationOutcome(
            booking.Id, tokens.Issue(TokenPurpose.Manage, booking.Id, booking.ManageTokenVersion)));
    }

    private async Task<Result<ConfirmSelfRegistrationOutcome>?> RefusalAsync(
        PendingRegistration registration, int tokenVersion, CancellationToken ct)
    {
        if (registration.Status == SelfRegistrationStatus.Confirmed)
        {
            var attendee = await attendees.GetByEmailAsync(registration.Email, ct);
            var booking = attendee is null
                ? null
                : await bookings.GetActiveForAttendeeAsync(attendee.Id, ct);
            if (booking is not null && booking.EventId == registration.EventId)
                return Result<ConfirmSelfRegistrationOutcome>.Failure(Error.AlreadyConfirmed(
                    $"This request already confirmed booking {booking.Id}.", booking.Id));
            return Result<ConfirmSelfRegistrationOutcome>.Failure(
                Error.TokenInvalid(InvalidLinkMessage));
        }

        if (registration.Status == SelfRegistrationStatus.Expired
            || clock.UtcNow >= registration.ExpiresAt)
            return Result<ConfirmSelfRegistrationOutcome>.Failure(
                Error.TokenExpired(ExpiredLinkMessage));

        if (tokenVersion != registration.TokenVersion)
            return Result<ConfirmSelfRegistrationOutcome>.Failure(
                Error.TokenInvalid(InvalidLinkMessage));

        return null;
    }

    private async Task<Result<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>>> LiveRequirementsAsync(
        EventGroup group, CancellationToken ct)
    {
        var live = new Dictionary<Guid, IReadOnlyCollection<Guid>>();
        foreach (var id in group.AttendeeGroups.Select(x => x.AttendeeGroupId))
        {
            var selected = await attendeeGroups.GetAsync(id, ct);
            if (selected is null || !selected.IsActive)
                return Result<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>>.Failure(
                    SelfRegistrationErrors.GroupNotSelectable());
            live[id] = selected.RequiredAppointmentTypeIds;
        }

        return Result<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>>.Success(live);
    }

    private async Task<Attendee?> ResolveAttendeeAsync(
        PendingRegistration registration,
        Domain.AttendeeGroups.AttendeeGroup attendeeGroup,
        CancellationToken ct)
    {
        var existing = await attendees.GetByEmailAsync(registration.Email, ct);
        if (existing is null)
        {
            var created = Attendee.Create(
                Guid.NewGuid(), registration.Name, registration.Email, attendeeGroup,
                clock.UtcNow);
            attendees.Add(created);
            return created;
        }

        var locked = await attendees.LockForUpdateAsync(existing.Id, ct);
        if (locked is null) return null;
        locked.UpdateDetails(registration.Name, registration.Email);
        locked.AssignAttendeeGroup(attendeeGroup);
        return locked;
    }
}
