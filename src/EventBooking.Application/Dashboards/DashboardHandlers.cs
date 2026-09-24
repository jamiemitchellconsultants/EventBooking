using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.ReadModels;
using EventBooking.Domain.Access;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Dashboards;

/// <summary>Requests the three dashboard tabs, optionally narrowing the Events tab to one location.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="LocationId">The location id, or null for every location.</param>
public sealed record GetDashboardsQuery(Guid StaffUserId, Guid? LocationId);

/// <summary>FR-13.1's awaiting-availability tab with its row count.</summary>
/// <param name="Count">The row count.</param>
/// <param name="Rows">The rows.</param>
public sealed record AwaitingAvailabilityTab(
    int Count, IReadOnlyList<AwaitingAvailabilityRow> Rows);

/// <summary>FR-13.1's no-response tab with its row count.</summary>
/// <param name="Count">The row count.</param>
/// <param name="Rows">The rows.</param>
public sealed record NoResponseTab(int Count, IReadOnlyList<NoResponseRow> Rows);

/// <summary>FR-13.1's events tab with its row count.</summary>
/// <param name="Count">The row count.</param>
/// <param name="Rows">The rows.</param>
public sealed record EventsTab(int Count, IReadOnlyList<EventOverviewRow> Rows);

/// <summary>FR-13.1's three tabs, each with its row count, plus FR-13.2's email counts. The count travels beside the rows so a tab badge does not depend on materialising them.</summary>
/// <param name="AwaitingAvailability">The awaiting-availability tab.</param>
/// <param name="NoResponse">The no-response tab.</param>
/// <param name="Events">The events tab.</param>
/// <param name="FailedEmails">How many attendees' latest delivery failed.</param>
/// <param name="PendingEmails">How many attendees' latest delivery is pending.</param>
public sealed record DashboardsView(
    AwaitingAvailabilityTab AwaitingAvailability,
    NoResponseTab NoResponse,
    EventsTab Events,
    int FailedEmails,
    int PendingEmails);

/// <summary>Returns the coordinator dashboards after a Coordinator-profile check.</summary>
/// <param name="queries">The queries.</param>
/// <param name="profiles">The access profiles.</param>
/// <param name="clock">The clock.</param>
/// <param name="zones">The zone abstraction.</param>
public sealed class GetDashboardsHandler(
    IDashboardQueries queries,
    IStaffAccessProfileRepository profiles,
    IClock clock,
    IEventWindowZones zones)
{
    // Coordinator capability: dashboards are attendee-facing reads an Admin does not hold.
    // The query re-checks the Admin shape itself.
    /// <summary>Handles the query.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<DashboardsView>> HandleAsync(
        GetDashboardsQuery query, CancellationToken ct)
    {
        var profile = await profiles.GetAsync(query.StaffUserId, ct);
        if (profile is null || !profile.IsCoordinator)
            return Result<DashboardsView>.Failure(
                Error.Forbidden("Dashboards need a Coordinator profile."));
        var shape = new CallerShape(
            profile.StaffUserId, profile.IsAdmin, RolesOf(profile));
        return Result<DashboardsView>.Success(await queries.GetDashboardsAsync(
            shape, query.LocationId, clock.UtcNow, zones, ct));
    }

    private static HashSet<string> RolesOf(StaffAccessProfile profile)
    {
        var roles = new HashSet<string>();
        if (profile.IsManager) roles.Add("Manager");
        if (profile.IsCoordinator) roles.Add("Coordinator");
        if (profile.IsAdmin) roles.Add("Admin");
        if (profile.IsAppointmentStaff) roles.Add("AppointmentStaff");
        return roles;
    }
}
