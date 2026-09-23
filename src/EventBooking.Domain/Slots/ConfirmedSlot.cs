using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Slots;

/// <summary>Defines confirmed slot for the current use case.</summary>
public sealed class ConfirmedSlot
{
    private readonly List<SlotCapacity> _capacities = [];

    private ConfirmedSlot()
    {
        // Required by the persistence layer's constructor binding.
        Window = null!;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines proposal id for the current use case.</summary>
    public Guid? ProposalId { get; private set; }

    /// <summary>Defines window for the current use case.</summary>
    public SlotWindow Window { get; private set; }

    /// <summary>Defines status for the current use case.</summary>
    public ConfirmedSlotStatus Status { get; private set; } = ConfirmedSlotStatus.Active;

    /// <summary>Defines capacities for the current use case.</summary>
    public IReadOnlyList<SlotCapacity> Capacities => _capacities;

    /// <summary>
    /// The only way a confirmed slot is created. Marks the proposal confirmed in the same call, so
    /// a proposal can never back a second slot.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="proposal">The proposal.</param>
    public static ConfirmedSlot CreateFrom(Guid id, SlotProposal proposal)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(proposal is null, "proposal must be supplied.");

        proposal!.MarkConfirmed();

        var slot = new ConfirmedSlot
        {
            Id = id,
            ProposalId = proposal.Id,
            Window = proposal.Window,
            Status = ConfirmedSlotStatus.Active,
        };

        foreach (var acceptance in proposal.Acceptances.OrderBy(a => a.AppointmentTypeId))
        {
            slot._capacities.Add(
                SlotCapacity.Initialise(id, acceptance.AppointmentTypeId, acceptance.Headcount));
        }

        return slot;
    }

    /// <summary>
    /// Creates an active confirmed slot with no backing proposal from a strictly complete set of
    /// positive headcounts for the three fixed appointment types.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="window">The window.</param>
    /// <param name="headcountsByAppointmentType">The headcounts by appointment type.</param>
    public static ConfirmedSlot CreateImported(
        Guid id, SlotWindow window, IReadOnlyDictionary<Guid, int> headcountsByAppointmentType)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(window is null, "window must be supplied.");
        Guard.Against(headcountsByAppointmentType is null, "headcountsByAppointmentType must be supplied.");
        Guard.Against(
            headcountsByAppointmentType!.Count != AppointmentTypeIds.All.Count
            || headcountsByAppointmentType.Keys.Any(id => !AppointmentTypeIds.All.Contains(id)),
            "Headcounts must be supplied for exactly the three fixed appointment types.");

        var slot = new ConfirmedSlot
        {
            Id = id,
            ProposalId = null,
            Window = window!,
            Status = ConfirmedSlotStatus.Active,
        };

        foreach (var appointmentTypeId in AppointmentTypeIds.All)
        {
            Guard.Against(
                !headcountsByAppointmentType.TryGetValue(appointmentTypeId, out var headcount),
                $"A headcount is required for {AppointmentTypeIds.NameOf(appointmentTypeId)}.");

            slot._capacities.Add(SlotCapacity.Initialise(id, appointmentTypeId, headcount));
        }

        return slot;
    }

    /// <summary>Defines capacity for for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public SlotCapacity CapacityFor(Guid appointmentTypeId)
    {
        AppointmentTypeIds.EnsureKnown(appointmentTypeId);

        var capacity = _capacities.SingleOrDefault(c => c.AppointmentTypeId == appointmentTypeId);
        Guard.Against(capacity is null, $"This slot has no capacity counter for {appointmentTypeId}.");

        return capacity!;
    }

    /// <summary>Defines has spare capacity for all for the current use case.</summary>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public bool HasSpareCapacityForAll(IEnumerable<Guid> appointmentTypeIds) =>
        Status == ConfirmedSlotStatus.Active
        && appointmentTypeIds.All(id => CapacityFor(id).HasSpare);

    /// <summary>Defines cancel for the current use case.</summary>
    public void Cancel()
    {
        Guard.Against(Status == ConfirmedSlotStatus.Cancelled, "This slot has already been cancelled.");
        Status = ConfirmedSlotStatus.Cancelled;
    }
}
