# 00a — Port source 77 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs","encoding":"utf8","sha256":"0d6ae05e8e04f2d8d13f2693d0b8ef6990826a0a4af662e477568e52c05e3e6b","parts":1,"part":1} -->

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

## tests/EventBooking.Mcp.Tests/OperationsMcpTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Mcp.Tests/OperationsMcpTests.cs","encoding":"utf8","sha256":"d64847375175c2b5c4031dd8d2ed06b52e23ff93de0464d4ba2a699ed9f06614","parts":1,"part":1} -->

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

## tests/EventBooking.Mcp.Tests/SlotMcpTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Mcp.Tests/SlotMcpTests.cs","encoding":"utf8","sha256":"7e7f3da9e6a617daf281e66498d3fd6b8465e426d3e9f313dfda29d1d45aac6c","parts":1,"part":1} -->

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

## tests/EventBooking.Mcp.Tests/StaffAccessMcpTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Mcp.Tests/StaffAccessMcpTests.cs","encoding":"utf8","sha256":"49f5c14abf64f4acde23b4e2860f94d2e776e69aa1270ffa9b6bb372be04b44b","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Mcp.Tests;

/// <summary>Covers the staff-number administration contract exposed by MCP.</summary>
[Collection("mcp")]
public sealed class StaffAccessMcpTests(McpFactory factory)
{
    /// <summary>Listing access emits a scalar staff number rather than a value-object shape.</summary>
    [Fact]
    public async Task ListStaffAccess_ReturnsScalarStaffNumber()
    {
        var admin = await SignInAdminAsync("U345678");

        var result = await CallToolResultAsync("list_staff_access", new { });
        var profile = result.EnumerateArray().Single(item =>
            item.GetProperty("staffUserId").GetGuid() == admin);

        Assert.Equal(JsonValueKind.String, profile.GetProperty("staffId").ValueKind);
        Assert.Equal("U345678", profile.GetProperty("staffId").GetString());
    }

    /// <summary>An observed staff number selects the profile whose scope is replaced.</summary>
    [Fact]
    public async Task ReplaceStaffAccessScope_ResolvesCanonicalStaffNumber()
    {
        var target = await factory.GivenStaffAsync([Role.Manager], null, "u456789");
        factory.SignedInAs = target;
        await CallToolResultAsync("get_my_access", new { });
        await SignInAdminAsync("U345679");

        var result = await CallToolResultAsync("replace_staff_access_scope", new
        {
            staffId = "u456789",
            historicStaffUserId = (Guid?)null,
            appointmentTypeId = AppointmentTypeIds.UniformFitting,
            expectedVersion = 1L,
        });

        Assert.Equal("U456789", result.GetProperty("profile").GetProperty("staffId").GetString());
        Assert.Contains(Role.Manager, (await factory.FindProfileAsync(target))!.Roles);
        Assert.Equal(
            AppointmentTypeIds.UniformFitting,
            (await factory.FindProfileAsync(target))!.AppointmentTypeId);
    }

    /// <summary>An observed staff number selects the profile whose scope is cleared.</summary>
    [Fact]
    public async Task ClearStaffAccessScope_ResolvesStaffNumber()
    {
        var target = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.UniformFitting, "U567890");
        factory.SignedInAs = target;
        await CallToolResultAsync("get_my_access", new { });
        await SignInAdminAsync("U345680");

        var payload = await CallToolAsync("clear_staff_access_scope", new
        {
            staffId = "U567890",
            historicStaffUserId = (Guid?)null,
            expectedVersion = 1L,
        });

        Assert.False(IsToolError(payload), payload.GetRawText());
        var profile = (await factory.FindProfileAsync(target))!;
        Assert.Null(profile.AppointmentTypeId);
        Assert.Equal([Role.AppointmentStaff], profile.Roles);
    }

    /// <summary>A provider key remains usable only for a profile with no observed staff number.</summary>
    [Fact]
    public async Task ReplaceStaffAccessScope_AllowsHistoricProviderKeyFallback()
    {
        var target = await factory.GivenStaffAsync([Role.Manager], null, "U567891");
        await SignInAdminAsync("U345681");

        var result = await CallToolResultAsync("replace_staff_access_scope", new
        {
            staffId = (string?)null,
            historicStaffUserId = target,
            appointmentTypeId = AppointmentTypeIds.UniformFitting,
            expectedVersion = 1L,
        });

        Assert.Equal(target, result.GetProperty("profile").GetProperty("staffUserId").GetGuid());
        Assert.Equal(2, (await factory.FindProfileAsync(target))!.Version);
    }

    /// <summary>An observed profile can no longer be selected through the historic fallback.</summary>
    [Fact]
    public async Task ReplaceStaffAccessScope_RejectsHistoricFallbackAfterStaffNumberObserved()
    {
        var target = await factory.GivenStaffAsync([Role.Manager], null, "U567892");
        factory.SignedInAs = target;
        await CallToolResultAsync("get_my_access", new { });
        await SignInAdminAsync("U345684");

        var payload = await CallToolAsync("replace_staff_access_scope", new
        {
            staffId = (string?)null,
            historicStaffUserId = target,
            appointmentTypeId = AppointmentTypeIds.UniformFitting,
            expectedVersion = 1L,
        });

        Assert.True(IsToolError(payload));
        Assert.Equal(1, (await factory.FindProfileAsync(target))!.Version);
    }

    /// <summary>Exactly one target selector must be supplied.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task ReplaceStaffAccessScope_RequiresExactlyOneSelector(
        bool hasStaffId,
        bool hasHistoricKey)
    {
        await SignInAdminAsync(hasStaffId ? "U345682" : "U345683");

        var payload = await CallToolAsync("replace_staff_access_scope", new
        {
            staffId = hasStaffId ? "U678901" : null,
            historicStaffUserId = hasHistoricKey ? Guid.NewGuid() : (Guid?)null,
            appointmentTypeId = (Guid?)null,
            expectedVersion = 1L,
        });

        Assert.True(IsToolError(payload));
    }

    private async Task<Guid> SignInAdminAsync(string staffId)
    {
        var admin = await factory.GivenStaffAsync([Role.Admin], null, staffId);
        factory.SignedInAs = admin;
        return admin;
    }

    private async Task FirstSightAsync(Guid staffUserId, string staffId)
    {
        factory.SignedInAs = staffUserId;
        factory.StaffIdClaim = staffId;
        factory.RolesClaim = [];
        var response = await PostRpcAsync(
            new { jsonrpc = "2.0", id = "1", method = "tools/list" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<JsonElement> CallToolResultAsync(string name, object arguments)
    {
        var payload = await CallToolAsync(name, arguments);
        Assert.False(IsToolError(payload), payload.GetRawText());
        var text = payload.GetProperty("result").GetProperty("content")[0]
            .GetProperty("text").GetString();
        using var document = JsonDocument.Parse(text!);
        return document.RootElement.Clone();
    }

    private Task<JsonElement> CallToolAsync(string name, object arguments) =>
        PostRpcJsonAsync(new
        {
            jsonrpc = "2.0",
            id = "1",
            method = "tools/call",
            @params = new { name, arguments },
        });

    private async Task<JsonElement> PostRpcJsonAsync(object body)
    {
        var response = await PostRpcAsync(body);
        response.EnsureSuccessStatusCode();
        return ParseRpcPayload(await response.Content.ReadAsStringAsync());
    }

    private async Task<HttpResponseMessage> PostRpcAsync(object body)
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return await client.SendAsync(request);
    }

    private static JsonElement ParseRpcPayload(string body)
    {
        if (body.TrimStart().StartsWith('{'))
        {
            return JsonDocument.Parse(body).RootElement.Clone();
        }

        var data = body.Split('\n').Select(line => line.Trim())
            .Last(line => line.StartsWith("data: "))["data: ".Length..];
        return JsonDocument.Parse(data).RootElement.Clone();
    }

    private static bool IsToolError(JsonElement payload) =>
        payload.TryGetProperty("result", out var result) &&
        result.TryGetProperty("isError", out var isError) &&
        isError.ValueKind == JsonValueKind.True;
}
`````

## tests/EventBooking.Mcp.Tests/StaffIdentityMcpTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Mcp.Tests/StaffIdentityMcpTests.cs","encoding":"utf8","sha256":"4f3c7387623185197b6486d30b3e3d8c09e43cab6001674237351f68652555e8","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EventBooking.Mcp.Tests;

/// <summary>Covers identity recording at the authenticated MCP boundary.</summary>
[Collection("mcp")]
public sealed class StaffIdentityMcpTests(McpFactory factory)
{
    /// <summary>A valid first sighting is recorded before an unassigned caller is forbidden.</summary>
    [Fact]
    public async Task ValidFirstSighting_IsRecordedBeforeAuthorization()
    {
        var originalStaffUserId = factory.SignedInAs;
        var originalStaffIdClaim = factory.StaffIdClaim;
        var staffUserId = Guid.NewGuid();
        try
        {
            factory.SignedInAs = staffUserId;
            factory.StaffIdClaim = "u234567";

            var response = await PostToolsListAsync();

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var identity = await factory.FindIdentityAsync(staffUserId);
            Assert.Equal("U234567", identity?.StaffId.Value);
        }
        finally
        {
            factory.SignedInAs = originalStaffUserId;
            factory.StaffIdClaim = originalStaffIdClaim;
        }
    }

    /// <summary>An absent or malformed claim is never recorded.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("not-a-staff-id")]
    public async Task InvalidFirstSighting_IsNotRecorded(string? staffIdClaim)
    {
        var originalStaffUserId = factory.SignedInAs;
        var originalStaffIdClaim = factory.StaffIdClaim;
        var staffUserId = Guid.NewGuid();
        try
        {
            factory.SignedInAs = staffUserId;
            factory.StaffIdClaim = staffIdClaim;

            var response = await PostToolsListAsync();

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Null(await factory.FindIdentityAsync(staffUserId));
        }
        finally
        {
            factory.SignedInAs = originalStaffUserId;
            factory.StaffIdClaim = originalStaffIdClaim;
        }
    }

    private async Task<HttpResponseMessage> PostToolsListAsync()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    id = "1",
                    method = "tools/list",
                }),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return await client.SendAsync(request);
    }
}
`````

## tests/EventBooking.SeedData.Tests/AppointmentStaffDemoSeedTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.SeedData.Tests/AppointmentStaffDemoSeedTests.cs","encoding":"utf8","sha256":"86f7ca04132d3dd2d72871c37ed44916bbdd5110a88ff4384e69443f086864b1","parts":1,"part":1} -->

`````csharp
using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>Verifies the demo AppointmentStaff identity and application profile stay aligned.</summary>
public sealed class AppointmentStaffDemoSeedTests
{
    private static readonly Guid AppointmentStaffId =
        Guid.Parse("dc5f9a90-7f54-46d1-8603-d4c317f47226");

    /// <summary>Verifies one AppointmentStaff-only profile is scoped to Uniform Fitting.</summary>
    [Fact]
    public void SeedContainsOneScopedAppointmentStaffProfile()
    {
        var profile = Assert.Single(DemoSeedSpec.Staff(), value =>
            value.Roles.SequenceEqual([Role.AppointmentStaff]));

        Assert.Equal(AppointmentStaffId, profile.UserId);
        Assert.Equal(AppointmentTypeIds.UniformFitting, profile.AppointmentTypeId);
    }

    /// <summary>Verifies Keycloak supplies the fixed identity and AppointmentStaff role.</summary>
    [Fact]
    public void LocalRealmContainsTheIdentityWithAppointmentStaffRole()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            RepoFile("deploy/keycloak/realm-export.json")));
        var user = Assert.Single(
            document.RootElement.GetProperty("users").EnumerateArray(),
            value => value.GetProperty("username").GetString() == "appointment.staff");

        Assert.Equal(AppointmentStaffId.ToString(), user.GetProperty("id").GetString());
        Assert.Equal(
            [Role.AppointmentStaff.ToString()],
            user.GetProperty("realmRoles").EnumerateArray()
                .Select(value => value.GetString()!).ToArray());
    }

    /// <summary>Verifies all local identities still have unique ids and usernames.</summary>
    [Fact]
    public void LocalRealmIdentityKeysRemainUnique()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            RepoFile("deploy/keycloak/realm-export.json")));
        var users = document.RootElement.GetProperty("users").EnumerateArray().ToList();

        Assert.Equal(users.Count, users.Select(user => user.GetProperty("id").GetString()).Distinct().Count());
        Assert.Equal(users.Count, users.Select(user => user.GetProperty("username").GetString()).Distinct().Count());
    }

    private static string RepoFile(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "EventBooking.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, relativePath);
    }
}
`````

## tests/EventBooking.SeedData.Tests/DemoEmailOptionsTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.SeedData.Tests/DemoEmailOptionsTests.cs","encoding":"utf8","sha256":"e20af69a6b684713502a73f06debd5fdeab763f326fb3c6f4988c2aaf8dbfa13","parts":1,"part":1} -->

`````csharp
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Tokens;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>Checks configuration needed for demo links and SMTP delivery.</summary>
public sealed class DemoEmailOptionsTests
{
    /// <summary>Default tokens are readable with the local API's configured key.</summary>
    [Fact]
    public void DefaultsMatchLocalApiAndMailpit()
    {
        var options = DemoEmailOptions.From(_ => null);
        var seed = new HmacTokenService(options.Tokens);
        var api = new HmacTokenService(new TokenOptions(
            "a-local-signing-key-that-is-at-least-32-characters"));
        var id = Guid.NewGuid();
        Assert.True(api.TryRead(seed.Issue(id).Token, out var read));
        Assert.Equal(id, read);
        Assert.Equal("http://localhost:5002", options.Portal.BaseUrl);
        Assert.Equal("localhost", options.Smtp.Host);
        Assert.Equal(1025, options.Smtp.Port);
        Assert.Equal(EmailProvider.Smtp, options.Sender.Provider);
        Assert.Equal("Europe/London", options.HeadOffice.TimeZoneId);
    }

    /// <summary>Explicit deployment settings drive usable links and email transport.</summary>
    [Fact]
    public void OverridesUseConfiguredKeyAndNormalizePortal()
    {
        var values = Complete();
        values["Portal__BaseUrl"] = "https://demo.example.test/portal/";
        values["Email__Smtp__Port"] = "2525";
        values["Email__FromAddress"] = "demo@example.com";
        values["Email__FromName"] = "Demo recruitment";
        values["HeadOffice__Address"] = "Demo office";
        values["Portal__CoordinatorContact"] = "help@example.com";
        values["HeadOffice__TimeZoneId"] = "UTC";
        var options = DemoEmailOptions.From(values.GetValueOrDefault);
        var token = new HmacTokenService(options.Tokens).Issue(Guid.NewGuid()).Token;
        var api = new HmacTokenService(new TokenOptions(values["Tokens__SigningKey"]!));
        Assert.True(api.TryRead(token, out _));
        Assert.Equal("https://demo.example.test/portal", options.Portal.BaseUrl);
        Assert.Equal("mailpit", options.Smtp.Host);
        Assert.Equal(2525, options.Smtp.Port);
        Assert.Equal("demo@example.com", options.Sender.FromAddress);
        Assert.Equal("Demo recruitment", options.Sender.FromName);
        Assert.Equal("Demo office", options.Portal.HeadOfficeAddress);
        Assert.Equal("help@example.com", options.Portal.CoordinatorContact);
        Assert.Equal("UTC", options.HeadOffice.TimeZoneId);
        Assert.DoesNotContain(values["Tokens__SigningKey"]!, options.ToString());
    }

    /// <summary>Non-local links never fall back to the local API's development secret or SMTP host.</summary>
    [Theory]
    [InlineData("Tokens__SigningKey")]
    [InlineData("Email__Smtp__Host")]
    public void NonLocalPortalRequiresExplicitSetting(string missing)
    {
        var values = Complete();
        values.Remove(missing);
        var error = Assert.Throws<SeedException>(() =>
            DemoEmailOptions.From(values.GetValueOrDefault));
        Assert.Contains(missing, error.Message);
    }

    /// <summary>Invalid values fail by setting name without exposing the supplied value.</summary>
    [Theory]
    [InlineData("Portal__BaseUrl", "not-a-url")]
    [InlineData("Portal__BaseUrl", "ftp://demo.example.test")]
    [InlineData("Portal__BaseUrl", "https://user:secret@demo.example.test")]
    [InlineData("Portal__BaseUrl", "https://demo.example.test?secret=x")]
    [InlineData("Portal__BaseUrl", "https://demo.example.test#fragment")]
    [InlineData("Tokens__SigningKey", "short-secret")]
    [InlineData("Email__Smtp__Port", "0")]
    [InlineData("Email__Smtp__Port", "65536")]
    [InlineData("Email__Smtp__Port", "invalid-port")]
    [InlineData("Email__Smtp__Host", " ")]
    [InlineData("Email__FromAddress", "not-an-email")]
    [InlineData("HeadOffice__TimeZoneId", "missing/timezone")]
    public void InvalidConfigurationFailsWithoutValueDisclosure(string key, string value)
    {
        var values = Complete();
        values[key] = value;
        var error = Assert.Throws<SeedException>(() =>
            DemoEmailOptions.From(values.GetValueOrDefault));
        Assert.Contains(key, error.Message);
        if (!string.IsNullOrWhiteSpace(value) && value.Length > 1)
            Assert.DoesNotContain(value, error.Message);
    }

    private static Dictionary<string, string?> Complete() => new()
    {
        ["Portal__BaseUrl"] = "https://demo.example.test",
        ["Tokens__SigningKey"] = "a-test-signing-key-with-at-least-32-characters",
        ["Email__Smtp__Host"] = "mailpit",
    };
}
`````

## tests/EventBooking.SeedData.Tests/DemoInvitationHostTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.SeedData.Tests/DemoInvitationHostTests.cs","encoding":"utf8","sha256":"91200f314edcdc05573cb4404522f8eeb77a4abf534a7e7f1ff7086abc284404","parts":1,"part":1} -->

`````csharp
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using EventBooking.Application;
using EventBooking.Application.Bookings;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EventBooking.SeedData.Tests;

/// <summary>Tests real seed-process configuration, exit codes and MIME delivery.</summary>
public sealed class DemoInvitationHostTests : IAsyncLifetime
{
    private const string SigningKey = "host-test-signing-key-at-least-32-characters";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly LoopbackSmtpReceiver _smtp = new();

    /// <inheritdoc/>
    public Task InitializeAsync() => _postgres.StartAsync();
    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await _smtp.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>The console sends clickable HTML/text emails and a second invocation sends none.</summary>
    [Fact]
    public async Task ConsoleSeedSendsUsableMailAndRerunDoesNotDuplicate()
    {
        var first = await RunAsync(false);
        Assert.True(first.ExitCode == 0, first.Output);
        Assert.Equal(5, _smtp.Messages.Count);
        using var services = VerificationServices();
        using var scope = services.CreateScope();
        var view = scope.ServiceProvider.GetRequiredService<ViewInviteHandler>();
        foreach (var message in _smtp.Messages)
        {
            Assert.NotNull(message.TextBody);
            Assert.NotNull(message.HtmlBody);
            var token = Regex.Match(message.TextBody,
                @"https://host-demo\.example\.test/book/([^\s]+)").Groups[1].Value;
            Assert.NotEmpty(token);
            Assert.Contains($"href=\"https://host-demo.example.test/book/{token}\"", message.HtmlBody);
            var result = await view.HandleAsync(new ViewInviteQuery(token), default);
            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value.Options.Count);
            Assert.DoesNotContain(token, first.Output);
        }
        Assert.DoesNotContain(SigningKey, first.Output);
        var second = await RunAsync(false);
        Assert.True(second.ExitCode == 0, second.Output);
        Assert.Equal(5, _smtp.Messages.Count);
    }

    /// <summary>Migration-only bypasses even invalid email settings and creates no demo Candidates.</summary>
    [Fact]
    public async Task MigrationOnlyDoesNotReadEmailConfiguration()
    {
        var result = await RunAsync(true, new Dictionary<string, string?>
        {
            ["Tokens__SigningKey"] = "invalid",
            ["Portal__BaseUrl"] = "invalid",
            ["Email__Smtp__Port"] = "invalid",
        });
        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Empty(_smtp.Messages);
        using var services = VerificationServices();
        using var scope = services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.Equal(0, await database.Candidates.CountAsync());
        Assert.NotEmpty(await database.Database.GetAppliedMigrationsAsync());
    }

    /// <summary>SMTP rejection yields a failed durable delivery and a nonzero process exit.</summary>
    [Fact]
    public async Task ConsoleFailureCanResumeAfterSmtpRecovery()
    {
        _smtp.RejectMessages = true;
        var failed = await RunAsync(false);
        Assert.Equal(2, failed.ExitCode);
        Assert.Contains("delivery failed", failed.Output);
        Assert.Empty(_smtp.Messages);
        using (var services = VerificationServices())
        using (var scope = services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            Assert.Equal(1, await database.EmailLogs.CountAsync(e => e.Status == EmailStatus.Failed));
        }
        _smtp.RejectMessages = false;
        var resumed = await RunAsync(false);
        Assert.True(resumed.ExitCode == 0, resumed.Output);
        Assert.Equal(5, _smtp.Messages.Count);
    }

    private ServiceProvider VerificationServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBookingInfrastructure(_postgres.GetConnectionString(),
            new HeadOfficeOptions("Europe/London"), new TokenOptions(SigningKey));
        services.AddEventBookingApplication(new CandidatePortalOptions(
            "https://host-demo.example.test", "Demo office", "help@example.com"));
        return services.BuildServiceProvider();
    }

    private async Task<(int ExitCode, string Output)> RunAsync(
        bool skipSeed, Dictionary<string, string?>? overrides = null)
    {
        var root = RepoRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "run", "--no-build", "--configuration", configuration,
            "--project", "src/EventBooking.SeedData", "--", _postgres.GetConnectionString() })
            start.ArgumentList.Add(argument);
        // Never inherit developer Keycloak credentials or an external SMTP destination.
        foreach (var key in start.Environment.Keys.Where(key =>
            key.StartsWith("Keycloak__", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("Email__", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("Tokens__", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("Portal__", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("HeadOffice__", StringComparison.OrdinalIgnoreCase)).ToArray())
            start.Environment.Remove(key);
        start.Environment["Tokens__SigningKey"] = SigningKey;
        start.Environment["Portal__BaseUrl"] = "https://host-demo.example.test/";
        start.Environment["Email__Smtp__Host"] = "127.0.0.1";
        start.Environment["Email__Smtp__Port"] = _smtp.Port.ToString(CultureInfo.InvariantCulture);
        if (skipSeed) start.ArgumentList.Add("--skip-seed");
        else
        {
            start.ArgumentList.Add("--reanchor");
            start.ArgumentList.Add(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")).DateTime)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        if (overrides is not null)
            foreach (var (key, value) in overrides) start.Environment[key] = value;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Seed did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException("Seed process exceeded 90 seconds.");
        }
        return (process.ExitCode, await stdout + await stderr);
    }

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EventBooking.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Cannot locate repository root.");
    }
}
`````

## tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs","encoding":"utf8","sha256":"db1a4cd08679cf3af5b6250b3227d13aaefeb786b0a003b589175e80e7707256","parts":1,"part":1} -->

`````csharp
using System.Text.RegularExpressions;
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Candidates;
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

/// <summary>Exercises real seeded links, delivery recovery and candidate lifecycle preservation.</summary>
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
        DemoSeedSpec.OverrideAnchor(_clock.TodayAtHeadOffice);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBookingInfrastructure(_postgres.GetConnectionString(),
            new HeadOfficeOptions("Europe/London"),
            new TokenOptions("test-seed-and-api-share-this-signing-key"));
        services.AddEventBookingApplication(new CandidatePortalOptions(
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
        Assert.Equal(Enumerable.Range(1, 5).Select(i => $"demo-candidate-{i:000}@example.com"),
            _mail.Messages.Select(m => m.ToAddress));
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.Equal(100, await db.Candidates.CountAsync());
        Assert.Equal(5, await db.Candidates.CountAsync(c => c.Status == CandidateStatus.Invited));
        // Journey seeds write booking rows directly without transitioning Candidate
        // status, so only the 5 newly invited candidates leave NotYetInvited.
        Assert.Equal(95, await db.Candidates.CountAsync(c => c.Status == CandidateStatus.NotYetInvited));
        Assert.Equal(5, await db.Invites.CountAsync(i => i.Status == InviteStatus.Pending));
        Assert.Equal(5, await db.EmailLogs.CountAsync(e => e.Status == EmailStatus.Sent));
        Assert.Equal(12, await db.ConfirmedSlots.CountAsync());
        Assert.Equal(5, await db.SlotProposals.CountAsync());
        Assert.Equal(5, await db.Candidates.Where(c => c.Status == CandidateStatus.Invited)
            .Select(c => c.EmployeeGroupId).Distinct().CountAsync());
        var view = scope.ServiceProvider.GetRequiredService<ViewInviteHandler>();
        foreach (var message in _mail.Messages.ToArray())
        {
            var token = Token(message);
            Assert.Contains($"href=\"https://demo.example.test/book/{token}\"", message.HtmlBody);
            var result = await view.HandleAsync(new ViewInviteQuery(token), default);
            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value.Options.Count);
            Assert.All(result.Value.Options, option => Assert.True(option.Date > _clock.TodayAtHeadOffice));
            var candidate = await db.Candidates.Include(c => c.Requirements)
                .SingleAsync(c => c.Id == message.CandidateId);
            var invite = await db.Invites.Include(i => i.Requirements)
                .SingleAsync(i => i.Id == result.Value.InviteId);
            Assert.Equal(candidate.RequiredAppointmentTypeIds.Order(), invite.RequiredAppointmentTypeIds.Order());
            Assert.NotEqual(token, invite.TokenHash);
        }
        var first = Token(_mail.Messages[0]);
        var offered = await view.HandleAsync(new ViewInviteQuery(first), default);
        var booking = await scope.ServiceProvider.GetRequiredService<ConfirmBookingHandler>()
            .HandleAsync(new ConfirmBookingCommand(first, offered.Value.Options[0].ConfirmedSlotId), default);
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
            .HandleAsync(new ConfirmBookingCommand(first, offered.Value.Options[0].ConfirmedSlotId), default);
        Assert.True(booked.IsSuccess);
        var hashes = await db.Invites.AsNoTracking().OrderBy(i => i.Id).Select(i => i.TokenHash).ToListAsync();
        var capacities = await db.ConfirmedSlots.AsNoTracking().Include(s => s.Capacities)
            .OrderBy(s => s.Id).ToListAsync();
        var before = capacities.SelectMany(s => s.Capacities.OrderBy(c => c.AppointmentTypeId))
            .Select(c => c.RemainingCapacity).ToArray();
        Assert.Equal(0, await SeedAsync());
        Assert.Equal(6, _mail.Messages.Count);
        Assert.Equal(hashes, await db.Invites.AsNoTracking().OrderBy(i => i.Id).Select(i => i.TokenHash).ToListAsync());
        var after = await db.ConfirmedSlots.AsNoTracking().Include(s => s.Capacities)
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
        Assert.Equal(5, _mail.Messages.Select(m => m.CandidateId).Distinct().Count());
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
        DemoSeedSpec.OverrideAnchor(_clock.TodayAtHeadOffice);
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
            var prior = await db.EmailLogs.SingleAsync(e => e.CandidateId == _mail.Messages[0].CandidateId);
            var pending = EmailLog.RecordPending(Guid.NewGuid(), prior.CandidateId,
                EmailTemplate.CandidateInvite, _clock.UtcNow.AddSeconds(1), prior.InviteId);
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
            await db.EmailLogs.Where(e => e.CandidateId == _mail.Messages[0].CandidateId).ExecuteDeleteAsync();
        }
        var error = await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        Assert.Contains("no matching delivery", error.Message);
        Assert.Equal(5, _mail.Messages.Count);
    }

    /// <summary>An old failure cannot hide a successful Coordinator replacement for the same Candidate.</summary>
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
            var candidate = await db.Candidates.SingleAsync(c => c.Email == "demo-candidate-001@example.com");
            var issued = await scope.ServiceProvider
                .GetRequiredService<EventBooking.Application.Invites.TriggerInviteHandler>()
                .HandleAsync(new EventBooking.Application.Invites.TriggerInviteCommand(
                    DemoSeedSpec.CoordinatorUserId(), candidate.Id), default);
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
            var other = EmailLog.RecordPending(Guid.NewGuid(), prior.CandidateId,
                EmailTemplate.CandidateReinvite, _clock.UtcNow.AddSeconds(1), inviteId);
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
        public DateTimeOffset NowAtHeadOffice => TimeZoneInfo.ConvertTime(UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/London"));
        /// <inheritdoc/>
        public DateOnly TodayAtHeadOffice => DateAtHeadOffice(UtcNow);
        /// <inheritdoc/>
        public DateOnly DateAtHeadOffice(DateTimeOffset instant) => DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")).DateTime);
        /// <inheritdoc/>
        public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(
            instant, TimeZoneInfo.FindSystemTimeZoneById("Europe/London"));
        /// <summary>Moves the observation clock without modifying persisted data.</summary>
        public void Advance(TimeSpan elapsed) => UtcNow += elapsed;
    }
}
`````

## tests/EventBooking.SeedData.Tests/DemoSeedSpecTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.SeedData.Tests/DemoSeedSpecTests.cs","encoding":"utf8","sha256":"06e73effe0d8008bbe9be5e89d13aa13f72d8fab8c4b277f1ac1902348bfcdf4","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>
/// Verifies the shape and relationships of the embedded demo dataset.
/// </summary>
public sealed class DemoSeedSpecTests
{
    /// <summary>
    /// Verifies that the demo dataset provides three distinct agreed slot windows with capacity
    /// for all three appointment types.
    /// </summary>
    [Fact]
    public void AgreedSlots_AreThreeWindowsWithEightTwelveSix()
    {
        var slots = DemoSeedSpec.AgreedSlots();

        Assert.Equal(3, slots.Count);
        Assert.Equal(3, slots.Select(s => (s.Date, s.StartTime)).Distinct().Count());
        foreach (var slot in slots)
        {
            Assert.Equal(8, slot.DatHeadcount);
            Assert.Equal(12, slot.MedHeadcount);
            Assert.Equal(6, slot.UniHeadcount);
        }
    }

    /// <summary>
    /// Verifies that every open slot proposal is after the dataset anchor and initially records
    /// headcount for exactly one appointment type.
    /// </summary>
    [Fact]
    public void OpenProposals_AreFiveWindowsWithOneHeadcountEach()
    {
        var anchor = DemoSeedSpec.AnchorDate();

        var proposals = DemoSeedSpec.OpenProposals();

        Assert.Equal(5, proposals.Count);
        Assert.Equal(5, proposals.Select(p => (p.Date, p.StartTime)).Distinct().Count());
        foreach (var proposal in proposals)
        {
            Assert.True(proposal.Date > anchor);
            var filled = new[] { proposal.DatHeadcount, proposal.MedHeadcount, proposal.UniHeadcount }
                .Count(h => h is not null);
            Assert.Equal(1, filled);
        }
    }

    /// <summary>
    /// Verifies that seeded staff assignments match the users and role scopes in the Keycloak
    /// demo realm.
    /// </summary>
    [Fact]
    public void Staff_MatchesTheKeycloakRealmUsers()
    {
        var staff = DemoSeedSpec.Staff();

        Assert.Equal(6, staff.Count);
        Assert.Single(staff, s => s.Roles.SequenceEqual([Role.Admin]));
        Assert.Equal(3, staff.Count(s => s.Roles.Contains(Role.Manager)));
        Assert.Single(staff, s => s.Roles.SequenceEqual([Role.Coordinator]));
        Assert.Single(staff, s => s.Roles.SequenceEqual([Role.AppointmentStaff]));
        Assert.All(
            staff.Where(s => s.Roles.Contains(Role.Manager)),
            s => Assert.NotNull(s.AppointmentTypeId));
        Assert.Equal(
            staff.Single(s => s.Roles.SequenceEqual([Role.Coordinator])).UserId,
            DemoSeedSpec.CoordinatorUserId());
        Assert.Equal(6, staff.Select(s => s.StaffId).Distinct().Count());
        Assert.All(staff, s => Assert.True(StaffId.TryParse(s.StaffId.Value, out _)));
    }

    /// <summary>
    /// Verifies the deterministic candidate journey mix across all five employee groups.
    /// </summary>
    [Fact]
    public void Candidates_CoverEveryGroupAndJourney()
    {
        var candidates = DemoSeedSpec.Candidates();

        Assert.Equal(100, candidates.Count);
        Assert.Equal(100, candidates.Select(c => c.Email).Distinct().Count());
        Assert.All(candidates, c => Assert.False(string.IsNullOrWhiteSpace(c.Name)));
        Assert.All(
            candidates,
            c => Assert.Contains(
                c.EmployeeGroupCode,
                new[]
                {
                    "CABIN_CREW", "PILOTS", "GROUND_OPERATIONS_AGENT", "ENGINEERING",
                    "GROUND_TRANSPORT_SERVICES",
                }));
        Assert.Equal(20, candidates.Count(c => c.Journey == DemoCandidateJourney.Unbooked));
        Assert.Equal(30, candidates.Count(c => c.Journey == DemoCandidateJourney.Ready));
        Assert.Equal(25, candidates.Count(c => c.Journey == DemoCandidateJourney.Outstanding));
        Assert.Equal(15, candidates.Count(c => c.Journey == DemoCandidateJourney.NoShow));
        Assert.Equal(10, candidates.Count(c => c.Journey == DemoCandidateJourney.RecoveryCompleted));
    }
}
`````
