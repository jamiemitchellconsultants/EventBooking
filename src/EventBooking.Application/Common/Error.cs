namespace EventBooking.Application.Common;

/// <summary>A machine-readable code plus text safe to show a user.</summary>
/// <param name="Code">The code.</param>
/// <param name="Message">The message.</param>
public sealed record Error(string Code, string Message)
{
    /// <summary>Identifies a stale booking-appointment version conflict.</summary>
    public const string AppointmentVersionConflictCode = "appointment_version_conflict";

    /// <summary>Defines none for the current use case.</summary>
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>Defines validation for the current use case.</summary>
    /// <param name="message">The message.</param>
    public static Error Validation(string message) => new("validation", message);

    /// <summary>Defines not found for the current use case.</summary>
    /// <param name="message">The message.</param>
    public static Error NotFound(string message) => new("not_found", message);

    /// <summary>Defines conflict for the current use case.</summary>
    /// <param name="message">The message.</param>
    public static Error Conflict(string message) => new("conflict", message);

    /// <summary>Creates a stale booking-appointment version conflict.</summary>
    /// <param name="message">The message.</param>
    public static Error AppointmentVersionConflict(string message) =>
        new(AppointmentVersionConflictCode, message);

    /// <summary>Identifies a group change that would alter an active Booking's requirements.</summary>
    public const string CandidateGroupActiveBookingConflictCode = "candidate_group_active_booking_conflict";

    /// <summary>Creates a group change rejected by an active Booking.</summary>
    /// <param name="message">The message.</param>
    public static Error CandidateGroupActiveBookingConflict(string message) =>
        new(CandidateGroupActiveBookingConflictCode, message);

    /// <summary>Identifies an Invite or readiness action for a legacy unassigned Candidate.</summary>
    public const string CandidateReconciliationRequiredCode = "candidate_reconciliation_required";

    /// <summary>Creates a reconciliation hold for a legacy unassigned Candidate.</summary>
    /// <param name="message">The message.</param>
    public static Error CandidateReconciliationRequired(string message) =>
        new(CandidateReconciliationRequiredCode, message);

    /// <summary>Identifies materialized requirements disagreeing with authoritative state.</summary>
    public const string CandidateRequirementSnapshotMismatchCode = "candidate_requirement_snapshot_mismatch";

    /// <summary>Creates a mismatch between materialized and authoritative requirements.</summary>
    /// <param name="message">The message.</param>
    public static Error CandidateRequirementSnapshotMismatch(string message) =>
        new(CandidateRequirementSnapshotMismatchCode, message);

    /// <summary>Defines forbidden for the current use case.</summary>
    /// <param name="message">The message.</param>
    public static Error Forbidden(string message) => new("forbidden", message);

    /// <summary>Identifies a recovery request while a pending Invite or active recovery exists.</summary>
    public const string RecoveryAlreadyPendingCode = "recovery_already_pending";

    /// <summary>Creates a recovery request rejected by an already-pending recovery.</summary>
    /// <param name="message">The message.</param>
    public static Error RecoveryAlreadyPending(string message) =>
        new(RecoveryAlreadyPendingCode, message);

    /// <summary>Identifies a recovery request with no recoverable no-show type.</summary>
    public const string RecoveryNotAvailableCode = "recovery_not_available";

    /// <summary>Creates a recovery request with nothing eligible to recover.</summary>
    /// <param name="message">The message.</param>
    public static Error RecoveryNotAvailable(string message) =>
        new(RecoveryNotAvailableCode, message);

    /// <summary>Identifies a recovery request invalidated by a concurrent eligibility change.</summary>
    public const string RecoveryStateChangedCode = "recovery_state_changed";

    /// <summary>Creates a recovery request invalidated by a concurrent eligibility change.</summary>
    /// <param name="message">The message.</param>
    public static Error RecoveryStateChanged(string message) =>
        new(RecoveryStateChangedCode, message);
}
