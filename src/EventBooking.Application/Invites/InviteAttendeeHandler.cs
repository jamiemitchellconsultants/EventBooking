using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Invites;

/// <summary>Invites an attendee to events at the chosen locations, or at every active location.</summary>
/// <param name="StaffUserId">The inviting coordinator.</param>
/// <param name="AttendeeId">The attendee to invite.</param>
/// <param name="LocationIds">
/// The locations the Coordinator opened for this invite. Empty means every active location, so the
/// one-click invite offers events wherever they are; a Coordinator narrows it by choosing.
/// </param>
public sealed record InviteAttendeeCommand(
    Guid StaffUserId,
    Guid AttendeeId,
    IReadOnlyList<Guid> LocationIds);

/// <summary>The invite that was issued.</summary>
/// <param name="InviteId">The new invite identifier.</param>
/// <param name="Status">The attendee status after inviting.</param>
public sealed record InviteAttendeeOutcome(Guid? InviteId, string Status);

/// <summary>Issues an invite at explicit locations and stages its email for sending.</summary>
/// <param name="attendees">The attendee repository.</param>
/// <param name="locations">The location repository.</param>
/// <param name="access">The staff access authorizer.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="clock">The clock.</param>
/// <param name="issuer">The invite issuer.</param>
public sealed class InviteAttendeeHandler(
    IAttendeeRepository attendees,
    ILocationRepository locations,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IClock clock,
    IInviteIssuer issuer)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<InviteAttendeeOutcome>> HandleAsync(
        InviteAttendeeCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId, StaffCapability.ManageAttendees, null, ct);
        if (authorized.IsFailure) return Result<InviteAttendeeOutcome>.Failure(authorized.Error);

        var known = await locations.ListAsync(ct);
        IReadOnlyList<Guid> locationIds;
        if (command.LocationIds.Count == 0)
        {
            locationIds = known.Where(l => l.IsActive).Select(l => l.Id).ToList();
            if (locationIds.Count == 0)
                return Result<InviteAttendeeOutcome>.Failure(
                    Error.Validation("There is no active location to invite for."));
            if (locationIds.Count > Domain.Invites.Invite.MaximumLocationCount)
                return Result<InviteAttendeeOutcome>.Failure(Error.Validation(
                    $"More than {Domain.Invites.Invite.MaximumLocationCount} locations are active; choose which to invite for."));
        }
        else
        {
            locationIds = command.LocationIds.Distinct().ToList();
            var chosen = known.Where(l => locationIds.Contains(l.Id)).ToList();
            if (chosen.Count != locationIds.Count)
                return Result<InviteAttendeeOutcome>.Failure(Error.Validation("Unknown location."));
            var inactive = chosen.FirstOrDefault(l => !l.IsActive);
            if (inactive is not null)
                return Result<InviteAttendeeOutcome>.Failure(
                    Error.Validation($"Location {inactive.Code} is not active."));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var attendee = await attendees.LockForUpdateAsync(command.AttendeeId, ct);
        if (attendee is null)
            return Result<InviteAttendeeOutcome>.Failure(Error.NotFound("No such attendee."));
        // Every non-terminal status may trigger, including Invited: a Coordinator
        // replacement supersedes the usable pending invite. Only a booked attendee,
        // whose journey already concluded, is refused.
        if (attendee.Status is not AttendeeStatus.NotYetInvited
            and not AttendeeStatus.AwaitingAvailability
            and not AttendeeStatus.Invited
            and not AttendeeStatus.NoResponseNeedsFollowUp)
            return Result<InviteAttendeeOutcome>.Failure(
                Error.Conflict($"A {attendee.Status} attendee cannot be invited."));

        var issued = await issuer.IssueInitialAsync(
            attendee, locationIds, Domain.Notifications.EmailTemplate.AttendeeInvite,
            ActorType.Staff, command.StaffUserId.ToString(), ct);
        if (issued.IsFailure)
        {
            if (attendee.Status != AttendeeStatus.AwaitingAvailability)
            {
                if (Attendee.IsLegalTransition(attendee.Status, AttendeeStatus.AwaitingAvailability))
                    attendee.MarkAwaitingAvailability(clock.UtcNow);
                else
                    attendee.MarkNoResponse(clock.UtcNow);
            }

            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result<InviteAttendeeOutcome>.Failure(issued.Error);
        }

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result<InviteAttendeeOutcome>.Success(
            new InviteAttendeeOutcome(issued.Value.InviteId, attendee.Status.ToString()));
    }
}
