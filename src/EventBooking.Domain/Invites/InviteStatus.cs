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
