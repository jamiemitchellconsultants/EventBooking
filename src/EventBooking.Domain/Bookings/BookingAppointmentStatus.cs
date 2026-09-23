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
