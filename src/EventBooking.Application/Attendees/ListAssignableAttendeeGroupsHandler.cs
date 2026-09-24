using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Attendees;

/// <summary>Lists the active Attendee Groups a Coordinator may assign to Attendees.</summary>
/// <param name="groups">Reads change-controlled Attendee Group reference data.</param>
/// <param name="access">Authorizes attendee management.</param>
public sealed class ListAssignableAttendeeGroupsHandler(
    IAttendeeGroupRepository groups,
    IStaffAccessAuthorizer access)
{
    /// <summary>Returns active mapped groups ordered by display name.</summary>
    /// <param name="query">The staff list request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The assignable groups with their required appointment types.</returns>
    public async Task<Result<IReadOnlyList<AssignableAttendeeGroupItem>>> HandleAsync(
        ListAssignableAttendeeGroupsQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<IReadOnlyList<AssignableAttendeeGroupItem>>.Failure(authorized.Error);
        }

        var active = await groups.ListActiveAsync(cancellationToken);

        var items = active
            .Select(group => new AssignableAttendeeGroupItem(
                group.Id,
                group.Code,
                group.Name,
                group.RequiredAppointmentTypeIds
                    .Select(typeId => new AppointmentTypeSummary(
                        AppointmentTypeIds.CodeOf(typeId), AppointmentTypeIds.NameOf(typeId)))
                    .OrderBy(summary => summary.Code, StringComparer.Ordinal)
                    .ToList()))
            .ToList();

        return Result<IReadOnlyList<AssignableAttendeeGroupItem>>.Success(items);
    }
}
