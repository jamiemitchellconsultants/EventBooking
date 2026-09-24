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
    public async Task RepostingAnAcceptanceUpdatesTheHeadcountReturnedByTheBoard()
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
                ListedAppointmentTypeIds = AppointmentTypeIds.All,
                ProposerHeadcount = 10,
            });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var proposalId = (await created.Content.ReadFromJsonAsync<CreatedResponse>())!.ProposalId;

        var accepted = await client.PostAsJsonAsync(
            $"/api/event-proposals/{proposalId}/acceptance",
            new { Headcount = 10 });
        var revised = await client.PostAsJsonAsync(
            $"/api/event-proposals/{proposalId}/acceptance",
            new { Headcount = 12 });

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);

        var board = await client.GetFromJsonAsync<BoardResponse>("/api/events/board");
        var proposal = Assert.Single(
            board!.OpenProposals,
            item => item.ProposalId == proposalId);
        Assert.True(proposal.AcceptedByMe);
        Assert.Equal(12, proposal.MyAcceptedHeadcount);
    }

    private sealed record CreatedResponse(Guid ProposalId, string Status, Guid? EventId);

    private sealed record BoardResponse(
        IReadOnlyList<OpenProposalResponse> OpenProposals);

    private sealed record OpenProposalResponse(
        Guid ProposalId,
        bool AcceptedByMe,
        int? MyAcceptedHeadcount);
}
