using EventBooking.Application.Access;

namespace EventBooking.Mcp.Tools;

/// <summary>Describes the caller's access using scalar values suitable for MCP clients.</summary>
public sealed record MyAccessToolView
{
    /// <summary>Gets the caller's validated enterprise staff number.</summary>
    public required string StaffId { get; init; }

    /// <summary>Gets the caller's complete role names.</summary>
    public required IReadOnlyList<string> Roles { get; init; }

    /// <summary>Gets the shared appointment type scope, when one applies.</summary>
    public Guid? AppointmentTypeId { get; init; }

    /// <summary>Gets the display name for the shared appointment type scope.</summary>
    public string? AppointmentTypeName { get; init; }
}

/// <summary>Describes a staff access profile using scalar values suitable for MCP clients.</summary>
public sealed record StaffAccessToolProfile
{
    /// <summary>Gets the provider key retained for historic-profile fallback.</summary>
    public Guid StaffUserId { get; init; }

    /// <summary>Gets the observed enterprise staff number, or null for an historic profile.</summary>
    public string? StaffId { get; init; }

    /// <summary>Gets the profile's complete role names.</summary>
    public required IReadOnlyList<string> Roles { get; init; }

    /// <summary>Gets the shared appointment type scope, when one applies.</summary>
    public Guid? AppointmentTypeId { get; init; }

    /// <summary>Gets the display name for the shared appointment type scope.</summary>
    public string? AppointmentTypeName { get; init; }

    /// <summary>Gets the optimistic concurrency version.</summary>
    public long Version { get; init; }
}

/// <summary>Describes the result of replacing a staff access profile through MCP.</summary>
public sealed record StaffAccessToolMutation
{
    /// <summary>Gets the created or replaced profile.</summary>
    public required StaffAccessToolProfile Profile { get; init; }

    /// <summary>Gets the displaced manager's provider key, when a manager changed.</summary>
    public Guid? FormerManagerStaffUserId { get; init; }
}

/// <summary>Maps application access views to stable MCP response contracts.</summary>
internal static class AdminToolViewMapper
{
    /// <summary>Maps the caller view to a scalar MCP contract.</summary>
    /// <param name="view">The application caller view.</param>
    /// <returns>The MCP caller view.</returns>
    internal static MyAccessToolView ToToolView(this MeView view) => new()
    {
        StaffId = view.StaffId?.Value
            ?? throw new InvalidOperationException("The MCP staff policy requires a staff number."),
        Roles = view.Roles.Select(role => role.ToString()).ToList(),
        AppointmentTypeId = view.AppointmentTypeId,
        AppointmentTypeName = view.AppointmentTypeName,
    };

    /// <summary>Maps a profile to a scalar MCP contract.</summary>
    /// <param name="view">The application profile view.</param>
    /// <param name="staffId">An optional resolved staff number override.</param>
    /// <returns>The MCP profile view.</returns>
    internal static StaffAccessToolProfile ToToolView(
        this StaffAccessProfileView view,
        string? staffId = null) => new()
    {
        StaffUserId = view.StaffUserId,
        StaffId = staffId ?? view.StaffId?.Value,
        Roles = view.Roles.Select(role => role.ToString()).ToList(),
        AppointmentTypeId = view.AppointmentTypeId,
        AppointmentTypeName = view.AppointmentTypeName,
        Version = view.Version,
    };
}
