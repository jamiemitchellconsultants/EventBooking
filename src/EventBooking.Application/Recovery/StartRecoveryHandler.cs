using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Domain.Access;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Recovery;

/// <summary>Starts one recovery invite for the attendee's recoverable no-show types.</summary>
/// <param name="StaffUserId">The coordinator starting the recovery.</param>
/// <param name="AttendeeId">The booked attendee whose no-shows are recovered.</param>
/// <param name="AdditionalLocationIds">Further locations the coordinator opens up.</param>
public sealed record StartRecoveryCommand(Guid StaffUserId, Guid AttendeeId, IReadOnlyList<Guid> AdditionalLocationIds);

/// <summary>The issued recovery invite, its locations and its recoverable snapshot.</summary>
/// <param name="RecoveryInviteId">The newly issued recovery invite identifier.</param>
/// <param name="LocationIds">The original booking's locations unioned with the additional ones.</param>
/// <param name="RecoverableTypeIds">The snapshot the recovery offers.</param>
public sealed record StartRecoveryOutcome(Guid RecoveryInviteId, IReadOnlyList<Guid> LocationIds, IReadOnlyList<Guid> RecoverableTypeIds);

/// <summary>
/// Starts one recovery invite for the attendee's recoverable no-show types, defaulting to
/// the original booking's locations. Locks attendee, pending invite, original and active
/// recovery, then the journey's appointments, so the selection reads authoritative state.
/// </summary>
/// <param name="attendees">The attendees.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="appointments">The appointments.</param>
/// <param name="invites">The invites.</param>
/// <param name="locations">The locations.</param>
/// <param name="settings">The system settings repository.</param>
/// <param name="access">The staff access authorizer.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="clock">The clock.</param>
/// <param name="eligibility">The event eligibility query.</param>
/// <param name="issuer">The invite issuer.</param>
public sealed class StartRecoveryHandler(
    IAttendeeRepository attendees,
    IBookingRepository bookings,
    IBookingAppointmentRepository appointments,
    IInviteRepository invites,
    ILocationRepository locations,
    ISystemSettingsRepository settings,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IClock clock,
    IEventEligibilityQuery eligibility,
    IInviteIssuer issuer)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<StartRecoveryOutcome>> HandleAsync(
        StartRecoveryCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId, StaffCapability.ManageAttendees, null, ct);
        if (authorized.IsFailure) return Result<StartRecoveryOutcome>.Failure(authorized.Error);

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var attendee = await attendees.LockForUpdateAsync(command.AttendeeId, ct);
        if (attendee is null)
            return Result<StartRecoveryOutcome>.Failure(Error.NotFound("No such attendee."));

        // The pending invite is locked before the bookings: Invite sits below Booking in
        // the ladder, and a second start must see the first's invite, not select past it.
        var pendingInvite = await invites.LockPendingForAttendeeAsync(attendee.Id, ct);
        if (pendingInvite?.RecoveryOfBookingId is not null)
            return Result<StartRecoveryOutcome>.Failure(
                Error.RecoveryActive("A recovery is already active for this attendee."));

        var activeOriginal = await bookings.LockActiveOriginalForAttendeeAsync(attendee.Id, ct);
        if (activeOriginal is null)
            return Result<StartRecoveryOutcome>.Failure(
                Error.RecoveryNotAvailable("No active original booking can be recovered."));
        if (activeOriginal.AttendeeId != attendee.Id)
            return Result<StartRecoveryOutcome>.Failure(Error.NotFound("No such booking."));

        var pendingRecovery = await bookings.LockActiveRecoveryAsync(activeOriginal.Id, ct);
        if (pendingRecovery is not null)
            return Result<StartRecoveryOutcome>.Failure(
                Error.RecoveryActive("A recovery is already active for this booking."));

        var journey = await bookings.ListJourneyAsync(activeOriginal.Id, ct);
        var attempts = new List<RecoveryAttempt>();
        foreach (var journeyBooking in journey)
        {
            // Locked, not just read: a no-show correction racing this start must
            // serialize here, so the selection below cannot issue for a state that
            // no longer holds.
            var rows = await appointments.LockForBookingAsync(journeyBooking.Id, ct);
            attempts.AddRange(rows.Select(a => new RecoveryAttempt(
                a.Id, a.AppointmentTypeId, a.Status, journeyBooking.CreatedAt)));
        }

        var pendingTypes = pendingInvite?.RecoveryOfBookingId is not null
            ? pendingInvite.RequiredAppointmentTypeIds.ToList()
            : [];
        var recoverable = new RecoveryRequirementSelector().Select(
            attendee.RequiredAppointmentTypeIds, attempts, pendingTypes);
        if (recoverable.Count == 0)
            return Result<StartRecoveryOutcome>.Failure(
                Error.RecoveryNotAvailable("Nothing remains to recover."));

        var configuration = await settings.GetAsync(ct);
        var originalInvite = await invites.GetAsync(activeOriginal.InviteId, ct);
        var locationIds = new List<Guid>();
        if (originalInvite is not null) locationIds.AddRange(originalInvite.LocationIds);
        foreach (var extra in command.AdditionalLocationIds.Distinct())
        {
            var location = await locations.GetAsync(extra, ct);
            if (location is null || !location.IsActive)
                return Result<StartRecoveryOutcome>.Failure(
                    Error.Validation("Every additional location must exist and be active."));
            if (!locationIds.Contains(extra)) locationIds.Add(extra);
        }

        var fresh = await eligibility.FindEligibleEventsAsync(
            recoverable, locationIds, [], configuration.InviteOptionCount, clock.UtcNow, ct);
        if (fresh.Count < configuration.InviteOptionCount)
            return Result<StartRecoveryOutcome>.Failure(Error.InsufficientEvents(
                $"Only {fresh.Count} eligible events for {configuration.InviteOptionCount} options.",
                fresh.Count,
                configuration.InviteOptionCount));

        var issued = await issuer.IssueRecoveryAsync(attendee, activeOriginal.Id, recoverable,
            locationIds, fresh, ActorType.Staff, command.StaffUserId.ToString(), ct);
        if (issued.IsFailure) return Result<StartRecoveryOutcome>.Failure(issued.Error);

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result<StartRecoveryOutcome>.Success(new StartRecoveryOutcome(
            issued.Value.InviteId, locationIds, recoverable));
    }
}
