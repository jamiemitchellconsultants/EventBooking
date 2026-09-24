// src/EventBooking.SeedData/DemoEventFactory.cs (complete)
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Persistence;

namespace EventBooking.SeedData;

internal static class DemoEventFactory
{
    internal static EventProposal CreateProposal(
        EventBookingDbContext database,
        IDemoProposalScenario spec,
        DemoDataset data,
        IEventWindowZones zones)
    {
        var location = data.Locations.Single(x => x.Code == spec.LocationCode);
        var types = spec.TypeCodes.Select(code => data.AppointmentTypes.Single(x => x.Code == code)).ToList();
        var staff = data.Staff.ToDictionary(x => x.Username);
        var proposer = types[0];
        var manager = staff[proposer.ManagerUsername!];
        var window = new EventWindow(spec.Date, spec.StartTime, spec.DurationMinutes);
        var proposal = EventProposal.Propose(
            spec is DemoProposalSpec p ? p.Id : Guid.NewGuid(),
            location.Id, location.IsActive, location.TimeZoneId, window, zones,
            window.StartInstant(zones, location.TimeZoneId).AddDays(-1),
            types.Select(x => new ProposableAppointmentType(
                x.Id, x.Code, x.IsActive, x.ManagerUsername is not null)).ToList(),
            proposer.Id, manager.UserId, 12);
        foreach (var code in spec.AcceptedTypeCodes.Skip(1))
        {
            var type = data.AppointmentTypes.Single(x => x.Code == code);
            proposal.Accept(type.Id, staff[type.ManagerUsername!].UserId, 12);
        }
        database.EventProposals.Add(proposal);
        return proposal;
    }

    internal static Event CreateEvent(
        EventBookingDbContext database, DemoEventSpec spec, DemoDataset data, IEventWindowZones zones)
    {
        var proposal = CreateProposal(database, spec, data, zones);
        var eventItem = Event.CreateFrom(spec.Id, proposal);
        database.Events.Add(eventItem);
        return eventItem;
    }
}
