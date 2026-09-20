# 01e — Location-restricted invites and closed attendee transitions, edits 13 (Task 8)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — tests/EventBooking.Api.Tests/EventEndpointTests.cs — 1/1

<!-- retirement-file: {"id":29,"file":"tests/EventBooking.Api.Tests/EventEndpointTests.cs","beforeSha":"765f1ffe66dd5b23c1c22a411c49648eb663f6dac5e7fd89854a05982a726254","afterSha":"3731f8f5b82c279a56ff3ff6806884844ce5bda074d8a9dd75f3cf5665037fd6","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class EventEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task AnAnonymousCallerIsChallenged()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/events/board");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ASignedInUserWithNoRoleAssignmentIsForbidden()
    {
        factory.SignedInAs = Guid.NewGuid();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/events/board");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AManagerCanProposeAEventAndSeeItOnTheBoard()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        var created = await client.PostAsJsonAsync(
            "/api/event-proposals",
            new { Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), StartTime = new TimeOnly(9, 0) });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var board = await client.GetFromJsonAsync<BoardResponse>("/api/events/board");
        Assert.NotNull(board);
        Assert.Contains(board!.OpenProposals, p => p.StartTime == new TimeOnly(9, 0));
    }

    [Fact]
    public async Task AWindowInThePastIsRejectedWithFourHundred()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.MedicalCheckUp);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/event-proposals",
            new { Date = new DateOnly(2020, 1, 1), StartTime = new TimeOnly(9, 0) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ACoordinatorGetsForbiddenFromAManagerRoute()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/events/board");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    [Fact]
    public async Task AnAdminGetsEventRowsAndNoAttendeeData()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        await GivenEventAsync();
        var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/events/operations");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("eventId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("attendeeId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ACoordinatorGetsTheSameEventOperationsView()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var eventId = await GivenEventAsync();
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<EventOperationsResponse>("/api/events/operations");

        Assert.NotNull(view);
        Assert.Contains(view!.Events, eventItem => eventItem.EventId == eventId);
    }

    [Fact]
    public async Task ASignedInUserWithNoRoleAssignmentIsForbiddenFromEventOperations()
    {
        factory.SignedInAs = Guid.NewGuid();
        factory.RolesClaim = [];
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/events/operations");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TheTwoStageCancellationProtocolIsUnchangedForAnAdmin()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var eventId = await GivenEventWithOneBookingAsync();
        var client = factory.CreateClient();

        using var first = await client.DeleteAsync($"/api/events/{eventId}?confirm=false");
        Assert.Equal(HttpStatusCode.Conflict, first.StatusCode);

        using var second = await client.DeleteAsync($"/api/events/{eventId}?confirm=true");
        Assert.True(second.IsSuccessStatusCode, await second.Content.ReadAsStringAsync());
    }

    /// <summary>Seeds one event with capacity for every appointment type.</summary>
    private async Task<Guid> GivenEventAsync()
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        context.EventProposals.Add(proposal);
        context.Events.Add(eventItem);
        await context.SaveChangesAsync();
        return eventItem.Id;
    }

    /// <summary>Seeds one event holding a single active booking, so the cascade gate trips.</summary>
    private async Task<Guid> GivenEventWithOneBookingAsync()
    {
        var eventId = await GivenEventAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var pilots = await context.AttendeeGroups
            .Include(g => g.Requirements)
            .SingleAsync(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "S. Booked", $"s.booked.{Guid.NewGuid():N}@mail.com", pilots);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            $"hash-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(4),
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventId, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        attendee.MarkInvited();
        attendee.MarkBooked();

        // Mirrors ConfirmBookingHandler: one appointment per required type, each holding a place.
        var eventItem = await context.Events
            .Include(s => s.Capacities)
            .SingleAsync(s => s.Id == eventId);

        context.Attendees.Add(attendee);
        context.Invites.Add(invite);
        context.Bookings.Add(booking);
        foreach (var appointmentTypeId in attendee.RequiredAppointmentTypeIds)
        {
            context.BookingAppointments.Add(
                BookingAppointment.Create(Guid.NewGuid(), booking.Id, appointmentTypeId));
            eventItem.CapacityFor(appointmentTypeId).Decrement();
        }

        await context.SaveChangesAsync();
        return eventId;
    }

    private sealed record EventOperationsResponse(IReadOnlyList<EventOperationsRow> Events);

    private sealed record EventOperationsRow(Guid EventId, DateOnly Date, int ActiveBookings);

    private sealed record BoardResponse(IReadOnlyList<OpenProposalResponse> OpenProposals);

    private sealed record OpenProposalResponse(Guid ProposalId, DateOnly Date, TimeOnly StartTime);
}
`````

## after — tests/EventBooking.Api.Tests/EventEndpointTests.cs — 1/1

<!-- retirement-file: {"id":29,"file":"tests/EventBooking.Api.Tests/EventEndpointTests.cs","beforeSha":"765f1ffe66dd5b23c1c22a411c49648eb663f6dac5e7fd89854a05982a726254","afterSha":"3731f8f5b82c279a56ff3ff6806884844ce5bda074d8a9dd75f3cf5665037fd6","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class EventEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task AnAnonymousCallerIsChallenged()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/events/board");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ASignedInUserWithNoRoleAssignmentIsForbidden()
    {
        factory.SignedInAs = Guid.NewGuid();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/events/board");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AManagerCanProposeAEventAndSeeItOnTheBoard()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        var created = await client.PostAsJsonAsync(
            "/api/event-proposals",
            new { Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), StartTime = new TimeOnly(9, 0) });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var board = await client.GetFromJsonAsync<BoardResponse>("/api/events/board");
        Assert.NotNull(board);
        Assert.Contains(board!.OpenProposals, p => p.StartTime == new TimeOnly(9, 0));
    }

    [Fact]
    public async Task AWindowInThePastIsRejectedWithFourHundred()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.MedicalCheckUp);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/event-proposals",
            new { Date = new DateOnly(2020, 1, 1), StartTime = new TimeOnly(9, 0) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ACoordinatorGetsForbiddenFromAManagerRoute()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/events/board");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    [Fact]
    public async Task AnAdminGetsEventRowsAndNoAttendeeData()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        await GivenEventAsync();
        var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/events/operations");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("eventId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("attendeeId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ACoordinatorGetsTheSameEventOperationsView()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var eventId = await GivenEventAsync();
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<EventOperationsResponse>("/api/events/operations");

        Assert.NotNull(view);
        Assert.Contains(view!.Events, eventItem => eventItem.EventId == eventId);
    }

    [Fact]
    public async Task ASignedInUserWithNoRoleAssignmentIsForbiddenFromEventOperations()
    {
        factory.SignedInAs = Guid.NewGuid();
        factory.RolesClaim = [];
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/events/operations");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TheTwoStageCancellationProtocolIsUnchangedForAnAdmin()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var eventId = await GivenEventWithOneBookingAsync();
        var client = factory.CreateClient();

        using var first = await client.DeleteAsync($"/api/events/{eventId}?confirm=false");
        Assert.Equal(HttpStatusCode.Conflict, first.StatusCode);

        using var second = await client.DeleteAsync($"/api/events/{eventId}?confirm=true");
        Assert.True(second.IsSuccessStatusCode, await second.Content.ReadAsStringAsync());
    }

    /// <summary>Seeds one event with capacity for every appointment type.</summary>
    private async Task<Guid> GivenEventAsync()
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        context.EventProposals.Add(proposal);
        context.Events.Add(eventItem);
        await context.SaveChangesAsync();
        return eventItem.Id;
    }

    /// <summary>Seeds one event holding a single active booking, so the cascade gate trips.</summary>
    private async Task<Guid> GivenEventWithOneBookingAsync()
    {
        var eventId = await GivenEventAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var pilots = await context.AttendeeGroups
            .Include(g => g.Requirements)
            .SingleAsync(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "S. Booked",
            $"s.booked.{Guid.NewGuid():N}@mail.com",
            pilots,
            ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            $"hash-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventId, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        attendee.MarkInvited(ProposalFixture.Now);
        attendee.MarkBooked(ProposalFixture.Now);

        // Mirrors ConfirmBookingHandler: one appointment per required type, each holding a place.
        var eventItem = await context.Events
            .Include(s => s.Capacities)
            .SingleAsync(s => s.Id == eventId);

        context.Attendees.Add(attendee);
        context.Invites.Add(invite);
        context.Bookings.Add(booking);
        foreach (var appointmentTypeId in attendee.RequiredAppointmentTypeIds)
        {
            context.BookingAppointments.Add(
                BookingAppointment.Create(Guid.NewGuid(), booking.Id, appointmentTypeId));
            eventItem.CapacityFor(appointmentTypeId).Decrement();
        }

        await context.SaveChangesAsync();
        return eventId;
    }

    private sealed record EventOperationsResponse(IReadOnlyList<EventOperationsRow> Events);

    private sealed record EventOperationsRow(Guid EventId, DateOnly Date, int ActiveBookings);

    private sealed record BoardResponse(IReadOnlyList<OpenProposalResponse> OpenProposals);

    private sealed record OpenProposalResponse(Guid ProposalId, DateOnly Date, TimeOnly StartTime);
}
`````

## before — tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs — 1/1

<!-- retirement-file: {"id":30,"file":"tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs","beforeSha":"7cfd25e4a72782c35cca6b31e1a1bb3b3414bcd2aba84ac0fea52e0f926943e3","afterSha":"a98a4956c7eb256d6aff6ee3d5fac5079f938b332151649583518755f350db59","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class ManageBookingEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task TheManageLinkShowsTheBookedTime()
    {
        var booking = await GivenABooking();
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingResponse>(
            $"/api/booking/manage/{booking.ManageToken}");

        Assert.NotNull(view);
        Assert.Equal("Amara Novak", view!.AttendeeName);
        Assert.Contains("-", view.Display);

        using var document = System.Text.Json.JsonDocument.Parse(
            await client.GetStringAsync($"/api/booking/manage/{booking.ManageToken}"));
        var links = document.RootElement.GetProperty("_links");
        var cancel = links.GetProperty("cancel");
        Assert.Equal(
            $"/api/booking/manage/{Uri.EscapeDataString(booking.ManageToken)}/cancel",
            cancel.GetProperty("href").GetString());
        Assert.Equal("POST", cancel.GetProperty("method").GetString());
        Assert.Equal("cancelManagedBooking", cancel.GetProperty("operationId").GetString());
    }

    [Fact]
    public async Task CancellingWithoutRebookingReleasesTheBooking()
    {
        var booking = await GivenABooking();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/manage/{booking.ManageToken}/cancel", new { Rebook = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.False(outcome!.Reinvited);

        var afterwards = await client.GetAsync($"/api/booking/manage/{booking.ManageToken}");
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);

        var persisted = await ReadCancellationStateAsync(booking);
        Assert.Equal(BookingStatus.Cancelled, persisted.BookingStatus);
        Assert.Equal(AttendeeStatus.NotYetInvited, persisted.AttendeeStatus);
        Assert.Equal(persisted.TotalHeadcount, persisted.RemainingCapacity);
        Assert.Empty(persisted.PendingInviteIds);
    }

    [Fact]
    public async Task CancelAndRebookIssuesAFreshInvite()
    {
        var booking = await GivenABooking();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/manage/{booking.ManageToken}/cancel", new { Rebook = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.True(outcome!.Reinvited);

        var persisted = await ReadCancellationStateAsync(booking);
        Assert.Equal(BookingStatus.Cancelled, persisted.BookingStatus);
        Assert.Equal(AttendeeStatus.Invited, persisted.AttendeeStatus);
        Assert.Equal(persisted.TotalHeadcount, persisted.RemainingCapacity);
        var inviteId = Assert.Single(persisted.PendingInviteIds);
        Assert.NotEqual(booking.OriginalInviteId, inviteId);
    }

    [Fact]
    public async Task AnUnknownManageTokenIsNotFound()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/booking/manage/nonsense");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<BookingFixture> GivenABooking()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");

        var eventId = view!.Options[0].EventId;
        var confirmed = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm",
            new { EventId = eventId });

        var outcome = await confirmed.Content.ReadFromJsonAsync<ConfirmResponse>();
        return new BookingFixture(
            outcome!.BookingId,
            outcome.ManageToken,
            invite.AttendeeId,
            invite.Id,
            eventId);
    }

    private async Task<AttendeeInviteFixture> GivenAnInvitedAttendee()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var eventIds = new List<Guid>();
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
            eventIds.Add(eventId);
        }

        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", $"{Guid.NewGuid():N}@mail.com",
            AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        attendee.MarkInvited();
        context.Attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        context.Invites.Add(Invite.CreateInitial(
            inviteId, attendee.Id, issued.TokenHash, DateTimeOffset.UtcNow.AddDays(4),
            eventIds,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0));
        await context.SaveChangesAsync();

        return new AttendeeInviteFixture(issued.Token, attendee.Id, inviteId);
    }

    private async Task<CancellationState> ReadCancellationStateAsync(BookingFixture booking)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

        var persistedBooking = await context.Bookings.SingleAsync(b => b.Id == booking.BookingId);
        var attendee = await context.Attendees.SingleAsync(c => c.Id == booking.AttendeeId);
        var capacity = await context.EventCapacities.SingleAsync(c =>
            c.EventId == booking.EventId &&
            c.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting);
        var pendingInviteIds = await context.Invites
            .Where(i => i.AttendeeId == booking.AttendeeId && i.Status == InviteStatus.Pending)
            .Select(i => i.Id)
            .ToListAsync();

        return new CancellationState(
            persistedBooking.Status,
            attendee.Status,
            capacity.TotalHeadcount,
            capacity.RemainingCapacity,
            pendingInviteIds);
    }

    private sealed record ConfirmResponse(Guid BookingId, string ManageToken);

    private sealed record AttendeeInviteFixture(string Token, Guid AttendeeId, Guid Id);

    private sealed record BookingFixture(
        Guid BookingId,
        string ManageToken,
        Guid AttendeeId,
        Guid OriginalInviteId,
        Guid EventId);

    private sealed record CancellationState(
        BookingStatus BookingStatus,
        AttendeeStatus AttendeeStatus,
        int TotalHeadcount,
        int RemainingCapacity,
        IReadOnlyList<Guid> PendingInviteIds);

    private sealed record BookingResponse(DateOnly Date, string Display, string AttendeeName);

    private sealed record CancelResponse(bool Reinvited);
}
`````

## after — tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs — 1/1

<!-- retirement-file: {"id":30,"file":"tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs","beforeSha":"7cfd25e4a72782c35cca6b31e1a1bb3b3414bcd2aba84ac0fea52e0f926943e3","afterSha":"a98a4956c7eb256d6aff6ee3d5fac5079f938b332151649583518755f350db59","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class ManageBookingEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task TheManageLinkShowsTheBookedTime()
    {
        var booking = await GivenABooking();
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingResponse>(
            $"/api/booking/manage/{booking.ManageToken}");

        Assert.NotNull(view);
        Assert.Equal("Amara Novak", view!.AttendeeName);
        Assert.Contains("-", view.Display);

        using var document = System.Text.Json.JsonDocument.Parse(
            await client.GetStringAsync($"/api/booking/manage/{booking.ManageToken}"));
        var links = document.RootElement.GetProperty("_links");
        var cancel = links.GetProperty("cancel");
        Assert.Equal(
            $"/api/booking/manage/{Uri.EscapeDataString(booking.ManageToken)}/cancel",
            cancel.GetProperty("href").GetString());
        Assert.Equal("POST", cancel.GetProperty("method").GetString());
        Assert.Equal("cancelManagedBooking", cancel.GetProperty("operationId").GetString());
    }

    [Fact]
    public async Task CancellingWithoutRebookingReleasesTheBooking()
    {
        var booking = await GivenABooking();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/manage/{booking.ManageToken}/cancel", new { Rebook = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.False(outcome!.Reinvited);

        var afterwards = await client.GetAsync($"/api/booking/manage/{booking.ManageToken}");
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);

        var persisted = await ReadCancellationStateAsync(booking);
        Assert.Equal(BookingStatus.Cancelled, persisted.BookingStatus);
        Assert.Equal(AttendeeStatus.NotYetInvited, persisted.AttendeeStatus);
        Assert.Equal(persisted.TotalHeadcount, persisted.RemainingCapacity);
        Assert.Empty(persisted.PendingInviteIds);
    }

    [Fact]
    public async Task CancelAndRebookIssuesAFreshInvite()
    {
        var booking = await GivenABooking();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/manage/{booking.ManageToken}/cancel", new { Rebook = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.True(outcome!.Reinvited);

        var persisted = await ReadCancellationStateAsync(booking);
        Assert.Equal(BookingStatus.Cancelled, persisted.BookingStatus);
        Assert.Equal(AttendeeStatus.Invited, persisted.AttendeeStatus);
        Assert.Equal(persisted.TotalHeadcount, persisted.RemainingCapacity);
        var inviteId = Assert.Single(persisted.PendingInviteIds);
        Assert.NotEqual(booking.OriginalInviteId, inviteId);
    }

    [Fact]
    public async Task AnUnknownManageTokenIsNotFound()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/booking/manage/nonsense");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<BookingFixture> GivenABooking()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");

        var eventId = view!.Options[0].EventId;
        var confirmed = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm",
            new { EventId = eventId });

        var outcome = await confirmed.Content.ReadFromJsonAsync<ConfirmResponse>();
        return new BookingFixture(
            outcome!.BookingId,
            outcome.ManageToken,
            invite.AttendeeId,
            invite.Id,
            eventId);
    }

    private async Task<AttendeeInviteFixture> GivenAnInvitedAttendee()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var eventIds = new List<Guid>();
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
            eventIds.Add(eventId);
        }

        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            $"{Guid.NewGuid():N}@mail.com",
            AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
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
            eventIds,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            0));
        await context.SaveChangesAsync();

        return new AttendeeInviteFixture(issued.Token, attendee.Id, inviteId);
    }

    private async Task<CancellationState> ReadCancellationStateAsync(BookingFixture booking)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

        var persistedBooking = await context.Bookings.SingleAsync(b => b.Id == booking.BookingId);
        var attendee = await context.Attendees.SingleAsync(c => c.Id == booking.AttendeeId);
        var capacity = await context.EventCapacities.SingleAsync(c =>
            c.EventId == booking.EventId &&
            c.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting);
        var pendingInviteIds = await context.Invites
            .Where(i => i.AttendeeId == booking.AttendeeId && i.Status == InviteStatus.Pending)
            .Select(i => i.Id)
            .ToListAsync();

        return new CancellationState(
            persistedBooking.Status,
            attendee.Status,
            capacity.TotalHeadcount,
            capacity.RemainingCapacity,
            pendingInviteIds);
    }

    private sealed record ConfirmResponse(Guid BookingId, string ManageToken);

    private sealed record AttendeeInviteFixture(string Token, Guid AttendeeId, Guid Id);

    private sealed record BookingFixture(
        Guid BookingId,
        string ManageToken,
        Guid AttendeeId,
        Guid OriginalInviteId,
        Guid EventId);

    private sealed record CancellationState(
        BookingStatus BookingStatus,
        AttendeeStatus AttendeeStatus,
        int TotalHeadcount,
        int RemainingCapacity,
        IReadOnlyList<Guid> PendingInviteIds);

    private sealed record BookingResponse(DateOnly Date, string Display, string AttendeeName);

    private sealed record CancelResponse(bool Reinvited);
}
`````

## before — tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs — 1/1

<!-- retirement-file: {"id":31,"file":"tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs","beforeSha":"e821b2a0f779560c25b2fc8df821a72581da0137858a1d20a66f2d636d108076","afterSha":"0667698248802ea540c730693aded24ef2f1d526f4d7331bd2ad15dab6ebaa8b","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

/// <summary>Verifies recently past events keep the scoped, minimum-data workspace boundary.</summary>
[Collection("api")]
public sealed class RecentPastWorkspaceBoundaryTests(ApiFactory factory)
{
    /// <summary>Verifies Manager and AppointmentStaff both reach a recently past eventItem.</summary>
    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.AppointmentStaff)]
    public async Task ScopedRolesReachRecentlyPastEvents(Role role)
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [role], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var list = await client.GetAsync("/api/appointment-workspace/events");
        using var detail = await client.GetAsync(
            $"/api/appointment-workspace/events/{data.EventId}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var body = await list.Content.ReadAsStringAsync();
        Assert.Contains(data.EventId.ToString(), body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies a recently past event outside trusted scope is indistinguishable from missing.</summary>
    [Fact]
    public async Task CrossTypeRecentlyPastEventIsNotFound()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.UniformFitting);

        using var response = await factory.CreateClient().GetAsync(
            $"/api/appointment-workspace/events/{data.EventId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies unscoped callers are denied recently past events without data.</summary>
    [Fact]
    public async Task UnassignedCallerIsForbiddenRecentlyPastEvent()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = Guid.NewGuid();

        using var response = await factory.CreateClient().GetAsync(
            $"/api/appointment-workspace/events/{data.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Verifies a recently past event keeps the exact approved minimum-data shape.</summary>
    [Fact]
    public async Task RecentlyPastResponsesKeepTheApprovedPropertySets()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var detail = JsonDocument.Parse(await client.GetStringAsync(
            $"/api/appointment-workspace/events/{data.EventId}"));
        AssertKeys(detail.RootElement,
            "appointmentTypeName", "eventId", "date", "startTime", "endTime", "appointments", "_links");
        var row = Assert.Single(detail.RootElement.GetProperty("appointments").EnumerateArray());
        AssertKeys(row, "bookingAppointmentId", "attendeeName", "attendeeEmail",
            "status", "checkedInAt", "outcomeAt", "version", "_links");
        Assert.DoesNotContain("attendeeId", detail.RootElement.GetRawText());
        Assert.DoesNotContain("bookingId", detail.RootElement.GetRawText());
    }

    private async Task<(Guid EventId, Guid AppointmentId)> GivenWorkspaceAsync(
        Guid appointmentTypeId,
        int daysBeforeToday)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;
        var eventItem = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today.AddDays(-daysBeforeToday), new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var groupId = appointmentTypeId == AppointmentTypeIds.MedicalCheckUp
            ? AttendeeGroupIds.GroundOperationsAgent
            : AttendeeGroupIds.Pilots;
        var group = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == groupId);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);
        context.AddRange(eventItem, attendee, booking, appointment);
        await context.SaveChangesAsync();
        return (eventItem.Id, appointment.Id);
    }

    /// <summary>Asserts a JSON object carries exactly the approved property names.</summary>
    private static void AssertKeys(JsonElement value, params string[] expected) =>
        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
}
`````

## after — tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs — 1/1

<!-- retirement-file: {"id":31,"file":"tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs","beforeSha":"e821b2a0f779560c25b2fc8df821a72581da0137858a1d20a66f2d636d108076","afterSha":"0667698248802ea540c730693aded24ef2f1d526f4d7331bd2ad15dab6ebaa8b","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

/// <summary>Verifies recently past events keep the scoped, minimum-data workspace boundary.</summary>
[Collection("api")]
public sealed class RecentPastWorkspaceBoundaryTests(ApiFactory factory)
{
    /// <summary>Verifies Manager and AppointmentStaff both reach a recently past eventItem.</summary>
    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.AppointmentStaff)]
    public async Task ScopedRolesReachRecentlyPastEvents(Role role)
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [role], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var list = await client.GetAsync("/api/appointment-workspace/events");
        using var detail = await client.GetAsync(
            $"/api/appointment-workspace/events/{data.EventId}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var body = await list.Content.ReadAsStringAsync();
        Assert.Contains(data.EventId.ToString(), body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies a recently past event outside trusted scope is indistinguishable from missing.</summary>
    [Fact]
    public async Task CrossTypeRecentlyPastEventIsNotFound()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.UniformFitting);

        using var response = await factory.CreateClient().GetAsync(
            $"/api/appointment-workspace/events/{data.EventId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies unscoped callers are denied recently past events without data.</summary>
    [Fact]
    public async Task UnassignedCallerIsForbiddenRecentlyPastEvent()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = Guid.NewGuid();

        using var response = await factory.CreateClient().GetAsync(
            $"/api/appointment-workspace/events/{data.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Verifies a recently past event keeps the exact approved minimum-data shape.</summary>
    [Fact]
    public async Task RecentlyPastResponsesKeepTheApprovedPropertySets()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var detail = JsonDocument.Parse(await client.GetStringAsync(
            $"/api/appointment-workspace/events/{data.EventId}"));
        AssertKeys(detail.RootElement,
            "appointmentTypeName", "eventId", "date", "startTime", "endTime", "appointments", "_links");
        var row = Assert.Single(detail.RootElement.GetProperty("appointments").EnumerateArray());
        AssertKeys(row, "bookingAppointmentId", "attendeeName", "attendeeEmail",
            "status", "checkedInAt", "outcomeAt", "version", "_links");
        Assert.DoesNotContain("attendeeId", detail.RootElement.GetRawText());
        Assert.DoesNotContain("bookingId", detail.RootElement.GetRawText());
    }

    private async Task<(Guid EventId, Guid AppointmentId)> GivenWorkspaceAsync(
        Guid appointmentTypeId,
        int daysBeforeToday)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;
        var eventItem = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today.AddDays(-daysBeforeToday), new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var groupId = appointmentTypeId == AppointmentTypeIds.MedicalCheckUp
            ? AttendeeGroupIds.GroundOperationsAgent
            : AttendeeGroupIds.Pilots;
        var group = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == groupId);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Alex Morgan",
            $"alex-{Guid.NewGuid():N}@example.com",
            group,
            ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);
        context.AddRange(eventItem, attendee, booking, appointment);
        await context.SaveChangesAsync();
        return (eventItem.Id, appointment.Id);
    }

    /// <summary>Asserts a JSON object carries exactly the approved property names.</summary>
    private static void AssertKeys(JsonElement value, params string[] expected) =>
        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
}
`````

## before — tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs — 1/1

<!-- retirement-file: {"id":32,"file":"tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs","beforeSha":"7cbfdcf9e286abe3af5e40790ca5ff61de8a210cb83d4e8e226ffbf7465eac5b","afterSha":"d9a23763e938813680874cfe861c1b10f6e334326c779894092a4e32ead27a08","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

/// <summary>Verifies recovery-invite start/cancel routes, auth, and the delivery-outcome projection.</summary>
[Collection("api")]
public sealed class RecoveryInviteEndpointTests(ApiFactory factory)
{
    private sealed record DeliveryOutcomeResponse(
        Guid InviteId,
        IReadOnlyList<Guid> AppointmentTypeIds,
        bool EmailSent);

    /// <summary>A Coordinator starts recovery for a missed appointment and gets the outcome.</summary>
    [Fact]
    public async Task CoordinatorStartsRecoveryAndReceivesDeliveryOutcome()
    {
        var attendeeId = await GivenAttendeeWithNoShowAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsync(
            $"/api/attendees/{attendeeId}/recovery-invites", null);
        var body = await response.Content.ReadFromJsonAsync<DeliveryOutcomeResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(Guid.Empty, body!.InviteId);
        Assert.Contains(AppointmentTypeIds.MedicalCheckUp, body.AppointmentTypeIds);
    }

    /// <summary>A Coordinator learns nothing is recoverable through the stable error code.</summary>
    [Fact]
    public async Task CoordinatorReceivesConflictWhenNothingRecoverable()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/attendees", new
        {
            Name = "Amara Novak",
            Email = $"{Guid.NewGuid():N}@example.com",
            AttendeeGroupId = AttendeeGroupIds.CabinCrew,
        });
        var attendeeId = await created.Content.ReadFromJsonAsync<Guid>();

        using var response = await client.PostAsync(
            $"/api/attendees/{attendeeId}/recovery-invites", null);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "recovery_not_available",
            problem.RootElement.GetProperty("title").GetString());
    }

    /// <summary>Only Coordinators may start or cancel a recovery invite.</summary>
    [Fact]
    public async Task AdminCannotStartRecovery()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);

        using var response = await factory.CreateClient().PostAsync(
            $"/api/attendees/{Guid.NewGuid()}/recovery-invites", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>A scoped Manager still lacks the Coordinator-only recovery capability.</summary>
    [Fact]
    public async Task ManagerCannotStartRecovery()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient().PostAsync(
            $"/api/attendees/{Guid.NewGuid()}/recovery-invites", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Anonymous callers cannot start a recovery invite.</summary>
    [Fact]
    public async Task AnonymousCannotStartRecovery()
    {
        factory.SignedInAs = null;

        using var response = await factory.CreateClient().PostAsync(
            $"/api/attendees/{Guid.NewGuid()}/recovery-invites", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>A Coordinator cancels a pending recovery, and a second cancel conflicts.</summary>
    [Fact]
    public async Task CoordinatorCancelsPendingRecovery()
    {
        var attendeeId = await GivenAttendeeWithNoShowAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        using var started = await client.PostAsync(
            $"/api/attendees/{attendeeId}/recovery-invites", null);
        var outcome = await started.Content.ReadFromJsonAsync<DeliveryOutcomeResponse>();

        using var cancelled = await client.DeleteAsync(
            $"/api/attendees/{attendeeId}/recovery-invites/{outcome!.InviteId}");
        using var again = await client.DeleteAsync(
            $"/api/attendees/{attendeeId}/recovery-invites/{outcome.InviteId}");

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    /// <summary>Cancelling an unknown recovery invite is not found.</summary>
    [Fact]
    public async Task CancellingUnknownRecoveryReturnsNotFound()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/attendees", new
        {
            Name = "Amara Novak",
            Email = $"{Guid.NewGuid():N}@example.com",
            AttendeeGroupId = AttendeeGroupIds.CabinCrew,
        });
        var attendeeId = await created.Content.ReadFromJsonAsync<Guid>();

        using var response = await client.DeleteAsync(
            $"/api/attendees/{attendeeId}/recovery-invites/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Only Coordinators may cancel a recovery invite.</summary>
    [Fact]
    public async Task AdminCannotCancelRecovery()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);

        using var response = await factory.CreateClient().DeleteAsync(
            $"/api/attendees/{Guid.NewGuid()}/recovery-invites/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>A scoped Manager still lacks the Coordinator-only recovery capability.</summary>
    [Fact]
    public async Task ManagerCannotCancelRecovery()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient().DeleteAsync(
            $"/api/attendees/{Guid.NewGuid()}/recovery-invites/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Seeds a booked attendee with one Medical Check-up no-show and a spare eventItem.</summary>
    private async Task<Guid> GivenAttendeeWithNoShowAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;
        var bookedEvent = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today.AddDays(-1), new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var spareEvents = new[]
        {
            new TimeOnly(11, 0),
            new TimeOnly(13, 0),
            new TimeOnly(15, 0),
        }
        .Select(start => EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today.AddDays(2), start, 240),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20)))
        .ToList();
        var group = context.AttendeeGroups
            .Include(g => g.Requirements)
            .Single(g => g.Id == AttendeeGroupIds.GroundOperationsAgent);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [bookedEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, bookedEvent.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp);
        context.AddRange(bookedEvent);
        context.AddRange(spareEvents);
        context.AddRange(attendee, booking, appointment);
        await context.SaveChangesAsync();

        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.MedicalCheckUp);
        using var marked = await factory.CreateClient().PutAsJsonAsync(
            $"/api/appointment-workspace/appointments/{appointment.Id}/status",
            new { status = "NoShow", expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        return attendee.Id;
    }
}
`````
