namespace EventBooking.Application.Common;

/// <summary>A machine-readable code plus text safe to show a user.</summary>
/// <param name="Code">The code.</param>
/// <param name="Message">The message.</param>
/// <param name="Data">Machine-readable refusal detail, keyed by field.</param>
public sealed record Error(string Code, string Message, IReadOnlyDictionary<string, long>? Data = null)
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
    public const string AttendeeGroupActiveBookingConflictCode = "attendee_group_active_booking_conflict";

    /// <summary>Creates a group change rejected by an active Booking.</summary>
    /// <param name="message">The message.</param>
    public static Error AttendeeGroupActiveBookingConflict(string message) =>
        new(AttendeeGroupActiveBookingConflictCode, message);

    /// <summary>Identifies materialized requirements disagreeing with authoritative state.</summary>
    public const string AttendeeRequirementSnapshotMismatchCode = "attendee_requirement_snapshot_mismatch";

    /// <summary>Creates a mismatch between materialized and authoritative requirements.</summary>
    /// <param name="message">The message.</param>
    public static Error AttendeeRequirementSnapshotMismatch(string message) =>
        new(AttendeeRequirementSnapshotMismatchCode, message);

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

    /// <summary>Identifies reference data that live use keeps from changing.</summary>
    public const string ReferenceDataInUseCode = "in-use";

    /// <summary>Creates a refusal carrying its blocking counts.</summary>
    /// <param name="message">The message.</param>
    /// <param name="blocking">Each kind of live use, with its count.</param>
    public static Error ReferenceDataInUse(string message, IReadOnlyDictionary<string, int> blocking) =>
        new(ReferenceDataInUseCode, message, blocking.ToDictionary(kv => kv.Key, kv => (long)kv.Value));

    /// <summary>Identifies a group change blocked by members holding active bookings.</summary>
    public const string RequirementsLockedCode = "requirements-locked";

    /// <summary>Creates a group change refused by booked members.</summary>
    /// <param name="message">The message.</param>
    /// <param name="blockingMembers">How many members hold an active booking.</param>
    public static Error RequirementsLocked(string message, int blockingMembers) =>
        new(RequirementsLockedCode, message, new Dictionary<string, long> { ["blockingMembers"] = blockingMembers });

    /// <summary>Identifies a write against a stale optimistic-concurrency version.</summary>
    public const string VersionConflictCode = "version-conflict";

    /// <summary>Creates a stale-version refusal carrying the current version.</summary>
    /// <param name="message">The message.</param>
    /// <param name="currentVersion">The version the row carries now.</param>
    public static Error VersionConflict(string message, long currentVersion) =>
        new(VersionConflictCode, message, new Dictionary<string, long> { ["currentVersion"] = currentVersion });
}
