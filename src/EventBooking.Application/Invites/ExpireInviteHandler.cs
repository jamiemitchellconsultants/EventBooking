using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Invites;

/// <summary>Expires one due invite, reissuing it while retries remain.</summary>
/// <param name="InviteId">The invite identifier.</param>
public sealed record ExpireInviteCommand(Guid InviteId);

/// <summary>Expires one invite and reissues it from its own snapshot while retries remain.</summary>
/// <param name="invites">The invite repository.</param>
/// <param name="attendees">The attendee repository.</param>
/// <param name="eligibility">The event eligibility query.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit logger.</param>
/// <param name="clock">The clock.</param>
/// <param name="issuer">The invite issuer.</param>
public sealed class ExpireInviteHandler(
    IInviteRepository invites,
    IAttendeeRepository attendees,
    IEventEligibilityQuery eligibility,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock,
    IInviteIssuer issuer)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result> HandleAsync(ExpireInviteCommand command, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        // Port read first: the attendee lock must precede the invite lock in canonical
        // order. Everything is re-validated after locking.
        var port = await invites.GetAsync(command.InviteId, ct);
        if (port is null) return Result.Failure(Error.NotFound("No such invite."));

        var attendee = await attendees.LockForUpdateAsync(port.AttendeeId, ct);
        if (attendee is null) return Result.Failure(Error.NotFound("No such attendee."));

        var invite = await invites.LockForUpdateAsync(command.InviteId, ct);
        if (invite is null) return Result.Failure(Error.NotFound("No such invite."));
        if (invite.Status != Domain.Invites.InviteStatus.Pending || invite.IsUsableAt(clock.UtcNow))
        {
            await transaction.CommitAsync(ct);
            return Result.Success();
        }

        // Recovery invites expire without reissue and without moving the attendee: the
        // attendee already travelled once, and the recovery flow owns what happens next.
        if (invite.RecoveryOfBookingId.HasValue)
        {
            invite.MarkExpired();
            audit.Record(AuditEntityTypes.Invite, invite.Id, AuditAction.InviteExpired,
                ActorType.System, null, $"retry {invite.RetryCount}");
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result.Success();
        }

        // A booked attendee's pending invite is left alone, exactly as the sweep always
        // has: moving them to follow-up would contradict the booking they hold.
        if (attendee.Status == AttendeeStatus.Booked)
        {
            await transaction.CommitAsync(ct);
            return Result.Success();
        }

        invite.MarkExpired();

        // The retry limit and the reissue width come from the invite's own snapshot, not
        // from current settings: a later settings change never alters an outstanding
        // invite, and the reissue must offer exactly the count its snapshot bounds.
        if (invite.RetryCount < invite.MaxAutoRetryCount)
        {
            var fresh = await eligibility.FindEligibleEventsAsync(
                invite.RequiredAppointmentTypeIds, invite.LocationIds,
                invite.Options.Select(o => o.EventId).ToList(), invite.InviteOptionCount,
                clock.UtcNow, ct);
            if (fresh.Count >= invite.InviteOptionCount)
            {
                var reissued = await issuer.IssueReissueAsync(
                    invite, fresh.Take(invite.InviteOptionCount).ToList(),
                    ActorType.System, string.Empty, ct);
                attendee.MarkInvited(clock.UtcNow);
                audit.Record(AuditEntityTypes.Invite, invite.Id, AuditAction.InviteExpired,
                    ActorType.System, null, $"reissued as {reissued.Value.InviteId}");
                await unitOfWork.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Result.Success();
            }
        }

        attendee.MarkNoResponse(clock.UtcNow);
        audit.Record(AuditEntityTypes.Invite, invite.Id, AuditAction.InviteExpired,
            ActorType.System, null,
            invite.RetryCount >= invite.MaxAutoRetryCount
                ? $"retry limit {invite.MaxAutoRetryCount} reached"
                : "reissue failed: insufficient eligible events");
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result.Success();
    }
}
