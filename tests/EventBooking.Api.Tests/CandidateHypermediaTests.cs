using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class CandidateHypermediaTests(ApiFactory factory)
{
    [Fact]
    public async Task CandidateArrayStaysArrayAndItemsCarryActions()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/candidates/", new
        {
            name = "Hyper Media",
            email = $"hyper-{Guid.NewGuid():N}@example.com",
            employeeGroupId = EmployeeGroupIds.Pilots,
        });
        var id = await created.Content.ReadFromJsonAsync<Guid>();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/candidates/"));
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        var candidate = document.RootElement.EnumerateArray()
            .Single(x => x.GetProperty("candidateId").GetGuid() == id);
        var links = candidate.GetProperty("_links");
        AssertLink(links, "bookings", $"/api/candidates/{id}/bookings", "GET", "listCandidateBookings");
        AssertLink(links, "readiness", $"/api/candidates/{id}/readiness", "GET", "getCandidateReadiness");
        AssertLink(links, "audit", $"/api/audit/candidate/{id}", "GET", "getCandidateAuditHistory");
        AssertLink(links, "update", $"/api/candidates/{id}", "PUT", "updateCandidate");
        AssertLink(links, "delete", $"/api/candidates/{id}", "DELETE", "deleteCandidate");
        AssertLink(links, "invite", $"/api/candidates/{id}/invite", "POST", "triggerCandidateInvite");
    }

    [Fact]
    public async Task GuidAndNoContentContractsRemainUnchanged()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        using var created = await client.PostAsJsonAsync("/api/candidates/", new
        {
            name = "Compatibility",
            email = $"compat-{Guid.NewGuid():N}@example.com",
            employeeGroupId = EmployeeGroupIds.CabinCrew,
        });
        var text = await created.Content.ReadAsStringAsync();
        Assert.True(Guid.TryParse(JsonSerializer.Deserialize<string>(text) ?? text.Trim('"'), out var id));
        using var updated = await client.PutAsJsonAsync($"/api/candidates/{id}", new
        {
            name = "Compatibility Two",
            email = $"compat2-{Guid.NewGuid():N}@example.com",
            employeeGroupId = EmployeeGroupIds.CabinCrew,
        });
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        Assert.Equal(string.Empty, await updated.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ReadinessLinksBackToCandidateWorkflows()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/candidates/", new
        {
            name = "Ready Links",
            email = $"ready-{Guid.NewGuid():N}@example.com",
            employeeGroupId = EmployeeGroupIds.Engineering,
        });
        var id = await created.Content.ReadFromJsonAsync<Guid>();
        using var json = JsonDocument.Parse(await client.GetStringAsync($"/api/candidates/{id}/readiness"));
        var links = json.RootElement.GetProperty("_links");
        AssertLink(links, "bookings", $"/api/candidates/{id}/bookings", "GET", "listCandidateBookings");
        AssertLink(links, "readiness", $"/api/candidates/{id}/readiness", "GET", "getCandidateReadiness");
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
