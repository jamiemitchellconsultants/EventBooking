namespace EventBooking.Domain.Invites;

/// <summary>One fixed Appointment Type snapshotted for an initial or recovery Invite.</summary>
public sealed class InviteRequirement
{
    private InviteRequirement()
    {
    }

    /// <summary>Gets the owning Invite identifier.</summary>
    public Guid InviteId { get; private set; }

    /// <summary>Gets the snapshotted fixed Appointment Type identifier.</summary>
    public Guid AppointmentTypeId { get; private set; }

    internal static InviteRequirement For(Guid inviteId, Guid appointmentTypeId) =>
        new() { InviteId = inviteId, AppointmentTypeId = appointmentTypeId };
}
