namespace EventBooking.Domain.Candidates;

/// <summary>Defines candidate status for the current use case.</summary>
public enum CandidateStatus
{
    /// <summary>Defines not yet invited for the current use case.</summary>
    NotYetInvited = 1,
    /// <summary>Defines awaiting availability for the current use case.</summary>
    AwaitingAvailability = 2,
    /// <summary>Defines invited for the current use case.</summary>
    Invited = 3,
    /// <summary>Defines booked for the current use case.</summary>
    Booked = 4,
    /// <summary>Defines no response needs follow up for the current use case.</summary>
    NoResponseNeedsFollowUp = 5,
}
