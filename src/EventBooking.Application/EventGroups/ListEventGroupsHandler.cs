using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.EventGroups;

namespace EventBooking.Application.EventGroups;

/// <summary>Staff reads of event groups; every read also requires the management capability.</summary>
/// <param name="repository">The event groups.</param>
/// <param name="attendeeGroups">The attendee groups.</param>
/// <param name="access">The access.</param>
public sealed class ListEventGroupsHandler(
    IEventGroupRepository repository,
    IAttendeeGroupRepository attendeeGroups,
    IStaffAccessAuthorizer access)
{
    /// <summary>Lists every group ordered by title, without attendee identity or counts.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<EventGroupResult>>> ListAsync(
        Guid staffUserId, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(staffUserId,
            StaffCapability.ManageEventGroups, null, ct);
        if (authorized.IsFailure) return Result<IReadOnlyList<EventGroupResult>>.Failure(authorized.Error);

        var groups = await repository.ListAsync(ct);
        var results = new List<EventGroupResult>(groups.Count);
        foreach (var group in groups)
            results.Add(await ToResultAsync(group, ct));
        return Result<IReadOnlyList<EventGroupResult>>.Success(results);
    }

    /// <summary>Gets one group without attendee identity or counts.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="id">The event group id.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<EventGroupResult>> GetAsync(
        Guid staffUserId, Guid id, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(staffUserId,
            StaffCapability.ManageEventGroups, null, ct);
        if (authorized.IsFailure) return Result<EventGroupResult>.Failure(authorized.Error);

        var group = await repository.GetAsync(id, ct);
        if (group is null) return Result<EventGroupResult>.Failure(Error.NotFound("No such event group."));
        return Result<EventGroupResult>.Success(await ToResultAsync(group, ct));
    }

    // Reads resolve selected-group requirements from live reference data rather than
    // trusting a stored snapshot: the reference-data guard keeps the two in agreement.
    private async Task<EventGroupResult> ToResultAsync(EventGroup group, CancellationToken ct)
    {
        var selected = group.AttendeeGroups.Select(x => x.AttendeeGroupId).Order().ToArray();
        var requirements = new List<Guid>();
        foreach (var id in selected)
        {
            var attendeeGroup = await attendeeGroups.GetAsync(id, ct);
            if (attendeeGroup is not null)
                requirements.AddRange(attendeeGroup.RequiredAppointmentTypeIds);
        }

        return new EventGroupResult(
            group.Id, group.Title, group.Description, group.IsOpen, group.Version,
            selected, [.. requirements.Distinct().Order()],
            [.. group.Events.Select(x => new EventGroupEventResult(x.EventId, x.IsOpen))]);
    }
}
