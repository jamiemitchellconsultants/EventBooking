namespace EventBooking.Domain.Slots;

/// <summary>Defines slot proposal status for the current use case.</summary>
public enum SlotProposalStatus
{
    /// <summary>Defines open for the current use case.</summary>
    Open = 1,
    /// <summary>Defines withdrawn for the current use case.</summary>
    Withdrawn = 2,
    /// <summary>Defines confirmed for the current use case.</summary>
    Confirmed = 3,
}
