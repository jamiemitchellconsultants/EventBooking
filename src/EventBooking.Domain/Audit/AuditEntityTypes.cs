namespace EventBooking.Domain.Audit;

/// <summary>Defines audit entity types for the current use case.</summary>
public static class AuditEntityTypes
{
    /// <summary>Defines slot proposal for the current use case.</summary>
    public const string SlotProposal = "SlotProposal";
    /// <summary>Defines confirmed slot for the current use case.</summary>
    public const string ConfirmedSlot = "ConfirmedSlot";
    /// <summary>Defines invite for the current use case.</summary>
    public const string Invite = "Invite";
    /// <summary>Defines booking for the current use case.</summary>
    public const string Booking = "Booking";
    /// <summary>Defines staff access profile for the current use case.</summary>
    public const string StaffAccessProfile = "StaffAccessProfile";
    /// <summary>Audit entity name for one independently progressing booking appointment.</summary>
    public const string BookingAppointment = "BookingAppointment";
    /// <summary>Audit entity name for one invited person and their derived requirements.</summary>
    public const string Candidate = "Candidate";

    /// <summary>Defines all for the current use case.</summary>
    public static readonly IReadOnlyList<string> All =
        [SlotProposal, ConfirmedSlot, Invite, Booking, StaffAccessProfile, BookingAppointment, Candidate];
}
