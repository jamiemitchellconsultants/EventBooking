namespace EventBooking.Application.Access;

/// <summary>Defines staff capability for the current use case.</summary>
public enum StaffCapability
{
    /// <summary>Defines contract for the current use case.</summary>
    ManageSettings,
    /// <summary>Defines contract for the current use case.</summary>
    ManageStaffAccess,
    /// <summary>Defines contract for the current use case.</summary>
    ImportConfirmedSlots,
    /// <summary>Defines contract for the current use case.</summary>
    ManageCandidates,
    /// <summary>Defines contract for the current use case.</summary>
    ViewCandidateDashboards,
    /// <summary>Defines contract for the current use case.</summary>
    ViewCandidateAudit,
    /// <summary>Defines contract for the current use case.</summary>
    ViewSlotAudit,
    /// <summary>Defines contract for the current use case.</summary>
    ManageSlotNegotiation,
    /// <summary>Defines contract for the current use case.</summary>
    ViewSlotOperations,
    /// <summary>Defines contract for the current use case.</summary>
    CancelConfirmedSlot,
    /// <summary>Defines contract for the current use case.</summary>
    ConductAppointments,
}
