using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Api.Tests.Catalogue;

/// <summary>
/// The five event routes. The two-step cancellation is the case the master plan names: the
/// first call must change nothing, which is asserted against the stored status rather than
/// against the second response.
/// </summary>
[Collection("api")]
public sealed class EventEndpointTests(ApiFactory factory)
    : CatalogueSuite(factory)
{
    [Fact]
    public async Task ACoordinatorSeesEveryCapacityAndAManagerSeesOne()
    {
        var seeded = await GivenConfirmedEventAsync("EVT_SCOPE", "ES");

        // One client, two identities: the factory's auth state is per-request, so the reads
        // must flip SignedInAs between them rather than holding two signed-in clients.
        var coordinator = await CoordinatorAsync("U700201");
        var everyType = await BodyAsync(
            await coordinator.GetAsync($"/api/events/{seeded.EventId}"));
        Factory.SignedInAs = await Factory.GivenStaffAsync(
            [Role.Manager], seeded.SecondType);
        Factory.StaffIdClaim = "U700202";
        var ownType = await BodyAsync(
            await coordinator.GetAsync($"/api/events/{seeded.EventId}"));

        Assert.Equal(2, everyType.GetProperty("capacities").GetArrayLength());
        var only = Assert.Single(ownType.GetProperty("capacities").EnumerateArray());
        Assert.Equal(seeded.SecondType, only.GetProperty("appointmentTypeId").GetGuid());
    }

    [Fact]
    public async Task OnlyTheCallersOwnCapacityRowOffersAdjust()
    {
        var seeded = await GivenConfirmedEventAsync("EVT_ADJ", "EA2");

        var manager = await ManagerAsync(seeded.SecondType, "U700206");
        var own = await BodyAsync(await manager.GetAsync($"/api/events/{seeded.EventId}"));
        var only = Assert.Single(own.GetProperty("capacities").EnumerateArray());
        Assert.True(only.GetProperty("_links").TryGetProperty("adjust", out var adjust));
        Assert.Equal(
            $"/api/events/{seeded.EventId}/capacities/{seeded.SecondType}",
            adjust.GetProperty("href").GetString());

        var coordinator = await CoordinatorAsync("U700207");
        var every = await BodyAsync(await coordinator.GetAsync($"/api/events/{seeded.EventId}"));
        Assert.All(
            every.GetProperty("capacities").EnumerateArray(),
            row => Assert.False(row.GetProperty("_links").TryGetProperty("adjust", out _)));
    }

    /// <summary>
    /// An Admin reads the same full view a Coordinator does, and the row carries no attendee
    /// data at all — the operations surface is capacity, never people.
    /// </summary>
    [Fact]
    public async Task AnAdminReadsFullCapacitiesAndNoAttendeeData()
    {
        var seeded = await GivenConfirmedEventAsync("EVT_ADMIN", "EA");
        var client = await AdminAsync("U700209");

        var body = await BodyAsync(await client.GetAsync($"/api/events/{seeded.EventId}"));

        Assert.Equal(2, body.GetProperty("capacities").GetArrayLength());
        foreach (var member in new[] { "attendee", "attendees", "email" })
        {
            Assert.False(
                body.EnumerateObject().Any(x =>
                    x.Name.Contains(member, StringComparison.OrdinalIgnoreCase)),
                member);
        }
    }

    /// <summary>
    /// Task 21's one event-time representation, seen through a route. Every member design 05
    /// names has to be present, because the Web work in Phase 5 renders from these and nothing
    /// else.
    /// </summary>
    [Fact]
    public async Task AnEventCarriesEveryMemberOfTheTimeContract()
    {
        var seeded = await GivenConfirmedEventAsync("EVT_TIME", "ET");
        var client = await CoordinatorAsync("U700203");

        var time = (await BodyAsync(await client.GetAsync($"/api/events/{seeded.EventId}")))
            .GetProperty("time");

        foreach (var member in new[]
        {
            "date", "startTime", "durationMinutes", "startLocal", "endLocal",
            "startUtc", "endUtc", "timeZoneId", "zoneAbbreviation",
        })
        {
            Assert.True(time.TryGetProperty(member, out _), member);
        }
    }

    [Fact]
    public async Task AnEventOutsideTheCallersScopeIsNotFound()
    {
        var seeded = await GivenConfirmedEventAsync("EVT_HIDDEN", "EH");
        var stranger = await GivenAppointmentTypeAsync("EHO");
        var client = await ManagerAsync(stranger, "U700204");

        var response = await client.GetAsync($"/api/events/{seeded.EventId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("not-found", (await BodyAsync(response)).GetProperty("type").GetString());
    }

    [Fact]
    public async Task TheListFiltersByLocationAndDate()
    {
        var seeded = await GivenConfirmedEventAsync("EVT_FILTER", "EF");
        var client = await CoordinatorAsync("U700205");

        var matching = await BodyAsync(await client.GetAsync(
            $"/api/events?locationId={seeded.LocationId}&from=2026-11-01&to=2026-11-30&limit=50"));
        var missing = await BodyAsync(await client.GetAsync(
            "/api/events?from=2027-01-01&to=2027-01-31&limit=50"));

        Assert.Contains(
            matching.GetProperty("items").EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == seeded.EventId);
        Assert.Empty(missing.GetProperty("items").EnumerateArray());
    }

    /// <summary>
    /// The route names the type. A Manager naming somebody else's is refused — Task 22a's rule,
    /// reached through the endpoint, which is the only place a caller can supply the wrong one.
    /// </summary>
    [Fact]
    public async Task AManagerCannotAdjustAnotherTypesCapacity()
    {
        var seeded = await GivenConfirmedEventAsync("EVT_CAP", "EC");
        var client = await ManagerAsync(seeded.SecondType, "U700206");

        var own = await client.PutAsJsonAsync(
            $"/api/events/{seeded.EventId}/capacities/{seeded.SecondType}",
            new { totalHeadcount = 12 });
        var other = await client.PutAsJsonAsync(
            $"/api/events/{seeded.EventId}/capacities/{seeded.ProposerType}",
            new { totalHeadcount = 12 });

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
    }

    /// <summary>
    /// Two-step cancellation. The first call must leave the event Active — asserting only the
    /// 409 would pass even if the endpoint had cancelled it and then reported a consequence.
    /// </summary>
    [Fact]
    public async Task CancellationIsTwoStepAndTheFirstCallChangesNothing()
    {
        var seeded = await GivenConfirmedEventAsync("EVT_CANCEL", "EX");
        var client = await CoordinatorAsync("U700207");

        var first = await PostAsync(client, $"/api/events/{seeded.EventId}/cancel", new { });
        var afterFirst = await StatusOfAsync(seeded.EventId);
        var second = await PostAsync(
            client, $"/api/events/{seeded.EventId}/cancel?confirm=true", new { });
        var afterSecond = await StatusOfAsync(seeded.EventId);

        Assert.Equal(HttpStatusCode.Conflict, first.StatusCode);
        var problem = await BodyAsync(first);
        Assert.Equal("confirmation-required", problem.GetProperty("type").GetString());
        Assert.True(problem.TryGetProperty("consequence", out _));
        Assert.Equal("Active", afterFirst);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("Cancelled", afterSecond);
    }

    [Fact]
    public async Task TheCancellableListExcludesACancelledEvent()
    {
        var seeded = await GivenConfirmedEventAsync("EVT_LIST", "EL");
        var client = await CoordinatorAsync("U700208");
        await PostAsync(client, $"/api/events/{seeded.EventId}/cancel?confirm=true", new { });

        var body = await BodyAsync(await client.GetAsync("/api/events/cancellable?limit=50"));

        Assert.DoesNotContain(
            body.GetProperty("items").EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == seeded.EventId);
    }

    private sealed record SeededEvent(
        Guid EventId, Guid LocationId, Guid ProposerType, Guid SecondType);

    /// <summary>
    /// Proposes for two types and accepts as both, which confirms the proposal and creates the
    /// event with two capacity rows — the shape every case here needs.
    /// </summary>
    /// <param name="prefix">A per-case code prefix, so the suite's rows never collide.</param>
    /// <param name="tag">The two-letter type-code tag: type codes hold eight characters.</param>
    /// <returns>The seeded event.</returns>
    private async Task<SeededEvent> GivenConfirmedEventAsync(string prefix, string tag)
    {
        var proposerType = await GivenAppointmentTypeAsync($"{tag}1");
        var secondType = await GivenAppointmentTypeAsync($"{tag}2");
        var locationId = await GivenLocationAsync($"{prefix}_LOC");

        var proposer = await ManagerAsync(proposerType, $"U7003{tag}");
        await Factory.GivenStaffAsync([Role.Manager], secondType);
        var created = await BodyAsync(await PostAsync(proposer, "/api/event-proposals", new
        {
            locationId,
            date = "2026-11-25",
            startTime = "09:30",
            durationMinutes = 240,
            appointmentTypeIds = new[] { proposerType, secondType },
            headcount = 8,
        }));
        var proposalId = created.GetProperty("proposalId").GetGuid();

        var second = await ManagerAsync(secondType, $"U7004{tag}");
        var confirmed = await BodyAsync(await second.PutAsJsonAsync(
            $"/api/event-proposals/{proposalId}/acceptance", new { headcount = 6 }));

        return new SeededEvent(
            confirmed.GetProperty("eventId").GetGuid(), locationId, proposerType, secondType);
    }

    /// <summary>Reads the stored status, so a two-step case can assert state not responses.</summary>
    /// <param name="eventId">The event.</param>
    /// <returns>The status name.</returns>
    private async Task<string> StatusOfAsync(Guid eventId)
    {
        await using var scoped = NewScope();
        var status = await scoped.Context.Events
            .Where(e => e.Id == eventId)
            .Select(e => e.Status)
            .SingleAsync();
        return status.ToString();
    }
}
