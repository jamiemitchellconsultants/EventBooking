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
