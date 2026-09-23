namespace EventBooking.Domain.Invites;

/// <summary>One of the events offered inside an invite.</summary>
public sealed class InviteOption
{
    private InviteOption()
    {
    }

    /// <summary>Defines invite id for the current use case.</summary>
    public Guid InviteId { get; private set; }

    /// <summary>Defines event id for the current use case.</summary>
    public Guid EventId { get; private set; }

    internal static InviteOption For(Guid inviteId, Guid eventId) =>
        new() { InviteId = inviteId, EventId = eventId };
}
