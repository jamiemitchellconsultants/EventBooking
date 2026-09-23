namespace EventBooking.Domain.Invites;

/// <summary>One of the confirmed slots offered inside an invite.</summary>
public sealed class InviteOption
{
    private InviteOption()
    {
    }

    /// <summary>Defines invite id for the current use case.</summary>
    public Guid InviteId { get; private set; }

    /// <summary>Defines confirmed slot id for the current use case.</summary>
    public Guid ConfirmedSlotId { get; private set; }

    internal static InviteOption For(Guid inviteId, Guid confirmedSlotId) =>
        new() { InviteId = inviteId, ConfirmedSlotId = confirmedSlotId };
}
