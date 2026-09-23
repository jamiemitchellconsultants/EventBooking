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
