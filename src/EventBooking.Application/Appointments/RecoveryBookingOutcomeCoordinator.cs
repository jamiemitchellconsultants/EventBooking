using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;

namespace EventBooking.Application.Appointments;

/// <summary>Moves a recovery Booking with its own locked appointment outcomes.</summary>
public sealed class RecoveryBookingOutcomeCoordinator
{
    /// <summary>Concludes or reopens a recovery Booking from its locked appointment collection.</summary>
    /// <param name="booking">The locked recovery Booking to synchronize.</param>
    /// <param name="appointments">The Booking's locked appointments, in any order.</param>
    /// <param name="laterRecoveryExists">Whether a later recovery covers the corrected type.</param>
    /// <returns>Whether the Booking status changed.</returns>
    public bool Synchronize(
        Booking booking,
        IReadOnlyCollection<BookingAppointment> appointments,
        bool laterRecoveryExists)
    {
        if (booking.IsOriginal)
        {
            return false;
        }

        if (booking.Status == BookingStatus.Active
            && appointments.Count > 0
            && appointments.All(IsTerminal))
        {
            booking.Conclude();
            return true;
        }

        if (booking.Status == BookingStatus.Concluded
            && appointments.Any(appointment => !IsTerminal(appointment)))
        {
            if (laterRecoveryExists)
            {
                throw new DomainException(
                    "A later recovery covers this appointment type. Cancel the recovery first.");
            }

            booking.Reopen();
            return true;
        }

        return false;
    }

    private static bool IsTerminal(BookingAppointment appointment) =>
        appointment.Status is BookingAppointmentStatus.Completed or BookingAppointmentStatus.NoShow;
}
