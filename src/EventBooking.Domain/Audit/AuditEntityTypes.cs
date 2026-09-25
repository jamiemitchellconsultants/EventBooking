namespace EventBooking.Domain.Audit;

/// <summary>Defines audit entity types for the current use case.</summary>
public static class AuditEntityTypes
{
    /// <summary>Defines event proposal for the current use case.</summary>
    public const string EventProposal = "EventProposal";
    /// <summary>Defines event for the current use case.</summary>
    public const string Event = "Event";
    /// <summary>Defines invite for the current use case.</summary>
    public const string Invite = "Invite";
    /// <summary>Defines booking for the current use case.</summary>
    public const string Booking = "Booking";
    /// <summary>Defines staff access profile for the current use case.</summary>
    public const string StaffAccessProfile = "StaffAccessProfile";
    /// <summary>Audit entity name for one independently progressing booking appointment.</summary>
    public const string BookingAppointment = "BookingAppointment";
    /// <summary>Audit entity name for one invited person and their derived requirements.</summary>
    public const string Attendee = "Attendee";
    /// <summary>Audit entity name for one Admin-managed location.</summary>
    public const string Location = "Location";
    /// <summary>Audit entity name for one Admin-managed appointment type.</summary>
    public const string AppointmentType = "AppointmentType";
    /// <summary>Audit entity name for one Admin-managed attendee group.</summary>
    public const string AttendeeGroup = "AttendeeGroup";
    /// <summary>Audit entity name for one staff-managed event group.</summary>
    public const string EventGroup = "EventGroup";
    /// <summary>Audit entity name for the singleton system settings.</summary>
    public const string SystemSettings = "SystemSettings";

    /// <summary>Defines all for the current use case.</summary>
    public static readonly IReadOnlyList<string> All =
        [EventProposal, Event, Invite, Booking, StaffAccessProfile, BookingAppointment, Attendee,
            Location, AppointmentType, AttendeeGroup, EventGroup, SystemSettings];
}
