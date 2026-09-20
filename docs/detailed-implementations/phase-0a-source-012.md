# 00a — Port source 12 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.Domain/Access/StaffIdentity.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Access/StaffIdentity.cs","encoding":"utf8","sha256":"28a355fbf3442cb014bb86f9b34f4389f844811096b9362903cbec0f6880f59f","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Access;

/// <summary>
/// The application mirror pairing a provider-assigned staff identity with the immutable enterprise
/// staff number carried by its validated token.
/// </summary>
public sealed class StaffIdentity
{
    private StaffIdentity()
    {
        StaffId = null!;
    }

    /// <summary>Gets the provider-assigned identity used as the authorization aggregate key.</summary>
    public Guid StaffUserId { get; private set; }

    /// <summary>Gets the enterprise staff number learned from the identity provider.</summary>
    public StaffId StaffId { get; private set; }

    /// <summary>
    /// Gets the human-readable name mirrored from the identity provider's token name claim, or
    /// null when no name has been observed. Presentation data only: its absence never blocks a
    /// request and it never affects authorization.
    /// </summary>
    public string? DisplayName { get; private set; }

    /// <summary>Gets the approximate time this identity was most recently recorded.</summary>
    public DateTimeOffset LastSeenAt { get; private set; }

    /// <summary>Creates the mirror row for a validated identity pair.</summary>
    /// <param name="staffUserId">The non-empty provider-assigned identity.</param>
    /// <param name="staffId">The immutable enterprise staff number.</param>
    /// <param name="displayName">The observed provider name, or null when the token carries none.</param>
    /// <param name="lastSeenAt">The time the pair was observed.</param>
    /// <returns>A valid staff identity mirror.</returns>
    public static StaffIdentity Create(
        Guid staffUserId,
        StaffId staffId,
        string? displayName,
        DateTimeOffset lastSeenAt)
    {
        Guard.Against(staffUserId == Guid.Empty, "staffUserId must not be empty.");
        Guard.Against(staffId is null, "staffId must not be null.");
        return new StaffIdentity
        {
            StaffUserId = staffUserId,
            StaffId = staffId!,
            DisplayName = displayName,
            LastSeenAt = lastSeenAt,
        };
    }

    /// <summary>
    /// Advances the approximate last-observed time without changing either identity, and
    /// overwrites the mirrored name with whatever the latest token carried — including back to
    /// null when it carried no name.
    /// </summary>
    /// <param name="displayName">The observed provider name, or null when the token carries none.</param>
    /// <param name="seenAt">A time no earlier than the current observation.</param>
    public void MarkSeen(string? displayName, DateTimeOffset seenAt)
    {
        Guard.Against(seenAt < LastSeenAt, "lastSeenAt must not move backwards.");
        DisplayName = displayName;
        LastSeenAt = seenAt;
    }
}
`````

## src/EventBooking.Domain/AppointmentTypes/AppointmentType.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/AppointmentTypes/AppointmentType.cs","encoding":"utf8","sha256":"b3e1865ce66194e7380817546d41b9c03c40d2b5c4e489d1d736504351f32d23","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Domain.AppointmentTypes;

/// <summary>Defines appointment type for the current use case.</summary>
public sealed class AppointmentType
{
    private AppointmentType()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    private AppointmentType(Guid id, string code, string name)
    {
        Id = id;
        Code = code;
        Name = name;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }
    /// <summary>Defines code for the current use case.</summary>
    public string Code { get; private set; }
    /// <summary>Defines name for the current use case.</summary>
    public string Name { get; private set; }

    /// <summary>Defines create fixed set for the current use case.</summary>
    public static IReadOnlyList<AppointmentType> CreateFixedSet() =>
        AppointmentTypeIds.All
            .Select(id => new AppointmentType(
                id,
                AppointmentTypeIds.CodeOf(id),
                AppointmentTypeIds.NameOf(id)))
            .ToList();
}
`````

## src/EventBooking.Domain/AppointmentTypes/AppointmentTypeIds.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/AppointmentTypes/AppointmentTypeIds.cs","encoding":"utf8","sha256":"85a18c0553ab49bfc0b39fe6fde11dc815ca945c0c48261550ffef0ff2565269","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.AppointmentTypes;

/// <summary>
/// The 3 appointment types are fixed by the spec. Their identifiers are constants rather than
/// generated values so that seed data, tests and migrations all agree without a lookup.
/// </summary>
public static class AppointmentTypeIds
{
    /// <summary>Defines drug and alcohol testing for the current use case.</summary>
    public static readonly Guid DrugAndAlcoholTesting = Guid.Parse("a0000001-0000-0000-0000-000000000001");
    /// <summary>Defines medical check up for the current use case.</summary>
    public static readonly Guid MedicalCheckUp = Guid.Parse("a0000002-0000-0000-0000-000000000002");
    /// <summary>Defines uniform fitting for the current use case.</summary>
    public static readonly Guid UniformFitting = Guid.Parse("a0000003-0000-0000-0000-000000000003");

    /// <summary>Defines all for the current use case.</summary>
    public static readonly IReadOnlyList<Guid> All =
    [
        DrugAndAlcoholTesting,
        MedicalCheckUp,
        UniformFitting,
    ];

    private static readonly Dictionary<string, Guid> CodeToId = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DAT"] = DrugAndAlcoholTesting,
        ["MED"] = MedicalCheckUp,
        ["UNI"] = UniformFitting,
    };

    private static readonly Dictionary<Guid, string> IdToCode = new()
    {
        [DrugAndAlcoholTesting] = "DAT",
        [MedicalCheckUp] = "MED",
        [UniformFitting] = "UNI",
    };

    private static readonly Dictionary<Guid, string> IdToName = new()
    {
        [DrugAndAlcoholTesting] = "Drug & Alcohol Testing",
        [MedicalCheckUp] = "Medical Check-up",
        [UniformFitting] = "Uniform Fitting",
    };

    /// <summary>Defines codes for the current use case.</summary>
    public static IReadOnlyCollection<string> Codes => CodeToId.Keys;

    /// <summary>Defines try from code for the current use case.</summary>
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

    /// <summary>Defines code of for the current use case.</summary>
    /// <param name="id">The id.</param>
    public static string CodeOf(Guid id) =>
        IdToCode.TryGetValue(id, out var code)
            ? code
            : throw new DomainException($"{id} is not one of the 3 appointment types.");

    /// <summary>Defines name of for the current use case.</summary>
    /// <param name="id">The id.</param>
    public static string NameOf(Guid id) =>
        IdToName.TryGetValue(id, out var name)
            ? name
            : throw new DomainException($"{id} is not one of the 3 appointment types.");

    /// <summary>Defines ensure known for the current use case.</summary>
    /// <param name="id">The id.</param>
    public static void EnsureKnown(Guid id)
    {
        Guard.Against(!IdToName.ContainsKey(id), $"{id} is not one of the 3 appointment types.");
    }
}
`````

## src/EventBooking.Domain/Audit/ActorType.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Audit/ActorType.cs","encoding":"utf8","sha256":"dd7eb788431c077b73349fa10db7c5716d12dd495eb8687fa3173548cb9a4e8b","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Domain.Audit;

/// <summary>Defines actor type for the current use case.</summary>
public enum ActorType
{
    /// <summary>Defines staff for the current use case.</summary>
    Staff = 1,
    /// <summary>Defines candidate token for the current use case.</summary>
    CandidateToken = 2,
    /// <summary>Defines system for the current use case.</summary>
    System = 3,
}
`````

## src/EventBooking.Domain/Audit/AuditAction.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Audit/AuditAction.cs","encoding":"utf8","sha256":"b8f36678c71b37fba9a50f1077087a7752cc79591b62e0e6421bdbe5d5463555","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Domain.Audit;

/// <summary>Defines audit action for the current use case.</summary>
public enum AuditAction
{
    /// <summary>Defines proposal created for the current use case.</summary>
    ProposalCreated = 1,
    /// <summary>Defines proposal withdrawn for the current use case.</summary>
    ProposalWithdrawn = 2,
    /// <summary>Defines acceptance recorded for the current use case.</summary>
    AcceptanceRecorded = 3,
    /// <summary>Defines acceptance withdrawn for the current use case.</summary>
    AcceptanceWithdrawn = 4,
    /// <summary>Defines slot confirmed for the current use case.</summary>
    SlotConfirmed = 5,
    /// <summary>Defines slot cancelled for the current use case.</summary>
    SlotCancelled = 6,
    /// <summary>Defines capacity decremented for the current use case.</summary>
    CapacityDecremented = 7,
    /// <summary>Defines capacity incremented for the current use case.</summary>
    CapacityIncremented = 8,
    /// <summary>Defines invite created for the current use case.</summary>
    InviteCreated = 9,
    /// <summary>Defines invite sent for the current use case.</summary>
    InviteSent = 10,
    /// <summary>Defines invite expired for the current use case.</summary>
    InviteExpired = 11,
    /// <summary>Defines invite option replaced for the current use case.</summary>
    InviteOptionReplaced = 12,
    /// <summary>Defines booking created for the current use case.</summary>
    BookingCreated = 13,
    /// <summary>Defines booking cancelled for the current use case.</summary>
    BookingCancelled = 14,
    /// <summary>Defines capacity adjusted for the current use case.</summary>
    CapacityAdjusted = 15,
    /// <summary>Defines slot imported for the current use case.</summary>
    SlotImported = 16,
    /// <summary>Defines staff access changed for the current use case.</summary>
    StaffAccessChanged = 17,
    /// <summary>Defines staff access removed for the current use case.</summary>
    StaffAccessRemoved = 18,
    /// <summary>Records an Expected appointment moving to CheckedIn.</summary>
    AppointmentCheckedIn = 19,
    /// <summary>Records a CheckedIn appointment moving to Completed.</summary>
    AppointmentCompleted = 20,
    /// <summary>Records an Expected appointment moving to NoShow.</summary>
    AppointmentMarkedNoShow = 21,
    /// <summary>Records one approved reverse appointment transition.</summary>
    AppointmentStatusCorrected = 22,
    /// <summary>Records the initial Employee Group assignment of a Candidate.</summary>
    EmployeeGroupAssigned = 23,
    /// <summary>Records a Candidate Employee Group change and its derived requirements.</summary>
    EmployeeGroupChanged = 24,
    /// <summary>Records a Coordinator issuing a recovery Invite for missed appointments.</summary>
    RecoveryInviteCreated = 25,
    /// <summary>Records a Coordinator cancelling a pending recovery Invite.</summary>
    RecoveryInviteCancelled = 26,
    /// <summary>Records a recovery Booking linked to its original journey root.</summary>
    RecoveryBookingCreated = 27,
    /// <summary>Records a recovery Booking concluded after terminal appointment outcomes.</summary>
    RecoveryBookingConcluded = 28,
    /// <summary>Records an identity-provider-driven role change applied by the claims sync.</summary>
    StaffRolesSynced = 29,
    /// <summary>Records a Coordinator deleting a Candidate and cascading onto their active bookings.</summary>
    CandidateDeleted = 30,
}
`````

## src/EventBooking.Domain/Audit/AuditEntityTypes.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Audit/AuditEntityTypes.cs","encoding":"utf8","sha256":"130b7421be70c59502be69107d039acc45d1502c3a600ff779d43cf433c8b2af","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Domain.Audit;

/// <summary>Defines audit entity types for the current use case.</summary>
public static class AuditEntityTypes
{
    /// <summary>Defines slot proposal for the current use case.</summary>
    public const string SlotProposal = "SlotProposal";
    /// <summary>Defines confirmed slot for the current use case.</summary>
    public const string ConfirmedSlot = "ConfirmedSlot";
    /// <summary>Defines invite for the current use case.</summary>
    public const string Invite = "Invite";
    /// <summary>Defines booking for the current use case.</summary>
    public const string Booking = "Booking";
    /// <summary>Defines staff access profile for the current use case.</summary>
    public const string StaffAccessProfile = "StaffAccessProfile";
    /// <summary>Audit entity name for one independently progressing booking appointment.</summary>
    public const string BookingAppointment = "BookingAppointment";
    /// <summary>Audit entity name for one invited person and their derived requirements.</summary>
    public const string Candidate = "Candidate";

    /// <summary>Defines all for the current use case.</summary>
    public static readonly IReadOnlyList<string> All =
        [SlotProposal, ConfirmedSlot, Invite, Booking, StaffAccessProfile, BookingAppointment, Candidate];
}
`````

## src/EventBooking.Domain/Audit/AuditLog.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Audit/AuditLog.cs","encoding":"utf8","sha256":"8db55c9761fb0e487e9eded75f1950834bb3b30d614c001536aec9a88c117810","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Audit;

/// <summary>An append-only record of one state change. Never updated, never deleted.</summary>
public sealed class AuditLog
{
    private AuditLog()
    {
        EntityType = string.Empty;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines entity type for the current use case.</summary>
    public string EntityType { get; private set; }

    /// <summary>Defines entity id for the current use case.</summary>
    public Guid EntityId { get; private set; }

    /// <summary>Defines action for the current use case.</summary>
    public AuditAction Action { get; private set; }

    /// <summary>Defines actor type for the current use case.</summary>
    public ActorType ActorType { get; private set; }

    /// <summary>The Entra object id for staff, the invite or booking id for a candidate token,
    /// null for the system.</summary>
    public string? ActorId { get; private set; }

    /// <summary>Defines timestamp for the current use case.</summary>
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>Defines details for the current use case.</summary>
    public string? Details { get; private set; }

    /// <summary>Defines record for the current use case.</summary>
    /// <param name="id">The id.</param>
    /// <param name="entityType">The entity type.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="action">The action.</param>
    /// <param name="actorType">The actor type.</param>
    /// <param name="actorId">The actor id.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <param name="details">The details.</param>
    public static AuditLog Record(
        Guid id,
        string entityType,
        Guid entityId,
        AuditAction action,
        ActorType actorType,
        string? actorId,
        DateTimeOffset timestamp,
        string? details)
    {
        Guard.Against(
            !AuditEntityTypes.All.Contains(entityType),
            $"{entityType} is not an audited entity type.");
        Guard.Against(entityId == Guid.Empty, "entityId must not be empty.");

        if (actorType != ActorType.System)
        {
            Guard.NotBlank(actorId, "actorId");
        }

        return new AuditLog
        {
            Id = id,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ActorType = actorType,
            ActorId = actorId,
            Timestamp = timestamp,
            Details = details,
        };
    }
}
`````

## src/EventBooking.Domain/Bookings/Booking.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Bookings/Booking.cs","encoding":"utf8","sha256":"7d9815a8cb380a74e26d990fb6cb9b98a69a45d4b96fd618ac110d752a7396ad","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Bookings;

/// <summary>Defines booking for the current use case.</summary>
public sealed class Booking
{
    private Booking()
    {
        // Required by the persistence layer's constructor binding.
        ManageTokenHash = string.Empty;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines candidate id for the current use case.</summary>
    public Guid CandidateId { get; private set; }

    /// <summary>Defines confirmed slot id for the current use case.</summary>
    public Guid ConfirmedSlotId { get; private set; }

    /// <summary>Defines invite id for the current use case.</summary>
    public Guid InviteId { get; private set; }

    /// <summary>Defines created at for the current use case.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Defines status for the current use case.</summary>
    public BookingStatus Status { get; private set; } = BookingStatus.Active;

    /// <summary>Gets the original active Booking ID, or null for the journey root.</summary>
    public Guid? RecoveryOfBookingId { get; private set; }

    /// <summary>Gets whether this Booking is the original journey root.</summary>
    public bool IsOriginal => RecoveryOfBookingId is null;

    /// <summary>Hash of the single-use token behind the cancel/reschedule link.</summary>
    public string ManageTokenHash { get; private set; }

    /// <summary>Replaces the persisted management-link hash after issuing a fresh raw token.</summary>
    /// <param name="manageTokenHash">The manage token hash.</param>
    public void RotateManageTokenHash(string? manageTokenHash)
    {
        Guard.Against(Status != BookingStatus.Active, "Only an active booking token can be rotated.");
        ManageTokenHash = Guard.NotBlank(manageTokenHash, "manageTokenHash");
    }

    /// <summary>Defines create for the current use case.</summary>
    /// <param name="id">The id.</param>
    /// <param name="invite">The invite.</param>
    /// <param name="confirmedSlotId">The confirmed slot id.</param>
    /// <param name="manageTokenHash">The manage token hash.</param>
    /// <param name="createdAt">The created at.</param>
    public static Booking Create(
        Guid id,
        Invite invite,
        Guid confirmedSlotId,
        string? manageTokenHash,
        DateTimeOffset createdAt)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(invite is null, "invite must be supplied.");
        Guard.Against(invite!.Status != InviteStatus.Pending, "This invite can no longer be used.");
        Guard.Against(
            !invite.Offers(confirmedSlotId),
            "The chosen slot is not one of this invite's options.");

        return new Booking
        {
            Id = id,
            CandidateId = invite.CandidateId,
            ConfirmedSlotId = confirmedSlotId,
            InviteId = invite.Id,
            CreatedAt = createdAt,
            Status = BookingStatus.Active,
            ManageTokenHash = Guard.NotBlank(manageTokenHash, "manageTokenHash"),
        };
    }

    /// <summary>Defines cancel for the current use case.</summary>
    public void Cancel()
    {
        Guard.Against(Status == BookingStatus.Cancelled, "This booking has already been cancelled.");
        Status = BookingStatus.Cancelled;
    }

    /// <summary>Creates a recovery Booking directly linked to the original Booking.</summary>
    /// <param name="id">The stable recovery booking identifier.</param>
    /// <param name="recoveryInvite">The pending recovery invite issued for the original Booking.</param>
    /// <param name="originalBooking">The active original journey root being recovered.</param>
    /// <param name="confirmedSlotId">The recovery slot offered by the invite.</param>
    /// <param name="manageTokenHash">The management-link hash for the recovery booking.</param>
    /// <param name="createdAt">When the recovery booking is created.</param>
    /// <returns>An active recovery Booking pointing at the original root.</returns>
    public static Booking CreateRecovery(
        Guid id,
        Invite recoveryInvite,
        Booking originalBooking,
        Guid confirmedSlotId,
        string? manageTokenHash,
        DateTimeOffset createdAt)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(recoveryInvite is null, "recoveryInvite must be supplied.");
        Guard.Against(originalBooking is null, "originalBooking must be supplied.");
        Guard.Against(!originalBooking!.IsOriginal, "A recovery booking cannot point at another recovery.");
        Guard.Against(
            originalBooking.Status != BookingStatus.Active,
            "A recovery booking requires an active original booking.");
        Guard.Against(
            recoveryInvite!.RecoveryOfBookingId != originalBooking.Id,
            "The recovery invite must point at the supplied original booking.");
        Guard.Against(
            recoveryInvite.CandidateId != originalBooking.CandidateId,
            "The recovery invite must belong to the original booking candidate.");
        Guard.Against(
            recoveryInvite.Status != InviteStatus.Pending,
            "This invite can no longer be used.");
        Guard.Against(
            !recoveryInvite.Offers(confirmedSlotId),
            "The chosen slot is not one of this invite's options.");

        return new Booking
        {
            Id = id,
            CandidateId = originalBooking.CandidateId,
            ConfirmedSlotId = confirmedSlotId,
            InviteId = recoveryInvite.Id,
            CreatedAt = createdAt,
            Status = BookingStatus.Active,
            RecoveryOfBookingId = originalBooking.Id,
            ManageTokenHash = Guard.NotBlank(manageTokenHash, "manageTokenHash"),
        };
    }

    /// <summary>Concludes an Active recovery Booking after all of its appointments are terminal.</summary>
    public void Conclude()
    {
        Guard.Against(IsOriginal, "Only a recovery booking can conclude.");
        Guard.Against(Status != BookingStatus.Active, "Only an active recovery booking can conclude.");
        Status = BookingStatus.Concluded;
    }

    /// <summary>Reopens a Concluded recovery Booking after an allowed outcome correction.</summary>
    public void Reopen()
    {
        Guard.Against(IsOriginal, "Only a recovery booking can reopen.");
        Guard.Against(Status != BookingStatus.Concluded, "Only a concluded recovery booking can reopen.");
        Status = BookingStatus.Active;
    }
}
`````

## src/EventBooking.Domain/Bookings/BookingAppointment.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Bookings/BookingAppointment.cs","encoding":"utf8","sha256":"c77ed9fee5811c58564ce2907d646fbab382f1e43a528d29dee4256a31b50dc1","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Bookings;

/// <summary>The operational instance of one required appointment type within one booking.</summary>
public sealed class BookingAppointment
{
    private BookingAppointment()
    {
    }

    /// <summary>Gets the stable appointment-record identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the parent booking identifier.</summary>
    public Guid BookingId { get; private set; }

    /// <summary>Gets the required appointment type delivered by this record.</summary>
    public Guid AppointmentTypeId { get; private set; }

    /// <summary>Gets this appointment's independent operational status.</summary>
    public BookingAppointmentStatus Status { get; private set; }

    /// <summary>Gets when staff checked the candidate in, or null until check-in.</summary>
    public DateTimeOffset? CheckedInAt { get; private set; }

    /// <summary>Gets when staff recorded completion or no-show, or null before an outcome.</summary>
    public DateTimeOffset? OutcomeAt { get; private set; }

    /// <summary>Gets the staff identity responsible for the latest real transition.</summary>
    public Guid? LastChangedByStaffUserId { get; private set; }

    /// <summary>Gets when the latest real transition occurred.</summary>
    public DateTimeOffset? LastChangedAt { get; private set; }

    /// <summary>Gets the positive concurrency version, initially one.</summary>
    public long Version { get; private set; }

    /// <summary>Creates an Expected appointment for one booking requirement.</summary>
    /// <param name="id">The stable appointment-record identifier.</param>
    /// <param name="bookingId">The parent booking identifier.</param>
    /// <param name="appointmentTypeId">The required fixed appointment-type identifier.</param>
    /// <returns>A new untouched appointment at version one.</returns>
    public static BookingAppointment Create(Guid id, Guid bookingId, Guid appointmentTypeId)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(bookingId == Guid.Empty, "bookingId must not be empty.");
        AppointmentTypeIds.EnsureKnown(appointmentTypeId);

        return new BookingAppointment
        {
            Id = id,
            BookingId = bookingId,
            AppointmentTypeId = appointmentTypeId,
            Status = BookingAppointmentStatus.Expected,
            Version = 1,
        };
    }

    /// <summary>Moves to the requested approved state or reports an idempotent same-state request.</summary>
    /// <param name="target">The requested operational status.</param>
    /// <param name="staffUserId">The authenticated staff identity making the request.</param>
    /// <param name="changedAt">The UTC instant supplied by the application clock.</param>
    /// <param name="checkInAllowed">Whether the slot is on the current head-office date.</param>
    /// <param name="noShowAllowed">Whether the slot's four-hour window has ended.</param>
    /// <returns><see langword="true"/> for a real transition; otherwise <see langword="false"/>.</returns>
    public bool TransitionTo(
        BookingAppointmentStatus target,
        Guid staffUserId,
        DateTimeOffset changedAt,
        bool checkInAllowed,
        bool noShowAllowed)
    {
        Guard.Against(staffUserId == Guid.Empty, "staffUserId must not be empty.");
        Guard.Against(changedAt == default, "changedAt must be supplied.");
        Guard.Against(!Enum.IsDefined(target), "target status must be recognised.");

        if (target == Status)
        {
            return false;
        }

        switch (Status, target)
        {
            case (BookingAppointmentStatus.Expected, BookingAppointmentStatus.CheckedIn):
                Guard.Against(!checkInAllowed, "Check-in is available only on the confirmed-slot date.");
                CheckedInAt = changedAt;
                OutcomeAt = null;
                break;

            case (BookingAppointmentStatus.CheckedIn, BookingAppointmentStatus.Completed):
                OutcomeAt = changedAt;
                break;

            case (BookingAppointmentStatus.Expected, BookingAppointmentStatus.NoShow):
                Guard.Against(!noShowAllowed, "No-show is available only after the slot window ends.");
                CheckedInAt = null;
                OutcomeAt = changedAt;
                break;

            case (BookingAppointmentStatus.CheckedIn, BookingAppointmentStatus.Expected):
                CheckedInAt = null;
                OutcomeAt = null;
                break;

            case (BookingAppointmentStatus.Completed, BookingAppointmentStatus.CheckedIn):
                OutcomeAt = null;
                break;

            case (BookingAppointmentStatus.NoShow, BookingAppointmentStatus.Expected):
                CheckedInAt = null;
                OutcomeAt = null;
                break;

            default:
                throw new DomainException($"An appointment cannot move from {Status} to {target}.");
        }

        Status = target;
        LastChangedByStaffUserId = staffUserId;
        LastChangedAt = changedAt;
        Version++;
        return true;
    }
}
`````

## src/EventBooking.Domain/Bookings/BookingAppointmentStatus.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Bookings/BookingAppointmentStatus.cs","encoding":"utf8","sha256":"f02869ce44b5890b50caa4f251c9fbe801cfd3e676539ec614aedbaf77ac07c3","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Domain.Bookings;

/// <summary>Independent operational progress for one required appointment within a booking.</summary>
public enum BookingAppointmentStatus
{
    /// <summary>The candidate is booked and has not checked in for this appointment.</summary>
    Expected = 1,

    /// <summary>The candidate has checked in for this appointment.</summary>
    CheckedIn = 2,

    /// <summary>The required appointment was completed after check-in.</summary>
    Completed = 3,

    /// <summary>The candidate did not attend this required appointment.</summary>
    NoShow = 4,
}
`````

## src/EventBooking.Domain/Bookings/BookingStatus.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Bookings/BookingStatus.cs","encoding":"utf8","sha256":"5786fa3bc5cacf3e4ea8ce882d36a360a56ca0eac232168ad97b62bb3324eb17","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Domain.Bookings;

/// <summary>Defines booking status for the current use case.</summary>
public enum BookingStatus
{
    /// <summary>Defines active for the current use case.</summary>
    Active = 1,
    /// <summary>Defines cancelled for the current use case.</summary>
    Cancelled = 2,
    /// <summary>A recovery Booking whose own appointments all have terminal outcomes.</summary>
    Concluded = 3,
}
`````

## src/EventBooking.Domain/Candidates/Candidate.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Candidates/Candidate.cs","encoding":"utf8","sha256":"540001e361c120ce954665d79b677a29f8f5257243b492585414d6bcf7ed3a82","parts":1,"part":1} -->

`````csharp
using System.Net.Mail;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Domain.Candidates;

/// <summary>A person invited to attend appointments, whose requirements derive from one employee group.</summary>
public sealed class Candidate
{
    private readonly List<CandidateRequirement> _requirements = [];

    private Candidate()
    {
        // Required by the persistence layer's constructor binding.
        Name = string.Empty;
        Email = string.Empty;
    }

    /// <summary>Gets the candidate identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the candidate display name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets the normalized candidate email address.</summary>
    public string Email { get; private set; }

    /// <summary>Gets the assigned Employee Group, or null during legacy reconciliation.</summary>
    public Guid? EmployeeGroupId { get; private set; }

    /// <summary>Gets where the candidate sits in the invite and booking lifecycle.</summary>
    public CandidateStatus Status { get; private set; } = CandidateStatus.NotYetInvited;

    /// <summary>Gets the materialized appointment types the candidate currently requires.</summary>
    public IReadOnlyList<CandidateRequirement> Requirements => _requirements;

    /// <summary>Gets the identifiers of the appointment types the candidate currently requires.</summary>
    public IReadOnlyList<Guid> RequiredAppointmentTypeIds =>
        _requirements.Select(r => r.AppointmentTypeId).ToList();

    /// <summary>Creates a Candidate and derives every requirement from the active mapped group.</summary>
    /// <param name="id">The id.</param>
    /// <param name="name">The name.</param>
    /// <param name="email">The email.</param>
    /// <param name="employeeGroup">The employee group.</param>
    public static Candidate Create(Guid id, string? name, string? email, EmployeeGroup employeeGroup)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");

        var candidate = new Candidate
        {
            Id = id,
            Name = Guard.NotBlank(name, "name"),
            Email = NormaliseEmail(email),
            Status = CandidateStatus.NotYetInvited,
        };

        candidate.AssignEmployeeGroup(employeeGroup);

        return candidate;
    }

    /// <summary>Replaces the candidate name and email after validating both.</summary>
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
    /// <param name="employeeGroup">The employee group.</param>
    public bool AssignEmployeeGroup(EmployeeGroup employeeGroup)
    {
        ArgumentNullException.ThrowIfNull(employeeGroup);
        Guard.Against(!employeeGroup.IsActive, "An inactive employee group cannot be assignment authority.");

        var mapping = employeeGroup.RequiredAppointmentTypeIds.ToList();

        Guard.Against(mapping.Count == 0, "An employee group must map at least one appointment type.");

        foreach (var appointmentTypeId in mapping)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

        EmployeeGroupId = employeeGroup.Id;

        if (_requirements.Select(r => r.AppointmentTypeId).Order().SequenceEqual(mapping.Order()))
        {
            return false;
        }

        _requirements.Clear();
        foreach (var appointmentTypeId in mapping.Order())
        {
            _requirements.Add(CandidateRequirement.For(Id, appointmentTypeId));
        }

        return true;
    }

    /// <summary>Moves the candidate to Invited from a pre-booking lifecycle state.</summary>
    public void MarkInvited() => TransitionTo(
        CandidateStatus.Invited,
        CandidateStatus.NotYetInvited,
        CandidateStatus.AwaitingAvailability,
        CandidateStatus.Invited,
        CandidateStatus.NoResponseNeedsFollowUp);

    /// <summary>Moves the candidate to AwaitingAvailability from a pre-booking lifecycle state.</summary>
    public void MarkAwaitingAvailability() => TransitionTo(
        CandidateStatus.AwaitingAvailability,
        CandidateStatus.NotYetInvited,
        CandidateStatus.AwaitingAvailability,
        CandidateStatus.Invited,
        CandidateStatus.NoResponseNeedsFollowUp);

    /// <summary>Moves an invited candidate to Booked.</summary>
    public void MarkBooked() => TransitionTo(CandidateStatus.Booked, CandidateStatus.Invited);

    /// <summary>Moves an invited candidate to NoResponseNeedsFollowUp.</summary>
    public void MarkNoResponse() => TransitionTo(
        CandidateStatus.NoResponseNeedsFollowUp,
        CandidateStatus.Invited);

    /// <summary>Returns an invited or booked candidate to NotYetInvited.</summary>
    public void ResetToNotYetInvited() => TransitionTo(
        CandidateStatus.NotYetInvited,
        CandidateStatus.Invited,
        CandidateStatus.Booked);

    /// <summary>Resets an unbooked Candidate after a derived requirement-set change.</summary>
    public void ResetAfterRequirementChange()
    {
        if (Status is CandidateStatus.NotYetInvited)
        {
            return;
        }

        TransitionTo(
            CandidateStatus.NotYetInvited,
            CandidateStatus.AwaitingAvailability,
            CandidateStatus.NoResponseNeedsFollowUp,
            CandidateStatus.Invited);
    }

    private void TransitionTo(CandidateStatus target, params CandidateStatus[] allowedOrigins)
    {
        Guard.Against(
            !allowedOrigins.Contains(Status),
            $"A candidate cannot move from {Status} to {target}.");

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

## src/EventBooking.Domain/Candidates/CandidateRequirement.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Candidates/CandidateRequirement.cs","encoding":"utf8","sha256":"ef86ddea361f2b1e38ca1aec97d925d12b78f5e2d18a4622d72c49d4f08f01a8","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Domain.Candidates;

/// <summary>One appointment type a given candidate has to attend.</summary>
public sealed class CandidateRequirement
{
    private CandidateRequirement()
    {
    }

    /// <summary>Defines candidate id for the current use case.</summary>
    public Guid CandidateId { get; private set; }

    /// <summary>Defines appointment type id for the current use case.</summary>
    public Guid AppointmentTypeId { get; private set; }

    internal static CandidateRequirement For(Guid candidateId, Guid appointmentTypeId) =>
        new() { CandidateId = candidateId, AppointmentTypeId = appointmentTypeId };
}
`````

## src/EventBooking.Domain/Candidates/CandidateStatus.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Candidates/CandidateStatus.cs","encoding":"utf8","sha256":"b7823364e67d40be98f42c79115a9eb5a40d7165ea0bf21eb93142cc4aec767b","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Domain.Candidates;

/// <summary>Defines candidate status for the current use case.</summary>
public enum CandidateStatus
{
    /// <summary>Defines not yet invited for the current use case.</summary>
    NotYetInvited = 1,
    /// <summary>Defines awaiting availability for the current use case.</summary>
    AwaitingAvailability = 2,
    /// <summary>Defines invited for the current use case.</summary>
    Invited = 3,
    /// <summary>Defines booked for the current use case.</summary>
    Booked = 4,
    /// <summary>Defines no response needs follow up for the current use case.</summary>
    NoResponseNeedsFollowUp = 5,
}
`````

## src/EventBooking.Domain/Common/DomainException.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Common/DomainException.cs","encoding":"utf8","sha256":"439588f11a0efea10ce68d240d76f21b8c1473e71648182e7c9a688709e818db","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Domain.Common;

/// <summary>The single failure mode of the domain layer: an invariant was violated.</summary>
public sealed class DomainException : Exception
{
    /// <summary>Defines domain exception for the current use case.</summary>
    /// <param name="message">The message.</param>
    public DomainException(string message) : base(message)
    {
    }
}
`````

## src/EventBooking.Domain/Common/Guard.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Common/Guard.cs","encoding":"utf8","sha256":"3f04c0afc177337a14a5243b69431f2879fdfba49590b7a0f224fcf53af1dcb2","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Domain.Common;

/// <summary>Defines guard for the current use case.</summary>
public static class Guard
{
    /// <summary>Defines against for the current use case.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="message">The message.</param>
    public static void Against(bool condition, string message)
    {
        if (condition)
        {
            throw new DomainException(message);
        }
    }

    /// <summary>Defines not blank for the current use case.</summary>
    /// <param name="value">The value.</param>
    /// <param name="field">The field.</param>
    public static string NotBlank(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{field} must not be blank.");
        }

        return value.Trim();
    }

    /// <summary>Defines positive for the current use case.</summary>
    /// <param name="value">The value.</param>
    /// <param name="field">The field.</param>
    public static int Positive(int value, string field)
    {
        if (value <= 0)
        {
            throw new DomainException($"{field} must be greater than zero.");
        }

        return value;
    }

    /// <summary>Defines not negative for the current use case.</summary>
    /// <param name="value">The value.</param>
    /// <param name="field">The field.</param>
    public static int NotNegative(int value, string field)
    {
        if (value < 0)
        {
            throw new DomainException($"{field} must not be negative.");
        }

        return value;
    }
}
`````

## src/EventBooking.Domain/EmployeeGroups/EmployeeGroup.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/EmployeeGroups/EmployeeGroup.cs","encoding":"utf8","sha256":"2c78ca13024f6ab679f254566f17361955aea427d18f8e292b1dd63e38d33950","parts":1,"part":1} -->

`````csharp
using System.Text.RegularExpressions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.EmployeeGroups;

/// <summary>One change-controlled employment category that determines candidate requirements.</summary>
public sealed class EmployeeGroup
{
    private static readonly Regex CanonicalCodeExpression =
        new("^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$", RegexOptions.CultureInvariant);

    private readonly List<EmployeeGroupRequirement> _requirements = [];

    private EmployeeGroup()
    {
        // Materializes persisted rows, including inactive ones that Define would reject.
        Code = string.Empty;
        Name = string.Empty;
    }

    /// <summary>Gets the stable reference-data identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the immutable canonical uppercase snake-case code.</summary>
    public string Code { get; private set; }

    /// <summary>Gets the canonical Coordinator-facing display name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets whether new and changed Candidates may be assigned this group.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the fixed Appointment Type mappings owned by this group.</summary>
    public IReadOnlyList<EmployeeGroupRequirement> Requirements => _requirements;

    /// <summary>Gets the mapped Appointment Type identifiers in stable identifier order.</summary>
    public IReadOnlyList<Guid> RequiredAppointmentTypeIds =>
        _requirements.Select(requirement => requirement.AppointmentTypeId).Order().ToList();

    /// <summary>Defines one validated reference-data row and its complete mapping.</summary>
    /// <param name="id">The id.</param>
    /// <param name="code">The code.</param>
    /// <param name="name">The name.</param>
    /// <param name="isActive">The is active.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public static EmployeeGroup Define(
        Guid id,
        string? code,
        string? name,
        bool isActive,
        IEnumerable<Guid> appointmentTypeIds)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(
            code is null || !CanonicalCodeExpression.IsMatch(code),
            "code must be canonical uppercase snake case.");
        Guard.Against(!isActive, "An inactive employee group cannot be assignment authority.");

        var displayName = Guard.NotBlank(name, "name");
        var mapping = appointmentTypeIds.ToList();

        Guard.Against(mapping.Count == 0, "An employee group must map at least one appointment type.");
        Guard.Against(
            mapping.Distinct().Count() != mapping.Count,
            "An employee group cannot map the same appointment type twice.");

        foreach (var appointmentTypeId in mapping)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

        var group = new EmployeeGroup
        {
            Id = id,
            Code = code!,
            Name = displayName,
            IsActive = isActive,
        };

        foreach (var appointmentTypeId in mapping.Order())
        {
            group._requirements.Add(EmployeeGroupRequirement.For(id, appointmentTypeId));
        }

        return group;
    }
}
`````

## src/EventBooking.Domain/EmployeeGroups/EmployeeGroupIds.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/EmployeeGroups/EmployeeGroupIds.cs","encoding":"utf8","sha256":"c38b4a39e8d86dafc0f4e5806a777da06eb7ecbd2747f3b3a68dab224bd25f0b","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.EmployeeGroups;

/// <summary>Stable identifiers and canonical codes for the five approved Employee Groups.</summary>
public static class EmployeeGroupIds
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

    /// <summary>Gets every approved Employee Group identifier.</summary>
    public static IReadOnlyCollection<Guid> All { get; } =
    [
        CabinCrew,
        Pilots,
        GroundOperationsAgent,
        Engineering,
        GroundTransportServices,
    ];

    /// <summary>Gets every approved canonical Employee Group code.</summary>
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

    /// <summary>Gets the canonical code for a known Employee Group identifier.</summary>
    /// <param name="id">The id.</param>
    public static string CodeOf(Guid id) =>
        IdToCode.TryGetValue(id, out var code)
            ? code
            : throw new DomainException($"{id} is not one of the 5 employee groups.");

    /// <summary>Ensures the identifier names a known Employee Group.</summary>
    /// <param name="id">The id.</param>
    public static void EnsureKnown(Guid id)
    {
        Guard.Against(!IdToCode.ContainsKey(id), $"{id} is not one of the 5 employee groups.");
    }
}
`````

## src/EventBooking.Domain/EmployeeGroups/EmployeeGroupRequirement.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/EmployeeGroups/EmployeeGroupRequirement.cs","encoding":"utf8","sha256":"eefda867b3ee179c1a0174573718ad0a3fbd7a575327ba63390b47fc60529772","parts":1,"part":1} -->

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

## src/EventBooking.Domain/EventBooking.Domain.csproj — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/EventBooking.Domain.csproj","encoding":"utf8","sha256":"0e18f178a380a51c6f1f0dddfa2d993c1188e1512824e27001e60f3bb33f2581","parts":1,"part":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk">

</Project>
`````
