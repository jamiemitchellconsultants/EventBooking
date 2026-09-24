using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class StaffHypermediaTests(ApiFactory factory)
{
    [Fact]
    public async Task SettingsCarriesSelfAndUpdate()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        using var json = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/api/settings"));
        var links = json.RootElement.GetProperty("_links");
        AssertLink(links, "self", "/api/settings", "GET", "getSettings");
        AssertLink(links, "update", "/api/settings", "PUT", "updateSettings");
    }

    /// <summary>
    /// Design 05 puts no links on a page: the envelope is items plus a cursor, and the
    /// entry links live on the items. An empty page is still a page.
    /// </summary>
    [Fact]
    public async Task WorkspaceItemsCarryRosterEntryLinks()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;
            context.Events.Add(EventFixture.Create(
                Guid.NewGuid(), new EventWindow(today.AddDays(3), new TimeOnly(9, 0), 240),
                AppointmentTypeIds.All.ToDictionary(id => id, _ => 20)));
            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();
        using var workspace = JsonDocument.Parse(
            await client.GetStringAsync("/api/appointment-workspace/events"));

        Assert.False(workspace.RootElement.TryGetProperty("_links", out _));
        var items = workspace.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        foreach (var item in items)
        {
            var eventId = item.GetProperty("eventId").GetGuid();
            var links = item.GetProperty("_links");
            AssertLink(links, "roster", $"/api/appointment-workspace/events/{eventId}",
                "GET", "getWorkspaceRoster");
            AssertLink(links, "rosterCsv", $"/api/appointment-workspace/events/{eventId}/roster.csv",
                "GET", "exportWorkspaceRoster");
        }
    }

    [Fact]
    public async Task AuditSearchReturnsAPageAndOmitsNextCursorWhenExhausted()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var actorId = $"hypermedia-{Guid.NewGuid():N}";
        using var json = JsonDocument.Parse(await factory.CreateClient().GetStringAsync(
            $"/api/audit?actorId={actorId}&limit=50"));

        Assert.False(json.RootElement.TryGetProperty("_links", out _));
        Assert.Empty(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(
            JsonValueKind.Null, json.RootElement.GetProperty("nextCursor").ValueKind);
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
