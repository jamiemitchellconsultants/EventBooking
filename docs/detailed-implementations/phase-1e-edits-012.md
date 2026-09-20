# 01e — Location-restricted invites and closed attendee transitions, edits 12 (Task 8)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Api.Tests/AuditEndpointTests.cs — 1/1

<!-- retirement-file: {"id":25,"file":"tests/EventBooking.Api.Tests/AuditEndpointTests.cs","beforeSha":"a3daadabfdf4e76617003dbf70528ce21bafa91fecb78ba57bdf4cd5f4d83d87","afterSha":"edd4832851aad5f301463071c3cde44d20b00f88a9b0e118d40a511be48f676f","side":"after","part":1,"parts":1} -->

`````csharp
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
                "hash",
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
`````

## before — tests/EventBooking.Api.Tests/BookingEndpointTests.cs — 1/1

<!-- retirement-file: {"id":26,"file":"tests/EventBooking.Api.Tests/BookingEndpointTests.cs","beforeSha":"e8e4d04f6dc2991764ff1f61ba1e1458eb20f08bf05faf011b4d8a4cbc5c05d5","afterSha":"ae1535aa5b307c2cbd8c1e77b6f5cd9794c5ed85a24e22542a2779c234273cef","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using EventBooking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class BookingEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task AValidTokenReturnsOnlyTheAttendeeFacingOptionsEarliestFirst()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/booking/{invite.Token}");
        var json = await response.Content.ReadAsStringAsync();
        var view = JsonSerializer.Deserialize<InviteResponse>(json, JsonSerializerOptions.Web);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(view);

        // Catches an incomplete root projection that drops the invite or attendee identity.
        Assert.Equal(invite.Id, view!.InviteId);
        Assert.Equal("Amara Novak", view!.AttendeeName);

        // Catches projecting no appointment types or types from the wrong source.
        Assert.Equal(["Drug & Alcohol Testing", "Uniform Fitting"], view.AppointmentTypeNames);

        // Catches removing chronological ordering or ordering by the offered list or event ID.
        Assert.Equal(
            [
                Guid.Parse("00000000-0000-0000-0000-000000000010"),
                Guid.Parse("00000000-0000-0000-0000-000000000090"),
                Guid.Parse("00000000-0000-0000-0000-000000000050"),
            ],
            view.Options.Select(option => option.EventId));
        Assert.Equal(
            [new DateOnly(2030, 1, 14), new DateOnly(2030, 1, 15), new DateOnly(2030, 1, 16)],
            view.Options.Select(option => option.Date));

        // Catches omitted or incorrectly mapped attendee-facing window fields.
        Assert.Equal(
            [new TimeOnly(9, 0), new TimeOnly(11, 0), new TimeOnly(13, 0)],
            view.Options.Select(option => option.StartTime));
        Assert.Equal(
            [new TimeOnly(13, 0), new TimeOnly(15, 0), new TimeOnly(17, 0)],
            view.Options.Select(option => option.EndTime));
        Assert.Equal(
            [
                "Monday 14 Jan 2030, 09:00-13:00",
                "Tuesday 15 Jan 2030, 11:00-15:00",
                "Wednesday 16 Jan 2030, 13:00-17:00",
            ],
            view.Options.Select(option => option.Display));

        // Catches returning a domain event/capacity object instead of the attendee-facing projection.
        using var document = JsonDocument.Parse(json);
        var links = document.RootElement.GetProperty("_links");
        var confirm = links.GetProperty("confirm");
        Assert.Equal($"/api/booking/{Uri.EscapeDataString(invite.Token)}/confirm", confirm.GetProperty("href").GetString());
        Assert.Equal("POST", confirm.GetProperty("method").GetString());
        Assert.Equal("confirmBooking", confirm.GetProperty("operationId").GetString());
        Assert.DoesNotContain("tokenHash", json, StringComparison.OrdinalIgnoreCase);
        AssertNoPropertiesNamed(
            document.RootElement,
            "capacities",
            "remainingCapacity",
            "totalHeadcount",
            "headcount",
            "status",
            "id",
            "proposalId",
            "window",
            "acceptances",
            "createdByManagerUserId",
            "attendeeId",
            "tokenHash",
            "offeredEventIds",
            "expiresAt",
            "usedAt");
    }

    [Fact]
    public async Task AnUnknownTokenIsNotFoundWithTheGenericMessage()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/booking/nonsense");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("no longer valid", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheRouteNeedsNoAuthentication()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/booking/nonsense");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Seeds three deliberately non-ID-ordered events, a attendee and a pending invite.</summary>
    internal async Task<InviteFixture> GivenAnInvitedAttendee()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var offeredEvents = new[]
        {
            new InviteOptionFixture(
                Guid.Parse("00000000-0000-0000-0000-000000000050"), new DateOnly(2030, 1, 16), new TimeOnly(13, 0)),
            new InviteOptionFixture(
                Guid.Parse("00000000-0000-0000-0000-000000000010"), new DateOnly(2030, 1, 14), new TimeOnly(9, 0)),
            new InviteOptionFixture(
                Guid.Parse("00000000-0000-0000-0000-000000000090"), new DateOnly(2030, 1, 15), new TimeOnly(11, 0)),
        };

        foreach (var offeredEvent in offeredEvents)
        {
            var proposal = ProposalFixture.Create(
                Guid.NewGuid(),
                new EventWindow(offeredEvent.Date, offeredEvent.StartTime, 240),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            var eventItem = Event.CreateFrom(offeredEvent.Id, proposal);
            context.EventProposals.Add(proposal);
            context.Events.Add(eventItem);
        }

        var pilots = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", $"{Guid.NewGuid():N}@mail.com", pilots);
        attendee.MarkInvited();
        context.Attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        context.Invites.Add(Invite.CreateInitial(
            inviteId, attendee.Id, issued.TokenHash, DateTimeOffset.UtcNow.AddDays(4),
            offeredEvents.Select(eventItem => eventItem.Id).ToList(),
            attendee.RequiredAppointmentTypeIds, 0));

        await context.SaveChangesAsync();

        return new InviteFixture(issued.Token, inviteId);
    }

    private static void AssertNoPropertiesNamed(JsonElement element, params string[] forbiddenNames)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                Assert.DoesNotContain(property.Name, forbiddenNames);
                AssertNoPropertiesNamed(property.Value, forbiddenNames);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                AssertNoPropertiesNamed(item, forbiddenNames);
            }
        }
    }

    internal sealed record InviteFixture(string Token, Guid Id);

    internal sealed record InviteOptionFixture(Guid Id, DateOnly Date, TimeOnly StartTime);

    internal sealed record InviteOptionResponse(
        Guid EventId,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        string Display);

    internal sealed record InviteResponse(
        Guid InviteId,
        string AttendeeName,
        IReadOnlyList<string> AppointmentTypeNames,
        IReadOnlyList<InviteOptionResponse> Options);
}
`````

## after — tests/EventBooking.Api.Tests/BookingEndpointTests.cs — 1/1

<!-- retirement-file: {"id":26,"file":"tests/EventBooking.Api.Tests/BookingEndpointTests.cs","beforeSha":"e8e4d04f6dc2991764ff1f61ba1e1458eb20f08bf05faf011b4d8a4cbc5c05d5","afterSha":"ae1535aa5b307c2cbd8c1e77b6f5cd9794c5ed85a24e22542a2779c234273cef","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using EventBooking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class BookingEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task AValidTokenReturnsOnlyTheAttendeeFacingOptionsEarliestFirst()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/booking/{invite.Token}");
        var json = await response.Content.ReadAsStringAsync();
        var view = JsonSerializer.Deserialize<InviteResponse>(json, JsonSerializerOptions.Web);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(view);

        // Catches an incomplete root projection that drops the invite or attendee identity.
        Assert.Equal(invite.Id, view!.InviteId);
        Assert.Equal("Amara Novak", view!.AttendeeName);

        // Catches projecting no appointment types or types from the wrong source.
        Assert.Equal(["Drug & Alcohol Testing", "Uniform Fitting"], view.AppointmentTypeNames);

        // Catches removing chronological ordering or ordering by the offered list or event ID.
        Assert.Equal(
            [
                Guid.Parse("00000000-0000-0000-0000-000000000010"),
                Guid.Parse("00000000-0000-0000-0000-000000000090"),
                Guid.Parse("00000000-0000-0000-0000-000000000050"),
            ],
            view.Options.Select(option => option.EventId));
        Assert.Equal(
            [new DateOnly(2030, 1, 14), new DateOnly(2030, 1, 15), new DateOnly(2030, 1, 16)],
            view.Options.Select(option => option.Date));

        // Catches omitted or incorrectly mapped attendee-facing window fields.
        Assert.Equal(
            [new TimeOnly(9, 0), new TimeOnly(11, 0), new TimeOnly(13, 0)],
            view.Options.Select(option => option.StartTime));
        Assert.Equal(
            [new TimeOnly(13, 0), new TimeOnly(15, 0), new TimeOnly(17, 0)],
            view.Options.Select(option => option.EndTime));
        Assert.Equal(
            [
                "Monday 14 Jan 2030, 09:00-13:00",
                "Tuesday 15 Jan 2030, 11:00-15:00",
                "Wednesday 16 Jan 2030, 13:00-17:00",
            ],
            view.Options.Select(option => option.Display));

        // Catches returning a domain event/capacity object instead of the attendee-facing projection.
        using var document = JsonDocument.Parse(json);
        var links = document.RootElement.GetProperty("_links");
        var confirm = links.GetProperty("confirm");
        Assert.Equal($"/api/booking/{Uri.EscapeDataString(invite.Token)}/confirm", confirm.GetProperty("href").GetString());
        Assert.Equal("POST", confirm.GetProperty("method").GetString());
        Assert.Equal("confirmBooking", confirm.GetProperty("operationId").GetString());
        Assert.DoesNotContain("tokenHash", json, StringComparison.OrdinalIgnoreCase);
        AssertNoPropertiesNamed(
            document.RootElement,
            "capacities",
            "remainingCapacity",
            "totalHeadcount",
            "headcount",
            "status",
            "id",
            "proposalId",
            "window",
            "acceptances",
            "createdByManagerUserId",
            "attendeeId",
            "tokenHash",
            "offeredEventIds",
            "expiresAt",
            "usedAt");
    }

    [Fact]
    public async Task AnUnknownTokenIsNotFoundWithTheGenericMessage()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/booking/nonsense");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("no longer valid", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheRouteNeedsNoAuthentication()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/booking/nonsense");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Seeds three deliberately non-ID-ordered events, a attendee and a pending invite.</summary>
    internal async Task<InviteFixture> GivenAnInvitedAttendee()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var offeredEvents = new[]
        {
            new InviteOptionFixture(
                Guid.Parse("00000000-0000-0000-0000-000000000050"), new DateOnly(2030, 1, 16), new TimeOnly(13, 0)),
            new InviteOptionFixture(
                Guid.Parse("00000000-0000-0000-0000-000000000010"), new DateOnly(2030, 1, 14), new TimeOnly(9, 0)),
            new InviteOptionFixture(
                Guid.Parse("00000000-0000-0000-0000-000000000090"), new DateOnly(2030, 1, 15), new TimeOnly(11, 0)),
        };

        foreach (var offeredEvent in offeredEvents)
        {
            var proposal = ProposalFixture.Create(
                Guid.NewGuid(),
                new EventWindow(offeredEvent.Date, offeredEvent.StartTime, 240),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            var eventItem = Event.CreateFrom(offeredEvent.Id, proposal);
            context.EventProposals.Add(proposal);
            context.Events.Add(eventItem);
        }

        var pilots = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            $"{Guid.NewGuid():N}@mail.com",
            pilots,
            ProposalFixture.Now);
        attendee.MarkInvited(ProposalFixture.Now);
        context.Attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        context.Invites.Add(Invite.CreateInitial(
            inviteId,
            attendee.Id,
            issued.TokenHash,
            DateTimeOffset.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            offeredEvents.Select(eventItem => eventItem.Id).ToList(),
            attendee.RequiredAppointmentTypeIds,
            0));

        await context.SaveChangesAsync();

        return new InviteFixture(issued.Token, inviteId);
    }

    private static void AssertNoPropertiesNamed(JsonElement element, params string[] forbiddenNames)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                Assert.DoesNotContain(property.Name, forbiddenNames);
                AssertNoPropertiesNamed(property.Value, forbiddenNames);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                AssertNoPropertiesNamed(item, forbiddenNames);
            }
        }
    }

    internal sealed record InviteFixture(string Token, Guid Id);

    internal sealed record InviteOptionFixture(Guid Id, DateOnly Date, TimeOnly StartTime);

    internal sealed record InviteOptionResponse(
        Guid EventId,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        string Display);

    internal sealed record InviteResponse(
        Guid InviteId,
        string AttendeeName,
        IReadOnlyList<string> AppointmentTypeNames,
        IReadOnlyList<InviteOptionResponse> Options);
}
`````

## before — tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs — 1/1

<!-- retirement-file: {"id":27,"file":"tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs","beforeSha":"6827350b3ff4f99bdad2e5f99578e1c43afaa36dfffe526a70886e1af25f3f2e","afterSha":"f7e57811f9dc132698f6e00fe646e293d11f64089fa18d291731971bc5ac4a20","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using EventBooking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

/// <summary>Verifies booking confirmation delivery outcomes at the HTTP boundary.</summary>
[Collection("api")]
public class ConfirmBookingEndpointTests(ApiFactory factory)
{
    /// <summary>A attendee can confirm one of the offered events.</summary>
    [Fact]
    public async Task AAttendeeCanConfirmOneOfTheirOptions()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");
        var chosen = view!.Options[1];

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = chosen.EventId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();
        Assert.NotEqual(Guid.Empty, outcome!.BookingId);
        Assert.Equal(chosen.Date, outcome.Date);
        Assert.Equal(chosen.StartTime, outcome.StartTime);
        Assert.Equal(chosen.EndTime, outcome.EndTime);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ManageToken));
        Assert.Equal("Sent", outcome.DeliveryStatus);
    }

    /// <summary>Booking confirmation remains successful while a provider rejection is reported.</summary>
    [Fact]
    public async Task AProviderFailureReturnsAConfirmedBookingAndFailedDeliveryStatus()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.EmailTransport.FailNextSend = true;
        factory.SignedInAs = null;
        var client = factory.CreateClient();
        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm",
            new { EventId = view!.Options[0].EventId });
        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Failed", outcome!.DeliveryStatus);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ManageToken));
    }

    /// <summary>The same confirmation link cannot be consumed twice.</summary>
    [Fact]
    public async Task TheSameLinkCannotBeUsedTwice()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");
        var chosen = view!.Options[0].EventId;

        var first = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = chosen });
        var second = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = chosen });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    /// <summary>A event absent from the invitation is rejected as a conflict.</summary>
    [Fact]
    public async Task ChoosingAEventThatWasNeverOfferedIsAConflict()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = invite.UnofferedEventId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private sealed record ConfirmResponse(
        Guid BookingId,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        string ManageToken,
        string DeliveryStatus);

    private async Task<InviteFixture> GivenAnInvitedAttendee()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var offeredEventIds = new List<Guid>();

        foreach (var (date, startTime) in new[]
                 {
                     (new DateOnly(2030, 1, 14), new TimeOnly(9, 0)),
                     (new DateOnly(2030, 1, 15), new TimeOnly(11, 0)),
                     (new DateOnly(2030, 1, 16), new TimeOnly(13, 0)),
                 })
        {
            var proposal = ProposalFixture.Create(
                Guid.NewGuid(), new EventWindow(date, startTime, 240), Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            var eventId = Guid.NewGuid();
            context.EventProposals.Add(proposal);
            context.Events.Add(Event.CreateFrom(eventId, proposal));
            offeredEventIds.Add(eventId);
        }

        var unofferedProposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2030, 1, 17), new TimeOnly(9, 0), 240), Guid.NewGuid());
        unofferedProposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        unofferedProposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        unofferedProposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var unofferedEventId = Guid.NewGuid();
        context.EventProposals.Add(unofferedProposal);
        context.Events.Add(Event.CreateFrom(unofferedEventId, unofferedProposal));

        var pilots = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", $"{Guid.NewGuid():N}@mail.com", pilots);
        attendee.MarkInvited();
        context.Attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        context.Invites.Add(Invite.CreateInitial(
            inviteId,
            attendee.Id,
            issued.TokenHash,
            new DateTimeOffset(2030, 1, 20, 0, 0, 0, TimeSpan.Zero),
            offeredEventIds,
            attendee.RequiredAppointmentTypeIds, 0));
        await context.SaveChangesAsync();

        return new InviteFixture(issued.Token, unofferedEventId);
    }

    private sealed record InviteFixture(string Token, Guid UnofferedEventId);
}
`````

## after — tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs — 1/1

<!-- retirement-file: {"id":27,"file":"tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs","beforeSha":"6827350b3ff4f99bdad2e5f99578e1c43afaa36dfffe526a70886e1af25f3f2e","afterSha":"f7e57811f9dc132698f6e00fe646e293d11f64089fa18d291731971bc5ac4a20","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using EventBooking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

/// <summary>Verifies booking confirmation delivery outcomes at the HTTP boundary.</summary>
[Collection("api")]
public class ConfirmBookingEndpointTests(ApiFactory factory)
{
    /// <summary>A attendee can confirm one of the offered events.</summary>
    [Fact]
    public async Task AAttendeeCanConfirmOneOfTheirOptions()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");
        var chosen = view!.Options[1];

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = chosen.EventId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();
        Assert.NotEqual(Guid.Empty, outcome!.BookingId);
        Assert.Equal(chosen.Date, outcome.Date);
        Assert.Equal(chosen.StartTime, outcome.StartTime);
        Assert.Equal(chosen.EndTime, outcome.EndTime);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ManageToken));
        Assert.Equal("Sent", outcome.DeliveryStatus);
    }

    /// <summary>Booking confirmation remains successful while a provider rejection is reported.</summary>
    [Fact]
    public async Task AProviderFailureReturnsAConfirmedBookingAndFailedDeliveryStatus()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.EmailTransport.FailNextSend = true;
        factory.SignedInAs = null;
        var client = factory.CreateClient();
        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm",
            new { EventId = view!.Options[0].EventId });
        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Failed", outcome!.DeliveryStatus);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ManageToken));
    }

    /// <summary>The same confirmation link cannot be consumed twice.</summary>
    [Fact]
    public async Task TheSameLinkCannotBeUsedTwice()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");
        var chosen = view!.Options[0].EventId;

        var first = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = chosen });
        var second = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = chosen });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    /// <summary>A event absent from the invitation is rejected as a conflict.</summary>
    [Fact]
    public async Task ChoosingAEventThatWasNeverOfferedIsAConflict()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = invite.UnofferedEventId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private sealed record ConfirmResponse(
        Guid BookingId,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        string ManageToken,
        string DeliveryStatus);

    private async Task<InviteFixture> GivenAnInvitedAttendee()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var offeredEventIds = new List<Guid>();

        foreach (var (date, startTime) in new[]
                 {
                     (new DateOnly(2030, 1, 14), new TimeOnly(9, 0)),
                     (new DateOnly(2030, 1, 15), new TimeOnly(11, 0)),
                     (new DateOnly(2030, 1, 16), new TimeOnly(13, 0)),
                 })
        {
            var proposal = ProposalFixture.Create(
                Guid.NewGuid(), new EventWindow(date, startTime, 240), Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            var eventId = Guid.NewGuid();
            context.EventProposals.Add(proposal);
            context.Events.Add(Event.CreateFrom(eventId, proposal));
            offeredEventIds.Add(eventId);
        }

        var unofferedProposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2030, 1, 17), new TimeOnly(9, 0), 240), Guid.NewGuid());
        unofferedProposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        unofferedProposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        unofferedProposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var unofferedEventId = Guid.NewGuid();
        context.EventProposals.Add(unofferedProposal);
        context.Events.Add(Event.CreateFrom(unofferedEventId, unofferedProposal));

        var pilots = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            $"{Guid.NewGuid():N}@mail.com",
            pilots,
            ProposalFixture.Now);
        attendee.MarkInvited(ProposalFixture.Now);
        context.Attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        context.Invites.Add(Invite.CreateInitial(
            inviteId,
            attendee.Id,
            issued.TokenHash,
            new DateTimeOffset(2030, 1, 20, 0, 0, 0, TimeSpan.Zero),
            [ProposalFixture.LocationId],
            offeredEventIds,
            attendee.RequiredAppointmentTypeIds,
            0));
        await context.SaveChangesAsync();

        return new InviteFixture(issued.Token, unofferedEventId);
    }

    private sealed record InviteFixture(string Token, Guid UnofferedEventId);
}
`````

## before — tests/EventBooking.Api.Tests/DashboardEndpointTests.cs — 1/1

<!-- retirement-file: {"id":28,"file":"tests/EventBooking.Api.Tests/DashboardEndpointTests.cs","beforeSha":"c7c5ed46d9adb3198a2cd43762e4238824d3dcfb670d1ebd40806e7dec1a0413","afterSha":"d085fd6e9f2225b46c0d3197c89edb47951dd72227617bc4e5f51bb3398ed8e7","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class DashboardEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task ACoordinatorGetsAllThreeViewsInOneResponse()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboards");
        var dashboards = await response.Content.ReadFromJsonAsync<DashboardsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(dashboards);
        Assert.NotNull(dashboards!.AwaitingAvailability);
        Assert.NotNull(dashboards.NoResponse);
        Assert.NotNull(dashboards.Events);
    }

    [Fact]
    public async Task AnAdminIsForbiddenTheDashboards()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboards");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AManagerIsForbidden()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.MedicalCheckUp);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboards");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnUnauthenticatedCallerIsChallenged()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboards");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The Dashboards page binds to Email, RequiredCodes, WaitingSince/DaysWaiting, GaveUpOn and the
    /// event Capacities list. Earlier coverage only asserted the row lists were non-null, so a DTO
    /// field could be renamed or dropped without failing a test — the page would simply render blank
    /// cells. This seeds one row of each kind and checks every field the page actually reads.
    /// </summary>
    [Fact]
    public async Task TheResponseCarriesEveryFieldThePageBindsTo()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var awaitingId = Guid.NewGuid();
        var noResponseId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

            var cabinCrew = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.CabinCrew);
            var awaiting = Attendee.Create(
                awaitingId, "A. Waiting", "a.waiting@mail.com", cabinCrew);
            awaiting.MarkAwaitingAvailability();
            context.Attendees.Add(awaiting);

            var groundOps = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.GroundOperationsAgent);
            var noResponse = Attendee.Create(
                noResponseId, "B. Stuck", "b.stuck@mail.com", groundOps);
            noResponse.MarkInvited();
            noResponse.MarkNoResponse();
            context.Attendees.Add(noResponse);

            var proposal = ProposalFixture.Create(
                Guid.NewGuid(),
                new EventWindow(today.AddDays(30), new TimeOnly(9, 0), 240),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
            var eventItem = Event.CreateFrom(eventId, proposal);
            eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
            context.EventProposals.Add(proposal);
            context.Events.Add(eventItem);

            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var dashboards = await client.GetFromJsonAsync<DashboardsResponse>("/api/dashboards");

        Assert.NotNull(dashboards);

        var awaitingRow = Assert.Single(dashboards!.AwaitingAvailability, r => r.AttendeeId == awaitingId);
        Assert.Equal("A. Waiting", awaitingRow.Name);
        Assert.Equal("a.waiting@mail.com", awaitingRow.Email);
        Assert.Equal(new[] { "DAT", "MED", "UNI" }, awaitingRow.RequiredCodes);
        Assert.Equal(today, awaitingRow.WaitingSince);
        Assert.Equal(0, awaitingRow.DaysWaiting);

        var noResponseRow = Assert.Single(dashboards.NoResponse, r => r.AttendeeId == noResponseId);
        Assert.Equal("B. Stuck", noResponseRow.Name);
        Assert.Equal("b.stuck@mail.com", noResponseRow.Email);
        Assert.Equal(new[] { "MED" }, noResponseRow.RequiredCodes);
        Assert.Equal(today, noResponseRow.GaveUpOn);

        var eventRow = Assert.Single(dashboards.Events, s => s.EventId == eventId);
        Assert.Equal(today.AddDays(30), eventRow.Date);
        Assert.Equal(new TimeOnly(9, 0), eventRow.StartTime);
        Assert.Equal(new TimeOnly(13, 0), eventRow.EndTime);
        Assert.Equal(0, eventRow.ActiveBookings);
        Assert.Equal(new[] { "DAT", "MED", "UNI" }, eventRow.Capacities.Select(c => c.Code));
        var drugAndAlcohol = eventRow.Capacities.Single(c => c.Code == "DAT");
        Assert.Equal(10, drugAndAlcohol.TotalHeadcount);
        Assert.Equal(9, drugAndAlcohol.RemainingCapacity);
    }

    private sealed record RowResponse(
        Guid AttendeeId,
        string Name,
        string Email,
        IReadOnlyList<string> RequiredCodes,
        DateOnly? WaitingSince,
        int? DaysWaiting,
        DateOnly? GaveUpOn);

    private sealed record EventCapacityResponse(string Code, int TotalHeadcount, int RemainingCapacity);

    private sealed record EventResponse(
        Guid EventId,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        IReadOnlyList<EventCapacityResponse> Capacities,
        int ActiveBookings);

    private sealed record DashboardsResponse(
        IReadOnlyList<RowResponse> AwaitingAvailability,
        IReadOnlyList<RowResponse> NoResponse,
        IReadOnlyList<EventResponse> Events);
}
`````

## after — tests/EventBooking.Api.Tests/DashboardEndpointTests.cs — 1/1

<!-- retirement-file: {"id":28,"file":"tests/EventBooking.Api.Tests/DashboardEndpointTests.cs","beforeSha":"c7c5ed46d9adb3198a2cd43762e4238824d3dcfb670d1ebd40806e7dec1a0413","afterSha":"d085fd6e9f2225b46c0d3197c89edb47951dd72227617bc4e5f51bb3398ed8e7","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class DashboardEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task ACoordinatorGetsAllThreeViewsInOneResponse()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboards");
        var dashboards = await response.Content.ReadFromJsonAsync<DashboardsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(dashboards);
        Assert.NotNull(dashboards!.AwaitingAvailability);
        Assert.NotNull(dashboards.NoResponse);
        Assert.NotNull(dashboards.Events);
    }

    [Fact]
    public async Task AnAdminIsForbiddenTheDashboards()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboards");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AManagerIsForbidden()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.MedicalCheckUp);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboards");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnUnauthenticatedCallerIsChallenged()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboards");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The Dashboards page binds to Email, RequiredCodes, WaitingSince/DaysWaiting, GaveUpOn and the
    /// event Capacities list. Earlier coverage only asserted the row lists were non-null, so a DTO
    /// field could be renamed or dropped without failing a test — the page would simply render blank
    /// cells. This seeds one row of each kind and checks every field the page actually reads.
    /// </summary>
    [Fact]
    public async Task TheResponseCarriesEveryFieldThePageBindsTo()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var stampedAt = DateTimeOffset.UtcNow;
        var awaitingId = Guid.NewGuid();
        var noResponseId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

            var cabinCrew = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.CabinCrew);
            var awaiting = Attendee.Create(
                awaitingId,
                "A. Waiting",
                "a.waiting@mail.com",
                cabinCrew,
                stampedAt);
            awaiting.MarkAwaitingAvailability(stampedAt);
            context.Attendees.Add(awaiting);

            var groundOps = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.GroundOperationsAgent);
            var noResponse = Attendee.Create(
                noResponseId,
                "B. Stuck",
                "b.stuck@mail.com",
                groundOps,
                stampedAt);
            noResponse.MarkInvited(stampedAt);
            noResponse.MarkNoResponse(stampedAt);
            context.Attendees.Add(noResponse);

            var proposal = ProposalFixture.Create(
                Guid.NewGuid(),
                new EventWindow(today.AddDays(30), new TimeOnly(9, 0), 240),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
            var eventItem = Event.CreateFrom(eventId, proposal);
            eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
            context.EventProposals.Add(proposal);
            context.Events.Add(eventItem);

            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var dashboards = await client.GetFromJsonAsync<DashboardsResponse>("/api/dashboards");

        Assert.NotNull(dashboards);

        var awaitingRow = Assert.Single(dashboards!.AwaitingAvailability, r => r.AttendeeId == awaitingId);
        Assert.Equal("A. Waiting", awaitingRow.Name);
        Assert.Equal("a.waiting@mail.com", awaitingRow.Email);
        Assert.Equal(new[] { "DAT", "MED", "UNI" }, awaitingRow.RequiredCodes);
        Assert.Equal(today, awaitingRow.WaitingSince);
        Assert.Equal(0, awaitingRow.DaysWaiting);

        var noResponseRow = Assert.Single(dashboards.NoResponse, r => r.AttendeeId == noResponseId);
        Assert.Equal("B. Stuck", noResponseRow.Name);
        Assert.Equal("b.stuck@mail.com", noResponseRow.Email);
        Assert.Equal(new[] { "MED" }, noResponseRow.RequiredCodes);
        Assert.Equal(today, noResponseRow.GaveUpOn);

        var eventRow = Assert.Single(dashboards.Events, s => s.EventId == eventId);
        Assert.Equal(today.AddDays(30), eventRow.Date);
        Assert.Equal(new TimeOnly(9, 0), eventRow.StartTime);
        Assert.Equal(new TimeOnly(13, 0), eventRow.EndTime);
        Assert.Equal(0, eventRow.ActiveBookings);
        Assert.Equal(new[] { "DAT", "MED", "UNI" }, eventRow.Capacities.Select(c => c.Code));
        var drugAndAlcohol = eventRow.Capacities.Single(c => c.Code == "DAT");
        Assert.Equal(10, drugAndAlcohol.TotalHeadcount);
        Assert.Equal(9, drugAndAlcohol.RemainingCapacity);
    }

    private sealed record RowResponse(
        Guid AttendeeId,
        string Name,
        string Email,
        IReadOnlyList<string> RequiredCodes,
        DateOnly? WaitingSince,
        int? DaysWaiting,
        DateOnly? GaveUpOn);

    private sealed record EventCapacityResponse(string Code, int TotalHeadcount, int RemainingCapacity);

    private sealed record EventResponse(
        Guid EventId,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        IReadOnlyList<EventCapacityResponse> Capacities,
        int ActiveBookings);

    private sealed record DashboardsResponse(
        IReadOnlyList<RowResponse> AwaitingAvailability,
        IReadOnlyList<RowResponse> NoResponse,
        IReadOnlyList<EventResponse> Events);
}
`````
