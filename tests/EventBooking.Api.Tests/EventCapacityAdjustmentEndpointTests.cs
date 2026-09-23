using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class EventCapacityAdjustmentEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task AManagerCanReplaceTheirOwnConfirmedTotal()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting);
        var eventId = await GivenEventAsync(totalHeadcount: 10, occupied: 6);
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/events/{eventId}/capacity",
            new { TotalHeadcount = 12 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<AdjustmentResponse>();
        Assert.Equal(12, outcome!.TotalHeadcount);
        Assert.Equal(6, outcome.RemainingCapacity);

        var board = await client.GetFromJsonAsync<BoardResponse>("/api/events/board");
        var eventItem = Assert.Single(
            board!.Events,
            item => item.EventId == eventId);
        Assert.Equal(12, eventItem.MyHeadcount);
        Assert.Equal(6, eventItem.MyRemainingCapacity);
    }

    [Fact]
    public async Task ADecreaseBelowActiveBookingsReturnsTheirCountAndChangesNothing()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting);
        var eventId = await GivenEventAsync(totalHeadcount: 10, occupied: 6);
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/events/{eventId}/capacity",
            new { TotalHeadcount = 5 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal(
            "Headcount cannot be lower than the active-booking count of 6.",
            problem!.Detail);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var capacity = await context.EventCapacities.AsNoTracking().SingleAsync(item =>
            item.EventId == eventId
            && item.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(4, capacity.RemainingCapacity);
    }

    [Fact]
    public async Task ACoordinatorCannotAdjustConfirmedCapacity()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var eventId = await GivenEventAsync(totalHeadcount: 10, occupied: 0);
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/events/{eventId}/capacity",
            new { TotalHeadcount = 12 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<Guid> GivenEventAsync(int totalHeadcount, int occupied)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), totalHeadcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 20);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 20);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        for (var index = 0; index < occupied; index++)
        {
            eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        context.EventProposals.Add(proposal);
        context.Events.Add(eventItem);
        await context.SaveChangesAsync();
        return eventItem.Id;
    }

    private sealed record AdjustmentResponse(
        Guid EventId,
        int TotalHeadcount,
        int RemainingCapacity);

    private sealed record BoardResponse(
        IReadOnlyList<EventResponse> Events);

    private sealed record EventResponse(
        Guid EventId,
        int MyHeadcount,
        int MyRemainingCapacity);

    private sealed record ProblemResponse(string? Detail);
}
