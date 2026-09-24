using System.Text.Json.Serialization;
using EventBooking.Application.Settings;

namespace EventBooking.Api.Contracts;

/// <summary>Application settings plus the self and update affordances.</summary>
public sealed record SettingsResourceResponse(
    /// <summary>Gets the number of days an invite stays usable.</summary>
    int InviteExpiryDays,
    /// <summary>Gets the maximum number of times an unanswered invite is automatically re-issued.</summary>
    int MaxAutoRetryCount,
    /// <summary>Gets the appointment types with their assigned managers.</summary>
    IReadOnlyList<AppointmentTypeView> AppointmentTypes,
    /// <summary>Gets the settings affordances.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one settings view into its hypermedia resource.</summary>
    /// <param name="view">The application settings view to project.</param>
    /// <returns>The API resource with settings links.</returns>
    public static SettingsResourceResponse From(SettingsView view) =>
        new(view.InviteExpiryDays, view.MaxAutoRetryCount, view.AppointmentTypes,
            StaffResourceLinks.ForSettings());
}

/// <summary>One staff access profile plus its replace and clear affordances.</summary>
public sealed record StaffAccessResourceResponse(
    /// <summary>Gets the stable staff identity targeted by administration.</summary>
    Guid StaffUserId,
    /// <summary>Gets the enterprise staff number, or null until the identity signs in.</summary>
    string? StaffId,
    /// <summary>Gets the identity-provider roles mirrored on the profile.</summary>
    IReadOnlyList<string> Roles,
    /// <summary>Gets the scoped appointment-type identifier, or null when unscoped.</summary>
    Guid? AppointmentTypeId,
    /// <summary>Gets the scoped appointment-type name, or null when unscoped.</summary>
    string? AppointmentTypeName,
    /// <summary>Gets the positive concurrency version required by scope commands.</summary>
    long Version,
    /// <summary>Gets the human-readable name mirrored from the identity provider, or null when absent.</summary>
    string? DisplayName,
    /// <summary>Gets the replace and clear affordances for the profile.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>A staff-access mutation result plus the follow-up affordances.</summary>
public sealed record StaffAccessMutationResourceResponse(
    /// <summary>Gets the updated staff access profile.</summary>
    StaffAccessResourceResponse Profile,
    /// <summary>Gets the displaced manager identity, when a replacement displaced one.</summary>
    Guid? FormerManagerStaffUserId,
    /// <summary>Gets the follow-up affordances for the mutated profile.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>The signed-in staff identity plus self and collection entry affordances.</summary>
public sealed record MeResourceResponse(
    /// <summary>Gets the caller's enterprise staff number, or null until recorded.</summary>
    string? StaffId,
    /// <summary>Gets the caller's current role names.</summary>
    IReadOnlyList<string> Roles,
    /// <summary>Gets the caller's scoped appointment-type identifier, or null when unscoped.</summary>
    Guid? AppointmentTypeId,
    /// <summary>Gets the caller's scoped appointment-type name, or null when unscoped.</summary>
    string? AppointmentTypeName,
    /// <summary>Gets the capability names the caller's profile grants.</summary>
    IReadOnlyList<string> Capabilities,
    /// <summary>Gets the no-role explanation, or null for a full view.</summary>
    string? Problem,
    /// <summary>Gets the self and role-relevant collection entry affordances. Links are discoverability hints, not authorization.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Builds the identity resource with self plus role-relevant entry links.</summary>
    /// <param name="staffId">The caller's enterprise staff number, or null until recorded.</param>
    /// <param name="roles">The caller's current role names.</param>
    /// <param name="appointmentTypeId">The caller's scoped appointment-type identifier, or null.</param>
    /// <param name="appointmentTypeName">The caller's scoped appointment-type name, or null.</param>
    /// <param name="capabilities">The capability names the caller's profile grants.</param>
    /// <param name="problem">The no-role explanation, or null for a full view.</param>
    /// <returns>The API resource with identity links.</returns>
    public static MeResourceResponse From(
        string? staffId,
        IReadOnlyList<string> roles,
        Guid? appointmentTypeId,
        string? appointmentTypeName,
        IReadOnlyList<string> capabilities,
        string? problem)
    {
        var links = new Dictionary<string, ApiLink>
        {
            ["self"] = new("/api/me", "GET", "getMyAccess"),
        };
        if (roles.Contains("Coordinator") || roles.Contains("Admin"))
        {
            links["attendees"] = new("/api/attendees", "GET", "listAttendees");
            links["dashboards"] = new("/api/dashboards", "GET", "getDashboards");
            links["audit"] = new("/api/audit/search", "GET", "searchAudit");
        }

        if (roles.Contains("Manager"))
        {
            links["eventBoard"] = new("/api/events/board", "GET", "getEventBoard");
            links["eventOperations"] = new("/api/events/operations", "GET", "getEventOperations");
        }

        if (roles.Contains("AppointmentStaff") || roles.Contains("Manager"))
        {
            links["appointmentEvents"] = new("/api/appointment-workspace/events", "GET", "listAppointmentEvents");
        }

        if (roles.Contains("Admin"))
        {
            links["settings"] = new("/api/admin/settings", "GET", "getSettings");
            links["staffAccess"] = new("/api/admin/staff-access", "GET", "listStaffAccess");
        }

        return new MeResourceResponse(
            staffId, roles, appointmentTypeId, appointmentTypeName, capabilities, problem, links);
    }
}
