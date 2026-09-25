using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Persistence;
using EventBooking.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class SelfRegistrationEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task PublicListAndDetailShowOnlyOpenGroups()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var openId = await CreateGroupAsync(client, "Open intake");
        var closedId = await CreateGroupAsync(client, "Closed intake");
        var eventId = await SeedEventAsync(new DateOnly(2030, 5, 10));
        await AddEventAsync(client, openId, eventId, 1);
        await AddEventAsync(client, closedId, eventId, 1);
        await OpenMemberAsync(client, openId, eventId, 2);
        await OpenGroupAsync(client, openId, "Open intake", 3);

        factory.SignedInAs = null;
        var anonymous = factory.CreateClient();
        var list = await BodyAsync(await anonymous.GetAsync("/api/public/event-groups"));
        var ids = list.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()).ToArray();
        Assert.Contains(openId, ids);
        Assert.DoesNotContain(closedId, ids);

        var detail = await BodyAsync(await anonymous.GetAsync($"/api/public/event-groups/{openId}"));
        Assert.Equal("Open intake", detail.GetProperty("title").GetString());
        Assert.Equal("Choose a date", detail.GetProperty("description").GetString());
        Assert.Single(detail.GetProperty("attendeeGroups").EnumerateArray());
        Assert.Single(detail.GetProperty("events").EnumerateArray());

        var missing = await anonymous.GetAsync($"/api/public/event-groups/{closedId}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task SubmitReturns201AndClosedGatesOrStaleEventsAreRejected()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var groupId = await CreateGroupAsync(client, "Submit intake");
        var eventId = await SeedEventAsync(new DateOnly(2030, 6, 10));
        await AddEventAsync(client, groupId, eventId, 1);

        factory.SignedInAs = null;
        var anonymous = factory.CreateClient();
        var closed = await anonymous.PostAsJsonAsync(
            $"/api/public/event-groups/{groupId}/registrations", new
            {
                eventId,
                attendeeGroupId = AttendeeGroupIds.CabinCrew,
                name = "Robin Public",
                email = "robin@example.com",
            });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, closed.StatusCode);

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        await OpenMemberAsync(client, groupId, eventId, 2);
        await OpenGroupAsync(client, groupId, "Submit intake", 3);

        factory.SignedInAs = null;
        var submitted = await anonymous.PostAsJsonAsync(
            $"/api/public/event-groups/{groupId}/registrations", new
            {
                eventId,
                attendeeGroupId = AttendeeGroupIds.CabinCrew,
                name = "Robin Public",
                email = "robin@example.com",
            });
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var body = await BodyAsync(submitted);
        Assert.Equal(71, body.GetProperty("confirmationToken").GetString()!.Length);
        Assert.Equal("robin@example.com", body.GetProperty("email").GetString());

        var stale = await anonymous.PostAsJsonAsync(
            $"/api/public/event-groups/{groupId}/registrations", new
            {
                eventId = Guid.NewGuid(),
                attendeeGroupId = AttendeeGroupIds.CabinCrew,
                name = "Robin Public",
                email = "other@example.com",
            });
        Assert.Equal(HttpStatusCode.NotFound, stale.StatusCode);
    }

    [Fact]
    public async Task PublicFailuresExposeNoInternals()
    {
        factory.SignedInAs = null;
        var anonymous = factory.CreateClient();

        var unknownGroup = await anonymous.PostAsJsonAsync(
            $"/api/public/event-groups/{Guid.NewGuid()}/registrations", new
            {
                eventId = Guid.NewGuid(),
                attendeeGroupId = Guid.NewGuid(),
                name = "Robin Public",
                email = "robin@example.com",
            });
        Assert.Equal(HttpStatusCode.NotFound, unknownGroup.StatusCode);

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var groupId = await CreateGroupAsync(client, "Hygiene intake");
        var eventId = await SeedEventAsync(new DateOnly(2030, 7, 10));
        await AddEventAsync(client, groupId, eventId, 1);
        await OpenMemberAsync(client, groupId, eventId, 2);
        await OpenGroupAsync(client, groupId, "Hygiene intake", 3);

        factory.SignedInAs = null;
        var badEmail = await anonymous.PostAsJsonAsync(
            $"/api/public/event-groups/{groupId}/registrations", new
            {
                eventId,
                attendeeGroupId = AttendeeGroupIds.CabinCrew,
                name = "Robin Public",
                email = "not-an-email",
            });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badEmail.StatusCode);

        foreach (var response in new[] { unknownGroup, badEmail })
        {
            var json = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Exception", json);
            Assert.DoesNotContain("at EventBooking", json);
            Assert.DoesNotContain("robin@example.com", json);
        }
    }

    private async Task<Guid> CreateGroupAsync(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync("/api/event-groups", new
        {
            title,
            description = "Choose a date",
            attendeeGroupIds = new[] { AttendeeGroupIds.CabinCrew },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await BodyAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task AddEventAsync(HttpClient client, Guid groupId, Guid eventId, long version)
    {
        var response = await client.PutAsJsonAsync(
            $"/api/event-groups/{groupId}/events/{eventId}", new { expectedVersion = version });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task OpenMemberAsync(HttpClient client, Guid groupId, Guid eventId, long version)
    {
        var response = await client.PatchAsJsonAsync(
            $"/api/event-groups/{groupId}/events/{eventId}",
            new { isOpen = true, expectedVersion = version });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task OpenGroupAsync(HttpClient client, Guid groupId, string title, long version)
    {
        var response = await client.PutAsJsonAsync($"/api/event-groups/{groupId}", new
        {
            title,
            description = "Choose a date",
            attendeeGroupIds = new[] { AttendeeGroupIds.CabinCrew },
            isOpen = true,
            expectedVersion = version,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<Guid> SeedEventAsync(DateOnly date)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0), 240), Guid.NewGuid());
        foreach (var type in proposal.ListedAppointmentTypeIds) proposal.Accept(type, Guid.NewGuid(), 5);
        var eventId = Guid.NewGuid();
        db.EventProposals.Add(proposal);
        db.Events.Add(Event.CreateFrom(eventId, proposal));
        await db.SaveChangesAsync();
        return eventId;
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();
}
