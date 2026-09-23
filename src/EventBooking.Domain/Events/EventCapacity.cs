using EventBooking.Domain.Common;

namespace EventBooking.Domain.Events;

/// <summary>
/// Remaining bookable headcount for one appointment type on one eventItem. This row is the
/// single source of truth for booking eligibility, and the row the confirm transaction locks.
/// </summary>
public sealed class EventCapacity
{
    private EventCapacity()
    {
    }

    /// <summary>Defines event id for the current use case.</summary>
    public Guid EventId { get; private set; }

    /// <summary>Defines appointment type id for the current use case.</summary>
    public Guid AppointmentTypeId { get; private set; }

    /// <summary>Defines total headcount for the current use case.</summary>
    public int TotalHeadcount { get; private set; }

    /// <summary>Defines remaining capacity for the current use case.</summary>
    public int RemainingCapacity { get; private set; }

    /// <summary>Defines has spare for the current use case.</summary>
    public bool HasSpare => RemainingCapacity > 0;

    /// <summary>Defines occupied capacity for the current use case.</summary>
    public int OccupiedCapacity => TotalHeadcount - RemainingCapacity;

    internal static EventCapacity Initialise(Guid eventId, Guid appointmentTypeId, int totalHeadcount)
    {
        AppointmentTypeIdsGuard(appointmentTypeId);

        var total = Guard.Positive(totalHeadcount, "totalHeadcount");

        return new EventCapacity
        {
            EventId = eventId,
            AppointmentTypeId = appointmentTypeId,
            TotalHeadcount = total,
            RemainingCapacity = total,
        };
    }

    /// <summary>Defines decrement for the current use case.</summary>
    public void Decrement()
    {
        Guard.Against(
            RemainingCapacity <= 0,
            "No remaining capacity for this appointment type on this eventItem.");

        RemainingCapacity -= 1;
    }

    /// <summary>Defines increment for the current use case.</summary>
    public void Increment()
    {
        Guard.Against(
            RemainingCapacity >= TotalHeadcount,
            "Remaining capacity cannot exceed the headcount the manager accepted.");

        RemainingCapacity += 1;
    }

    /// <summary>Defines adjust total headcount for the current use case.</summary>
    /// <param name="totalHeadcount">The total headcount.</param>
    public bool AdjustTotalHeadcount(int totalHeadcount)
    {
        var next = Guard.Positive(totalHeadcount, "totalHeadcount");
        if (next == TotalHeadcount)
        {
            return false;
        }

        Guard.Against(
            next < OccupiedCapacity,
            "totalHeadcount cannot be lower than occupied capacity.");

        var delta = next - TotalHeadcount;
        TotalHeadcount = next;
        RemainingCapacity += delta;
        return true;
    }

    private static void AppointmentTypeIdsGuard(Guid appointmentTypeId) =>
        AppointmentTypes.AppointmentTypeIds.EnsureKnown(appointmentTypeId);
}
