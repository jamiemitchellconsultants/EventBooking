namespace EventBooking.Application.SelfRegistrations;

/// <summary>One attendee-group choice with its public copy.</summary>
/// <param name="AttendeeGroupId">The attendee group id.</param>
/// <param name="Name">The display name.</param>
/// <param name="Description">The public description.</param>
public sealed record PublicAttendeeGroupChoice(Guid AttendeeGroupId, string Name, string Description);

/// <summary>One open event choice with its public copy.</summary>
/// <param name="EventId">The event id.</param>
/// <param name="LocationName">The location name.</param>
/// <param name="Address">The location address.</param>
/// <param name="Date">The local date.</param>
/// <param name="StartTime">The local start time.</param>
/// <param name="DurationMinutes">The duration in minutes.</param>
/// <param name="TimeZoneId">The location's IANA zone.</param>
/// <param name="AppointmentTypeCodes">The capacity type codes.</param>
public sealed record PublicEventGroupEventChoice(
    Guid EventId, string LocationName, string Address, DateOnly Date, TimeOnly StartTime,
    int DurationMinutes, string TimeZoneId, IReadOnlyList<string> AppointmentTypeCodes);

/// <summary>One open event group with its public choices.</summary>
/// <param name="Id">The event group id.</param>
/// <param name="Title">The public title.</param>
/// <param name="Description">The public description.</param>
/// <param name="Version">The version.</param>
/// <param name="AttendeeGroups">The selectable attendee groups.</param>
/// <param name="Events">The open event choices.</param>
public sealed record PublicEventGroupResult(
    Guid Id, string Title, string Description, long Version,
    IReadOnlyList<PublicAttendeeGroupChoice> AttendeeGroups,
    IReadOnlyList<PublicEventGroupEventChoice> Events);
