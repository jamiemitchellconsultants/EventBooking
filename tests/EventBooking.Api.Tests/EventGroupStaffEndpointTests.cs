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
public sealed class EventGroupStaffEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task AdminAndCoordinatorMayCreateButManagerMayNot()
    {
        var client = factory.CreateClient();
        var body = new
        {
            title = "Autumn intake",
            description = "Choose a date",
            attendeeGroupIds = new[] { AttendeeGroupIds.CabinCrew },
        };

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var admin = await client.PostAsJsonAsync("/api/event-groups", body);
        Assert.Equal(HttpStatusCode.Created, admin.StatusCode);
        var json = await admin.Content.ReadAsStringAsync();
        Assert.DoesNotContain("attendeeEmail", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("memberCount", json, StringComparison.OrdinalIgnoreCase);

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var coordinator = await client.PostAsJsonAsync("/api/event-groups", body);
        Assert.Equal(HttpStatusCode.Created, coordinator.StatusCode);

        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.MedicalCheckUp);
        var manager = await client.PostAsJsonAsync("/api/event-groups", body);
        Assert.Equal(HttpStatusCode.Forbidden, manager.StatusCode);
    }

    [Fact]
    public async Task IncompatibleStartedOrCancelledEventIsRejected()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var groupId = await CreateGroupAsync(client, "Compat intake");
        var compatible = await SeedEventAsync(new DateOnly(2030, 2, 10));
        var missingTypes = await SeedEventAsync(
            new DateOnly(2030, 2, 11), [AppointmentTypeIds.MedicalCheckUp]);
        var started = await SeedEventAsync(new DateOnly(2026, 2, 10));
        var cancelled = await SeedEventAsync(new DateOnly(2030, 2, 12), cancelled: true);

        var added = await client.PutAsJsonAsync(
            $"/api/event-groups/{groupId}/events/{compatible}", new { expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);

        foreach (var eventId in new[] { missingTypes, started, cancelled })
        {
            var response = await client.PutAsJsonAsync(
                $"/api/event-groups/{groupId}/events/{eventId}", new { expectedVersion = 2 });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }
    }

    [Fact]
    public async Task MembershipGatesAreIndependentAcrossGroups()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var client = factory.CreateClient();
        var firstId = await CreateGroupAsync(client, "First intake");
        var secondId = await CreateGroupAsync(client, "Second intake");
        var eventId = await SeedEventAsync(new DateOnly(2030, 3, 10));

        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(
            $"/api/event-groups/{firstId}/events/{eventId}", new { expectedVersion = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(
            $"/api/event-groups/{secondId}/events/{eventId}", new { expectedVersion = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync(
            $"/api/event-groups/{firstId}/events/{eventId}",
            new { isOpen = true, expectedVersion = 2 })).StatusCode);

        var first = await BodyAsync(await client.GetAsync($"/api/event-groups/{firstId}"));
        var second = await BodyAsync(await client.GetAsync($"/api/event-groups/{secondId}"));
        Assert.True(first.GetProperty("events")[0].GetProperty("isOpen").GetBoolean());
        Assert.False(second.GetProperty("events")[0].GetProperty("isOpen").GetBoolean());
    }

    [Fact]
    public async Task MembershipAddRemoveAndStaleVersionConflict()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var groupId = await CreateGroupAsync(client, "Version intake");
        var eventId = await SeedEventAsync(new DateOnly(2030, 4, 10));

        var added = await BodyAsync(await client.PutAsJsonAsync(
            $"/api/event-groups/{groupId}/events/{eventId}", new { expectedVersion = 1 }));
        Assert.Equal(2, added.GetProperty("version").GetInt64());

        var removed = await client.DeleteAsync(
            $"/api/event-groups/{groupId}/events/{eventId}?expectedVersion=2");
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);

        var repeat = await client.DeleteAsync(
            $"/api/event-groups/{groupId}/events/{eventId}?expectedVersion=3");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, repeat.StatusCode);

        var stale = await client.PutAsJsonAsync($"/api/event-groups/{groupId}", new
        {
            title = "Version intake",
            description = "Stale write.",
            attendeeGroupIds = new[] { AttendeeGroupIds.CabinCrew },
            isOpen = false,
            expectedVersion = 999L,
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("version-conflict", (await BodyAsync(stale)).GetProperty("type").GetString());
    }

    [Fact]
    public async Task StaffReadsCarryNoAttendeeIdentityOrCounts()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var client = factory.CreateClient();
        var groupId = await CreateGroupAsync(client, "No data intake");

        foreach (var url in new[] { "/api/event-groups", $"/api/event-groups/{groupId}" })
        {
            var json = await (await client.GetAsync(url)).Content.ReadAsStringAsync();
            Assert.DoesNotContain("attendeeEmail", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("memberCount", json, StringComparison.OrdinalIgnoreCase);
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

    private async Task<Guid> SeedEventAsync(
        DateOnly date, IEnumerable<Guid>? listedTypes = null, bool cancelled = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var listed = (listedTypes ?? AppointmentTypeIds.All).ToList();
        var proposer = listed.Contains(AppointmentTypeIds.DrugAndAlcoholTesting)
            ? AppointmentTypeIds.DrugAndAlcoholTesting
            : listed[0];
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0), 240),
            Guid.NewGuid(), proposer, listed);
        foreach (var type in listed)
            proposal.Accept(type, Guid.NewGuid(), 5);
        var eventId = Guid.NewGuid();
        var eventItem = Event.CreateFrom(eventId, proposal);
        if (cancelled)
            eventItem.Cancel(ProposalFixture.Zones, ProposalFixture.TimeZoneId, ProposalFixture.Now);
        db.EventProposals.Add(proposal);
        db.Events.Add(eventItem);
        await db.SaveChangesAsync();
        return eventId;
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();
}
