using EventBooking.Application.Abstractions;
using EventBooking.Application.Dashboards;
using EventBooking.Application.ReadModels;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;
using EventBooking.Domain.Locations;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using EventBooking.Infrastructure.Time;
using EventBooking.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests.Queries;

[Collection("postgres")]
public class DashboardQueryTests(PostgresFixture fixture)
{
    private static readonly IEventWindowZones Zones = new NodaTimeEventWindowZones();
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly CallerShape Coordinator =
        new(Guid.NewGuid(), false, new HashSet<string> { "Coordinator" });

    // The bound is on the END instant, so each edge is seeded with an event whose end
    // falls just inside and just outside. An event starting inside the window but ending
    // outside it is the case a start-only filter would wrongly keep.
    [Fact]
    public async Task The_events_tab_is_bounded_by_end_instant_at_both_edges()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();

        var inside = await AddEventAsync(context, Now.AddDays(30));
        await AddEventAsync(context, Now.AddDays(-8));
        await AddEventAsync(context, Now.AddDays(61));

        var view = await Query(context).GetDashboardsAsync(
            Coordinator, null, Now, Zones, CancellationToken.None);

        Assert.Equal(inside, Assert.Single(view.Events.Rows).EventId);
        Assert.Equal(1, view.Events.Count);

        await fixture.ResetAsync();
    }

    [Fact]
    public async Task The_location_filter_narrows_the_events_tab_only()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();

        var tokyo = await AddTokyoAsync(context);
        var london = await AddEventAsync(context, Now.AddDays(10));
        await AddEventAsync(context, Now.AddDays(10), locationId: tokyo);
        await AddAttendeeAsync(context, "Amy", AttendeeStatus.AwaitingAvailability);

        var view = await Query(context).GetDashboardsAsync(
            Coordinator, TransitionalLocation.Id, Now, Zones, CancellationToken.None);

        Assert.Equal(london, Assert.Single(view.Events.Rows).EventId);
        // The attendee tabs are untouched by a location filter: an attendee awaiting
        // availability has no event, so no location.
        Assert.Equal(1, view.AwaitingAvailability.Count);

        await fixture.ResetAsync();
    }

    [Fact]
    public async Task Each_attendee_tab_is_one_status_with_its_count()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();

        await AddAttendeeAsync(context, "Amy", AttendeeStatus.AwaitingAvailability);
        await AddAttendeeAsync(context, "Ben", AttendeeStatus.AwaitingAvailability);
        await AddAttendeeAsync(context, "Cat", AttendeeStatus.NoResponseNeedsFollowUp);
        await AddAttendeeAsync(context, "Dan", AttendeeStatus.Booked);

        var view = await Query(context).GetDashboardsAsync(
            Coordinator, null, Now, Zones, CancellationToken.None);

        Assert.Equal(2, view.AwaitingAvailability.Count);
        Assert.Equal(1, view.NoResponse.Count);
        // Booked appears in neither tab: FR-13.1 names three tabs, and a booked attendee
        // is in none of them.
        Assert.DoesNotContain(view.AwaitingAvailability.Rows, r => r.Name == "Dan");
        Assert.DoesNotContain(view.NoResponse.Rows, r => r.Name == "Dan");

        await fixture.ResetAsync();
    }

    [Fact]
    public async Task An_admin_shaped_caller_reads_nothing_even_here()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        await AddAttendeeAsync(context, "Amy", AttendeeStatus.AwaitingAvailability);

        var view = await Query(context).GetDashboardsAsync(
            new CallerShape(Guid.NewGuid(), true, new HashSet<string> { "Admin" }),
            null, Now, Zones, CancellationToken.None);

        Assert.Equal(0, view.AwaitingAvailability.Count);
        Assert.Empty(view.Events.Rows);

        await fixture.ResetAsync();
    }

    private static DashboardQueries Query(EventBookingDbContext context) =>
        new(context, new FixedClock(Now), new NodaTimeEventWindowZones());

    private static async Task<Guid> AddEventAsync(
        EventBookingDbContext context, DateTimeOffset endInstant, Guid? locationId = null)
    {
        var location = locationId ?? TransitionalLocation.Id;
        var zoneId = location == TransitionalLocation.Id
            ? TransitionalLocation.TimeZoneId
            : "Asia/Tokyo";
        var localEnd = TimeZoneInfo.ConvertTime(
            endInstant, TimeZoneInfo.FindSystemTimeZoneById(zoneId));
        var window = new EventWindow(
            DateOnly.FromDateTime(localEnd.DateTime),
            TimeOnly.FromDateTime(localEnd.DateTime).Add(TimeSpan.FromMinutes(-60), out _),
            60);
        var proposal = locationId is null
            ? ProposalFixture.Create(Guid.NewGuid(), window, Guid.NewGuid())
            : EventProposal.Propose(
                Guid.NewGuid(),
                location,
                locationIsActive: true,
                zoneId,
                window,
                ProposalFixture.Zones,
                ProposalFixture.Now,
                [
                    new(AppointmentTypeIds.DrugAndAlcoholTesting, "DAT", true, true),
                    new(AppointmentTypeIds.MedicalCheckUp, "MED", true, true),
                    new(AppointmentTypeIds.UniformFitting, "UNI", true, true),
                ],
                ProposalFixture.ProposerType,
                Guid.NewGuid(),
                headcount: 1);
        foreach (var type in AppointmentTypeIds.All)
            proposal.Accept(type, Guid.NewGuid(), 10);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        context.EventProposals.Add(proposal);
        context.Events.Add(eventItem);
        await context.SaveChangesAsync();
        return eventItem.Id;
    }

    private static async Task<Guid> AddTokyoAsync(EventBookingDbContext context)
    {
        var tokyo = Location.Create(
            Guid.NewGuid(),
            "TOKYO",
            "Tokyo",
            "Second site for the location filter.",
            "Asia/Tokyo",
            new NodaTimeEventWindowZones());
        context.Locations.Add(tokyo);
        await context.SaveChangesAsync();
        return tokyo.Id;
    }

    private static async Task<Guid> AddAttendeeAsync(
        EventBookingDbContext context, string name, AttendeeStatus status)
    {
        var group = await context.AttendeeGroups
            .Include(g => g.Requirements)
            .SingleAsync(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(
            Guid.NewGuid(), name, $"{name.ToLowerInvariant()}@example.invalid", group,
            Now.AddDays(-30));
        switch (status)
        {
            case AttendeeStatus.AwaitingAvailability:
                attendee.MarkAwaitingAvailability(Now.AddDays(-30));
                break;
            case AttendeeStatus.NoResponseNeedsFollowUp:
                attendee.MarkInvited(Now.AddDays(-30));
                attendee.MarkNoResponse(Now.AddDays(-29));
                break;
            case AttendeeStatus.Booked:
                attendee.MarkInvited(Now.AddDays(-30));
                attendee.MarkBooked(Now.AddDays(-29));
                break;
            default:
                attendee.MarkInvited(Now.AddDays(-30));
                break;
        }

        context.Attendees.Add(attendee);
        await context.SaveChangesAsync();
        return attendee.Id;
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;

        public DateTimeOffset NowAtTransitionalLocation => now;

        public DateOnly TodayAtTransitionalLocation => DateOnly.FromDateTime(now.UtcDateTime);

        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant;
    }
}
