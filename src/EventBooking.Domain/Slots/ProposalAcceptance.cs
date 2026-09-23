using EventBooking.Domain.Common;

namespace EventBooking.Domain.Slots;

/// <summary>One manager's acceptance of a proposal, carrying their own headcount.</summary>
public sealed class ProposalAcceptance
{
    private ProposalAcceptance()
    {
    }

    /// <summary>Defines proposal id for the current use case.</summary>
    public Guid ProposalId { get; private set; }

    /// <summary>Defines appointment type id for the current use case.</summary>
    public Guid AppointmentTypeId { get; private set; }

    /// <summary>Defines manager user id for the current use case.</summary>
    public Guid ManagerUserId { get; private set; }

    /// <summary>Defines headcount for the current use case.</summary>
    public int Headcount { get; private set; }

    internal static ProposalAcceptance Record(
        Guid proposalId,
        Guid appointmentTypeId,
        Guid managerUserId,
        int headcount)
    {
        Guard.Against(managerUserId == Guid.Empty, "managerUserId must not be empty.");

        return new ProposalAcceptance
        {
            ProposalId = proposalId,
            AppointmentTypeId = appointmentTypeId,
            ManagerUserId = managerUserId,
            Headcount = Guard.Positive(headcount, "headcount"),
        };
    }

    internal bool ChangeHeadcount(int headcount)
    {
        var next = Guard.Positive(headcount, "headcount");
        if (next == Headcount)
        {
            return false;
        }

        Headcount = next;
        return true;
    }
}
