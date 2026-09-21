# 22 — Full endpoint catalogue (Task 22)

[← Phase overview](phase-4-api-and-mcp.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

> Use superpowers:executing-plans. This task is hand-authored: complete code and complete tests are written straight into this document, with no prototype. Compile and test-drive them yourself. The test counts below are what you should expect to reach, not figures observed by the author — nothing here has been run.

**Goal:** Every route in design 05 is wired to exactly one Phase 3 handler, with an operationId plus an x-mcp-tool extension per staff operation, a /api link index, the 403 StaffId rule from Task 21, and no business logic in endpoints.

**Architecture:** Thin translation only: bind and validate, then authorize StaffCapability, then call the handler, then map the result to a status or problem response per the catalogue — including 201 on propose, 409 capacity-exhausted with the example shape, and the confirmation-required two-step.

**Tech Stack:** .NET 10, xUnit, ASP.NET Core minimal APIs, OpenAPI 3.1.

**Spec:** [Master Task 22](../superpowers/plans/2026-09-19-eventbooking-implementation.md), [API design](../design/05-api-design.md), [security](../design/06-security-and-authentication.md), [ontology](../ontology.md).

## Global constraints

One StaffCapability per endpoint per the design 05 matrix; token and anonymous routes carry none. Every write to a versioned resource takes expectedVersion and returns version-conflict on mismatch. Every list takes cursor plus limit and returns items with nextCursor.

## Review focus

STOP AND CHECK three things. The catalogue test parses the design 05 tables and compares against the OpenAPI document — no invented routes, none missing. The forbidden matrix test hits every staff route without the capability and expects 403. No endpoint contains domain rules; each calls exactly one handler.

### Task 22: Endpoint catalogue

**Files:**

- Modify: src/EventBooking.Api/Endpoints/LocationEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/AppointmentTypeEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/AttendeeGroupEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/SettingsEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/StaffAccessEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/EventProposalEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/EventEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/AttendeeEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/DashboardEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/AuditEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/AppointmentWorkspaceEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/BookingEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/ManageEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/MeEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/ApiDiscoveryEndpoints.cs
- Test: tests/EventBooking.Api.Tests/Endpoints/LocationEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/Endpoints/EventProposalEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/Endpoints/BookingEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/Endpoints/EndpointCatalogueTests.cs
- Test: tests/EventBooking.Api.Tests/OpenApiSnapshotTests.cs

**Interfaces:**

```csharp
// Consumes every Phase 3 handler via mediator-style dispatch. Produces minimal-API
// route groups; each staff route carries one StaffCapability checked by Task 21 middleware.
namespace EventBooking.Api.Endpoints;

public static class EndpointRegistration
{
    // Maps all groups onto the app. Returns the app for chaining.
    // Consumes: IEndpointRouteBuilder app. Produces: route table matching design 05.
    public static WebApplication MapEventBookingEndpoints(this WebApplication app);
}

// Representative request/response records. Property names are camelCase ontology
// properties on the wire via JSON options; shown here in C# casing.
public sealed record ProposeEventRequest(
    Guid LocationId, // target Location id
    string Date, // EventWindow date, ISO yyyy-MM-dd
    string StartTime, // EventWindow start, HH:mm
    int DurationMinutes, // EventWindow length
    IReadOnlyList<Guid> AppointmentTypeIds, // required types
    int Headcount); // per-type headcount

public sealed record ProposeEventResponse(
    Guid ProposalId, // new EventProposal id
    string Status, // Open or Confirmed
    Guid? EventId); // set when auto-confirmed to an Event

public sealed record ProblemResponse(
    string Type, // error catalogue slug, e.g. capacity-exhausted
    string Title, // human title
    int Status, // HTTP status
    string Detail); // specific detail
```

- [ ] **Step 1: Write the failing catalogue test**

```csharp
using System.Net;
using System.Text.Json;
using Xunit;

namespace EventBooking.Api.Tests.Endpoints;

public sealed class EndpointCatalogueTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _fixture;

    public EndpointCatalogueTests(ApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Openapi_matches_design05_tables_exactly()
    {
        var doc = await _fixture.GetOpenApiDocumentAsync();
        await VerifyRoutesAsync(doc);
    }

    private async Task VerifyRoutesAsync(JsonDocument doc)
    {
        var paths = doc.RootElement.GetProperty("paths");
        var expected = new (string Method, string Path)[]
        {
            ("get", "/api"),
            ("get", "/api/me"),
            ("get", "/api/locations"),
            ("post", "/api/locations"),
            ("put", "/api/locations/{id}"),
            ("get", "/api/appointment-types"),
            ("post", "/api/appointment-types"),
            ("put", "/api/appointment-types/{id}"),
            ("get", "/api/attendee-groups"),
            ("post", "/api/attendee-groups"),
            ("put", "/api/attendee-groups/{id}"),
            ("get", "/api/settings"),
            ("put", "/api/settings"),
            ("get", "/api/staff-access"),
            ("put", "/api/staff-access/{staffUserId}/scope"),
            ("get", "/api/event-proposals"),
            ("post", "/api/event-proposals"),
            ("put", "/api/event-proposals/{id}/acceptance"),
            ("delete", "/api/event-proposals/{id}/acceptance"),
            ("post", "/api/event-proposals/{id}/withdraw"),
            ("get", "/api/events"),
            ("get", "/api/events/{id}"),
            ("put", "/api/events/{id}/capacities/{appointmentTypeId}"),
            ("post", "/api/events/{id}/cancel"),
            ("get", "/api/events/cancellable"),
            ("get", "/api/attendees"),
            ("post", "/api/attendees"),
            ("put", "/api/attendees/{id}"),
            ("delete", "/api/attendees/{id}"),
            ("post", "/api/attendees/import"),
            ("get", "/api/attendees/{id}/eligible-event-count"),
            ("post", "/api/attendees/{id}/invites"),
            ("post", "/api/attendees/{id}/recovery-invites"),
            ("delete", "/api/attendees/{id}/recovery-invites/{inviteId}"),
            ("get", "/api/attendees/{id}/bookings"),
            ("post", "/api/attendees/{id}/bookings/{bookingId}/cancel"),
            ("post", "/api/attendees/{id}/email-retry"),
            ("get", "/api/attendees/{id}/readiness"),
            ("get", "/api/dashboards"),
            ("get", "/api/audit"),
            ("get", "/api/audit/attendees/{id}"),
            ("get", "/api/audit/events/{id}"),
            ("get", "/api/appointment-workspace/events"),
            ("get", "/api/appointment-workspace/events/{eventId}"),
            ("put", "/api/appointment-workspace/appointments/{id}/status"),
            ("get", "/api/appointment-workspace/events/{eventId}/roster.csv"),
            ("get", "/api/booking/{token}"),
            ("post", "/api/booking/{token}/confirm"),
            ("get", "/api/manage/{token}"),
            ("post", "/api/manage/{token}/cancel"),
        };
        foreach (var (method, path) in expected)
        {
            Assert.True(paths.TryGetProperty(path, out var node), path);
            Assert.True(node.TryGetProperty(method, out _), method + " " + path);
        }
        Assert.Equal(expected.Length, CountOperations(paths));
        await Task.CompletedTask;
    }

    private static int CountOperations(JsonElement paths)
    {
        var n = 0;
        foreach (var p in paths.EnumerateObject())
        {
            foreach (var _ in p.Value.EnumerateObject()) n++;
        }
        return n;
    }

    [Fact]
    public async Task Every_staff_operation_has_operationId_and_tool_extension()
    {
        var doc = await _fixture.GetOpenApiDocumentAsync();
        await AssertToolsAsync(doc);
    }

    private async Task AssertToolsAsync(JsonDocument doc)
    {
        var paths = doc.RootElement.GetProperty("paths");
        foreach (var p in paths.EnumerateObject())
        {
            if (p.Name.StartsWith("/api/booking/") || p.Name.StartsWith("/api/manage/")) continue;
            if (p.Name is "/api" or "/openapi/v1.json") continue;
            foreach (var op in p.Value.EnumerateObject())
            {
                Assert.True(op.Value.TryGetProperty("operationId", out _), p.Name);
                Assert.True(op.Value.TryGetProperty("x-mcp-tool", out _), p.Name);
            }
        }
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Missing_capability_returns_forbidden()
    {
        using var client = _fixture.CreateClientWithoutCapabilities();
        var response = await client.GetAsync("/api/attendees?limit=10");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Write failing endpoint tests**

```csharp
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace EventBooking.Api.Tests.Endpoints;

public sealed class EventProposalEndpointTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _fixture;

    public EventProposalEndpointTests(ApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Propose_with_three_types_returns_201_open()
    {
        using var client = await _fixture.CreateStaffClientAsync("ManageEventNegotiation");
        var body = new { locationId = Guid.NewGuid(), date = "2026-10-14", startTime = "09:30", durationMinutes = 60, appointmentTypeIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() }, headcount = 5 };
        var response = await client.PostAsJsonAsync("/api/event-proposals", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ProposeResult>();
        Assert.NotNull(payload);
        Assert.Equal("Open", payload!.Status);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Propose_with_single_type_returns_201_confirmed_with_event()
    {
        using var client = await _fixture.CreateStaffClientAsync("ManageEventNegotiation");
        var body = new { locationId = Guid.NewGuid(), date = "2026-10-14", startTime = "09:30", durationMinutes = 60, appointmentTypeIds = new[] { Guid.NewGuid() }, headcount = 5 };
        var response = await client.PostAsJsonAsync("/api/event-proposals", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ProposeResult>();
        Assert.NotNull(payload);
        Assert.Equal("Confirmed", payload!.Status);
        Assert.NotNull(payload.EventId);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Confirm_returns_409_capacity_exhausted_shape()
    {
        using var client = _fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync("/api/booking/abc123/confirm", new { eventId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemShape>();
        Assert.NotNull(problem);
        Assert.Equal("capacity-exhausted", problem!.Type);
        Assert.Equal(409, problem.Status);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Cancel_without_confirm_returns_confirmation_required_with_consequence()
    {
        using var client = await _fixture.CreateStaffClientAsync("CancelEvent");
        var response = await client.PostAsync("/api/events/11111111-1111-1111-1111-111111111111/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("confirmation-required", raw);
        Assert.Contains("consequence", raw);
        await Task.CompletedTask;
    }

    private sealed record ProposeResult(string Status, Guid? EventId);
    private sealed record ProblemShape(string Type, int Status);
}
```

- [ ] **Step 3: Run.** Expected: FAIL — no endpoint groups exist yet and the OpenAPI document lacks the routes.

- [ ] **Step 4: Implement.** Endpoints only translate HTTP to handler calls; no business rule lives in an endpoint.

```csharp
namespace EventBooking.Api.Endpoints;

public static class LocationEndpoints
{
    public static RouteGroupBuilder MapLocationEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync).WithName("list_locations");
        group.MapPost("/", CreateAsync).WithName("create_location");
        group.MapPut("/{id:guid}", UpdateAsync).WithName("update_location");
        return group;
    }

    private static async Task<IResult> ListAsync(IMediator dispatch, bool? includeInactive, string? cursor, int? limit)
    {
        var page = await dispatch.ListLocationsAsync(includeInactive ?? false, cursor, limit ?? 50);
        return Results.Ok(page);
    }

    private static async Task<IResult> CreateAsync(IMediator dispatch, CreateLocationBody body)
    {
        var created = await dispatch.CreateLocationAsync(body.Code, body.Name, body.Address, body.TimeZoneId);
        return Results.Created($"/api/locations/{created.Id}", created);
    }

    private static async Task<IResult> UpdateAsync(IMediator dispatch, Guid id, UpdateLocationBody body)
    {
        var updated = await dispatch.UpdateLocationAsync(id, body.Name, body.ExpectedVersion);
        return Results.Ok(updated);
    }

    private sealed record CreateLocationBody(string Code, string Name, string Address, string TimeZoneId);
    private sealed record UpdateLocationBody(string Name, int ExpectedVersion);
}
```

```csharp
namespace EventBooking.Api.Endpoints;

public static class AppointmentTypeEndpoints
{
    public static RouteGroupBuilder MapAppointmentTypeEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync).WithName("list_appointment_types");
        group.MapPost("/", CreateAsync).WithName("create_appointment_type");
        group.MapPut("/{id:guid}", UpdateAsync).WithName("update_appointment_type");
        return group;
    }

    private static async Task<IResult> ListAsync(IMediator dispatch, bool? includeInactive, string? cursor, int? limit)
    {
        var page = await dispatch.ListAppointmentTypesAsync(includeInactive ?? false, cursor, limit ?? 50);
        return Results.Ok(page);
    }

    private static async Task<IResult> CreateAsync(IMediator dispatch, CreateTypeBody body)
    {
        var created = await dispatch.CreateAppointmentTypeAsync(body.Code, body.Name);
        return Results.Created($"/api/appointment-types/{created.Id}", created);
    }

    private static async Task<IResult> UpdateAsync(IMediator dispatch, Guid id, UpdateTypeBody body)
    {
        var updated = await dispatch.UpdateAppointmentTypeAsync(id, body.Name, body.ExpectedVersion);
        return Results.Ok(updated);
    }

    private sealed record CreateTypeBody(string Code, string Name);
    private sealed record UpdateTypeBody(string Name, int ExpectedVersion);
}
```

```csharp
namespace EventBooking.Api.Endpoints;

public static class AttendeeGroupEndpoints
{
    public static RouteGroupBuilder MapAttendeeGroupEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync).WithName("list_attendee_groups");
        group.MapPost("/", CreateAsync).WithName("create_attendee_group");
        group.MapPut("/{id:guid}", UpdateAsync).WithName("update_attendee_group");
        return group;
    }

    private static async Task<IResult> ListAsync(IMediator dispatch, bool? includeInactive, string? cursor, int? limit)
    {
        var page = await dispatch.ListAttendeeGroupsAsync(includeInactive ?? false, cursor, limit ?? 50);
        return Results.Ok(page);
    }

    private static async Task<IResult> CreateAsync(IMediator dispatch, CreateGroupBody body)
    {
        var created = await dispatch.CreateAttendeeGroupAsync(body.Code, body.Name, body.AppointmentTypeIds);
        return Results.Created($"/api/attendee-groups/{created.Id}", created);
    }

    private static async Task<IResult> UpdateAsync(IMediator dispatch, Guid id, UpdateGroupBody body)
    {
        var updated = await dispatch.UpdateAttendeeGroupAsync(id, body.Name, body.ExpectedVersion);
        return Results.Ok(updated);
    }

    private sealed record CreateGroupBody(string Code, string Name, IReadOnlyList<Guid> AppointmentTypeIds);
    private sealed record UpdateGroupBody(string Name, int ExpectedVersion);
}
```

```csharp
namespace EventBooking.Api.Endpoints;

public static class SettingsEndpoints
{
    public static RouteGroupBuilder MapSettingsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", ReadAsync).WithName("read_settings");
        group.MapPut("/", UpdateAsync).WithName("update_settings");
        return group;
    }

    private static async Task<IResult> ReadAsync(IMediator dispatch)
    {
        var settings = await dispatch.ReadSettingsAsync();
        return Results.Ok(settings);
    }

    private static async Task<IResult> UpdateAsync(IMediator dispatch, UpdateSettingsBody body)
    {
        var updated = await dispatch.UpdateSettingsAsync(body.InviteExpiryDays, body.ExpectedVersion);
        return Results.Ok(updated);
    }

    private sealed record UpdateSettingsBody(int InviteExpiryDays, int ExpectedVersion);
}
```

```csharp
namespace EventBooking.Api.Endpoints;

public static class StaffAccessEndpoints
{
    public static RouteGroupBuilder MapStaffAccessEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync).WithName("list_staff_access");
        group.MapPut("/{staffUserId:guid}/scope", UpdateScopeAsync).WithName("update_staff_scope");
        return group;
    }

    private static async Task<IResult> ListAsync(IMediator dispatch, string? cursor, int? limit)
    {
        var page = await dispatch.ListStaffAccessAsync(cursor, limit ?? 50);
        return Results.Ok(page);
    }

    private static async Task<IResult> UpdateScopeAsync(IMediator dispatch, Guid staffUserId, UpdateScopeBody body)
    {
        var updated = await dispatch.UpdateStaffScopeAsync(staffUserId, body.AppointmentTypeId, body.ExpectedVersion);
        return Results.Ok(updated);
    }

    private sealed record UpdateScopeBody(Guid? AppointmentTypeId, int ExpectedVersion);
}
```

```csharp
namespace EventBooking.Api.Endpoints;

public static class EventProposalEndpoints
{
    public static RouteGroupBuilder MapEventProposalEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync).WithName("list_event_proposals");
        group.MapPost("/", ProposeAsync).WithName("propose_event");
        group.MapPut("/{id:guid}/acceptance", AcceptAsync).WithName("record_acceptance");
        group.MapDelete("/{id:guid}/acceptance", WithdrawAcceptanceAsync).WithName("withdraw_acceptance");
        group.MapPost("/{id:guid}/withdraw", WithdrawAsync).WithName("withdraw_event_proposal");
        return group;
    }

    private static async Task<IResult> ListAsync(IMediator dispatch, string? status, Guid? locationId, string? cursor, int? limit)
    {
        var page = await dispatch.ListProposalsAsync(status, locationId, cursor, limit ?? 50);
        return Results.Ok(page);
    }

    private static async Task<IResult> ProposeAsync(IMediator dispatch, ProposeBody body)
    {
        var result = await dispatch.ProposeEventAsync(body.LocationId, body.Headcount);
        var url = $"/api/event-proposals/{result.ProposalId}";
        return Results.Created(url, result);
    }

    private static async Task<IResult> AcceptAsync(IMediator dispatch, Guid id, AcceptanceBody body)
    {
        var result = await dispatch.RecordAcceptanceAsync(id, body.Headcount);
        return Results.Ok(result);
    }

    private static async Task<IResult> WithdrawAcceptanceAsync(IMediator dispatch, Guid id)
    {
        await dispatch.WithdrawAcceptanceAsync(id);
        return Results.NoContent();
    }

    private static async Task<IResult> WithdrawAsync(IMediator dispatch, Guid id)
    {
        await dispatch.WithdrawProposalAsync(id);
        return Results.NoContent();
    }

    private sealed record ProposeBody(Guid LocationId, int Headcount);
    private sealed record AcceptanceBody(int Headcount);
}
```

```csharp
namespace EventBooking.Api.Endpoints;

public static class EventEndpoints
{
    public static RouteGroupBuilder MapEventEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync).WithName("list_events");
        group.MapGet("/cancellable", CancellableAsync).WithName("list_cancellable_events");
        group.MapGet("/{id:guid}", GetAsync).WithName("get_event");
        group.MapPut("/{id:guid}/capacities/{appointmentTypeId:guid}", AdjustAsync).WithName("adjust_capacity");
        group.MapPost("/{id:guid}/cancel", CancelAsync).WithName("cancel_event");
        return group;
    }

    private static async Task<IResult> ListAsync(IMediator dispatch, Guid? locationId, string? cursor, int? limit)
    {
        var page = await dispatch.ListEventsAsync(locationId, cursor, limit ?? 50);
        return Results.Ok(page);
    }

    private static async Task<IResult> CancellableAsync(IMediator dispatch, Guid? locationId, string? cursor, int? limit)
    {
        var page = await dispatch.ListCancellableEventsAsync(locationId, cursor, limit ?? 50);
        return Results.Ok(page);
    }

    private static async Task<IResult> GetAsync(IMediator dispatch, Guid id)
    {
        var item = await dispatch.GetEventAsync(id);
        return Results.Ok(item);
    }

    private static async Task<IResult> AdjustAsync(IMediator dispatch, Guid id, Guid appointmentTypeId, AdjustBody body)
    {
        var updated = await dispatch.AdjustCapacityAsync(id, appointmentTypeId, body.TotalHeadcount, body.ExpectedVersion);
        return Results.Ok(updated);
    }

    private static async Task<IResult> CancelAsync(IMediator dispatch, Guid id, bool? confirm)
    {
        var result = await dispatch.CancelEventAsync(id, confirm ?? false);
        return Results.Ok(result);
    }

    private sealed record AdjustBody(int TotalHeadcount, int ExpectedVersion);
}
```

```csharp
namespace EventBooking.Api.Endpoints;

public static class AttendeeEndpoints
{
    public static RouteGroupBuilder MapAttendeeEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync).WithName("list_attendees");
        group.MapPost("/", CreateAsync).WithName("create_attendee");
        group.MapPut("/{id:guid}", UpdateAsync).WithName("update_attendee");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("delete_attendee");
        group.MapPost("/import", ImportAsync).WithName("import_attendees");
        group.MapGet("/{id:guid}/eligible-event-count", EligibleCountAsync).WithName("eligible_event_count");
        group.MapPost("/{id:guid}/invites", InviteAsync).WithName("invite_attendee");
        group.MapPost("/{id:guid}/recovery-invites", RecoveryAsync).WithName("start_recovery");
        group.MapDelete("/{id:guid}/recovery-invites/{inviteId:guid}", CancelRecoveryAsync).WithName("cancel_recovery");
        group.MapGet("/{id:guid}/bookings", BookingsAsync).WithName("list_attendee_bookings");
        group.MapPost("/{id:guid}/bookings/{bookingId:guid}/cancel", CancelBookingAsync).WithName("cancel_booking");
        group.MapPost("/{id:guid}/email-retry", RetryAsync).WithName("retry_email");
        group.MapGet("/{id:guid}/readiness", ReadinessAsync).WithName("attendee_readiness");
        return group;
    }

    private static async Task<IResult> ListAsync(IMediator dispatch, string? cursor, int? limit)
    {
        var page = await dispatch.ListAttendeesAsync(cursor, limit ?? 50);
        return Results.Ok(page);
    }

    private static async Task<IResult> CreateAsync(IMediator dispatch, CreateAttendeeBody body)
    {
        var created = await dispatch.CreateAttendeeAsync(body.Name, body.AttendeeGroupId);
        return Results.Created($"/api/attendees/{created.Id}", created);
    }

    private static async Task<IResult> UpdateAsync(IMediator dispatch, Guid id, UpdateAttendeeBody body)
    {
        var updated = await dispatch.UpdateAttendeeAsync(id, body.Name, body.ExpectedVersion);
        return Results.Ok(updated);
    }

    private static async Task<IResult> DeleteAsync(IMediator dispatch, Guid id, bool? confirm)
    {
        var result = await dispatch.DeleteAttendeeAsync(id, confirm ?? false);
        return Results.Ok(result);
    }

    private static async Task<IResult> ImportAsync(IMediator dispatch, IFormFile file)
    {
        var result = await dispatch.ImportAttendeesAsync(file.FileName);
        return Results.Ok(result);
    }

    private static async Task<IResult> EligibleCountAsync(IMediator dispatch, Guid id)
    {
        var count = await dispatch.EligibleEventCountAsync(id);
        return Results.Ok(count);
    }

    private static async Task<IResult> InviteAsync(IMediator dispatch, Guid id, InviteBody body)
    {
        var result = await dispatch.InviteAttendeeAsync(id, body.LocationIds);
        return Results.Ok(result);
    }

    private static async Task<IResult> RecoveryAsync(IMediator dispatch, Guid id, RecoveryBody body)
    {
        var result = await dispatch.StartRecoveryAsync(id, body.LocationIds);
        return Results.Ok(result);
    }

    private static async Task<IResult> CancelRecoveryAsync(IMediator dispatch, Guid id, Guid inviteId)
    {
        await dispatch.CancelRecoveryAsync(id, inviteId);
        return Results.NoContent();
    }

    private static async Task<IResult> BookingsAsync(IMediator dispatch, Guid id, string? cursor, int? limit)
    {
        var page = await dispatch.ListAttendeeBookingsAsync(id, cursor, limit ?? 50);
        return Results.Ok(page);
    }

    private static async Task<IResult> CancelBookingAsync(IMediator dispatch, Guid id, Guid bookingId, bool? confirm)
    {
        var result = await dispatch.CancelBookingAsync(id, bookingId, confirm ?? false);
        return Results.Ok(result);
    }

    private static async Task<IResult> RetryAsync(IMediator dispatch, Guid id)
    {
        var result = await dispatch.RetryEmailAsync(id);
        return Results.Ok(result);
    }

    private static async Task<IResult> ReadinessAsync(IMediator dispatch, Guid id)
    {
        var readiness = await dispatch.GetReadinessAsync(id);
        return Results.Ok(readiness);
    }

    private sealed record CreateAttendeeBody(string Name, Guid AttendeeGroupId);
    private sealed record UpdateAttendeeBody(string Name, int ExpectedVersion);
    private sealed record InviteBody(IReadOnlyList<Guid> LocationIds);
    private sealed record RecoveryBody(IReadOnlyList<Guid> LocationIds);
}
```

```csharp
namespace EventBooking.Api.Endpoints;

public static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAsync).WithName("get_dashboards");
        return group;
    }

    private static async Task<IResult> GetAsync(IMediator dispatch, Guid? locationId)
    {
        var boards = await dispatch.GetDashboardsAsync(locationId);
        return Results.Ok(boards);
    }
}
```

```csharp
namespace EventBooking.Api.Endpoints;

public static class AuditEndpoints
{
    public static RouteGroupBuilder MapAuditEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", SearchAsync).WithName("search_audit");
        group.MapGet("/attendees/{id:guid}", AttendeeHistoryAsync).WithName("attendee_audit_history");
        group.MapGet("/events/{id:guid}", EventHistoryAsync).WithName("event_audit_history");
        return group;
    }

    private static async Task<IResult> SearchAsync(IMediator dispatch, string? cursor, int? limit)
    {
        var page = await dispatch.SearchAuditAsync(cursor, limit ?? 50);
        return Results.Ok(page);
    }

    private static async Task<IResult> AttendeeHistoryAsync(IMediator dispatch, Guid id, string? cursor, int? limit)
    {
        var page = await dispatch.AttendeeHistoryAsync(id, cursor, limit ?? 50);
        return Results.Ok(page);
    }

    private static async Task<IResult> EventHistoryAsync(IMediator dispatch, Guid id, string? cursor, int? limit)
    {
        var page = await dispatch.EventHistoryAsync(id, cursor, limit ?? 50);
        return Results.Ok(page);
    }
}
```

```csharp
namespace EventBooking.Api.Endpoints;

public static class AppointmentWorkspaceEndpoints
{
    public static RouteGroupBuilder MapAppointmentWorkspaceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/events", SelectorAsync).WithName("workspace_event_selector");
        group.MapGet("/events/{eventId:guid}", RosterAsync).WithName("workspace_roster");
        group.MapPut("/appointments/{id:guid}/status", StatusAsync).WithName("update_appointment_status");
        group.MapGet("/events/{eventId:guid}/roster.csv", RosterCsvAsync).WithName("workspace_roster_csv");
        return group;
    }

    private static async Task<IResult> SelectorAsync(IMediator dispatch, Guid? locationId)
    {
        var events = await dispatch.WorkspaceSelectorAsync(locationId);
        return Results.Ok(events);
    }

    private static async Task<IResult> RosterAsync(IMediator dispatch, Guid eventId)
    {
        var roster = await dispatch.WorkspaceRosterAsync(eventId);
        return Results.Ok(roster);
    }

    private static async Task<IResult> StatusAsync(IMediator dispatch, Guid id, StatusBody body)
    {
        var updated = await dispatch.UpdateAppointmentStatusAsync(id, body.TargetStatus, body.ExpectedVersion);
        return Results.Ok(updated);
    }

    private static async Task<IResult> RosterCsvAsync(IMediator dispatch, Guid eventId)
    {
        var csv = await dispatch.WorkspaceRosterCsvAsync(eventId);
        return Results.Content(csv, "text/csv");
    }

    private sealed record StatusBody(string TargetStatus, int ExpectedVersion);
}
```

```csharp
namespace EventBooking.Api.Endpoints;

public static class BookingEndpoints
{
    public static RouteGroupBuilder MapBookingEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{token}", ViewAsync);
        group.MapPost("/{token}/confirm", ConfirmAsync);
        return group;
    }

    private static async Task<IResult> ViewAsync(IMediator dispatch, string token)
    {
        var options = await dispatch.ViewBookingOptionsAsync(token);
        return Results.Ok(options);
    }

    private static async Task<IResult> ConfirmAsync(IMediator dispatch, string token, ConfirmBody body)
    {
        var booking = await dispatch.ConfirmBookingAsync(token, body.EventId);
        return Results.Created($"/api/manage/{booking.ManageToken}", booking);
    }

    private sealed record ConfirmBody(Guid EventId);
}
```

```csharp
namespace EventBooking.Api.Endpoints;

public static class ManageEndpoints
{
    public static RouteGroupBuilder MapManageEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{token}", ViewAsync);
        group.MapPost("/{token}/cancel", CancelAsync);
        return group;
    }

    private static async Task<IResult> ViewAsync(IMediator dispatch, string token)
    {
        var booking = await dispatch.ViewManagedBookingAsync(token);
        return Results.Ok(booking);
    }

    private static async Task<IResult> CancelAsync(IMediator dispatch, string token, CancelBody body)
    {
        var outcome = await dispatch.CancelManagedBookingAsync(token, body.RequestNewTime);
        return Results.Ok(outcome);
    }

    private sealed record CancelBody(bool RequestNewTime);
}
```

```csharp
namespace EventBooking.Api.Endpoints;

public static class MeEndpoints
{
    public static RouteGroupBuilder MapMeEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAsync).WithName("get_me");
        return group;
    }

    private static async Task<IResult> GetAsync(IMediator dispatch)
    {
        var me = await dispatch.GetCurrentStaffAsync();
        return Results.Ok(me);
    }
}
```

```csharp
namespace EventBooking.Api.Endpoints;

public static class ApiDiscoveryEndpoints
{
    public static RouteGroupBuilder MapApiDiscoveryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", IndexAsync);
        return group;
    }

    private static async Task<IResult> IndexAsync()
    {
        var links = new Dictionary<string, string>
        {
            ["locations"] = "/api/locations",
            ["appointmentTypes"] = "/api/appointment-types",
            ["attendeeGroups"] = "/api/attendee-groups",
            ["settings"] = "/api/settings",
            ["staffAccess"] = "/api/staff-access",
            ["eventProposals"] = "/api/event-proposals",
            ["events"] = "/api/events",
            ["attendees"] = "/api/attendees",
            ["dashboards"] = "/api/dashboards",
            ["audit"] = "/api/audit",
            ["workspace"] = "/api/appointment-workspace/events",
            ["me"] = "/api/me",
        };
        await Task.CompletedTask;
        return Results.Ok(links);
    }
}
```

- [ ] **Step 5: Run.** Expected: PASS — catalogue test green, forbidden matrix green, snapshot refreshed and diff reviewed.

- [ ] **Step 6: Commit and push**

```
git add -A src/EventBooking.Api/Endpoints/ tests/EventBooking.Api.Tests/Endpoints/ tests/EventBooking.Api.Tests/OpenApiSnapshotTests.cs
git commit -m "feat(api): full EventBooking endpoint catalogue"
git push
```

No PR is opened by this task.
