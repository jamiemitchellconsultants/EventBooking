namespace EventBooking.Domain.Invites;

/// <summary>
/// One Location a Coordinator selected when issuing an Invite. Options, replacements and automatic
/// re-issues are drawn only from events at these locations.
/// </summary>
public sealed class InviteLocation
{
    private InviteLocation()
    {
    }

    /// <summary>Gets the owning Invite identifier.</summary>
    public Guid InviteId { get; private set; }

    /// <summary>Gets the selected Location identifier.</summary>
    public Guid LocationId { get; private set; }

    internal static InviteLocation For(Guid inviteId, Guid locationId) =>
        new() { InviteId = inviteId, LocationId = locationId };
}
