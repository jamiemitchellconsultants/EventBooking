namespace EventBooking.Domain.EventGroups;

/// <summary>Membership of one Event in one Event Group, with its own public-registration gate.</summary>
public sealed class EventGroupEvent
{
    private EventGroupEvent()
    {
    }

    /// <summary>Gets the owning Event Group identifier.</summary>
    public Guid EventGroupId { get; private set; }

    /// <summary>Gets the member Event identifier.</summary>
    public Guid EventId { get; private set; }

    /// <summary>Gets whether anonymous registration is open for this membership.</summary>
    public bool IsOpen { get; private set; }

    internal static EventGroupEvent Join(Guid eventGroupId, Guid eventId) =>
        new() { EventGroupId = eventGroupId, EventId = eventId, IsOpen = false };

    internal void SetOpen(bool open) => IsOpen = open;
}
