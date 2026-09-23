using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Appointments;

/// <summary>Counts scoped booking appointments in each operational state.</summary>
public sealed record AppointmentStatusCounts
{
    /// <summary>Gets attendees booked but not checked in for this appointment.</summary>
    public required int Expected { get; init; }
    /// <summary>Gets attendees checked in for this appointment.</summary>
    public required int CheckedIn { get; init; }
    /// <summary>Gets required appointments completed after check-in.</summary>
    public required int Completed { get; init; }
    /// <summary>Gets attendees recorded as not attending this required appointment.</summary>
    public required int NoShow { get; init; }
}

/// <summary>Describes one selectable active event without attendee rows.</summary>
public sealed record AppointmentEventSummary
{
    /// <summary>Gets the event identifier.</summary>
    public required Guid EventId { get; init; }
    /// <summary>Gets the event's transitional-location calendar date.</summary>
    public required DateOnly Date { get; init; }
    /// <summary>Gets the start of the shared four-hour window.</summary>
    public required TimeOnly StartTime { get; init; }
    /// <summary>Gets the derived end of the shared four-hour window.</summary>
    public required TimeOnly EndTime { get; init; }
    /// <summary>Gets scoped counts grouped by independent operational state.</summary>
    public required AppointmentStatusCounts Counts { get; init; }
}

/// <summary>Returns the trusted appointment-type name and its selectable active events.</summary>
public sealed record AppointmentWorkspaceEventList
{
    /// <summary>Gets the fixed appointment-type name for the caller's trusted scope.</summary>
    public required string AppointmentTypeName { get; init; }
    /// <summary>Gets current and upcoming active events containing scoped active bookings.</summary>
    public required IReadOnlyList<AppointmentEventSummary> Events { get; init; }
}

/// <summary>Contains only the fields needed to identify and conduct one booked appointment.</summary>
public sealed record BookingAppointmentRow
{
    /// <summary>Gets the stable booking-appointment command identifier.</summary>
    public required Guid BookingAppointmentId { get; init; }
    /// <summary>Gets the attendee name used for primary human identification.</summary>
    public required string AttendeeName { get; init; }
    /// <summary>Gets the attendee email used for secondary human identification.</summary>
    public required string AttendeeEmail { get; init; }
    /// <summary>Gets this appointment's independent operational status.</summary>
    public required BookingAppointmentStatus Status { get; init; }
    /// <summary>Gets when staff checked the attendee in, or null until check-in.</summary>
    public required DateTimeOffset? CheckedInAt { get; init; }
    /// <summary>Gets when staff recorded completion or no-show, or null before an outcome.</summary>
    public required DateTimeOffset? OutcomeAt { get; init; }
    /// <summary>Gets the positive concurrency version required by a status command.</summary>
    public required long Version { get; init; }
}

/// <summary>Returns one scoped active event and only its minimum-data operational rows.</summary>
public sealed record AppointmentEventDetail
{
    /// <summary>Gets the fixed appointment-type name for the caller's trusted scope.</summary>
    public required string AppointmentTypeName { get; init; }
    /// <summary>Gets the selected event identifier.</summary>
    public required Guid EventId { get; init; }
    /// <summary>Gets the event's transitional-location calendar date.</summary>
    public required DateOnly Date { get; init; }
    /// <summary>Gets the start of the shared four-hour window.</summary>
    public required TimeOnly StartTime { get; init; }
    /// <summary>Gets the derived end of the shared four-hour window.</summary>
    public required TimeOnly EndTime { get; init; }
    /// <summary>Gets scoped active-booking appointment rows ordered for staff identification.</summary>
    public required IReadOnlyList<BookingAppointmentRow> Appointments { get; init; }
}
