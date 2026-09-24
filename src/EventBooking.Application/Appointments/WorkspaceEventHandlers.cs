using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Events;
using EventBooking.Domain.Locations;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Appointments;

/// <summary>Lists the workspace events for one appointment-staff member.</summary>
/// <param name="StaffUserId">The staff identity asking.</param>
/// <param name="LocationId">The location to narrow to, or null for every location.</param>
public sealed record ListWorkspaceEventsQuery(Guid StaffUserId, Guid? LocationId);

/// <summary>One workspace event row: window plus the zone it is read in.</summary>
/// <param name="EventId">The event identifier.</param>
/// <param name="LocationId">The location identifier.</param>
/// <param name="LocationName">The location display name.</param>
/// <param name="Date">The window date.</param>
/// <param name="StartTime">The window start time.</param>
/// <param name="EndTime">The window end time.</param>
/// <param name="ZoneAbbreviation">The location zone abbreviation at the end instant.</param>
/// <param name="Status">The event's own status name.</param>
/// <param name="TimeZoneId">The location's IANA zone identifier.</param>
/// <param name="DurationMinutes">The window length.</param>
public sealed record WorkspaceEventView(
    Guid EventId, Guid LocationId, string LocationName, DateOnly Date,
    TimeOnly StartTime, TimeOnly EndTime, string ZoneAbbreviation, string Status,
    string TimeZoneId, int DurationMinutes);

/// <summary>
/// Lists the workspace events inside the recent-past to near-future band, ordered by end
/// instant: the first row is the nearest current or next event, with no special case.
/// </summary>
/// <param name="workspace">The workspace candidate-set query.</param>
/// <param name="events">The events.</param>
/// <param name="locations">The locations.</param>
/// <param name="access">The staff access authorizer.</param>
/// <param name="clock">The clock.</param>
/// <param name="zones">The zone abstraction windows are read in.</param>
public sealed class ListWorkspaceEventsHandler(
    IWorkspaceQueries workspace,
    IEventRepository events,
    ILocationRepository locations,
    IStaffAccessAuthorizer access,
    IClock clock,
    IEventWindowZones zones)
{
    /// <summary>Handles the query.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<WorkspaceEventView>>> HandleAsync(
        ListWorkspaceEventsQuery query, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId, StaffCapability.ConductAppointments, null, ct);
        if (authorized.IsFailure) return Result<IReadOnlyList<WorkspaceEventView>>.Failure(authorized.Error);
        if (authorized.Value.AppointmentTypeId is not { } myType)
            return Result<IReadOnlyList<WorkspaceEventView>>.Failure(
                Error.Forbidden("The workspace needs an assigned appointment type."));

        var zoneByLocation = (await locations.ListAsync(ct)).ToDictionary(l => l.Id);
        var now = clock.UtcNow;
        var from = now.AddDays(-7);
        var to = now.AddDays(14);

        // The candidate set comes from the query, not from every active event ever
        // written. Loading them all and filtering here is the shape Task 11 removed from
        // the eligibility path, where it cost some 680 ms against 18 ms; the workspace
        // would reintroduce it at exactly the same scale.
        var candidateIds = await workspace.ListWorkspaceEventIdsAsync(
            myType, query.LocationId, from.AddMinutes(-EventWindow.MaximumDurationMinutes),
            to, ct);
        if (candidateIds.Count == 0)
            return Result<IReadOnlyList<WorkspaceEventView>>.Success([]);

        // The exact end bound stays here: there is no stored end instant, because
        // PostgreSQL cannot evaluate IANA rules deterministically (design 04), so SQL
        // narrows by start instant and the resolver decides the edge.
        var rows = (await events.ListByIdsAsync(candidateIds, ct))
            .Where(e => zoneByLocation.TryGetValue(e.LocationId, out var location)
                && EventEnd(e, location.TimeZoneId, zones) >= from
                && EventEnd(e, location.TimeZoneId, zones) <= to)
            .OrderBy(e => EventEnd(e, zoneByLocation[e.LocationId].TimeZoneId, zones))
            .ThenBy(e => e.Id)
            .Select(e => new WorkspaceEventView(e.Id, e.LocationId,
                zoneByLocation[e.LocationId].Name,
                e.Window.Date, e.Window.StartTime, e.Window.EndTime,
                zones.AbbreviationOf(EventEnd(e, zoneByLocation[e.LocationId].TimeZoneId, zones),
                    zoneByLocation[e.LocationId].TimeZoneId),
                e.Status.ToString(),
                zoneByLocation[e.LocationId].TimeZoneId, e.Window.DurationMinutes))
            .ToList();

        return Result<IReadOnlyList<WorkspaceEventView>>.Success(rows);
    }

    private static DateTimeOffset EventEnd(Event eventItem, string timeZoneId, IEventWindowZones zones) =>
        zones.InstantOf(eventItem.Window.Date, eventItem.Window.EndTime, timeZoneId);
}
