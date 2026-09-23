using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Invites;

/// <summary>Starts one recovery Invite for a attendee with missed appointments.</summary>
/// <param name="StaffUserId">The Coordinator starting the recovery.</param>
/// <param name="AttendeeId">The booked attendee whose no-shows are recovered.</param>
public sealed record StartRecoveryCommand(Guid StaffUserId, Guid AttendeeId);

/// <summary>Reports recovery Invite creation, or the awaiting-availability outcome.</summary>
/// <param name="InviteId">The new recovery Invite identifier, or empty when no events exist.</param>
/// <param name="AppointmentTypeIds">The recoverable snapshot offered, or awaiting availability.</param>
/// <param name="EmailSent">Whether the post-commit provider attempt completed successfully.</param>
public sealed record StartRecoveryResult(
    Guid InviteId,
    IReadOnlyList<Guid> AppointmentTypeIds,
    bool EmailSent);

/// <summary>
/// Gathers every currently recoverable no-show type into one recovery Invite under the
/// Attendee-first lifecycle lock order, revalidating eligibility after the locks because
/// preflight reads are never authoritative.
/// </summary>
/// <param name="attendees">The attendees.</param>
/// <param name="access">The access.</param>
/// <param name="invites">The invites.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="appointments">The appointments.</param>
/// <param name="issuer">The issuer.</param>
/// <param name="eventFinder">The event finder.</param>
/// <param name="deliveries">The deliveries.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class StartRecoveryHandler(
    IAttendeeRepository attendees,
    IStaffAccessAuthorizer access,
    IInviteRepository invites,
    IBookingRepository bookings,
    IBookingAppointmentRepository appointments,
    InviteIssuer issuer,
    EligibleEventFinder eventFinder,
    EmailDeliveryService deliveries,
    IUnitOfWork unitOfWork)
{
    /// <summary>Creates one recovery Invite after revalidating recoverability under lifecycle locks.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<StartRecoveryResult>> HandleAsync(
        StartRecoveryCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<StartRecoveryResult>.Failure(authorized.Error);
        }

        var located = await attendees.GetAsync(command.AttendeeId, cancellationToken);
        if (located is null)
        {
            return Result<StartRecoveryResult>.Failure(Error.NotFound("No such attendee."));
        }

        var preflight = await SelectRecoverableAsync(located, null, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var attendee = await attendees.LockForUpdateAsync(command.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.NotFound("No such attendee."));
        }

        var pending = await invites.LockPendingListForAttendeeAsync(attendee.Id, cancellationToken);
        var pendingRecoveries = pending
            .Where(invite => invite.RecoveryOfBookingId.HasValue)
            .ToList();

        var original = await bookings.LockActiveOriginalForAttendeeAsync(attendee.Id, cancellationToken);
        if (original is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.RecoveryNotAvailable(
                "No active booking has a recoverable missed appointment."));
        }

        var activeRecovery = await bookings.LockActiveRecoveryAsync(original.Id, cancellationToken);
        if (pendingRecoveries.Count > 0 || activeRecovery is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.RecoveryAlreadyPending(
                "A recovery is already pending for this attendee."));
        }

        var journey = await bookings.ListJourneyAsync(original.Id, cancellationToken);
        var selected = await SelectRecoverableAsync(attendee, journey, pendingRecoveries, cancellationToken);
        if (!selected.SequenceEqual(preflight))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.RecoveryStateChanged(
                "Recovery eligibility changed while starting the recovery."));
        }

        if (selected.Count == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.RecoveryNotAvailable(
                "No current requirement has a recoverable missed appointment."));
        }

        var options = await eventFinder.FindAsync(
            selected,
            Invite.RequiredOptionCount,
            [],
            cancellationToken);
        if (options.Count < Invite.RequiredOptionCount)
        {
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

            return Result<StartRecoveryResult>.Success(new StartRecoveryResult(Guid.Empty, selected, false));
        }

        InviteIssueResult issued;
        try
        {
            var issueResult = await issuer.IssueRecoveryAsync(
                attendee,
                original.Id,
                selected,
                options,
                ActorType.Staff,
                command.StaffUserId.ToString(),
                cancellationToken);
            if (issueResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<StartRecoveryResult>.Failure(issueResult.Error);
            }

            issued = issueResult.Value;
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.Validation(ex.Message));
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.RecoveryAlreadyPending(
                "A recovery is already pending for this attendee."));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var emailSent = false;
        if (issued.DispatchPlan is { } plan)
        {
            var status = await deliveries.DispatchClaimedAsync(
                plan.DeliveryId, plan.Message, cancellationToken, plan.OnSent);
            emailSent = status == EmailStatus.Sent;
        }

        return Result<StartRecoveryResult>.Success(
            new StartRecoveryResult(issued.InviteId!.Value, selected, emailSent));
    }

    private async Task<IReadOnlyList<Guid>> SelectRecoverableAsync(
        Attendee attendee,
        IReadOnlyList<Booking>? journey,
        CancellationToken cancellationToken) =>
        await SelectRecoverableAsync(attendee, journey, [], cancellationToken);

    private async Task<IReadOnlyList<Guid>> SelectRecoverableAsync(
        Attendee attendee,
        IReadOnlyList<Booking>? journey,
        IReadOnlyList<Invite> pendingRecoveries,
        CancellationToken cancellationToken)
    {
        if (journey is null or { Count: 0 })
        {
            var rootId = await LocateRootBookingIdAsync(attendee.Id, cancellationToken);
            if (rootId is null)
            {
                return [];
            }

            journey = await bookings.ListJourneyAsync(rootId.Value, cancellationToken);
        }

        var ids = journey.Select(booking => booking.Id).ToList();
        var rows = ids.Count == 0
            ? []
            : await appointments.ListForBookingsAsync(ids, cancellationToken);

        var attempts = RecoveryConfirmationValidator.BuildAttempts(journey, rows);

        var covered = pendingRecoveries
            .SelectMany(invite => invite.RequiredAppointmentTypeIds)
            .Distinct()
            .ToList();

        return new RecoveryRequirementSelector().Select(
            attendee.RequiredAppointmentTypeIds, attempts, covered);
    }

    private async Task<Guid?> LocateRootBookingIdAsync(Guid attendeeId, CancellationToken cancellationToken)
    {
        var active = await bookings.GetActiveForAttendeeAsync(attendeeId, cancellationToken);
        return active is null ? null : active.RecoveryOfBookingId ?? active.Id;
    }
}
