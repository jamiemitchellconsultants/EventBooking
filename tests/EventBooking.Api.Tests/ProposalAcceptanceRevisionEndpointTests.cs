using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Locations;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class ProposalAcceptanceRevisionEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task RepostingAnAcceptanceUpdatesTheHeadcountReturnedByTheList()
    {
        await factory.GivenStaffAsync(Role.Manager, AppointmentTypeIds.MedicalCheckUp);
        await factory.GivenStaffAsync(Role.Manager, AppointmentTypeIds.UniformFitting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        var created = await client.PostAsJsonAsync(
            "/api/event-proposals",
            new
            {
                LocationId = TransitionalLocation.Id,
                Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60),
                StartTime = new TimeOnly(9, 0),
                DurationMinutes = 240,
                AppointmentTypeIds = AppointmentTypeIds.All,
                Headcount = 10,
            });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var proposalId = (await created.Content.ReadFromJsonAsync<CreatedResponse>())!.Id;

        var accepted = await client.PutAsJsonAsync(
            $"/api/event-proposals/{proposalId}/acceptance",
            new { Headcount = 10 });
        var revised = await client.PutAsJsonAsync(
            $"/api/event-proposals/{proposalId}/acceptance",
            new { Headcount = 12 });

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);

        var listed = await client.GetFromJsonAsync<ProposalPage>(
            "/api/event-proposals?status=Open&limit=50");
        var proposal = Assert.Single(
            listed!.Items,
            item => item.Id == proposalId);
        Assert.True(proposal.AcceptedByMe);
        Assert.Equal(12, proposal.MyAcceptedHeadcount);
    }

    private sealed record CreatedResponse(Guid Id, string Status, Guid? EventId);

    private sealed record ProposalPage(
        IReadOnlyList<OpenProposalResponse> Items, string? NextCursor);

    private sealed record OpenProposalResponse(
        Guid Id,
        bool AcceptedByMe,
        int? MyAcceptedHeadcount);
}
