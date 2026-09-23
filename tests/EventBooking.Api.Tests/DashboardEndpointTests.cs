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

            var proposal = EventProposal.Create(
                Guid.NewGuid(),
                new EventWindow(today.AddDays(30), new TimeOnly(9, 0)),
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
