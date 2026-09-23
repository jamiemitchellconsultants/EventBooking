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
