# 00b — Vocabulary edits 10 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — src/EventBooking.Application/Bookings/CancelAttendeeBookingHandler.cs — 1/1

<!-- vocabulary-file: {"id":52,"oldPath":"src/EventBooking.Application/Bookings/CancelCandidateBookingHandler.cs","newPath":"src/EventBooking.Application/Bookings/CancelAttendeeBookingHandler.cs","beforeSha":"e498dd164fd2e1e3152da9c5631412636fcc307381e27a363c418c5792b91781","afterSha":"3f12a48e14049a0bcd029180acd43fc71e25fee1a91f1d77c3d5efba889072b6","side":"after","part":1,"parts":1} -->

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
using EventBooking.Domain.Events;

namespace EventBooking.Application.Bookings;

/// <summary>Requests staff cancellation of one attendee's booking.</summary>
/// <param name="StaffUserId">The coordinator performing the cancellation; recorded as the audit actor.</param>
/// <param name="AttendeeId">The attendee the booking must belong to.</param>
/// <param name="BookingId">The booking to cancel.</param>
/// <param name="Rebook">
/// Whether to issue a replacement invite. Applies only to an original booking; rebooking a
/// cancelled recovery booking is the recovery path's job and is refused here.
/// </param>
public sealed record CancelAttendeeBookingCommand(
    Guid StaffUserId,
    Guid AttendeeId,
    Guid BookingId,
    bool Rebook);

/// <summary>
/// Cancels one attendee booking on a coordinator's behalf, reusing the attendee self-service
/// cancellation path so capacity release, recovery cascade, and audit semantics stay identical.
/// </summary>
/// <param name="access">Authorizes attendee management before anything is read or locked.</param>
/// <param name="bookings">Locks and re-reads the targeted booking and any active recovery.</param>
/// <param name="events">Locks every event whose capacity is released.</param>
/// <param name="attendees">Locks the attendee lifecycle root.</param>
/// <param name="invites">Locks pending invites so recovery invites can be superseded.</param>
/// <param name="bookingCanceller">Performs the cancellation, capacity release, and audit write.</param>
/// <param name="issuer">Issues the optional replacement invite.</param>
/// <param name="deliveries">Dispatches a staged replacement invite after commit.</param>
/// <param name="unitOfWork">Owns the transaction enclosing the lifecycle transitions.</param>
/// <param name="clock">The clock.</param>
public sealed class CancelAttendeeBookingHandler(
    IStaffAccessAuthorizer access,
    IBookingRepository bookings,
    IEventRepository events,
    IAttendeeRepository attendees,
    IInviteRepository invites,
    BookingCanceller bookingCanceller,
    InviteIssuer issuer,
    EmailDeliveryService deliveries,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    private const string NoSuchBooking = "No such active booking for this attendee.";

    /// <summary>
    /// Cancels the targeted booking under the attendee lifecycle lock order, cascading onto an
    /// active recovery when the target is the original, and optionally re-inviting the attendee.
    /// </summary>
    /// <param name="command">The coordinator's cancellation request.</param>
    /// <param name="cancellationToken">Cancels the authorization, locks, and dispatch.</param>
    /// <returns>
    /// The cancellation outcome, a forbidden failure when the caller lacks attendee management,
    /// a not-found failure for an unknown or already-inactive booking, or a conflict when a
    /// replacement invite is requested for a recovery booking.
    /// </returns>
    public async Task<Result<CancelBookingOutcome>> HandleAsync(
        CancelAttendeeBookingCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<CancelBookingOutcome>.Failure(authorized.Error);
        }

        var actorId = command.StaffUserId.ToString();

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var attendee = await attendees.LockForUpdateAsync(command.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            return Result<CancelBookingOutcome>.Failure(Error.NotFound(NoSuchBooking));
        }

        var pending = await invites.LockPendingListForAttendeeAsync(attendee.Id, cancellationToken);

        var booking = await bookings.LockByIdForAttendeeAsync(
            command.BookingId, attendee.Id, cancellationToken);
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

        var eventIds = new List<Guid> { booking.EventId };
        if (activeRecovery is not null && activeRecovery.EventId != booking.EventId)
        {
            eventIds.Add(activeRecovery.EventId);
        }

        eventIds.Sort();
        var lockedEvents = new Dictionary<Guid, Event>();
        foreach (var eventId in eventIds)
        {
            var locked = await events.LockForUpdateAsync(eventId, cancellationToken);
            if (locked is null)
            {
                return Result<CancelBookingOutcome>.Failure(Error.NotFound(NoSuchBooking));
            }

            lockedEvents[eventId] = locked;
        }

        var eventItem = lockedEvents[booking.EventId];

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
                        lockedEvents[activeRecovery.EventId],
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
                    eventItem,
                    ActorType.Staff,
                    actorId,
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

## before — src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs — 1/1

<!-- vocabulary-file: {"id":53,"oldPath":"src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs","newPath":"src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs","beforeSha":"a32bc6c72e9b45524f3b32aa469d017dd4c7f92f3dea81b070a950b7250c6024","afterSha":"365e07a9a5a54eb66e163cfa4db70e824d3eb296652c2ee8f5fa7e8b181a07a4","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Bookings;

/// <summary>Defines confirm booking command for the current use case.</summary>
/// <param name="Token">The token.</param>
/// <param name="ConfirmedSlotId">The confirmed slot id.</param>
public sealed record ConfirmBookingCommand(string? Token, Guid ConfirmedSlotId);

/// <summary>Returns the durable booking link and actual confirmation-email outcome.</summary>
/// <param name="BookingId">The newly created active booking identifier.</param>
/// <param name="Date">The confirmed slot's head-office date.</param>
/// <param name="StartTime">The confirmed slot's start time.</param>
/// <param name="EndTime">The derived four-hour end time.</param>
/// <param name="ManageToken">The raw management token returned once to the candidate.</param>
/// <param name="DeliveryStatus">The post-commit provider outcome.</param>
/// <param name="DeliveryId">The durable confirmation-delivery identifier.</param>
/// <param name="HeadOfficeAddress">
/// The head-office address the candidate attends, from the same portal configuration the
/// confirmation email uses, so the confirmed page and the email always name one address.
/// </param>
public sealed record ConfirmBookingOutcome(
    Guid BookingId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string ManageToken,
    string DeliveryStatus = "Pending",
    Guid? DeliveryId = null,
    string HeadOfficeAddress = "");

/// <summary>
/// Confirms one offered slot while serializing the candidate lifecycle and capacity rows,
/// atomically creating one operational appointment per candidate requirement.
/// </summary>
/// <param name="bookings">Persists the new booking row.</param>
/// <param name="appointments">Snapshots one operational appointment per candidate requirement.</param>
/// <param name="deliveries">Stages and dispatches the post-commit confirmation email.</param>
/// <param name="invites">The invites.</param>
/// <param name="candidates">The candidates.</param>
/// <param name="slots">The slots.</param>
/// <param name="capacities">The capacities.</param>
/// <param name="slotFinder">The slot finder.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="clock">The clock.</param>
/// <param name="portal">The portal.</param>
public sealed class ConfirmBookingHandler(
    IInviteRepository invites,
    ICandidateRepository candidates,
    IConfirmedSlotRepository slots,
    IBookingRepository bookings,
    IBookingAppointmentRepository appointments,
    ISlotCapacityRepository capacities,
    EligibleSlotFinder slotFinder,
    ITokenService tokens,
    EmailDeliveryService deliveries,
    IAuditLogger audit,
    IUnitOfWork unitOfWork,
    IClock clock,
    CandidatePortalOptions portal)
{
    private const string FilledUpMessage =
        "That time filled up while you were choosing. Please pick from the updated options.";

    /// <summary>
    /// Confirms a candidate's offered future slot while holding the slot, invite and capacity locks,
    /// snapshotting one Expected operational appointment per candidate requirement in the same save.
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

        // This pre-read locates only the candidate row that defines the lock order. Invite state
        // is re-read under lock below and this value must not be used as authority.
        var preflightInvite = await invites.GetByTokenHashAsync(tokens.Hash(command.Token), cancellationToken);
        if (preflightInvite is null)
        {
            return Result<ConfirmBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // Lock order for every candidate lifecycle transition is Candidate -> Invite -> Booking
        // -> ConfirmedSlot -> SlotCapacity. The candidate lock also serializes disjoint, legacy
        // tokens that could otherwise book different slots at the same time.
        var candidate = await candidates.LockForUpdateAsync(preflightInvite.CandidateId, cancellationToken);
        if (candidate is null)
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

        if (invite.CandidateId != candidate.Id)
        {
            return Result<ConfirmBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var isRecovery = invite.RecoveryOfBookingId.HasValue;
        IReadOnlyList<Guid> required;
        Booking? original = null;

        if (!isRecovery)
        {
            var existingBooking = await bookings.LockActiveForCandidateAsync(candidate.Id, cancellationToken);
            if (existingBooking is not null)
            {
                return Result<ConfirmBookingOutcome>.Failure(Error.Conflict("This candidate is already booked."));
            }

            if (!candidate.RequiredAppointmentTypeIds
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
            original = await bookings.LockActiveOriginalForCandidateAsync(candidate.Id, cancellationToken);
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
                candidate.RequiredAppointmentTypeIds,
                RecoveryConfirmationValidator.BuildAttempts(journey, rows),
                []);
            if (validated.IsFailure)
            {
                return await StaleRecoveryAsync(invite, transaction, cancellationToken);
            }

            required = validated.Value;
        }

        if (!invite.Offers(command.ConfirmedSlotId))
        {
            return Result<ConfirmBookingOutcome>.Failure(
                Error.Conflict("That time is not one of your options."));
        }

        var slot = await slots.LockForUpdateAsync(command.ConfirmedSlotId, cancellationToken);
        if (slot is null)
        {
            return Result<ConfirmBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var actorId = invite.Id.ToString();

        var locked = await capacities.LockForUpdateAsync(
            command.ConfirmedSlotId,
            required,
            cancellationToken);

        var stillAvailable =
            slot.Status == ConfirmedSlotStatus.Active
            && slot.Window.StartsAfter(clock.TodayAtHeadOffice)
            && locked.Count == required.Count
            && locked.All(c => c.HasSpare);

        if (!stillAvailable)
        {
            await DropAndReplaceOptionAsync(invite, candidate, required, slot.Id, actorId, cancellationToken);
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
                    bookingId, invite, original!, slot.Id, manageToken.TokenHash, clock.UtcNow)
                : Booking.Create(bookingId, invite, slot.Id, manageToken.TokenHash, clock.UtcNow);

            foreach (var capacity in locked)
            {
                capacity.Decrement();

                audit.Record(
                    AuditEntityTypes.ConfirmedSlot,
                    slot.Id,
                    AuditAction.CapacityDecremented,
                    ActorType.CandidateToken,
                    actorId,
                    $"{capacity.AppointmentTypeId} now {capacity.RemainingCapacity}");
            }

            invite.MarkUsed();
            if (!isRecovery)
            {
                candidate.MarkBooked();
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
            ActorType.CandidateToken,
            actorId,
            isRecovery ? $"root {original!.Id} {slot.Window}" : slot.Window.ToString());

        var delivery = deliveries.StagePending(
            candidate.Id,
            EmailTemplate.BookingConfirmation,
            bookingId: bookingId);
        deliveries.ClaimForDispatch(delivery);
        var message = CandidateEmailComposer.BookingConfirmation(
            candidate, required, slot, $"{portal.BaseUrl}/manage/{manageToken.Token}", portal);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ConfirmBookingOutcome>.Failure(Error.Conflict("This candidate is already booked."));
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
                slot.Window.Date,
                slot.Window.StartTime,
                slot.Window.EndTime,
                manageToken.Token,
                deliveryStatus.ToString(),
                delivery.Id,
                portal.HeadOfficeAddress));
    }

    /// <summary>
    /// Supersedes a recovery Invite whose snapshot no longer matches locked journey state,
    /// keeping the candidate-facing invalid-link response free of internal eligibility detail.
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
    /// replacement exists and the invite is left short, flags the candidate for coordinator
    /// follow-up (Issue #242) instead of leaving them with a silently shrinking choice.
    /// </summary>
    private async Task DropAndReplaceOptionAsync(
        Domain.Invites.Invite invite,
        Candidate candidate,
        IReadOnlyList<Guid> requiredAppointmentTypeIds,
        Guid lostSlotId,
        string actorId,
        CancellationToken cancellationToken)
    {
        invite.RemoveOption(lostSlotId);

        var replacement = await slotFinder.FindAsync(
            requiredAppointmentTypeIds,
            1,
            invite.OfferedSlotIds.Append(lostSlotId).ToList(),
            cancellationToken);

        if (replacement.Count == 1)
        {
            invite.AddOption(replacement[0].Id);

            audit.Record(
                AuditEntityTypes.Invite,
                invite.Id,
                AuditAction.InviteOptionReplaced,
                ActorType.CandidateToken,
                actorId,
                $"{lostSlotId} replaced by {replacement[0].Id}");

            return;
        }

        audit.Record(
            AuditEntityTypes.Invite,
            invite.Id,
            AuditAction.InviteOptionReplaced,
            ActorType.CandidateToken,
            actorId,
            $"{lostSlotId} dropped, no replacement available");

        if (invite.OfferedSlotIds.Count < Domain.Invites.Invite.RequiredOptionCount
            && candidate.Status == CandidateStatus.Invited)
        {
            candidate.MarkNoResponse();

            audit.Record(
                AuditEntityTypes.Invite,
                invite.Id,
                AuditAction.InviteOptionReplaced,
                ActorType.CandidateToken,
                actorId,
                $"only {invite.OfferedSlotIds.Count} live option(s) remain, candidate flagged for follow-up");
        }
    }
}
`````

## after — src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs — 1/1

<!-- vocabulary-file: {"id":53,"oldPath":"src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs","newPath":"src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs","beforeSha":"a32bc6c72e9b45524f3b32aa469d017dd4c7f92f3dea81b070a950b7250c6024","afterSha":"365e07a9a5a54eb66e163cfa4db70e824d3eb296652c2ee8f5fa7e8b181a07a4","side":"after","part":1,"parts":1} -->

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
/// <param name="TransitionalLocationAddress">
/// The transitional-location address the attendee attends, from the same portal configuration the
/// confirmation email uses, so the confirmed page and the email always name one address.
/// </param>
public sealed record ConfirmBookingOutcome(
    Guid BookingId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string ManageToken,
    string DeliveryStatus = "Pending",
    Guid? DeliveryId = null,
    string TransitionalLocationAddress = "");

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
                delivery.Id,
                portal.TransitionalLocationAddress));
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
`````

## before — src/EventBooking.Application/Bookings/RecoveryConfirmationValidator.cs — 1/1

<!-- vocabulary-file: {"id":54,"oldPath":"src/EventBooking.Application/Bookings/RecoveryConfirmationValidator.cs","newPath":"src/EventBooking.Application/Bookings/RecoveryConfirmationValidator.cs","beforeSha":"6a839d039c94a04aaa64f5652563258f8da095c3652756b7e9d659fa56a6d5ac","afterSha":"191666ab572d1c275dcdadc85021bea7533af5bfca3799b3a1cf69df2cb48793","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Bookings;

/// <summary>Revalidates a Pending recovery Invite snapshot against locked journey state.</summary>
public sealed class RecoveryConfirmationValidator
{
    /// <summary>Revalidates a Pending recovery Invite snapshot against locked journey state.</summary>
    /// <param name="invite">The Pending recovery Invite being confirmed.</param>
    /// <param name="currentRequirementTypeIds">The candidate's current derived requirement set.</param>
    /// <param name="attempts">Non-cancelled attempts across the journey, in any order.</param>
    /// <param name="typesAlreadyCoveredByAnotherRecovery">Types another recovery already covers.</param>
    /// <returns>The revalidated snapshot, or a stale-snapshot failure.</returns>
    public Result<IReadOnlyList<Guid>> Validate(
        Invite invite,
        IReadOnlyCollection<Guid> currentRequirementTypeIds,
        IReadOnlyCollection<RecoveryAttempt> attempts,
        IReadOnlyCollection<Guid> typesAlreadyCoveredByAnotherRecovery)
    {
        if (invite.RecoveryOfBookingId is null)
        {
            return Result<IReadOnlyList<Guid>>.Failure(Error.RecoveryStateChanged(
                "The invite is not a recovery invite."));
        }

        var selected = new RecoveryRequirementSelector().Select(
            currentRequirementTypeIds, attempts, typesAlreadyCoveredByAnotherRecovery);

        if (!selected.SequenceEqual(invite.RequiredAppointmentTypeIds.Order()))
        {
            return Result<IReadOnlyList<Guid>>.Failure(Error.RecoveryStateChanged(
                "The recovery snapshot no longer matches current eligibility."));
        }

        return Result<IReadOnlyList<Guid>>.Success(selected);
    }

    /// <summary>Builds non-cancelled journey attempts for the selector, in any order.</summary>
    /// <param name="journey">The original and direct recovery Bookings.</param>
    /// <param name="rows">The appointments owned by that journey.</param>
    /// <returns>One attempt per appointment outside cancelled Bookings.</returns>
    public static IReadOnlyList<RecoveryAttempt> BuildAttempts(
        IReadOnlyList<Booking> journey,
        IReadOnlyList<BookingAppointment> rows)
    {
        var createdByBooking = journey.ToDictionary(booking => booking.Id);
        var cancelled = journey
            .Where(booking => booking.Status == BookingStatus.Cancelled)
            .Select(booking => booking.Id)
            .ToHashSet();

        return rows
            .Where(row => !cancelled.Contains(row.BookingId) && createdByBooking.ContainsKey(row.BookingId))
            .Select(row => new RecoveryAttempt(
                row.Id,
                row.AppointmentTypeId,
                row.Status,
                createdByBooking[row.BookingId].CreatedAt))
            .ToList();
    }
}
`````

## after — src/EventBooking.Application/Bookings/RecoveryConfirmationValidator.cs — 1/1

<!-- vocabulary-file: {"id":54,"oldPath":"src/EventBooking.Application/Bookings/RecoveryConfirmationValidator.cs","newPath":"src/EventBooking.Application/Bookings/RecoveryConfirmationValidator.cs","beforeSha":"6a839d039c94a04aaa64f5652563258f8da095c3652756b7e9d659fa56a6d5ac","afterSha":"191666ab572d1c275dcdadc85021bea7533af5bfca3799b3a1cf69df2cb48793","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Bookings;

/// <summary>Revalidates a Pending recovery Invite snapshot against locked journey state.</summary>
public sealed class RecoveryConfirmationValidator
{
    /// <summary>Revalidates a Pending recovery Invite snapshot against locked journey state.</summary>
    /// <param name="invite">The Pending recovery Invite being confirmed.</param>
    /// <param name="currentRequirementTypeIds">The attendee's current derived requirement set.</param>
    /// <param name="attempts">Non-cancelled attempts across the journey, in any order.</param>
    /// <param name="typesAlreadyCoveredByAnotherRecovery">Types another recovery already covers.</param>
    /// <returns>The revalidated snapshot, or a stale-snapshot failure.</returns>
    public Result<IReadOnlyList<Guid>> Validate(
        Invite invite,
        IReadOnlyCollection<Guid> currentRequirementTypeIds,
        IReadOnlyCollection<RecoveryAttempt> attempts,
        IReadOnlyCollection<Guid> typesAlreadyCoveredByAnotherRecovery)
    {
        if (invite.RecoveryOfBookingId is null)
        {
            return Result<IReadOnlyList<Guid>>.Failure(Error.RecoveryStateChanged(
                "The invite is not a recovery invite."));
        }

        var selected = new RecoveryRequirementSelector().Select(
            currentRequirementTypeIds, attempts, typesAlreadyCoveredByAnotherRecovery);

        if (!selected.SequenceEqual(invite.RequiredAppointmentTypeIds.Order()))
        {
            return Result<IReadOnlyList<Guid>>.Failure(Error.RecoveryStateChanged(
                "The recovery snapshot no longer matches current eligibility."));
        }

        return Result<IReadOnlyList<Guid>>.Success(selected);
    }

    /// <summary>Builds non-cancelled journey attempts for the selector, in any order.</summary>
    /// <param name="journey">The original and direct recovery Bookings.</param>
    /// <param name="rows">The appointments owned by that journey.</param>
    /// <returns>One attempt per appointment outside cancelled Bookings.</returns>
    public static IReadOnlyList<RecoveryAttempt> BuildAttempts(
        IReadOnlyList<Booking> journey,
        IReadOnlyList<BookingAppointment> rows)
    {
        var createdByBooking = journey.ToDictionary(booking => booking.Id);
        var cancelled = journey
            .Where(booking => booking.Status == BookingStatus.Cancelled)
            .Select(booking => booking.Id)
            .ToHashSet();

        return rows
            .Where(row => !cancelled.Contains(row.BookingId) && createdByBooking.ContainsKey(row.BookingId))
            .Select(row => new RecoveryAttempt(
                row.Id,
                row.AppointmentTypeId,
                row.Status,
                createdByBooking[row.BookingId].CreatedAt))
            .ToList();
    }
}
`````

## before — src/EventBooking.Application/Bookings/ViewBookingHandler.cs — 1/1

<!-- vocabulary-file: {"id":55,"oldPath":"src/EventBooking.Application/Bookings/ViewBookingHandler.cs","newPath":"src/EventBooking.Application/Bookings/ViewBookingHandler.cs","beforeSha":"7e4fdb2b22dcf5a9ab73a68061528775fafb568907645c5006d131a1cfed9120","afterSha":"b5c826cb9cdb9eab27508e070ca4d5f5283a0f867ce4ba4257f5c580220a4175","side":"before","part":1,"parts":1} -->

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
/// <param name="CandidateName">The candidate name.</param>
public sealed record BookingView(
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Display,
    string CandidateName);

/// <summary>Defines view booking query for the current use case.</summary>
/// <param name="ManageToken">The manage token.</param>
public sealed record ViewBookingQuery(string? ManageToken);

/// <summary>Defines view booking handler for the current use case.</summary>
/// <param name="bookings">The bookings.</param>
/// <param name="candidates">The candidates.</param>
/// <param name="slots">The slots.</param>
/// <param name="tokens">The tokens.</param>
public sealed class ViewBookingHandler(
    IBookingRepository bookings,
    ICandidateRepository candidates,
    IConfirmedSlotRepository slots,
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

        var slot = await slots.GetAsync(booking.ConfirmedSlotId, cancellationToken);
        var candidate = await candidates.GetAsync(booking.CandidateId, cancellationToken);

        if (slot is null || candidate is null)
        {
            return Result<BookingView>.Failure(Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        return Result<BookingView>.Success(new BookingView(
            slot.Window.Date,
            slot.Window.StartTime,
            slot.Window.EndTime,
            CandidateEmailComposer.FormatWindow(slot.Window),
            candidate.Name));
    }
}
`````

## after — src/EventBooking.Application/Bookings/ViewBookingHandler.cs — 1/1

<!-- vocabulary-file: {"id":55,"oldPath":"src/EventBooking.Application/Bookings/ViewBookingHandler.cs","newPath":"src/EventBooking.Application/Bookings/ViewBookingHandler.cs","beforeSha":"7e4fdb2b22dcf5a9ab73a68061528775fafb568907645c5006d131a1cfed9120","afterSha":"b5c826cb9cdb9eab27508e070ca4d5f5283a0f867ce4ba4257f5c580220a4175","side":"after","part":1,"parts":1} -->

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
