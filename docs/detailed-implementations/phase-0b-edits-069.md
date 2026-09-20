# 00b — Vocabulary edits 69 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — tests/EventBooking.Api.Tests/AuditEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":223,"oldPath":"tests/EventBooking.Api.Tests/AuditEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/AuditEndpointTests.cs","beforeSha":"3c505f046c94aaa0a6b9bdac0fdd2378e0bce751bdd6e4b83dc6bb2f24cfc7af","afterSha":"a3daadabfdf4e76617003dbf70528ce21bafa91fecb78ba57bdf4cd5f4d83d87","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
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
    public async Task ACoordinatorGetsASlotsHistoryWithEveryFieldThePanelBindsTo()
    {
        var slotId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            context.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, slotId, AuditAction.SlotConfirmed,
                ActorType.Staff, "staff-1", Now, "6 headcount total"));
            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var rows = await client.GetFromJsonAsync<List<RowResponse>>($"/api/audit/slot/{slotId}");

        var row = Assert.Single(rows!);
        Assert.Equal(Now, row.Timestamp);
        Assert.Equal(AuditEntityTypes.ConfirmedSlot, row.EntityType);
        Assert.Equal(slotId, row.EntityId);
        Assert.Equal("SlotConfirmed", row.Action);
        Assert.Equal("Staff", row.ActorType);
        Assert.Equal("staff-1", row.ActorId);
        Assert.Equal("6 headcount total", row.Details);
    }

    [Fact]
    public async Task ACoordinatorGetsACandidatesHistoryFromItsInvitesAndBookings()
    {
        var candidateId = Guid.NewGuid();
        var inviteId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var pilots = context.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
            var candidate = Candidate.Create(candidateId, "Amara Novak", "a.novak@mail.com", pilots);
            context.Candidates.Add(candidate);
            context.Invites.Add(Invite.CreateInitial(
                inviteId, candidate.Id, "hash", Now.AddDays(4),
                [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
                candidate.RequiredAppointmentTypeIds, 0));
            context.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Invite, inviteId, AuditAction.InviteCreated,
                ActorType.System, null, Now, "retry 0"));
            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var rows = await client.GetFromJsonAsync<List<RowResponse>>($"/api/audit/candidate/{candidateId}");

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

        var slotResponse = await client.GetAsync($"/api/audit/slot/{Guid.NewGuid()}");
        var candidateResponse = await client.GetAsync($"/api/audit/candidate/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, slotResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, candidateResponse.StatusCode);
    }

    [Fact]
    public async Task AnUnauthenticatedCallerIsChallenged()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/audit/slot/{Guid.NewGuid()}");

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
                Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, older, AuditAction.SlotConfirmed,
                ActorType.Staff, actorId, Now, null));
            context.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, newer, AuditAction.SlotConfirmed,
                ActorType.Staff, actorId, Now.AddHours(1), null));
            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<SearchPageResponse>(
            $"/api/audit/search?action=SlotConfirmed&actorType=Staff&identifier={actorId}&pageSize=10");

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
                Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, inRange, AuditAction.SlotConfirmed,
                ActorType.Staff, actorId, Now, null));
            context.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, Guid.NewGuid(), AuditAction.SlotConfirmed,
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
        var slotId = Guid.NewGuid();
        var actorId = $"search-actor-{Guid.NewGuid():N}";

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            context.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, slotId, AuditAction.SlotConfirmed,
                ActorType.Staff, actorId, Now, null));
            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<SearchPageResponse>(
            $"/api/audit/search?identifier={actorId}&cursor=not-valid-base64!!");

        Assert.NotNull(page);
        Assert.Contains(page!.Rows, r => r.EntityId == slotId);
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
    public async Task SearchNeverReturnsCandidateRowsToAnAdmin()
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

## after — tests/EventBooking.Api.Tests/AuditEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":223,"oldPath":"tests/EventBooking.Api.Tests/AuditEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/AuditEndpointTests.cs","beforeSha":"3c505f046c94aaa0a6b9bdac0fdd2378e0bce751bdd6e4b83dc6bb2f24cfc7af","afterSha":"a3daadabfdf4e76617003dbf70528ce21bafa91fecb78ba57bdf4cd5f4d83d87","side":"after","part":1,"parts":1} -->

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
            var attendee = Attendee.Create(attendeeId, "Amara Novak", "a.novak@mail.com", pilots);
            context.Attendees.Add(attendee);
            context.Invites.Add(Invite.CreateInitial(
                inviteId, attendee.Id, "hash", Now.AddDays(4),
                [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
                attendee.RequiredAppointmentTypeIds, 0));
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

## before — tests/EventBooking.Api.Tests/AuthorizationMatrixTests.cs — 1/1

<!-- vocabulary-file: {"id":224,"oldPath":"tests/EventBooking.Api.Tests/AuthorizationMatrixTests.cs","newPath":"tests/EventBooking.Api.Tests/AuthorizationMatrixTests.cs","beforeSha":"1a7458a3254cef1aa9cf1ee16b9b1542c8f0cda6ff87d0ee716ccbeb8b5b248d","afterSha":"8f5ae0a71f66ea315f9844c8db9493f499867b10b0d1df579875040ed70b4b92","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class AuthorizationMatrixTests(ApiFactory factory)
{
    public static TheoryData<Role, string, HttpStatusCode> Matrix => new()
    {
        { Role.Admin, "/api/candidates", HttpStatusCode.Forbidden },
        { Role.Admin, "/api/dashboards", HttpStatusCode.Forbidden },
        { Role.Admin, $"/api/audit/candidate/{Guid.NewGuid()}", HttpStatusCode.Forbidden },
        { Role.Admin, "/api/admin/settings", HttpStatusCode.OK },
        { Role.Coordinator, "/api/candidates", HttpStatusCode.OK },
        { Role.Coordinator, "/api/dashboards", HttpStatusCode.OK },
        { Role.Coordinator, "/api/admin/settings", HttpStatusCode.Forbidden },
        { Role.Manager, "/api/slots/board", HttpStatusCode.OK },
        { Role.AppointmentStaff, "/api/slots/board", HttpStatusCode.Forbidden },
        { Role.AppointmentStaff, "/api/candidates", HttpStatusCode.Forbidden },
    };

    public static TheoryData<Role[], string, HttpStatusCode> CombinedMatrix => new()
    {
        { [Role.Coordinator, Role.Manager], "/api/candidates", HttpStatusCode.OK },
        { [Role.Coordinator, Role.Manager], "/api/slots/board", HttpStatusCode.OK },
        { [Role.Coordinator, Role.AppointmentStaff], "/api/candidates", HttpStatusCode.OK },
        { [Role.Coordinator, Role.AppointmentStaff], "/api/slots/board", HttpStatusCode.Forbidden },
        { [Role.Manager, Role.AppointmentStaff], "/api/slots/board", HttpStatusCode.OK },
        { [Role.Manager, Role.AppointmentStaff], "/api/candidates", HttpStatusCode.Forbidden },
        { [Role.Coordinator, Role.Manager, Role.AppointmentStaff], "/api/candidates", HttpStatusCode.OK },
        { [Role.Coordinator, Role.Manager, Role.AppointmentStaff], "/api/slots/board", HttpStatusCode.OK },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task SingleRoleEndpointMatrix(
        Role role,
        string route,
        HttpStatusCode expected)
    {
        Guid? scope = role is Role.Manager or Role.AppointmentStaff
            ? AppointmentTypeIds.DrugAndAlcoholTesting
            : null;
        factory.SignedInAs = await factory.GivenStaffAsync(role, scope);

        var response = await factory.CreateClient().GetAsync(route);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(CombinedMatrix))]
    public async Task CombinedProfileEndpointMatrix(
        Role[] roles,
        string route,
        HttpStatusCode expected)
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            roles,
            AppointmentTypeIds.DrugAndAlcoholTesting);

        var response = await factory.CreateClient().GetAsync(route);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task UnassignedAndAnonymousCallersRemainProtected()
    {
        factory.SignedInAs = Guid.NewGuid();
        factory.RolesClaim = [];
        var unassigned = await factory.CreateClient().GetAsync("/api/candidates");

        factory.SignedInAs = null;
        var anonymous = await factory.CreateClient().GetAsync("/api/candidates");

        Assert.Equal(HttpStatusCode.Forbidden, unassigned.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }
}
`````

## after — tests/EventBooking.Api.Tests/AuthorizationMatrixTests.cs — 1/1

<!-- vocabulary-file: {"id":224,"oldPath":"tests/EventBooking.Api.Tests/AuthorizationMatrixTests.cs","newPath":"tests/EventBooking.Api.Tests/AuthorizationMatrixTests.cs","beforeSha":"1a7458a3254cef1aa9cf1ee16b9b1542c8f0cda6ff87d0ee716ccbeb8b5b248d","afterSha":"8f5ae0a71f66ea315f9844c8db9493f499867b10b0d1df579875040ed70b4b92","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class AuthorizationMatrixTests(ApiFactory factory)
{
    public static TheoryData<Role, string, HttpStatusCode> Matrix => new()
    {
        { Role.Admin, "/api/attendees", HttpStatusCode.Forbidden },
        { Role.Admin, "/api/dashboards", HttpStatusCode.Forbidden },
        { Role.Admin, $"/api/audit/attendee/{Guid.NewGuid()}", HttpStatusCode.Forbidden },
        { Role.Admin, "/api/admin/settings", HttpStatusCode.OK },
        { Role.Coordinator, "/api/attendees", HttpStatusCode.OK },
        { Role.Coordinator, "/api/dashboards", HttpStatusCode.OK },
        { Role.Coordinator, "/api/admin/settings", HttpStatusCode.Forbidden },
        { Role.Manager, "/api/events/board", HttpStatusCode.OK },
        { Role.AppointmentStaff, "/api/events/board", HttpStatusCode.Forbidden },
        { Role.AppointmentStaff, "/api/attendees", HttpStatusCode.Forbidden },
    };

    public static TheoryData<Role[], string, HttpStatusCode> CombinedMatrix => new()
    {
        { [Role.Coordinator, Role.Manager], "/api/attendees", HttpStatusCode.OK },
        { [Role.Coordinator, Role.Manager], "/api/events/board", HttpStatusCode.OK },
        { [Role.Coordinator, Role.AppointmentStaff], "/api/attendees", HttpStatusCode.OK },
        { [Role.Coordinator, Role.AppointmentStaff], "/api/events/board", HttpStatusCode.Forbidden },
        { [Role.Manager, Role.AppointmentStaff], "/api/events/board", HttpStatusCode.OK },
        { [Role.Manager, Role.AppointmentStaff], "/api/attendees", HttpStatusCode.Forbidden },
        { [Role.Coordinator, Role.Manager, Role.AppointmentStaff], "/api/attendees", HttpStatusCode.OK },
        { [Role.Coordinator, Role.Manager, Role.AppointmentStaff], "/api/events/board", HttpStatusCode.OK },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task SingleRoleEndpointMatrix(
        Role role,
        string route,
        HttpStatusCode expected)
    {
        Guid? scope = role is Role.Manager or Role.AppointmentStaff
            ? AppointmentTypeIds.DrugAndAlcoholTesting
            : null;
        factory.SignedInAs = await factory.GivenStaffAsync(role, scope);

        var response = await factory.CreateClient().GetAsync(route);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(CombinedMatrix))]
    public async Task CombinedProfileEndpointMatrix(
        Role[] roles,
        string route,
        HttpStatusCode expected)
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            roles,
            AppointmentTypeIds.DrugAndAlcoholTesting);

        var response = await factory.CreateClient().GetAsync(route);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task UnassignedAndAnonymousCallersRemainProtected()
    {
        factory.SignedInAs = Guid.NewGuid();
        factory.RolesClaim = [];
        var unassigned = await factory.CreateClient().GetAsync("/api/attendees");

        factory.SignedInAs = null;
        var anonymous = await factory.CreateClient().GetAsync("/api/attendees");

        Assert.Equal(HttpStatusCode.Forbidden, unassigned.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }
}
`````

## before — tests/EventBooking.Api.Tests/BookingEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":225,"oldPath":"tests/EventBooking.Api.Tests/BookingEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/BookingEndpointTests.cs","beforeSha":"a5961b374719335ddf48f7562ce3049739c4c1b07145cc167428cf8f6d9ef399","afterSha":"b9c7d090f50802f0edb0d77889797a43705694e8547c03a037e1273215942d93","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;
using Microsoft.EntityFrameworkCore;
using EventBooking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class BookingEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task AValidTokenReturnsOnlyTheCandidateFacingOptionsEarliestFirst()
    {
        var invite = await GivenAnInvitedCandidate();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/booking/{invite.Token}");
        var json = await response.Content.ReadAsStringAsync();
        var view = JsonSerializer.Deserialize<InviteResponse>(json, JsonSerializerOptions.Web);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(view);

        // Catches an incomplete root projection that drops the invite or candidate identity.
        Assert.Equal(invite.Id, view!.InviteId);
        Assert.Equal("Amara Novak", view!.CandidateName);

        // Catches projecting no appointment types or types from the wrong source.
        Assert.Equal(["Drug & Alcohol Testing", "Uniform Fitting"], view.AppointmentTypeNames);

        // Catches removing chronological ordering or ordering by the offered list or slot ID.
        Assert.Equal(
            [
                Guid.Parse("00000000-0000-0000-0000-000000000010"),
                Guid.Parse("00000000-0000-0000-0000-000000000090"),
                Guid.Parse("00000000-0000-0000-0000-000000000050"),
            ],
            view.Options.Select(option => option.ConfirmedSlotId));
        Assert.Equal(
            [new DateOnly(2030, 1, 14), new DateOnly(2030, 1, 15), new DateOnly(2030, 1, 16)],
            view.Options.Select(option => option.Date));

        // Catches omitted or incorrectly mapped candidate-facing window fields.
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

        // Catches returning a domain slot/capacity object instead of the candidate-facing projection.
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
            "candidateId",
            "tokenHash",
            "offeredSlotIds",
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

    /// <summary>Seeds three deliberately non-ID-ordered slots, a candidate and a pending invite.</summary>
    internal async Task<InviteFixture> GivenAnInvitedCandidate()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var offeredSlots = new[]
        {
            new InviteOptionFixture(
                Guid.Parse("00000000-0000-0000-0000-000000000050"), new DateOnly(2030, 1, 16), new TimeOnly(13, 0)),
            new InviteOptionFixture(
                Guid.Parse("00000000-0000-0000-0000-000000000010"), new DateOnly(2030, 1, 14), new TimeOnly(9, 0)),
            new InviteOptionFixture(
                Guid.Parse("00000000-0000-0000-0000-000000000090"), new DateOnly(2030, 1, 15), new TimeOnly(11, 0)),
        };

        foreach (var offeredSlot in offeredSlots)
        {
            var proposal = SlotProposal.Create(
                Guid.NewGuid(),
                new SlotWindow(offeredSlot.Date, offeredSlot.StartTime),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            var slot = ConfirmedSlot.CreateFrom(offeredSlot.Id, proposal);
            context.SlotProposals.Add(proposal);
            context.ConfirmedSlots.Add(slot);
        }

        var pilots = context.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", $"{Guid.NewGuid():N}@mail.com", pilots);
        candidate.MarkInvited();
        context.Candidates.Add(candidate);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        context.Invites.Add(Invite.CreateInitial(
            inviteId, candidate.Id, issued.TokenHash, DateTimeOffset.UtcNow.AddDays(4),
            offeredSlots.Select(slot => slot.Id).ToList(),
            candidate.RequiredAppointmentTypeIds, 0));

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
        Guid ConfirmedSlotId,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        string Display);

    internal sealed record InviteResponse(
        Guid InviteId,
        string CandidateName,
        IReadOnlyList<string> AppointmentTypeNames,
        IReadOnlyList<InviteOptionResponse> Options);
}
`````

## after — tests/EventBooking.Api.Tests/BookingEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":225,"oldPath":"tests/EventBooking.Api.Tests/BookingEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/BookingEndpointTests.cs","beforeSha":"a5961b374719335ddf48f7562ce3049739c4c1b07145cc167428cf8f6d9ef399","afterSha":"b9c7d090f50802f0edb0d77889797a43705694e8547c03a037e1273215942d93","side":"after","part":1,"parts":1} -->

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
            var proposal = EventProposal.Create(
                Guid.NewGuid(),
                new EventWindow(offeredEvent.Date, offeredEvent.StartTime),
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

## before — tests/EventBooking.Api.Tests/CandidateBookingCancellationEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":226,"oldPath":"tests/EventBooking.Api.Tests/CandidateBookingCancellationEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/AttendeeBookingCancellationEndpointTests.cs","beforeSha":"091015b7821a42cc2006249956fd508fda1cee5fa47d9bde690c36bbe4708ec5","afterSha":"5f09c6d5cd4aa99a8a4e47623816acd3ee271b31c4e1835605caf0cea7e0d744","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

/// <summary>Verifies the staff booking-cancellation and active-booking-listing routes and their auth.</summary>
[Collection("api")]
public sealed class CandidateBookingCancellationEndpointTests(ApiFactory factory)
{
    private sealed record CancelResponse(
        bool Reinvited, bool InviteCreated, string? DeliveryStatus, Guid? DeliveryId);

    private sealed record BookingRow(
        Guid BookingId, bool IsOriginal, DateOnly SlotDate, TimeOnly SlotStartTime, TimeOnly SlotEndTime);

    [Fact]
    public async Task CoordinatorCanCancelAnOriginalBooking()
    {
        var (candidateId, bookingId) = await GivenBookedCandidateAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/candidates/{candidateId}/bookings/{bookingId}/cancel", new { Rebook = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.False(body!.Reinvited);
        Assert.False(body.InviteCreated);
        Assert.Equal(BookingStatus.Cancelled, await StatusOfAsync(bookingId));
    }

    [Fact]
    public async Task CoordinatorCanCancelAndRebookAnOriginalBooking()
    {
        var (candidateId, bookingId) = await GivenBookedCandidateAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/candidates/{candidateId}/bookings/{bookingId}/cancel", new { Rebook = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.True(body!.Reinvited);
        Assert.True(body.InviteCreated);
        Assert.NotNull(body.DeliveryStatus);
        Assert.Equal(BookingStatus.Cancelled, await StatusOfAsync(bookingId));
    }

    [Fact]
    public async Task RebookTrueOnARecoveryBookingIsAConflict()
    {
        var (candidateId, originalId) = await GivenBookedCandidateAsync();
        var recoveryId = await GivenActiveRecoveryAsync(candidateId, originalId);
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/candidates/{candidateId}/bookings/{recoveryId}/cancel", new { Rebook = true });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("conflict", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(BookingStatus.Active, await StatusOfAsync(recoveryId));
    }

    [Fact]
    public async Task UnknownBookingIdIsNotFound()
    {
        var (candidateId, _) = await GivenBookedCandidateAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/candidates/{candidateId}/bookings/{Guid.NewGuid()}/cancel", new { Rebook = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("AppointmentStaff")]
    [InlineData("Admin")]
    public async Task NonCoordinatorRolesAreForbidden(string role)
    {
        var (candidateId, bookingId) = await GivenBookedCandidateAsync();
        factory.SignedInAs = role switch
        {
            "Manager" => await factory.GivenStaffAsync(Role.Manager, AppointmentTypeIds.UniformFitting),
            "AppointmentStaff" => await factory.GivenStaffAsync(
                Role.AppointmentStaff, AppointmentTypeIds.UniformFitting),
            _ => await factory.GivenStaffAsync(Role.Admin),
        };
        var client = factory.CreateClient();

        using var cancel = await client.PostAsJsonAsync(
            $"/api/candidates/{candidateId}/bookings/{bookingId}/cancel", new { Rebook = false });
        using var list = await client.GetAsync($"/api/candidates/{candidateId}/bookings");

        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(BookingStatus.Active, await StatusOfAsync(bookingId));
    }

    [Fact]
    public async Task AnUnassignedProfileIsForbidden()
    {
        var (candidateId, bookingId) = await GivenBookedCandidateAsync();
        factory.SignedInAs = Guid.NewGuid();
        factory.RolesClaim = [];
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/candidates/{candidateId}/bookings/{bookingId}/cancel", new { Rebook = false });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousCallerIsUnauthorized()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        using var cancel = await client.PostAsJsonAsync(
            $"/api/candidates/{Guid.NewGuid()}/bookings/{Guid.NewGuid()}/cancel", new { Rebook = false });
        using var list = await client.GetAsync($"/api/candidates/{Guid.NewGuid()}/bookings");

        Assert.Equal(HttpStatusCode.Unauthorized, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
    }

    [Fact]
    public async Task CoordinatorCanListActiveBookings()
    {
        var (candidateId, originalId) = await GivenBookedCandidateAsync();
        var recoveryId = await GivenActiveRecoveryAsync(candidateId, originalId);
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var rows = await client.GetFromJsonAsync<List<BookingRow>>(
            $"/api/candidates/{candidateId}/bookings");

        Assert.NotNull(rows);
        Assert.Equal(2, rows!.Count);
        Assert.True(rows[0].IsOriginal);
        Assert.Equal(originalId, rows[0].BookingId);
        Assert.False(rows[1].IsOriginal);
        Assert.Equal(recoveryId, rows[1].BookingId);
        Assert.Equal(rows[0].SlotStartTime.AddHours(4), rows[0].SlotEndTime);
    }

    [Fact]
    public async Task ListingForAnUnknownCandidateIsNotFound()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/candidates/{Guid.NewGuid()}/bookings");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<BookingStatus> StatusOfAsync(Guid bookingId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        return await context.Bookings
            .AsNoTracking()
            .Where(b => b.Id == bookingId)
            .Select(b => b.Status)
            .SingleAsync();
    }

    /// <summary>Seeds a candidate holding one active original booking plus spare future slots.</summary>
    private async Task<(Guid CandidateId, Guid BookingId)> GivenBookedCandidateAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtHeadOffice;

        var bookedSlot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(today.AddDays(30), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var spareSlots = new[] { new TimeOnly(11, 0), new TimeOnly(13, 0), new TimeOnly(15, 0) }
            .Select(start => ConfirmedSlot.CreateImported(
                Guid.NewGuid(), new SlotWindow(today.AddDays(31), start),
                AppointmentTypeIds.All.ToDictionary(id => id, _ => 20)))
            .ToList();

        var group = context.EmployeeGroups
            .Include(g => g.Requirements)
            .Single(g => g.Id == EmployeeGroupIds.GroundOperationsAgent);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [bookedSlot.Id, spareSlots[0].Id, spareSlots[1].Id],
            candidate.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, bookedSlot.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);

        candidate.MarkInvited();
        invite.MarkUsed();
        candidate.MarkBooked();

        context.AddRange(bookedSlot);
        context.AddRange(spareSlots);
        context.AddRange(candidate, invite, booking);
        foreach (var typeId in candidate.RequiredAppointmentTypeIds)
        {
            context.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
            bookedSlot.CapacityFor(typeId).Decrement();
        }

        await context.SaveChangesAsync();
        return (candidate.Id, booking.Id);
    }

    /// <summary>Seeds one active recovery booking on a later slot for an existing original.</summary>
    private async Task<Guid> GivenActiveRecoveryAsync(Guid candidateId, Guid originalId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtHeadOffice;

        var recoverySlot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(today.AddDays(40), new TimeOnly(13, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var original = await context.Bookings.SingleAsync(b => b.Id == originalId);
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), candidateId, originalId, $"recovery-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(2),
            [recoverySlot.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);
        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoverySlot.Id,
            $"manage-recovery-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddHours(1));
        recoveryInvite.MarkUsed();

        context.Add(recoverySlot);
        context.AddRange(recoveryInvite, recovery);
        context.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.MedicalCheckUp));
        recoverySlot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();

        await context.SaveChangesAsync();
        return recovery.Id;
    }
}
`````
