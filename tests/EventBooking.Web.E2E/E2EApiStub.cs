namespace EventBooking.Web.E2E;

public static class E2EApiStub
{
    public const string StateCookie = "eventbooking-e2e-state";

    public static string FixtureState(HttpContext context) =>
        context.Request.Cookies.TryGetValue(StateCookie, out var value) ? value : "ready";

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/me", (HttpContext context) => Results.Json(new
        {
            displayName = "E2E staff member", staffId = "E2E1",
            roles = new[] { "Coordinator", "Manager", "AppointmentStaff" },
            scopeAppointmentTypeId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            scopeAppointmentTypeCode = "MED",
            scopeAppointmentTypeName = "Medical check",
            capabilities = new[] { "ManageAttendees", "ViewAttendeeDashboards", "ViewAttendeeAudit", "ViewEventAudit", "ManageEventNegotiation", "CancelEvent", "ConductAppointments" },
            problem = (string?)null,
            _links = FixtureState(context) == "reference-read-only"
                ? new Dictionary<string, object>()
                : CollectionLinks(),
        }));
    }

    private static Dictionary<string, object> CollectionLinks() => new()
    {
        ["createLocation"] = new { href = "/api/locations", method = "POST", operationId = "createLocation" },
        ["createAppointmentType"] = new { href = "/api/appointment-types", method = "POST", operationId = "createAppointmentType" },
        ["createAttendeeGroup"] = new { href = "/api/attendee-groups", method = "POST", operationId = "createAttendeeGroup" },
        ["proposeEvent"] = new { href = "/api/event-proposals", method = "POST", operationId = "proposeEvent" },
        ["createAttendee"] = new { href = "/api/attendees", method = "POST", operationId = "createAttendee" },
        ["importAttendees"] = new { href = "/api/attendees/import", method = "POST", operationId = "importAttendees" },
    };
}
