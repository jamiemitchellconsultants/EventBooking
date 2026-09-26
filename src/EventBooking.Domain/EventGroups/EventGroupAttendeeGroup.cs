namespace EventBooking.Domain.EventGroups;

/// <summary>One active Attendee Group selectable for self-registration in an Event Group.</summary>
public sealed class EventGroupAttendeeGroup
{
    private EventGroupAttendeeGroup()
    {
    }

    /// <summary>Gets the owning Event Group identifier.</summary>
    public Guid EventGroupId { get; private set; }

    /// <summary>Gets the selected Attendee Group identifier.</summary>
    public Guid AttendeeGroupId { get; private set; }

    internal static EventGroupAttendeeGroup For(Guid eventGroupId, Guid attendeeGroupId) =>
        new() { EventGroupId = eventGroupId, AttendeeGroupId = attendeeGroupId };
}
