namespace EventBooking.Domain.SelfRegistrations;

/// <summary>Defines self registration status for the current use case.</summary>
public enum SelfRegistrationStatus
{
    /// <summary>Defines pending for the current use case.</summary>
    Pending = 1,
    /// <summary>Defines confirmed for the current use case.</summary>
    Confirmed = 2,
    /// <summary>Defines expired for the current use case.</summary>
    Expired = 3,
}
