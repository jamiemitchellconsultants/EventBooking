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
        MapTask25(app);
    }

    private static void MapTask25(WebApplication app)
    {
        app.MapGet("/api/locations", (HttpContext context) =>
        {
            var state = FixtureState(context);
            object[] rows = state == "locations-empty" ? [] : [new
            {
                id = Guid.Parse("10000000-0000-0000-0000-000000000001"), code = "LON",
                name = "London HQ", address = "1 Example St", timeZoneId = "Europe/London",
                isActive = true, version = 1,
                _links = state == "reference-read-only" ? new Dictionary<string, object>() :
                    new Dictionary<string, object> { ["update"] = new { href = "/api/locations/10000000-0000-0000-0000-000000000001", method = "PUT", operationId = "updateLocation" } },
            }];
            return Results.Json(new { items = rows, nextCursor = (string?)null });
        });
        app.MapGet("/api/appointment-types", (HttpContext context) => Results.Json(new { items = new[] { new { id = Guid.NewGuid(), code = "MED", name = "Medical check", isActive = true, version = 1, hasManager = true, managerDisplayName = "M. Manager",
            _links = FixtureState(context) == "reference-read-only" ? new Dictionary<string, object>() :
                new Dictionary<string, object> { ["update"] = new { href = "/api/appointment-types/1", method = "PUT", operationId = "updateAppointmentType" } } } }, nextCursor = (string?)null }));
        app.MapGet("/api/attendee-groups", (HttpContext context) => Results.Json(new { items = new[] { new { id = Guid.NewGuid(), code = "FIELD", name = "Field staff", isActive = true, version = 1, requirementTypeIds = Array.Empty<Guid>(), memberCount = 17,
            _links = FixtureState(context) == "reference-read-only" ? new Dictionary<string, object>() :
                new Dictionary<string, object> { ["update"] = new { href = "/api/attendee-groups/1", method = "PUT", operationId = "updateAttendeeGroup" } } } }, nextCursor = (string?)null }));
        app.MapGet("/api/settings", () => Results.Json(new { inviteExpiryDays = 7, maxAutoRetryCount = 2, inviteOptionCount = 3, version = 1, _links = new { update = new { href = "/api/settings", method = "PUT", operationId = "updateSettings" } } }));
        app.MapGet("/api/staff-access", () => Results.Json(new { items = new[] { new { staffUserId = Guid.NewGuid(), staffId = "A1", displayName = "A. Admin", roles = new[] { "Admin" }, appointmentTypeId = (Guid?)null, version = 1, _links = new Dictionary<string, object>() } }, nextCursor = (string?)null }));
        app.MapPut("/api/locations/{id:guid}", (HttpContext context, Guid id) =>
            FixtureState(context) == "locations-conflict"
                ? Results.Problem(statusCode: 409, type: "version-conflict",
                    extensions: new Dictionary<string, object?> { ["current"] = new { currentVersion = 2 } })
                : Results.Ok(new
                {
                    id,
                    code = "LON",
                    name = "London HQ",
                    address = "1 Example St",
                    timeZoneId = "Europe/London",
                    isActive = true,
                    version = 2,
                    _links = new Dictionary<string, object>
                    {
                        ["update"] = new
                        {
                            href = $"/api/locations/{id}",
                            method = "PUT",
                            operationId = "updateLocation",
                        },
                    },
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
