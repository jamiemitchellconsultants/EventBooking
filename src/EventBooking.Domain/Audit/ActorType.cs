namespace EventBooking.Domain.Audit;

/// <summary>Defines actor type for the current use case.</summary>
public enum ActorType
{
    /// <summary>Defines staff for the current use case.</summary>
    Staff = 1,
    /// <summary>Defines attendee token for the current use case.</summary>
    AttendeeToken = 2,
    /// <summary>Defines system for the current use case.</summary>
    System = 3,
}
