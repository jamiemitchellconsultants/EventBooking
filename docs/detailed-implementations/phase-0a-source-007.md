# 00a — Port source 7 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.Application/Bookings/CancelBookingHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Bookings/CancelBookingHandler.cs","encoding":"utf8","sha256":"03e21506cf30c0bccfabd4acd19e1973edad1bd141c7d1cc754188dc0f3774f8","parts":1,"part":1} -->

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

## src/EventBooking.Application/Bookings/CancelCandidateBookingHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Bookings/CancelCandidateBookingHandler.cs","encoding":"utf8","sha256":"e498dd164fd2e1e3152da9c5631412636fcc307381e27a363c418c5792b91781","parts":1,"part":1} -->

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

## src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs","encoding":"utf8","sha256":"a32bc6c72e9b45524f3b32aa469d017dd4c7f92f3dea81b070a950b7250c6024","parts":1,"part":1} -->

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

## src/EventBooking.Application/Bookings/RecoveryConfirmationValidator.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Bookings/RecoveryConfirmationValidator.cs","encoding":"utf8","sha256":"6a839d039c94a04aaa64f5652563258f8da095c3652756b7e9d659fa56a6d5ac","parts":1,"part":1} -->

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

## src/EventBooking.Application/Bookings/ViewBookingHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Bookings/ViewBookingHandler.cs","encoding":"utf8","sha256":"7e4fdb2b22dcf5a9ab73a68061528775fafb568907645c5006d131a1cfed9120","parts":1,"part":1} -->

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

## src/EventBooking.Application/Bookings/ViewInviteHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Bookings/ViewInviteHandler.cs","encoding":"utf8","sha256":"ae62716c097f82d42df5663feb34eafcd755c0e3b9a8ee49424f566118193341","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Bookings;

/// <summary>Defines invite option view for the current use case.</summary>
/// <param name="ConfirmedSlotId">The confirmed slot id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="Display">The display.</param>
public sealed record InviteOptionView(
    Guid ConfirmedSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Display);

/// <summary>Defines invite view for the current use case.</summary>
/// <param name="InviteId">The invite id.</param>
/// <param name="CandidateName">The candidate name.</param>
/// <param name="AppointmentTypeNames">The appointment type names.</param>
/// <param name="Options">The options.</param>
/// <param name="IsRecovery">The is recovery.</param>
public sealed record InviteView(
    Guid InviteId,
    string CandidateName,
    IReadOnlyList<string> AppointmentTypeNames,
    IReadOnlyList<InviteOptionView> Options,
    bool IsRecovery);

/// <summary>Defines view invite query for the current use case.</summary>
/// <param name="Token">The token.</param>
public sealed record ViewInviteQuery(string? Token);

/// <summary>Defines view invite handler for the current use case.</summary>
/// <param name="invites">The invites.</param>
/// <param name="candidates">The candidates.</param>
/// <param name="slots">The slots.</param>
/// <param name="slotFinder">The slot finder.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="clock">The clock.</param>
public sealed class ViewInviteHandler(
    IInviteRepository invites,
    ICandidateRepository candidates,
    IConfirmedSlotRepository slots,
    EligibleSlotFinder slotFinder,
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
    /// Projects the usable future appointment options for the supplied candidate invite token.
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

        var candidate = await candidates.GetAsync(invite.CandidateId, cancellationToken);
        if (candidate is null)
        {
            return Result<InviteView>.Failure(Error.NotFound(InvalidLinkMessage));
        }

        var required = invite.RequiredAppointmentTypeIds;
        var today = clock.TodayAtHeadOffice;

        var options = new List<ConfirmedSlot>();
        var deadSlotIds = new List<Guid>();
        foreach (var slotId in invite.OfferedSlotIds)
        {
            var slot = await slots.GetAsync(slotId, cancellationToken);
            if (slot is not null
                && slot.Status == ConfirmedSlotStatus.Active
                && slot.Window.StartsAfter(today)
                && HasSpareFor(slot, required))
            {
                options.Add(slot);
            }
            else
            {
                deadSlotIds.Add(slotId);
            }
        }

        var mutated = false;
        if (deadSlotIds.Count > 0)
        {
            foreach (var deadSlotId in deadSlotIds)
            {
                invite.RemoveOption(deadSlotId);
            }

            var replacements = await slotFinder.FindAsync(
                required,
                deadSlotIds.Count,
                invite.OfferedSlotIds.Concat(deadSlotIds).ToList(),
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
                    ActorType.CandidateToken,
                    invite.Id.ToString(),
                    $"{deadSlotIds.Count} lost option(s) replaced by {replacement.Id}");
            }

            mutated = true;
        }

        if (options.Count < Domain.Invites.Invite.RequiredOptionCount
            && candidate.Status == CandidateStatus.Invited)
        {
            candidate.MarkNoResponse();
            mutated = true;

            audit.Record(
                AuditEntityTypes.Invite,
                invite.Id,
                AuditAction.InviteOptionReplaced,
                ActorType.CandidateToken,
                invite.Id.ToString(),
                $"only {options.Count} live option(s) remain, candidate flagged for follow-up");
        }

        if (mutated)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var view = new InviteView(
            invite.Id,
            candidate.Name,
            invite.RequiredAppointmentTypeIds.Select(AppointmentTypeIds.NameOf).ToList(),
            options
                .OrderBy(s => s.Window)
                .Select(s => new InviteOptionView(
                    s.Id,
                    s.Window.Date,
                    s.Window.StartTime,
                    s.Window.EndTime,
                    CandidateEmailComposer.FormatWindow(s.Window)))
                .ToList(),
            invite.RecoveryOfBookingId is not null);

        return Result<InviteView>.Success(view);
    }

    /// <summary>
    /// Determines whether the slot holds spare capacity for every snapshotted requirement.
    /// A missing capacity row is treated as no spare capacity rather than throwing.
    /// </summary>
    private static bool HasSpareFor(ConfirmedSlot slot, IReadOnlyList<Guid> required)
    {
        try
        {
            return slot.HasSpareCapacityForAll(required);
        }
        catch (DomainException)
        {
            return false;
        }
    }
}
`````

## src/EventBooking.Application/Candidates/CandidateCsvParser.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Candidates/CandidateCsvParser.cs","encoding":"utf8","sha256":"60acea7f411fea84dec9ea9f56c3a5da16372cf5944bff887d5bd3aeb2957b8e","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Application.Candidates;

/// <summary>Defines candidate csv row for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="EmployeeGroupCode">The employee group code.</param>
public sealed record CandidateCsvRow(
    int LineNumber,
    string Name,
    string Email,
    string EmployeeGroupCode);

/// <summary>Defines candidate csv error for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Message">The message.</param>
public sealed record CandidateCsvError(int LineNumber, string Message);

/// <summary>Defines candidate csv parse result for the current use case.</summary>
/// <param name="Rows">The rows.</param>
/// <param name="Errors">The errors.</param>
public sealed record CandidateCsvParseResult(
    IReadOnlyList<CandidateCsvRow> Rows,
    IReadOnlyList<CandidateCsvError> Errors);

/// <summary>
/// Structural validation only — field count, blank fields, one canonicalized Employee Group code,
/// and duplicate emails within the file. Group existence is the import handler's rule.
/// </summary>
public static class CandidateCsvParser
{
    /// <summary>Defines required header for the current use case.</summary>
    public const string RequiredHeader = "name,email,employee_group";

    /// <summary>Defines parse for the current use case.</summary>
    /// <param name="content">The content.</param>
    public static CandidateCsvParseResult Parse(string? content)
    {
        var rows = new List<CandidateCsvRow>();
        var errors = new List<CandidateCsvError>();

        if (string.IsNullOrWhiteSpace(content))
        {
            errors.Add(new CandidateCsvError(0, "The file is empty."));
            return new CandidateCsvParseResult(rows, errors);
        }

        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        if (!string.Equals(lines[0].Trim(), RequiredHeader, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new CandidateCsvError(1, $"The header line must read exactly: {RequiredHeader}"));
            return new CandidateCsvParseResult(rows, errors);
        }

        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 1; index < lines.Length; index++)
        {
            var lineNumber = index + 1;
            var line = lines[index];

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = line.Split(',');
            if (fields.Length != 3)
            {
                errors.Add(new CandidateCsvError(
                    lineNumber, "Expected 3 comma-separated fields: name, email, employee_group."));
                continue;
            }

            var name = fields[0].Trim();
            var email = fields[1].Trim();
            var groupCode = fields[2].Trim();

            if (name.Length == 0)
            {
                errors.Add(new CandidateCsvError(lineNumber, "Name is required."));
                continue;
            }

            if (email.Length == 0)
            {
                errors.Add(new CandidateCsvError(lineNumber, "Email is required."));
                continue;
            }

            if (groupCode.Length == 0)
            {
                errors.Add(new CandidateCsvError(lineNumber, "Employee group is required."));
                continue;
            }

            if (!seenEmails.Add(email))
            {
                errors.Add(new CandidateCsvError(
                    lineNumber, $"{email.ToLowerInvariant()} appears more than once in this file."));
                continue;
            }

            rows.Add(new CandidateCsvRow(
                lineNumber, name, email.ToLowerInvariant(), groupCode.ToUpperInvariant()));
        }

        if (errors.Count == 0 && rows.Count == 0)
        {
            errors.Add(new CandidateCsvError(1, "The file contains no candidate rows."));
        }

        return new CandidateCsvParseResult(rows, errors);
    }
}
`````
