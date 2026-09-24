using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Negotiation;

/// <summary>Design 05: the caller's type's proposals (FR-2.13), filtered by status and Location.</summary>
/// <param name="StaffUserId">The calling staff identity.</param>
/// <param name="Status">An EventProposalStatus name, or null for every status.</param>
/// <param name="LocationId">The location filter, or null for every site.</param>
/// <param name="Cursor">The keyset cursor, or null for the first page.</param>
/// <param name="Limit">The page size.</param>
public sealed record ListEventProposalsQuery(
    Guid StaffUserId, string? Status, Guid? LocationId, string? Cursor, int Limit);

/// <summary>One listed proposal with the caller's relationship to it.</summary>
/// <param name="ProposalId">The proposal.</param>
/// <param name="LocationId">The location that would host the event.</param>
/// <param name="LocationCode">The location code.</param>
/// <param name="LocationName">The location name.</param>
/// <param name="TimeZoneId">The location's time zone.</param>
/// <param name="Date">The window's local date.</param>
/// <param name="StartTime">The window's local start time.</param>
/// <param name="DurationMinutes">The window length.</param>
/// <param name="Status">The proposal status name.</param>
/// <param name="ListedTypeCount">How many types the proposal lists.</param>
/// <param name="AcceptedTypeCount">How many have accepted.</param>
/// <param name="MyAcceptedHeadcount">The caller's accepted headcount, or null.</param>
/// <param name="AcceptedByMe">Whether the caller's type accepted.</param>
/// <param name="CreatedByMe">Whether the caller raised it for their type.</param>
/// <param name="Cursor">The keyset cursor for this row.</param>
public sealed record EventProposalListItem(
    Guid ProposalId, Guid LocationId, string LocationCode, string LocationName, string TimeZoneId,
    DateOnly Date, TimeOnly StartTime, int DurationMinutes, string Status,
    int ListedTypeCount, int AcceptedTypeCount, int? MyAcceptedHeadcount,
    bool AcceptedByMe, bool CreatedByMe, string Cursor);

/// <summary>One page of listed proposals.</summary>
/// <param name="Items">The rows.</param>
/// <param name="NextCursor">The next cursor, or null on the last page.</param>
public sealed record EventProposalListView(
    IReadOnlyList<EventProposalListItem> Items, string? NextCursor);

/// <summary>Lists the caller's type's proposals (FR-2.13), filtered and keyset-paged.</summary>
/// <param name="access">The authorizer.</param>
/// <param name="queries">The read side.</param>
public sealed class ListEventProposalsHandler(
    IStaffAccessAuthorizer access, IEventProposalListQueries queries)
{
    /// <summary>Returns one page of proposals.</summary>
    /// <param name="query">The filters and the page.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The page.</returns>
    public async Task<Result<EventProposalListView>> HandleAsync(
        ListEventProposalsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Limit is < 1 or > 200)
        {
            return Result<EventProposalListView>.Failure(
                Error.Validation("Limit must be between 1 and 200."));
        }

        // A misspelt status is a field error, not an empty page: a caller who asks for "open"
        // and is shown nothing will conclude there are no proposals.
        EventProposalStatus? status = null;
        if (query.Status is not null)
        {
            if (!Enum.TryParse<EventProposalStatus>(query.Status, ignoreCase: false, out var parsed)
                || !Enum.IsDefined(parsed))
            {
                return Result<EventProposalListView>.Failure(
                    Error.Validation($"'{query.Status}' is not a proposal status."));
            }

            status = parsed;
        }

        var authorized = await access.AuthorizeAsync(
            query.StaffUserId, StaffCapability.ManageEventNegotiation, null, ct);
        if (authorized.IsFailure)
        {
            return Result<EventProposalListView>.Failure(authorized.Error);
        }

        var actingType = authorized.Value.AppointmentTypeId!.Value;
        var rows = await queries.ListAsync(
            actingType, query.StaffUserId, status, query.LocationId, query.Cursor,
            query.Limit + 1, ct);

        var kept = rows.Count > query.Limit ? rows.Take(query.Limit).ToList() : rows.ToList();
        return Result<EventProposalListView>.Success(new EventProposalListView(
            kept, rows.Count > query.Limit ? kept[^1].Cursor : null));
    }
}

/// <summary>
/// The proposal read side. The acting type is a parameter rather than a filter the caller can
/// set: FR-2.13 is the caller's own type's board, and a Manager must not be able to page
/// another type's negotiation by asking.
/// </summary>
public interface IEventProposalListQueries
{
    /// <summary>Returns one keyset page of the acting type's proposals.</summary>
    /// <param name="actingAppointmentTypeId">The caller's own type.</param>
    /// <param name="staffUserId">The caller, for the accepted-by-me and created-by-me flags.</param>
    /// <param name="status">The status filter, or null for every status.</param>
    /// <param name="locationId">The location filter, or null for every site.</param>
    /// <param name="cursor">The keyset cursor, or null for the first page.</param>
    /// <param name="limit">The page size, over-read by one.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The rows.</returns>
    Task<IReadOnlyList<EventProposalListItem>> ListAsync(
        Guid actingAppointmentTypeId, Guid staffUserId, EventProposalStatus? status,
        Guid? locationId, string? cursor, int limit, CancellationToken ct);
}
