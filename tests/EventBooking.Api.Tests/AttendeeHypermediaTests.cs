using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class AttendeeHypermediaTests(ApiFactory factory)
{
    [Fact]
    public async Task AttendeePageCarriesItemsAndItemsCarryActions()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/attendees/", new
        {
            name = "Hyper Media",
            email = $"hyper-{Guid.NewGuid():N}@example.com",
            attendeeGroupId = AttendeeGroupIds.Pilots,
        });
        var id = await created.Content.ReadFromJsonAsync<Guid>();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/attendees/"));
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.False(document.RootElement.TryGetProperty("_links", out _));
        var attendee = document.RootElement.GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("attendeeId").GetGuid() == id);
        var links = attendee.GetProperty("_links");
        AssertLink(links, "bookings", $"/api/attendees/{id}/bookings", "GET", "listAttendeeBookings");
        AssertLink(links, "readiness", $"/api/attendees/{id}/readiness", "GET", "getAttendeeReadiness");
        AssertLink(links, "audit", $"/api/audit/attendees/{id}", "GET", "getAttendeeAuditHistory");
        AssertLink(links, "update", $"/api/attendees/{id}", "PUT", "updateAttendee");
        AssertLink(links, "delete", $"/api/attendees/{id}", "DELETE", "deleteAttendee");
        AssertLink(links, "invite", $"/api/attendees/{id}/invites", "POST", "inviteAttendee");
        AssertLink(links, "emailRetry", $"/api/attendees/{id}/email-retry", "POST", "retryAttendeeEmail");
    }

    [Fact]
    public async Task GuidAndNoContentContractsRemainUnchanged()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        using var created = await client.PostAsJsonAsync("/api/attendees/", new
        {
            name = "Compatibility",
            email = $"compat-{Guid.NewGuid():N}@example.com",
            attendeeGroupId = AttendeeGroupIds.CabinCrew,
        });
        var text = await created.Content.ReadAsStringAsync();
        Assert.True(Guid.TryParse(JsonSerializer.Deserialize<string>(text) ?? text.Trim('"'), out var id));
        using var updated = await client.PutAsJsonAsync($"/api/attendees/{id}", new
        {
            name = "Compatibility Two",
            email = $"compat2-{Guid.NewGuid():N}@example.com",
            attendeeGroupId = AttendeeGroupIds.CabinCrew,
        });
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        Assert.Equal(string.Empty, await updated.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ReadinessLinksBackToAttendeeWorkflows()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/attendees/", new
        {
            name = "Ready Links",
            email = $"ready-{Guid.NewGuid():N}@example.com",
            attendeeGroupId = AttendeeGroupIds.Engineering,
        });
        var id = await created.Content.ReadFromJsonAsync<Guid>();
        using var json = JsonDocument.Parse(await client.GetStringAsync($"/api/attendees/{id}/readiness"));
        var links = json.RootElement.GetProperty("_links");
        AssertLink(links, "bookings", $"/api/attendees/{id}/bookings", "GET", "listAttendeeBookings");
        AssertLink(links, "readiness", $"/api/attendees/{id}/readiness", "GET", "getAttendeeReadiness");
        Assert.False(links.TryGetProperty("startRecovery", out _));
    }

    private static void AssertLink(JsonElement links, string relation, string href, string method, string operationId)
    {
        var link = links.GetProperty(relation);
        Assert.Equal(href, link.GetProperty("href").GetString());
        Assert.Equal(method, link.GetProperty("method").GetString());
        Assert.Equal(operationId, link.GetProperty("operationId").GetString());
    }
}
