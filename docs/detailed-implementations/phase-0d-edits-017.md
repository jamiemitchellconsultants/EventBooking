# 00d — Retire direct event import, edits 17 (Task 3b)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Mcp.Tests/AgentSurfaceParityTests.cs — 1/1

<!-- retirement-file: {"id":55,"file":"tests/EventBooking.Mcp.Tests/AgentSurfaceParityTests.cs","beforeSha":"e814d3a03868d57fb05c2638c76dd92cbfa7e9beb7d7c226063ccd014ee0214c","afterSha":"db456b484b8a0385ba44542b2e3b6e4bcbfd7b8ca991b59c0b59ee3333e47339","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Api.OpenApi;
using EventBooking.Domain.Access;

namespace EventBooking.Mcp.Tests;

[Collection("mcp")]
public sealed class AgentSurfaceParityTests(McpFactory factory)
{
    [Fact]
    public async Task ToolsListExactlyMatchesCatalogAndHints()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { jsonrpc = "2.0", id = "1", method = "tools/list" }),
                Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var data = body.TrimStart().StartsWith('{') ? body : body.Split('\n').Select(x => x.Trim())
            .Last(x => x.StartsWith("data: "))["data: ".Length..];
        using var json = JsonDocument.Parse(data);
        var actual = json.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .ToDictionary(x => x.GetProperty("name").GetString()!);
        var expected = AgentOperationCatalog.All.Values.Where(x => x.McpTool is not null)
            .ToDictionary(x => x.McpTool!);
        Assert.Equal(35, actual.Count);
        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        foreach (var pair in expected)
        {
            var annotations = actual[pair.Key].GetProperty("annotations");
            Assert.Equal(pair.Value.Hints.ReadOnly, annotations.GetProperty("readOnlyHint").GetBoolean());
            Assert.Equal(pair.Value.Hints.Destructive, annotations.GetProperty("destructiveHint").GetBoolean());
            Assert.Equal(pair.Value.Hints.Idempotent, annotations.GetProperty("idempotentHint").GetBoolean());
            Assert.Equal(pair.Value.Hints.OpenWorld, annotations.GetProperty("openWorldHint").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(actual[pair.Key].GetProperty("description").GetString()));
        }
    }
}
`````

## after — tests/EventBooking.Mcp.Tests/Fixtures/EventFixture.cs — 1/1

<!-- retirement-file: {"id":56,"file":"tests/EventBooking.Mcp.Tests/Fixtures/EventFixture.cs","beforeSha":null,"afterSha":"0c13090f3c6ad44a0345bbc0bdf398e7e8c71acbc46ef53a8d49dba1a3cfcb05","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

internal static class EventFixture
{
    public static Event Create(Guid id, EventWindow window, IReadOnlyDictionary<Guid, int> headcounts)
    {
        var manager = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var proposal = EventProposal.Create(Guid.NewGuid(), window, manager);
        foreach (var type in AppointmentTypeIds.All)
            proposal.Accept(type, manager, headcounts[type]);
        return Event.CreateFrom(id, proposal);
    }
}
`````

## before — tests/EventBooking.Mcp.Tests/McpEndpointTests.cs — 1/1

<!-- retirement-file: {"id":57,"file":"tests/EventBooking.Mcp.Tests/McpEndpointTests.cs","beforeSha":"8f55ee27decdd71adf2deed6a9f1634288a648c312360c80548b5f50a4bf5340","afterSha":"d34c4828c315fe29607f7afc1df66880538fac6918873ee2f8c550b9c6994029","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;

namespace EventBooking.Mcp.Tests;

/// <summary>Covers the MCP transport: authorization, tool discovery, and stateless calls.</summary>
[Collection("mcp")]
public sealed class McpEndpointTests(McpFactory factory)
{
    private static readonly string[] ExpectedTools =
    [
        "propose_event", "accept_proposal", "withdraw_acceptance", "withdraw_proposal",
        "event_board", "adjust_event_capacity", "cancel_event", "import_events",
        "list_attendees", "create_attendee", "update_attendee", "delete_attendee",
        "list_attendee_groups",
        "import_attendees", "trigger_invite", "retry_attendee_email",
        "start_recovery_invite", "cancel_recovery_invite", "list_attendee_bookings",
        "cancel_attendee_booking", "get_attendee_readiness",
        "get_settings", "update_settings", "list_staff_access", "replace_staff_access_scope",
        "clear_staff_access_scope", "get_my_access",
        "get_dashboards", "event_audit_history", "attendee_audit_history", "search_audit",
        "appointment_events", "appointment_event_detail", "export_appointment_roster", "update_appointment_status",
        "get_event_operations",
    ];

    /// <summary>Anonymous MCP requests are refused before any tool runs.</summary>
    [Fact]
    public async Task AnonymousMcpRequest_IsUnauthorized()
    {
        factory.SignedInAs = null;

        var response = await PostRpcAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>An authenticated profile without a valid staff claim cannot reach MCP tools.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("not-a-staff-id")]
    public async Task AuthenticatedMcpRequestWithoutValidStaffClaim_IsForbidden(string? staffIdClaim)
    {
        var originalStaffUserId = factory.SignedInAs;
        var originalStaffIdClaim = factory.StaffIdClaim;
        try
        {
            factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
            factory.StaffIdClaim = staffIdClaim;

            var response = await PostRpcAsync(
                new { jsonrpc = "2.0", id = "1", method = "tools/list" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            factory.SignedInAs = originalStaffUserId;
            factory.StaffIdClaim = originalStaffIdClaim;
        }
    }

    /// <summary>An authenticated caller discovers the full staff tool surface.</summary>
    [Fact]
    public async Task ToolsList_ExposesFullStaffSurface()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);

        var payload = await PostRpcJsonAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });
        var names = payload
            .GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString())
            .ToHashSet();

        Assert.Equal(ExpectedTools.Order(), names.Order());
        Assert.Equal(36, names.Count);
    }

    /// <summary>Every tool carries explicit safety hints with a closed world.</summary>
    [Fact]
    public async Task ToolsList_ExposesExplicitSafetyHints()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);

        var payload = await PostRpcJsonAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });
        foreach (var tool in payload.GetProperty("result").GetProperty("tools").EnumerateArray())
        {
            Assert.True(tool.TryGetProperty("annotations", out var annotations), $"Tool {tool.GetProperty("name")} is missing annotations.");
            Assert.True(annotations.TryGetProperty("readOnlyHint", out _), $"Tool {tool.GetProperty("name")} is missing readOnlyHint.");
            Assert.True(annotations.TryGetProperty("destructiveHint", out _), $"Tool {tool.GetProperty("name")} is missing destructiveHint.");
            Assert.True(annotations.TryGetProperty("idempotentHint", out _), $"Tool {tool.GetProperty("name")} is missing idempotentHint.");
            Assert.True(
                annotations.TryGetProperty("openWorldHint", out var openWorld) && openWorld.ValueKind == JsonValueKind.False,
                $"Tool {tool.GetProperty("name")} must have openWorldHint false.");
        }
    }

    /// <summary>A tool call runs as the signed-in identity.</summary>
    [Fact]
    public async Task GetMyAccess_ReturnsCallerRoles()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await CallToolAsync("get_my_access", new { });

        Assert.Contains("Coordinator", payload.GetRawText());
        Assert.False(IsToolError(payload));
    }

    /// <summary>The caller's validated staff claim is returned by the self-description tool.</summary>
    [Fact]
    public async Task GetMyAccess_ReturnsCallerStaffNumber()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Coordinator], null, "U123456");

        var payload = await CallToolAsync("get_my_access", new { });
        var contentText = payload.GetProperty("result").GetProperty("content")[0]
            .GetProperty("text").GetString();
        using var content = JsonDocument.Parse(contentText!);

        Assert.Equal(
            "U123456",
            content.RootElement.GetProperty("staffId").GetString());
        Assert.False(IsToolError(payload));
    }

    /// <summary>Sequential calls without any session identifier each succeed.</summary>
    [Fact]
    public async Task StatelessCalls_NeedNoSession()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var first = await CallToolAsync("get_my_access", new { });
        var second = await CallToolAsync("get_dashboards", new { });

        Assert.True(first.TryGetProperty("result", out _));
        Assert.True(second.TryGetProperty("result", out _));
        Assert.False(IsToolError(first));
        Assert.False(IsToolError(second));
    }

    /// <summary>A capability failure surfaces as a tool error, not a transport failure.</summary>
    [Fact]
    public async Task ForbiddenCapability_SurfacesAsToolError()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await CallToolAsync(
            "propose_event", new { date = "2026-10-01", startTime = "09:00" });

        Assert.True(IsToolError(payload));
    }

    private async Task<HttpResponseMessage> PostRpcAsync(object body)
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return await client.SendAsync(request);
    }

    private async Task<JsonElement> PostRpcJsonAsync(object body)
    {
        var response = await PostRpcAsync(body);
        response.EnsureSuccessStatusCode();
        return ParseRpcPayload(await response.Content.ReadAsStringAsync());
    }

    private Task<JsonElement> CallToolAsync(string name, object arguments) =>
        PostRpcJsonAsync(new
        {
            jsonrpc = "2.0",
            id = "1",
            method = "tools/call",
            @params = new { name, arguments },
        });

    private static JsonElement ParseRpcPayload(string body)
    {
        if (body.TrimStart().StartsWith('{'))
        {
            return JsonDocument.Parse(body).RootElement;
        }

        var data = body
            .Split('\n')
            .Select(line => line.Trim())
            .LastOrDefault(line => line.StartsWith("data: "))
            ?.Substring("data: ".Length);
        Assert.False(string.IsNullOrWhiteSpace(data), "MCP response carried no data frame.");
        return JsonDocument.Parse(data!).RootElement;
    }

    private static bool IsToolError(JsonElement payload) =>
        payload.TryGetProperty("result", out var result) &&
        result.TryGetProperty("isError", out var isError) &&
        isError.ValueKind == JsonValueKind.True;
}
`````

## after — tests/EventBooking.Mcp.Tests/McpEndpointTests.cs — 1/1

<!-- retirement-file: {"id":57,"file":"tests/EventBooking.Mcp.Tests/McpEndpointTests.cs","beforeSha":"8f55ee27decdd71adf2deed6a9f1634288a648c312360c80548b5f50a4bf5340","afterSha":"d34c4828c315fe29607f7afc1df66880538fac6918873ee2f8c550b9c6994029","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;

namespace EventBooking.Mcp.Tests;

/// <summary>Covers the MCP transport: authorization, tool discovery, and stateless calls.</summary>
[Collection("mcp")]
public sealed class McpEndpointTests(McpFactory factory)
{
    private static readonly string[] ExpectedTools =
    [
        "propose_event", "accept_proposal", "withdraw_acceptance", "withdraw_proposal",
        "event_board", "adjust_event_capacity", "cancel_event",
        "list_attendees", "create_attendee", "update_attendee", "delete_attendee",
        "list_attendee_groups",
        "import_attendees", "trigger_invite", "retry_attendee_email",
        "start_recovery_invite", "cancel_recovery_invite", "list_attendee_bookings",
        "cancel_attendee_booking", "get_attendee_readiness",
        "get_settings", "update_settings", "list_staff_access", "replace_staff_access_scope",
        "clear_staff_access_scope", "get_my_access",
        "get_dashboards", "event_audit_history", "attendee_audit_history", "search_audit",
        "appointment_events", "appointment_event_detail", "export_appointment_roster", "update_appointment_status",
        "get_event_operations",
    ];

    /// <summary>Anonymous MCP requests are refused before any tool runs.</summary>
    [Fact]
    public async Task AnonymousMcpRequest_IsUnauthorized()
    {
        factory.SignedInAs = null;

        var response = await PostRpcAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>An authenticated profile without a valid staff claim cannot reach MCP tools.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("not-a-staff-id")]
    public async Task AuthenticatedMcpRequestWithoutValidStaffClaim_IsForbidden(string? staffIdClaim)
    {
        var originalStaffUserId = factory.SignedInAs;
        var originalStaffIdClaim = factory.StaffIdClaim;
        try
        {
            factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
            factory.StaffIdClaim = staffIdClaim;

            var response = await PostRpcAsync(
                new { jsonrpc = "2.0", id = "1", method = "tools/list" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            factory.SignedInAs = originalStaffUserId;
            factory.StaffIdClaim = originalStaffIdClaim;
        }
    }

    /// <summary>An authenticated caller discovers the full staff tool surface.</summary>
    [Fact]
    public async Task ToolsList_ExposesFullStaffSurface()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);

        var payload = await PostRpcJsonAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });
        var names = payload
            .GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString())
            .ToHashSet();

        Assert.Equal(ExpectedTools.Order(), names.Order());
        Assert.Equal(35, names.Count);
    }

    /// <summary>Every tool carries explicit safety hints with a closed world.</summary>
    [Fact]
    public async Task ToolsList_ExposesExplicitSafetyHints()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);

        var payload = await PostRpcJsonAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });
        foreach (var tool in payload.GetProperty("result").GetProperty("tools").EnumerateArray())
        {
            Assert.True(tool.TryGetProperty("annotations", out var annotations), $"Tool {tool.GetProperty("name")} is missing annotations.");
            Assert.True(annotations.TryGetProperty("readOnlyHint", out _), $"Tool {tool.GetProperty("name")} is missing readOnlyHint.");
            Assert.True(annotations.TryGetProperty("destructiveHint", out _), $"Tool {tool.GetProperty("name")} is missing destructiveHint.");
            Assert.True(annotations.TryGetProperty("idempotentHint", out _), $"Tool {tool.GetProperty("name")} is missing idempotentHint.");
            Assert.True(
                annotations.TryGetProperty("openWorldHint", out var openWorld) && openWorld.ValueKind == JsonValueKind.False,
                $"Tool {tool.GetProperty("name")} must have openWorldHint false.");
        }
    }

    /// <summary>A tool call runs as the signed-in identity.</summary>
    [Fact]
    public async Task GetMyAccess_ReturnsCallerRoles()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await CallToolAsync("get_my_access", new { });

        Assert.Contains("Coordinator", payload.GetRawText());
        Assert.False(IsToolError(payload));
    }

    /// <summary>The caller's validated staff claim is returned by the self-description tool.</summary>
    [Fact]
    public async Task GetMyAccess_ReturnsCallerStaffNumber()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Coordinator], null, "U123456");

        var payload = await CallToolAsync("get_my_access", new { });
        var contentText = payload.GetProperty("result").GetProperty("content")[0]
            .GetProperty("text").GetString();
        using var content = JsonDocument.Parse(contentText!);

        Assert.Equal(
            "U123456",
            content.RootElement.GetProperty("staffId").GetString());
        Assert.False(IsToolError(payload));
    }

    /// <summary>Sequential calls without any session identifier each succeed.</summary>
    [Fact]
    public async Task StatelessCalls_NeedNoSession()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var first = await CallToolAsync("get_my_access", new { });
        var second = await CallToolAsync("get_dashboards", new { });

        Assert.True(first.TryGetProperty("result", out _));
        Assert.True(second.TryGetProperty("result", out _));
        Assert.False(IsToolError(first));
        Assert.False(IsToolError(second));
    }

    /// <summary>A capability failure surfaces as a tool error, not a transport failure.</summary>
    [Fact]
    public async Task ForbiddenCapability_SurfacesAsToolError()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await CallToolAsync(
            "propose_event", new { date = "2026-10-01", startTime = "09:00" });

        Assert.True(IsToolError(payload));
    }

    private async Task<HttpResponseMessage> PostRpcAsync(object body)
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return await client.SendAsync(request);
    }

    private async Task<JsonElement> PostRpcJsonAsync(object body)
    {
        var response = await PostRpcAsync(body);
        response.EnsureSuccessStatusCode();
        return ParseRpcPayload(await response.Content.ReadAsStringAsync());
    }

    private Task<JsonElement> CallToolAsync(string name, object arguments) =>
        PostRpcJsonAsync(new
        {
            jsonrpc = "2.0",
            id = "1",
            method = "tools/call",
            @params = new { name, arguments },
        });

    private static JsonElement ParseRpcPayload(string body)
    {
        if (body.TrimStart().StartsWith('{'))
        {
            return JsonDocument.Parse(body).RootElement;
        }

        var data = body
            .Split('\n')
            .Select(line => line.Trim())
            .LastOrDefault(line => line.StartsWith("data: "))
            ?.Substring("data: ".Length);
        Assert.False(string.IsNullOrWhiteSpace(data), "MCP response carried no data frame.");
        return JsonDocument.Parse(data!).RootElement;
    }

    private static bool IsToolError(JsonElement payload) =>
        payload.TryGetProperty("result", out var result) &&
        result.TryGetProperty("isError", out var isError) &&
        isError.ValueKind == JsonValueKind.True;
}
`````

## before — tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs — 1/1

<!-- retirement-file: {"id":58,"file":"tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs","beforeSha":"a8101b2d01956c37dbebb93442c2a1a80edbececabe029d7c44c967cd49d3fb6","afterSha":"72a694ab3cf963de1518d3d6060ae2314b5d819da3abe967a424d359372987bb","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Appointments;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Mcp.Tests;

/// <summary>Seeds booked and no-show attendee scenarios for MCP parity tests.</summary>
public static class McpScenarioSeeder
{
    /// <summary>Seeds a attendee holding one active original booking plus spare future events.</summary>
    /// <param name="factory">The MCP host providing services and identity helpers.</param>
    /// <returns>The seeded attendee and booking identifiers.</returns>
    public static async Task<(Guid AttendeeId, Guid BookingId)> GivenBookedAttendeeAsync(McpFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;

        var bookedEvent = Event.CreateImported(
            Guid.NewGuid(), new EventWindow(today.AddDays(30), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var spareEvents = new[] { new TimeOnly(11, 0), new TimeOnly(13, 0), new TimeOnly(15, 0) }
            .Select(start => Event.CreateImported(
                Guid.NewGuid(), new EventWindow(today.AddDays(31), start),
                AppointmentTypeIds.All.ToDictionary(id => id, _ => 20)))
            .ToList();

        var group = context.AttendeeGroups
            .Include(g => g.Requirements)
            .Single(g => g.Id == AttendeeGroupIds.GroundOperationsAgent);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [bookedEvent.Id, spareEvents[0].Id, spareEvents[1].Id],
            attendee.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, bookedEvent.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);

        attendee.MarkInvited();
        invite.MarkUsed();
        attendee.MarkBooked();

        context.AddRange(bookedEvent);
        context.AddRange(spareEvents);
        context.AddRange(attendee, invite, booking);
        foreach (var typeId in attendee.RequiredAppointmentTypeIds)
        {
            context.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
            bookedEvent.CapacityFor(typeId).Decrement();
        }

        await context.SaveChangesAsync();
        return (attendee.Id, booking.Id);
    }

    /// <summary>Seeds a booked attendee with one no-show appointment for recovery tests.</summary>
    /// <param name="factory">The MCP host providing services and identity helpers.</param>
    /// <returns>The seeded attendee and booking identifiers.</returns>
    public static async Task<(Guid AttendeeId, Guid BookingId)> GivenAttendeeWithNoShowAsync(McpFactory factory)
    {
        Guid attendeeId;
        Guid appointmentId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;
            var bookedEvent = Event.CreateImported(
                Guid.NewGuid(), new EventWindow(today.AddDays(-1), new TimeOnly(9, 0)),
                AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
            var spareEvents = new[]
            {
                new TimeOnly(11, 0),
                new TimeOnly(13, 0),
                new TimeOnly(15, 0),
            }
            .Select(start => Event.CreateImported(
                Guid.NewGuid(), new EventWindow(today.AddDays(2), start),
                AppointmentTypeIds.All.ToDictionary(id => id, _ => 20)))
            .ToList();
            var group = context.AttendeeGroups
                .Include(g => g.Requirements)
                .Single(g => g.Id == AttendeeGroupIds.GroundOperationsAgent);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
            var invite = Invite.CreateInitial(
                Guid.NewGuid(), attendee.Id, $"invite-{Guid.NewGuid():N}",
                DateTimeOffset.UtcNow.AddDays(1), [bookedEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
                attendee.RequiredAppointmentTypeIds, 0);
            var booking = Booking.Create(
                Guid.NewGuid(), invite, bookedEvent.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
            var appointment = BookingAppointment.Create(
                Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp);
            context.AddRange(bookedEvent);
            context.AddRange(spareEvents);
            context.AddRange(attendee, booking, appointment);
            await context.SaveChangesAsync();
            attendeeId = attendee.Id;
            appointmentId = appointment.Id;
        }

        var staffUserId = await factory.GivenStaffAsync([Role.AppointmentStaff], AppointmentTypeIds.MedicalCheckUp);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<UpdateBookingAppointmentStatusHandler>();
            var result = await handler.HandleAsync(
                new UpdateBookingAppointmentStatusCommand
                {
                    StaffUserId = staffUserId,
                    BookingAppointmentId = appointmentId,
                    Status = BookingAppointmentStatus.NoShow,
                    ExpectedVersion = 1,
                },
                CancellationToken.None);
            Assert.True(result.IsSuccess, result.Error.Message);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var bookingId = await context.BookingAppointments
                .Where(a => a.Id == appointmentId)
                .Select(a => a.BookingId)
                .SingleAsync();
            return (attendeeId, bookingId);
        }
    }

    /// <summary>Seeds one active event with a single scoped booking appointment for roster tests.</summary>
    /// <param name="factory">The MCP host providing services and identity helpers.</param>
    /// <param name="appointmentTypeId">The appointment type scoping the seeded workspace.</param>
    /// <returns>The seeded event identifier.</returns>
    public static async Task<Guid> GivenAppointmentWorkspaceAsync(McpFactory factory, Guid appointmentTypeId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;
        var eventItem = Event.CreateImported(
            Guid.NewGuid(), new EventWindow(today, new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var groupId = appointmentTypeId == AppointmentTypeIds.MedicalCheckUp
            ? AttendeeGroupIds.GroundOperationsAgent
            : AttendeeGroupIds.Pilots;
        var group = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == groupId);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);
        context.AddRange(eventItem, attendee, booking, appointment);
        await context.SaveChangesAsync();
        return eventItem.Id;
    }
}
`````

## after — tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs — 1/1

<!-- retirement-file: {"id":58,"file":"tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs","beforeSha":"a8101b2d01956c37dbebb93442c2a1a80edbececabe029d7c44c967cd49d3fb6","afterSha":"72a694ab3cf963de1518d3d6060ae2314b5d819da3abe967a424d359372987bb","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Appointments;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Mcp.Tests;

/// <summary>Seeds booked and no-show attendee scenarios for MCP parity tests.</summary>
public static class McpScenarioSeeder
{
    /// <summary>Seeds a attendee holding one active original booking plus spare future events.</summary>
    /// <param name="factory">The MCP host providing services and identity helpers.</param>
    /// <returns>The seeded attendee and booking identifiers.</returns>
    public static async Task<(Guid AttendeeId, Guid BookingId)> GivenBookedAttendeeAsync(McpFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;

        var bookedEvent = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today.AddDays(30), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var spareEvents = new[] { new TimeOnly(11, 0), new TimeOnly(13, 0), new TimeOnly(15, 0) }
            .Select(start => EventFixture.Create(
                Guid.NewGuid(), new EventWindow(today.AddDays(31), start),
                AppointmentTypeIds.All.ToDictionary(id => id, _ => 20)))
            .ToList();

        var group = context.AttendeeGroups
            .Include(g => g.Requirements)
            .Single(g => g.Id == AttendeeGroupIds.GroundOperationsAgent);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [bookedEvent.Id, spareEvents[0].Id, spareEvents[1].Id],
            attendee.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, bookedEvent.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);

        attendee.MarkInvited();
        invite.MarkUsed();
        attendee.MarkBooked();

        context.AddRange(bookedEvent);
        context.AddRange(spareEvents);
        context.AddRange(attendee, invite, booking);
        foreach (var typeId in attendee.RequiredAppointmentTypeIds)
        {
            context.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
            bookedEvent.CapacityFor(typeId).Decrement();
        }

        await context.SaveChangesAsync();
        return (attendee.Id, booking.Id);
    }

    /// <summary>Seeds a booked attendee with one no-show appointment for recovery tests.</summary>
    /// <param name="factory">The MCP host providing services and identity helpers.</param>
    /// <returns>The seeded attendee and booking identifiers.</returns>
    public static async Task<(Guid AttendeeId, Guid BookingId)> GivenAttendeeWithNoShowAsync(McpFactory factory)
    {
        Guid attendeeId;
        Guid appointmentId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;
            var bookedEvent = EventFixture.Create(
                Guid.NewGuid(), new EventWindow(today.AddDays(-1), new TimeOnly(9, 0)),
                AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
            var spareEvents = new[]
            {
                new TimeOnly(11, 0),
                new TimeOnly(13, 0),
                new TimeOnly(15, 0),
            }
            .Select(start => EventFixture.Create(
                Guid.NewGuid(), new EventWindow(today.AddDays(2), start),
                AppointmentTypeIds.All.ToDictionary(id => id, _ => 20)))
            .ToList();
            var group = context.AttendeeGroups
                .Include(g => g.Requirements)
                .Single(g => g.Id == AttendeeGroupIds.GroundOperationsAgent);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
            var invite = Invite.CreateInitial(
                Guid.NewGuid(), attendee.Id, $"invite-{Guid.NewGuid():N}",
                DateTimeOffset.UtcNow.AddDays(1), [bookedEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
                attendee.RequiredAppointmentTypeIds, 0);
            var booking = Booking.Create(
                Guid.NewGuid(), invite, bookedEvent.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
            var appointment = BookingAppointment.Create(
                Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp);
            context.AddRange(bookedEvent);
            context.AddRange(spareEvents);
            context.AddRange(attendee, booking, appointment);
            await context.SaveChangesAsync();
            attendeeId = attendee.Id;
            appointmentId = appointment.Id;
        }

        var staffUserId = await factory.GivenStaffAsync([Role.AppointmentStaff], AppointmentTypeIds.MedicalCheckUp);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<UpdateBookingAppointmentStatusHandler>();
            var result = await handler.HandleAsync(
                new UpdateBookingAppointmentStatusCommand
                {
                    StaffUserId = staffUserId,
                    BookingAppointmentId = appointmentId,
                    Status = BookingAppointmentStatus.NoShow,
                    ExpectedVersion = 1,
                },
                CancellationToken.None);
            Assert.True(result.IsSuccess, result.Error.Message);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var bookingId = await context.BookingAppointments
                .Where(a => a.Id == appointmentId)
                .Select(a => a.BookingId)
                .SingleAsync();
            return (attendeeId, bookingId);
        }
    }

    /// <summary>Seeds one active event with a single scoped booking appointment for roster tests.</summary>
    /// <param name="factory">The MCP host providing services and identity helpers.</param>
    /// <param name="appointmentTypeId">The appointment type scoping the seeded workspace.</param>
    /// <returns>The seeded event identifier.</returns>
    public static async Task<Guid> GivenAppointmentWorkspaceAsync(McpFactory factory, Guid appointmentTypeId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;
        var eventItem = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today, new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var groupId = appointmentTypeId == AppointmentTypeIds.MedicalCheckUp
            ? AttendeeGroupIds.GroundOperationsAgent
            : AttendeeGroupIds.Pilots;
        var group = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == groupId);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);
        context.AddRange(eventItem, attendee, booking, appointment);
        await context.SaveChangesAsync();
        return eventItem.Id;
    }
}
`````

## before — tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs — 1/1

<!-- retirement-file: {"id":59,"file":"tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs","beforeSha":"f98030ff70cc26d1cccaa8d8f2e5651d80413638f547f9231368e326de6c64d7","afterSha":"ba2e38c53e30245ccd52041e0664e3506c32ad0a6be941a7462a50cda6e66826","side":"before","part":1,"parts":1} -->

`````csharp
using System.Text.RegularExpressions;
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;
using EventBooking.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EventBooking.SeedData.Tests;

/// <summary>Exercises real seeded links, delivery recovery and attendee lifecycle preservation.</summary>
[Collection("seed-anchor")]
public sealed class DemoInvitationSeederTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly CapturingTransport _mail = new();
    private readonly DemoClock _clock = new();
    private ServiceProvider _services = null!;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        DemoSeedSpec.OverrideAnchor(_clock.TodayAtTransitionalLocation);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBookingInfrastructure(_postgres.GetConnectionString(),
            new TransitionalLocationOptions("Europe/London"),
            new TokenOptions("test-seed-and-api-share-this-signing-key"));
        services.AddEventBookingApplication(new AttendeePortalOptions(
            "https://demo.example.test", "Demo office", "help@example.com"));
        services.AddSingleton<IClock>(_clock);
        services.AddSingleton<IEmailTransport>(_mail);
        services.AddScoped<DemoSeeder>();
        services.AddScoped<DemoInvitationSeeder>();
        _services = services.BuildServiceProvider();
        using var scope = _services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<EventBookingDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().RunAsync(default);
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        DemoSeedSpec.OverrideAnchor(null);
        if (_services is not null) await _services.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>All five delivered HTML/text links open future options; one can book and be managed.</summary>
    [Fact]
    public async Task FreshSeedProducesFiveUsableLinksAndARealBooking()
    {
        Assert.Equal(5, await SeedAsync());
        Assert.Equal(5, _mail.Messages.Count);
        Assert.Equal(Enumerable.Range(1, 5).Select(i => $"demo-attendee-{i:000}@example.com"),
            _mail.Messages.Select(m => m.ToAddress));
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.Equal(100, await db.Attendees.CountAsync());
        Assert.Equal(5, await db.Attendees.CountAsync(c => c.Status == AttendeeStatus.Invited));
        // Journey seeds write booking rows directly without transitioning Attendee
        // status, so only the 5 newly invited attendees leave NotYetInvited.
        Assert.Equal(95, await db.Attendees.CountAsync(c => c.Status == AttendeeStatus.NotYetInvited));
        Assert.Equal(5, await db.Invites.CountAsync(i => i.Status == InviteStatus.Pending));
        Assert.Equal(5, await db.EmailLogs.CountAsync(e => e.Status == EmailStatus.Sent));
        Assert.Equal(12, await db.Events.CountAsync());
        Assert.Equal(5, await db.EventProposals.CountAsync());
        Assert.Equal(5, await db.Attendees.Where(c => c.Status == AttendeeStatus.Invited)
            .Select(c => c.AttendeeGroupId).Distinct().CountAsync());
        var view = scope.ServiceProvider.GetRequiredService<ViewInviteHandler>();
        foreach (var message in _mail.Messages.ToArray())
        {
            var token = Token(message);
            Assert.Contains($"href=\"https://demo.example.test/book/{token}\"", message.HtmlBody);
            var result = await view.HandleAsync(new ViewInviteQuery(token), default);
            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value.Options.Count);
            Assert.All(result.Value.Options, option => Assert.True(option.Date > _clock.TodayAtTransitionalLocation));
            var attendee = await db.Attendees.Include(c => c.Requirements)
                .SingleAsync(c => c.Id == message.AttendeeId);
            var invite = await db.Invites.Include(i => i.Requirements)
                .SingleAsync(i => i.Id == result.Value.InviteId);
            Assert.Equal(attendee.RequiredAppointmentTypeIds.Order(), invite.RequiredAppointmentTypeIds.Order());
            Assert.NotEqual(token, invite.TokenHash);
        }
        var first = Token(_mail.Messages[0]);
        var offered = await view.HandleAsync(new ViewInviteQuery(first), default);
        var booking = await scope.ServiceProvider.GetRequiredService<ConfirmBookingHandler>()
            .HandleAsync(new ConfirmBookingCommand(first, offered.Value.Options[0].EventId), default);
        Assert.True(booking.IsSuccess);
        Assert.Equal("Sent", booking.Value.DeliveryStatus);
        Assert.True((await scope.ServiceProvider.GetRequiredService<ViewBookingHandler>()
            .HandleAsync(new ViewBookingQuery(booking.Value.ManageToken), default)).IsSuccess);
        Assert.False((await view.HandleAsync(new ViewInviteQuery(first), default)).IsSuccess);
        Assert.Equal(EmailTemplate.BookingConfirmation, _mail.Messages[^1].Template);
    }

    /// <summary>A rerun preserves successful tokens, capacities and an already-consumed invitation.</summary>
    [Fact]
    public async Task RerunDoesNotResendOrUndoBooking()
    {
        await SeedAsync();
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var first = Token(_mail.Messages[0]);
        var offered = await scope.ServiceProvider.GetRequiredService<ViewInviteHandler>()
            .HandleAsync(new ViewInviteQuery(first), default);
        var booked = await scope.ServiceProvider.GetRequiredService<ConfirmBookingHandler>()
            .HandleAsync(new ConfirmBookingCommand(first, offered.Value.Options[0].EventId), default);
        Assert.True(booked.IsSuccess);
        var hashes = await db.Invites.AsNoTracking().OrderBy(i => i.Id).Select(i => i.TokenHash).ToListAsync();
        var capacities = await db.Events.AsNoTracking().Include(s => s.Capacities)
            .OrderBy(s => s.Id).ToListAsync();
        var before = capacities.SelectMany(s => s.Capacities.OrderBy(c => c.AppointmentTypeId))
            .Select(c => c.RemainingCapacity).ToArray();
        Assert.Equal(0, await SeedAsync());
        Assert.Equal(6, _mail.Messages.Count);
        Assert.Equal(hashes, await db.Invites.AsNoTracking().OrderBy(i => i.Id).Select(i => i.TokenHash).ToListAsync());
        var after = await db.Events.AsNoTracking().Include(s => s.Capacities)
            .OrderBy(s => s.Id).ToListAsync();
        Assert.Equal(before, after.SelectMany(s => s.Capacities.OrderBy(c => c.AppointmentTypeId))
            .Select(c => c.RemainingCapacity).ToArray());
    }

    /// <summary>Provider failure leaves a durable attempt that the next run retries once.</summary>
    [Fact]
    public async Task FailedDeliveryResumesWithoutDuplicatingSuccesses()
    {
        _mail.FailOnAttempt = 3;
        await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var failed = await db.EmailLogs.AsNoTracking().SingleAsync(e => e.Status == EmailStatus.Failed);
        var oldHash = await db.Invites.Where(i => i.Id == failed.InviteId).Select(i => i.TokenHash).SingleAsync();
        Assert.Equal(2, _mail.Messages.Count);
        _mail.FailOnAttempt = null;
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(3, await SeedAsync());
        Assert.Equal(5, _mail.Messages.Count);
        Assert.Equal(5, _mail.Messages.Select(m => m.AttendeeId).Distinct().Count());
        Assert.Equal(5, await db.Invites.CountAsync(i => i.Status == InviteStatus.Pending));
        Assert.Equal(1, await db.EmailLogs.CountAsync(e => e.Status == EmailStatus.Resolved));
        Assert.Equal(5, await db.EmailLogs.CountAsync(e => e.Status == EmailStatus.Sent));
        Assert.NotEqual(oldHash, await db.Invites.Where(i => i.Id == failed.InviteId)
            .Select(i => i.TokenHash).SingleAsync());
        Assert.Equal(0, await SeedAsync());
    }

    /// <summary>Resetting database state creates new invitations and invalidates old raw links.</summary>
    [Fact]
    public async Task ReseedCreatesFreshLinks()
    {
        await SeedAsync();
        var oldToken = Token(_mail.Messages[0]);
        using (var scope = _services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<DemoSeeder>().ReseedAsync(default);
        Assert.Equal(5, await SeedAsync());
        using var verify = _services.CreateScope();
        var view = verify.ServiceProvider.GetRequiredService<ViewInviteHandler>();
        Assert.False((await view.HandleAsync(new ViewInviteQuery(oldToken), default)).IsSuccess);
        Assert.True((await view.HandleAsync(new ViewInviteQuery(Token(_mail.Messages[5])), default)).IsSuccess);
    }

    /// <summary>Expired history is not replaced even when demo dates are moved forward.</summary>
    [Fact]
    public async Task ExpiredInvitationsArePreserved()
    {
        await SeedAsync();
        _clock.Advance(TimeSpan.FromDays(30));
        DemoSeedSpec.OverrideAnchor(_clock.TodayAtTransitionalLocation);
        Assert.Equal(0, await SeedAsync());
        Assert.Equal(5, _mail.Messages.Count);
        using var scope = _services.CreateScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<ViewInviteHandler>()
            .HandleAsync(new ViewInviteQuery(Token(_mail.Messages[0])), default)).IsSuccess);
    }

    /// <summary>Stale windows fail before issuing a misleading invitation.</summary>
    [Fact]
    public async Task StaleAnchorFailsBeforeSending()
    {
        _clock.Advance(TimeSpan.FromDays(4));
        var error = await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        Assert.Contains("--reanchor", error.Message);
        Assert.Empty(_mail.Messages);
    }

    /// <summary>A live claim is not stolen, but an expired claim can be recovered on a later run.</summary>
    [Fact]
    public async Task PendingClaimUsesExistingLease()
    {
        await SeedAsync();
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var prior = await db.EmailLogs.SingleAsync(e => e.AttendeeId == _mail.Messages[0].AttendeeId);
            var pending = EmailLog.RecordPending(Guid.NewGuid(), prior.AttendeeId,
                EmailTemplate.AttendeeInvite, _clock.UtcNow.AddSeconds(1), prior.InviteId);
            Assert.True(pending.TryClaim(_clock.UtcNow, TimeSpan.FromMinutes(5)));
            db.EmailLogs.Add(pending);
            await db.SaveChangesAsync();
        }
        var error = await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        Assert.Contains("already being delivered", error.Message);
        Assert.Equal(5, _mail.Messages.Count);
        _clock.Advance(TimeSpan.FromMinutes(6));
        Assert.Equal(1, await SeedAsync());
        Assert.Equal(6, _mail.Messages.Count);
    }

    /// <summary>A pending invitation lacking its delivery record is not reported as sent.</summary>
    [Fact]
    public async Task MissingDeliveryFailsVisibly()
    {
        await SeedAsync();
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            await db.EmailLogs.Where(e => e.AttendeeId == _mail.Messages[0].AttendeeId).ExecuteDeleteAsync();
        }
        var error = await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        Assert.Contains("no matching delivery", error.Message);
        Assert.Equal(5, _mail.Messages.Count);
    }

    /// <summary>An old failure cannot hide a successful Coordinator replacement for the same Attendee.</summary>
    [Fact]
    public async Task CoordinatorReplacementPreservesItsSuccessfulDelivery()
    {
        _mail.FailOnAttempt = 1;
        await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        _mail.FailOnAttempt = null;
        _clock.Advance(TimeSpan.FromSeconds(1));
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var attendee = await db.Attendees.SingleAsync(c => c.Email == "demo-attendee-001@example.com");
            var issued = await scope.ServiceProvider
                .GetRequiredService<EventBooking.Application.Invites.TriggerInviteHandler>()
                .HandleAsync(new EventBooking.Application.Invites.TriggerInviteCommand(
                    DemoSeedSpec.CoordinatorUserId(), attendee.Id), default);
            Assert.True(issued.IsSuccess);
            Assert.True(issued.Value.EmailSent);
        }
        var replacementToken = Token(Assert.Single(_mail.Messages));
        Assert.Equal(4, await SeedAsync());
        Assert.Equal(5, _mail.Messages.Count);
        using var verify = _services.CreateScope();
        Assert.True((await verify.ServiceProvider.GetRequiredService<ViewInviteHandler>()
            .HandleAsync(new ViewInviteQuery(replacementToken), default)).IsSuccess);
        Assert.Equal(0, await SeedAsync());
    }

    /// <summary>Unrelated outstanding work is not retried or token-rotated by demo seeding.</summary>
    [Fact]
    public async Task AnotherOutstandingTemplateRequiresCoordinatorReview()
    {
        _mail.FailOnAttempt = 1;
        await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        _mail.FailOnAttempt = null;
        string originalHash;
        Guid inviteId;
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var prior = await db.EmailLogs.SingleAsync(e => e.Status == EmailStatus.Failed);
            inviteId = prior.InviteId!.Value;
            originalHash = await db.Invites.Where(i => i.Id == inviteId).Select(i => i.TokenHash).SingleAsync();
            var other = EmailLog.RecordPending(Guid.NewGuid(), prior.AttendeeId,
                EmailTemplate.AttendeeReinvite, _clock.UtcNow.AddSeconds(1), inviteId);
            db.EmailLogs.Add(other);
            await db.SaveChangesAsync();
        }
        var error = await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        Assert.Contains("Another outstanding delivery", error.Message);
        Assert.Empty(_mail.Messages);
        using var verify = _services.CreateScope();
        var database = verify.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.Equal(originalHash, await database.Invites.Where(i => i.Id == inviteId)
            .Select(i => i.TokenHash).SingleAsync());
    }

    private async Task<int> SeedAsync()
    {
        using var scope = _services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DemoInvitationSeeder>().RunAsync(default);
    }

    private static string Token(EmailMessage message) =>
        Regex.Match(message.TextBody, @"https://demo\.example\.test/book/([^\s]+)").Groups[1].Value;

    private sealed class CapturingTransport : IEmailTransport
    {
        /// <summary>Messages accepted by the external provider boundary.</summary>
        public List<EmailMessage> Messages { get; } = [];
        /// <summary>Optional one-based provider attempt to fail.</summary>
        public int? FailOnAttempt { get; set; }
        private int _attempt;
        /// <inheritdoc/>
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (++_attempt == FailOnAttempt) throw new IOException("Test SMTP failure.");
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class DemoClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow { get; private set; } = new(2030, 1, 7, 12, 0, 0, TimeSpan.Zero);
        /// <inheritdoc/>
        public DateTimeOffset NowAtTransitionalLocation => TimeZoneInfo.ConvertTime(UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/London"));
        /// <inheritdoc/>
        public DateOnly TodayAtTransitionalLocation => DateAtTransitionalLocation(UtcNow);
        /// <inheritdoc/>
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")).DateTime);
        /// <inheritdoc/>
        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(
            instant, TimeZoneInfo.FindSystemTimeZoneById("Europe/London"));
        /// <summary>Moves the observation clock without modifying persisted data.</summary>
        public void Advance(TimeSpan elapsed) => UtcNow += elapsed;
    }
}
`````
