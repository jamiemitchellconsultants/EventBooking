using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Invites;

/// <summary>The invite the issuer created.</summary>
/// <param name="InviteId">The new invite identifier.</param>
public sealed record InviteIssueOutcome(Guid InviteId);

/// <summary>Creates invites for the Coordinator trigger, the expiry sweep and recovery paths.</summary>
public interface IInviteIssuer
{
    /// <summary>Issues an initial invite for the attendee's own requirement snapshot.</summary>
    /// <param name="attendee">The locked attendee.</param>
    /// <param name="locationIds">The locations the Coordinator opened.</param>
    /// <param name="template">The email template to stage.</param>
    /// <param name="actor">The actor type.</param>
    /// <param name="actorId">The actor identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="excludeEventIds">Events that must not be offered, such as one being cancelled in the same unsaved transaction.</param>
    /// <param name="lockedPending">The attendee's pending invites the caller has already locked, or null to have the issuer lock and supersede the pending invite. A caller past the Invite level of the lock ladder must pass them, because taking an Invite lock there is a violation.</param>
    Task<Result<InviteIssueOutcome>> IssueInitialAsync(
        Attendee attendee,
        IReadOnlyList<Guid> locationIds,
        EmailTemplate template,
        ActorType actor,
        string actorId,
        CancellationToken ct,
        IReadOnlyCollection<Guid>? excludeEventIds = null,
        IReadOnlyCollection<Invite>? lockedPending = null);

    /// <summary>Reissues an expired invite with the same locations and retry count plus one.</summary>
    /// <param name="expired">The expired invite being replaced.</param>
    /// <param name="freshEventIds">The freshly chosen event options.</param>
    /// <param name="actor">The actor type.</param>
    /// <param name="actorId">The actor identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<Result<InviteIssueOutcome>> IssueReissueAsync(
        Invite expired,
        IReadOnlyList<Guid> freshEventIds,
        ActorType actor,
        string actorId,
        CancellationToken ct);

    /// <summary>Issues a recovery invite for already-selected no-show types.</summary>
    /// <param name="attendee">The locked attendee.</param>
    /// <param name="rootBookingId">The booking being recovered.</param>
    /// <param name="selectedAppointmentTypeIds">The selected no-show types.</param>
    /// <param name="locationIds">The locations the Coordinator opened.</param>
    /// <param name="freshEventIds">The freshly chosen event options.</param>
    /// <param name="actor">The actor type.</param>
    /// <param name="actorId">The actor identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<Result<InviteIssueOutcome>> IssueRecoveryAsync(
        Attendee attendee,
        Guid rootBookingId,
        IReadOnlyList<Guid> selectedAppointmentTypeIds,
        IReadOnlyList<Guid> locationIds,
        IReadOnlyList<Guid> freshEventIds,
        ActorType actor,
        string actorId,
        CancellationToken ct);
}

/// <summary>Creates invites against live eligibility and stages their emails for sending.</summary>
/// <param name="invites">The invite repository.</param>
/// <param name="settings">The system settings repository.</param>
/// <param name="emails">The email delivery repository.</param>
/// <param name="audit">The audit logger.</param>
/// <param name="clock">The clock.</param>
/// <param name="eligibility">The event eligibility query.</param>
public sealed class InviteIssuer(
    IInviteRepository invites,
    ISystemSettingsRepository settings,
    IEmailDeliveryRepository emails,
    IAuditLogger audit,
    IClock clock,
    IEventEligibilityQuery eligibility) : IInviteIssuer
{
    /// <summary>Issues an initial invite for the attendee's own requirement snapshot.</summary>
    /// <param name="attendee">The locked attendee.</param>
    /// <param name="locationIds">The locations the Coordinator opened.</param>
    /// <param name="template">The email template to stage.</param>
    /// <param name="actor">The actor type.</param>
    /// <param name="actorId">The actor identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="excludeEventIds">Events that must not be offered.</param>
    /// <param name="lockedPending">Pending invites the caller already holds locks on, if any.</param>
    public async Task<Result<InviteIssueOutcome>> IssueInitialAsync(
        Attendee attendee,
        IReadOnlyList<Guid> locationIds,
        EmailTemplate template,
        ActorType actor,
        string actorId,
        CancellationToken ct,
        IReadOnlyCollection<Guid>? excludeEventIds = null,
        IReadOnlyCollection<Invite>? lockedPending = null)
    {
        var configuration = await settings.GetAsync(ct);
        var found = await eligibility.FindEligibleEventsAsync(
            attendee.RequiredAppointmentTypeIds, locationIds, excludeEventIds ?? [],
            configuration.InviteOptionCount, clock.UtcNow, ct);
        if (found.Count < configuration.InviteOptionCount)
            return Result<InviteIssueOutcome>.Failure(Error.InsufficientEvents(
                $"Only {found.Count} eligible events for {configuration.InviteOptionCount} options.",
                found.Count, configuration.InviteOptionCount));

        if (lockedPending is null)
        {
            (await invites.LockPendingForAttendeeAsync(attendee.Id, ct))?.MarkSuperseded();
        }
        else
        {
            foreach (var pending in lockedPending.Where(i => i.Status == InviteStatus.Pending))
                pending.MarkSuperseded();
        }

        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, clock.UtcNow.AddDays(configuration.InviteExpiryDays),
            locationIds, found, attendee.RequiredAppointmentTypeIds, 0,
            configuration.InviteExpiryDays, configuration.MaxAutoRetryCount,
            configuration.InviteOptionCount);
        invites.Add(invite);
        emails.Add(EmailLog.RecordPending(
            Guid.NewGuid(), attendee.Id, template, clock.UtcNow, inviteId: invite.Id));
        audit.Record(AuditEntityTypes.Invite, invite.Id, AuditAction.InviteCreated,
            actor, actorId, $"locations {locationIds.Count}");
        attendee.MarkInvited(clock.UtcNow);
        return Result<InviteIssueOutcome>.Success(new InviteIssueOutcome(invite.Id));
    }

    /// <summary>Reissues an expired invite with the same locations and retry count plus one.</summary>
    /// <param name="expired">The expired invite being replaced.</param>
    /// <param name="freshEventIds">The freshly chosen event options.</param>
    /// <param name="actor">The actor type.</param>
    /// <param name="actorId">The actor identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<InviteIssueOutcome>> IssueReissueAsync(
        Invite expired,
        IReadOnlyList<Guid> freshEventIds,
        ActorType actor,
        string actorId,
        CancellationToken ct)
    {
        var invite = Invite.Reissue(Guid.NewGuid(), expired,
            clock.UtcNow.AddDays(expired.InviteExpiryDays), freshEventIds);
        invites.Add(invite);
        emails.Add(EmailLog.RecordPending(
            Guid.NewGuid(), invite.AttendeeId, EmailTemplate.AttendeeReinvite, clock.UtcNow,
            inviteId: invite.Id));
        audit.Record(AuditEntityTypes.Invite, invite.Id, AuditAction.InviteCreated,
            actor, actorId, $"locations {invite.LocationIds.Count}");
        return Result<InviteIssueOutcome>.Success(new InviteIssueOutcome(invite.Id));
    }

    /// <summary>Issues a recovery invite for already-selected no-show types.</summary>
    /// <param name="attendee">The locked attendee.</param>
    /// <param name="rootBookingId">The booking being recovered.</param>
    /// <param name="selectedAppointmentTypeIds">The selected no-show types.</param>
    /// <param name="locationIds">The locations the Coordinator opened.</param>
    /// <param name="freshEventIds">The freshly chosen event options.</param>
    /// <param name="actor">The actor type.</param>
    /// <param name="actorId">The actor identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<InviteIssueOutcome>> IssueRecoveryAsync(
        Attendee attendee,
        Guid rootBookingId,
        IReadOnlyList<Guid> selectedAppointmentTypeIds,
        IReadOnlyList<Guid> locationIds,
        IReadOnlyList<Guid> freshEventIds,
        ActorType actor,
        string actorId,
        CancellationToken ct)
    {
        Guard.Against(locationIds.Count == 0, "At least one location is required.");

        var configuration = await settings.GetAsync(ct);
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(), attendee.Id, rootBookingId,
            clock.UtcNow.AddDays(configuration.InviteExpiryDays),
            locationIds[0], locationIds.Skip(1).ToList(), freshEventIds,
            selectedAppointmentTypeIds,
            configuration.InviteExpiryDays, configuration.MaxAutoRetryCount,
            configuration.InviteOptionCount);
        invites.Add(invite);
        emails.Add(EmailLog.RecordPending(
            Guid.NewGuid(), attendee.Id, EmailTemplate.AttendeeInvite, clock.UtcNow,
            inviteId: invite.Id));
        audit.Record(AuditEntityTypes.Invite, invite.Id, AuditAction.RecoveryInviteCreated,
            actor, actorId, $"locations {locationIds.Count}");
        return Result<InviteIssueOutcome>.Success(new InviteIssueOutcome(invite.Id));
    }
}
