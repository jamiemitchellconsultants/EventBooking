using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Candidates;

/// <summary>Lists the active Employee Groups a Coordinator may assign to Candidates.</summary>
/// <param name="groups">Reads change-controlled Employee Group reference data.</param>
/// <param name="access">Authorizes candidate management.</param>
public sealed class ListEmployeeGroupsHandler(
    IEmployeeGroupRepository groups,
    IStaffAccessAuthorizer access)
{
    /// <summary>Returns active mapped groups ordered by display name.</summary>
    /// <param name="query">The staff list request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The assignable groups with their required appointment types.</returns>
    public async Task<Result<IReadOnlyList<EmployeeGroupListItem>>> HandleAsync(
        ListEmployeeGroupsQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ManageCandidates,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<IReadOnlyList<EmployeeGroupListItem>>.Failure(authorized.Error);
        }

        var active = await groups.ListActiveAsync(cancellationToken);

        var items = active
            .Select(group => new EmployeeGroupListItem(
                group.Id,
                group.Code,
                group.Name,
                group.RequiredAppointmentTypeIds
                    .Select(typeId => new AppointmentTypeSummary(
                        AppointmentTypeIds.CodeOf(typeId), AppointmentTypeIds.NameOf(typeId)))
                    .OrderBy(summary => summary.Code, StringComparer.Ordinal)
                    .ToList()))
            .ToList();

        return Result<IReadOnlyList<EmployeeGroupListItem>>.Success(items);
    }
}
