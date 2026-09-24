using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Attendees;

/// <summary>Lists the active Attendee Groups a Coordinator may assign to Attendees.</summary>
/// <param name="groups">Reads change-controlled Attendee Group reference data.</param>
/// <param name="access">Authorizes attendee management.</param>
/// <param name="types">Resolves requirement codes and names.</param>
public sealed class ListAssignableAttendeeGroupsHandler(
    IAttendeeGroupRepository groups,
    IStaffAccessAuthorizer access,
    IAppointmentTypeRepository types)
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

        // Resolved from the stored rows, not the canonical constants: groups can require
        // Admin-created types, and the picker must name those too.
        var summaries = (await types.ListAsync(cancellationToken))
            .ToDictionary(t => t.Id, t => new AppointmentTypeSummary(t.Code, t.Name));
        var items = active
            .Select(group => new AssignableAttendeeGroupItem(
                group.Id,
                group.Code,
                group.Name,
                group.RequiredAppointmentTypeIds
                    .Select(typeId => summaries.GetValueOrDefault(
                        typeId,
                        new AppointmentTypeSummary(typeId.ToString(), typeId.ToString())))
                    .OrderBy(summary => summary.Code, StringComparer.Ordinal)
                    .ToList()))
            .ToList();

        return Result<IReadOnlyList<AssignableAttendeeGroupItem>>.Success(items);
    }
}
