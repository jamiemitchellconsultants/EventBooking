using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Dashboards;

/// <summary>Event-only view of every cancellable confirmed window, free of attendee data.</summary>
/// <param name="Events">One row per event with per-appointment-type capacity and aggregate booking count.</param>
public sealed record EventOperationsView(IReadOnlyList<EventOverviewRow> Events);

/// <summary>Query carrying the caller's staff identity for the event-only operations view.</summary>
/// <param name="StaffUserId">The authenticated staff identity making the request.</param>
public sealed record GetEventOperationsQuery(Guid StaffUserId);

/// <summary>Returns the event-only operations view after a view-event-operations check.</summary>
/// <param name="queries">The queries.</param>
/// <param name="access">The access.</param>
public sealed class GetEventOperationsHandler(
    IDashboardQueries queries,
    IStaffAccessAuthorizer access)
{
    /// <summary>Authorizes the caller then returns every cancellable event-overview row.</summary>
    /// <param name="query">The query carrying the caller's staff identity.</param>
    /// <param name="cancellationToken">Propagated to the authorizer and queries.</param>
    /// <returns>The event-only view, or the authorizer's forbidden failure.</returns>
    public async Task<Result<EventOperationsView>> HandleAsync(
        GetEventOperationsQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ViewEventOperations,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<EventOperationsView>.Failure(authorized.Error);
        }

        var events = await queries.EventsOverviewAsync(cancellationToken);
        return Result<EventOperationsView>.Success(new EventOperationsView(events));
    }
}
