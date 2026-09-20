# 00b — Vocabulary edits 104 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — tests/EventBooking.Mcp.Tests/McpEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":355,"oldPath":"tests/EventBooking.Mcp.Tests/McpEndpointTests.cs","newPath":"tests/EventBooking.Mcp.Tests/McpEndpointTests.cs","beforeSha":"8e620d676694f54dccd134a01d445e6cbf84ab996579c7e9e274d46f4c69be5d","afterSha":"8f55ee27decdd71adf2deed6a9f1634288a648c312360c80548b5f50a4bf5340","side":"before","part":1,"parts":1} -->

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
        "propose_slot", "accept_proposal", "withdraw_acceptance", "withdraw_proposal",
        "slot_board", "adjust_slot_capacity", "cancel_confirmed_slot", "import_confirmed_slots",
        "list_candidates", "create_candidate", "update_candidate", "delete_candidate",
        "list_employee_groups",
        "import_candidates", "trigger_invite", "retry_candidate_email",
        "start_recovery_invite", "cancel_recovery_invite", "list_candidate_bookings",
        "cancel_candidate_booking", "get_candidate_readiness",
        "get_settings", "update_settings", "list_staff_access", "replace_staff_access_scope",
        "clear_staff_access_scope", "get_my_access",
        "get_dashboards", "slot_audit_history", "candidate_audit_history", "search_audit",
        "appointment_slots", "appointment_slot_detail", "export_appointment_roster", "update_appointment_status",
        "get_slot_operations",
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
            "propose_slot", new { date = "2026-10-01", startTime = "09:00" });

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

<!-- vocabulary-file: {"id":355,"oldPath":"tests/EventBooking.Mcp.Tests/McpEndpointTests.cs","newPath":"tests/EventBooking.Mcp.Tests/McpEndpointTests.cs","beforeSha":"8e620d676694f54dccd134a01d445e6cbf84ab996579c7e9e274d46f4c69be5d","afterSha":"8f55ee27decdd71adf2deed6a9f1634288a648c312360c80548b5f50a4bf5340","side":"after","part":1,"parts":1} -->

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

## before — tests/EventBooking.Mcp.Tests/McpFactory.cs — 1/1

<!-- vocabulary-file: {"id":356,"oldPath":"tests/EventBooking.Mcp.Tests/McpFactory.cs","newPath":"tests/EventBooking.Mcp.Tests/McpFactory.cs","beforeSha":"98f0b347aa2090c96b15150125b9b425562844247b94e867c39ff5fff60a56ed","afterSha":"cfe66d170f711531f6f565261f464578a8b27c9d30774ad604caa7c477d4fd12","side":"before","part":1,"parts":1} -->

`````csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using EventBooking.Domain.Access;
using EventBooking.Infrastructure.Email;
using EventBooking.Mcp.Tests.Fakes;
using EventBooking.Mcp.Tools;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace EventBooking.Mcp.Tests;

/// <summary>
/// Starts the real MCP host against a throwaway PostgreSQL container, with the bearer
/// token validation replaced by a test scheme so a test can say who is calling by
/// setting <see cref="SignedInAs"/>.
/// </summary>
public sealed class McpFactory : WebApplicationFactory<SlotTools>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();
    private int _staffIdSequence;

    /// <summary>The Entra object identifier every request is made as. Null means anonymous.</summary>
    public Guid? SignedInAs { get; set; }

    /// <summary>The provider-issued staff number claim, or null to omit the claim.</summary>
    public string? StaffIdClaim { get; set; } = "U999999";

    /// <summary>The untrusted <c>roles</c> claim values attached to authenticated test requests.</summary>
    public IReadOnlyCollection<string> RolesClaim { get; set; } = [];

    /// <summary>Stands in for AWS SES so MCP tools that send email don't need an AWS environment.</summary>
    public RecordingEmailTransport EmailTransport { get; } = new();

    /// <summary>Creates a staff access profile for a fresh identity.</summary>
    /// <param name="roles">The roles to grant.</param>
    /// <param name="appointmentTypeId">The shared scope, if any.</param>
    /// <param name="staffIdClaim">The fixed staff number for the request, or null to allocate one.</param>
    /// <returns>The new staff identity.</returns>
    public async Task<Guid> GivenStaffAsync(
        IReadOnlyCollection<Role> roles,
        Guid? appointmentTypeId,
        string? staffIdClaim = null)
    {
        StaffIdClaim = staffIdClaim ?? $"U{Interlocked.Increment(ref _staffIdSequence):D6}";
        RolesClaim = roles.Select(role => role.ToString()).ToList();
        var staffUserId = Guid.NewGuid();
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EventBooking.Infrastructure.Persistence.EventBookingDbContext>();
        if (roles.Contains(Role.Manager) && appointmentTypeId is not null)
        {
            var previous = await context.StaffAccessProfiles.SingleOrDefaultAsync(profile =>
                profile.IsManager && profile.AppointmentTypeId == appointmentTypeId);
            if (previous is not null)
            {
                context.StaffAccessProfiles.Remove(previous);
            }
        }

        context.StaffAccessProfiles.Add(
            StaffAccessProfile.Create(staffUserId, roles, appointmentTypeId));
        await context.SaveChangesAsync();
        return staffUserId;
    }

    /// <summary>Returns the recorded identity for the provider key, when one exists.</summary>
    /// <param name="staffUserId">The provider key to find.</param>
    /// <returns>The recorded identity, or null.</returns>
    public async Task<StaffIdentity?> FindIdentityAsync(Guid staffUserId)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EventBooking.Infrastructure.Persistence.EventBookingDbContext>();
        return await context.StaffIdentities.AsNoTracking().SingleOrDefaultAsync(
            identity => identity.StaffUserId == staffUserId);
    }

    /// <summary>Returns the access profile for the provider key, when one exists.</summary>
    /// <param name="staffUserId">The provider key to find.</param>
    /// <returns>The access profile, or null.</returns>
    public async Task<StaffAccessProfile?> FindProfileAsync(Guid staffUserId)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EventBooking.Infrastructure.Persistence.EventBookingDbContext>();
        return await context.StaffAccessProfiles.AsNoTracking().SingleOrDefaultAsync(
            profile => profile.StaffUserId == staffUserId);
    }

    /// <summary>Starts the container and migrates the throwaway database.</summary>
    /// <returns>A task tracking initialization.</returns>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EventBooking.Infrastructure.Persistence.EventBookingDbContext>();
        await context.Database.MigrateAsync();
    }

    /// <summary>Disposes the container and the host.</summary>
    /// <returns>A task tracking disposal.</returns>
    public new async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:EventBooking", _container.GetConnectionString());
        builder.UseSetting("Tokens:SigningKey", "a-test-signing-key-that-is-long-enough-here");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(this);
            services
                .AddAuthentication(TestAuthenticationHandler.Scheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.Scheme, _ => { });

            // The real transport constructs an AWS SES client that throws immediately outside an
            // AWS environment (no RegionEndpoint or ServiceURL configured) — exactly where CI runs.
            services.AddSingleton<IEmailTransport>(EmailTransport);
        });
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        McpFactory factory) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "Test";

        /// <inheritdoc />
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (factory.SignedInAs is null)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new("oid", factory.SignedInAs.Value.ToString()),
            };
            if (factory.StaffIdClaim is not null)
            {
                claims.Add(new Claim("staff_id", factory.StaffIdClaim));
            }
            foreach (var role in factory.RolesClaim)
            {
                claims.Add(new Claim("roles", role));
            }

            var identity = new ClaimsIdentity(claims, Scheme);

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}

/// <summary>Shares one MCP host across the endpoint tests.</summary>
[CollectionDefinition("mcp")]
public sealed class McpCollection : ICollectionFixture<McpFactory>;
`````

## after — tests/EventBooking.Mcp.Tests/McpFactory.cs — 1/1

<!-- vocabulary-file: {"id":356,"oldPath":"tests/EventBooking.Mcp.Tests/McpFactory.cs","newPath":"tests/EventBooking.Mcp.Tests/McpFactory.cs","beforeSha":"98f0b347aa2090c96b15150125b9b425562844247b94e867c39ff5fff60a56ed","afterSha":"cfe66d170f711531f6f565261f464578a8b27c9d30774ad604caa7c477d4fd12","side":"after","part":1,"parts":1} -->

`````csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using EventBooking.Domain.Access;
using EventBooking.Infrastructure.Email;
using EventBooking.Mcp.Tests.Fakes;
using EventBooking.Mcp.Tools;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace EventBooking.Mcp.Tests;

/// <summary>
/// Starts the real MCP host against a throwaway PostgreSQL container, with the bearer
/// token validation replaced by a test scheme so a test can say who is calling by
/// setting <see cref="SignedInAs"/>.
/// </summary>
public sealed class McpFactory : WebApplicationFactory<EventTools>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();
    private int _staffIdSequence;

    /// <summary>The Entra object identifier every request is made as. Null means anonymous.</summary>
    public Guid? SignedInAs { get; set; }

    /// <summary>The provider-issued staff number claim, or null to omit the claim.</summary>
    public string? StaffIdClaim { get; set; } = "U999999";

    /// <summary>The untrusted <c>roles</c> claim values attached to authenticated test requests.</summary>
    public IReadOnlyCollection<string> RolesClaim { get; set; } = [];

    /// <summary>Stands in for AWS SES so MCP tools that send email don't need an AWS environment.</summary>
    public RecordingEmailTransport EmailTransport { get; } = new();

    /// <summary>Creates a staff access profile for a fresh identity.</summary>
    /// <param name="roles">The roles to grant.</param>
    /// <param name="appointmentTypeId">The shared scope, if any.</param>
    /// <param name="staffIdClaim">The fixed staff number for the request, or null to allocate one.</param>
    /// <returns>The new staff identity.</returns>
    public async Task<Guid> GivenStaffAsync(
        IReadOnlyCollection<Role> roles,
        Guid? appointmentTypeId,
        string? staffIdClaim = null)
    {
        StaffIdClaim = staffIdClaim ?? $"U{Interlocked.Increment(ref _staffIdSequence):D6}";
        RolesClaim = roles.Select(role => role.ToString()).ToList();
        var staffUserId = Guid.NewGuid();
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EventBooking.Infrastructure.Persistence.EventBookingDbContext>();
        if (roles.Contains(Role.Manager) && appointmentTypeId is not null)
        {
            var previous = await context.StaffAccessProfiles.SingleOrDefaultAsync(profile =>
                profile.IsManager && profile.AppointmentTypeId == appointmentTypeId);
            if (previous is not null)
            {
                context.StaffAccessProfiles.Remove(previous);
            }
        }

        context.StaffAccessProfiles.Add(
            StaffAccessProfile.Create(staffUserId, roles, appointmentTypeId));
        await context.SaveChangesAsync();
        return staffUserId;
    }

    /// <summary>Returns the recorded identity for the provider key, when one exists.</summary>
    /// <param name="staffUserId">The provider key to find.</param>
    /// <returns>The recorded identity, or null.</returns>
    public async Task<StaffIdentity?> FindIdentityAsync(Guid staffUserId)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EventBooking.Infrastructure.Persistence.EventBookingDbContext>();
        return await context.StaffIdentities.AsNoTracking().SingleOrDefaultAsync(
            identity => identity.StaffUserId == staffUserId);
    }

    /// <summary>Returns the access profile for the provider key, when one exists.</summary>
    /// <param name="staffUserId">The provider key to find.</param>
    /// <returns>The access profile, or null.</returns>
    public async Task<StaffAccessProfile?> FindProfileAsync(Guid staffUserId)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EventBooking.Infrastructure.Persistence.EventBookingDbContext>();
        return await context.StaffAccessProfiles.AsNoTracking().SingleOrDefaultAsync(
            profile => profile.StaffUserId == staffUserId);
    }

    /// <summary>Starts the container and migrates the throwaway database.</summary>
    /// <returns>A task tracking initialization.</returns>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EventBooking.Infrastructure.Persistence.EventBookingDbContext>();
        await context.Database.MigrateAsync();
    }

    /// <summary>Disposes the container and the host.</summary>
    /// <returns>A task tracking disposal.</returns>
    public new async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:EventBooking", _container.GetConnectionString());
        builder.UseSetting("Tokens:SigningKey", "a-test-signing-key-that-is-long-enough-here");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(this);
            services
                .AddAuthentication(TestAuthenticationHandler.Scheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.Scheme, _ => { });

            // The real transport constructs an AWS SES client that throws immediately outside an
            // AWS environment (no RegionEndpoint or ServiceURL configured) — exactly where CI runs.
            services.AddSingleton<IEmailTransport>(EmailTransport);
        });
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        McpFactory factory) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "Test";

        /// <inheritdoc />
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (factory.SignedInAs is null)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new("oid", factory.SignedInAs.Value.ToString()),
            };
            if (factory.StaffIdClaim is not null)
            {
                claims.Add(new Claim("staff_id", factory.StaffIdClaim));
            }
            foreach (var role in factory.RolesClaim)
            {
                claims.Add(new Claim("roles", role));
            }

            var identity = new ClaimsIdentity(claims, Scheme);

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}

/// <summary>Shares one MCP host across the endpoint tests.</summary>
[CollectionDefinition("mcp")]
public sealed class McpCollection : ICollectionFixture<McpFactory>;
`````

## before — tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs — 1/1

<!-- vocabulary-file: {"id":357,"oldPath":"tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs","newPath":"tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs","beforeSha":"0d6ae05e8e04f2d8d13f2693d0b8ef6990826a0a4af662e477568e52c05e3e6b","afterSha":"a8101b2d01956c37dbebb93442c2a1a80edbececabe029d7c44c967cd49d3fb6","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Appointments;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Mcp.Tests;

/// <summary>Seeds booked and no-show candidate scenarios for MCP parity tests.</summary>
public static class McpScenarioSeeder
{
    /// <summary>Seeds a candidate holding one active original booking plus spare future slots.</summary>
    /// <param name="factory">The MCP host providing services and identity helpers.</param>
    /// <returns>The seeded candidate and booking identifiers.</returns>
    public static async Task<(Guid CandidateId, Guid BookingId)> GivenBookedCandidateAsync(McpFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtHeadOffice;

        var bookedSlot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(today.AddDays(30), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var spareSlots = new[] { new TimeOnly(11, 0), new TimeOnly(13, 0), new TimeOnly(15, 0) }
            .Select(start => ConfirmedSlot.CreateImported(
                Guid.NewGuid(), new SlotWindow(today.AddDays(31), start),
                AppointmentTypeIds.All.ToDictionary(id => id, _ => 20)))
            .ToList();

        var group = context.EmployeeGroups
            .Include(g => g.Requirements)
            .Single(g => g.Id == EmployeeGroupIds.GroundOperationsAgent);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [bookedSlot.Id, spareSlots[0].Id, spareSlots[1].Id],
            candidate.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, bookedSlot.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);

        candidate.MarkInvited();
        invite.MarkUsed();
        candidate.MarkBooked();

        context.AddRange(bookedSlot);
        context.AddRange(spareSlots);
        context.AddRange(candidate, invite, booking);
        foreach (var typeId in candidate.RequiredAppointmentTypeIds)
        {
            context.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
            bookedSlot.CapacityFor(typeId).Decrement();
        }

        await context.SaveChangesAsync();
        return (candidate.Id, booking.Id);
    }

    /// <summary>Seeds a booked candidate with one no-show appointment for recovery tests.</summary>
    /// <param name="factory">The MCP host providing services and identity helpers.</param>
    /// <returns>The seeded candidate and booking identifiers.</returns>
    public static async Task<(Guid CandidateId, Guid BookingId)> GivenCandidateWithNoShowAsync(McpFactory factory)
    {
        Guid candidateId;
        Guid appointmentId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtHeadOffice;
            var bookedSlot = ConfirmedSlot.CreateImported(
                Guid.NewGuid(), new SlotWindow(today.AddDays(-1), new TimeOnly(9, 0)),
                AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
            var spareSlots = new[]
            {
                new TimeOnly(11, 0),
                new TimeOnly(13, 0),
                new TimeOnly(15, 0),
            }
            .Select(start => ConfirmedSlot.CreateImported(
                Guid.NewGuid(), new SlotWindow(today.AddDays(2), start),
                AppointmentTypeIds.All.ToDictionary(id => id, _ => 20)))
            .ToList();
            var group = context.EmployeeGroups
                .Include(g => g.Requirements)
                .Single(g => g.Id == EmployeeGroupIds.GroundOperationsAgent);
            var candidate = Candidate.Create(
                Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
            var invite = Invite.CreateInitial(
                Guid.NewGuid(), candidate.Id, $"invite-{Guid.NewGuid():N}",
                DateTimeOffset.UtcNow.AddDays(1), [bookedSlot.Id, Guid.NewGuid(), Guid.NewGuid()],
                candidate.RequiredAppointmentTypeIds, 0);
            var booking = Booking.Create(
                Guid.NewGuid(), invite, bookedSlot.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
            var appointment = BookingAppointment.Create(
                Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp);
            context.AddRange(bookedSlot);
            context.AddRange(spareSlots);
            context.AddRange(candidate, booking, appointment);
            await context.SaveChangesAsync();
            candidateId = candidate.Id;
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
            return (candidateId, bookingId);
        }
    }

    /// <summary>Seeds one active slot with a single scoped booking appointment for roster tests.</summary>
    /// <param name="factory">The MCP host providing services and identity helpers.</param>
    /// <param name="appointmentTypeId">The appointment type scoping the seeded workspace.</param>
    /// <returns>The seeded confirmed slot identifier.</returns>
    public static async Task<Guid> GivenAppointmentWorkspaceAsync(McpFactory factory, Guid appointmentTypeId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtHeadOffice;
        var slot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(today, new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var groupId = appointmentTypeId == AppointmentTypeIds.MedicalCheckUp
            ? EmployeeGroupIds.GroundOperationsAgent
            : EmployeeGroupIds.Pilots;
        var group = context.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == groupId);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [slot.Id, Guid.NewGuid(), Guid.NewGuid()],
            candidate.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, slot.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);
        context.AddRange(slot, candidate, booking, appointment);
        await context.SaveChangesAsync();
        return slot.Id;
    }
}
`````

## after — tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs — 1/1

<!-- vocabulary-file: {"id":357,"oldPath":"tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs","newPath":"tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs","beforeSha":"0d6ae05e8e04f2d8d13f2693d0b8ef6990826a0a4af662e477568e52c05e3e6b","afterSha":"a8101b2d01956c37dbebb93442c2a1a80edbececabe029d7c44c967cd49d3fb6","side":"after","part":1,"parts":1} -->

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

## before — tests/EventBooking.Mcp.Tests/OperationsMcpTests.cs — 1/1

<!-- vocabulary-file: {"id":358,"oldPath":"tests/EventBooking.Mcp.Tests/OperationsMcpTests.cs","newPath":"tests/EventBooking.Mcp.Tests/OperationsMcpTests.cs","beforeSha":"d64847375175c2b5c4031dd8d2ed06b52e23ff93de0464d4ba2a699ed9f06614","afterSha":"9676cc8f346a63537330d328c42fa7970b19ecbf8c88c66d8de7f42c6e226af3","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Mcp.Tests;

[Collection("mcp")]
public sealed class OperationsMcpTests(McpFactory factory)
{
    [Fact]
    public async Task AdminSearchesOperationalAuditWithoutCandidateRows()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
        using var result = await CallResultAsync("search_audit", new { pageSize = 10 });
        Assert.Equal(JsonValueKind.Array, result.RootElement.GetProperty("rows").ValueKind);
        Assert.True(result.RootElement.TryGetProperty("nextCursor", out _));
    }

    [Fact]
    public async Task SearchRejectsMalformedTimestamp()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        var payload = await CallAsync("search_audit", new { from = "not-a-timestamp" });
        Assert.True(payload.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task ScopedStaffExportsRosterTextAndFilename()
    {
        var slotId = await McpScenarioSeeder.GivenAppointmentWorkspaceAsync(
            factory, AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        using var result = await CallResultAsync(
            "export_appointment_roster", new { confirmedSlotId = slotId });
        Assert.StartsWith("roster-drug-&-alcohol-testing-", result.RootElement.GetProperty("fileName").GetString());
        Assert.Contains("Candidate Name,Candidate Email", result.RootElement.GetProperty("csvText").GetString());
    }

    private async Task<JsonDocument> CallResultAsync(string name, object arguments)
    {
        var payload = await CallAsync(name, arguments);
        Assert.False(payload.GetProperty("result").TryGetProperty("isError", out var error) && error.GetBoolean(), payload.GetRawText());
        return JsonDocument.Parse(payload.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!);
    }

    private async Task<JsonElement> CallAsync(string name, object arguments)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = "1", method = "tools/call", @params = new { name, arguments },
            }), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await factory.CreateClient().SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var data = body.TrimStart().StartsWith('{') ? body : body.Split('\n').Select(x => x.Trim())
            .Last(x => x.StartsWith("data: "))["data: ".Length..];
        return JsonDocument.Parse(data).RootElement.Clone();
    }
}
`````

## after — tests/EventBooking.Mcp.Tests/OperationsMcpTests.cs — 1/1

<!-- vocabulary-file: {"id":358,"oldPath":"tests/EventBooking.Mcp.Tests/OperationsMcpTests.cs","newPath":"tests/EventBooking.Mcp.Tests/OperationsMcpTests.cs","beforeSha":"d64847375175c2b5c4031dd8d2ed06b52e23ff93de0464d4ba2a699ed9f06614","afterSha":"9676cc8f346a63537330d328c42fa7970b19ecbf8c88c66d8de7f42c6e226af3","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Mcp.Tests;

[Collection("mcp")]
public sealed class OperationsMcpTests(McpFactory factory)
{
    [Fact]
    public async Task AdminSearchesOperationalAuditWithoutAttendeeRows()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
        using var result = await CallResultAsync("search_audit", new { pageSize = 10 });
        Assert.Equal(JsonValueKind.Array, result.RootElement.GetProperty("rows").ValueKind);
        Assert.True(result.RootElement.TryGetProperty("nextCursor", out _));
    }

    [Fact]
    public async Task SearchRejectsMalformedTimestamp()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        var payload = await CallAsync("search_audit", new { from = "not-a-timestamp" });
        Assert.True(payload.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task ScopedStaffExportsRosterTextAndFilename()
    {
        var eventId = await McpScenarioSeeder.GivenAppointmentWorkspaceAsync(
            factory, AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        using var result = await CallResultAsync(
            "export_appointment_roster", new { eventId = eventId });
        Assert.StartsWith("roster-drug-&-alcohol-testing-", result.RootElement.GetProperty("fileName").GetString());
        Assert.Contains("Attendee Name,Attendee Email", result.RootElement.GetProperty("csvText").GetString());
    }

    private async Task<JsonDocument> CallResultAsync(string name, object arguments)
    {
        var payload = await CallAsync(name, arguments);
        Assert.False(payload.GetProperty("result").TryGetProperty("isError", out var error) && error.GetBoolean(), payload.GetRawText());
        return JsonDocument.Parse(payload.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!);
    }

    private async Task<JsonElement> CallAsync(string name, object arguments)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = "1", method = "tools/call", @params = new { name, arguments },
            }), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await factory.CreateClient().SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var data = body.TrimStart().StartsWith('{') ? body : body.Split('\n').Select(x => x.Trim())
            .Last(x => x.StartsWith("data: "))["data: ".Length..];
        return JsonDocument.Parse(data).RootElement.Clone();
    }
}
`````

## before — tests/EventBooking.Mcp.Tests/SlotMcpTests.cs — 1/1

<!-- vocabulary-file: {"id":359,"oldPath":"tests/EventBooking.Mcp.Tests/SlotMcpTests.cs","newPath":"tests/EventBooking.Mcp.Tests/EventMcpTests.cs","beforeSha":"7e7f3da9e6a617daf281e66498d3fd6b8465e426d3e9f313dfda29d1d45aac6c","afterSha":"c3f5ff56644880d3c528ab0dd72dddb2d0fb6fb4737ead5d64bc822e78fa6491","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;

namespace EventBooking.Mcp.Tests;

[Collection("mcp")]
public sealed class SlotMcpTests(McpFactory factory)
{
    [Fact]
    public async Task SlotOperationsReturnsCandidateFreeView()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
        var payload = await CallToolAsync("get_slot_operations", new { });
        Assert.False(IsToolError(payload), payload.GetRawText());
        var text = payload.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString();
        using var result = JsonDocument.Parse(text!);
        Assert.Equal(JsonValueKind.Array, result.RootElement.GetProperty("slots").ValueKind);
        Assert.DoesNotContain("candidate", result.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SlotOperationsIsDeniedWithoutCapability()
    {
        // ViewSlotOperations allows scoped managers, so the unscoped
        // AppointmentStaff profile exercises the denial path instead.
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], null);
        var payload = await CallToolAsync("get_slot_operations", new { });
        Assert.True(IsToolError(payload));
    }

    private async Task<JsonElement> CallToolAsync(string name, object arguments)
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = "1", method = "tools/call", @params = new { name, arguments },
            }), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var data = body.TrimStart().StartsWith('{') ? body : body.Split('\n').Select(x => x.Trim())
            .Last(x => x.StartsWith("data: "))["data: ".Length..];
        return JsonDocument.Parse(data).RootElement.Clone();
    }

    private static bool IsToolError(JsonElement payload) =>
        payload.GetProperty("result").TryGetProperty("isError", out var error) && error.GetBoolean();
}
`````

## after — tests/EventBooking.Mcp.Tests/EventMcpTests.cs — 1/1

<!-- vocabulary-file: {"id":359,"oldPath":"tests/EventBooking.Mcp.Tests/SlotMcpTests.cs","newPath":"tests/EventBooking.Mcp.Tests/EventMcpTests.cs","beforeSha":"7e7f3da9e6a617daf281e66498d3fd6b8465e426d3e9f313dfda29d1d45aac6c","afterSha":"c3f5ff56644880d3c528ab0dd72dddb2d0fb6fb4737ead5d64bc822e78fa6491","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;

namespace EventBooking.Mcp.Tests;

[Collection("mcp")]
public sealed class EventMcpTests(McpFactory factory)
{
    [Fact]
    public async Task EventOperationsReturnsAttendeeFreeView()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
        var payload = await CallToolAsync("get_event_operations", new { });
        Assert.False(IsToolError(payload), payload.GetRawText());
        var text = payload.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString();
        using var result = JsonDocument.Parse(text!);
        Assert.Equal(JsonValueKind.Array, result.RootElement.GetProperty("events").ValueKind);
        Assert.DoesNotContain("attendee", result.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EventOperationsIsDeniedWithoutCapability()
    {
        // ViewEventOperations allows scoped managers, so the unscoped
        // AppointmentStaff profile exercises the denial path instead.
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], null);
        var payload = await CallToolAsync("get_event_operations", new { });
        Assert.True(IsToolError(payload));
    }

    private async Task<JsonElement> CallToolAsync(string name, object arguments)
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = "1", method = "tools/call", @params = new { name, arguments },
            }), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var data = body.TrimStart().StartsWith('{') ? body : body.Split('\n').Select(x => x.Trim())
            .Last(x => x.StartsWith("data: "))["data: ".Length..];
        return JsonDocument.Parse(data).RootElement.Clone();
    }

    private static bool IsToolError(JsonElement payload) =>
        payload.GetProperty("result").TryGetProperty("isError", out var error) && error.GetBoolean();
}
`````
