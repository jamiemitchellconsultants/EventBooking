using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;
using EventBooking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class AuditEndpointTests(ApiFactory factory)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ACoordinatorGetsAEventsHistoryWithEveryFieldThePanelBindsTo()
    {
        var eventId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            context.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Event, eventId, AuditAction.EventConfirmed,
                ActorType.Staff, "staff-1", Now, "6 headcount total"));
            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var rows = await client.GetFromJsonAsync<List<RowResponse>>($"/api/audit/event/{eventId}");

        var row = Assert.Single(rows!);
        Assert.Equal(Now, row.Timestamp);
        Assert.Equal(AuditEntityTypes.Event, row.EntityType);
        Assert.Equal(eventId, row.EntityId);
        Assert.Equal("EventConfirmed", row.Action);
        Assert.Equal("Staff", row.ActorType);
        Assert.Equal("staff-1", row.ActorId);
        Assert.Equal("6 headcount total", row.Details);
    }

    [Fact]
    public async Task ACoordinatorGetsAAttendeesHistoryFromItsInvitesAndBookings()
    {
        var attendeeId = Guid.NewGuid();
        var inviteId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var pilots = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var attendee = Attendee.Create(attendeeId, "Amara Novak", "a.novak@mail.com", pilots, ProposalFixture.Now);
            context.Attendees.Add(attendee);
            context.Invites.Add(Invite.CreateInitial(
                inviteId,
                attendee.Id,
                Now.AddDays(4),
                [ProposalFixture.LocationId],
                [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
                attendee.RequiredAppointmentTypeIds,
                0));
            context.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Invite, inviteId, AuditAction.InviteCreated,
                ActorType.System, null, Now, "retry 0"));
            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var rows = await client.GetFromJsonAsync<List<RowResponse>>($"/api/audit/attendee/{attendeeId}");

        var row = Assert.Single(rows!);
        Assert.Equal("InviteCreated", row.Action);
        Assert.Equal("System", row.ActorType);
        Assert.Null(row.ActorId);
        Assert.Equal("retry 0", row.Details);
    }

    [Fact]
    public async Task AManagerIsForbiddenFromEitherAuditRoute()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.MedicalCheckUp);
        var client = factory.CreateClient();

        var eventResponse = await client.GetAsync($"/api/audit/event/{Guid.NewGuid()}");
        var attendeeResponse = await client.GetAsync($"/api/audit/attendee/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, eventResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, attendeeResponse.StatusCode);
    }

    [Fact]
    public async Task AnUnauthenticatedCallerIsChallenged()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/audit/event/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task SearchParsesQueryParametersAndReturnsNewestFirst()
    {
        var actorId = $"search-actor-{Guid.NewGuid():N}";
        var older = Guid.NewGuid();
        var newer = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            context.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Event, older, AuditAction.EventConfirmed,
                ActorType.Staff, actorId, Now, null));
            context.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Event, newer, AuditAction.EventConfirmed,
                ActorType.Staff, actorId, Now.AddHours(1), null));
            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<SearchPageResponse>(
            $"/api/audit/search?action=EventConfirmed&actorType=Staff&identifier={actorId}&pageSize=10");

        Assert.NotNull(page);
        Assert.Equal(2, page!.Rows.Count);
        Assert.Equal(newer, page.Rows[0].EntityId);
        Assert.Equal(older, page.Rows[1].EntityId);
    }

    [Fact]
    public async Task SearchHonoursTheFromAndToBounds()
    {
        var actorId = $"search-actor-{Guid.NewGuid():N}";
        var inRange = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            context.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Event, inRange, AuditAction.EventConfirmed,
                ActorType.Staff, actorId, Now, null));
            context.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventConfirmed,
                ActorType.Staff, actorId, Now.AddDays(-30), null));
            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<SearchPageResponse>(
            $"/api/audit/search?identifier={actorId}"
            + $"&from={Uri.EscapeDataString(Now.AddHours(-1).ToString("O"))}"
            + $"&to={Uri.EscapeDataString(Now.AddHours(1).ToString("O"))}");

        Assert.NotNull(page);
        var row = Assert.Single(page!.Rows);
        Assert.Equal(inRange, row.EntityId);
    }

    [Fact]
    public async Task SearchClampsOversizePageSize()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/audit/search?pageSize=1000");

        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<SearchPageResponse>();
        Assert.NotNull(page);
        Assert.True(page!.Rows.Count <= 200);
    }

    [Fact]
    public async Task SearchWithMalformedCursorRestartsFromNewest()
    {
        var eventId = Guid.NewGuid();
        var actorId = $"search-actor-{Guid.NewGuid():N}";

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            context.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Event, eventId, AuditAction.EventConfirmed,
                ActorType.Staff, actorId, Now, null));
            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<SearchPageResponse>(
            $"/api/audit/search?identifier={actorId}&cursor=not-valid-base64!!");

        Assert.NotNull(page);
        Assert.Contains(page!.Rows, r => r.EntityId == eventId);
    }

    [Fact]
    public async Task SearchRejectsAnUnparsableTimestampBound()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/audit/search?from=yesterday");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchNeverReturnsAttendeeRowsToAnAdmin()
    {
        var bookingId = Guid.NewGuid();
        var actorId = $"search-actor-{Guid.NewGuid():N}";

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            context.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Booking, bookingId, AuditAction.BookingCreated,
                ActorType.Staff, actorId, Now, null));
            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<SearchPageResponse>(
            $"/api/audit/search?identifier={actorId}");

        Assert.NotNull(page);
        Assert.Empty(page!.Rows);

        var forbidden = await client.GetAsync(
            $"/api/audit/search?entityType={AuditEntityTypes.Booking}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task AManagerIsForbiddenFromSearch()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.UniformFitting);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/audit/search");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed record SearchPageResponse(List<RowResponse> Rows, string? NextCursor);

    private sealed record RowResponse(
        DateTimeOffset Timestamp,
        string EntityType,
        Guid EntityId,
        string Action,
        string ActorType,
        string? ActorId,
        string? Details);
}
