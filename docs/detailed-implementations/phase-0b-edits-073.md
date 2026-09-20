# 00b — Vocabulary edits 73 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":238,"oldPath":"tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs","beforeSha":"52d14a745dbf3c166f4d9517214af3dd14cb60b53b630ebf37fc3dbe888eca99","afterSha":"f40a87937723d0cc58c8ad4a0c62f959ce1d5173fea3b1f00e05f6d78e94e4d1","side":"after","part":1,"parts":1} -->

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
        var bookedEvent = Event.CreateImported(
            Guid.NewGuid(), new EventWindow(today.AddDays(-1), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var spareEvents = new[]
        {
            new TimeOnly(11, 0),
            new TimeOnly(13, 0),
            new TimeOnly(15, 0),
        }
        .Select(start => Event.CreateImported(
            Guid.NewGuid(), new EventWindow(today.AddDays(2), start),
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

## before — tests/EventBooking.Api.Tests/SlotEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":239,"oldPath":"tests/EventBooking.Api.Tests/SlotEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/EventEndpointTests.cs","beforeSha":"af886fe1601f9f414d3bd52ec4be8f427d17d74a749f405d0224783ce61feba2","afterSha":"31428533755811d3081f0d62fa9e396f9413d9cb7b6e13354156a76153254b46","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
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

[Collection("api")]
public class SlotEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task AnAnonymousCallerIsChallenged()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/slots/board");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ASignedInUserWithNoRoleAssignmentIsForbidden()
    {
        factory.SignedInAs = Guid.NewGuid();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/slots/board");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AManagerCanProposeASlotAndSeeItOnTheBoard()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        var created = await client.PostAsJsonAsync(
            "/api/slots/proposals",
            new { Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), StartTime = new TimeOnly(9, 0) });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var board = await client.GetFromJsonAsync<BoardResponse>("/api/slots/board");
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
            "/api/slots/proposals",
            new { Date = new DateOnly(2020, 1, 1), StartTime = new TimeOnly(9, 0) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ACoordinatorGetsForbiddenFromAManagerRoute()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/slots/board");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    [Fact]
    public async Task AnAdminGetsSlotRowsAndNoCandidateData()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        await GivenSlotAsync();
        var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/slots/operations");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("confirmedSlotId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("candidateId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ACoordinatorGetsTheSameSlotOperationsView()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var slotId = await GivenSlotAsync();
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<SlotOperationsResponse>("/api/slots/operations");

        Assert.NotNull(view);
        Assert.Contains(view!.Slots, slot => slot.ConfirmedSlotId == slotId);
    }

    [Fact]
    public async Task ASignedInUserWithNoRoleAssignmentIsForbiddenFromSlotOperations()
    {
        factory.SignedInAs = Guid.NewGuid();
        factory.RolesClaim = [];
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/slots/operations");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TheTwoStageCancellationProtocolIsUnchangedForAnAdmin()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var slotId = await GivenSlotWithOneBookingAsync();
        var client = factory.CreateClient();

        using var first = await client.DeleteAsync($"/api/slots/confirmed/{slotId}?confirm=false");
        Assert.Equal(HttpStatusCode.Conflict, first.StatusCode);

        using var second = await client.DeleteAsync($"/api/slots/confirmed/{slotId}?confirm=true");
        Assert.True(second.IsSuccessStatusCode, await second.Content.ReadAsStringAsync());
    }

    /// <summary>Seeds one confirmed slot with capacity for every appointment type.</summary>
    private async Task<Guid> GivenSlotAsync()
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        context.SlotProposals.Add(proposal);
        context.ConfirmedSlots.Add(slot);
        await context.SaveChangesAsync();
        return slot.Id;
    }

    /// <summary>Seeds one confirmed slot holding a single active booking, so the cascade gate trips.</summary>
    private async Task<Guid> GivenSlotWithOneBookingAsync()
    {
        var slotId = await GivenSlotAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var pilots = await context.EmployeeGroups
            .Include(g => g.Requirements)
            .SingleAsync(g => g.Id == EmployeeGroupIds.Pilots);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "S. Booked", $"s.booked.{Guid.NewGuid():N}@mail.com", pilots);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            candidate.Id,
            $"hash-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(4),
            [slotId, Guid.NewGuid(), Guid.NewGuid()],
            candidate.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, slotId, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        candidate.MarkInvited();
        candidate.MarkBooked();

        // Mirrors ConfirmBookingHandler: one appointment per required type, each holding a place.
        var slot = await context.ConfirmedSlots
            .Include(s => s.Capacities)
            .SingleAsync(s => s.Id == slotId);

        context.Candidates.Add(candidate);
        context.Invites.Add(invite);
        context.Bookings.Add(booking);
        foreach (var appointmentTypeId in candidate.RequiredAppointmentTypeIds)
        {
            context.BookingAppointments.Add(
                BookingAppointment.Create(Guid.NewGuid(), booking.Id, appointmentTypeId));
            slot.CapacityFor(appointmentTypeId).Decrement();
        }

        await context.SaveChangesAsync();
        return slotId;
    }

    private sealed record SlotOperationsResponse(IReadOnlyList<SlotOperationsRow> Slots);

    private sealed record SlotOperationsRow(Guid ConfirmedSlotId, DateOnly Date, int ActiveBookings);

    private sealed record BoardResponse(IReadOnlyList<OpenProposalResponse> OpenProposals);

    private sealed record OpenProposalResponse(Guid ProposalId, DateOnly Date, TimeOnly StartTime);
}
`````

## after — tests/EventBooking.Api.Tests/EventEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":239,"oldPath":"tests/EventBooking.Api.Tests/SlotEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/EventEndpointTests.cs","beforeSha":"af886fe1601f9f414d3bd52ec4be8f427d17d74a749f405d0224783ce61feba2","afterSha":"31428533755811d3081f0d62fa9e396f9413d9cb7b6e13354156a76153254b46","side":"after","part":1,"parts":1} -->

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
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60), new TimeOnly(9, 0)),
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

## before — tests/EventBooking.Api.Tests/StaffHypermediaTests.cs — 1/1

<!-- vocabulary-file: {"id":240,"oldPath":"tests/EventBooking.Api.Tests/StaffHypermediaTests.cs","newPath":"tests/EventBooking.Api.Tests/StaffHypermediaTests.cs","beforeSha":"634b322ec28e58cecd355a1028ca92150f952cf9ab1b43ab9aedbecc20118390","afterSha":"b171679cdc09c92e4fac4212f3e9fdf259985e33f1b652a86629daa7deac209e","side":"before","part":1,"parts":1} -->

`````csharp
using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class StaffHypermediaTests(ApiFactory factory)
{
    [Fact]
    public async Task SettingsCarriesSelfAndUpdate()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        using var json = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/api/admin/settings"));
        var links = json.RootElement.GetProperty("_links");
        AssertLink(links, "self", "/api/admin/settings", "GET", "getSettings");
        AssertLink(links, "update", "/api/admin/settings", "PUT", "updateSettings");
    }

    [Fact]
    public async Task SlotCollectionsCarryEntryLinksEvenWhenEmpty()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();
        using var board = JsonDocument.Parse(await client.GetStringAsync("/api/slots/board"));
        AssertLink(board.RootElement.GetProperty("_links"), "self", "/api/slots/board", "GET", "getSlotBoard");
        using var workspace = JsonDocument.Parse(await client.GetStringAsync("/api/appointment-workspace/slots"));
        AssertLink(workspace.RootElement.GetProperty("_links"), "self",
            "/api/appointment-workspace/slots", "GET", "listAppointmentSlots");
    }

    [Fact]
    public async Task AuditSearchCarriesSelfAndOmitsNextWhenExhausted()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        using var json = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/api/audit/search?pageSize=50"));
        var links = json.RootElement.GetProperty("_links");
        AssertLink(links, "self", "/api/audit/search?pageSize=50", "GET", "searchAudit");
        Assert.False(links.TryGetProperty("next", out _));
    }

    [Fact]
    public async Task MePreservesFieldsAndAddsLinks()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        using var json = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/api/me"));
        Assert.True(json.RootElement.TryGetProperty("staffId", out _));
        Assert.True(json.RootElement.TryGetProperty("roles", out _));
        AssertLink(json.RootElement.GetProperty("_links"), "self", "/api/me", "GET", "getMyAccess");
    }

    private static void AssertLink(JsonElement links, string relation, string href, string method, string operationId)
    {
        var link = links.GetProperty(relation);
        Assert.Equal(href, link.GetProperty("href").GetString());
        Assert.Equal(method, link.GetProperty("method").GetString());
        Assert.Equal(operationId, link.GetProperty("operationId").GetString());
    }
}
`````

## after — tests/EventBooking.Api.Tests/StaffHypermediaTests.cs — 1/1

<!-- vocabulary-file: {"id":240,"oldPath":"tests/EventBooking.Api.Tests/StaffHypermediaTests.cs","newPath":"tests/EventBooking.Api.Tests/StaffHypermediaTests.cs","beforeSha":"634b322ec28e58cecd355a1028ca92150f952cf9ab1b43ab9aedbecc20118390","afterSha":"b171679cdc09c92e4fac4212f3e9fdf259985e33f1b652a86629daa7deac209e","side":"after","part":1,"parts":1} -->

`````csharp
using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class StaffHypermediaTests(ApiFactory factory)
{
    [Fact]
    public async Task SettingsCarriesSelfAndUpdate()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        using var json = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/api/admin/settings"));
        var links = json.RootElement.GetProperty("_links");
        AssertLink(links, "self", "/api/admin/settings", "GET", "getSettings");
        AssertLink(links, "update", "/api/admin/settings", "PUT", "updateSettings");
    }

    [Fact]
    public async Task EventCollectionsCarryEntryLinksEvenWhenEmpty()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();
        using var board = JsonDocument.Parse(await client.GetStringAsync("/api/events/board"));
        AssertLink(board.RootElement.GetProperty("_links"), "self", "/api/events/board", "GET", "getEventBoard");
        using var workspace = JsonDocument.Parse(await client.GetStringAsync("/api/appointment-workspace/events"));
        AssertLink(workspace.RootElement.GetProperty("_links"), "self",
            "/api/appointment-workspace/events", "GET", "listAppointmentEvents");
    }

    [Fact]
    public async Task AuditSearchCarriesSelfAndOmitsNextWhenExhausted()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        using var json = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/api/audit/search?pageSize=50"));
        var links = json.RootElement.GetProperty("_links");
        AssertLink(links, "self", "/api/audit/search?pageSize=50", "GET", "searchAudit");
        Assert.False(links.TryGetProperty("next", out _));
    }

    [Fact]
    public async Task MePreservesFieldsAndAddsLinks()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        using var json = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/api/me"));
        Assert.True(json.RootElement.TryGetProperty("staffId", out _));
        Assert.True(json.RootElement.TryGetProperty("roles", out _));
        AssertLink(json.RootElement.GetProperty("_links"), "self", "/api/me", "GET", "getMyAccess");
    }

    private static void AssertLink(JsonElement links, string relation, string href, string method, string operationId)
    {
        var link = links.GetProperty(relation);
        Assert.Equal(href, link.GetProperty("href").GetString());
        Assert.Equal(method, link.GetProperty("method").GetString());
        Assert.Equal(operationId, link.GetProperty("operationId").GetString());
    }
}
`````

## before — tests/EventBooking.Application.Tests/Access/AdminCandidateDataIsolationTests.cs — 1/1

<!-- vocabulary-file: {"id":241,"oldPath":"tests/EventBooking.Application.Tests/Access/AdminCandidateDataIsolationTests.cs","newPath":"tests/EventBooking.Application.Tests/Access/AdminAttendeeDataIsolationTests.cs","beforeSha":"1232897cb477ef4d18c21476854a70623b74811730173b8be2ed8cfd7a91a615","afterSha":"e33ad871479305e0f237c930e32acf72ef2b484f9f0c405a6fe09abb29fd1bfd","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Candidates;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Tests.Access;

public class AdminCandidateDataIsolationTests
{
    [Fact]
    public async Task AdminCandidateListIsDeniedBeforeRepositoryInvocation()
    {
        var admin = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(admin, [Role.Admin], null));
        var candidates = new CountingCandidateRepository();
        var handler = new ListCandidatesHandler(candidates, new InMemoryEmployeeGroupRepository(), new StaffAccessAuthorizer(profiles));

        var result = await handler.HandleAsync(
            new ListCandidatesQuery(admin, null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, candidates.Calls);
    }

    [Fact]
    public async Task AdminDashboardIsDeniedBeforeAnyQueryInvocation()
    {
        var admin = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(admin, [Role.Admin], null));
        var queries = new CountingDashboardQueries();
        var handler = new GetDashboardsHandler(queries, new StaffAccessAuthorizer(profiles));

        var result = await handler.HandleAsync(
            new GetDashboardsQuery(admin), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, queries.Calls);
    }

    [Fact]
    public async Task CoordinatorCandidateListStillRuns()
    {
        var coordinator = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(coordinator, [Role.Coordinator], null));
        var candidates = new CountingCandidateRepository();
        var handler = new ListCandidatesHandler(candidates, new InMemoryEmployeeGroupRepository(), new StaffAccessAuthorizer(profiles));

        var result = await handler.HandleAsync(
            new ListCandidatesQuery(coordinator, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, candidates.Calls);
    }

    [Fact]
    public async Task AdminCanReadSlotAuditButNotCandidateAudit()
    {
        var admin = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(admin, [Role.Admin], null));
        var queries = new CountingAuditQueries();
        var handler = new GetAuditHistoryHandler(queries, new StaffAccessAuthorizer(profiles));

        var candidate = await handler.HandleAsync(
            new GetAuditHistoryQuery(admin, null, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(candidate.IsFailure);
        Assert.Equal(0, queries.Calls);

        var slot = await handler.HandleAsync(
            new GetAuditHistoryQuery(
                admin, AuditEntityTypes.ConfirmedSlot, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(slot.IsSuccess);
        Assert.Equal(1, queries.Calls);
    }

    private static InMemoryStaffAccessProfileRepository Profiles(StaffAccessProfile profile)
    {
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(profile);
        return profiles;
    }

    private sealed class CountingCandidateRepository : ICandidateRepository
    {
        public int Calls { get; private set; }

        public Task<Candidate?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<Candidate?>(null);
        }

        public Task<Candidate?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<Candidate?>(null);
        }

        public Task<Candidate?> GetByEmailAsync(string email, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<Candidate?>(null);
        }

        public Task<IReadOnlyList<Candidate>> ListAsync(
            CandidateStatus? status,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<Candidate>>([]);
        }

        public void Add(Candidate candidate) => Calls++;
        public void Remove(Candidate candidate) => Calls++;
    }

    private sealed class CountingDashboardQueries : IDashboardQueries
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<AwaitingAvailabilityRow>> AwaitingAvailabilityAsync(
            CancellationToken cancellationToken) => Return<AwaitingAvailabilityRow>();

        public Task<IReadOnlyList<NoResponseRow>> NoResponseAsync(
            CancellationToken cancellationToken) => Return<NoResponseRow>();

        public Task<IReadOnlyList<SlotOverviewRow>> SlotsOverviewAsync(
            CancellationToken cancellationToken) => Return<SlotOverviewRow>();

        public Task<IReadOnlyList<CandidateEmailStatusRow>> LatestEmailStatusAsync(
            CancellationToken cancellationToken) => Return<CandidateEmailStatusRow>();

        private Task<IReadOnlyList<T>> Return<T>()
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<T>>([]);
        }
    }

    private sealed class CountingAuditQueries : IAuditQueries
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<AuditHistoryRow>> ForEntityAsync(
            string entityType,
            Guid entityId,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<AuditHistoryRow>>([]);
        }

        public Task<IReadOnlyList<AuditHistoryRow>> ForCandidateAsync(
            Guid candidateId,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<AuditHistoryRow>>([]);
        }

        public Task<AuditSearchPage> SearchAsync(
            AuditSearchFilter filter,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new AuditSearchPage([], null));
        }
    }
}
`````

## after — tests/EventBooking.Application.Tests/Access/AdminAttendeeDataIsolationTests.cs — 1/1

<!-- vocabulary-file: {"id":241,"oldPath":"tests/EventBooking.Application.Tests/Access/AdminCandidateDataIsolationTests.cs","newPath":"tests/EventBooking.Application.Tests/Access/AdminAttendeeDataIsolationTests.cs","beforeSha":"1232897cb477ef4d18c21476854a70623b74811730173b8be2ed8cfd7a91a615","afterSha":"e33ad871479305e0f237c930e32acf72ef2b484f9f0c405a6fe09abb29fd1bfd","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Tests.Access;

public class AdminAttendeeDataIsolationTests
{
    [Fact]
    public async Task AdminAttendeeListIsDeniedBeforeRepositoryInvocation()
    {
        var admin = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(admin, [Role.Admin], null));
        var attendees = new CountingAttendeeRepository();
        var handler = new ListAttendeesHandler(attendees, new InMemoryAttendeeGroupRepository(), new StaffAccessAuthorizer(profiles));

        var result = await handler.HandleAsync(
            new ListAttendeesQuery(admin, null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, attendees.Calls);
    }

    [Fact]
    public async Task AdminDashboardIsDeniedBeforeAnyQueryInvocation()
    {
        var admin = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(admin, [Role.Admin], null));
        var queries = new CountingDashboardQueries();
        var handler = new GetDashboardsHandler(queries, new StaffAccessAuthorizer(profiles));

        var result = await handler.HandleAsync(
            new GetDashboardsQuery(admin), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, queries.Calls);
    }

    [Fact]
    public async Task CoordinatorAttendeeListStillRuns()
    {
        var coordinator = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(coordinator, [Role.Coordinator], null));
        var attendees = new CountingAttendeeRepository();
        var handler = new ListAttendeesHandler(attendees, new InMemoryAttendeeGroupRepository(), new StaffAccessAuthorizer(profiles));

        var result = await handler.HandleAsync(
            new ListAttendeesQuery(coordinator, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, attendees.Calls);
    }

    [Fact]
    public async Task AdminCanReadEventAuditButNotAttendeeAudit()
    {
        var admin = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(admin, [Role.Admin], null));
        var queries = new CountingAuditQueries();
        var handler = new GetAuditHistoryHandler(queries, new StaffAccessAuthorizer(profiles));

        var attendee = await handler.HandleAsync(
            new GetAuditHistoryQuery(admin, null, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(attendee.IsFailure);
        Assert.Equal(0, queries.Calls);

        var eventItem = await handler.HandleAsync(
            new GetAuditHistoryQuery(
                admin, AuditEntityTypes.Event, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(eventItem.IsSuccess);
        Assert.Equal(1, queries.Calls);
    }

    private static InMemoryStaffAccessProfileRepository Profiles(StaffAccessProfile profile)
    {
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(profile);
        return profiles;
    }

    private sealed class CountingAttendeeRepository : IAttendeeRepository
    {
        public int Calls { get; private set; }

        public Task<Attendee?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<Attendee?>(null);
        }

        public Task<Attendee?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<Attendee?>(null);
        }

        public Task<Attendee?> GetByEmailAsync(string email, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<Attendee?>(null);
        }

        public Task<IReadOnlyList<Attendee>> ListAsync(
            AttendeeStatus? status,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<Attendee>>([]);
        }

        public void Add(Attendee attendee) => Calls++;
        public void Remove(Attendee attendee) => Calls++;
    }

    private sealed class CountingDashboardQueries : IDashboardQueries
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<AwaitingAvailabilityRow>> AwaitingAvailabilityAsync(
            CancellationToken cancellationToken) => Return<AwaitingAvailabilityRow>();

        public Task<IReadOnlyList<NoResponseRow>> NoResponseAsync(
            CancellationToken cancellationToken) => Return<NoResponseRow>();

        public Task<IReadOnlyList<EventOverviewRow>> EventsOverviewAsync(
            CancellationToken cancellationToken) => Return<EventOverviewRow>();

        public Task<IReadOnlyList<AttendeeEmailStatusRow>> LatestEmailStatusAsync(
            CancellationToken cancellationToken) => Return<AttendeeEmailStatusRow>();

        private Task<IReadOnlyList<T>> Return<T>()
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<T>>([]);
        }
    }

    private sealed class CountingAuditQueries : IAuditQueries
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<AuditHistoryRow>> ForEntityAsync(
            string entityType,
            Guid entityId,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<AuditHistoryRow>>([]);
        }

        public Task<IReadOnlyList<AuditHistoryRow>> ForAttendeeAsync(
            Guid attendeeId,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<AuditHistoryRow>>([]);
        }

        public Task<AuditSearchPage> SearchAsync(
            AuditSearchFilter filter,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new AuditSearchPage([], null));
        }
    }
}
`````

## before — tests/EventBooking.Application.Tests/Access/StaffAccessAuthorizerTests.cs — 1/1

<!-- vocabulary-file: {"id":242,"oldPath":"tests/EventBooking.Application.Tests/Access/StaffAccessAuthorizerTests.cs","newPath":"tests/EventBooking.Application.Tests/Access/StaffAccessAuthorizerTests.cs","beforeSha":"115ed949e9b2846e87b111a9c38402b171fedf85400f6b8566bccd56b7bdb6fe","afterSha":"0a7bc9cee12100c6d395efc131f759f6fc41b07e70947ccec8b1a161c91d841c","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Access;

public class StaffAccessAuthorizerTests
{
    private readonly InMemoryStaffAccessProfileRepository _profiles = new();

    [Theory]
    [InlineData(Role.Admin, StaffCapability.ManageSettings, true)]
    [InlineData(Role.Admin, StaffCapability.ManageCandidates, false)]
    [InlineData(Role.Admin, StaffCapability.ViewCandidateDashboards, false)]
    [InlineData(Role.Admin, StaffCapability.ImportConfirmedSlots, true)]
    [InlineData(Role.Coordinator, StaffCapability.ManageCandidates, true)]
    [InlineData(Role.Coordinator, StaffCapability.ImportConfirmedSlots, true)]
    [InlineData(Role.Coordinator, StaffCapability.ManageSettings, false)]
    [InlineData(Role.Manager, StaffCapability.ManageSlotNegotiation, true)]
    [InlineData(Role.Manager, StaffCapability.ConductAppointments, true)]
    [InlineData(Role.AppointmentStaff, StaffCapability.ConductAppointments, true)]
    [InlineData(Role.AppointmentStaff, StaffCapability.CancelConfirmedSlot, false)]
    [InlineData(Role.Admin, StaffCapability.ViewSlotOperations, true)]
    [InlineData(Role.Coordinator, StaffCapability.ViewSlotOperations, true)]
    public async Task SingleRoleCapabilitiesMatchTheMatrix(
        Role role,
        StaffCapability capability,
        bool expected)
    {
        var staffUserId = Add(role);
        var result = await Authorizer().AuthorizeAsync(
            staffUserId, capability, null, CancellationToken.None);

        Assert.Equal(expected, result.IsSuccess);
    }

    [Fact]
    public async Task CombinedCoordinatorManagerGetsTheUnionAndTrustedScope()
    {
        var staffUserId = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            staffUserId,
            [Role.Coordinator, Role.Manager],
            AppointmentTypeIds.MedicalCheckUp));

        var candidate = await Authorizer().AuthorizeAsync(
            staffUserId, StaffCapability.ManageCandidates, null, CancellationToken.None);
        var manager = await Authorizer().AuthorizeAsync(
            staffUserId,
            StaffCapability.ManageSlotNegotiation,
            AppointmentTypeIds.MedicalCheckUp,
            CancellationToken.None);

        Assert.True(candidate.IsSuccess);
        Assert.True(manager.IsSuccess);
        Assert.Equal(AppointmentTypeIds.MedicalCheckUp, manager.Value.AppointmentTypeId);
    }

    [Fact]
    public async Task AScopedCapabilityRejectsAnotherAppointmentType()
    {
        var manager = Add(Role.Manager);

        var result = await Authorizer().AuthorizeAsync(
            manager,
            StaffCapability.ManageSlotNegotiation,
            AppointmentTypeIds.UniformFitting,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public async Task AnUnassignedIdentityIsDenied()
    {
        var result = await Authorizer().AuthorizeAsync(
            Guid.NewGuid(), StaffCapability.ViewSlotOperations, null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Theory]
    [InlineData(StaffCapability.ManageSettings)]
    [InlineData(StaffCapability.ManageStaffAccess)]
    [InlineData(StaffCapability.ImportConfirmedSlots)]
    [InlineData(StaffCapability.ManageCandidates)]
    [InlineData(StaffCapability.ViewCandidateDashboards)]
    [InlineData(StaffCapability.ViewCandidateAudit)]
    [InlineData(StaffCapability.ViewSlotAudit)]
    [InlineData(StaffCapability.ManageSlotNegotiation)]
    [InlineData(StaffCapability.ViewSlotOperations)]
    [InlineData(StaffCapability.CancelConfirmedSlot)]
    [InlineData(StaffCapability.ConductAppointments)]
    public async Task AScopedCapabilityIsDeniedWhenScopeIsNull(StaffCapability capability)
    {
        var profile = StaffAccessProfile.Create(Guid.NewGuid(), [Role.Manager], null);
        var repository = new InMemoryStaffAccessProfileRepository();
        repository.Items.Add(profile);
        var authorizer = new StaffAccessAuthorizer(repository);

        var result = await authorizer.AuthorizeAsync(
            profile.StaffUserId, capability, requiredAppointmentTypeId: null, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task AnUnscopedCoordinatorCapabilityIsStillGrantedWhenScopeIsNull()
    {
        var profile = StaffAccessProfile.Create(
            Guid.NewGuid(), [Role.Coordinator, Role.Manager], null);
        var repository = new InMemoryStaffAccessProfileRepository();
        repository.Items.Add(profile);
        var authorizer = new StaffAccessAuthorizer(repository);

        var result = await authorizer.AuthorizeAsync(
            profile.StaffUserId,
            StaffCapability.ManageCandidates,
            requiredAppointmentTypeId: null,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    private Guid Add(Role role)
    {
        var id = Guid.NewGuid();
        Guid? scope = role is Role.Manager or Role.AppointmentStaff
            ? AppointmentTypeIds.DrugAndAlcoholTesting
            : null;
        _profiles.Add(StaffAccessProfile.Create(id, role, scope));
        return id;
    }

    private StaffAccessAuthorizer Authorizer() => new(_profiles);
}
`````
