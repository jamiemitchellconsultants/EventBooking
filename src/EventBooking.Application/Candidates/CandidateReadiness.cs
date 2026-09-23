namespace EventBooking.Application.Candidates;

/// <summary>Explains whether EventBooking has completed every current Candidate requirement.</summary>
public enum CandidateReadinessCode
{
    /// <summary>Every current requirement has a Completed non-cancelled attempt.</summary>
    Ready = 1,
    /// <summary>The Candidate has no assigned Employee Group during Release 1 reconciliation.</summary>
    EmployeeGroupUnassigned = 2,
    /// <summary>The Candidate has no Active original Booking journey.</summary>
    NoActiveBooking = 3,
    /// <summary>The current requirements differ from the journey's Appointment Type snapshots.</summary>
    RequirementSnapshotMismatch = 4,
    /// <summary>At least one current requirement has no Completed non-cancelled attempt.</summary>
    AppointmentsOutstanding = 5,
}

/// <summary>One booked attempt used to choose the latest non-cancelled result per type.</summary>
/// <param name="BookingAppointmentId">The stable appointment-record identifier.</param>
/// <param name="AppointmentTypeId">The attempted appointment type.</param>
/// <param name="Status">The appointment's operational status.</param>
/// <param name="BookingId">The parent booking identifier.</param>
/// <param name="BookingStatus">The parent booking lifecycle status.</param>
/// <param name="BookingCreatedAt">When the parent booking was created.</param>
public sealed record CandidateReadinessAttempt(
    Guid BookingAppointmentId,
    Guid AppointmentTypeId,
    EventBooking.Domain.Bookings.BookingAppointmentStatus Status,
    Guid BookingId,
    EventBooking.Domain.Bookings.BookingStatus BookingStatus,
    DateTimeOffset BookingCreatedAt);

/// <summary>The authorized persistence projection consumed by the readiness calculator.</summary>
/// <param name="CandidateId">The candidate identifier.</param>
/// <param name="EmployeeGroupId">The assigned group, or null during reconciliation.</param>
/// <param name="CurrentRequirementTypeIds">The group's current requirement set.</param>
/// <param name="ActiveOriginalBookingId">The active journey root, or null when absent.</param>
/// <param name="Attempts">Every booked attempt in the original and recovery journey.</param>
public sealed record CandidateReadinessSnapshot(
    Guid CandidateId,
    Guid? EmployeeGroupId,
    IReadOnlyList<Guid> CurrentRequirementTypeIds,
    Guid? ActiveOriginalBookingId,
    IReadOnlyList<CandidateReadinessAttempt> Attempts);

/// <summary>Minimum canonical detail for one incomplete current Appointment Type.</summary>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="Name">The canonical appointment-type name.</param>
/// <param name="IsRecoverable">Whether the latest attempt is a recoverable no-show.</param>
public sealed record OutstandingAppointmentType(
    string Code,
    string Name,
    bool IsRecoverable);

/// <summary>The internal EventBooking readiness result shown to a Coordinator.</summary>
/// <param name="CandidateId">The candidate identifier.</param>
/// <param name="Code">The machine-readable readiness reason.</param>
/// <param name="OutstandingAppointmentTypes">Incomplete types sorted by code.</param>
public sealed record CandidateReadiness(
    Guid CandidateId,
    CandidateReadinessCode Code,
    IReadOnlyList<OutstandingAppointmentType> OutstandingAppointmentTypes);
