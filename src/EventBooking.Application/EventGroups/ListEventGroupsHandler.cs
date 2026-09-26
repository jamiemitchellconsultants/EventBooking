using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.SelfRegistrations;
using EventBooking.Domain.Common;
using EventBooking.Domain.EventGroups;
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;

namespace EventBooking.Application.EventGroups;

/// <summary>Staff and public reads of event groups.</summary>
/// <param name="repository">The event groups.</param>
/// <param name="attendeeGroups">The attendee groups.</param>
/// <param name="events">The events.</param>
/// <param name="locations">The locations.</param>
/// <param name="types">The appointment types.</param>
/// <param name="access">The access.</param>
/// <param name="clock">The clock.</param>
/// <param name="zones">The zones.</param>
public sealed class ListEventGroupsHandler(
    IEventGroupRepository repository,
    IAttendeeGroupRepository attendeeGroups,
    IEventRepository events,
    ILocationRepository locations,
    IAppointmentTypeRepository types,
    IStaffAccessAuthorizer access,
    IClock clock,
    IEventWindowZones zones)
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

    /// <summary>Lists every open group with its public choices; anonymous.</summary>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<PublicEventGroupResult>>> ListPublicAsync(
        CancellationToken ct)
    {
        var groups = await repository.ListAsync(ct);
        var results = new List<PublicEventGroupResult>();
        foreach (var group in groups.Where(x => x.IsOpen))
        {
            var projected = await ToPublicResultAsync(group, ct);
            if (projected is not null) results.Add(projected);
        }

        return Result<IReadOnlyList<PublicEventGroupResult>>.Success(results);
    }

    /// <summary>Gets one open group with its public choices; anonymous.</summary>
    /// <param name="id">The event group id.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<PublicEventGroupResult>> GetPublicAsync(
        Guid id, CancellationToken ct)
    {
        var group = await repository.GetAsync(id, ct);
        if (group is null || !group.IsOpen)
            return Result<PublicEventGroupResult>.Failure(Error.NotFound("No such event group."));
        var projected = await ToPublicResultAsync(group, ct);
        if (projected is null)
            return Result<PublicEventGroupResult>.Failure(Error.NotFound("No such event group."));
        return Result<PublicEventGroupResult>.Success(projected);
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

    private static bool HasSpare(Event eventItem, IReadOnlyCollection<Guid> typeIds)
    {
        try
        {
            return eventItem.HasSpareCapacityForAll(typeIds);
        }
        catch (DomainException)
        {
            return false;
        }
    }

    // Public projections carry group and event choices but no Attendee state: no member
    // lists, no counts. Only open memberships on live future events are offered.
    private async Task<PublicEventGroupResult?> ToPublicResultAsync(
        EventGroup group, CancellationToken ct)
    {
        var choices = new List<PublicAttendeeGroupChoice>();
        var required = new Dictionary<Guid, IReadOnlyCollection<Guid>>();
        foreach (var id in group.AttendeeGroups.Select(x => x.AttendeeGroupId).Order())
        {
            var attendeeGroup = await attendeeGroups.GetAsync(id, ct);
            if (attendeeGroup is null || !attendeeGroup.IsActive) continue;
            required[attendeeGroup.Id] = attendeeGroup.RequiredAppointmentTypeIds;
            choices.Add(new PublicAttendeeGroupChoice(
                attendeeGroup.Id, attendeeGroup.Name, attendeeGroup.Description));
        }

        if (choices.Count == 0) return null;

        var codeById = (await types.ListAsync(ct)).ToDictionary(x => x.Id, x => x.Code);
        var eventChoices = new List<PublicEventGroupEventChoice>();
        foreach (var membership in group.Events.Where(x => x.IsOpen))
        {
            var eventItem = await events.GetAsync(membership.EventId, ct);
            if (eventItem is null || eventItem.Status != EventStatus.Active) continue;
            var location = await locations.GetAsync(eventItem.LocationId, ct);
            if (location is null
                || eventItem.Window.HasStarted(zones, location.TimeZoneId, clock.UtcNow))
                continue;
            eventChoices.Add(new PublicEventGroupEventChoice(
                eventItem.Id, location.Name, location.Address,
                eventItem.Window.Date, eventItem.Window.StartTime, eventItem.Window.DurationMinutes,
                location.TimeZoneId,
                [.. eventItem.Capacities
                    .Select(c => codeById.GetValueOrDefault(c.AppointmentTypeId, "?"))
                    .Order()],
                [.. choices.Select(c => c.AttendeeGroupId)
                    .Where(id => HasSpare(eventItem, required[id]))]));
        }

        return new PublicEventGroupResult(
            group.Id, group.Title, group.Description, group.Version, choices, eventChoices);
    }
}
