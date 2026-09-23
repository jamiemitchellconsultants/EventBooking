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
