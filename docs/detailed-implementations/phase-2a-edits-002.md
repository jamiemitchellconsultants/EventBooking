# 02a — Deterministic attendee links and the token version counter, edits 2 (Task 9a)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs — 1/1

<!-- retirement-file: {"id":4,"file":"src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs","beforeSha":"6c33aaff72c5eaa4682db83316924bc5180311ba5cb7f714ce0fe829b422ce85","afterSha":"7d16a8cdfb3e49541150e7b4b4abc920c5af5483c261f10358dcb0cd45a119e9","side":"after","part":1,"parts":1} -->

`````csharp
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
        if (!tokens.TryRead(command.Token, out var link) || link.Purpose != TokenPurpose.Book)
        {
            return Result<ConfirmBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        // This pre-read locates only the attendee row that defines the lock order. Invite state
        // is re-read under lock below and this value must not be used as authority.
        var preflightInvite = await invites.GetAsync(link.EntityId, cancellationToken);
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

        var invite = await invites.LockForUpdateAsync(link.EntityId, cancellationToken);
        if (invite is null
            || invite.TokenVersion != link.Version
            || !invite.IsUsableAt(clock.UtcNow))
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
        var manageToken = tokens.Issue(
            TokenPurpose.Manage, bookingId, Booking.InitialManageTokenVersion);

        Booking booking;
        try
        {
            booking = isRecovery
                ? Booking.CreateRecovery(bookingId, invite, original!, eventItem.Id, clock.UtcNow)
                : Booking.Create(bookingId, invite, eventItem.Id, clock.UtcNow);

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
                attendee.MarkBooked(clock.UtcNow);
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
            attendee, required, eventItem, $"{portal.BaseUrl}/manage/{manageToken}", portal);

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
                manageToken,
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
            attendee.MarkNoResponse(clock.UtcNow);

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
`````

## before — src/EventBooking.Application/Bookings/ViewBookingHandler.cs — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Application/Bookings/ViewBookingHandler.cs","beforeSha":"b5c826cb9cdb9eab27508e070ca4d5f5283a0f867ce4ba4257f5c580220a4175","afterSha":"e4422738ef63e4e37e352143e35620bb1a967ed621cae2c79e3de2c67bbee827","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Bookings;

/// <summary>Defines booking view for the current use case.</summary>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="Display">The display.</param>
/// <param name="AttendeeName">The attendee name.</param>
public sealed record BookingView(
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Display,
    string AttendeeName);

/// <summary>Defines view booking query for the current use case.</summary>
/// <param name="ManageToken">The manage token.</param>
public sealed record ViewBookingQuery(string? ManageToken);

/// <summary>Defines view booking handler for the current use case.</summary>
/// <param name="bookings">The bookings.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="events">The events.</param>
/// <param name="tokens">The tokens.</param>
public sealed class ViewBookingHandler(
    IBookingRepository bookings,
    IAttendeeRepository attendees,
    IEventRepository events,
    ITokenService tokens)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<BookingView>> HandleAsync(
        ViewBookingQuery query,
        CancellationToken cancellationToken)
    {
        if (query.ManageToken is null || !tokens.TryRead(query.ManageToken, out _))
        {
            return Result<BookingView>.Failure(Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var booking = await bookings.GetByManageTokenHashAsync(
            tokens.Hash(query.ManageToken), cancellationToken);

        if (booking is null || booking.Status != BookingStatus.Active)
        {
            return Result<BookingView>.Failure(Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var eventItem = await events.GetAsync(booking.EventId, cancellationToken);
        var attendee = await attendees.GetAsync(booking.AttendeeId, cancellationToken);

        if (eventItem is null || attendee is null)
        {
            return Result<BookingView>.Failure(Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        return Result<BookingView>.Success(new BookingView(
            eventItem.Window.Date,
            eventItem.Window.StartTime,
            eventItem.Window.EndTime,
            AttendeeEmailComposer.FormatWindow(eventItem.Window),
            attendee.Name));
    }
}
`````

## after — src/EventBooking.Application/Bookings/ViewBookingHandler.cs — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Application/Bookings/ViewBookingHandler.cs","beforeSha":"b5c826cb9cdb9eab27508e070ca4d5f5283a0f867ce4ba4257f5c580220a4175","afterSha":"e4422738ef63e4e37e352143e35620bb1a967ed621cae2c79e3de2c67bbee827","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Bookings;

/// <summary>Defines booking view for the current use case.</summary>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="Display">The display.</param>
/// <param name="AttendeeName">The attendee name.</param>
public sealed record BookingView(
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Display,
    string AttendeeName);

/// <summary>Defines view booking query for the current use case.</summary>
/// <param name="ManageToken">The manage token.</param>
public sealed record ViewBookingQuery(string? ManageToken);

/// <summary>Defines view booking handler for the current use case.</summary>
/// <param name="bookings">The bookings.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="events">The events.</param>
/// <param name="tokens">The tokens.</param>
public sealed class ViewBookingHandler(
    IBookingRepository bookings,
    IAttendeeRepository attendees,
    IEventRepository events,
    ITokenService tokens)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<BookingView>> HandleAsync(
        ViewBookingQuery query,
        CancellationToken cancellationToken)
    {
        if (!tokens.TryRead(query.ManageToken, out var link) || link.Purpose != TokenPurpose.Manage)
        {
            return Result<BookingView>.Failure(Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var booking = await bookings.GetAsync(link.EntityId, cancellationToken);

        if (booking is null
            || booking.ManageTokenVersion != link.Version
            || booking.Status != BookingStatus.Active)
        {
            return Result<BookingView>.Failure(Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var eventItem = await events.GetAsync(booking.EventId, cancellationToken);
        var attendee = await attendees.GetAsync(booking.AttendeeId, cancellationToken);

        if (eventItem is null || attendee is null)
        {
            return Result<BookingView>.Failure(Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        return Result<BookingView>.Success(new BookingView(
            eventItem.Window.Date,
            eventItem.Window.StartTime,
            eventItem.Window.EndTime,
            AttendeeEmailComposer.FormatWindow(eventItem.Window),
            attendee.Name));
    }
}
`````

## before — src/EventBooking.Application/Bookings/ViewInviteHandler.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Application/Bookings/ViewInviteHandler.cs","beforeSha":"f2e0b744f3bbf0789d9b5d5fe1e5a8ec391158a710574d48c7ab74a8d4dec0a1","afterSha":"78874e707395085afa3d8cb7ad10b6de97f79d703c5597164d91d07a34291a3a","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Bookings;

/// <summary>Defines invite option view for the current use case.</summary>
/// <param name="EventId">The event id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="Display">The display.</param>
public sealed record InviteOptionView(
    Guid EventId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Display);

/// <summary>Defines invite view for the current use case.</summary>
/// <param name="InviteId">The invite id.</param>
/// <param name="AttendeeName">The attendee name.</param>
/// <param name="AppointmentTypeNames">The appointment type names.</param>
/// <param name="Options">The options.</param>
/// <param name="IsRecovery">The is recovery.</param>
public sealed record InviteView(
    Guid InviteId,
    string AttendeeName,
    IReadOnlyList<string> AppointmentTypeNames,
    IReadOnlyList<InviteOptionView> Options,
    bool IsRecovery);

/// <summary>Defines view invite query for the current use case.</summary>
/// <param name="Token">The token.</param>
public sealed record ViewInviteQuery(string? Token);

/// <summary>Defines view invite handler for the current use case.</summary>
/// <param name="invites">The invites.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="events">The events.</param>
/// <param name="eventFinder">The event finder.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="clock">The clock.</param>
public sealed class ViewInviteHandler(
    IInviteRepository invites,
    IAttendeeRepository attendees,
    IEventRepository events,
    EligibleEventFinder eventFinder,
    IAuditLogger audit,
    IUnitOfWork unitOfWork,
    ITokenService tokens,
    IClock clock)
{
    /// <summary>
    /// One message for every failure. A caller must not be able to tell a forged token from an
    /// expired one.
    /// </summary>
    public const string InvalidLinkMessage = "This booking link is no longer valid.";

    /// <summary>
    /// Projects the usable future appointment options for the supplied attendee invite token.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<InviteView>> HandleAsync(
        ViewInviteQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Token is null || !tokens.TryRead(query.Token, out _))
        {
            return Result<InviteView>.Failure(Error.NotFound(InvalidLinkMessage));
        }

        var invite = await invites.GetByTokenHashAsync(tokens.Hash(query.Token), cancellationToken);
        if (invite is null || !invite.IsUsableAt(clock.UtcNow))
        {
            return Result<InviteView>.Failure(Error.NotFound(InvalidLinkMessage));
        }

        var attendee = await attendees.GetAsync(invite.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            return Result<InviteView>.Failure(Error.NotFound(InvalidLinkMessage));
        }

        var required = invite.RequiredAppointmentTypeIds;
        var today = clock.TodayAtTransitionalLocation;

        var options = new List<Event>();
        var deadEventIds = new List<Guid>();
        foreach (var eventId in invite.OfferedEventIds)
        {
            var eventItem = await events.GetAsync(eventId, cancellationToken);
            if (eventItem is not null
                && eventItem.Status == EventStatus.Active
                && eventItem.Window.StartsAfter(today)
                && HasSpareFor(eventItem, required))
            {
                options.Add(eventItem);
            }
            else
            {
                deadEventIds.Add(eventId);
            }
        }

        var mutated = false;
        if (deadEventIds.Count > 0)
        {
            foreach (var deadEventId in deadEventIds)
            {
                invite.RemoveOption(deadEventId);
            }

            var replacements = await eventFinder.FindAsync(
                required,
                deadEventIds.Count,
                invite.OfferedEventIds.Concat(deadEventIds).ToList(),
                cancellationToken);

            foreach (var replacement in replacements)
            {
                if (replacement is null)
                {
                    continue;
                }

                invite.AddOption(replacement.Id);
                options.Add(replacement);
                mutated = true;

                audit.Record(
                    AuditEntityTypes.Invite,
                    invite.Id,
                    AuditAction.InviteOptionReplaced,
                    ActorType.AttendeeToken,
                    invite.Id.ToString(),
                    $"{deadEventIds.Count} lost option(s) replaced by {replacement.Id}");
            }

            mutated = true;
        }

        if (options.Count < Domain.Invites.Invite.RequiredOptionCount
            && attendee.Status == AttendeeStatus.Invited)
        {
            attendee.MarkNoResponse(clock.UtcNow);
            mutated = true;

            audit.Record(
                AuditEntityTypes.Invite,
                invite.Id,
                AuditAction.InviteOptionReplaced,
                ActorType.AttendeeToken,
                invite.Id.ToString(),
                $"only {options.Count} live option(s) remain, attendee flagged for follow-up");
        }

        if (mutated)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var view = new InviteView(
            invite.Id,
            attendee.Name,
            invite.RequiredAppointmentTypeIds.Select(AppointmentTypeIds.NameOf).ToList(),
            options
                .OrderBy(s => s.Window)
                .Select(s => new InviteOptionView(
                    s.Id,
                    s.Window.Date,
                    s.Window.StartTime,
                    s.Window.EndTime,
                    AttendeeEmailComposer.FormatWindow(s.Window)))
                .ToList(),
            invite.RecoveryOfBookingId is not null);

        return Result<InviteView>.Success(view);
    }

    /// <summary>
    /// Determines whether the event holds spare capacity for every snapshotted requirement.
    /// A missing capacity row is treated as no spare capacity rather than throwing.
    /// </summary>
    private static bool HasSpareFor(Event eventItem, IReadOnlyList<Guid> required)
    {
        try
        {
            return eventItem.HasSpareCapacityForAll(required);
        }
        catch (DomainException)
        {
            return false;
        }
    }
}
`````

## after — src/EventBooking.Application/Bookings/ViewInviteHandler.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Application/Bookings/ViewInviteHandler.cs","beforeSha":"f2e0b744f3bbf0789d9b5d5fe1e5a8ec391158a710574d48c7ab74a8d4dec0a1","afterSha":"78874e707395085afa3d8cb7ad10b6de97f79d703c5597164d91d07a34291a3a","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Bookings;

/// <summary>Defines invite option view for the current use case.</summary>
/// <param name="EventId">The event id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="Display">The display.</param>
public sealed record InviteOptionView(
    Guid EventId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Display);

/// <summary>Defines invite view for the current use case.</summary>
/// <param name="InviteId">The invite id.</param>
/// <param name="AttendeeName">The attendee name.</param>
/// <param name="AppointmentTypeNames">The appointment type names.</param>
/// <param name="Options">The options.</param>
/// <param name="IsRecovery">The is recovery.</param>
public sealed record InviteView(
    Guid InviteId,
    string AttendeeName,
    IReadOnlyList<string> AppointmentTypeNames,
    IReadOnlyList<InviteOptionView> Options,
    bool IsRecovery);

/// <summary>Defines view invite query for the current use case.</summary>
/// <param name="Token">The token.</param>
public sealed record ViewInviteQuery(string? Token);

/// <summary>Defines view invite handler for the current use case.</summary>
/// <param name="invites">The invites.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="events">The events.</param>
/// <param name="eventFinder">The event finder.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="clock">The clock.</param>
public sealed class ViewInviteHandler(
    IInviteRepository invites,
    IAttendeeRepository attendees,
    IEventRepository events,
    EligibleEventFinder eventFinder,
    IAuditLogger audit,
    IUnitOfWork unitOfWork,
    ITokenService tokens,
    IClock clock)
{
    /// <summary>
    /// One message for every failure. A caller must not be able to tell a forged token from an
    /// expired one.
    /// </summary>
    public const string InvalidLinkMessage = "This booking link is no longer valid.";

    /// <summary>
    /// Projects the usable future appointment options for the supplied attendee invite token.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<InviteView>> HandleAsync(
        ViewInviteQuery query,
        CancellationToken cancellationToken)
    {
        if (!tokens.TryRead(query.Token, out var link) || link.Purpose != TokenPurpose.Book)
        {
            return Result<InviteView>.Failure(Error.NotFound(InvalidLinkMessage));
        }

        var invite = await invites.GetAsync(link.EntityId, cancellationToken);
        if (invite is null || invite.TokenVersion != link.Version || !invite.IsUsableAt(clock.UtcNow))
        {
            return Result<InviteView>.Failure(Error.NotFound(InvalidLinkMessage));
        }

        var attendee = await attendees.GetAsync(invite.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            return Result<InviteView>.Failure(Error.NotFound(InvalidLinkMessage));
        }

        var required = invite.RequiredAppointmentTypeIds;
        var today = clock.TodayAtTransitionalLocation;

        var options = new List<Event>();
        var deadEventIds = new List<Guid>();
        foreach (var eventId in invite.OfferedEventIds)
        {
            var eventItem = await events.GetAsync(eventId, cancellationToken);
            if (eventItem is not null
                && eventItem.Status == EventStatus.Active
                && eventItem.Window.StartsAfter(today)
                && HasSpareFor(eventItem, required))
            {
                options.Add(eventItem);
            }
            else
            {
                deadEventIds.Add(eventId);
            }
        }

        var mutated = false;
        if (deadEventIds.Count > 0)
        {
            foreach (var deadEventId in deadEventIds)
            {
                invite.RemoveOption(deadEventId);
            }

            var replacements = await eventFinder.FindAsync(
                required,
                deadEventIds.Count,
                invite.OfferedEventIds.Concat(deadEventIds).ToList(),
                cancellationToken);

            foreach (var replacement in replacements)
            {
                if (replacement is null)
                {
                    continue;
                }

                invite.AddOption(replacement.Id);
                options.Add(replacement);
                mutated = true;

                audit.Record(
                    AuditEntityTypes.Invite,
                    invite.Id,
                    AuditAction.InviteOptionReplaced,
                    ActorType.AttendeeToken,
                    invite.Id.ToString(),
                    $"{deadEventIds.Count} lost option(s) replaced by {replacement.Id}");
            }

            mutated = true;
        }

        if (options.Count < Domain.Invites.Invite.RequiredOptionCount
            && attendee.Status == AttendeeStatus.Invited)
        {
            attendee.MarkNoResponse(clock.UtcNow);
            mutated = true;

            audit.Record(
                AuditEntityTypes.Invite,
                invite.Id,
                AuditAction.InviteOptionReplaced,
                ActorType.AttendeeToken,
                invite.Id.ToString(),
                $"only {options.Count} live option(s) remain, attendee flagged for follow-up");
        }

        if (mutated)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var view = new InviteView(
            invite.Id,
            attendee.Name,
            invite.RequiredAppointmentTypeIds.Select(AppointmentTypeIds.NameOf).ToList(),
            options
                .OrderBy(s => s.Window)
                .Select(s => new InviteOptionView(
                    s.Id,
                    s.Window.Date,
                    s.Window.StartTime,
                    s.Window.EndTime,
                    AttendeeEmailComposer.FormatWindow(s.Window)))
                .ToList(),
            invite.RecoveryOfBookingId is not null);

        return Result<InviteView>.Success(view);
    }

    /// <summary>
    /// Determines whether the event holds spare capacity for every snapshotted requirement.
    /// A missing capacity row is treated as no spare capacity rather than throwing.
    /// </summary>
    private static bool HasSpareFor(Event eventItem, IReadOnlyList<Guid> required)
    {
        try
        {
            return eventItem.HasSpareCapacityForAll(required);
        }
        catch (DomainException)
        {
            return false;
        }
    }
}
`````

## before — src/EventBooking.Application/Invites/CancelRecoveryInviteHandler.cs — 1/1

<!-- retirement-file: {"id":7,"file":"src/EventBooking.Application/Invites/CancelRecoveryInviteHandler.cs","beforeSha":"e2dbb458e31f1845935dcc751092faebcb638ec707a03fde002ed9d0197309a5","afterSha":"547f27393a00d9087f84fe8a2dd6c7776f239760a19d44a60fb6584a32ddccba","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Invites;

/// <summary>Cancels one pending recovery Invite without touching capacity or appointments.</summary>
/// <param name="StaffUserId">The Coordinator cancelling the recovery Invite.</param>
/// <param name="AttendeeId">The attendee route the Invite must belong to.</param>
/// <param name="InviteId">The pending recovery Invite to cancel.</param>
public sealed record CancelRecoveryInviteCommand(Guid StaffUserId, Guid AttendeeId, Guid InviteId);

/// <summary>Cancels one Pending recovery Invite without changing capacity or Attendee status.</summary>
/// <param name="attendees">The attendees.</param>
/// <param name="access">The access.</param>
/// <param name="invites">The invites.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class CancelRecoveryInviteHandler(
    IAttendeeRepository attendees,
    IStaffAccessAuthorizer access,
    IInviteRepository invites,
    IBookingRepository bookings,
    IAuditLogger audit,
    IUnitOfWork unitOfWork)
{
    /// <summary>Cancels one Pending recovery Invite without changing capacity or Attendee status.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> HandleAsync(
        CancelRecoveryInviteCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var attendee = await attendees.LockForUpdateAsync(command.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("No such attendee."));
        }

        var invite = await invites.LockForUpdateAsync(command.InviteId, cancellationToken);
        if (invite is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("No such invite."));
        }

        if (invite.RecoveryOfBookingId is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("Only a recovery invite can be cancelled."));
        }

        if (invite.AttendeeId != command.AttendeeId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("This invite does not belong to this attendee."));
        }

        var root = await bookings.LockForUpdateAsync(invite.RecoveryOfBookingId.Value, cancellationToken);
        if (root is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("The original booking no longer exists."));
        }

        if (root.AttendeeId != command.AttendeeId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("This invite does not belong to this attendee."));
        }

        if (invite.Status != InviteStatus.Pending)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("This recovery invite is no longer pending."));
        }

        try
        {
            invite.RotateTokenHash(Guid.NewGuid().ToString("N"));
            invite.CancelRecovery();
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Validation(ex.Message));
        }

        audit.Record(
            AuditEntityTypes.Invite,
            invite.Id,
            AuditAction.RecoveryInviteCancelled,
            ActorType.Staff,
            command.StaffUserId.ToString(),
            $"root {root.Id}");

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

        return Result.Success();
    }
}
`````

## after — src/EventBooking.Application/Invites/CancelRecoveryInviteHandler.cs — 1/1

<!-- retirement-file: {"id":7,"file":"src/EventBooking.Application/Invites/CancelRecoveryInviteHandler.cs","beforeSha":"e2dbb458e31f1845935dcc751092faebcb638ec707a03fde002ed9d0197309a5","afterSha":"547f27393a00d9087f84fe8a2dd6c7776f239760a19d44a60fb6584a32ddccba","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Invites;

/// <summary>Cancels one pending recovery Invite without touching capacity or appointments.</summary>
/// <param name="StaffUserId">The Coordinator cancelling the recovery Invite.</param>
/// <param name="AttendeeId">The attendee route the Invite must belong to.</param>
/// <param name="InviteId">The pending recovery Invite to cancel.</param>
public sealed record CancelRecoveryInviteCommand(Guid StaffUserId, Guid AttendeeId, Guid InviteId);

/// <summary>Cancels one Pending recovery Invite without changing capacity or Attendee status.</summary>
/// <param name="attendees">The attendees.</param>
/// <param name="access">The access.</param>
/// <param name="invites">The invites.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class CancelRecoveryInviteHandler(
    IAttendeeRepository attendees,
    IStaffAccessAuthorizer access,
    IInviteRepository invites,
    IBookingRepository bookings,
    IAuditLogger audit,
    IUnitOfWork unitOfWork)
{
    /// <summary>Cancels one Pending recovery Invite without changing capacity or Attendee status.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> HandleAsync(
        CancelRecoveryInviteCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var attendee = await attendees.LockForUpdateAsync(command.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("No such attendee."));
        }

        var invite = await invites.LockForUpdateAsync(command.InviteId, cancellationToken);
        if (invite is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("No such invite."));
        }

        if (invite.RecoveryOfBookingId is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("Only a recovery invite can be cancelled."));
        }

        if (invite.AttendeeId != command.AttendeeId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("This invite does not belong to this attendee."));
        }

        var root = await bookings.LockForUpdateAsync(invite.RecoveryOfBookingId.Value, cancellationToken);
        if (root is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("The original booking no longer exists."));
        }

        if (root.AttendeeId != command.AttendeeId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("This invite does not belong to this attendee."));
        }

        if (invite.Status != InviteStatus.Pending)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("This recovery invite is no longer pending."));
        }

        try
        {
            invite.RotateToken();
            invite.CancelRecovery();
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Validation(ex.Message));
        }

        audit.Record(
            AuditEntityTypes.Invite,
            invite.Id,
            AuditAction.RecoveryInviteCancelled,
            ActorType.Staff,
            command.StaffUserId.ToString(),
            $"root {root.Id}");

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

        return Result.Success();
    }
}
`````
