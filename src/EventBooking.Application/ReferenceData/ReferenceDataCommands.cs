namespace EventBooking.Application.ReferenceData;

/// <summary>Creates a location.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
/// <param name="Address">The address.</param>
/// <param name="TimeZoneId">The time zone id.</param>
public sealed record CreateLocationCommand(Guid StaffUserId, string? Code, string? Name, string? Address, string? TimeZoneId);

/// <summary>Updates a location, including its activation.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="LocationId">The location id.</param>
/// <param name="Name">The name.</param>
/// <param name="Address">The address.</param>
/// <param name="TimeZoneId">The time zone id.</param>
/// <param name="IsActive">Whether the location is active.</param>
/// <param name="ExpectedVersion">The expected version.</param>
public sealed record UpdateLocationCommand(Guid StaffUserId, Guid LocationId, string? Name, string? Address, string? TimeZoneId, bool IsActive, long ExpectedVersion);

/// <summary>Lists locations. Open to any staff member; the endpoint's staff policy is the gate.</summary>
/// <param name="IncludeInactive">Whether to include inactive rows.</param>
public sealed record ListLocationsQuery(bool IncludeInactive);

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
/// <param name="Address">The address.</param>
/// <param name="TimeZoneId">The time zone id.</param>
/// <param name="Version">The version.</param>
public sealed record LocationListItem(Guid Id, string Code, string Name, bool IsActive, string Address, string TimeZoneId, long Version);

/// <summary>Creates an appointment type.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
public sealed record CreateAppointmentTypeCommand(Guid StaffUserId, string? Code, string? Name);

/// <summary>Renames an appointment type, including its activation.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="AppointmentTypeId">The appointment type id.</param>
/// <param name="Name">The name.</param>
/// <param name="IsActive">Whether the type is active.</param>
/// <param name="ExpectedVersion">The expected version.</param>
public sealed record UpdateAppointmentTypeCommand(Guid StaffUserId, Guid AppointmentTypeId, string? Name, bool IsActive, long ExpectedVersion);

/// <summary>Lists appointment types. Open to any staff member; the endpoint's staff policy is the gate.</summary>
/// <param name="IncludeInactive">Whether to include inactive rows.</param>
public sealed record ListAppointmentTypesQuery(bool IncludeInactive);

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
/// <param name="Version">The version.</param>
/// <param name="HasManager">Whether a current Manager profile scopes to this type.</param>
public sealed record AppointmentTypeListItem(Guid Id, string Code, string Name, bool IsActive, string? ManagerDisplayName, long Version, bool HasManager);

/// <summary>Creates an attendee group.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
/// <param name="AppointmentTypeIds">The appointment type ids.</param>
/// <param name="Description">The public description.</param>
public sealed record CreateAttendeeGroupCommand(Guid StaffUserId, string? Code, string? Name, IReadOnlyList<Guid> AppointmentTypeIds, string? Description = null);

/// <summary>Renames an attendee group, replaces its requirement set, or changes activation.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="AttendeeGroupId">The attendee group id.</param>
/// <param name="Name">The name.</param>
/// <param name="AppointmentTypeIds">The appointment type ids.</param>
/// <param name="IsActive">Whether the group is active.</param>
/// <param name="ExpectedVersion">The expected version.</param>
/// <param name="Description">The public description.</param>
public sealed record UpdateAttendeeGroupCommand(Guid StaffUserId, Guid AttendeeGroupId, string? Name, IReadOnlyList<Guid>? AppointmentTypeIds, bool IsActive, long ExpectedVersion, string? Description = null);

/// <summary>Lists attendee groups. Open to any staff member; the endpoint's staff policy is the gate.</summary>
/// <param name="IncludeInactive">Whether to include inactive rows.</param>
public sealed record ListAttendeeGroupsQuery(bool IncludeInactive);

/// <summary>An attendee group with its requirements, version and member count.</summary>
/// <param name="Id">The id.</param>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
/// <param name="IsActive">Whether the group is active.</param>
/// <param name="Version">The version.</param>
/// <param name="RequirementTypeIds">The requirement type ids.</param>
/// <param name="MemberCount">The member count.</param>
/// <param name="Description">The public description.</param>
public sealed record AttendeeGroupResult(Guid Id, string Code, string Name, bool IsActive, long Version, IReadOnlyList<Guid> RequirementTypeIds, int MemberCount, string Description);

/// <summary>One attendee-group row for staff listings.</summary>
/// <param name="Id">The id.</param>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
/// <param name="IsActive">Whether the group is active.</param>
/// <param name="RequirementTypeIds">The requirement type ids.</param>
/// <param name="MemberCount">The member count.</param>
/// <param name="Version">The version.</param>
/// <param name="Description">The public description.</param>
public sealed record AttendeeGroupListItem(Guid Id, string Code, string Name, bool IsActive, IReadOnlyList<Guid> RequirementTypeIds, int MemberCount, long Version, string Description);
