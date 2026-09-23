using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Invites;

/// <summary>An offer of event options carrying an immutable requirement snapshot.</summary>
public sealed class Invite
{
    /// <summary>Gets the number of event options every invite offers.</summary>
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

    /// <summary>Gets the invited attendee identifier.</summary>
    public Guid AttendeeId { get; private set; }

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

    /// <summary>Gets the offered event options.</summary>
    public IReadOnlyList<InviteOption> Options => _options;

    /// <summary>Gets the offered event identifiers.</summary>
    public IReadOnlyList<Guid> OfferedEventIds => _options.Select(o => o.EventId).ToList();

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
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="tokenHash">The token hash.</param>
    /// <param name="expiresAt">The expires at.</param>
    /// <param name="eventIds">The event ids.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="retryCount">The retry count.</param>
    public static Invite CreateInitial(
        Guid id,
        Guid attendeeId,
        string? tokenHash,
        DateTimeOffset expiresAt,
        IEnumerable<Guid> eventIds,
        IEnumerable<Guid> appointmentTypeIds,
        int retryCount) =>
        Create(id, attendeeId, null, tokenHash, expiresAt, eventIds, appointmentTypeIds, retryCount);

    /// <summary>Creates a recovery invite snapshotting only recoverable no-show types.</summary>
    /// <param name="id">The id.</param>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="recoveryOfBookingId">The recovery of booking id.</param>
    /// <param name="tokenHash">The token hash.</param>
    /// <param name="expiresAt">The expires at.</param>
    /// <param name="eventIds">The event ids.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public static Invite CreateRecovery(
        Guid id,
        Guid attendeeId,
        Guid recoveryOfBookingId,
        string? tokenHash,
        DateTimeOffset expiresAt,
        IEnumerable<Guid> eventIds,
        IEnumerable<Guid> appointmentTypeIds)
    {
        Guard.Against(recoveryOfBookingId == Guid.Empty, "recoveryOfBookingId must not be empty.");
        return Create(
            id, attendeeId, recoveryOfBookingId, tokenHash, expiresAt,
            eventIds, appointmentTypeIds, 0);
    }

    /// <summary>Determines whether the invite can still be used at the supplied instant.</summary>
    /// <param name="now">The now.</param>
    public bool IsUsableAt(DateTimeOffset now) =>
        Status == InviteStatus.Pending && now < ExpiresAt;

    /// <summary>Determines whether the invite offers the supplied eventItem.</summary>
    /// <param name="eventId">The event id.</param>
    public bool Offers(Guid eventId) =>
        _options.Any(o => o.EventId == eventId);

    /// <summary>Removes one offered event from a pending invite.</summary>
    /// <param name="eventId">The event id.</param>
    public void RemoveOption(Guid eventId)
    {
        EnsurePending("Only a pending invite's options can change.");

        var option = _options.SingleOrDefault(o => o.EventId == eventId);
        Guard.Against(option is null, "This invite does not offer that eventItem.");

        _options.Remove(option!);
    }

    /// <summary>Adds one offered event to a pending invite.</summary>
    /// <param name="eventId">The event id.</param>
    public void AddOption(Guid eventId)
    {
        EnsurePending("Only a pending invite's options can change.");
        Guard.Against(
            _options.Count >= RequiredOptionCount,
            $"An invite cannot offer more than {RequiredOptionCount} event options.");
        Guard.Against(Offers(eventId), "An invite cannot offer the same event twice.");

        _options.Add(InviteOption.For(Id, eventId));
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
        Guid attendeeId,
        Guid? recoveryOfBookingId,
        string? tokenHash,
        DateTimeOffset expiresAt,
        IEnumerable<Guid> eventIds,
        IEnumerable<Guid> appointmentTypeIds,
        int retryCount)
    {
        var invite = CreateCore(id, attendeeId, recoveryOfBookingId, tokenHash, expiresAt, eventIds, retryCount);

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
        Guid attendeeId,
        Guid? recoveryOfBookingId,
        string? tokenHash,
        DateTimeOffset expiresAt,
        IEnumerable<Guid> eventIds,
        int retryCount)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(attendeeId == Guid.Empty, "attendeeId must not be empty.");

        var offeredEventIds = eventIds.ToList();
        Guard.Against(
            offeredEventIds.Count != RequiredOptionCount,
            $"An invite must offer exactly {RequiredOptionCount} event options.");
        Guard.Against(
            offeredEventIds.Distinct().Count() != offeredEventIds.Count,
            "An invite cannot offer the same event twice.");

        var invite = new Invite
        {
            Id = id,
            AttendeeId = attendeeId,
            RecoveryOfBookingId = recoveryOfBookingId,
            TokenHash = Guard.NotBlank(tokenHash, "tokenHash"),
            ExpiresAt = expiresAt,
            Status = InviteStatus.Pending,
            RetryCount = Guard.NotNegative(retryCount, "retryCount"),
        };

        foreach (var eventId in eventIds)
        {
            invite._options.Add(InviteOption.For(id, eventId));
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
