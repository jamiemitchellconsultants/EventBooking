namespace EventBooking.Domain.Events;

/// <summary>
/// One appointment type listed on a proposal. The list is fixed at creation: changing it after
/// other Managers have accepted would make their headcounts refer to a different event.
/// </summary>
public sealed class EventProposalAppointmentType
{
    private EventProposalAppointmentType()
    {
    }

    /// <summary>The proposal that lists the type.</summary>
    public Guid ProposalId { get; private set; }

    /// <summary>The listed appointment type.</summary>
    public Guid AppointmentTypeId { get; private set; }

    internal static EventProposalAppointmentType For(Guid proposalId, Guid appointmentTypeId) =>
        new() { ProposalId = proposalId, AppointmentTypeId = appointmentTypeId };
}
