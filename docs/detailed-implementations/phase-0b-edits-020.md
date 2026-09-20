# 00b — Vocabulary edits 20 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — src/EventBooking.Domain/AttendeeGroups/AttendeeGroupIds.cs — 1/1

<!-- vocabulary-file: {"id":105,"oldPath":"src/EventBooking.Domain/EmployeeGroups/EmployeeGroupIds.cs","newPath":"src/EventBooking.Domain/AttendeeGroups/AttendeeGroupIds.cs","beforeSha":"c38b4a39e8d86dafc0f4e5806a777da06eb7ecbd2747f3b3a68dab224bd25f0b","afterSha":"09982a7f58d754d7e37ffbe93d702f99ca7abd0c3c4fedd349a21d09b42ea0bc","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.AttendeeGroups;

/// <summary>Stable identifiers and canonical codes for the five approved Attendee Groups.</summary>
public static class AttendeeGroupIds
{
    /// <summary>Gets the stable identifier for Cabin Crew.</summary>
    public static readonly Guid CabinCrew = Guid.Parse("e0000001-0000-0000-0000-000000000001");

    /// <summary>Gets the stable identifier for Pilots.</summary>
    public static readonly Guid Pilots = Guid.Parse("e0000002-0000-0000-0000-000000000002");

    /// <summary>Gets the stable identifier for Ground Operations Agent.</summary>
    public static readonly Guid GroundOperationsAgent = Guid.Parse("e0000003-0000-0000-0000-000000000003");

    /// <summary>Gets the stable identifier for Engineering.</summary>
    public static readonly Guid Engineering = Guid.Parse("e0000004-0000-0000-0000-000000000004");

    /// <summary>Gets the stable identifier for Ground Transport Services.</summary>
    public static readonly Guid GroundTransportServices = Guid.Parse("e0000005-0000-0000-0000-000000000005");

    private static readonly Dictionary<string, Guid> CodeToId = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CABIN_CREW"] = CabinCrew,
        ["PILOTS"] = Pilots,
        ["GROUND_OPERATIONS_AGENT"] = GroundOperationsAgent,
        ["ENGINEERING"] = Engineering,
        ["GROUND_TRANSPORT_SERVICES"] = GroundTransportServices,
    };

    private static readonly Dictionary<Guid, string> IdToCode = new()
    {
        [CabinCrew] = "CABIN_CREW",
        [Pilots] = "PILOTS",
        [GroundOperationsAgent] = "GROUND_OPERATIONS_AGENT",
        [Engineering] = "ENGINEERING",
        [GroundTransportServices] = "GROUND_TRANSPORT_SERVICES",
    };

    /// <summary>Gets every approved Attendee Group identifier.</summary>
    public static IReadOnlyCollection<Guid> All { get; } =
    [
        CabinCrew,
        Pilots,
        GroundOperationsAgent,
        Engineering,
        GroundTransportServices,
    ];

    /// <summary>Gets every approved canonical Attendee Group code.</summary>
    public static IReadOnlyCollection<string> Codes => CodeToId.Keys;

    /// <summary>Tries to resolve a trimmed case-insensitive canonical-code input to its identifier.</summary>
    /// <param name="code">The code.</param>
    /// <param name="id">The id.</param>
    public static bool TryFromCode(string? code, out Guid id)
    {
        id = Guid.Empty;

        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        return CodeToId.TryGetValue(code.Trim(), out id);
    }

    /// <summary>Gets the canonical code for a known Attendee Group identifier.</summary>
    /// <param name="id">The id.</param>
    public static string CodeOf(Guid id) =>
        IdToCode.TryGetValue(id, out var code)
            ? code
            : throw new DomainException($"{id} is not one of the 5 attendee groups.");

    /// <summary>Ensures the identifier names a known Attendee Group.</summary>
    /// <param name="id">The id.</param>
    public static void EnsureKnown(Guid id)
    {
        Guard.Against(!IdToCode.ContainsKey(id), $"{id} is not one of the 5 attendee groups.");
    }
}
`````

## before — src/EventBooking.Domain/EmployeeGroups/EmployeeGroupRequirement.cs — 1/1

<!-- vocabulary-file: {"id":106,"oldPath":"src/EventBooking.Domain/EmployeeGroups/EmployeeGroupRequirement.cs","newPath":"src/EventBooking.Domain/AttendeeGroups/AttendeeGroupRequirement.cs","beforeSha":"eefda867b3ee179c1a0174573718ad0a3fbd7a575327ba63390b47fc60529772","afterSha":"63dfcdd861635fd01c2d187971aef225c7af94b80ad6752b332e84157e5b9c45","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.EmployeeGroups;

/// <summary>One fixed Appointment Type required by an Employee Group.</summary>
public sealed class EmployeeGroupRequirement
{
    private EmployeeGroupRequirement()
    {
    }

    /// <summary>Gets the owning Employee Group identifier.</summary>
    public Guid EmployeeGroupId { get; private set; }

    /// <summary>Gets the required fixed Appointment Type identifier.</summary>
    public Guid AppointmentTypeId { get; private set; }

    internal static EmployeeGroupRequirement For(Guid employeeGroupId, Guid appointmentTypeId) =>
        new() { EmployeeGroupId = employeeGroupId, AppointmentTypeId = appointmentTypeId };
}
`````

## after — src/EventBooking.Domain/AttendeeGroups/AttendeeGroupRequirement.cs — 1/1

<!-- vocabulary-file: {"id":106,"oldPath":"src/EventBooking.Domain/EmployeeGroups/EmployeeGroupRequirement.cs","newPath":"src/EventBooking.Domain/AttendeeGroups/AttendeeGroupRequirement.cs","beforeSha":"eefda867b3ee179c1a0174573718ad0a3fbd7a575327ba63390b47fc60529772","afterSha":"63dfcdd861635fd01c2d187971aef225c7af94b80ad6752b332e84157e5b9c45","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.AttendeeGroups;

/// <summary>One fixed Appointment Type required by an Attendee Group.</summary>
public sealed class AttendeeGroupRequirement
{
    private AttendeeGroupRequirement()
    {
    }

    /// <summary>Gets the owning Attendee Group identifier.</summary>
    public Guid AttendeeGroupId { get; private set; }

    /// <summary>Gets the required fixed Appointment Type identifier.</summary>
    public Guid AppointmentTypeId { get; private set; }

    internal static AttendeeGroupRequirement For(Guid attendeeGroupId, Guid appointmentTypeId) =>
        new() { AttendeeGroupId = attendeeGroupId, AppointmentTypeId = appointmentTypeId };
}
`````

## before — src/EventBooking.Domain/Invites/Invite.cs — 1/1

<!-- vocabulary-file: {"id":107,"oldPath":"src/EventBooking.Domain/Invites/Invite.cs","newPath":"src/EventBooking.Domain/Invites/Invite.cs","beforeSha":"2c34b37c359d4ec5bc3c03a363e9398f7ff9957cf47d9f2a46dee7e3daa4d26e","afterSha":"3df352dcc48b474087847be09920cc882eaba4c5dfbbddaee177c8cc9b1c9188","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Domain/Invites/Invite.cs — 1/1

<!-- vocabulary-file: {"id":107,"oldPath":"src/EventBooking.Domain/Invites/Invite.cs","newPath":"src/EventBooking.Domain/Invites/Invite.cs","beforeSha":"2c34b37c359d4ec5bc3c03a363e9398f7ff9957cf47d9f2a46dee7e3daa4d26e","afterSha":"3df352dcc48b474087847be09920cc882eaba4c5dfbbddaee177c8cc9b1c9188","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## before — src/EventBooking.Domain/Invites/InviteOption.cs — 1/1

<!-- vocabulary-file: {"id":108,"oldPath":"src/EventBooking.Domain/Invites/InviteOption.cs","newPath":"src/EventBooking.Domain/Invites/InviteOption.cs","beforeSha":"e11d7bf184372b2c5e1c3978d21b86f3098435aa524596cd1afd3074182d0dd8","afterSha":"6c21e0d1c6059c52baef23ff538ef1f0aedaca81eef7b579ce80f0e390aa18b1","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Domain/Invites/InviteOption.cs — 1/1

<!-- vocabulary-file: {"id":108,"oldPath":"src/EventBooking.Domain/Invites/InviteOption.cs","newPath":"src/EventBooking.Domain/Invites/InviteOption.cs","beforeSha":"e11d7bf184372b2c5e1c3978d21b86f3098435aa524596cd1afd3074182d0dd8","afterSha":"6c21e0d1c6059c52baef23ff538ef1f0aedaca81eef7b579ce80f0e390aa18b1","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Invites;

/// <summary>One of the events offered inside an invite.</summary>
public sealed class InviteOption
{
    private InviteOption()
    {
    }

    /// <summary>Defines invite id for the current use case.</summary>
    public Guid InviteId { get; private set; }

    /// <summary>Defines event id for the current use case.</summary>
    public Guid EventId { get; private set; }

    internal static InviteOption For(Guid inviteId, Guid eventId) =>
        new() { InviteId = inviteId, EventId = eventId };
}
`````

## before — src/EventBooking.Domain/Invites/InviteStatus.cs — 1/1

<!-- vocabulary-file: {"id":109,"oldPath":"src/EventBooking.Domain/Invites/InviteStatus.cs","newPath":"src/EventBooking.Domain/Invites/InviteStatus.cs","beforeSha":"e85b6e10429bbd50567c7411758f247fa8fcead392db220d7d2baf30afb05281","afterSha":"cca8c2e8ba1bf0d7bb8fcd477ca4d4d77b0a71f6b27adae9e056c3d042154d15","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Domain/Invites/InviteStatus.cs — 1/1

<!-- vocabulary-file: {"id":109,"oldPath":"src/EventBooking.Domain/Invites/InviteStatus.cs","newPath":"src/EventBooking.Domain/Invites/InviteStatus.cs","beforeSha":"e85b6e10429bbd50567c7411758f247fa8fcead392db220d7d2baf30afb05281","afterSha":"cca8c2e8ba1bf0d7bb8fcd477ca4d4d77b0a71f6b27adae9e056c3d042154d15","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Invites;

/// <summary>Where an invite sits in its offer lifecycle.</summary>
public enum InviteStatus
{
    /// <summary>Awaiting a attendee response.</summary>
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

## before — src/EventBooking.Domain/Notifications/EmailLog.cs — 1/1

<!-- vocabulary-file: {"id":110,"oldPath":"src/EventBooking.Domain/Notifications/EmailLog.cs","newPath":"src/EventBooking.Domain/Notifications/EmailLog.cs","beforeSha":"6aecccdb8b6ad62d2bd578610609378b89f1b34d15dd262036dfa75f751fd98a","afterSha":"d1fca126627655b6034d20628b72d3e8c2a78b059c5c9e5ccf639bcc07486469","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Domain/Notifications/EmailLog.cs — 1/1

<!-- vocabulary-file: {"id":110,"oldPath":"src/EventBooking.Domain/Notifications/EmailLog.cs","newPath":"src/EventBooking.Domain/Notifications/EmailLog.cs","beforeSha":"6aecccdb8b6ad62d2bd578610609378b89f1b34d15dd262036dfa75f751fd98a","afterSha":"d1fca126627655b6034d20628b72d3e8c2a78b059c5c9e5ccf639bcc07486469","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Notifications;

/// <summary>
/// A durable record of one attendee email delivery attempt. Its identity and safe context are
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

    /// <summary>The attendee who is the recipient of this delivery attempt.</summary>
    public Guid AttendeeId { get; private set; }

    /// <summary>The attendee-facing template this attempt renders.</summary>
    public EmailTemplate TemplateName { get; private set; }

    /// <summary>The timestamp of the current or most recent attempt, supplied by <c>IClock</c>.</summary>
    public DateTimeOffset SentAt { get; private set; }

    /// <summary>The durable provider outcome, including <see cref="EmailStatus.Pending"/>.</summary>
    public EmailStatus Status { get; private set; }

    /// <summary>The invite context used by invite and re-invite templates, when applicable.</summary>
    public Guid? InviteId { get; private set; }

    /// <summary>The booking context used by a booking-confirmation template, when applicable.</summary>
    public Guid? BookingId { get; private set; }

    /// <summary>The event context used by a cancellation template, when applicable.</summary>
    public Guid? EventId { get; private set; }

    /// <summary>The in-progress claim timestamp used to prevent duplicate concurrent sends.</summary>
    public DateTimeOffset? ClaimedAt { get; private set; }

    /// <summary>Creates a legacy email attempt without a regeneration context.</summary>
    /// <param name="id">The id.</param>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="templateName">The template name.</param>
    /// <param name="sentAt">The sent at.</param>
    /// <param name="status">The status.</param>
    public static EmailLog Record(
        Guid id,
        Guid attendeeId,
        EmailTemplate templateName,
        DateTimeOffset sentAt,
        EmailStatus status)
        => PendingOrRecorded(id, attendeeId, templateName, sentAt, status, null, null, null);

    /// <summary>
    /// Creates a pending delivery with only safe context identifiers. The caller saves it in the
    /// same business transaction as the state change that caused the notification.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="templateName">The template name.</param>
    /// <param name="createdAt">The created at.</param>
    /// <param name="inviteId">The invite id.</param>
    /// <param name="bookingId">The booking id.</param>
    /// <param name="eventId">The event id.</param>
    public static EmailLog RecordPending(
        Guid id,
        Guid attendeeId,
        EmailTemplate templateName,
        DateTimeOffset createdAt,
        Guid? inviteId = null,
        Guid? bookingId = null,
        Guid? eventId = null)
        => PendingOrRecorded(
            id,
            attendeeId,
            templateName,
            createdAt,
            EmailStatus.Pending,
            inviteId,
            bookingId,
            eventId);

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
        Guid attendeeId,
        EmailTemplate templateName,
        DateTimeOffset sentAt,
        EmailStatus status,
        Guid? inviteId,
        Guid? bookingId,
        Guid? eventId)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(attendeeId == Guid.Empty, "attendeeId must not be empty.");

        return new EmailLog
        {
            Id = id,
            AttendeeId = attendeeId,
            TemplateName = templateName,
            SentAt = sentAt,
            Status = status,
            InviteId = inviteId,
            BookingId = bookingId,
            EventId = eventId,
        };
    }
}
`````

## before — src/EventBooking.Domain/Notifications/EmailStatus.cs — 1/1

<!-- vocabulary-file: {"id":111,"oldPath":"src/EventBooking.Domain/Notifications/EmailStatus.cs","newPath":"src/EventBooking.Domain/Notifications/EmailStatus.cs","beforeSha":"366a7979a37a4f9b5e2b82fd1ed36e343bfb5dda9d712a58fdef652d8faac587","afterSha":"b47f046e1630b69b4c83e2aa534f29a2d3bd9dde97961c26173f973913444a0c","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Domain/Notifications/EmailStatus.cs — 1/1

<!-- vocabulary-file: {"id":111,"oldPath":"src/EventBooking.Domain/Notifications/EmailStatus.cs","newPath":"src/EventBooking.Domain/Notifications/EmailStatus.cs","beforeSha":"366a7979a37a4f9b5e2b82fd1ed36e343bfb5dda9d712a58fdef652d8faac587","afterSha":"b47f046e1630b69b4c83e2aa534f29a2d3bd9dde97961c26173f973913444a0c","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Notifications;

/// <summary>Durable outcome of one attendee email delivery attempt.</summary>
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

## before — src/EventBooking.Domain/Notifications/EmailTemplate.cs — 1/1

<!-- vocabulary-file: {"id":112,"oldPath":"src/EventBooking.Domain/Notifications/EmailTemplate.cs","newPath":"src/EventBooking.Domain/Notifications/EmailTemplate.cs","beforeSha":"7d8df8d205845aa2dd649ed34fb8f8cfce51683c88d09882412d16dfe01f8fb0","afterSha":"86883102b1251f66816cd6c3038228f44d5fd24217f17c788022321153e6af53","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Domain/Notifications/EmailTemplate.cs — 1/1

<!-- vocabulary-file: {"id":112,"oldPath":"src/EventBooking.Domain/Notifications/EmailTemplate.cs","newPath":"src/EventBooking.Domain/Notifications/EmailTemplate.cs","beforeSha":"7d8df8d205845aa2dd649ed34fb8f8cfce51683c88d09882412d16dfe01f8fb0","afterSha":"86883102b1251f66816cd6c3038228f44d5fd24217f17c788022321153e6af53","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Notifications;

/// <summary>Defines email template for the current use case.</summary>
public enum EmailTemplate
{
    /// <summary>Defines attendee invite for the current use case.</summary>
    AttendeeInvite = 1,
    /// <summary>Defines booking confirmation for the current use case.</summary>
    BookingConfirmation = 2,
    /// <summary>Defines event cancelled rebooking needed for the current use case.</summary>
    EventCancelledRebookingNeeded = 3,
    /// <summary>Defines attendee reinvite for the current use case.</summary>
    AttendeeReinvite = 4,
}
`````

## before — src/EventBooking.Domain/Slots/ConfirmedSlot.cs — 1/1

<!-- vocabulary-file: {"id":113,"oldPath":"src/EventBooking.Domain/Slots/ConfirmedSlot.cs","newPath":"src/EventBooking.Domain/Events/Event.cs","beforeSha":"1f5aaebd3b1466db955c03cca49d20a2e4307b13ad1dd3c1531f731ce1175d62","afterSha":"50534079eb95597aaef7496e64b85f87f25b3927d7655c6ebfd542d338ebd094","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Domain/Events/Event.cs — 1/1

<!-- vocabulary-file: {"id":113,"oldPath":"src/EventBooking.Domain/Slots/ConfirmedSlot.cs","newPath":"src/EventBooking.Domain/Events/Event.cs","beforeSha":"1f5aaebd3b1466db955c03cca49d20a2e4307b13ad1dd3c1531f731ce1175d62","afterSha":"50534079eb95597aaef7496e64b85f87f25b3927d7655c6ebfd542d338ebd094","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Events;

/// <summary>Defines event for the current use case.</summary>
public sealed class Event
{
    private readonly List<EventCapacity> _capacities = [];

    private Event()
    {
        // Required by the persistence layer's constructor binding.
        Window = null!;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines proposal id for the current use case.</summary>
    public Guid? ProposalId { get; private set; }

    /// <summary>Defines window for the current use case.</summary>
    public EventWindow Window { get; private set; }

    /// <summary>Defines status for the current use case.</summary>
    public EventStatus Status { get; private set; } = EventStatus.Active;

    /// <summary>Defines capacities for the current use case.</summary>
    public IReadOnlyList<EventCapacity> Capacities => _capacities;

    /// <summary>
    /// The only way a eventItem is created. Marks the proposal confirmed in the same call, so
    /// a proposal can never back a second eventItem.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="proposal">The proposal.</param>
    public static Event CreateFrom(Guid id, EventProposal proposal)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(proposal is null, "proposal must be supplied.");

        proposal!.MarkConfirmed();

        var eventItem = new Event
        {
            Id = id,
            ProposalId = proposal.Id,
            Window = proposal.Window,
            Status = EventStatus.Active,
        };

        foreach (var acceptance in proposal.Acceptances.OrderBy(a => a.AppointmentTypeId))
        {
            eventItem._capacities.Add(
                EventCapacity.Initialise(id, acceptance.AppointmentTypeId, acceptance.Headcount));
        }

        return eventItem;
    }

    /// <summary>
    /// Creates an active event with no backing proposal from a strictly complete set of
    /// positive headcounts for the three fixed appointment types.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="window">The window.</param>
    /// <param name="headcountsByAppointmentType">The headcounts by appointment type.</param>
    public static Event CreateImported(
        Guid id, EventWindow window, IReadOnlyDictionary<Guid, int> headcountsByAppointmentType)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(window is null, "window must be supplied.");
        Guard.Against(headcountsByAppointmentType is null, "headcountsByAppointmentType must be supplied.");
        Guard.Against(
            headcountsByAppointmentType!.Count != AppointmentTypeIds.All.Count
            || headcountsByAppointmentType.Keys.Any(id => !AppointmentTypeIds.All.Contains(id)),
            "Headcounts must be supplied for exactly the three fixed appointment types.");

        var eventItem = new Event
        {
            Id = id,
            ProposalId = null,
            Window = window!,
            Status = EventStatus.Active,
        };

        foreach (var appointmentTypeId in AppointmentTypeIds.All)
        {
            Guard.Against(
                !headcountsByAppointmentType.TryGetValue(appointmentTypeId, out var headcount),
                $"A headcount is required for {AppointmentTypeIds.NameOf(appointmentTypeId)}.");

            eventItem._capacities.Add(EventCapacity.Initialise(id, appointmentTypeId, headcount));
        }

        return eventItem;
    }

    /// <summary>Defines capacity for for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public EventCapacity CapacityFor(Guid appointmentTypeId)
    {
        AppointmentTypeIds.EnsureKnown(appointmentTypeId);

        var capacity = _capacities.SingleOrDefault(c => c.AppointmentTypeId == appointmentTypeId);
        Guard.Against(capacity is null, $"This event has no capacity counter for {appointmentTypeId}.");

        return capacity!;
    }

    /// <summary>Defines has spare capacity for all for the current use case.</summary>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public bool HasSpareCapacityForAll(IEnumerable<Guid> appointmentTypeIds) =>
        Status == EventStatus.Active
        && appointmentTypeIds.All(id => CapacityFor(id).HasSpare);

    /// <summary>Defines cancel for the current use case.</summary>
    public void Cancel()
    {
        Guard.Against(Status == EventStatus.Cancelled, "This event has already been cancelled.");
        Status = EventStatus.Cancelled;
    }
}
`````

## before — src/EventBooking.Domain/Slots/ConfirmedSlotStatus.cs — 1/1

<!-- vocabulary-file: {"id":114,"oldPath":"src/EventBooking.Domain/Slots/ConfirmedSlotStatus.cs","newPath":"src/EventBooking.Domain/Events/EventStatus.cs","beforeSha":"079fc1fb8d98f666830e76c490f752eb0b30c40c8c8e726b0a5061616b32c844","afterSha":"26cdd2a7216743cf8515b29b10ef11b3c50649be8c9e5632327aeada9f8651d3","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Domain/Events/EventStatus.cs — 1/1

<!-- vocabulary-file: {"id":114,"oldPath":"src/EventBooking.Domain/Slots/ConfirmedSlotStatus.cs","newPath":"src/EventBooking.Domain/Events/EventStatus.cs","beforeSha":"079fc1fb8d98f666830e76c490f752eb0b30c40c8c8e726b0a5061616b32c844","afterSha":"26cdd2a7216743cf8515b29b10ef11b3c50649be8c9e5632327aeada9f8651d3","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Events;

/// <summary>Defines event status for the current use case.</summary>
public enum EventStatus
{
    /// <summary>Defines active for the current use case.</summary>
    Active = 1,
    /// <summary>Defines cancelled for the current use case.</summary>
    Cancelled = 2,
}
`````
