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
        MapTask26(app);
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

    private static void MapTask26(WebApplication app)
    {
        var eventId = Guid.Parse("30000000-0000-0000-0000-000000000003");
        var appointmentId = Guid.Parse("40000000-0000-0000-0000-000000000004");
        var time = new { date = "2026-10-14", startTime = "09:30:00", durationMinutes = 90,
            startLocal = "2026-10-14T09:30:00+01:00", endLocal = "2026-10-14T11:00:00+01:00",
            startUtc = "2026-10-14T08:30:00Z", endUtc = "2026-10-14T10:00:00Z",
            timeZoneId = "Europe/London", zoneAbbreviation = "BST" };
        app.MapGet("/api/event-proposals", (HttpContext context) =>
            FixtureState(context) == "negotiation-forbidden"
            ? Results.Problem(statusCode: 403, type: "forbidden")
            : Results.Json(new { items = new[] { new {
            id = Guid.NewGuid(), locationId = Guid.NewGuid(), locationCode = "LON", locationName = "London HQ",
            time, status = "Open", listedTypeCount = 3, acceptedTypeCount = 2, myAcceptedHeadcount = (int?)null,
            acceptedByMe = false, createdByMe = true, types = new[] { new { code = "MED", name = "Medical check" } },
            _links = new Dictionary<string, object> { ["accept"] = new { href = "/acceptance", method = "PUT", operationId = "recordAcceptance" } } } }, nextCursor = (string?)null }));
        // The real validation body carries `errors` as an array of field objects, not the
        // dictionary Results.ValidationProblem emits, so the stub builds it explicitly.
        app.MapPost("/api/event-proposals", (HttpContext context) =>
            FixtureState(context) == "proposal-validation"
                ? Results.Problem(
                    detail: "The request could not be accepted.",
                    statusCode: 422,
                    title: "The request could not be accepted.",
                    type: "validation-failed",
                    extensions: new Dictionary<string, object?>
                    {
                        ["errors"] = new[] { new { field = "headcount", line = (int?)null, code = "invalid", message = "Headcount must be at least one." } },
                    })
                : Results.Created("/api/event-proposals/1", new { proposalId = Guid.NewGuid(), status = "Open", eventId = (Guid?)null }));
        app.MapGet("/api/events", () => Results.Json(new { items = new[] { new { id = eventId,
            proposalId = Guid.NewGuid(), locationId = Guid.NewGuid(), locationCode = "LON", locationName = "London HQ",
            time, status = "Active", capacities = new[] { new { appointmentTypeId = Guid.NewGuid(), code = "MED", name = "Medical check", totalHeadcount = 6, remainingCapacity = 3,
                _links = new Dictionary<string, object> { ["adjust"] = new { href = "/capacity", method = "PUT", operationId = "adjustEventCapacity" } } } },
            activeBookings = 3, _links = new Dictionary<string, object> { ["cancel"] = new { href = "/cancel", method = "POST", operationId = "cancelEvent" } } } }, nextCursor = (string?)null }));
        app.MapPut("/api/events/{id:guid}/capacities/{typeId:guid}", (HttpContext context) =>
            FixtureState(context) == "capacity-conflict"
                ? Results.Problem(statusCode: 409, type: "capacity-below-bookings",
                    extensions: new Dictionary<string, object?>
                    {
                        ["minimum"] = 3,
                        ["current"] = new { totalHeadcount = 6, remainingCapacity = 3 },
                    })
                : Results.Ok(new { eventId, totalHeadcount = 6, remainingCapacity = 3, changed = true }));
        app.MapGet("/api/appointment-workspace/events", (HttpContext context) => Results.Json(new {
            items = FixtureState(context) == "workspace-empty" ? Array.Empty<object>() : [new { eventId,
                locationId = Guid.NewGuid(), locationName = "London HQ", time, status = "Active",
                _links = new Dictionary<string, object>() }], nextCursor = (string?)null }));
        app.MapGet("/api/appointment-workspace/events/{id:guid}", (Guid id) => Results.Json(new { items = new[] { new {
            appointmentId, name = "R. Singh", email = "r@example.org", scopeTypeCode = "MED",
            appointmentStatus = "Expected", checkedInAt = (string?)null, version = 1,
            _links = new Dictionary<string, object> { ["checkIn"] = new { href = $"/api/appointment-workspace/appointments/{appointmentId}/status", method = "PUT", operationId = "setAppointmentStatus" } } } }, nextCursor = (string?)null }));
        // The real endpoint answers 200 with the updated appointment, never 204.
        app.MapPut("/api/appointment-workspace/appointments/{id:guid}/status", (HttpContext context, Guid id) =>
            FixtureState(context) == "workspace-conflict"
                ? Results.Problem(statusCode: 409, type: "version-conflict")
                : Results.Ok(new { bookingAppointmentId = id, status = "CheckedIn", checkedInAt = "2026-10-14T09:35:00+01:00", outcomeAt = (string?)null, version = 2, _links = new Dictionary<string, object>() }));
    }
}