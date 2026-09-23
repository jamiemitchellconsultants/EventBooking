using EventBooking.Application.Abstractions;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Invites;

/// <summary>
/// The one place the "which slots may a candidate be offered" rule lives. Task 36 uses it to build
/// an invite; Task 40 uses it to find a single replacement when an option fills up.
/// </summary>
/// <param name="slots">The slots.</param>
/// <param name="clock">The clock.</param>
public sealed class EligibleSlotFinder(IConfirmedSlotRepository slots, IClock clock)
{
    /// <summary>Defines find async for the current use case.</summary>
    /// <param name="requiredAppointmentTypeIds">The required appointment type ids.</param>
    /// <param name="take">The take.</param>
    /// <param name="excludeSlotIds">The exclude slot ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<IReadOnlyList<ConfirmedSlot>> FindAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        int take,
        IReadOnlyCollection<Guid> excludeSlotIds,
        CancellationToken cancellationToken)
    {
        var today = clock.TodayAtHeadOffice;

        var candidates = await slots.ListActiveAsync(today, cancellationToken);

        return candidates
            .Where(s => !excludeSlotIds.Contains(s.Id))
            .Where(s => s.Window.StartsAfter(today))
            .Where(s => s.HasSpareCapacityForAll(requiredAppointmentTypeIds))
            .OrderBy(s => s.Window)
            .Take(take)
            .ToList();
    }
}
