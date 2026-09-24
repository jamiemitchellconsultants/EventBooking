using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventBooking.Domain.Access;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Api.Tests.Catalogue;

/// <summary>
/// The five proposal routes. The two cases the master plan names by hand are the shape of the
/// creation response: three listed types leaves it Open, one confirms it on the spot.
/// </summary>
[Collection("api")]
public sealed class NegotiationEndpointTests(ApiFactory factory)
    : CatalogueSuite(factory)
{
    [Fact]
    public async Task ProposingForThreeTypesReturnsCreatedAndOpen()
    {
        var medical = await GivenAppointmentTypeAsync("NEG3_MED");
        var fitting = await GivenAppointmentTypeAsync("NEG3_FIT");
        var induction = await GivenAppointmentTypeAsync("NEG3_IND");
        await GivenManagersForAsync(fitting, induction);
        var location = await GivenLocationAsync("NEG3_LON");
        var client = await ManagerAsync(medical, "U700110");

        var response = await PostAsync(client, "/api/event-proposals", new
        {
            locationId = location,
            date = "2026-11-10",
            startTime = "09:30",
            durationMinutes = 240,
            appointmentTypeIds = new[] { medical, fitting, induction },
            headcount = 8,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await BodyAsync(response);
        Assert.Equal("Open", body.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("eventId").ValueKind);
    }

    [Fact]
    public async Task ProposingForOneTypeReturnsCreatedConfirmedAndAnEventId()
    {
        var medical = await GivenAppointmentTypeAsync("NEG1_MED");
        var location = await GivenLocationAsync("NEG1_LON");
        var client = await ManagerAsync(medical, "U700111");

        var response = await PostAsync(client, "/api/event-proposals", new
        {
            locationId = location,
            date = "2026-11-11",
            startTime = "09:30",
            durationMinutes = 240,
            appointmentTypeIds = new[] { medical },
            headcount = 8,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await BodyAsync(response);
        Assert.Equal("Confirmed", body.GetProperty("status").GetString());
        Assert.NotEqual(Guid.Empty, body.GetProperty("eventId").GetGuid());
    }

    [Fact]
    public async Task TheCreationResponseNamesItsProposalId()
    {
        var medical = await GivenAppointmentTypeAsync("NEGIDMED");
        var location = await GivenLocationAsync("NEGIDLOC");
        var client = await ManagerAsync(medical, "U700119");

        var body = await BodyAsync(await PostAsync(client, "/api/event-proposals", new
        {
            locationId = location,
            date = "2026-11-13",
            startTime = "09:30",
            durationMinutes = 240,
            appointmentTypeIds = new[] { medical },
            headcount = 8,
        }));

        Assert.True(body.TryGetProperty("proposalId", out var proposalId));
        Assert.NotEqual(Guid.Empty, proposalId.GetGuid());
        Assert.False(body.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task ProposalListRowsCarryTheirListedTypes()
    {
        var (proposalId, otherType) = await GivenOpenProposalAsync("NEGT");
        var client = await ManagerAsync(otherType, "U700120");

        var row = (await BodyAsync(await client.GetAsync("/api/event-proposals?limit=50")))
            .GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == proposalId);

        var codes = row.GetProperty("types").EnumerateArray()
            .Select(x => x.GetProperty("code").GetString())
            .Order()
            .ToArray();
        Assert.Equal(["NEGT_ONE", "NEGT_TWO"], codes.Select(x => x!));
        Assert.All(
            row.GetProperty("types").EnumerateArray(),
            x => Assert.False(string.IsNullOrWhiteSpace(x.GetProperty("name").GetString())));
    }

    [Fact]
    public async Task AManagerWhoHasNotAcceptedSeesOnlyAccept()
    {
        var (proposalId, otherType) = await GivenOpenProposalAsync("NEGB");
        var client = await ManagerAsync(otherType, "U700121");

        var links = (await BodyAsync(await client.GetAsync("/api/event-proposals?limit=50")))
            .GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == proposalId)
            .GetProperty("_links");

        Assert.True(links.TryGetProperty("accept", out _));
        Assert.False(links.TryGetProperty("withdrawAcceptance", out _));
        Assert.False(links.TryGetProperty("withdraw", out _));
    }

    [Fact]
    public async Task AnAcceptingManagerSeesWithdrawAcceptanceButNotWithdraw()
    {
        var (proposalId, otherType) = await GivenOpenProposalAsync("NEGX", threeTypes: true);
        var client = await ManagerAsync(otherType, "U700122");
        var accepted = await client.PutAsJsonAsync(
            $"/api/event-proposals/{proposalId}/acceptance", new { headcount = 5 });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        var links = (await BodyAsync(await client.GetAsync("/api/event-proposals?limit=50")))
            .GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == proposalId)
            .GetProperty("_links");

        Assert.False(links.TryGetProperty("accept", out _));
        Assert.True(links.TryGetProperty("withdrawAcceptance", out _));
        Assert.False(links.TryGetProperty("withdraw", out _));
    }

    [Fact]
    public async Task TheProposingTypeSeesWithdrawAndWithdrawAcceptance()
    {
        var (proposalId, _) = await GivenOpenProposalAsync("NEGQ");
        var client = await ManagerAsync(await ProposerTypeOfAsync(proposalId), "U700123");

        var links = (await BodyAsync(await client.GetAsync("/api/event-proposals?limit=50")))
            .GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == proposalId)
            .GetProperty("_links");

        Assert.False(links.TryGetProperty("accept", out _));
        Assert.True(links.TryGetProperty("withdrawAcceptance", out _));
        Assert.True(links.TryGetProperty("withdraw", out _));
    }

    [Fact]
    public async Task AWithdrawnProposalOffersNoAction()
    {
        var (proposalId, otherType) = await GivenOpenProposalAsync("NEGV");
        var proposer = await ManagerAsync(await ProposerTypeOfAsync(proposalId), "U700124");
        await PostAsync(proposer, $"/api/event-proposals/{proposalId}/withdraw", new { });
        var client = await ManagerAsync(otherType, "U700125");

        var links = (await BodyAsync(await client.GetAsync("/api/event-proposals?limit=50")))
            .GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == proposalId)
            .GetProperty("_links");

        Assert.False(links.TryGetProperty("accept", out _));
        Assert.False(links.TryGetProperty("withdrawAcceptance", out _));
        Assert.False(links.TryGetProperty("withdraw", out _));
    }

    [Fact]
    public async Task AcceptanceIsRecordedRevisedAndWithdrawn()
    {
        // Three listed types: with two, the first acceptance would confirm the proposal and
        // the revision would come back proposal-not-open instead of OK.
        var (proposalId, otherType) = await GivenOpenProposalAsync("NEGA", threeTypes: true);
        var client = await ManagerAsync(otherType, "U700112");

        var recorded = await client.PutAsJsonAsync(
            $"/api/event-proposals/{proposalId}/acceptance", new { headcount = 5 });
        var revised = await client.PutAsJsonAsync(
            $"/api/event-proposals/{proposalId}/acceptance", new { headcount = 6 });
        var withdrawn = await client.DeleteAsync(
            $"/api/event-proposals/{proposalId}/acceptance");

        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, withdrawn.StatusCode);
    }

    /// <summary>
    /// The typed refusal Task 21 added. A withdrawn proposal is not open, and the body says so
    /// with a slug rather than only in its prose.
    /// </summary>
    [Fact]
    public async Task AcceptingAWithdrawnProposalIsProposalNotOpen()
    {
        var (proposalId, otherType) = await GivenOpenProposalAsync("NEGW");
        var proposer = await ManagerAsync(await ProposerTypeOfAsync(proposalId), "U700113");
        await PostAsync(proposer, $"/api/event-proposals/{proposalId}/withdraw", new { });
        var client = await ManagerAsync(otherType, "U700114");

        var response = await client.PutAsJsonAsync(
            $"/api/event-proposals/{proposalId}/acceptance", new { headcount = 5 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "proposal-not-open", (await BodyAsync(response)).GetProperty("type").GetString());
    }

    /// <summary>The forbidden case: negotiation is a Manager capability.</summary>
    [Fact]
    public async Task ACoordinatorCannotPropose()
    {
        var location = await GivenLocationAsync("NEGF_LON");
        var type = await GivenAppointmentTypeAsync("NEGF_MED");
        var client = await CoordinatorAsync("U700115");

        var response = await PostAsync(client, "/api/event-proposals", new
        {
            locationId = location, date = "2026-11-12", startTime = "09:30",
            durationMinutes = 240, appointmentTypeIds = new[] { type }, headcount = 8,
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TheProposalListIsTheCallersOwnTypesAndFiltersByStatus()
    {
        var (proposalId, otherType) = await GivenOpenProposalAsync("NEGL");
        var client = await ManagerAsync(otherType, "U700116");

        var open = await BodyAsync(
            await client.GetAsync("/api/event-proposals?status=Open&limit=50"));
        var confirmed = await BodyAsync(
            await client.GetAsync("/api/event-proposals?status=Confirmed&limit=50"));

        Assert.Contains(
            open.GetProperty("items").EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == proposalId);
        Assert.DoesNotContain(
            confirmed.GetProperty("items").EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == proposalId);
    }

    [Fact]
    public async Task AWindowInThePastIsRejected()
    {
        var medical = await GivenAppointmentTypeAsync("NEGP_MED");
        var location = await GivenLocationAsync("NEGP_LON");
        var client = await ManagerAsync(medical, "U700118");

        var response = await PostAsync(client, "/api/event-proposals", new
        {
            locationId = location,
            date = "2020-01-01",
            startTime = "09:00",
            durationMinutes = 240,
            appointmentTypeIds = new[] { medical },
            headcount = 10,
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task AnUnknownStatusIsAValidationFailureRatherThanAnEmptyPage()
    {
        var (_, otherType) = await GivenOpenProposalAsync("NEGS");
        var client = await ManagerAsync(otherType, "U700117");

        var response = await client.GetAsync("/api/event-proposals?status=open");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    /// <summary>
    /// Every listed type must have a current Manager before a proposal can be created, so a
    /// case listing three types has to seed two more profiles than it signs in as.
    /// </summary>
    /// <param name="appointmentTypeIds">The types needing a Manager.</param>
    /// <returns>A task tracking the seeding.</returns>
    private async Task GivenManagersForAsync(params Guid[] appointmentTypeIds)
    {
        foreach (var appointmentTypeId in appointmentTypeIds)
        {
            await Factory.GivenStaffAsync([Role.Manager], appointmentTypeId);
        }
    }

    /// <summary>
    /// Seeds two types with a Manager each and proposes through the route as the first,
    /// leaving the proposal Open because the second type has not accepted. Returns the
    /// proposal and the second type, which is the one the calling case signs in as.
    /// </summary>
    /// <param name="prefix">A per-case code prefix, so the suite's rows never collide.</param>
    /// <param name="threeTypes">Whether to list a third type, keeping the proposal Open after one acceptance.</param>
    /// <returns>The proposal identifier and the non-proposing type.</returns>
    private async Task<(Guid ProposalId, Guid OtherType)> GivenOpenProposalAsync(
        string prefix, bool threeTypes = false)
    {
        var proposerType = await GivenAppointmentTypeAsync($"{prefix}_ONE");
        var otherType = await GivenAppointmentTypeAsync($"{prefix}_TWO");
        var listed = new List<Guid> { proposerType, otherType };
        if (threeTypes)
        {
            var thirdType = await GivenAppointmentTypeAsync($"{prefix}_TRE");
            await GivenManagersForAsync(thirdType);
            listed.Add(thirdType);
        }

        await GivenManagersForAsync(otherType);
        var location = await GivenLocationAsync($"{prefix}_LOC");
        var proposer = await ManagerAsync(proposerType, $"U7002{prefix}");

        var created = await BodyAsync(await PostAsync(proposer, "/api/event-proposals", new
        {
            locationId = location,
            date = "2026-11-20",
            startTime = "09:30",
            durationMinutes = 240,
            appointmentTypeIds = listed.ToArray(),
            headcount = 8,
        }));

        return (created.GetProperty("proposalId").GetGuid(), otherType);
    }

    /// <summary>
    /// Reads the proposing type back. Settlement #1 judges withdrawal against the type, not
    /// the identity, so a case that withdraws has to sign in as that type's Manager.
    /// </summary>
    /// <param name="proposalId">The proposal.</param>
    /// <returns>The proposing appointment type.</returns>
    private async Task<Guid> ProposerTypeOfAsync(Guid proposalId)
    {
        await using var scoped = NewScope();
        return await scoped.Context.EventProposals
            .Where(p => p.Id == proposalId)
            .Select(p => p.ProposerAppointmentTypeId)
            .SingleAsync();
    }
}
