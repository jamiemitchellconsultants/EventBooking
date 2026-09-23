using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;

namespace EventBooking.SeedData;

/// <summary>Reconstructs accepted demo proposals instead of creating events without negotiation history.</summary>
internal static class DemoEventFactory
{
    internal static Event Create(EventBookingDbContext database, Guid id, EventWindow window,
        IReadOnlyDictionary<Guid, int> headcounts)
    {
        var managers = DemoSeedSpec.Staff().Where(staff => staff.Roles.Contains(Role.Manager))
            .ToDictionary(staff => staff.AppointmentTypeId!.Value, staff => staff.UserId);
        var types = AppointmentTypeIds.All.Order().ToArray();
        var proposal = EventProposal.Create(Guid.NewGuid(), window, managers[types[0]]);
        foreach (var type in types)
            proposal.Accept(type, managers[type], headcounts[type]);
        var created = Event.CreateFrom(id, proposal);
        database.EventProposals.Add(proposal);
        database.Events.Add(created);
        return created;
    }
}
