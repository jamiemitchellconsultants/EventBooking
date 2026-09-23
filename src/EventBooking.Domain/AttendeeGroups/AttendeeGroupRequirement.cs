namespace EventBooking.Domain.AttendeeGroups;

/// <summary>One fixed Appointment Type required by an Attendee Group.</summary>
public sealed class AttendeeGroupRequirement
{
    private AttendeeGroupRequirement()
    {
    }

    /// <summary>Gets the owning Attendee Group identifier.</summary>
    public Guid AttendeeGroupId { get; private set; }

    /// <summary>Gets the required fixed Appointment Type identifier.</summary>
    public Guid AppointmentTypeId { get; private set; }

    internal static AttendeeGroupRequirement For(Guid attendeeGroupId, Guid appointmentTypeId) =>
        new() { AttendeeGroupId = attendeeGroupId, AppointmentTypeId = appointmentTypeId };
}
