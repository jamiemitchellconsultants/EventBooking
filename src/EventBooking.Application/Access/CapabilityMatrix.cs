namespace EventBooking.Application.Access;

/// <summary>One capability-by-role cell of the design 06 authorization table.</summary>
/// <param name="Capability">The staff capability name.</param>
/// <param name="Role">The role name granting it.</param>
/// <param name="NeedsScope">Whether the grant needs a non-null profile scope.</param>
public sealed record CapabilityGrant(string Capability, string Role, bool NeedsScope);

/// <summary>The single authorization table. Rows mirror design 06; the generated matrix test
/// fails if code and design disagree.</summary>
public static class CapabilityMatrix
{
    /// <summary>Gets every capability-by-role grant in design order.</summary>
    public static IReadOnlyList<CapabilityGrant> Grants { get; } =
    [
        new("ManageReferenceData", "Admin", false),
        new("ManageSettings", "Admin", false),
        new("ManageStaffAccess", "Admin", false),
        new("ManageAttendees", "Coordinator", false),
        new("ViewAttendeeDashboards", "Coordinator", false),
        new("ViewAttendeeAudit", "Coordinator", false),
        new("ViewEventAudit", "Admin", false),
        new("ViewEventAudit", "Coordinator", false),
        new("ManageEventNegotiation", "Manager", true),
        new("ViewEventOperations", "Admin", false),
        new("ViewEventOperations", "Coordinator", false),
        new("ViewEventOperations", "Manager", false),
        new("ViewEventOperations", "AppointmentStaff", false),
        new("CancelEvent", "Admin", false),
        new("CancelEvent", "Coordinator", false),
        new("CancelEvent", "Manager", true),
        new("ConductAppointments", "Manager", true),
        new("ConductAppointments", "AppointmentStaff", true),
    ];

    /// <summary>Gets every staff capability name the table must cover.</summary>
    public static IReadOnlyList<string> AllCapabilities { get; } =
    [
        "ManageReferenceData", "ManageSettings", "ManageStaffAccess", "ManageAttendees",
        "ViewAttendeeDashboards", "ViewAttendeeAudit", "ViewEventAudit",
        "ManageEventNegotiation", "ViewEventOperations", "CancelEvent", "ConductAppointments",
    ];
}
