namespace EventBooking.Domain.Attendees;

/// <summary>One appointment type a given attendee has to attend.</summary>
public sealed class AttendeeRequirement
{
    private AttendeeRequirement()
    {
    }

    /// <summary>Defines attendee id for the current use case.</summary>
    public Guid AttendeeId { get; private set; }

    /// <summary>Defines appointment type id for the current use case.</summary>
    public Guid AppointmentTypeId { get; private set; }

    internal static AttendeeRequirement For(Guid attendeeId, Guid appointmentTypeId) =>
        new() { AttendeeId = attendeeId, AppointmentTypeId = appointmentTypeId };
}
