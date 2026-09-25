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
        MapTask27(app);
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
        app.MapGet("/api/attendee-groups", (HttpContext context) => Results.Json(new { items = new[] { new { id = Guid.NewGuid(), code = "FIELD", name = "Field staff", description = "", isActive = true, version = 1, requirementTypeIds = Array.Empty<Guid>(), memberCount = 17,
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

    private static void MapTask27(WebApplication app)
    {
        var attendeeId = Guid.Parse("50000000-0000-0000-0000-000000000005");
        var deliveryId = Guid.Parse("60000000-0000-0000-0000-000000000006");
        var bookingId = Guid.Parse("70000000-0000-0000-0000-000000000007");
        var eventId = Guid.Parse("30000000-0000-0000-0000-000000000003");
        var time = new { date = "2026-10-14", startTime = "09:30:00", durationMinutes = 90,
            startLocal = "2026-10-14T09:30:00+01:00", endLocal = "2026-10-14T11:00:00+01:00",
            startUtc = "2026-10-14T08:30:00Z", endUtc = "2026-10-14T10:00:00Z",
            timeZoneId = "Europe/London", zoneAbbreviation = "BST" };
        app.MapGet("/api/attendees", () => Results.Json(new { items = new object[]
        {
            new { attendeeId, name = "T. Okafor", email = "t@example.org",
                status = "AwaitingAvailability", statusDisplay = "Awaiting availability",
                groupCode = "FIELD", readiness = "NoActiveBooking",
                requiredTypeCodes = new[] { "MED" }, latestDeliveryStatus = (string?)"Sent",
                cursor = "cursor-1", latestDeliveryId = (Guid?)null,
                _links = new Dictionary<string, object>
                {
                    ["update"] = new { href = $"/api/attendees/{attendeeId}", method = "PUT", operationId = "updateAttendee" },
                    ["invite"] = new { href = $"/api/attendees/{attendeeId}/invites", method = "POST", operationId = "inviteAttendee" },
                    ["delete"] = new { href = $"/api/attendees/{attendeeId}", method = "DELETE", operationId = "deleteAttendee" },
                } },
            new { attendeeId = Guid.Parse("50000000-0000-0000-0000-000000000008"), name = "R. Singh", email = "r@example.org",
                status = "Invited", statusDisplay = "Invited (pending response)",
                groupCode = "FIELD", readiness = "NoActiveBooking",
                requiredTypeCodes = new[] { "MED" }, latestDeliveryStatus = (string?)"Failed",
                cursor = "cursor-2", latestDeliveryId = (Guid?)deliveryId,
                _links = new Dictionary<string, object>
                {
                    ["emailRetry"] = new { href = $"/api/attendees/{attendeeId}/email-retry", method = "POST", operationId = "retryEmail" },
                } },
        }, nextCursor = (string?)null }));
        app.MapPost("/api/attendees", () => Results.Created("/api/attendees/1", Guid.NewGuid()));
        app.MapPost("/api/attendees/import", () => Results.Json(new { accepted = true, importedCount = 2, errors = Array.Empty<object>() }));
        app.MapGet("/api/attendees/{id:guid}/eligible-event-count", (Guid id) =>
            Results.Json(new { count = 2, requiredOptionCount = 3 }));
        app.MapPost("/api/attendees/{id:guid}/invites", (Guid id) =>
            Results.Json(new { inviteId = Guid.NewGuid(), status = "Invited" }));
        app.MapPost("/api/attendees/{id:guid}/email-retry", (Guid id) =>
            Results.Json(new { emailLogId = Guid.NewGuid() }));
        app.MapGet("/api/attendees/{id:guid}/bookings", (Guid id) => Results.Json(new { items = new object[]
        {
            new { bookingId, isOriginal = true, eventDate = "2026-10-20",
                eventStartTime = "09:30:00", eventEndTime = "13:30:00",
                _links = new Dictionary<string, object>() },
        }, nextCursor = (string?)null }));
        app.MapPost("/api/attendees/{id:guid}/bookings/{bookingId:guid}/cancel", (Guid id, Guid bookingId) =>
            Results.Json(new { confirmationRequired = false, activeBookingCount = 0,
                cancelledBookingId = bookingId, _links = new Dictionary<string, object>() }));
        app.MapGet("/api/attendees/{id:guid}/readiness", (Guid id) => Results.Json(new
        {
            attendeeId = id, code = "AppointmentsOutstanding", display = "Appointments outstanding",
            outstandingAppointmentTypes = new[] { new { code = "MED", name = "Medical check", isRecoverable = true } },
            _links = new Dictionary<string, object>(),
        }));
        app.MapPost("/api/attendees/{id:guid}/recovery-invites", (Guid id) =>
            Results.Json(new { recoveryInviteId = Guid.NewGuid(),
                locationIds = new[] { Guid.Parse("10000000-0000-0000-0000-000000000001") },
                recoverableTypeIds = new[] { Guid.NewGuid() },
                _links = new Dictionary<string, object>() }));
        app.MapGet("/api/dashboards", () => Results.Json(new
        {
            awaitingAvailability = new { count = 1, rows = new[] { new {
                attendeeId, name = "T. Okafor", email = "t@example.org",
                requiredCodes = new[] { "MED" }, waitingSince = "2026-10-01", daysWaiting = 13 } } },
            noResponse = new { count = 0, rows = Array.Empty<object>() },
            events = new { count = 1, rows = new[] { new {
                eventId, locationId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                locationName = "London HQ", time,
                capacities = new[] { new { code = "MED", totalHeadcount = 6, remainingCapacity = 3 } },
                activeBookings = 3,
                _links = new Dictionary<string, object>
                {
                    ["audit"] = new { href = $"/api/audit/events/{eventId}", method = "GET", operationId = "eventAudit" },
                } } } },
            failedEmails = 1, pendingEmails = 0,
            _links = new Dictionary<string, object>(),
        }));
        app.MapGet("/api/audit", () => Results.Json(new { items = new object[]
        {
            new { id = Guid.NewGuid(), timestamp = "2026-10-13T10:00:00Z", entityType = "Event",
                entityId = eventId, action = "EventConfirmed",
                actorType = "Staff", actorId = "staff-1", actorDisplay = "M. Manager",
                details = "6 headcount", _links = new Dictionary<string, object>() },
            new { id = Guid.NewGuid(), timestamp = "2026-10-13T09:30:00Z", entityType = "Attendee",
                entityId = attendeeId, action = "InviteSent",
                actorType = "Staff", actorId = "staff-2", actorDisplay = "C. Coordinator",
                details = (string?)"Invite emailed", _links = new Dictionary<string, object>() },
        }, nextCursor = (string?)null }));
        app.MapGet("/api/audit/attendees/{id:guid}", (Guid id) =>
            Results.Json(new { items = Array.Empty<object>(), nextCursor = (string?)null }));
        app.MapGet("/api/audit/events/{id:guid}", (Guid id) =>
            Results.Json(new { items = Array.Empty<object>(), nextCursor = (string?)null }));
        app.MapGet("/api/booking/{token}", (string token) => Results.Json(new
        {
            inviteId = Guid.NewGuid(), attendeeName = "Ravi",
            appointmentTypeNames = new[] { "Medical check" },
            options = new object[]
            {
                new { eventId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                    locationName = "London HQ", address = "1 Example St", time },
                new { eventId = Guid.Parse("20000000-0000-0000-0000-000000000002"),
                    locationName = "Dublin Centre", address = "2 Sample Rd", time },
            },
            isRecovery = false,
            _links = new Dictionary<string, object>
            {
                ["confirm"] = new { href = $"/api/booking/{token}/confirm", method = "POST", operationId = "confirmBooking" },
            },
        }));
        app.MapPost("/api/booking/{token}/confirm", (string token) =>
            Results.Created($"/api/manage/e2e-manage-token",
                new { bookingId = Guid.NewGuid(), manageToken = "e2e-manage-token" }));
        app.MapGet("/api/manage/{token}", (string token) => Results.Json(new
        {
            attendeeName = "Ravi", locationName = "London HQ", address = "1 Example St", time,
            appointmentTypeNames = new[] { "Medical check" },
            _links = new Dictionary<string, object>
            {
                ["cancel"] = new { href = $"/api/manage/{token}/cancel", method = "POST", operationId = "cancelManagedBooking" },
            },
        }));
        app.MapPost("/api/manage/{token}/cancel", (string token) =>
            Results.Json(new { outcome = "cancelled", inviteId = (Guid?)null }));
    }
}