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
