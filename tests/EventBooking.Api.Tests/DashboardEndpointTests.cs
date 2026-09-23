using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Slots;
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
        Assert.NotNull(dashboards.Slots);
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
    /// slot Capacities list. Earlier coverage only asserted the row lists were non-null, so a DTO
    /// field could be renamed or dropped without failing a test — the page would simply render blank
    /// cells. This seeds one row of each kind and checks every field the page actually reads.
    /// </summary>
    [Fact]
    public async Task TheResponseCarriesEveryFieldThePageBindsTo()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var awaitingId = Guid.NewGuid();
        var noResponseId = Guid.NewGuid();
        var slotId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

            var cabinCrew = context.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.CabinCrew);
            var awaiting = Candidate.Create(
                awaitingId, "A. Waiting", "a.waiting@mail.com", cabinCrew);
            awaiting.MarkAwaitingAvailability();
            context.Candidates.Add(awaiting);

            var groundOps = context.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.GroundOperationsAgent);
            var noResponse = Candidate.Create(
                noResponseId, "B. Stuck", "b.stuck@mail.com", groundOps);
            noResponse.MarkInvited();
            noResponse.MarkNoResponse();
            context.Candidates.Add(noResponse);

            var proposal = SlotProposal.Create(
                Guid.NewGuid(),
                new SlotWindow(today.AddDays(30), new TimeOnly(9, 0)),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
            var slot = ConfirmedSlot.CreateFrom(slotId, proposal);
            slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
            context.SlotProposals.Add(proposal);
            context.ConfirmedSlots.Add(slot);

            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var dashboards = await client.GetFromJsonAsync<DashboardsResponse>("/api/dashboards");

        Assert.NotNull(dashboards);

        var awaitingRow = Assert.Single(dashboards!.AwaitingAvailability, r => r.CandidateId == awaitingId);
        Assert.Equal("A. Waiting", awaitingRow.Name);
        Assert.Equal("a.waiting@mail.com", awaitingRow.Email);
        Assert.Equal(new[] { "DAT", "MED", "UNI" }, awaitingRow.RequiredCodes);
        Assert.Equal(today, awaitingRow.WaitingSince);
        Assert.Equal(0, awaitingRow.DaysWaiting);

        var noResponseRow = Assert.Single(dashboards.NoResponse, r => r.CandidateId == noResponseId);
        Assert.Equal("B. Stuck", noResponseRow.Name);
        Assert.Equal("b.stuck@mail.com", noResponseRow.Email);
        Assert.Equal(new[] { "MED" }, noResponseRow.RequiredCodes);
        Assert.Equal(today, noResponseRow.GaveUpOn);

        var slotRow = Assert.Single(dashboards.Slots, s => s.ConfirmedSlotId == slotId);
        Assert.Equal(today.AddDays(30), slotRow.Date);
        Assert.Equal(new TimeOnly(9, 0), slotRow.StartTime);
        Assert.Equal(new TimeOnly(13, 0), slotRow.EndTime);
        Assert.Equal(0, slotRow.ActiveBookings);
        Assert.Equal(new[] { "DAT", "MED", "UNI" }, slotRow.Capacities.Select(c => c.Code));
        var drugAndAlcohol = slotRow.Capacities.Single(c => c.Code == "DAT");
        Assert.Equal(10, drugAndAlcohol.TotalHeadcount);
        Assert.Equal(9, drugAndAlcohol.RemainingCapacity);
    }

    private sealed record RowResponse(
        Guid CandidateId,
        string Name,
        string Email,
        IReadOnlyList<string> RequiredCodes,
        DateOnly? WaitingSince,
        int? DaysWaiting,
        DateOnly? GaveUpOn);

    private sealed record SlotCapacityResponse(string Code, int TotalHeadcount, int RemainingCapacity);

    private sealed record SlotResponse(
        Guid ConfirmedSlotId,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        IReadOnlyList<SlotCapacityResponse> Capacities,
        int ActiveBookings);

    private sealed record DashboardsResponse(
        IReadOnlyList<RowResponse> AwaitingAvailability,
        IReadOnlyList<RowResponse> NoResponse,
        IReadOnlyList<SlotResponse> Slots);
}
