namespace EventBooking.Application.Candidates;

/// <summary>One fixed Appointment Type projected for group and Candidate read models.</summary>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
public sealed record AppointmentTypeSummary(string Code, string Name);

/// <summary>One active Employee Group available for assignment.</summary>
/// <param name="EmployeeGroupId">The employee group id.</param>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
/// <param name="RequiredAppointmentTypes">The required appointment types.</param>
public sealed record EmployeeGroupListItem(
    Guid EmployeeGroupId,
    string Code,
    string Name,
    IReadOnlyList<AppointmentTypeSummary> RequiredAppointmentTypes);

/// <summary>Requests active Employee Groups for one authorized Coordinator.</summary>
/// <param name="StaffUserId">The staff user id.</param>
public sealed record ListEmployeeGroupsQuery(Guid StaffUserId);
