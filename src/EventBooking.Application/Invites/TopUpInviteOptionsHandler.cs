using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Invites;

/// <summary>Replaces one filled option on a pending invite with a fresh eligible event.</summary>
/// <param name="StaffUserId">The acting coordinator.</param>
/// <param name="InviteId">The invite identifier.</param>
/// <param name="FilledEventId">The option that just filled.</param>
/// <param name="ExcludeEventId">A further event to exclude, or null.</param>
public sealed record TopUpInviteOptionsCommand(
    Guid StaffUserId,
    Guid InviteId,
    Guid FilledEventId,
    Guid? ExcludeEventId);

/// <summary>Replaces a filled invite option while the invite stays pending.</summary>
/// <param name="invites">The invite repository.</param>
/// <param name="attendees">The attendee repository.</param>
/// <param name="eligibility">The event eligibility query.</param>
/// <param name="access">The staff access authorizer.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit logger.</param>
/// <param name="clock">The clock.</param>
public sealed class TopUpInviteOptionsHandler(
    IInviteRepository invites,
    IAttendeeRepository attendees,
    IEventEligibilityQuery eligibility,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result> HandleAsync(TopUpInviteOptionsCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId, StaffCapability.ManageAttendees, null, ct);
        if (authorized.IsFailure) return Result.Failure(authorized.Error);

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var invite = await invites.LockForUpdateAsync(command.InviteId, ct);
        if (invite is null) return Result.Failure(Error.NotFound("No such invite."));
        if (invite.Status != Domain.Invites.InviteStatus.Pending)
            return Result.Failure(Error.Conflict("Only a pending invite can be topped up."));

        if (invite.Offers(command.FilledEventId))
            invite.RemoveOption(command.FilledEventId);

        // The filled event stays excluded even though it is no longer offered: the fresh
        // query must never hand back the option being replaced.
        var exclude = invite.Options.Select(o => o.EventId)
            .Append(command.FilledEventId)
            .Concat(command.ExcludeEventId is { } extra ? [extra] : [])
            .ToList();
        var fresh = await eligibility.FindEligibleEventsAsync(
            invite.RequiredAppointmentTypeIds, invite.LocationIds, exclude, 1, clock.UtcNow, ct);
        if (fresh.Count == 1)
        {
            invite.AddOption(fresh[0]);
            audit.Record(AuditEntityTypes.Invite, invite.Id, AuditAction.InviteOptionReplaced,
                ActorType.Staff, command.StaffUserId.ToString(),
                $"{command.FilledEventId} replaced by {fresh[0]}");
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result.Success();
        }

        // The attendee is flagged only from Invited: any other status already says what
        // needs saying, and the transition table refuses most moves from elsewhere.
        var attendee = await attendees.LockForUpdateAsync(invite.AttendeeId, ct);
        if (attendee is null) return Result.Failure(Error.NotFound("No such attendee."));
        if (attendee.Status == AttendeeStatus.Invited)
            attendee.MarkNoResponse(clock.UtcNow);
        audit.Record(AuditEntityTypes.Invite, invite.Id, AuditAction.InviteOptionReplaced,
            ActorType.Staff, command.StaffUserId.ToString(),
            $"{command.FilledEventId} dropped, no replacement available");
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result.Success();
    }
}
