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
