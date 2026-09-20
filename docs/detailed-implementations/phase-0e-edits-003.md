# 00e — Require an attendee group, edits 3 (Task 3c)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — src/EventBooking.Application/Invites/InviteIssuer.cs — 1/1

<!-- retirement-file: {"id":8,"file":"src/EventBooking.Application/Invites/InviteIssuer.cs","beforeSha":"6021fcde351a824e150276dbc1ae8a1dc92f28bee8879e5d2069a3eb73c4c3f3","afterSha":"5f3d4645294b0026ef28705a46116e39e7a07f9fb618d1a9210edb465a14776d","side":"before","part":1,"parts":1} -->

`````csharp
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Invites;

/// <summary>Reports invite creation and the durable delivery state visible to callers.</summary>
/// <param name="Invited">Whether a new pending invite was created.</param>
/// <param name="InviteId">The new invite identifier, when one was created.</param>
/// <param name="EmailSent">Whether the post-commit provider attempt completed successfully.</param>
/// <param name="DeliveryStatus">The durable delivery status, or <c>Unavailable</c> when no invite exists.</param>
/// <param name="DeliveryId">The durable delivery identifier, when one was staged.</param>
public sealed record InviteIssueResult(
    bool Invited,
    Guid? InviteId,
    bool EmailSent,
    string DeliveryStatus = "Unavailable",
    Guid? DeliveryId = null)
{
    internal EmailDispatchPlan? DispatchPlan { get; init; }
}

internal sealed record EmailDispatchPlan(Guid DeliveryId, EmailMessage Message, Action? OnSent = null);

/// <summary>
/// Creates one invite and stages its delivery, or records that there is nothing to offer. Shared by the
/// coordinator trigger (Task 37), the expiry sweep (Task 38) and event cancellation (Task 42).
/// Never saves or calls a provider — the caller owns the unit of work and dispatches only after commit.
/// </summary>
/// <param name="invites">The invites.</param>
/// <param name="groups">The groups.</param>
/// <param name="eventFinder">The event finder.</param>
/// <param name="settings">The settings.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="deliveries">The deliveries.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
/// <param name="portal">The portal.</param>
public sealed class InviteIssuer(
    IInviteRepository invites,
    IAttendeeGroupRepository groups,
    EligibleEventFinder eventFinder,
    ISystemSettingsRepository settings,
    ITokenService tokens,
    EmailDeliveryService deliveries,
    IAuditLogger audit,
    IClock clock,
    AttendeePortalOptions portal)
{
    /// <summary>Issues an initial Invite from a locked, validated Attendee requirement set.</summary>
    /// <param name="attendee">The attendee whose lifecycle is already locked by the caller.</param>
    /// <param name="retryCount">The automated retry number to persist on the new invite.</param>
    /// <param name="actorType">The actor recorded for invite creation.</param>
    /// <param name="actorId">The actor identifier, when a staff identity caused the change.</param>
    /// <param name="isReinvite">Whether the reminder template should be used.</param>
    /// <param name="cancellationToken">Cancels repository and event reads.</param>
    /// <returns>A pending delivery plan the caller dispatches after commit.</returns>
    public async Task<Result<InviteIssueResult>> IssueInitialAsync(
        Attendee attendee,
        int retryCount,
        ActorType actorType,
        string? actorId,
        bool isReinvite,
        CancellationToken cancellationToken)
    {
        if (!attendee.AttendeeGroupId.HasValue)
        {
            return Result<InviteIssueResult>.Failure(Error.AttendeeReconciliationRequired(
                "Assign an attendee group before issuing an invite."));
        }

        var group = await groups.GetAsync(attendee.AttendeeGroupId.Value, cancellationToken);
        var mapping = group?.RequiredAppointmentTypeIds
            .Order()
            .ToList();
        var current = attendee.RequiredAppointmentTypeIds
            .Order()
            .ToList();
        if (group is null || !group.IsActive || mapping!.Count == 0 || !mapping.SequenceEqual(current))
        {
            return Result<InviteIssueResult>.Failure(Error.AttendeeRequirementSnapshotMismatch(
                "The attendee requirements do not match their attendee group."));
        }

        var pending = await invites.GetPendingForAttendeeAsync(attendee.Id, cancellationToken);
        if (pending?.Status == Domain.Invites.InviteStatus.Pending)
        {
            pending.MarkSuperseded();
        }

        InviteIssueResult issued;
        try
        {
            var options = await eventFinder.FindAsync(
                mapping,
                Invite.RequiredOptionCount,
                [],
                cancellationToken);

            if (options.Count < Invite.RequiredOptionCount)
            {
                attendee.MarkAwaitingAvailability();
                return Result<InviteIssueResult>.Success(new InviteIssueResult(false, null, false));
            }

            var configuration = await settings.GetAsync(cancellationToken);

            var inviteId = Guid.NewGuid();
            var token = tokens.Issue(inviteId);

            var invite = Invite.CreateInitial(
                inviteId,
                attendee.Id,
                token.TokenHash,
                clock.UtcNow.AddDays(configuration.InviteExpiryDays),
                options.Select(o => o.Id),
                mapping,
                retryCount);

            invites.Add(invite);
            attendee.MarkInvited();

            audit.Record(
                AuditEntityTypes.Invite,
                inviteId,
                AuditAction.InviteCreated,
                actorType,
                actorId,
                $"retry {retryCount}");

            var message = AttendeeEmailComposer.Invite(
                attendee,
                mapping,
                options,
                $"{portal.BaseUrl}/book/{token.Token}",
                isReinvite,
                isRecovery: false);

            var delivery = deliveries.StagePending(attendee.Id, message.Template, inviteId: inviteId);
            deliveries.ClaimForDispatch(delivery);
            var plan = new EmailDispatchPlan(
                delivery.Id,
                message,
                () => audit.Record(
                    AuditEntityTypes.Invite,
                    inviteId,
                    AuditAction.InviteSent,
                    actorType,
                    actorId,
                    $"invite {inviteId}"));

            issued = new InviteIssueResult(true, inviteId, false, EmailStatus.Pending.ToString(), delivery.Id)
            {
                DispatchPlan = plan,
            };
        }
        catch (DomainException ex)
        {
            return Result<InviteIssueResult>.Failure(Error.Validation(ex.Message));
        }

        return Result<InviteIssueResult>.Success(issued);
    }

    /// <summary>Issues a recovery Invite for already-selected no-show types from locked journey state.</summary>
    /// <param name="attendee">The attendee whose lifecycle is already locked by the caller.</param>
    /// <param name="rootBookingId">The original journey-root Booking the recovery belongs to.</param>
    /// <param name="selectedTypeIds">The recoverable snapshot, already revalidated under lock.</param>
    /// <param name="options">Exactly three future events with capacity for every selected type.</param>
    /// <param name="actorType">The actor recorded for invite creation.</param>
    /// <param name="actorId">The actor identifier, when a staff identity caused the change.</param>
    /// <param name="cancellationToken">Cancels repository reads.</param>
    /// <returns>A pending delivery plan the caller dispatches after commit.</returns>
    public async Task<Result<InviteIssueResult>> IssueRecoveryAsync(
        Attendee attendee,
        Guid rootBookingId,
        IReadOnlyList<Guid> selectedTypeIds,
        IReadOnlyList<Event> options,
        ActorType actorType,
        string? actorId,
        CancellationToken cancellationToken)
    {
        if (options.Count != Invite.RequiredOptionCount)
        {
            return Result<InviteIssueResult>.Failure(Error.Validation(
                "A recovery invite must offer exactly three event options."));
        }

        InviteIssueResult issued;
        try
        {
            var configuration = await settings.GetAsync(cancellationToken);

            var inviteId = Guid.NewGuid();
            var token = tokens.Issue(inviteId);

            var invite = Invite.CreateRecovery(
                inviteId,
                attendee.Id,
                rootBookingId,
                token.TokenHash,
                clock.UtcNow.AddDays(configuration.InviteExpiryDays),
                options.Select(o => o.Id),
                selectedTypeIds);

            invites.Add(invite);

            var codes = selectedTypeIds
                .Select(AppointmentTypeIds.CodeOf)
                .Order(StringComparer.Ordinal)
                .ToList();
            audit.Record(
                AuditEntityTypes.Invite,
                inviteId,
                AuditAction.RecoveryInviteCreated,
                actorType,
                actorId,
                JsonSerializer.Serialize(
                    new RecoveryInviteAudit(rootBookingId, codes),
                    RecoveryAuditJson));

            var message = AttendeeEmailComposer.Invite(
                attendee,
                selectedTypeIds,
                options,
                $"{portal.BaseUrl}/book/{token.Token}",
                isReinvite: false,
                isRecovery: true);

            var delivery = deliveries.StagePending(attendee.Id, message.Template, inviteId: inviteId);
            deliveries.ClaimForDispatch(delivery);
            var plan = new EmailDispatchPlan(
                delivery.Id,
                message,
                () => audit.Record(
                    AuditEntityTypes.Invite,
                    inviteId,
                    AuditAction.InviteSent,
                    actorType,
                    actorId,
                    $"invite {inviteId}"));

            issued = new InviteIssueResult(true, inviteId, false, EmailStatus.Pending.ToString(), delivery.Id)
            {
                DispatchPlan = plan,
            };
        }
        catch (DomainException ex)
        {
            return Result<InviteIssueResult>.Failure(Error.Validation(ex.Message));
        }

        return Result<InviteIssueResult>.Success(issued);
    }

    private static readonly JsonSerializerOptions RecoveryAuditJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed record RecoveryInviteAudit(Guid RootBookingId, IReadOnlyList<string> RequirementCodes);
}
`````

## after — src/EventBooking.Application/Invites/InviteIssuer.cs — 1/1

<!-- retirement-file: {"id":8,"file":"src/EventBooking.Application/Invites/InviteIssuer.cs","beforeSha":"6021fcde351a824e150276dbc1ae8a1dc92f28bee8879e5d2069a3eb73c4c3f3","afterSha":"5f3d4645294b0026ef28705a46116e39e7a07f9fb618d1a9210edb465a14776d","side":"after","part":1,"parts":1} -->

`````csharp
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Invites;

/// <summary>Reports invite creation and the durable delivery state visible to callers.</summary>
/// <param name="Invited">Whether a new pending invite was created.</param>
/// <param name="InviteId">The new invite identifier, when one was created.</param>
/// <param name="EmailSent">Whether the post-commit provider attempt completed successfully.</param>
/// <param name="DeliveryStatus">The durable delivery status, or <c>Unavailable</c> when no invite exists.</param>
/// <param name="DeliveryId">The durable delivery identifier, when one was staged.</param>
public sealed record InviteIssueResult(
    bool Invited,
    Guid? InviteId,
    bool EmailSent,
    string DeliveryStatus = "Unavailable",
    Guid? DeliveryId = null)
{
    internal EmailDispatchPlan? DispatchPlan { get; init; }
}

internal sealed record EmailDispatchPlan(Guid DeliveryId, EmailMessage Message, Action? OnSent = null);

/// <summary>
/// Creates one invite and stages its delivery, or records that there is nothing to offer. Shared by the
/// coordinator trigger (Task 37), the expiry sweep (Task 38) and event cancellation (Task 42).
/// Never saves or calls a provider — the caller owns the unit of work and dispatches only after commit.
/// </summary>
/// <param name="invites">The invites.</param>
/// <param name="groups">The groups.</param>
/// <param name="eventFinder">The event finder.</param>
/// <param name="settings">The settings.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="deliveries">The deliveries.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
/// <param name="portal">The portal.</param>
public sealed class InviteIssuer(
    IInviteRepository invites,
    IAttendeeGroupRepository groups,
    EligibleEventFinder eventFinder,
    ISystemSettingsRepository settings,
    ITokenService tokens,
    EmailDeliveryService deliveries,
    IAuditLogger audit,
    IClock clock,
    AttendeePortalOptions portal)
{
    /// <summary>Issues an initial Invite from a locked, validated Attendee requirement set.</summary>
    /// <param name="attendee">The attendee whose lifecycle is already locked by the caller.</param>
    /// <param name="retryCount">The automated retry number to persist on the new invite.</param>
    /// <param name="actorType">The actor recorded for invite creation.</param>
    /// <param name="actorId">The actor identifier, when a staff identity caused the change.</param>
    /// <param name="isReinvite">Whether the reminder template should be used.</param>
    /// <param name="cancellationToken">Cancels repository and event reads.</param>
    /// <returns>A pending delivery plan the caller dispatches after commit.</returns>
    public async Task<Result<InviteIssueResult>> IssueInitialAsync(
        Attendee attendee,
        int retryCount,
        ActorType actorType,
        string? actorId,
        bool isReinvite,
        CancellationToken cancellationToken)
    {
        var group = await groups.GetAsync(attendee.AttendeeGroupId, cancellationToken);
        var mapping = group?.RequiredAppointmentTypeIds
            .Order()
            .ToList();
        var current = attendee.RequiredAppointmentTypeIds
            .Order()
            .ToList();
        if (group is null || !group.IsActive || mapping!.Count == 0 || !mapping.SequenceEqual(current))
        {
            return Result<InviteIssueResult>.Failure(Error.AttendeeRequirementSnapshotMismatch(
                "The attendee requirements do not match their attendee group."));
        }

        var pending = await invites.GetPendingForAttendeeAsync(attendee.Id, cancellationToken);
        if (pending?.Status == Domain.Invites.InviteStatus.Pending)
        {
            pending.MarkSuperseded();
        }

        InviteIssueResult issued;
        try
        {
            var options = await eventFinder.FindAsync(
                mapping,
                Invite.RequiredOptionCount,
                [],
                cancellationToken);

            if (options.Count < Invite.RequiredOptionCount)
            {
                attendee.MarkAwaitingAvailability();
                return Result<InviteIssueResult>.Success(new InviteIssueResult(false, null, false));
            }

            var configuration = await settings.GetAsync(cancellationToken);

            var inviteId = Guid.NewGuid();
            var token = tokens.Issue(inviteId);

            var invite = Invite.CreateInitial(
                inviteId,
                attendee.Id,
                token.TokenHash,
                clock.UtcNow.AddDays(configuration.InviteExpiryDays),
                options.Select(o => o.Id),
                mapping,
                retryCount);

            invites.Add(invite);
            attendee.MarkInvited();

            audit.Record(
                AuditEntityTypes.Invite,
                inviteId,
                AuditAction.InviteCreated,
                actorType,
                actorId,
                $"retry {retryCount}");

            var message = AttendeeEmailComposer.Invite(
                attendee,
                mapping,
                options,
                $"{portal.BaseUrl}/book/{token.Token}",
                isReinvite,
                isRecovery: false);

            var delivery = deliveries.StagePending(attendee.Id, message.Template, inviteId: inviteId);
            deliveries.ClaimForDispatch(delivery);
            var plan = new EmailDispatchPlan(
                delivery.Id,
                message,
                () => audit.Record(
                    AuditEntityTypes.Invite,
                    inviteId,
                    AuditAction.InviteSent,
                    actorType,
                    actorId,
                    $"invite {inviteId}"));

            issued = new InviteIssueResult(true, inviteId, false, EmailStatus.Pending.ToString(), delivery.Id)
            {
                DispatchPlan = plan,
            };
        }
        catch (DomainException ex)
        {
            return Result<InviteIssueResult>.Failure(Error.Validation(ex.Message));
        }

        return Result<InviteIssueResult>.Success(issued);
    }

    /// <summary>Issues a recovery Invite for already-selected no-show types from locked journey state.</summary>
    /// <param name="attendee">The attendee whose lifecycle is already locked by the caller.</param>
    /// <param name="rootBookingId">The original journey-root Booking the recovery belongs to.</param>
    /// <param name="selectedTypeIds">The recoverable snapshot, already revalidated under lock.</param>
    /// <param name="options">Exactly three future events with capacity for every selected type.</param>
    /// <param name="actorType">The actor recorded for invite creation.</param>
    /// <param name="actorId">The actor identifier, when a staff identity caused the change.</param>
    /// <param name="cancellationToken">Cancels repository reads.</param>
    /// <returns>A pending delivery plan the caller dispatches after commit.</returns>
    public async Task<Result<InviteIssueResult>> IssueRecoveryAsync(
        Attendee attendee,
        Guid rootBookingId,
        IReadOnlyList<Guid> selectedTypeIds,
        IReadOnlyList<Event> options,
        ActorType actorType,
        string? actorId,
        CancellationToken cancellationToken)
    {
        if (options.Count != Invite.RequiredOptionCount)
        {
            return Result<InviteIssueResult>.Failure(Error.Validation(
                "A recovery invite must offer exactly three event options."));
        }

        InviteIssueResult issued;
        try
        {
            var configuration = await settings.GetAsync(cancellationToken);

            var inviteId = Guid.NewGuid();
            var token = tokens.Issue(inviteId);

            var invite = Invite.CreateRecovery(
                inviteId,
                attendee.Id,
                rootBookingId,
                token.TokenHash,
                clock.UtcNow.AddDays(configuration.InviteExpiryDays),
                options.Select(o => o.Id),
                selectedTypeIds);

            invites.Add(invite);

            var codes = selectedTypeIds
                .Select(AppointmentTypeIds.CodeOf)
                .Order(StringComparer.Ordinal)
                .ToList();
            audit.Record(
                AuditEntityTypes.Invite,
                inviteId,
                AuditAction.RecoveryInviteCreated,
                actorType,
                actorId,
                JsonSerializer.Serialize(
                    new RecoveryInviteAudit(rootBookingId, codes),
                    RecoveryAuditJson));

            var message = AttendeeEmailComposer.Invite(
                attendee,
                selectedTypeIds,
                options,
                $"{portal.BaseUrl}/book/{token.Token}",
                isReinvite: false,
                isRecovery: true);

            var delivery = deliveries.StagePending(attendee.Id, message.Template, inviteId: inviteId);
            deliveries.ClaimForDispatch(delivery);
            var plan = new EmailDispatchPlan(
                delivery.Id,
                message,
                () => audit.Record(
                    AuditEntityTypes.Invite,
                    inviteId,
                    AuditAction.InviteSent,
                    actorType,
                    actorId,
                    $"invite {inviteId}"));

            issued = new InviteIssueResult(true, inviteId, false, EmailStatus.Pending.ToString(), delivery.Id)
            {
                DispatchPlan = plan,
            };
        }
        catch (DomainException ex)
        {
            return Result<InviteIssueResult>.Failure(Error.Validation(ex.Message));
        }

        return Result<InviteIssueResult>.Success(issued);
    }

    private static readonly JsonSerializerOptions RecoveryAuditJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed record RecoveryInviteAudit(Guid RootBookingId, IReadOnlyList<string> RequirementCodes);
}
`````

## before — src/EventBooking.Domain/Attendees/Attendee.cs — 1/1

<!-- retirement-file: {"id":9,"file":"src/EventBooking.Domain/Attendees/Attendee.cs","beforeSha":"43e60c2c07ec61c07a019cfb4ad03c21f73c0b1a53f5139c030a60bdc14c5a5c","afterSha":"dcda95b3b8c5124cc674a93b7a44d170c4c3c80ada4c59ca46c1a725c565ca41","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net.Mail;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Domain.Attendees;

/// <summary>A person invited to attend appointments, whose requirements derive from one attendee group.</summary>
public sealed class Attendee
{
    private readonly List<AttendeeRequirement> _requirements = [];

    private Attendee()
    {
        // Required by the persistence layer's constructor binding.
        Name = string.Empty;
        Email = string.Empty;
    }

    /// <summary>Gets the attendee identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the attendee display name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets the normalized attendee email address.</summary>
    public string Email { get; private set; }

    /// <summary>Gets the assigned Attendee Group, or null during legacy reconciliation.</summary>
    public Guid? AttendeeGroupId { get; private set; }

    /// <summary>Gets where the attendee sits in the invite and booking lifecycle.</summary>
    public AttendeeStatus Status { get; private set; } = AttendeeStatus.NotYetInvited;

    /// <summary>Gets the materialized appointment types the attendee currently requires.</summary>
    public IReadOnlyList<AttendeeRequirement> Requirements => _requirements;

    /// <summary>Gets the identifiers of the appointment types the attendee currently requires.</summary>
    public IReadOnlyList<Guid> RequiredAppointmentTypeIds =>
        _requirements.Select(r => r.AppointmentTypeId).ToList();

    /// <summary>Creates a Attendee and derives every requirement from the active mapped group.</summary>
    /// <param name="id">The id.</param>
    /// <param name="name">The name.</param>
    /// <param name="email">The email.</param>
    /// <param name="attendeeGroup">The attendee group.</param>
    public static Attendee Create(Guid id, string? name, string? email, AttendeeGroup attendeeGroup)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");

        var attendee = new Attendee
        {
            Id = id,
            Name = Guard.NotBlank(name, "name"),
            Email = NormaliseEmail(email),
            Status = AttendeeStatus.NotYetInvited,
        };

        attendee.AssignAttendeeGroup(attendeeGroup);

        return attendee;
    }

    /// <summary>Replaces the attendee name and email after validating both.</summary>
    /// <param name="name">The name.</param>
    /// <param name="email">The email.</param>
    public void UpdateDetails(string? name, string? email)
    {
        // Validate both before mutating either.
        var newName = Guard.NotBlank(name, "name");
        var newEmail = NormaliseEmail(email);

        Name = newName;
        Email = newEmail;
    }

    /// <summary>Assigns a group and derives its complete set; returns whether that set changed.</summary>
    /// <param name="attendeeGroup">The attendee group.</param>
    public bool AssignAttendeeGroup(AttendeeGroup attendeeGroup)
    {
        ArgumentNullException.ThrowIfNull(attendeeGroup);
        Guard.Against(!attendeeGroup.IsActive, "An inactive attendee group cannot be assignment authority.");

        var mapping = attendeeGroup.RequiredAppointmentTypeIds.ToList();

        Guard.Against(mapping.Count == 0, "An attendee group must map at least one appointment type.");

        foreach (var appointmentTypeId in mapping)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

        AttendeeGroupId = attendeeGroup.Id;

        if (_requirements.Select(r => r.AppointmentTypeId).Order().SequenceEqual(mapping.Order()))
        {
            return false;
        }

        _requirements.Clear();
        foreach (var appointmentTypeId in mapping.Order())
        {
            _requirements.Add(AttendeeRequirement.For(Id, appointmentTypeId));
        }

        return true;
    }

    /// <summary>Moves the attendee to Invited from a pre-booking lifecycle state.</summary>
    public void MarkInvited() => TransitionTo(
        AttendeeStatus.Invited,
        AttendeeStatus.NotYetInvited,
        AttendeeStatus.AwaitingAvailability,
        AttendeeStatus.Invited,
        AttendeeStatus.NoResponseNeedsFollowUp);

    /// <summary>Moves the attendee to AwaitingAvailability from a pre-booking lifecycle state.</summary>
    public void MarkAwaitingAvailability() => TransitionTo(
        AttendeeStatus.AwaitingAvailability,
        AttendeeStatus.NotYetInvited,
        AttendeeStatus.AwaitingAvailability,
        AttendeeStatus.Invited,
        AttendeeStatus.NoResponseNeedsFollowUp);

    /// <summary>Moves an invited attendee to Booked.</summary>
    public void MarkBooked() => TransitionTo(AttendeeStatus.Booked, AttendeeStatus.Invited);

    /// <summary>Moves an invited attendee to NoResponseNeedsFollowUp.</summary>
    public void MarkNoResponse() => TransitionTo(
        AttendeeStatus.NoResponseNeedsFollowUp,
        AttendeeStatus.Invited);

    /// <summary>Returns an invited or booked attendee to NotYetInvited.</summary>
    public void ResetToNotYetInvited() => TransitionTo(
        AttendeeStatus.NotYetInvited,
        AttendeeStatus.Invited,
        AttendeeStatus.Booked);

    /// <summary>Resets an unbooked Attendee after a derived requirement-set change.</summary>
    public void ResetAfterRequirementChange()
    {
        if (Status is AttendeeStatus.NotYetInvited)
        {
            return;
        }

        TransitionTo(
            AttendeeStatus.NotYetInvited,
            AttendeeStatus.AwaitingAvailability,
            AttendeeStatus.NoResponseNeedsFollowUp,
            AttendeeStatus.Invited);
    }

    private void TransitionTo(AttendeeStatus target, params AttendeeStatus[] allowedOrigins)
    {
        Guard.Against(
            !allowedOrigins.Contains(Status),
            $"A attendee cannot move from {Status} to {target}.");

        Status = target;
    }

    private static string NormaliseEmail(string? email)
    {
        var trimmed = email?.Trim() ?? string.Empty;

        var valid =
            trimmed.Length > 0
            && !trimmed.Any(char.IsWhiteSpace)
            && MailAddress.TryCreate(trimmed, out var parsed)
            && parsed!.Host.Contains('.');

        Guard.Against(!valid, "email is not a valid email address.");

        return trimmed.ToLowerInvariant();
    }
}
`````

## after — src/EventBooking.Domain/Attendees/Attendee.cs — 1/1

<!-- retirement-file: {"id":9,"file":"src/EventBooking.Domain/Attendees/Attendee.cs","beforeSha":"43e60c2c07ec61c07a019cfb4ad03c21f73c0b1a53f5139c030a60bdc14c5a5c","afterSha":"dcda95b3b8c5124cc674a93b7a44d170c4c3c80ada4c59ca46c1a725c565ca41","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net.Mail;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Domain.Attendees;

/// <summary>A person invited to attend appointments, whose requirements derive from one attendee group.</summary>
public sealed class Attendee
{
    private readonly List<AttendeeRequirement> _requirements = [];

    private Attendee()
    {
        // Required by the persistence layer's constructor binding.
        Name = string.Empty;
        Email = string.Empty;
    }

    /// <summary>Gets the attendee identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the attendee display name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets the normalized attendee email address.</summary>
    public string Email { get; private set; }

    /// <summary>Gets the required assigned Attendee Group identifier.</summary>
    public Guid AttendeeGroupId { get; private set; }

    /// <summary>Gets where the attendee sits in the invite and booking lifecycle.</summary>
    public AttendeeStatus Status { get; private set; } = AttendeeStatus.NotYetInvited;

    /// <summary>Gets the materialized appointment types the attendee currently requires.</summary>
    public IReadOnlyList<AttendeeRequirement> Requirements => _requirements;

    /// <summary>Gets the identifiers of the appointment types the attendee currently requires.</summary>
    public IReadOnlyList<Guid> RequiredAppointmentTypeIds =>
        _requirements.Select(r => r.AppointmentTypeId).ToList();

    /// <summary>Creates a Attendee and derives every requirement from the active mapped group.</summary>
    /// <param name="id">The id.</param>
    /// <param name="name">The name.</param>
    /// <param name="email">The email.</param>
    /// <param name="attendeeGroup">The attendee group.</param>
    public static Attendee Create(Guid id, string? name, string? email, AttendeeGroup attendeeGroup)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");

        var attendee = new Attendee
        {
            Id = id,
            Name = Guard.NotBlank(name, "name"),
            Email = NormaliseEmail(email),
            Status = AttendeeStatus.NotYetInvited,
        };

        attendee.AssignAttendeeGroup(attendeeGroup);

        return attendee;
    }

    /// <summary>Replaces the attendee name and email after validating both.</summary>
    /// <param name="name">The name.</param>
    /// <param name="email">The email.</param>
    public void UpdateDetails(string? name, string? email)
    {
        // Validate both before mutating either.
        var newName = Guard.NotBlank(name, "name");
        var newEmail = NormaliseEmail(email);

        Name = newName;
        Email = newEmail;
    }

    /// <summary>Assigns a group and derives its complete set; returns whether that set changed.</summary>
    /// <param name="attendeeGroup">The attendee group.</param>
    public bool AssignAttendeeGroup(AttendeeGroup attendeeGroup)
    {
        ArgumentNullException.ThrowIfNull(attendeeGroup);
        Guard.Against(!attendeeGroup.IsActive, "An inactive attendee group cannot be assignment authority.");

        var mapping = attendeeGroup.RequiredAppointmentTypeIds.ToList();

        Guard.Against(mapping.Count == 0, "An attendee group must map at least one appointment type.");

        foreach (var appointmentTypeId in mapping)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

        AttendeeGroupId = attendeeGroup.Id;

        if (_requirements.Select(r => r.AppointmentTypeId).Order().SequenceEqual(mapping.Order()))
        {
            return false;
        }

        _requirements.Clear();
        foreach (var appointmentTypeId in mapping.Order())
        {
            _requirements.Add(AttendeeRequirement.For(Id, appointmentTypeId));
        }

        return true;
    }

    /// <summary>Moves the attendee to Invited from a pre-booking lifecycle state.</summary>
    public void MarkInvited() => TransitionTo(
        AttendeeStatus.Invited,
        AttendeeStatus.NotYetInvited,
        AttendeeStatus.AwaitingAvailability,
        AttendeeStatus.Invited,
        AttendeeStatus.NoResponseNeedsFollowUp);

    /// <summary>Moves the attendee to AwaitingAvailability from a pre-booking lifecycle state.</summary>
    public void MarkAwaitingAvailability() => TransitionTo(
        AttendeeStatus.AwaitingAvailability,
        AttendeeStatus.NotYetInvited,
        AttendeeStatus.AwaitingAvailability,
        AttendeeStatus.Invited,
        AttendeeStatus.NoResponseNeedsFollowUp);

    /// <summary>Moves an invited attendee to Booked.</summary>
    public void MarkBooked() => TransitionTo(AttendeeStatus.Booked, AttendeeStatus.Invited);

    /// <summary>Moves an invited attendee to NoResponseNeedsFollowUp.</summary>
    public void MarkNoResponse() => TransitionTo(
        AttendeeStatus.NoResponseNeedsFollowUp,
        AttendeeStatus.Invited);

    /// <summary>Returns an invited or booked attendee to NotYetInvited.</summary>
    public void ResetToNotYetInvited() => TransitionTo(
        AttendeeStatus.NotYetInvited,
        AttendeeStatus.Invited,
        AttendeeStatus.Booked);

    /// <summary>Resets an unbooked Attendee after a derived requirement-set change.</summary>
    public void ResetAfterRequirementChange()
    {
        if (Status is AttendeeStatus.NotYetInvited)
        {
            return;
        }

        TransitionTo(
            AttendeeStatus.NotYetInvited,
            AttendeeStatus.AwaitingAvailability,
            AttendeeStatus.NoResponseNeedsFollowUp,
            AttendeeStatus.Invited);
    }

    private void TransitionTo(AttendeeStatus target, params AttendeeStatus[] allowedOrigins)
    {
        Guard.Against(
            !allowedOrigins.Contains(Status),
            $"A attendee cannot move from {Status} to {target}.");

        Status = target;
    }

    private static string NormaliseEmail(string? email)
    {
        var trimmed = email?.Trim() ?? string.Empty;

        var valid =
            trimmed.Length > 0
            && !trimmed.Any(char.IsWhiteSpace)
            && MailAddress.TryCreate(trimmed, out var parsed)
            && parsed!.Host.Contains('.');

        Guard.Against(!valid, "email is not a valid email address.");

        return trimmed.ToLowerInvariant();
    }
}
`````

## before — src/EventBooking.Mcp/Tools/AttendeeTools.cs — 1/1

<!-- retirement-file: {"id":10,"file":"src/EventBooking.Mcp/Tools/AttendeeTools.cs","beforeSha":"8489f4e8506d6d99ee2121cc35d300a56cbcb877fb213a338f8442b325142f2d","afterSha":"b4a7a9d68537a8a4aa0c540f43f0946e6007f12541079f586ade75d26557b47d","side":"before","part":1,"parts":1} -->

`````csharp
using System.ComponentModel;
using EventBooking.Api.Auth;
using EventBooking.Api.Endpoints;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Attendees;
using ModelContextProtocol.Server;

namespace EventBooking.Mcp.Tools;

public sealed record AttendeeToolView
{
    /// <summary>Gets the attendee identifier for follow-up tool calls.</summary>
    public required Guid AttendeeId { get; init; }
    /// <summary>Gets the attendee display name.</summary>
    public required string Name { get; init; }
    /// <summary>Gets the attendee email address.</summary>
    public required string Email { get; init; }
    /// <summary>Gets the attendee lifecycle status name.</summary>
    public required string Status { get; init; }
    /// <summary>Gets the assigned Attendee Group name, or null during reconciliation.</summary>
    public required string? AttendeeGroupName { get; init; }
    /// <summary>Gets the canonical Attendee Group code, or null during Release 1 reconciliation.</summary>
    public required string? AttendeeGroupCode { get; init; }
    /// <summary>Gets whether explicit Coordinator assignment is still required.</summary>
    public required bool RequiresAttendeeGroupReconciliation { get; init; }
    /// <summary>Gets read-only derived Appointment Type summaries.</summary>
    public required IReadOnlyList<AppointmentTypeSummary> RequiredAppointmentTypes { get; init; }
    /// <summary>Gets internal readiness without exposing recovery mutation.</summary>
    public required AttendeeReadiness? Readiness { get; init; }
}

/// <summary>Tool-safe readiness including Coordinator display wording.</summary>
/// <param name="AttendeeId">The attendee the readiness was calculated for.</param>
/// <param name="Code">The readiness code name.</param>
/// <param name="Display">The Coordinator-facing display wording for the code.</param>
/// <param name="OutstandingAppointmentTypes">The appointment types still outstanding.</param>
public sealed record AttendeeReadinessToolView(
    Guid AttendeeId,
    string Code,
    string Display,
    IReadOnlyList<OutstandingAppointmentType> OutstandingAppointmentTypes);

/// <summary>Attendee management, invites, and delivery retry for coordinators.</summary>
[McpServerToolType]
public sealed class AttendeeTools
{
    private const int DefaultPageSize = 50;

    private const int MaxPageSize = 200;

    /// <summary>Lists attendees, optionally filtered by status or search text.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The list handler.</param>
    /// <param name="readiness">Resolves internal readiness per listed attendee.</param>
    /// <param name="status">The attendee status name, or null for all.</param>
    /// <param name="search">Free-text filter, or null.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">Results per page, clamped to the tool maximum.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The bounded page of attendee views.</returns>
    [McpServerTool(Name = "list_attendees", Title = "List attendees", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List attendees, optionally filtered by status name and search text. Caller must be a coordinator or admin.")]
    public async Task<IReadOnlyList<AttendeeToolView>> ListAttendeesAsync(
        ICallerAccessor caller,
        ListAttendeesHandler handler,
        GetAttendeeReadinessHandler readiness,
        [Description("Attendee status name (e.g. Invited) or null for all.")] string? status = null,
        [Description("Free-text name or email filter or null.")] string? search = null,
        [Description("1-based page number.")] int page = 1,
        [Description("Results per page, at most 200.")] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        AttendeeStatus? parsed = null;
        if (status is not null)
        {
            if (!Enum.TryParse<AttendeeStatus>(status, ignoreCase: false, out var value) ||
                !Enum.IsDefined(value))
            {
                throw new ModelContextProtocol.McpException(
                    "Status must be a recognised AttendeeStatus name.");
            }

            parsed = value;
        }

        var staffUserId = caller.RequireStaffUserId();
        var result = await handler.HandleAsync(
            new ListAttendeesQuery(staffUserId, parsed, search),
            cancellationToken);
        var bounded = Math.Clamp(pageSize, 1, MaxPageSize);
        var skipped = Math.Max(page - 1, 0) * bounded;

        var views = new List<AttendeeToolView>();
        foreach (var item in result.ValueOrThrow().Skip(skipped).Take(bounded))
        {
            var readinessResult = await readiness.HandleAsync(
                new GetAttendeeReadinessQuery(staffUserId, item.AttendeeId),
                cancellationToken);
            views.Add(new AttendeeToolView
            {
                AttendeeId = item.AttendeeId,
                Name = item.Name,
                Email = item.Email,
                Status = item.Status.ToString(),
                AttendeeGroupName = item.AttendeeGroupName,
                AttendeeGroupCode = item.AttendeeGroupCode,
                RequiresAttendeeGroupReconciliation = item.RequiresAttendeeGroupReconciliation,
                RequiredAppointmentTypes = item.RequiredAppointmentTypes,
                Readiness = readinessResult.IsSuccess ? readinessResult.Value : null,
            });
        }

        return views;
    }

    /// <summary>Lists the Attendee Groups available for attendee assignment.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The list handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The assignable groups with their required appointment types.</returns>
    [McpServerTool(Name = "list_attendee_groups", Title = "List attendee groups", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List the attendee groups that determine attendee requirements. Caller must be a coordinator or admin.")]
    public async Task<IReadOnlyList<AttendeeGroupListItem>> ListAttendeeGroupsAsync(
        ICallerAccessor caller,
        ListAttendeeGroupsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ListAttendeeGroupsQuery(caller.RequireStaffUserId()),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Creates one attendee in an Attendee Group.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The save handler.</param>
    /// <param name="groups">Resolves the assigned Attendee Group.</param>
    /// <param name="name">The attendee name.</param>
    /// <param name="email">The attendee email.</param>
    /// <param name="attendeeGroupCode">The canonical Attendee Group code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new attendee identifier.</returns>
    [McpServerTool(Name = "create_attendee", Title = "Create attendee", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Create a attendee in one attendee group; requirements derive from the group. Caller must be a coordinator or admin; creates a new attendee record.")]
    public async Task<Guid> CreateAttendeeAsync(
        ICallerAccessor caller,
        SaveAttendeeHandler handler,
        IAttendeeGroupRepository groups,
        [Description("Attendee full name.")] string name,
        [Description("Attendee email address.")] string email,
        [Description("Canonical attendee group code (e.g. PILOTS).")] string attendeeGroupCode,
        CancellationToken cancellationToken)
    {
        var groupId = await ResolveGroupIdAsync(groups, attendeeGroupCode, cancellationToken);
        var result = await handler.CreateAsync(
            new CreateAttendeeCommand(caller.RequireStaffUserId(), name, email, groupId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Updates a attendee's name, email, or Attendee Group.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The save handler.</param>
    /// <param name="groups">Resolves the assigned Attendee Group.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="name">The corrected name.</param>
    /// <param name="email">The corrected email.</param>
    /// <param name="attendeeGroupCode">The canonical Attendee Group code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "update_attendee", Title = "Update attendee", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Update a attendee. Caller must be a coordinator or admin; requirements are frozen while an active booking exists.")]
    public async Task<string> UpdateAttendeeAsync(
        ICallerAccessor caller,
        SaveAttendeeHandler handler,
        IAttendeeGroupRepository groups,
        [Description("The attendee identifier.")] Guid attendeeId,
        [Description("Corrected full name.")] string name,
        [Description("Corrected email address.")] string email,
        [Description("Canonical attendee group code (e.g. PILOTS).")] string attendeeGroupCode,
        CancellationToken cancellationToken)
    {
        var groupId = await ResolveGroupIdAsync(groups, attendeeGroupCode, cancellationToken);
        var result = await handler.UpdateAsync(
            new UpdateAttendeeCommand(
                caller.RequireStaffUserId(), attendeeId, name, email, groupId),
            cancellationToken);
        result.ThrowIfFailure();
        return "Attendee updated.";
    }

    /// <summary>Deletes a attendee, optionally cascading.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The delete handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="confirm">Whether dependent data may be removed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "delete_attendee", Title = "Delete attendee", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Delete a attendee. Caller must be a coordinator or admin; set confirm to true to also remove dependent data.")]
    public async Task<string> DeleteAttendeeAsync(
        ICallerAccessor caller,
        DeleteAttendeeHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        [Description("Whether dependent data may be removed.")] bool confirm,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new DeleteAttendeeCommand(caller.RequireStaffUserId(), attendeeId, confirm),
            cancellationToken);
        result.ThrowIfFailure();
        return "Attendee deleted.";
    }

    /// <summary>Imports attendees from CSV content.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The import handler.</param>
    /// <param name="csv">The CSV content.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The import outcome.</returns>
    [McpServerTool(Name = "import_attendees", Title = "Import attendees", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Import attendees from CSV content with name, email, and attendee group code. Caller must be a coordinator or admin; creates new attendee records.")]
    public async Task<AttendeeImportOutcome> ImportAttendeesAsync(
        ICallerAccessor caller,
        ImportAttendeesHandler handler,
        [Description("CSV content with one attendee per row.")] string csv,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ImportAttendeesCommand(caller.RequireStaffUserId(), csv),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Resolves a canonical Attendee Group code to its stable identifier.</summary>
    private static async Task<Guid> ResolveGroupIdAsync(
        IAttendeeGroupRepository groups,
        string attendeeGroupCode,
        CancellationToken cancellationToken)
    {
        var group = await groups.GetByCodeAsync(attendeeGroupCode, cancellationToken);
        if (group is null)
        {
            throw new ModelContextProtocol.McpException(
                $"Employee group '{attendeeGroupCode}' is not known. Use list_attendee_groups.");
        }

        return group.Id;
    }

    /// <summary>Triggers a fresh invite for a attendee.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The invite handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invite issue result.</returns>
    [McpServerTool(Name = "trigger_invite", Title = "Trigger invite", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Issue a fresh three-option invite to a attendee. Caller must be a coordinator or admin; sends an invite email.")]
    public async Task<InviteIssueResult> TriggerInviteAsync(
        ICallerAccessor caller,
        TriggerInviteHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new TriggerInviteCommand(caller.RequireStaffUserId(), attendeeId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Retries the latest unresolved email for a attendee.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The retry handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The retry outcome.</returns>
    [McpServerTool(Name = "retry_attendee_email", Title = "Retry attendee email", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Retry the latest unresolved attendee email delivery. Caller must be a coordinator or admin; resends the latest unresolved email.")]
    public async Task<RetryEmailOutcome> RetryAttendeeEmailAsync(
        ICallerAccessor caller,
        RetryEmailHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new RetryEmailCommand(caller.RequireStaffUserId(), attendeeId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Starts a recovery invite for a attendee with a missed appointment.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The recovery handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The recovery invite issue result.</returns>
    [McpServerTool(Name = "start_recovery_invite", Title = "Start recovery invite", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Start a recovery invite for a attendee with a missed appointment. Caller must have ManageAttendees; sends a recovery invite email.")]
    public async Task<StartRecoveryResult> StartRecoveryInviteAsync(
        ICallerAccessor caller,
        StartRecoveryHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new StartRecoveryCommand(caller.RequireStaffUserId(), attendeeId), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Cancels a pending recovery invite for a attendee.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The recovery cancellation handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="inviteId">The recovery invite identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "cancel_recovery_invite", Title = "Cancel recovery invite", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Cancel a pending recovery invite for a attendee. Caller must have ManageAttendees; cancels the pending recovery invite.")]
    public async Task<string> CancelRecoveryInviteAsync(
        ICallerAccessor caller,
        CancelRecoveryInviteHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        [Description("The recovery invite identifier.")] Guid inviteId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CancelRecoveryInviteCommand(caller.RequireStaffUserId(), attendeeId, inviteId), cancellationToken);
        result.ThrowIfFailure();
        return "Recovery invite cancelled.";
    }

    /// <summary>Lists the active bookings a coordinator may cancel for one attendee.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The bookings handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The attendee's active booking summaries.</returns>
    [McpServerTool(Name = "list_attendee_bookings", Title = "List attendee bookings", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List a attendee's active bookings. Caller must have ManageAttendees; returns cancellable bookings without management tokens.")]
    public async Task<IReadOnlyList<AttendeeBookingSummary>> ListAttendeeBookingsAsync(
        ICallerAccessor caller,
        GetAttendeeBookingsHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetAttendeeBookingsQuery(caller.RequireStaffUserId(), attendeeId), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Cancels one attendee booking, optionally rebooking the attendee.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The cancellation handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="bookingId">The booking identifier.</param>
    /// <param name="rebook">Whether to issue a replacement invite.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cancellation outcome.</returns>
    [McpServerTool(Name = "cancel_attendee_booking", Title = "Cancel attendee booking", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Cancel one attendee booking, optionally rebooking. Caller must have ManageAttendees; cancels the booking and optionally sends a replacement invite.")]
    public async Task<CancelBookingOutcome> CancelAttendeeBookingAsync(
        ICallerAccessor caller,
        CancelAttendeeBookingHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        [Description("The booking identifier.")] Guid bookingId,
        [Description("Whether to issue a replacement invite.")] bool rebook,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CancelAttendeeBookingCommand(caller.RequireStaffUserId(), attendeeId, bookingId, rebook), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Gets tool-safe readiness including Coordinator display wording.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The readiness handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool-safe readiness view.</returns>
    [McpServerTool(Name = "get_attendee_readiness", Title = "Get attendee readiness", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Get a attendee's readiness with Coordinator display wording. Caller must have ManageAttendees; returns internal readiness without recovery mutation.")]
    public async Task<AttendeeReadinessToolView> GetAttendeeReadinessAsync(
        ICallerAccessor caller,
        GetAttendeeReadinessHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetAttendeeReadinessQuery(caller.RequireStaffUserId(), attendeeId), cancellationToken);
        var value = result.ValueOrThrow();
        return new AttendeeReadinessToolView(
            value.AttendeeId,
            value.Code.ToString(),
            AttendeeEndpoints.DisplayForTool(value.Code),
            value.OutstandingAppointmentTypes);
    }
}
`````
