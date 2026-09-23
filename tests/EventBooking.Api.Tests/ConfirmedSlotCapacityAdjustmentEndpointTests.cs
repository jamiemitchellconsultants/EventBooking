using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class ConfirmedSlotCapacityAdjustmentEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task AManagerCanReplaceTheirOwnConfirmedTotal()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting);
        var slotId = await GivenSlotAsync(totalHeadcount: 10, occupied: 6);
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/slots/confirmed/{slotId}/capacity",
            new { TotalHeadcount = 12 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<AdjustmentResponse>();
        Assert.Equal(12, outcome!.TotalHeadcount);
        Assert.Equal(6, outcome.RemainingCapacity);

        var board = await client.GetFromJsonAsync<BoardResponse>("/api/slots/board");
        var slot = Assert.Single(
            board!.ConfirmedSlots,
            item => item.ConfirmedSlotId == slotId);
        Assert.Equal(12, slot.MyHeadcount);
        Assert.Equal(6, slot.MyRemainingCapacity);
    }

    [Fact]
    public async Task ADecreaseBelowActiveBookingsReturnsTheirCountAndChangesNothing()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting);
        var slotId = await GivenSlotAsync(totalHeadcount: 10, occupied: 6);
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/slots/confirmed/{slotId}/capacity",
            new { TotalHeadcount = 5 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal(
            "Headcount cannot be lower than the active-booking count of 6.",
            problem!.Detail);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var capacity = await context.SlotCapacities.AsNoTracking().SingleAsync(item =>
            item.ConfirmedSlotId == slotId
            && item.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(4, capacity.RemainingCapacity);
    }

    [Fact]
    public async Task ACoordinatorCannotAdjustConfirmedCapacity()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var slotId = await GivenSlotAsync(totalHeadcount: 10, occupied: 0);
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/slots/confirmed/{slotId}/capacity",
            new { TotalHeadcount = 12 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<Guid> GivenSlotAsync(int totalHeadcount, int occupied)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), totalHeadcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 20);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 20);
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);

        for (var index = 0; index < occupied; index++)
        {
            slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        context.SlotProposals.Add(proposal);
        context.ConfirmedSlots.Add(slot);
        await context.SaveChangesAsync();
        return slot.Id;
    }

    private sealed record AdjustmentResponse(
        Guid ConfirmedSlotId,
        int TotalHeadcount,
        int RemainingCapacity);

    private sealed record BoardResponse(
        IReadOnlyList<ConfirmedSlotResponse> ConfirmedSlots);

    private sealed record ConfirmedSlotResponse(
        Guid ConfirmedSlotId,
        int MyHeadcount,
        int MyRemainingCapacity);

    private sealed record ProblemResponse(string? Detail);
}
