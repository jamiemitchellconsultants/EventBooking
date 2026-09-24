namespace EventBooking.Application.Attendees;

/// <summary>One fixed Appointment Type projected for group and Attendee read models.</summary>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
public sealed record AppointmentTypeSummary(string Code, string Name);

/// <summary>One active Attendee Group available for assignment.</summary>
/// <param name="AttendeeGroupId">The attendee group id.</param>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
/// <param name="RequiredAppointmentTypes">The required appointment types.</param>
public sealed record AssignableAttendeeGroupItem(
    Guid AttendeeGroupId,
    string Code,
    string Name,
    IReadOnlyList<AppointmentTypeSummary> RequiredAppointmentTypes);

/// <summary>Requests active Attendee Groups for one authorized Coordinator.</summary>
/// <param name="StaffUserId">The staff user id.</param>
public sealed record ListAssignableAttendeeGroupsQuery(Guid StaffUserId);
