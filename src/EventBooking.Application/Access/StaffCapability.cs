namespace EventBooking.Application.Access;

/// <summary>Defines staff capability for the current use case.</summary>
public enum StaffCapability
{
    /// <summary>Defines contract for the current use case.</summary>
    ManageSettings,
    /// <summary>Defines contract for the current use case.</summary>
    ManageStaffAccess,
    /// <summary>Defines contract for the current use case.</summary>
    ImportEvents,
    /// <summary>Defines contract for the current use case.</summary>
    ManageAttendees,
    /// <summary>Defines contract for the current use case.</summary>
    ViewAttendeeDashboards,
    /// <summary>Defines contract for the current use case.</summary>
    ViewAttendeeAudit,
    /// <summary>Defines contract for the current use case.</summary>
    ViewEventAudit,
    /// <summary>Defines contract for the current use case.</summary>
    ManageEventNegotiation,
    /// <summary>Defines contract for the current use case.</summary>
    ViewEventOperations,
    /// <summary>Defines contract for the current use case.</summary>
    CancelEvent,
    /// <summary>Defines contract for the current use case.</summary>
    ConductAppointments,
}
