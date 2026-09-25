using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.EventGroups;
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;

namespace EventBooking.Application.EventGroups;

/// <summary>Staff commands publishing compatible event groups for self-registration.</summary>
/// <param name="repository">The event groups.</param>
/// <param name="attendeeGroups">The attendee groups.</param>
/// <param name="events">The events.</param>
/// <param name="locations">The locations.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unitOfWork.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
/// <param name="zones">The zones.</param>
public sealed class ManageEventGroupHandler(
    IEventGroupRepository repository,
    IAttendeeGroupRepository attendeeGroups,
    IEventRepository events,
    ILocationRepository locations,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock,
    IEventWindowZones zones)
{
    /// <summary>Creates a closed group serving the selected attendee groups.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<EventGroupResult>> CreateAsync(
        CreateEventGroupCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(command.StaffUserId,
            StaffCapability.ManageEventGroups, null, ct);
        if (authorized.IsFailure) return Result<EventGroupResult>.Failure(authorized.Error);

        var requirements = await ResolveRequirementsAsync(command.AttendeeGroupIds, ct);
        if (requirements.IsFailure) return Result<EventGroupResult>.Failure(requirements.Error);

        EventGroup group;
        try
        {
            group = EventGroup.Create(
                Guid.NewGuid(), command.Title, command.Description, requirements.Value);
        }
        catch (DomainException ex)
        {
            return Result<EventGroupResult>.Failure(Error.Validation(ex.Message));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        repository.Add(group);
        audit.Record(AuditEntityTypes.EventGroup, group.Id, AuditAction.EventGroupCreated,
            ActorType.Staff, command.StaffUserId.ToString(),
            $"groups {string.Join(",", requirements.Value.Keys.Order())}");
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result<EventGroupResult>.Success(ToResult(group, requirements.Value));
    }

    /// <summary>Edits copy, selected groups or the group gate.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<EventGroupResult>> UpdateAsync(
        UpdateEventGroupCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(command.StaffUserId,
            StaffCapability.ManageEventGroups, null, ct);
        if (authorized.IsFailure) return Result<EventGroupResult>.Failure(authorized.Error);

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var group = await repository.LockForUpdateAsync(command.EventGroupId, ct);
        if (group is null) return Result<EventGroupResult>.Failure(Error.NotFound("No such event group."));
        if (group.Version != command.ExpectedVersion)
            return Result<EventGroupResult>.Failure(Error.VersionConflict(
                "The event group changed under you.", group.Version));

        var requirements = await ResolveRequirementsAsync(command.AttendeeGroupIds, ct);
        if (requirements.IsFailure) return Result<EventGroupResult>.Failure(requirements.Error);

        var memberEventTypes = await MemberEventTypesAsync(group, ct);
        var wasOpen = group.IsOpen;
        try
        {
            group.Edit(command.Title, command.Description, requirements.Value, memberEventTypes);
        }
        catch (DomainException ex)
        {
            return Result<EventGroupResult>.Failure(Error.Validation(ex.Message));
        }

        group.SetOpen(command.IsOpen);
        audit.Record(AuditEntityTypes.EventGroup, group.Id, AuditAction.EventGroupUpdated,
            ActorType.Staff, command.StaffUserId.ToString(),
            $"groups {string.Join(",", requirements.Value.Keys.Order())}; " +
            $"isOpen {wasOpen} -> {group.IsOpen}");
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result<EventGroupResult>.Success(ToResult(group, requirements.Value));
    }

    /// <summary>Adds an event membership, toggles its gate, or removes it.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<EventGroupResult>> ChangeEventAsync(
        ChangeEventGroupEventCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(command.StaffUserId,
            StaffCapability.ManageEventGroups, null, ct);
        if (authorized.IsFailure) return Result<EventGroupResult>.Failure(authorized.Error);

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var group = await repository.LockForUpdateAsync(command.EventGroupId, ct);
        if (group is null) return Result<EventGroupResult>.Failure(Error.NotFound("No such event group."));
        if (group.Version != command.ExpectedVersion)
            return Result<EventGroupResult>.Failure(Error.VersionConflict(
                "The event group changed under you.", group.Version));

        var selected = group.AttendeeGroups.Select(x => x.AttendeeGroupId).ToArray();
        var requirements = await ResolveRequirementsAsync(selected, ct);
        if (requirements.IsFailure) return Result<EventGroupResult>.Failure(requirements.Error);

        var membership = group.Events.SingleOrDefault(x => x.EventId == command.EventId);
        var wasOpen = membership?.IsOpen;
        try
        {
            if (command.Remove)
            {
                group.RemoveEvent(command.EventId);
            }
            else if (command.IsOpen.HasValue)
            {
                if (membership is null)
                    return Result<EventGroupResult>.Failure(Error.NotFound("No such event membership."));
                group.SetEventOpen(command.EventId, command.IsOpen.Value);
            }
            else
            {
                var eventTypes = await ResolveJoinableEventTypesAsync(command.EventId, ct);
                if (eventTypes.IsFailure) return Result<EventGroupResult>.Failure(eventTypes.Error);
                group.AddEvent(command.EventId, eventTypes.Value, requirements.Value, isFuture: true);
            }
        }
        catch (DomainException ex)
        {
            return Result<EventGroupResult>.Failure(Error.Validation(ex.Message));
        }

        audit.Record(AuditEntityTypes.EventGroup, group.Id, AuditAction.EventGroupEventChanged,
            ActorType.Staff, command.StaffUserId.ToString(),
            command.Remove
                ? $"event {command.EventId} removed"
                : $"event {command.EventId}; isOpen {wasOpen} -> {group.Events.Single(x => x.EventId == command.EventId).IsOpen}");
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result<EventGroupResult>.Success(ToResult(group, requirements.Value));
    }

    private async Task<Result<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>>> ResolveRequirementsAsync(
        IReadOnlyList<Guid> selected, CancellationToken ct)
    {
        if (selected.Count == 0 || selected.Distinct().Count() != selected.Count)
            return Result<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>>.Failure(
                Error.Validation("An event group needs distinct selected attendee groups."));

        var requirements = new Dictionary<Guid, IReadOnlyCollection<Guid>>();
        foreach (var id in selected)
        {
            var attendeeGroup = await attendeeGroups.GetAsync(id, ct);
            if (attendeeGroup is null || !attendeeGroup.IsActive)
                return Result<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>>.Failure(
                    Error.Validation("An event group can only serve active attendee groups."));
            requirements[id] = attendeeGroup.RequiredAppointmentTypeIds;
        }

        return Result<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>>.Success(requirements);
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> MemberEventTypesAsync(
        EventGroup group, CancellationToken ct)
    {
        var ids = group.Events.Select(x => x.EventId).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, IReadOnlyCollection<Guid>>();
        var memberEvents = await events.ListByIdsAsync(ids, ct);
        return memberEvents.ToDictionary(
            x => x.Id,
            x => (IReadOnlyCollection<Guid>)x.Capacities.Select(c => c.AppointmentTypeId).ToArray());
    }

    private async Task<Result<IReadOnlyCollection<Guid>>> ResolveJoinableEventTypesAsync(
        Guid eventId, CancellationToken ct)
    {
        var eventItem = await events.GetAsync(eventId, ct);
        if (eventItem is null)
            return Result<IReadOnlyCollection<Guid>>.Failure(Error.NotFound("No such event."));
        if (eventItem.Status != EventStatus.Active)
            return Result<IReadOnlyCollection<Guid>>.Failure(
                Error.Validation("Only active events can join an event group."));

        var location = await locations.GetAsync(eventItem.LocationId, ct);
        if (location is null || eventItem.Window.HasStarted(zones, location.TimeZoneId, clock.UtcNow))
            return Result<IReadOnlyCollection<Guid>>.Failure(
                Error.Validation("Only future events can join an event group."));

        return Result<IReadOnlyCollection<Guid>>.Success(
            eventItem.Capacities.Select(c => c.AppointmentTypeId).ToArray());
    }

    private static EventGroupResult ToResult(
        EventGroup group, IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> requirements) =>
        new(group.Id, group.Title, group.Description, group.IsOpen, group.Version,
            [.. requirements.Keys.Order()],
            [.. requirements.Values.SelectMany(ids => ids).Distinct().Order()],
            [.. group.Events.Select(x => new EventGroupEventResult(x.EventId, x.IsOpen))]);
}
