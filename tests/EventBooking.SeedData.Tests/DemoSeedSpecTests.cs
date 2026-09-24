// tests/EventBooking.SeedData.Tests/DemoSeedSpecTests.cs (complete)
using EventBooking.Domain.Access;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Time;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

[Collection("seed-anchor")]
public sealed class DemoSeedSpecTests
{
    [Fact]
    public void DatasetCoversEveryRequiredAxisWithoutPuttingEscOrDocOnLiveScenarios()
    {
        DemoSeedSpec.OverrideAnchor(new DateOnly(2026, 9, 22));
        var data = DemoSeedSpec.Build();

        Assert.Equal(3, data.Locations.Count);
        Assert.Equal(2, data.Locations.Select(x => x.TimeZoneId).Distinct().Count());
        Assert.Single(data.Locations, x => !x.IsActive);

        Assert.Equal(6, data.AppointmentTypes.Count);
        Assert.Single(data.AppointmentTypes, x => x.Code == "ESC" && x.IsActive && x.ManagerUsername is null);
        Assert.Single(data.AppointmentTypes, x => x.Code == "DOC" && !x.IsActive);
        Assert.Single(data.AppointmentTypes, x => x.Code == "LAB" && x.IsActive && x.ManagerUsername is not null);

        Assert.Equal([1, 2, 3, 4], data.Events.Select(x => x.TypeCodes.Count).Distinct().Order());
        Assert.Equal([60, 90, 240, 480], data.Events.Select(x => x.DurationMinutes).Distinct().Order());
        Assert.Contains(data.Events, x => IsOffsetChangeDate(x.Date, data.Locations.Single(l => l.Code == x.LocationCode).TimeZoneId));
        Assert.DoesNotContain(data.Events.SelectMany(x => x.TypeCodes), x => x is "ESC" or "DOC");

        Assert.Contains(data.OpenProposals, x => x.TypeCodes.Count == 2 && x.AcceptedTypeCodes.Count == 1);
        Assert.Contains(data.OpenProposals, x => x.TypeCodes.Count == 4 && x.AcceptedTypeCodes.Count == 2);
        Assert.DoesNotContain(data.OpenProposals.SelectMany(x => x.TypeCodes), x => x is "ESC" or "DOC");

        Assert.Equal(Enum.GetValues<AttendeeStatus>().Order(), data.Attendees.Select(x => x.Status).Distinct().Order());
        Assert.Equal(Enum.GetValues<BookingAppointmentStatus>().Order(),
            data.Attendees.SelectMany(x => x.AppointmentStatuses).Distinct().Order());
        Assert.Contains(data.Attendees, x => x.Recovery == DemoRecovery.Pending);
        Assert.Contains(data.Attendees, x => x.Recovery == DemoRecovery.Completed);
        Assert.Contains(data.Attendees, x => x.GroupCode == "FIT_ESC" && x.Status == AttendeeStatus.AwaitingAvailability);
    }

    [Fact]
    public void EveryEventAndOpenProposalPassesTheRealProposalFactory()
    {
        DemoSeedSpec.OverrideAnchor(new DateOnly(2026, 9, 22));
        var data = DemoSeedSpec.Build();
        var zones = new NodaTimeEventWindowZones();
        var types = data.AppointmentTypes.ToDictionary(x => x.Code);
        var locations = data.Locations.ToDictionary(x => x.Code);
        var managers = data.Staff.Where(x => x.Roles.Contains(Role.Manager))
            .ToDictionary(x => x.AppointmentTypeId!.Value, x => x.UserId);

        foreach (var scenario in data.Events.Cast<IDemoProposalScenario>().Concat(data.OpenProposals))
        {
            var location = locations[scenario.LocationCode];
            var listed = scenario.TypeCodes.Select(code => types[code]).ToList();
            var proposer = listed[0];
            var window = new EventWindow(scenario.Date, scenario.StartTime, scenario.DurationMinutes);
            var createdAt = window.StartInstant(zones, location.TimeZoneId).AddDays(-1);
            var proposal = EventProposal.Propose(
                Guid.NewGuid(), location.Id, location.IsActive, location.TimeZoneId, window, zones,
                createdAt,
                listed.Select(x => new ProposableAppointmentType(x.Id, x.Code, x.IsActive, x.ManagerUsername is not null)).ToList(),
                proposer.Id, managers[proposer.Id], 12);

            foreach (var code in scenario.AcceptedTypeCodes.Skip(1))
            {
                var type = types[code];
                proposal.Accept(type.Id, managers[type.Id], 12);
            }

            Assert.Equal(scenario.AcceptedTypeCodes.Count, proposal.Acceptances.Count);
        }
    }

    private static bool IsOffsetChangeDate(DateOnly date, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return zone.GetUtcOffset(start) != zone.GetUtcOffset(start.AddDays(1));
    }
}
