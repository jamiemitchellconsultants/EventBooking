namespace EventBooking.Application.EventGroups;

/// <summary>One event membership with its own publication gate.</summary>
/// <param name="EventId">The member event id.</param>
/// <param name="IsOpen">Whether anonymous registration is open for this membership.</param>
public sealed record EventGroupEventResult(Guid EventId, bool IsOpen);

/// <summary>An event group with its selected groups, derived types and memberships.</summary>
/// <param name="Id">The event group id.</param>
/// <param name="Title">The public title.</param>
/// <param name="Description">The public description.</param>
/// <param name="IsOpen">Whether anonymous registration is open for this group.</param>
/// <param name="Version">The version.</param>
/// <param name="AttendeeGroupIds">The selected attendee groups.</param>
/// <param name="AppointmentTypeIds">The union of the selected groups' requirements.</param>
/// <param name="Events">The member events.</param>
public sealed record EventGroupResult(
    Guid Id, string Title, string Description, bool IsOpen, long Version,
    IReadOnlyList<Guid> AttendeeGroupIds, IReadOnlyList<Guid> AppointmentTypeIds,
    IReadOnlyList<EventGroupEventResult> Events);
