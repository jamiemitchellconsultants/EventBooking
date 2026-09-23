namespace EventBooking.Domain.Audit;

/// <summary>Defines audit action for the current use case.</summary>
public enum AuditAction
{
    /// <summary>Defines proposal created for the current use case.</summary>
    ProposalCreated = 1,
    /// <summary>Defines proposal withdrawn for the current use case.</summary>
    ProposalWithdrawn = 2,
    /// <summary>Defines acceptance recorded for the current use case.</summary>
    AcceptanceRecorded = 3,
    /// <summary>Defines acceptance withdrawn for the current use case.</summary>
    AcceptanceWithdrawn = 4,
    /// <summary>Defines event confirmed for the current use case.</summary>
    EventConfirmed = 5,
    /// <summary>Defines event cancelled for the current use case.</summary>
    EventCancelled = 6,
    /// <summary>Defines capacity decremented for the current use case.</summary>
    CapacityDecremented = 7,
    /// <summary>Defines capacity incremented for the current use case.</summary>
    CapacityIncremented = 8,
    /// <summary>Defines invite created for the current use case.</summary>
    InviteCreated = 9,
    /// <summary>Defines invite sent for the current use case.</summary>
    InviteSent = 10,
    /// <summary>Defines invite expired for the current use case.</summary>
    InviteExpired = 11,
    /// <summary>Defines invite option replaced for the current use case.</summary>
    InviteOptionReplaced = 12,
    /// <summary>Defines booking created for the current use case.</summary>
    BookingCreated = 13,
    /// <summary>Defines booking cancelled for the current use case.</summary>
    BookingCancelled = 14,
    /// <summary>Defines capacity adjusted for the current use case.</summary>
    CapacityAdjusted = 15,
    /// <summary>Defines staff access changed for the current use case.</summary>
    StaffAccessChanged = 17,
    /// <summary>Records an Expected appointment moving to CheckedIn.</summary>
    AppointmentCheckedIn = 19,
    /// <summary>Records a CheckedIn appointment moving to Completed.</summary>
    AppointmentCompleted = 20,
    /// <summary>Records an Expected appointment moving to NoShow.</summary>
    AppointmentMarkedNoShow = 21,
    /// <summary>Records one approved reverse appointment transition.</summary>
    AppointmentStatusCorrected = 22,
    /// <summary>Records the initial Attendee Group assignment of a Attendee.</summary>
    AttendeeGroupAssigned = 23,
    /// <summary>Records a Attendee Attendee Group change and its derived requirements.</summary>
    AttendeeGroupReassigned = 24,
    /// <summary>Records a Coordinator issuing a recovery Invite for missed appointments.</summary>
    RecoveryInviteCreated = 25,
    /// <summary>Records a Coordinator cancelling a pending recovery Invite.</summary>
    RecoveryInviteCancelled = 26,
    /// <summary>Records a recovery Booking linked to its original journey root.</summary>
    RecoveryBookingCreated = 27,
    /// <summary>Records a recovery Booking concluded after terminal appointment outcomes.</summary>
    RecoveryBookingConcluded = 28,
    /// <summary>Records an identity-provider-driven role change applied by the claims sync.</summary>
    StaffRolesSynced = 29,
    /// <summary>Records a Coordinator deleting a Attendee and cascading onto their active bookings.</summary>
    AttendeeDeleted = 30,
}
