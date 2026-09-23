using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class StaffHypermediaTests(ApiFactory factory)
{
    [Fact]
    public async Task SettingsCarriesSelfAndUpdate()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        using var json = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/api/admin/settings"));
        var links = json.RootElement.GetProperty("_links");
        AssertLink(links, "self", "/api/admin/settings", "GET", "getSettings");
        AssertLink(links, "update", "/api/admin/settings", "PUT", "updateSettings");
    }

    [Fact]
    public async Task EventCollectionsCarryEntryLinksEvenWhenEmpty()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();
        using var board = JsonDocument.Parse(await client.GetStringAsync("/api/events/board"));
        AssertLink(board.RootElement.GetProperty("_links"), "self", "/api/events/board", "GET", "getEventBoard");
        using var workspace = JsonDocument.Parse(await client.GetStringAsync("/api/appointment-workspace/events"));
        AssertLink(workspace.RootElement.GetProperty("_links"), "self",
            "/api/appointment-workspace/events", "GET", "listAppointmentEvents");
    }

    [Fact]
    public async Task AuditSearchCarriesSelfAndOmitsNextWhenExhausted()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        using var json = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/api/audit/search?pageSize=50"));
        var links = json.RootElement.GetProperty("_links");
        AssertLink(links, "self", "/api/audit/search?pageSize=50", "GET", "searchAudit");
        Assert.False(links.TryGetProperty("next", out _));
    }

    [Fact]
    public async Task MePreservesFieldsAndAddsLinks()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        using var json = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/api/me"));
        Assert.True(json.RootElement.TryGetProperty("staffId", out _));
        Assert.True(json.RootElement.TryGetProperty("roles", out _));
        AssertLink(json.RootElement.GetProperty("_links"), "self", "/api/me", "GET", "getMyAccess");
    }

    private static void AssertLink(JsonElement links, string relation, string href, string method, string operationId)
    {
        var link = links.GetProperty(relation);
        Assert.Equal(href, link.GetProperty("href").GetString());
        Assert.Equal(method, link.GetProperty("method").GetString());
        Assert.Equal(operationId, link.GetProperty("operationId").GetString());
    }
}
