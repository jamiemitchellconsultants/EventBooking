using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Time;

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
    public Guid ProposalId { get; private set; }

    /// <summary>The location hosting the event, carried from its proposal.</summary>
    public Guid LocationId { get; private set; }

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
            LocationId = proposal.LocationId,
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
    /// Sorts capacity keys into the order every command must take their row locks in: ascending
    /// event id, then ascending appointment type id (FR-3.3). Pure, so a caller can sort keys it
    /// has not loaded yet, and duplicates collapse.
    /// </summary>
    /// <param name="keys">The keys to sort, in any order and with any duplicates.</param>
    public static IReadOnlyList<EventCapacityKey> CapacityLockOrder(IEnumerable<EventCapacityKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        return
        [
            .. keys
                .Distinct()
                .OrderBy(key => key.EventId)
                .ThenBy(key => key.AppointmentTypeId),
        ];
    }

    /// <summary>Defines capacity for for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public EventCapacity CapacityFor(Guid appointmentTypeId)
    {
        var capacity = _capacities.SingleOrDefault(c => c.AppointmentTypeId == appointmentTypeId);
        Guard.Against(capacity is null, $"This event has no capacity counter for {appointmentTypeId}.");

        return capacity!;
    }

    /// <summary>
    /// Charges one place against each named type, and only those: an event may list types the
    /// attendee does not require, and those rows are untouched (FR-3.4). All or nothing — every
    /// row is checked before any row moves, so a refusal leaves the event exactly as it was.
    /// </summary>
    /// <param name="appointmentTypeIds">The types the attendee requires; every one must be listed.</param>
    public void ChargeRequiredTypes(IEnumerable<Guid> appointmentTypeIds)
    {
        var rows = RowsFor(appointmentTypeIds);

        Guard.Against(
            Status != EventStatus.Active,
            "A cancelled event cannot be charged.");

        if (rows.Find(row => !row.HasSpare) is { } exhausted)
        {
            throw new DomainException(EventCapacity.ExhaustedMessage(exhausted.AppointmentTypeId));
        }

        foreach (var row in rows)
        {
            row.Decrement();
        }
    }

    /// <summary>
    /// Returns one place to each named type. A cancelled event still releases: cancelling voids
    /// its bookings and gives their capacity back in the same transaction.
    /// </summary>
    /// <param name="appointmentTypeIds">The types to release; every one must be listed.</param>
    public void ReleaseTypes(IEnumerable<Guid> appointmentTypeIds)
    {
        var rows = RowsFor(appointmentTypeIds);

        if (rows.Find(row => row.RemainingCapacity >= row.TotalHeadcount) is { } full)
        {
            throw new DomainException(
                $"Appointment type {full.AppointmentTypeId} has nothing charged to release on this event.");
        }

        foreach (var row in rows)
        {
            row.Increment();
        }
    }

    /// <summary>Defines has spare capacity for all for the current use case.</summary>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public bool HasSpareCapacityForAll(IEnumerable<Guid> appointmentTypeIds) =>
        Status == EventStatus.Active
        && appointmentTypeIds.All(id => CapacityFor(id).HasSpare);

    /// <summary>
    /// Cancels the event. Refused once the window has started: the appointments have begun, so
    /// voiding the bookings and re-inviting would misdescribe what happened.
    /// </summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="now">The current instant.</param>
    public void Cancel(IEventWindowZones zones, string timeZoneId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(zones);

        Guard.Against(Status == EventStatus.Cancelled, "This event has already been cancelled.");
        Guard.Against(
            Window.HasStarted(zones, timeZoneId, now),
            "An event whose window has started cannot be cancelled.");

        Status = EventStatus.Cancelled;
    }

    private List<EventCapacity> RowsFor(IEnumerable<Guid> appointmentTypeIds)
    {
        ArgumentNullException.ThrowIfNull(appointmentTypeIds);

        var ids = appointmentTypeIds.Distinct().Order().ToList();
        Guard.Against(ids.Count == 0, "At least one appointment type must be named.");

        // CapacityFor refuses a type this event does not list, which is the check that keeps an
        // attendee whose requirements have drifted from silently booking a partial event.
        return [.. ids.Select(CapacityFor)];
    }
}
