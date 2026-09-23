using EventBooking.Domain.Common;

namespace EventBooking.Domain.Events;

/// <summary>
/// Remaining bookable headcount for one appointment type on one event. This row is the single
/// source of truth for booking eligibility, and the row the confirm transaction locks.
/// </summary>
public sealed class EventCapacity
{
    /// <summary>The largest total headcount any one type may carry (design 08 — boundary values).</summary>
    public const int MaximumTotalHeadcount = 1000;

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

    /// <summary>This row's key, for sorting a command's rows into lock order.</summary>
    public EventCapacityKey Key => new(EventId, AppointmentTypeId);

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

    /// <summary>Charges one place against this row, refusing once the row is exhausted.</summary>
    public void Decrement()
    {
        Guard.Against(RemainingCapacity <= 0, ExhaustedMessage(AppointmentTypeId));

        RemainingCapacity -= 1;
    }

    /// <summary>Returns one charged place to this row, refusing to exceed the accepted total.</summary>
    public void Increment()
    {
        Guard.Against(
            RemainingCapacity >= TotalHeadcount,
            "Remaining capacity cannot exceed the headcount the manager accepted.");

        RemainingCapacity += 1;
    }

    /// <summary>
    /// Replaces the total this type's Manager accepted, applying the same delta to the remaining
    /// count (FR-3.5). A total below the type's active-booking count is refused rather than
    /// thrown, because FR-3.6 wants the Manager told the minimum and the current server values.
    /// </summary>
    /// <param name="totalHeadcount">The new total: positive, and at most <see cref="MaximumTotalHeadcount"/>.</param>
    /// <param name="activeBookingCount">Active bookings requiring this type on this event.</param>
    public CapacityAdjustment AdjustTotalHeadcount(int totalHeadcount, int activeBookingCount)
    {
        var next = Guard.Positive(totalHeadcount, "totalHeadcount");
        Guard.Against(
            next > MaximumTotalHeadcount,
            $"totalHeadcount must not exceed {MaximumTotalHeadcount}.");
        var active = Guard.NotNegative(activeBookingCount, "activeBookingCount");

        if (next == TotalHeadcount)
        {
            return new CapacityAdjustment(
                CapacityAdjustmentStatus.Unchanged, active, TotalHeadcount, RemainingCapacity);
        }

        // The caller's count and this row's own occupancy should agree. Taking the larger of the
        // two keeps 0 <= remainingCapacity <= totalHeadcount true even if they have drifted, and
        // reports a minimum the next attempt will actually be allowed to use.
        var minimum = Math.Max(active, OccupiedCapacity);
        if (next < minimum)
        {
            return new CapacityAdjustment(
                CapacityAdjustmentStatus.BelowActiveBookings, minimum, TotalHeadcount, RemainingCapacity);
        }

        var delta = next - TotalHeadcount;
        TotalHeadcount = next;
        RemainingCapacity += delta;

        return new CapacityAdjustment(
            CapacityAdjustmentStatus.Adjusted, minimum, TotalHeadcount, RemainingCapacity);
    }

    internal static string ExhaustedMessage(Guid appointmentTypeId) =>
        $"capacity-exhausted: appointment type {appointmentTypeId} has no remaining capacity "
        + "on this event.";

    private static void AppointmentTypeIdsGuard(Guid appointmentTypeId) =>
        AppointmentTypes.AppointmentTypeIds.EnsureKnown(appointmentTypeId);
}
