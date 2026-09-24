using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

internal static class EventFixture
{
    public static Event Create(
        Guid id, EventWindow window, IReadOnlyDictionary<Guid, int> headcounts,
        IEnumerable<Guid>? acceptedTypes = null)
    {
        var accepted = (acceptedTypes ?? AppointmentTypeIds.All).ToList();
        var manager = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), window, manager,
            proposerAppointmentTypeId: accepted[0], listedTypeIds: accepted);
        foreach (var type in accepted)
            proposal.Accept(type, manager, headcounts[type]);
        return Event.CreateFrom(id, proposal);
    }
}
