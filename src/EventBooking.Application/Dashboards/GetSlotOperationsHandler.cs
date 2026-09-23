using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Dashboards;

/// <summary>Slot-only view of every cancellable confirmed window, free of candidate data.</summary>
/// <param name="Slots">One row per confirmed slot with per-appointment-type capacity and aggregate booking count.</param>
public sealed record SlotOperationsView(IReadOnlyList<SlotOverviewRow> Slots);

/// <summary>Query carrying the caller's staff identity for the slot-only operations view.</summary>
/// <param name="StaffUserId">The authenticated staff identity making the request.</param>
public sealed record GetSlotOperationsQuery(Guid StaffUserId);

/// <summary>Returns the slot-only operations view after a view-slot-operations check.</summary>
/// <param name="queries">The queries.</param>
/// <param name="access">The access.</param>
public sealed class GetSlotOperationsHandler(
    IDashboardQueries queries,
    IStaffAccessAuthorizer access)
{
    /// <summary>Authorizes the caller then returns every cancellable slot-overview row.</summary>
    /// <param name="query">The query carrying the caller's staff identity.</param>
    /// <param name="cancellationToken">Propagated to the authorizer and queries.</param>
    /// <returns>The slot-only view, or the authorizer's forbidden failure.</returns>
    public async Task<Result<SlotOperationsView>> HandleAsync(
        GetSlotOperationsQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ViewSlotOperations,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<SlotOperationsView>.Failure(authorized.Error);
        }

        var slots = await queries.SlotsOverviewAsync(cancellationToken);
        return Result<SlotOperationsView>.Success(new SlotOperationsView(slots));
    }
}
