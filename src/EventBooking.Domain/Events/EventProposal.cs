using EventBooking.Domain.Common;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Domain.Events;

/// <summary>Defines event proposal for the current use case.</summary>
public sealed class EventProposal
{
    private readonly List<ProposalAcceptance> _acceptances = [];

    private EventProposal()
    {
        // Required by the persistence layer's constructor binding.
        Window = null!;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines window for the current use case.</summary>
    public EventWindow Window { get; private set; }

    /// <summary>Defines status for the current use case.</summary>
    public EventProposalStatus Status { get; private set; } = EventProposalStatus.Open;

    /// <summary>Defines created by manager user id for the current use case.</summary>
    public Guid CreatedByManagerUserId { get; private set; }

    /// <summary>Defines acceptances for the current use case.</summary>
    public IReadOnlyList<ProposalAcceptance> Acceptances => _acceptances;

    /// <summary>Defines create for the current use case.</summary>
    /// <param name="id">The id.</param>
    /// <param name="window">The window.</param>
    /// <param name="createdByManagerUserId">The created by manager user id.</param>
    public static EventProposal Create(Guid id, EventWindow window, Guid createdByManagerUserId)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(window is null, "window must be supplied.");
        Guard.Against(createdByManagerUserId == Guid.Empty, "createdByManagerUserId must not be empty.");

        return new EventProposal
        {
            Id = id,
            Window = window!,
            Status = EventProposalStatus.Open,
            CreatedByManagerUserId = createdByManagerUserId,
        };
    }

    /// <summary>Withdraws an open proposal. Any Manager in scope or Admin may act, not just the creator.</summary>
    /// <param name="managerUserId">The manager user id.</param>
    public void Withdraw(Guid managerUserId)
    {
        Guard.Against(Status != EventProposalStatus.Open, "Only an open proposal can be withdrawn.");
        Guard.Against(managerUserId == Guid.Empty, "managerUserId must not be empty.");

        Status = EventProposalStatus.Withdrawn;
    }

    /// <summary>Defines accept for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    /// <param name="managerUserId">The manager user id.</param>
    /// <param name="headcount">The headcount.</param>
    public bool Accept(Guid appointmentTypeId, Guid managerUserId, int headcount)
    {
        Guard.Against(Status != EventProposalStatus.Open, "Only an open proposal can be accepted.");
        AppointmentTypeIds.EnsureKnown(appointmentTypeId);

        var existing = _acceptances.SingleOrDefault(
            acceptance => acceptance.AppointmentTypeId == appointmentTypeId);

        if (existing is null)
        {
            _acceptances.Add(
                ProposalAcceptance.Record(Id, appointmentTypeId, managerUserId, headcount));
            return true;
        }

        return existing.ChangeHeadcount(headcount);
    }

    /// <summary>Defines withdraw acceptance for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    /// <param name="managerUserId">The manager user id.</param>
    public void WithdrawAcceptance(Guid appointmentTypeId, Guid managerUserId)
    {
        Guard.Against(
            Status != EventProposalStatus.Open,
            "An acceptance can only be withdrawn while the proposal is still open.");
        AppointmentTypeIds.EnsureKnown(appointmentTypeId);

        var acceptance = _acceptances.SingleOrDefault(a => a.AppointmentTypeId == appointmentTypeId);
        Guard.Against(acceptance is null, "This appointment type has not accepted the proposal.");

        _acceptances.Remove(acceptance!);
    }

    /// <summary>Defines is accepted by for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public bool IsAcceptedBy(Guid appointmentTypeId) =>
        _acceptances.Any(a => a.AppointmentTypeId == appointmentTypeId);

    /// <summary>Defines is fully accepted for the current use case.</summary>
    public bool IsFullyAccepted =>
        Status == EventProposalStatus.Open
        && AppointmentTypeIds.All.All(IsAcceptedBy);

    internal void MarkConfirmed()
    {
        Guard.Against(!IsFullyAccepted, "A proposal can only be confirmed once all 3 managers have accepted it.");
        Status = EventProposalStatus.Confirmed;
    }
}
