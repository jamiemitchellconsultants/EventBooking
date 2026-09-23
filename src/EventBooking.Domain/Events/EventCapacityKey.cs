namespace EventBooking.Domain.Events;

/// <summary>
/// The key of one <see cref="EventCapacity"/> row. A command that touches several rows sorts its
/// keys with <see cref="Event.CapacityLockOrder"/> before locking, so two commands whose type sets
/// overlap can never deadlock (FR-3.3; design 01 — lock ordering).
/// </summary>
/// <param name="EventId">The event whose capacity row this is.</param>
/// <param name="AppointmentTypeId">The appointment type the row counts.</param>
public readonly record struct EventCapacityKey(Guid EventId, Guid AppointmentTypeId);
