using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Events;

/// <summary>Defines event for the current use case.</summary>
public sealed class Event
{
    private readonly List<EventCapacity> _capacities = [];

    private Event()
    {
        // Required by the persistence layer's constructor binding.
        Window = null!;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines proposal id for the current use case.</summary>
    public Guid? ProposalId { get; private set; }

    /// <summary>Defines window for the current use case.</summary>
    public EventWindow Window { get; private set; }

    /// <summary>Defines status for the current use case.</summary>
    public EventStatus Status { get; private set; } = EventStatus.Active;

    /// <summary>Defines capacities for the current use case.</summary>
    public IReadOnlyList<EventCapacity> Capacities => _capacities;

    /// <summary>
    /// The only way a eventItem is created. Marks the proposal confirmed in the same call, so
    /// a proposal can never back a second eventItem.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="proposal">The proposal.</param>
    public static Event CreateFrom(Guid id, EventProposal proposal)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(proposal is null, "proposal must be supplied.");

        proposal!.MarkConfirmed();

        var eventItem = new Event
        {
            Id = id,
            ProposalId = proposal.Id,
            Window = proposal.Window,
            Status = EventStatus.Active,
        };

        foreach (var acceptance in proposal.Acceptances.OrderBy(a => a.AppointmentTypeId))
        {
            eventItem._capacities.Add(
                EventCapacity.Initialise(id, acceptance.AppointmentTypeId, acceptance.Headcount));
        }

        return eventItem;
    }

    /// <summary>
    /// Creates an active event with no backing proposal from a strictly complete set of
    /// positive headcounts for the three fixed appointment types.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="window">The window.</param>
    /// <param name="headcountsByAppointmentType">The headcounts by appointment type.</param>
    public static Event CreateImported(
        Guid id, EventWindow window, IReadOnlyDictionary<Guid, int> headcountsByAppointmentType)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(window is null, "window must be supplied.");
        Guard.Against(headcountsByAppointmentType is null, "headcountsByAppointmentType must be supplied.");
        Guard.Against(
            headcountsByAppointmentType!.Count != AppointmentTypeIds.All.Count
            || headcountsByAppointmentType.Keys.Any(id => !AppointmentTypeIds.All.Contains(id)),
            "Headcounts must be supplied for exactly the three fixed appointment types.");

        var eventItem = new Event
        {
            Id = id,
            ProposalId = null,
            Window = window!,
            Status = EventStatus.Active,
        };

        foreach (var appointmentTypeId in AppointmentTypeIds.All)
        {
            Guard.Against(
                !headcountsByAppointmentType.TryGetValue(appointmentTypeId, out var headcount),
                $"A headcount is required for {AppointmentTypeIds.NameOf(appointmentTypeId)}.");

            eventItem._capacities.Add(EventCapacity.Initialise(id, appointmentTypeId, headcount));
        }

        return eventItem;
    }

    /// <summary>Defines capacity for for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public EventCapacity CapacityFor(Guid appointmentTypeId)
    {
        AppointmentTypeIds.EnsureKnown(appointmentTypeId);

        var capacity = _capacities.SingleOrDefault(c => c.AppointmentTypeId == appointmentTypeId);
        Guard.Against(capacity is null, $"This event has no capacity counter for {appointmentTypeId}.");

        return capacity!;
    }

    /// <summary>Defines has spare capacity for all for the current use case.</summary>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public bool HasSpareCapacityForAll(IEnumerable<Guid> appointmentTypeIds) =>
        Status == EventStatus.Active
        && appointmentTypeIds.All(id => CapacityFor(id).HasSpare);

    /// <summary>Defines cancel for the current use case.</summary>
    public void Cancel()
    {
        Guard.Against(Status == EventStatus.Cancelled, "This event has already been cancelled.");
        Status = EventStatus.Cancelled;
    }
}
