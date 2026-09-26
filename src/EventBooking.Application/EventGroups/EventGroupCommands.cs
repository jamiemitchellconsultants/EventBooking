namespace EventBooking.Application.EventGroups;

/// <summary>Publishes a closed event group serving the selected attendee groups.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Title">The public title.</param>
/// <param name="Description">The public description.</param>
/// <param name="AttendeeGroupIds">The selected attendee groups.</param>
public sealed record CreateEventGroupCommand(
    Guid StaffUserId, string? Title, string? Description, IReadOnlyList<Guid> AttendeeGroupIds);

/// <summary>Edits copy, selected groups or the group gate.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="EventGroupId">The event group id.</param>
/// <param name="Title">The public title.</param>
/// <param name="Description">The public description.</param>
/// <param name="AttendeeGroupIds">The selected attendee groups.</param>
/// <param name="IsOpen">Whether anonymous registration is open for this group.</param>
/// <param name="ExpectedVersion">The version the caller read.</param>
public sealed record UpdateEventGroupCommand(
    Guid StaffUserId, Guid EventGroupId, string? Title, string? Description,
    IReadOnlyList<Guid> AttendeeGroupIds, bool IsOpen, long ExpectedVersion);

/// <summary>Adds an event membership, toggles its gate, or removes it.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="EventGroupId">The event group id.</param>
/// <param name="EventId">The member event id.</param>
/// <param name="IsOpen">The membership gate, or null to leave it alone.</param>
/// <param name="Remove">Whether to remove the membership.</param>
/// <param name="ExpectedVersion">The version the caller read.</param>
public sealed record ChangeEventGroupEventCommand(
    Guid StaffUserId, Guid EventGroupId, Guid EventId, bool? IsOpen, bool Remove,
    long ExpectedVersion);
