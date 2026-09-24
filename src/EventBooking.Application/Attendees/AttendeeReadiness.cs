using EventBooking.Domain.Attendees;

namespace EventBooking.Application.Attendees;

/// <summary>Explains whether EventBooking has completed every current Attendee requirement.</summary>
public enum AttendeeReadinessCode
{
    /// <summary>Every current requirement has a Completed non-cancelled attempt.</summary>
    Ready = 1,
    /// <summary>The Attendee has no Active original Booking journey.</summary>
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
public sealed record AttendeeReadinessAttempt(
    Guid BookingAppointmentId,
    Guid AppointmentTypeId,
    EventBooking.Domain.Bookings.BookingAppointmentStatus Status,
    Guid BookingId,
    EventBooking.Domain.Bookings.BookingStatus BookingStatus,
    DateTimeOffset BookingCreatedAt);

/// <summary>The authorized persistence projection consumed by the readiness calculator.</summary>
/// <param name="AttendeeId">The attendee identifier.</param>
/// <param name="AttendeeGroupId">The required assigned group.</param>
/// <param name="CurrentRequirementTypeIds">The group's current requirement set.</param>
/// <param name="ActiveOriginalBookingId">The active journey root, or null when absent.</param>
/// <param name="Attempts">Every booked attempt in the original and recovery journey.</param>
/// <param name="AppointmentTypes">The appointment types by identifier, for codes and names.</param>
public sealed record AttendeeReadinessSnapshot(
    Guid AttendeeId,
    Guid AttendeeGroupId,
    IReadOnlyList<Guid> CurrentRequirementTypeIds,
    Guid? ActiveOriginalBookingId,
    IReadOnlyList<AttendeeReadinessAttempt> Attempts,
    IReadOnlyDictionary<Guid, AttendeeReadinessType> AppointmentTypes);

/// <summary>Minimum canonical detail for one appointment type in the snapshot.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="Name">The canonical appointment-type name.</param>
public sealed record AttendeeReadinessType(Guid Id, string Code, string Name);

/// <summary>Minimum canonical detail for one incomplete current Appointment Type.</summary>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="Name">The canonical appointment-type name.</param>
/// <param name="IsRecoverable">Whether the latest attempt is a recoverable no-show.</param>
public sealed record OutstandingAppointmentType(
    string Code,
    string Name,
    bool IsRecoverable);

/// <summary>The internal EventBooking readiness result shown to a Coordinator.</summary>
/// <param name="AttendeeId">The attendee identifier.</param>
/// <param name="Code">The machine-readable readiness reason.</param>
/// <param name="OutstandingAppointmentTypes">Incomplete types sorted by code.</param>
public sealed record AttendeeReadiness(
    Guid AttendeeId,
    AttendeeReadinessCode Code,
    IReadOnlyList<OutstandingAppointmentType> OutstandingAppointmentTypes)
{
    /// <summary>
    /// The attendee list's per-row readiness label from the status and latest delivery
    /// state alone. The list cannot see booking attempts, so it reports only what the
    /// status entails: every non-booked status means no active booking exists, while a
    /// booked attendee may be ready, outstanding, or concluded — the per-attendee
    /// readiness query decides that. No delivery state changes what the status entails;
    /// delivery travels in its own column.
    /// </summary>
    /// <param name="status">The attendee status.</param>
    /// <param name="delivery">The latest delivery status name, or null when never invited.</param>
    public static string Of(AttendeeStatus status, string? delivery) =>
        status == AttendeeStatus.Booked
            ? nameof(AttendeeStatus.Booked)
            : nameof(AttendeeReadinessCode.NoActiveBooking);
}
