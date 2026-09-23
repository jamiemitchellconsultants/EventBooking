namespace EventBooking.Application.ReferenceData;

/// <summary>Creates a location.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
/// <param name="Address">The address.</param>
/// <param name="TimeZoneId">The time zone id.</param>
public sealed record CreateLocationCommand(Guid StaffUserId, string? Code, string? Name, string? Address, string? TimeZoneId);

/// <summary>Updates a location.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="LocationId">The location id.</param>
/// <param name="Name">The name.</param>
/// <param name="Address">The address.</param>
/// <param name="TimeZoneId">The time zone id.</param>
/// <param name="ExpectedVersion">The expected version.</param>
public sealed record UpdateLocationCommand(Guid StaffUserId, Guid LocationId, string? Name, string? Address, string? TimeZoneId, long ExpectedVersion);

/// <summary>Activates or deactivates a location.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="LocationId">The location id.</param>
/// <param name="IsActive">Whether the location is active.</param>
/// <param name="ExpectedVersion">The expected version.</param>
public sealed record SetLocationActiveCommand(Guid StaffUserId, Guid LocationId, bool IsActive, long ExpectedVersion);

/// <summary>A location with its version.</summary>
/// <param name="Id">The id.</param>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
/// <param name="Address">The address.</param>
/// <param name="TimeZoneId">The time zone id.</param>
/// <param name="IsActive">Whether the location is active.</param>
/// <param name="Version">The version.</param>
public sealed record LocationResult(Guid Id, string Code, string Name, string Address, string TimeZoneId, bool IsActive, long Version);

/// <summary>One location row for staff listings.</summary>
/// <param name="Id">The id.</param>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
/// <param name="IsActive">Whether the location is active.</param>
public sealed record LocationListItem(Guid Id, string Code, string Name, bool IsActive);

/// <summary>Creates an appointment type.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
public sealed record CreateAppointmentTypeCommand(Guid StaffUserId, string? Code, string? Name);

/// <summary>Renames an appointment type.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="AppointmentTypeId">The appointment type id.</param>
/// <param name="Name">The name.</param>
/// <param name="ExpectedVersion">The expected version.</param>
public sealed record UpdateAppointmentTypeCommand(Guid StaffUserId, Guid AppointmentTypeId, string? Name, long ExpectedVersion);

/// <summary>Activates or deactivates an appointment type.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="AppointmentTypeId">The appointment type id.</param>
/// <param name="IsActive">Whether the type is active.</param>
/// <param name="ExpectedVersion">The expected version.</param>
public sealed record SetAppointmentTypeActiveCommand(Guid StaffUserId, Guid AppointmentTypeId, bool IsActive, long ExpectedVersion);

/// <summary>An appointment type with its version and manager.</summary>
/// <param name="Id">The id.</param>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
/// <param name="IsActive">Whether the type is active.</param>
/// <param name="Version">The version.</param>
/// <param name="ManagerDisplayName">The manager display name.</param>
public sealed record AppointmentTypeResult(Guid Id, string Code, string Name, bool IsActive, long Version, string? ManagerDisplayName);

/// <summary>One appointment-type row for staff listings.</summary>
/// <param name="Id">The id.</param>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
/// <param name="IsActive">Whether the type is active.</param>
/// <param name="ManagerDisplayName">The manager display name.</param>
public sealed record AppointmentTypeListItem(Guid Id, string Code, string Name, bool IsActive, string? ManagerDisplayName);

/// <summary>Creates an attendee group.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
/// <param name="AppointmentTypeIds">The appointment type ids.</param>
public sealed record CreateAttendeeGroupCommand(Guid StaffUserId, string? Code, string? Name, IReadOnlyList<Guid> AppointmentTypeIds);

/// <summary>Renames an attendee group or replaces its requirement set.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="AttendeeGroupId">The attendee group id.</param>
/// <param name="Name">The name.</param>
/// <param name="AppointmentTypeIds">The appointment type ids.</param>
/// <param name="ExpectedVersion">The expected version.</param>
public sealed record UpdateAttendeeGroupCommand(Guid StaffUserId, Guid AttendeeGroupId, string? Name, IReadOnlyList<Guid>? AppointmentTypeIds, long ExpectedVersion);

/// <summary>Activates or deactivates an attendee group.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="AttendeeGroupId">The attendee group id.</param>
/// <param name="IsActive">Whether the group is active.</param>
/// <param name="ExpectedVersion">The expected version.</param>
public sealed record SetAttendeeGroupActiveCommand(Guid StaffUserId, Guid AttendeeGroupId, bool IsActive, long ExpectedVersion);

/// <summary>An attendee group with its requirements, version and member count.</summary>
/// <param name="Id">The id.</param>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
/// <param name="IsActive">Whether the group is active.</param>
/// <param name="Version">The version.</param>
/// <param name="RequirementTypeIds">The requirement type ids.</param>
/// <param name="MemberCount">The member count.</param>
public sealed record AttendeeGroupResult(Guid Id, string Code, string Name, bool IsActive, long Version, IReadOnlyList<Guid> RequirementTypeIds, int MemberCount);

/// <summary>One attendee-group row for staff listings.</summary>
/// <param name="Id">The id.</param>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
/// <param name="IsActive">Whether the group is active.</param>
/// <param name="RequirementTypeIds">The requirement type ids.</param>
/// <param name="MemberCount">The member count.</param>
public sealed record AttendeeGroupListItem(Guid Id, string Code, string Name, bool IsActive, IReadOnlyList<Guid> RequirementTypeIds, int MemberCount);
