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
