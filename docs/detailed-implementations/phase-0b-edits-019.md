# 00b — Vocabulary edits 19 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — src/EventBooking.Domain/Bookings/Booking.cs — 1/1

<!-- vocabulary-file: {"id":98,"oldPath":"src/EventBooking.Domain/Bookings/Booking.cs","newPath":"src/EventBooking.Domain/Bookings/Booking.cs","beforeSha":"7d9815a8cb380a74e26d990fb6cb9b98a69a45d4b96fd618ac110d752a7396ad","afterSha":"54e28a7e769f7397f2e81ca36d2c5c228a65ec72d9ae9db7e0b8a424d3130714","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Domain/Bookings/Booking.cs — 1/1

<!-- vocabulary-file: {"id":98,"oldPath":"src/EventBooking.Domain/Bookings/Booking.cs","newPath":"src/EventBooking.Domain/Bookings/Booking.cs","beforeSha":"7d9815a8cb380a74e26d990fb6cb9b98a69a45d4b96fd618ac110d752a7396ad","afterSha":"54e28a7e769f7397f2e81ca36d2c5c228a65ec72d9ae9db7e0b8a424d3130714","side":"after","part":1,"parts":1} -->

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

    /// <summary>Defines attendee id for the current use case.</summary>
    public Guid AttendeeId { get; private set; }

    /// <summary>Defines event id for the current use case.</summary>
    public Guid EventId { get; private set; }

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
    /// <param name="eventId">The event id.</param>
    /// <param name="manageTokenHash">The manage token hash.</param>
    /// <param name="createdAt">The created at.</param>
    public static Booking Create(
        Guid id,
        Invite invite,
        Guid eventId,
        string? manageTokenHash,
        DateTimeOffset createdAt)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(invite is null, "invite must be supplied.");
        Guard.Against(invite!.Status != InviteStatus.Pending, "This invite can no longer be used.");
        Guard.Against(
            !invite.Offers(eventId),
            "The chosen eventItem is not one of this invite's options.");

        return new Booking
        {
            Id = id,
            AttendeeId = invite.AttendeeId,
            EventId = eventId,
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
    /// <param name="eventId">The recovery event offered by the invite.</param>
    /// <param name="manageTokenHash">The management-link hash for the recovery booking.</param>
    /// <param name="createdAt">When the recovery booking is created.</param>
    /// <returns>An active recovery Booking pointing at the original root.</returns>
    public static Booking CreateRecovery(
        Guid id,
        Invite recoveryInvite,
        Booking originalBooking,
        Guid eventId,
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
            recoveryInvite.AttendeeId != originalBooking.AttendeeId,
            "The recovery invite must belong to the original booking attendee.");
        Guard.Against(
            recoveryInvite.Status != InviteStatus.Pending,
            "This invite can no longer be used.");
        Guard.Against(
            !recoveryInvite.Offers(eventId),
            "The chosen eventItem is not one of this invite's options.");

        return new Booking
        {
            Id = id,
            AttendeeId = originalBooking.AttendeeId,
            EventId = eventId,
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

## before — src/EventBooking.Domain/Bookings/BookingAppointment.cs — 1/1

<!-- vocabulary-file: {"id":99,"oldPath":"src/EventBooking.Domain/Bookings/BookingAppointment.cs","newPath":"src/EventBooking.Domain/Bookings/BookingAppointment.cs","beforeSha":"c77ed9fee5811c58564ce2907d646fbab382f1e43a528d29dee4256a31b50dc1","afterSha":"6549381437c10bc17192859ac0db4d279696d47c3f1cfb82d2427dfe99cd2a4d","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Domain/Bookings/BookingAppointment.cs — 1/1

<!-- vocabulary-file: {"id":99,"oldPath":"src/EventBooking.Domain/Bookings/BookingAppointment.cs","newPath":"src/EventBooking.Domain/Bookings/BookingAppointment.cs","beforeSha":"c77ed9fee5811c58564ce2907d646fbab382f1e43a528d29dee4256a31b50dc1","afterSha":"6549381437c10bc17192859ac0db4d279696d47c3f1cfb82d2427dfe99cd2a4d","side":"after","part":1,"parts":1} -->

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

    /// <summary>Gets when staff checked the attendee in, or null until check-in.</summary>
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
    /// <param name="checkInAllowed">Whether the eventItem is on the current transitional-location date.</param>
    /// <param name="noShowAllowed">Whether the event's four-hour window has ended.</param>
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
                Guard.Against(!checkInAllowed, "Check-in is available only on the event date.");
                CheckedInAt = changedAt;
                OutcomeAt = null;
                break;

            case (BookingAppointmentStatus.CheckedIn, BookingAppointmentStatus.Completed):
                OutcomeAt = changedAt;
                break;

            case (BookingAppointmentStatus.Expected, BookingAppointmentStatus.NoShow):
                Guard.Against(!noShowAllowed, "No-show is available only after the event window ends.");
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

## before — src/EventBooking.Domain/Bookings/BookingAppointmentStatus.cs — 1/1

<!-- vocabulary-file: {"id":100,"oldPath":"src/EventBooking.Domain/Bookings/BookingAppointmentStatus.cs","newPath":"src/EventBooking.Domain/Bookings/BookingAppointmentStatus.cs","beforeSha":"f02869ce44b5890b50caa4f251c9fbe801cfd3e676539ec614aedbaf77ac07c3","afterSha":"c10c6dab48413355b09a9c0e3a7a6e70ff56fe9d81adb92792415aa72f584c2d","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Domain/Bookings/BookingAppointmentStatus.cs — 1/1

<!-- vocabulary-file: {"id":100,"oldPath":"src/EventBooking.Domain/Bookings/BookingAppointmentStatus.cs","newPath":"src/EventBooking.Domain/Bookings/BookingAppointmentStatus.cs","beforeSha":"f02869ce44b5890b50caa4f251c9fbe801cfd3e676539ec614aedbaf77ac07c3","afterSha":"c10c6dab48413355b09a9c0e3a7a6e70ff56fe9d81adb92792415aa72f584c2d","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Bookings;

/// <summary>Independent operational progress for one required appointment within a booking.</summary>
public enum BookingAppointmentStatus
{
    /// <summary>The attendee is booked and has not checked in for this appointment.</summary>
    Expected = 1,

    /// <summary>The attendee has checked in for this appointment.</summary>
    CheckedIn = 2,

    /// <summary>The required appointment was completed after check-in.</summary>
    Completed = 3,

    /// <summary>The attendee did not attend this required appointment.</summary>
    NoShow = 4,
}
`````

## before — src/EventBooking.Domain/Candidates/Candidate.cs — 1/1

<!-- vocabulary-file: {"id":101,"oldPath":"src/EventBooking.Domain/Candidates/Candidate.cs","newPath":"src/EventBooking.Domain/Attendees/Attendee.cs","beforeSha":"540001e361c120ce954665d79b677a29f8f5257243b492585414d6bcf7ed3a82","afterSha":"43e60c2c07ec61c07a019cfb4ad03c21f73c0b1a53f5139c030a60bdc14c5a5c","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Domain/Attendees/Attendee.cs — 1/1

<!-- vocabulary-file: {"id":101,"oldPath":"src/EventBooking.Domain/Candidates/Candidate.cs","newPath":"src/EventBooking.Domain/Attendees/Attendee.cs","beforeSha":"540001e361c120ce954665d79b677a29f8f5257243b492585414d6bcf7ed3a82","afterSha":"43e60c2c07ec61c07a019cfb4ad03c21f73c0b1a53f5139c030a60bdc14c5a5c","side":"after","part":1,"parts":1} -->

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

## before — src/EventBooking.Domain/Candidates/CandidateRequirement.cs — 1/1

<!-- vocabulary-file: {"id":102,"oldPath":"src/EventBooking.Domain/Candidates/CandidateRequirement.cs","newPath":"src/EventBooking.Domain/Attendees/AttendeeRequirement.cs","beforeSha":"ef86ddea361f2b1e38ca1aec97d925d12b78f5e2d18a4622d72c49d4f08f01a8","afterSha":"918c77bb87a51831db6845d4869ecbd8e0f2f3a43018654a863e7e19ec6b9972","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Domain/Attendees/AttendeeRequirement.cs — 1/1

<!-- vocabulary-file: {"id":102,"oldPath":"src/EventBooking.Domain/Candidates/CandidateRequirement.cs","newPath":"src/EventBooking.Domain/Attendees/AttendeeRequirement.cs","beforeSha":"ef86ddea361f2b1e38ca1aec97d925d12b78f5e2d18a4622d72c49d4f08f01a8","afterSha":"918c77bb87a51831db6845d4869ecbd8e0f2f3a43018654a863e7e19ec6b9972","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Attendees;

/// <summary>One appointment type a given attendee has to attend.</summary>
public sealed class AttendeeRequirement
{
    private AttendeeRequirement()
    {
    }

    /// <summary>Defines attendee id for the current use case.</summary>
    public Guid AttendeeId { get; private set; }

    /// <summary>Defines appointment type id for the current use case.</summary>
    public Guid AppointmentTypeId { get; private set; }

    internal static AttendeeRequirement For(Guid attendeeId, Guid appointmentTypeId) =>
        new() { AttendeeId = attendeeId, AppointmentTypeId = appointmentTypeId };
}
`````

## before — src/EventBooking.Domain/Candidates/CandidateStatus.cs — 1/1

<!-- vocabulary-file: {"id":103,"oldPath":"src/EventBooking.Domain/Candidates/CandidateStatus.cs","newPath":"src/EventBooking.Domain/Attendees/AttendeeStatus.cs","beforeSha":"b7823364e67d40be98f42c79115a9eb5a40d7165ea0bf21eb93142cc4aec767b","afterSha":"f6fb88d2d299e0a20d94fad3584bf94566003b2916e442458c8abb9337a8de22","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Domain/Attendees/AttendeeStatus.cs — 1/1

<!-- vocabulary-file: {"id":103,"oldPath":"src/EventBooking.Domain/Candidates/CandidateStatus.cs","newPath":"src/EventBooking.Domain/Attendees/AttendeeStatus.cs","beforeSha":"b7823364e67d40be98f42c79115a9eb5a40d7165ea0bf21eb93142cc4aec767b","afterSha":"f6fb88d2d299e0a20d94fad3584bf94566003b2916e442458c8abb9337a8de22","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Attendees;

/// <summary>Defines attendee status for the current use case.</summary>
public enum AttendeeStatus
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

## before — src/EventBooking.Domain/EmployeeGroups/EmployeeGroup.cs — 1/1

<!-- vocabulary-file: {"id":104,"oldPath":"src/EventBooking.Domain/EmployeeGroups/EmployeeGroup.cs","newPath":"src/EventBooking.Domain/AttendeeGroups/AttendeeGroup.cs","beforeSha":"2c78ca13024f6ab679f254566f17361955aea427d18f8e292b1dd63e38d33950","afterSha":"91a43af2ae168824ba0dcda534934654c88fe50632cdde5f97fba4420c2a048a","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Domain/AttendeeGroups/AttendeeGroup.cs — 1/1

<!-- vocabulary-file: {"id":104,"oldPath":"src/EventBooking.Domain/EmployeeGroups/EmployeeGroup.cs","newPath":"src/EventBooking.Domain/AttendeeGroups/AttendeeGroup.cs","beforeSha":"2c78ca13024f6ab679f254566f17361955aea427d18f8e292b1dd63e38d33950","afterSha":"91a43af2ae168824ba0dcda534934654c88fe50632cdde5f97fba4420c2a048a","side":"after","part":1,"parts":1} -->

`````csharp
using System.Text.RegularExpressions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.AttendeeGroups;

/// <summary>One change-controlled employment category that determines attendee requirements.</summary>
public sealed class AttendeeGroup
{
    private static readonly Regex CanonicalCodeExpression =
        new("^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$", RegexOptions.CultureInvariant);

    private readonly List<AttendeeGroupRequirement> _requirements = [];

    private AttendeeGroup()
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

    /// <summary>Gets whether new and changed Attendees may be assigned this group.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the fixed Appointment Type mappings owned by this group.</summary>
    public IReadOnlyList<AttendeeGroupRequirement> Requirements => _requirements;

    /// <summary>Gets the mapped Appointment Type identifiers in stable identifier order.</summary>
    public IReadOnlyList<Guid> RequiredAppointmentTypeIds =>
        _requirements.Select(requirement => requirement.AppointmentTypeId).Order().ToList();

    /// <summary>Defines one validated reference-data row and its complete mapping.</summary>
    /// <param name="id">The id.</param>
    /// <param name="code">The code.</param>
    /// <param name="name">The name.</param>
    /// <param name="isActive">The is active.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public static AttendeeGroup Define(
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
        Guard.Against(!isActive, "An inactive attendee group cannot be assignment authority.");

        var displayName = Guard.NotBlank(name, "name");
        var mapping = appointmentTypeIds.ToList();

        Guard.Against(mapping.Count == 0, "An attendee group must map at least one appointment type.");
        Guard.Against(
            mapping.Distinct().Count() != mapping.Count,
            "An attendee group cannot map the same appointment type twice.");

        foreach (var appointmentTypeId in mapping)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

        var group = new AttendeeGroup
        {
            Id = id,
            Code = code!,
            Name = displayName,
            IsActive = isActive,
        };

        foreach (var appointmentTypeId in mapping.Order())
        {
            group._requirements.Add(AttendeeGroupRequirement.For(id, appointmentTypeId));
        }

        return group;
    }
}
`````

## before — src/EventBooking.Domain/EmployeeGroups/EmployeeGroupIds.cs — 1/1

<!-- vocabulary-file: {"id":105,"oldPath":"src/EventBooking.Domain/EmployeeGroups/EmployeeGroupIds.cs","newPath":"src/EventBooking.Domain/AttendeeGroups/AttendeeGroupIds.cs","beforeSha":"c38b4a39e8d86dafc0f4e5806a777da06eb7ecbd2747f3b3a68dab224bd25f0b","afterSha":"09982a7f58d754d7e37ffbe93d702f99ca7abd0c3c4fedd349a21d09b42ea0bc","side":"before","part":1,"parts":1} -->

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
