namespace EventBooking.Domain.Notifications;

/// <summary>Defines email template for the current use case.</summary>
public enum EmailTemplate
{
    /// <summary>Defines candidate invite for the current use case.</summary>
    CandidateInvite = 1,
    /// <summary>Defines booking confirmation for the current use case.</summary>
    BookingConfirmation = 2,
    /// <summary>Defines slot cancelled rebooking needed for the current use case.</summary>
    SlotCancelledRebookingNeeded = 3,
    /// <summary>Defines candidate reinvite for the current use case.</summary>
    CandidateReinvite = 4,
}
