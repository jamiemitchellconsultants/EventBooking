using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Negotiation;

/// <summary>One open proposal, showing only the caller's headcount.</summary>
/// <param name="ProposalId">The proposal identifier.</param>
/// <param name="LocationId">The location that would host the event.</param>
/// <param name="Date">The window's local date at the location.</param>
/// <param name="StartTime">The window's local start time.</param>
/// <param name="EndTime">The window's local end time.</param>
/// <param name="ListedTypeCount">How many appointment types the proposal lists.</param>
/// <param name="AcceptedTypeCount">How many of them have accepted.</param>
/// <param name="MyAcceptedHeadcount">The caller's accepted headcount, or null when not accepted.</param>
/// <param name="AcceptedByMe">Whether the caller already accepted this proposal.</param>
/// <param name="CreatedByMe">Whether the caller created this proposal.</param>
public sealed record NegotiationBoardProposalView(
    Guid ProposalId, Guid LocationId, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime,
    int ListedTypeCount, int AcceptedTypeCount, int? MyAcceptedHeadcount, bool AcceptedByMe, bool CreatedByMe);

/// <summary>One event, showing only the caller's capacity row.</summary>
/// <param name="EventId">The event identifier.</param>
/// <param name="LocationId">The location hosting the event.</param>
/// <param name="Date">The window's local date at the location.</param>
/// <param name="StartTime">The window's local start time.</param>
/// <param name="EndTime">The window's local end time.</param>
/// <param name="MyHeadcount">The caller's total headcount on the event.</param>
/// <param name="MyRemainingCapacity">The caller's remaining capacity on the event.</param>
public sealed record NegotiationBoardEventView(
    Guid EventId, Guid LocationId, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime,
    int MyHeadcount, int MyRemainingCapacity);

/// <summary>The open proposals and events visible to one manager's appointment type.</summary>
/// <param name="OpenProposals">The open proposals listing the caller's type.</param>
/// <param name="Events">The caller's future active events.</param>
public sealed record NegotiationBoard(
    IReadOnlyList<NegotiationBoardProposalView> OpenProposals,
    IReadOnlyList<NegotiationBoardEventView> Events);

/// <summary>Reads the negotiation board for one manager.</summary>
/// <param name="StaffUserId">The reading manager.</param>
public sealed record GetNegotiationBoardQuery(Guid StaffUserId);

/// <summary>Reads the board scoped so a manager never sees another type's headcount.</summary>
/// <param name="proposals">The event proposal repository.</param>
/// <param name="events">The event repository.</param>
/// <param name="locations">The location repository.</param>
/// <param name="access">The staff access authorizer.</param>
/// <param name="clock">The clock.</param>
/// <param name="zones">The zone abstraction that judges recency at each location.</param>
public sealed class NegotiationBoardHandler(
    IEventProposalRepository proposals,
    IEventRepository events,
    ILocationRepository locations,
    IStaffAccessAuthorizer access,
    IClock clock,
    IEventWindowZones zones)
{
    /// <summary>Handles the query.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<NegotiationBoard>> HandleAsync(
        GetNegotiationBoardQuery query, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId, StaffCapability.ManageEventNegotiation, null, ct);
        if (authorized.IsFailure) return Result<NegotiationBoard>.Failure(authorized.Error);
        if (authorized.Value.AppointmentTypeId is not { } myType)
            return Result<NegotiationBoard>.Failure(
                Error.Forbidden("The board needs an assigned appointment type."));

        var zoneByLocation = (await locations.ListAsync(ct)).ToDictionary(l => l.Id, l => l.TimeZoneId);
        var now = clock.UtcNow;

        var open = (await proposals.ListOpenAsync(ct))
            .Where(p => p.ListedAppointmentTypeIds.Contains(myType))
            .OrderBy(p => p.Window)
            .Select(p =>
            {
                var mine = p.Acceptances.SingleOrDefault(a =>
                    a.AppointmentTypeId == myType && a.ManagerUserId == query.StaffUserId);
                return new NegotiationBoardProposalView(p.Id, p.LocationId,
                    p.Window.Date, p.Window.StartTime, p.Window.EndTime,
                    p.ListedAppointmentTypeIds.Count, p.Acceptances.Count,
                    mine?.Headcount, mine is not null, p.CreatedByManagerUserId == query.StaffUserId);
            })
            .ToList();

        var confirmed = (await events.ListActiveAsync(DateOnly.MinValue, ct))
            .Where(e => e.Status == EventStatus.Active
                && e.Capacities.Any(c => c.AppointmentTypeId == myType)
                && zoneByLocation.TryGetValue(e.LocationId, out var zone)
                && !e.Window.HasEnded(zones, zone, now))
            .OrderBy(e => e.Window)
            .Select(e => new NegotiationBoardEventView(e.Id, e.LocationId,
                e.Window.Date, e.Window.StartTime, e.Window.EndTime,
                e.CapacityFor(myType).TotalHeadcount, e.CapacityFor(myType).RemainingCapacity))
            .ToList();

        return Result<NegotiationBoard>.Success(new NegotiationBoard(open, confirmed));
    }
}
