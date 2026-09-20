# 00a — Port source 13 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.Domain/Invites/Invite.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Invites/Invite.cs","encoding":"utf8","sha256":"2c34b37c359d4ec5bc3c03a363e9398f7ff9957cf47d9f2a46dee7e3daa4d26e","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Invites;

/// <summary>An offer of slot options carrying an immutable requirement snapshot.</summary>
public sealed class Invite
{
    /// <summary>Gets the number of slot options every invite offers.</summary>
    public const int RequiredOptionCount = 3;

    private readonly List<InviteOption> _options = [];
    private readonly List<InviteRequirement> _requirements = [];

    private Invite()
    {
        // Required by the persistence layer's constructor binding.
        TokenHash = string.Empty;
    }

    /// <summary>Gets the invite identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the invited candidate identifier.</summary>
    public Guid CandidateId { get; private set; }

    /// <summary>Gets the original Booking recovered by this Invite, or null for an initial Invite.</summary>
    public Guid? RecoveryOfBookingId { get; private set; }

    /// <summary>The hash of the single-use token. The token itself is never stored.</summary>
    public string TokenHash { get; private set; }

    /// <summary>Gets when the invite stops being usable.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Gets the invite lifecycle state.</summary>
    public InviteStatus Status { get; private set; } = InviteStatus.Pending;

    /// <summary>Gets how many retries preceded this invite.</summary>
    public int RetryCount { get; private set; }

    /// <summary>Gets the offered slot options.</summary>
    public IReadOnlyList<InviteOption> Options => _options;

    /// <summary>Gets the offered slot identifiers.</summary>
    public IReadOnlyList<Guid> OfferedSlotIds => _options.Select(o => o.ConfirmedSlotId).ToList();

    /// <summary>Gets the immutable requirement snapshot used by every downstream operation.</summary>
    public IReadOnlyList<InviteRequirement> Requirements => _requirements;

    /// <summary>Gets the snapshotted Appointment Type identifiers in stable order.</summary>
    public IReadOnlyList<Guid> RequiredAppointmentTypeIds =>
        _requirements.Select(r => r.AppointmentTypeId).Order().ToList();

    /// <summary>Replaces the persisted hash after issuing a fresh in-memory invite token.</summary>
    /// <param name="tokenHash">The token hash.</param>
    public void RotateTokenHash(string? tokenHash)
    {
        EnsurePending("Only a pending invite token can be rotated.");
        TokenHash = Guard.NotBlank(tokenHash, "tokenHash");
    }

    /// <summary>Creates an initial invite snapshotting every current derived requirement.</summary>
    /// <param name="id">The id.</param>
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="tokenHash">The token hash.</param>
    /// <param name="expiresAt">The expires at.</param>
    /// <param name="confirmedSlotIds">The confirmed slot ids.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="retryCount">The retry count.</param>
    public static Invite CreateInitial(
        Guid id,
        Guid candidateId,
        string? tokenHash,
        DateTimeOffset expiresAt,
        IEnumerable<Guid> confirmedSlotIds,
        IEnumerable<Guid> appointmentTypeIds,
        int retryCount) =>
        Create(id, candidateId, null, tokenHash, expiresAt, confirmedSlotIds, appointmentTypeIds, retryCount);

    /// <summary>Creates a recovery invite snapshotting only recoverable no-show types.</summary>
    /// <param name="id">The id.</param>
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="recoveryOfBookingId">The recovery of booking id.</param>
    /// <param name="tokenHash">The token hash.</param>
    /// <param name="expiresAt">The expires at.</param>
    /// <param name="confirmedSlotIds">The confirmed slot ids.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public static Invite CreateRecovery(
        Guid id,
        Guid candidateId,
        Guid recoveryOfBookingId,
        string? tokenHash,
        DateTimeOffset expiresAt,
        IEnumerable<Guid> confirmedSlotIds,
        IEnumerable<Guid> appointmentTypeIds)
    {
        Guard.Against(recoveryOfBookingId == Guid.Empty, "recoveryOfBookingId must not be empty.");
        return Create(
            id, candidateId, recoveryOfBookingId, tokenHash, expiresAt,
            confirmedSlotIds, appointmentTypeIds, 0);
    }

    /// <summary>Determines whether the invite can still be used at the supplied instant.</summary>
    /// <param name="now">The now.</param>
    public bool IsUsableAt(DateTimeOffset now) =>
        Status == InviteStatus.Pending && now < ExpiresAt;

    /// <summary>Determines whether the invite offers the supplied slot.</summary>
    /// <param name="confirmedSlotId">The confirmed slot id.</param>
    public bool Offers(Guid confirmedSlotId) =>
        _options.Any(o => o.ConfirmedSlotId == confirmedSlotId);

    /// <summary>Removes one offered slot from a pending invite.</summary>
    /// <param name="confirmedSlotId">The confirmed slot id.</param>
    public void RemoveOption(Guid confirmedSlotId)
    {
        EnsurePending("Only a pending invite's options can change.");

        var option = _options.SingleOrDefault(o => o.ConfirmedSlotId == confirmedSlotId);
        Guard.Against(option is null, "This invite does not offer that slot.");

        _options.Remove(option!);
    }

    /// <summary>Adds one offered slot to a pending invite.</summary>
    /// <param name="confirmedSlotId">The confirmed slot id.</param>
    public void AddOption(Guid confirmedSlotId)
    {
        EnsurePending("Only a pending invite's options can change.");
        Guard.Against(
            _options.Count >= RequiredOptionCount,
            $"An invite cannot offer more than {RequiredOptionCount} slot options.");
        Guard.Against(Offers(confirmedSlotId), "An invite cannot offer the same slot twice.");

        _options.Add(InviteOption.For(Id, confirmedSlotId));
    }

    /// <summary>Moves a pending invite to used.</summary>
    public void MarkUsed() => TransitionFromPendingTo(InviteStatus.Used);

    /// <summary>Moves a pending invite to expired.</summary>
    public void MarkExpired() => TransitionFromPendingTo(InviteStatus.Expired);

    /// <summary>Moves a pending invite to superseded.</summary>
    public void MarkSuperseded() => TransitionFromPendingTo(InviteStatus.Superseded);

    /// <summary>Cancels a pending recovery Invite without changing capacity.</summary>
    public void CancelRecovery()
    {
        Guard.Against(RecoveryOfBookingId is null, "Only a recovery invite can be cancelled.");
        EnsurePending("Only a pending recovery invite can be cancelled.");
        Status = InviteStatus.Cancelled;
    }

    private static Invite Create(
        Guid id,
        Guid candidateId,
        Guid? recoveryOfBookingId,
        string? tokenHash,
        DateTimeOffset expiresAt,
        IEnumerable<Guid> confirmedSlotIds,
        IEnumerable<Guid> appointmentTypeIds,
        int retryCount)
    {
        var invite = CreateCore(id, candidateId, recoveryOfBookingId, tokenHash, expiresAt, confirmedSlotIds, retryCount);

        var snapshot = appointmentTypeIds.ToList();
        Guard.Against(snapshot.Count == 0, "An invite must snapshot at least one appointment type.");
        Guard.Against(snapshot.Count > 3, "An invite cannot snapshot more than three appointment types.");
        Guard.Against(
            snapshot.Distinct().Count() != snapshot.Count,
            "An invite cannot snapshot the same appointment type twice.");

        foreach (var appointmentTypeId in snapshot)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

        foreach (var appointmentTypeId in snapshot.Order())
        {
            invite._requirements.Add(InviteRequirement.For(id, appointmentTypeId));
        }

        return invite;
    }

    private static Invite CreateCore(
        Guid id,
        Guid candidateId,
        Guid? recoveryOfBookingId,
        string? tokenHash,
        DateTimeOffset expiresAt,
        IEnumerable<Guid> confirmedSlotIds,
        int retryCount)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(candidateId == Guid.Empty, "candidateId must not be empty.");

        var slotIds = confirmedSlotIds.ToList();
        Guard.Against(
            slotIds.Count != RequiredOptionCount,
            $"An invite must offer exactly {RequiredOptionCount} slot options.");
        Guard.Against(
            slotIds.Distinct().Count() != slotIds.Count,
            "An invite cannot offer the same slot twice.");

        var invite = new Invite
        {
            Id = id,
            CandidateId = candidateId,
            RecoveryOfBookingId = recoveryOfBookingId,
            TokenHash = Guard.NotBlank(tokenHash, "tokenHash"),
            ExpiresAt = expiresAt,
            Status = InviteStatus.Pending,
            RetryCount = Guard.NotNegative(retryCount, "retryCount"),
        };

        foreach (var slotId in slotIds)
        {
            invite._options.Add(InviteOption.For(id, slotId));
        }

        return invite;
    }

    private void TransitionFromPendingTo(InviteStatus target)
    {
        EnsurePending($"An invite that is already {Status} cannot become {target}.");
        Status = target;
    }

    private void EnsurePending(string message) =>
        Guard.Against(Status != InviteStatus.Pending, message);
}
`````

## src/EventBooking.Domain/Invites/InviteOption.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Invites/InviteOption.cs","encoding":"utf8","sha256":"e11d7bf184372b2c5e1c3978d21b86f3098435aa524596cd1afd3074182d0dd8","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Domain.Invites;

/// <summary>One of the confirmed slots offered inside an invite.</summary>
public sealed class InviteOption
{
    private InviteOption()
    {
    }

    /// <summary>Defines invite id for the current use case.</summary>
    public Guid InviteId { get; private set; }

    /// <summary>Defines confirmed slot id for the current use case.</summary>
    public Guid ConfirmedSlotId { get; private set; }

    internal static InviteOption For(Guid inviteId, Guid confirmedSlotId) =>
        new() { InviteId = inviteId, ConfirmedSlotId = confirmedSlotId };
}
`````

## src/EventBooking.Domain/Invites/InviteRequirement.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Invites/InviteRequirement.cs","encoding":"utf8","sha256":"349aac080345d627d24de48de09fdeffa5890cf446f121fccdb054f818fbeb29","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Domain.Invites;

/// <summary>One fixed Appointment Type snapshotted for an initial or recovery Invite.</summary>
public sealed class InviteRequirement
{
    private InviteRequirement()
    {
    }

    /// <summary>Gets the owning Invite identifier.</summary>
    public Guid InviteId { get; private set; }

    /// <summary>Gets the snapshotted fixed Appointment Type identifier.</summary>
    public Guid AppointmentTypeId { get; private set; }

    internal static InviteRequirement For(Guid inviteId, Guid appointmentTypeId) =>
        new() { InviteId = inviteId, AppointmentTypeId = appointmentTypeId };
}
`````

## src/EventBooking.Domain/Invites/InviteStatus.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Invites/InviteStatus.cs","encoding":"utf8","sha256":"e85b6e10429bbd50567c7411758f247fa8fcead392db220d7d2baf30afb05281","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Domain.Invites;

/// <summary>Where an invite sits in its offer lifecycle.</summary>
public enum InviteStatus
{
    /// <summary>Awaiting a candidate response.</summary>
    Pending = 1,
    /// <summary>Consumed by a booking.</summary>
    Used = 2,
    /// <summary>Passed its expiry without use.</summary>
    Expired = 3,
    /// <summary>Replaced by a newer invite or a group change.</summary>
    Superseded = 4,
    /// <summary>Explicitly ended by a Coordinator recovery cancellation.</summary>
    Cancelled = 5,
}
`````

## src/EventBooking.Domain/Notifications/EmailLog.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Notifications/EmailLog.cs","encoding":"utf8","sha256":"6aecccdb8b6ad62d2bd578610609378b89f1b34d15dd262036dfa75f751fd98a","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Notifications;

/// <summary>
/// A durable record of one candidate email delivery attempt. Its identity and safe context are
/// immutable while claim and outcome fields transition; raw tokens, URLs, and bodies never belong
/// in this record.
/// </summary>
public sealed class EmailLog
{
    private EmailLog()
    {
    }

    /// <summary>The durable identifier of this delivery attempt.</summary>
    public Guid Id { get; private set; }

    /// <summary>The candidate who is the recipient of this delivery attempt.</summary>
    public Guid CandidateId { get; private set; }

    /// <summary>The candidate-facing template this attempt renders.</summary>
    public EmailTemplate TemplateName { get; private set; }

    /// <summary>The timestamp of the current or most recent attempt, supplied by <c>IClock</c>.</summary>
    public DateTimeOffset SentAt { get; private set; }

    /// <summary>The durable provider outcome, including <see cref="EmailStatus.Pending"/>.</summary>
    public EmailStatus Status { get; private set; }

    /// <summary>The invite context used by invite and re-invite templates, when applicable.</summary>
    public Guid? InviteId { get; private set; }

    /// <summary>The booking context used by a booking-confirmation template, when applicable.</summary>
    public Guid? BookingId { get; private set; }

    /// <summary>The confirmed-slot context used by a cancellation template, when applicable.</summary>
    public Guid? ConfirmedSlotId { get; private set; }

    /// <summary>The in-progress claim timestamp used to prevent duplicate concurrent sends.</summary>
    public DateTimeOffset? ClaimedAt { get; private set; }

    /// <summary>Creates a legacy email attempt without a regeneration context.</summary>
    /// <param name="id">The id.</param>
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="templateName">The template name.</param>
    /// <param name="sentAt">The sent at.</param>
    /// <param name="status">The status.</param>
    public static EmailLog Record(
        Guid id,
        Guid candidateId,
        EmailTemplate templateName,
        DateTimeOffset sentAt,
        EmailStatus status)
        => PendingOrRecorded(id, candidateId, templateName, sentAt, status, null, null, null);

    /// <summary>
    /// Creates a pending delivery with only safe context identifiers. The caller saves it in the
    /// same business transaction as the state change that caused the notification.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="templateName">The template name.</param>
    /// <param name="createdAt">The created at.</param>
    /// <param name="inviteId">The invite id.</param>
    /// <param name="bookingId">The booking id.</param>
    /// <param name="confirmedSlotId">The confirmed slot id.</param>
    public static EmailLog RecordPending(
        Guid id,
        Guid candidateId,
        EmailTemplate templateName,
        DateTimeOffset createdAt,
        Guid? inviteId = null,
        Guid? bookingId = null,
        Guid? confirmedSlotId = null)
        => PendingOrRecorded(
            id,
            candidateId,
            templateName,
            createdAt,
            EmailStatus.Pending,
            inviteId,
            bookingId,
            confirmedSlotId);

    /// <summary>Claims a pending delivery unless another worker holds a fresh claim.</summary>
    /// <param name="now">The now.</param>
    /// <param name="lease">The lease.</param>
    public bool TryClaim(DateTimeOffset now, TimeSpan lease)
    {
        if (Status is EmailStatus.Sent or EmailStatus.Resolved)
        {
            return false;
        }

        if (ClaimedAt is not null && now - ClaimedAt.Value < lease)
        {
            return false;
        }

        ClaimedAt = now;
        return true;
    }

    /// <summary>Marks the claimed delivery as successfully sent.</summary>
    /// <param name="sentAt">The sent at.</param>
    public void MarkSent(DateTimeOffset sentAt)
    {
        Status = EmailStatus.Sent;
        SentAt = sentAt > SentAt ? sentAt : SentAt;
        ClaimedAt = null;
    }

    /// <summary>Marks the claimed delivery as failed while retaining it for staff retry.</summary>
    /// <param name="failedAt">The failed at.</param>
    public void MarkFailed(DateTimeOffset failedAt)
    {
        Status = EmailStatus.Failed;
        SentAt = failedAt > SentAt ? failedAt : SentAt;
        ClaimedAt = null;
    }

    /// <summary>Marks an outstanding attempt as superseded by a newer durable retry attempt.</summary>
    /// <param name="resolvedAt">The resolved at.</param>
    public void MarkResolved(DateTimeOffset resolvedAt)
    {
        Guard.Against(
            Status is EmailStatus.Sent or EmailStatus.Resolved,
            "Only an outstanding email delivery can be resolved.");
        Status = EmailStatus.Resolved;
        SentAt = resolvedAt > SentAt ? resolvedAt : SentAt;
        ClaimedAt = null;
    }

    private static EmailLog PendingOrRecorded(
        Guid id,
        Guid candidateId,
        EmailTemplate templateName,
        DateTimeOffset sentAt,
        EmailStatus status,
        Guid? inviteId,
        Guid? bookingId,
        Guid? confirmedSlotId)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(candidateId == Guid.Empty, "candidateId must not be empty.");

        return new EmailLog
        {
            Id = id,
            CandidateId = candidateId,
            TemplateName = templateName,
            SentAt = sentAt,
            Status = status,
            InviteId = inviteId,
            BookingId = bookingId,
            ConfirmedSlotId = confirmedSlotId,
        };
    }
}
`````

## src/EventBooking.Domain/Notifications/EmailStatus.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Notifications/EmailStatus.cs","encoding":"utf8","sha256":"366a7979a37a4f9b5e2b82fd1ed36e343bfb5dda9d712a58fdef652d8faac587","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Domain.Notifications;

/// <summary>Durable outcome of one candidate email delivery attempt.</summary>
public enum EmailStatus
{
    /// <summary>The provider accepted the message for this delivery attempt.</summary>
    Sent = 1,

    /// <summary>The provider rejected the message or the transport reported a failure.</summary>
    Failed = 2,

    /// <summary>The state change committed and the delivery still needs an attempt or retry.</summary>
    Pending = 3,

    /// <summary>A later durable retry attempt superseded this failed or pending attempt.</summary>
    Resolved = 4,
}
`````

## src/EventBooking.Domain/Notifications/EmailTemplate.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Notifications/EmailTemplate.cs","encoding":"utf8","sha256":"7d8df8d205845aa2dd649ed34fb8f8cfce51683c88d09882412d16dfe01f8fb0","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Domain.Notifications;

/// <summary>Defines email template for the current use case.</summary>
public enum EmailTemplate
{
    /// <summary>Defines candidate invite for the current use case.</summary>
    CandidateInvite = 1,
    /// <summary>Defines booking confirmation for the current use case.</summary>
    BookingConfirmation = 2,
    /// <summary>Defines slot cancelled rebooking needed for the current use case.</summary>
    SlotCancelledRebookingNeeded = 3,
    /// <summary>Defines candidate reinvite for the current use case.</summary>
    CandidateReinvite = 4,
}
`````

## src/EventBooking.Domain/Settings/SystemSettings.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Settings/SystemSettings.cs","encoding":"utf8","sha256":"6a541e191a55637d3d93e8d15d16c1ff4025e0a1efe8663591e04e246401c472","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Settings;

/// <summary>Admin-configurable settings. Exactly one row exists, with a constant identifier.</summary>
public sealed class SystemSettings
{
    /// <summary>Defines singleton id for the current use case.</summary>
    public const int SingletonId = 1;

    private SystemSettings()
    {
    }

    /// <summary>Defines id for the current use case.</summary>
    public int Id { get; private set; } = SingletonId;

    /// <summary>Defines invite expiry days for the current use case.</summary>
    public int InviteExpiryDays { get; private set; }

    /// <summary>Defines max auto retry count for the current use case.</summary>
    public int MaxAutoRetryCount { get; private set; }

    /// <summary>Defines create default for the current use case.</summary>
    public static SystemSettings CreateDefault() =>
        new() { Id = SingletonId, InviteExpiryDays = 4, MaxAutoRetryCount = 2 };

    /// <summary>Defines update for the current use case.</summary>
    /// <param name="inviteExpiryDays">The invite expiry days.</param>
    /// <param name="maxAutoRetryCount">The max auto retry count.</param>
    public void Update(int inviteExpiryDays, int maxAutoRetryCount)
    {
        // Validate both before mutating either, so a rejected update changes nothing.
        var days = Guard.Positive(inviteExpiryDays, "inviteExpiryDays");
        var retries = Guard.NotNegative(maxAutoRetryCount, "maxAutoRetryCount");

        InviteExpiryDays = days;
        MaxAutoRetryCount = retries;
    }
}
`````

## src/EventBooking.Domain/Slots/ConfirmedSlot.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Slots/ConfirmedSlot.cs","encoding":"utf8","sha256":"1f5aaebd3b1466db955c03cca49d20a2e4307b13ad1dd3c1531f731ce1175d62","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Slots;

/// <summary>Defines confirmed slot for the current use case.</summary>
public sealed class ConfirmedSlot
{
    private readonly List<SlotCapacity> _capacities = [];

    private ConfirmedSlot()
    {
        // Required by the persistence layer's constructor binding.
        Window = null!;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines proposal id for the current use case.</summary>
    public Guid? ProposalId { get; private set; }

    /// <summary>Defines window for the current use case.</summary>
    public SlotWindow Window { get; private set; }

    /// <summary>Defines status for the current use case.</summary>
    public ConfirmedSlotStatus Status { get; private set; } = ConfirmedSlotStatus.Active;

    /// <summary>Defines capacities for the current use case.</summary>
    public IReadOnlyList<SlotCapacity> Capacities => _capacities;

    /// <summary>
    /// The only way a confirmed slot is created. Marks the proposal confirmed in the same call, so
    /// a proposal can never back a second slot.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="proposal">The proposal.</param>
    public static ConfirmedSlot CreateFrom(Guid id, SlotProposal proposal)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(proposal is null, "proposal must be supplied.");

        proposal!.MarkConfirmed();

        var slot = new ConfirmedSlot
        {
            Id = id,
            ProposalId = proposal.Id,
            Window = proposal.Window,
            Status = ConfirmedSlotStatus.Active,
        };

        foreach (var acceptance in proposal.Acceptances.OrderBy(a => a.AppointmentTypeId))
        {
            slot._capacities.Add(
                SlotCapacity.Initialise(id, acceptance.AppointmentTypeId, acceptance.Headcount));
        }

        return slot;
    }

    /// <summary>
    /// Creates an active confirmed slot with no backing proposal from a strictly complete set of
    /// positive headcounts for the three fixed appointment types.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="window">The window.</param>
    /// <param name="headcountsByAppointmentType">The headcounts by appointment type.</param>
    public static ConfirmedSlot CreateImported(
        Guid id, SlotWindow window, IReadOnlyDictionary<Guid, int> headcountsByAppointmentType)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(window is null, "window must be supplied.");
        Guard.Against(headcountsByAppointmentType is null, "headcountsByAppointmentType must be supplied.");
        Guard.Against(
            headcountsByAppointmentType!.Count != AppointmentTypeIds.All.Count
            || headcountsByAppointmentType.Keys.Any(id => !AppointmentTypeIds.All.Contains(id)),
            "Headcounts must be supplied for exactly the three fixed appointment types.");

        var slot = new ConfirmedSlot
        {
            Id = id,
            ProposalId = null,
            Window = window!,
            Status = ConfirmedSlotStatus.Active,
        };

        foreach (var appointmentTypeId in AppointmentTypeIds.All)
        {
            Guard.Against(
                !headcountsByAppointmentType.TryGetValue(appointmentTypeId, out var headcount),
                $"A headcount is required for {AppointmentTypeIds.NameOf(appointmentTypeId)}.");

            slot._capacities.Add(SlotCapacity.Initialise(id, appointmentTypeId, headcount));
        }

        return slot;
    }

    /// <summary>Defines capacity for for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public SlotCapacity CapacityFor(Guid appointmentTypeId)
    {
        AppointmentTypeIds.EnsureKnown(appointmentTypeId);

        var capacity = _capacities.SingleOrDefault(c => c.AppointmentTypeId == appointmentTypeId);
        Guard.Against(capacity is null, $"This slot has no capacity counter for {appointmentTypeId}.");

        return capacity!;
    }

    /// <summary>Defines has spare capacity for all for the current use case.</summary>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public bool HasSpareCapacityForAll(IEnumerable<Guid> appointmentTypeIds) =>
        Status == ConfirmedSlotStatus.Active
        && appointmentTypeIds.All(id => CapacityFor(id).HasSpare);

    /// <summary>Defines cancel for the current use case.</summary>
    public void Cancel()
    {
        Guard.Against(Status == ConfirmedSlotStatus.Cancelled, "This slot has already been cancelled.");
        Status = ConfirmedSlotStatus.Cancelled;
    }
}
`````

## src/EventBooking.Domain/Slots/ConfirmedSlotStatus.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Slots/ConfirmedSlotStatus.cs","encoding":"utf8","sha256":"079fc1fb8d98f666830e76c490f752eb0b30c40c8c8e726b0a5061616b32c844","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Domain.Slots;

/// <summary>Defines confirmed slot status for the current use case.</summary>
public enum ConfirmedSlotStatus
{
    /// <summary>Defines active for the current use case.</summary>
    Active = 1,
    /// <summary>Defines cancelled for the current use case.</summary>
    Cancelled = 2,
}
`````

## src/EventBooking.Domain/Slots/ProposalAcceptance.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Slots/ProposalAcceptance.cs","encoding":"utf8","sha256":"56f058bd91c19cfa45b5fa92e9495a684b7a1bfc884e96f660aff045b8add3ea","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Slots;

/// <summary>One manager's acceptance of a proposal, carrying their own headcount.</summary>
public sealed class ProposalAcceptance
{
    private ProposalAcceptance()
    {
    }

    /// <summary>Defines proposal id for the current use case.</summary>
    public Guid ProposalId { get; private set; }

    /// <summary>Defines appointment type id for the current use case.</summary>
    public Guid AppointmentTypeId { get; private set; }

    /// <summary>Defines manager user id for the current use case.</summary>
    public Guid ManagerUserId { get; private set; }

    /// <summary>Defines headcount for the current use case.</summary>
    public int Headcount { get; private set; }

    internal static ProposalAcceptance Record(
        Guid proposalId,
        Guid appointmentTypeId,
        Guid managerUserId,
        int headcount)
    {
        Guard.Against(managerUserId == Guid.Empty, "managerUserId must not be empty.");

        return new ProposalAcceptance
        {
            ProposalId = proposalId,
            AppointmentTypeId = appointmentTypeId,
            ManagerUserId = managerUserId,
            Headcount = Guard.Positive(headcount, "headcount"),
        };
    }

    internal bool ChangeHeadcount(int headcount)
    {
        var next = Guard.Positive(headcount, "headcount");
        if (next == Headcount)
        {
            return false;
        }

        Headcount = next;
        return true;
    }
}
`````

## src/EventBooking.Domain/Slots/SlotCapacity.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Slots/SlotCapacity.cs","encoding":"utf8","sha256":"e917831f5ce494e22577cab38b0b3e1bd5b01098909f35a512893b3a7f166d32","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Slots;

/// <summary>
/// Remaining bookable headcount for one appointment type on one confirmed slot. This row is the
/// single source of truth for booking eligibility, and the row the confirm transaction locks.
/// </summary>
public sealed class SlotCapacity
{
    private SlotCapacity()
    {
    }

    /// <summary>Defines confirmed slot id for the current use case.</summary>
    public Guid ConfirmedSlotId { get; private set; }

    /// <summary>Defines appointment type id for the current use case.</summary>
    public Guid AppointmentTypeId { get; private set; }

    /// <summary>Defines total headcount for the current use case.</summary>
    public int TotalHeadcount { get; private set; }

    /// <summary>Defines remaining capacity for the current use case.</summary>
    public int RemainingCapacity { get; private set; }

    /// <summary>Defines has spare for the current use case.</summary>
    public bool HasSpare => RemainingCapacity > 0;

    /// <summary>Defines occupied capacity for the current use case.</summary>
    public int OccupiedCapacity => TotalHeadcount - RemainingCapacity;

    internal static SlotCapacity Initialise(Guid confirmedSlotId, Guid appointmentTypeId, int totalHeadcount)
    {
        AppointmentTypeIdsGuard(appointmentTypeId);

        var total = Guard.Positive(totalHeadcount, "totalHeadcount");

        return new SlotCapacity
        {
            ConfirmedSlotId = confirmedSlotId,
            AppointmentTypeId = appointmentTypeId,
            TotalHeadcount = total,
            RemainingCapacity = total,
        };
    }

    /// <summary>Defines decrement for the current use case.</summary>
    public void Decrement()
    {
        Guard.Against(
            RemainingCapacity <= 0,
            "No remaining capacity for this appointment type on this slot.");

        RemainingCapacity -= 1;
    }

    /// <summary>Defines increment for the current use case.</summary>
    public void Increment()
    {
        Guard.Against(
            RemainingCapacity >= TotalHeadcount,
            "Remaining capacity cannot exceed the headcount the manager accepted.");

        RemainingCapacity += 1;
    }

    /// <summary>Defines adjust total headcount for the current use case.</summary>
    /// <param name="totalHeadcount">The total headcount.</param>
    public bool AdjustTotalHeadcount(int totalHeadcount)
    {
        var next = Guard.Positive(totalHeadcount, "totalHeadcount");
        if (next == TotalHeadcount)
        {
            return false;
        }

        Guard.Against(
            next < OccupiedCapacity,
            "totalHeadcount cannot be lower than occupied capacity.");

        var delta = next - TotalHeadcount;
        TotalHeadcount = next;
        RemainingCapacity += delta;
        return true;
    }

    private static void AppointmentTypeIdsGuard(Guid appointmentTypeId) =>
        AppointmentTypes.AppointmentTypeIds.EnsureKnown(appointmentTypeId);
}
`````

## src/EventBooking.Domain/Slots/SlotProposal.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Slots/SlotProposal.cs","encoding":"utf8","sha256":"71f545967934064c041595534dbc62593155c120a5e453886f733ac3447043c2","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Domain.Slots;

/// <summary>Defines slot proposal for the current use case.</summary>
public sealed class SlotProposal
{
    private readonly List<ProposalAcceptance> _acceptances = [];

    private SlotProposal()
    {
        // Required by the persistence layer's constructor binding.
        Window = null!;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines window for the current use case.</summary>
    public SlotWindow Window { get; private set; }

    /// <summary>Defines status for the current use case.</summary>
    public SlotProposalStatus Status { get; private set; } = SlotProposalStatus.Open;

    /// <summary>Defines created by manager user id for the current use case.</summary>
    public Guid CreatedByManagerUserId { get; private set; }

    /// <summary>Defines acceptances for the current use case.</summary>
    public IReadOnlyList<ProposalAcceptance> Acceptances => _acceptances;

    /// <summary>Defines create for the current use case.</summary>
    /// <param name="id">The id.</param>
    /// <param name="window">The window.</param>
    /// <param name="createdByManagerUserId">The created by manager user id.</param>
    public static SlotProposal Create(Guid id, SlotWindow window, Guid createdByManagerUserId)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(window is null, "window must be supplied.");
        Guard.Against(createdByManagerUserId == Guid.Empty, "createdByManagerUserId must not be empty.");

        return new SlotProposal
        {
            Id = id,
            Window = window!,
            Status = SlotProposalStatus.Open,
            CreatedByManagerUserId = createdByManagerUserId,
        };
    }

    /// <summary>Withdraws an open proposal. Any Manager in scope or Admin may act, not just the creator.</summary>
    /// <param name="managerUserId">The manager user id.</param>
    public void Withdraw(Guid managerUserId)
    {
        Guard.Against(Status != SlotProposalStatus.Open, "Only an open proposal can be withdrawn.");
        Guard.Against(managerUserId == Guid.Empty, "managerUserId must not be empty.");

        Status = SlotProposalStatus.Withdrawn;
    }

    /// <summary>Defines accept for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    /// <param name="managerUserId">The manager user id.</param>
    /// <param name="headcount">The headcount.</param>
    public bool Accept(Guid appointmentTypeId, Guid managerUserId, int headcount)
    {
        Guard.Against(Status != SlotProposalStatus.Open, "Only an open proposal can be accepted.");
        AppointmentTypeIds.EnsureKnown(appointmentTypeId);

        var existing = _acceptances.SingleOrDefault(
            acceptance => acceptance.AppointmentTypeId == appointmentTypeId);

        if (existing is null)
        {
            _acceptances.Add(
                ProposalAcceptance.Record(Id, appointmentTypeId, managerUserId, headcount));
            return true;
        }

        return existing.ChangeHeadcount(headcount);
    }

    /// <summary>Defines withdraw acceptance for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    /// <param name="managerUserId">The manager user id.</param>
    public void WithdrawAcceptance(Guid appointmentTypeId, Guid managerUserId)
    {
        Guard.Against(
            Status != SlotProposalStatus.Open,
            "An acceptance can only be withdrawn while the proposal is still open.");
        AppointmentTypeIds.EnsureKnown(appointmentTypeId);

        var acceptance = _acceptances.SingleOrDefault(a => a.AppointmentTypeId == appointmentTypeId);
        Guard.Against(acceptance is null, "This appointment type has not accepted the proposal.");

        _acceptances.Remove(acceptance!);
    }

    /// <summary>Defines is accepted by for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public bool IsAcceptedBy(Guid appointmentTypeId) =>
        _acceptances.Any(a => a.AppointmentTypeId == appointmentTypeId);

    /// <summary>Defines is fully accepted for the current use case.</summary>
    public bool IsFullyAccepted =>
        Status == SlotProposalStatus.Open
        && AppointmentTypeIds.All.All(IsAcceptedBy);

    internal void MarkConfirmed()
    {
        Guard.Against(!IsFullyAccepted, "A proposal can only be confirmed once all 3 managers have accepted it.");
        Status = SlotProposalStatus.Confirmed;
    }
}
`````

## src/EventBooking.Domain/Slots/SlotProposalStatus.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Slots/SlotProposalStatus.cs","encoding":"utf8","sha256":"b432848d2379e93d3da7d95f3e108b2ddca5c70c05e873465bf09074eddeb8fb","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Domain.Slots;

/// <summary>Defines slot proposal status for the current use case.</summary>
public enum SlotProposalStatus
{
    /// <summary>Defines open for the current use case.</summary>
    Open = 1,
    /// <summary>Defines withdrawn for the current use case.</summary>
    Withdrawn = 2,
    /// <summary>Defines confirmed for the current use case.</summary>
    Confirmed = 3,
}
`````

## src/EventBooking.Domain/Slots/SlotWindow.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Slots/SlotWindow.cs","encoding":"utf8","sha256":"915ec92574247cfff33ff6c0654fdaf8a35480937bcf3f32e564c7e14873db85","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Slots;

/// <summary>
/// The 4-hour candidate-facing window. Duration is fixed by the domain, so only the date and the
/// start time are ever stored; the end time is always derived.
/// </summary>
public sealed record SlotWindow : IComparable<SlotWindow>
{
    /// <summary>Defines duration for the current use case.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromHours(4);

    /// <summary>Defines slot window for the current use case.</summary>
    /// <param name="date">The date.</param>
    /// <param name="startTime">The start time.</param>
    public SlotWindow(DateOnly date, TimeOnly startTime)
    {
        Guard.Against(
            startTime.ToTimeSpan() + Duration > TimeSpan.FromHours(24),
            "startTime must leave room for the full 4-hour window on the same day.");

        Date = date;
        StartTime = startTime;
    }

    /// <summary>Defines date for the current use case.</summary>
    public DateOnly Date { get; }

    /// <summary>Defines start time for the current use case.</summary>
    public TimeOnly StartTime { get; }

    /// <summary>Defines end time for the current use case.</summary>
    public TimeOnly EndTime => StartTime.Add(Duration);

    /// <summary>Defines starts after for the current use case.</summary>
    /// <param name="today">The today.</param>
    public bool StartsAfter(DateOnly today) => Date > today;

    /// <summary>Defines compare to for the current use case.</summary>
    /// <param name="other">The other.</param>
    public int CompareTo(SlotWindow? other)
    {
        if (other is null)
        {
            return 1;
        }

        var byDate = Date.CompareTo(other.Date);
        return byDate != 0 ? byDate : StartTime.CompareTo(other.StartTime);
    }

    /// <summary>Defines to string for the current use case.</summary>
    public override string ToString() =>
        $"{Date:yyyy-MM-dd} {StartTime:HH\\:mm}-{EndTime:HH\\:mm}";
}
`````

## src/EventBooking.Infrastructure/Audit/EfAuditLogger.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Audit/EfAuditLogger.cs","encoding":"utf8","sha256":"1ea1a32c042a75ca6c7b46c5a78f35e7b72a1953adc41ece19e733edcb7dc96d","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Audit;
using EventBooking.Infrastructure.Persistence;

namespace EventBooking.Infrastructure.Audit;

/// <summary>
/// Stages the audit row on the caller's own context, so it commits with the change it describes.
/// </summary>
public sealed class EfAuditLogger(EventBookingDbContext context, IClock clock) : IAuditLogger
{
    public void Record(
        string entityType,
        Guid entityId,
        AuditAction action,
        ActorType actorType,
        string? actorId,
        string? details = null)
    {
        context.AuditLogs.Add(AuditLog.Record(
            Guid.NewGuid(), entityType, entityId, action, actorType, actorId, clock.UtcNow, details));
    }
}
`````

## src/EventBooking.Infrastructure/DependencyInjection.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/DependencyInjection.cs","encoding":"utf8","sha256":"56e223322fb4ff37a3ead1219c9f1abeccce7e160732fc1655fd32bd4cb0c9ec","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using EventBooking.Infrastructure.Persistence.Repositories;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddEventBookingPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        // Registered as a factory, with a scoped context created from it. Task 51's email sender
        // needs a context of its own that is not tied to the request's unit of work, and this is
        // the pattern that gives it one without a second registration of the context type.
        services.AddSingleton<StatusStampingInterceptor>();
        services.AddDbContextFactory<EventBookingDbContext>((sp, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(sp.GetRequiredService<StatusStampingInterceptor>()));
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<EventBookingDbContext>>().CreateDbContext());

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAppointmentTypeRepository, AppointmentTypeRepository>();
        services.AddScoped<ISystemSettingsRepository, SystemSettingsRepository>();
        services.AddScoped<ISlotProposalRepository, SlotProposalRepository>();
        services.AddScoped<IConfirmedSlotRepository, ConfirmedSlotRepository>();
        services.AddScoped<ISlotCapacityRepository, SlotCapacityRepository>();
        services.AddScoped<ICandidateRepository, CandidateRepository>();
        services.AddScoped<IEmployeeGroupRepository, EmployeeGroupRepository>();
        services.AddScoped<IInviteRepository, InviteRepository>();
        services.AddScoped<IEmailDeliveryRepository, EmailDeliveryRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IBookingAppointmentRepository, BookingAppointmentRepository>();
        services.AddScoped<IStaffAccessProfileRepository, StaffAccessProfileRepository>();
        services.AddScoped<IStaffIdentityRepository, StaffIdentityRepository>();
        services.AddScoped<IDashboardQueries, DashboardQueries>();
        services.AddScoped<IAuditQueries, AuditQueries>();
        services.AddScoped<IAppointmentWorkspaceQueries, AppointmentWorkspaceQueries>();
        services.AddScoped<ICandidateReadinessQueries, CandidateReadinessQueries>();
        services.AddScoped<ICandidateBookingQueries, CandidateBookingQueries>();

        return services;
    }

    public static IServiceCollection AddEventBookingInfrastructure(
        this IServiceCollection services,
        string connectionString,
        HeadOfficeOptions headOffice,
        TokenOptions tokens)
    {
        services.AddEventBookingPersistence(connectionString);

        services.AddSingleton(headOffice);
        services.AddSingleton(tokens);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITokenService, HmacTokenService>();

        services.AddScoped<IEmailSender, LoggingEmailSender>();

        services.AddScoped<IAuditLogger, EfAuditLogger>();

        return services;
    }
}
`````

## src/EventBooking.Infrastructure/Email/EmailOptions.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Email/EmailOptions.cs","encoding":"utf8","sha256":"c407e1d73ede456f713147cda63ac0351a809a0defe26bbdf1bf53e227e6aa9f","parts":1,"part":1} -->

`````csharp
using System.Net.Mail;

namespace EventBooking.Infrastructure.Email;

/// <summary>Which mail provider is configured for this deployment.</summary>
public enum EmailProvider { Smtp }

/// <summary>The verified identity messages are sent from, and which provider sends them.</summary>
public sealed record EmailOptions
{
    public EmailOptions(string fromAddress, string fromName, EmailProvider provider)
    {
        if (string.IsNullOrWhiteSpace(fromAddress) || !IsMailboxAddress(fromAddress))
        {
            throw new ArgumentException("A sender address is required.", nameof(fromAddress));
        }

        if (string.IsNullOrWhiteSpace(fromName))
        {
            throw new ArgumentException("A sender name is required.", nameof(fromName));
        }

        FromAddress = fromAddress;
        FromName = fromName;
        Provider = provider;
    }

    public string FromAddress { get; }

    public string FromName { get; }

    public EmailProvider Provider { get; }

    public override string ToString() => "EmailOptions { Sender = [REDACTED] }";

    private static bool IsMailboxAddress(string value)
    {
        try
        {
            return string.Equals(new MailAddress(value).Address, value, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
`````

## src/EventBooking.Infrastructure/Email/IEmailTransport.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Email/IEmailTransport.cs","encoding":"utf8","sha256":"59606a9f0015a0043d209eeb78fbd4b82b74c0103e91d7f5e3fbc03a9ae41794","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;

namespace EventBooking.Infrastructure.Email;

/// <summary>
/// The one-method seam over the mail provider. Throws when the provider rejects the message; the
/// sender above it turns that into a logged failure.
/// </summary>
public interface IEmailTransport
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
`````

## src/EventBooking.Infrastructure/Email/LocalInfrastructureExtensions.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Email/LocalInfrastructureExtensions.cs","encoding":"utf8","sha256":"d679e115ace008b0800dc9998c6f90b2815465a254ebf6ada52996bfb145ef79","parts":1,"part":1} -->

`````csharp
using EventBooking.Infrastructure.Email;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Infrastructure.Email;

public static class LocalInfrastructureExtensions
{
    public static IServiceCollection AddLocalEmailTransport(
        this IServiceCollection services, EmailOptions options, SmtpOptions smtp)
    {
        services.AddSingleton(options);
        services.AddSingleton(smtp);
        services.AddScoped<IEmailTransport, SmtpEmailTransport>();

        return services;
    }
}
`````

## src/EventBooking.Infrastructure/Email/LoggingEmailSender.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Email/LoggingEmailSender.cs","encoding":"utf8","sha256":"6eab1f5ceb7399d58887cebfabdba939c9efe3c388c1e44398b5bbc54b1d0c4f","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventBooking.Infrastructure.Email;

/// <summary>
/// Sends through the transport and records one email log row either way. Returns false rather than
/// throwing: a bounced confirmation must never roll back a good booking.
/// </summary>
public sealed class LoggingEmailSender(
    IEmailTransport transport,
    IDbContextFactory<EventBookingDbContext> contextFactory,
    IClock clock,
    ILogger<LoggingEmailSender> logger) : IEmailSender
{
    /// <summary>
    /// Sends through the configured transport and returns a provider outcome. Coordinated durable
    /// deliveries already own their <see cref="EmailLog"/> row and therefore are not logged twice.
    /// </summary>
    public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var status = EmailStatus.Sent;

        try
        {
            await transport.SendAsync(message, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            status = EmailStatus.Failed;
            logger.LogError(
                ex,
                "Sending the {Template} email to candidate {CandidateId} failed.",
                message.Template,
                message.CandidateId);
        }

        if (message.DeliveryId is null)
        {
            await RecordAsync(message, status, cancellationToken);
        }

        return status == EmailStatus.Sent;
    }

    private async Task RecordAsync(
        EmailMessage message,
        EmailStatus status,
        CancellationToken cancellationToken)
    {
        try
        {
            // A context of its own: the confirmation email is sent after its booking transaction
            // has already committed, so there is no unit of work left to save this row on.
            await using var context = contextFactory.CreateDbContext();

            context.EmailLogs.Add(EmailLog.Record(
                Guid.NewGuid(), message.CandidateId, message.Template, clock.UtcNow, status));

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Losing the audit row is bad, but not as bad as failing the caller's operation for it.
            logger.LogWarning(ex, "Writing the email log row failed.");
        }
    }
}
`````

## src/EventBooking.Infrastructure/Email/SmtpEmailTransport.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Email/SmtpEmailTransport.cs","encoding":"utf8","sha256":"7c132e41ce1334a7f1ee43b82e5d74ca5a1e9facf03947e221917e828310b320","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure.Email;
using MailKit.Net.Smtp;
using MimeKit;

namespace EventBooking.Infrastructure.Email;

/// <summary>
/// The only place in the system that touches MailKit. No branching lives here on purpose: anything
/// worth testing belongs one layer up, in the sender. Mailpit accepts unauthenticated, unencrypted
/// SMTP, so no credentials or TLS options are configured.
/// </summary>
public sealed class SmtpEmailTransport(SmtpOptions options, EmailOptions email) : IEmailTransport
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(email.FromName, email.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.ToAddress));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder
        {
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody,
        }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(options.Host, options.Port, false, cancellationToken);
        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
`````

## src/EventBooking.Infrastructure/Email/SmtpOptions.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Email/SmtpOptions.cs","encoding":"utf8","sha256":"e50ae64fb3c1e4d4b63b1e2a683a66d77b5919cab16964e3cb04500251f1e19b","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Infrastructure.Email;

/// <summary>The local SMTP catcher's address — Mailpit in Docker Compose.</summary>
public sealed record SmtpOptions(string Host, int Port);
`````
