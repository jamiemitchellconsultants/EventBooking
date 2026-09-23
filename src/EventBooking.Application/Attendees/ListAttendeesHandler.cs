using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Attendees;

/// <summary>One Attendee with its assigned Attendee Group and derived requirements.</summary>
/// <param name="AttendeeId">The attendee id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="AttendeeGroupId">The attendee group id.</param>
/// <param name="AttendeeGroupCode">The attendee group code.</param>
/// <param name="AttendeeGroupName">The attendee group name.</param>
/// <param name="RequiresAttendeeGroupReconciliation">The requires attendee group reconciliation.</param>
/// <param name="RequiredAppointmentTypes">The required appointment types.</param>
/// <param name="Status">The status.</param>
/// <param name="StatusDisplay">The status display.</param>
public sealed record AttendeeListItem(
    Guid AttendeeId,
    string Name,
    string Email,
    Guid? AttendeeGroupId,
    string? AttendeeGroupCode,
    string? AttendeeGroupName,
    bool RequiresAttendeeGroupReconciliation,
    IReadOnlyList<AppointmentTypeSummary> RequiredAppointmentTypes,
    AttendeeStatus Status,
    string StatusDisplay);

/// <summary>Requests Attendees, optionally narrowed by status or search text.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Status">The status.</param>
/// <param name="Search">The search.</param>
public sealed record ListAttendeesQuery(Guid StaffUserId, AttendeeStatus? Status, string? Search);

/// <summary>Lists attendees with their assigned Attendee Group and derived requirements.</summary>
/// <param name="attendees">Reads attendee rows.</param>
/// <param name="groups">Resolves assigned Attendee Group identity.</param>
/// <param name="access">Authorizes attendee management.</param>
public sealed class ListAttendeesHandler(
    IAttendeeRepository attendees,
    IAttendeeGroupRepository groups,
    IStaffAccessAuthorizer access)
{
    /// <summary>The Area C screen wording for each status.</summary>
    /// <param name="status">The status.</param>
    public static string DisplayOf(AttendeeStatus status) => status switch
    {
        AttendeeStatus.NotYetInvited => "Not yet invited",
        AttendeeStatus.AwaitingAvailability => "Awaiting availability",
        AttendeeStatus.Invited => "Invited (pending response)",
        AttendeeStatus.Booked => "Booked",
        AttendeeStatus.NoResponseNeedsFollowUp => "No response - needs follow-up",
        _ => status.ToString(),
    };

    /// <summary>Returns matching Attendees ordered by name with group identity.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<AttendeeListItem>>> HandleAsync(
        ListAttendeesQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<IReadOnlyList<AttendeeListItem>>.Failure(authorized.Error);
        }

        var all = await attendees.ListAsync(query.Status, cancellationToken);

        var search = query.Search?.Trim();
        var filtered = string.IsNullOrEmpty(search)
            ? all
            : all
                .Where(c =>
                    c.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || c.Email.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

        // The five reference rows are loaded once; only exceptional unlisted groups fall back
        // to an individual lookup.
        var reference = (await groups.ListActiveAsync(cancellationToken))
            .ToDictionary(group => group.Id);
        foreach (var missing in filtered
            .Select(attendee => attendee.AttendeeGroupId)
            .Where(id => id.HasValue && !reference.ContainsKey(id.Value))
            .Select(id => id!.Value)
            .Distinct()
            .ToList())
        {
            var group = await groups.GetAsync(missing, cancellationToken);
            if (group is not null)
            {
                reference[missing] = group;
            }
        }

        var items = filtered
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new AttendeeListItem(
                c.Id,
                c.Name,
                c.Email,
                c.AttendeeGroupId,
                c.AttendeeGroupId.HasValue && reference.TryGetValue(c.AttendeeGroupId.Value, out var group)
                    ? group.Code
                    : null,
                c.AttendeeGroupId.HasValue && reference.TryGetValue(c.AttendeeGroupId.Value, out var named)
                    ? named.Name
                    : null,
                !c.AttendeeGroupId.HasValue,
                c.RequiredAppointmentTypeIds
                    .Select(typeId => new AppointmentTypeSummary(
                        AppointmentTypeIds.CodeOf(typeId), AppointmentTypeIds.NameOf(typeId)))
                    .OrderBy(summary => summary.Code, StringComparer.Ordinal)
                    .ToList(),
                c.Status,
                DisplayOf(c.Status)))
            .ToList();

        return Result<IReadOnlyList<AttendeeListItem>>.Success(items);
    }
}
