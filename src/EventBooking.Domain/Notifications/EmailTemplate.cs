namespace EventBooking.Domain.Notifications;

/// <summary>Defines email template for the current use case.</summary>
public enum EmailTemplate
{
    /// <summary>Defines attendee invite for the current use case.</summary>
    AttendeeInvite = 1,
    /// <summary>Defines booking confirmation for the current use case.</summary>
    BookingConfirmation = 2,
    /// <summary>Defines event cancelled rebooking needed for the current use case.</summary>
    EventCancelledRebookingNeeded = 3,
    /// <summary>Defines attendee reinvite for the current use case.</summary>
    AttendeeReinvite = 4,
}
