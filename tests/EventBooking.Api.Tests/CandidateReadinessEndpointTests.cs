using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Api.Tests;

/// <summary>Verifies readiness is a Coordinator-only EventBooking response.</summary>
[Collection("api")]
public sealed class CandidateReadinessEndpointTests(ApiFactory factory)
{
    private sealed record OutstandingResponse(string Code, string Name, bool IsRecoverable);

    private sealed record ReadinessResponse(
        Guid CandidateId,
        string Code,
        string Display,
        IReadOnlyList<OutstandingResponse> OutstandingAppointmentTypes);

    /// <summary>A Coordinator receives the stable no-booking reason for a grouped Candidate.</summary>
    [Fact]
    public async Task CoordinatorCanReadNoActiveBookingReason()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/candidates", new
        {
            Name = "Amara Novak",
            Email = $"{Guid.NewGuid():N}@example.com",
            EmployeeGroupId = EmployeeGroupIds.CabinCrew,
        });
        var candidateId = await created.Content.ReadFromJsonAsync<Guid>();

        var response = await client.GetAsync($"/api/candidates/{candidateId}/readiness");
        var body = await response.Content.ReadFromJsonAsync<ReadinessResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("NoActiveBooking", body!.Code);
        Assert.Empty(body.OutstandingAppointmentTypes);
    }

    /// <summary>An Admin is forbidden from receiving any Candidate readiness payload.</summary>
    [Fact]
    public async Task AdminIsForbidden()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);

        var response = await factory.CreateClient()
            .GetAsync($"/api/candidates/{Guid.NewGuid()}/readiness");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
