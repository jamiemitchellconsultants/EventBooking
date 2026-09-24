using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Invites;

/// <summary>Lists the active locations a Coordinator can restrict an invite to.</summary>
/// <param name="StaffUserId">The signed-in Coordinator.</param>
public sealed record ListInviteLocationsQuery(Guid StaffUserId);

/// <summary>One location a Coordinator may open for an invite.</summary>
/// <param name="LocationId">The location identifier.</param>
/// <param name="Code">The location code.</param>
/// <param name="Name">The location display name.</param>
public sealed record InviteLocationItem(Guid LocationId, string Code, string Name);

/// <summary>Lists active locations for the invite location chooser.</summary>
/// <param name="locations">The location repository.</param>
/// <param name="access">The staff access authorizer.</param>
public sealed class ListInviteLocationsHandler(
    ILocationRepository locations,
    IStaffAccessAuthorizer access)
{
    /// <summary>Handles the query.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<InviteLocationItem>>> HandleAsync(
        ListInviteLocationsQuery query, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId, StaffCapability.ManageAttendees, null, ct);
        if (authorized.IsFailure)
            return Result<IReadOnlyList<InviteLocationItem>>.Failure(authorized.Error);

        var active = (await locations.ListAsync(ct))
            .Where(l => l.IsActive)
            .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .Select(l => new InviteLocationItem(l.Id, l.Code, l.Name))
            .ToList();
        return Result<IReadOnlyList<InviteLocationItem>>.Success(active);
    }
}
