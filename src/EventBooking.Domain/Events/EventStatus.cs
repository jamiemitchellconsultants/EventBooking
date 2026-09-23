namespace EventBooking.Domain.Events;

/// <summary>Defines event status for the current use case.</summary>
public enum EventStatus
{
    /// <summary>Defines active for the current use case.</summary>
    Active = 1,
    /// <summary>Defines cancelled for the current use case.</summary>
    Cancelled = 2,
}
