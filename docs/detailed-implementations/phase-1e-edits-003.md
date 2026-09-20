# 01e — Location-restricted invites and closed attendee transitions, edits 3 (Task 8)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — src/EventBooking.Application/Bookings/ViewInviteHandler.cs — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Application/Bookings/ViewInviteHandler.cs","beforeSha":"472388a9ef5c33ff5cf9cfdf4e952ecb3b2cbd75048c540a70a775bc8d8b5fe1","afterSha":"f2e0b744f3bbf0789d9b5d5fe1e5a8ec391158a710574d48c7ab74a8d4dec0a1","side":"before","part":1,"parts":1} -->

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
            attendee.MarkNoResponse();
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

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Application/Bookings/ViewInviteHandler.cs","beforeSha":"472388a9ef5c33ff5cf9cfdf4e952ecb3b2cbd75048c540a70a775bc8d8b5fe1","afterSha":"f2e0b744f3bbf0789d9b5d5fe1e5a8ec391158a710574d48c7ab74a8d4dec0a1","side":"after","part":1,"parts":1} -->

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

## before — src/EventBooking.Application/Events/CancelEventHandler.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Application/Events/CancelEventHandler.cs","beforeSha":"c12e5cbf90e47ea06f7bdda8fb65d06961229072e9ce73c64463ae17839c18f2","afterSha":"05dcbe299c5155524bc638000e822fb155fb01b0860b239d5eff9eea6ead4624","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Application/Events/CancelEventHandler.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Application/Events/CancelEventHandler.cs","beforeSha":"c12e5cbf90e47ea06f7bdda8fb65d06961229072e9ce73c64463ae17839c18f2","afterSha":"05dcbe299c5155524bc638000e822fb155fb01b0860b239d5eff9eea6ead4624","side":"after","part":1,"parts":1} -->

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

                    attendee.ResetToNotYetInvited(clock.UtcNow);

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
