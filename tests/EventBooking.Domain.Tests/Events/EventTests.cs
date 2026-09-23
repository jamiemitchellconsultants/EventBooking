using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventTests
{
    private static EventProposal FullyAcceptedProposal(
        int drugAndAlcohol = 10, int medical = 6, int uniform = 8)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), drugAndAlcohol);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), medical);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), uniform);
        return proposal;
    }

    [Fact]
    public void ConfirmingCarriesTheWindowAndMarksTheProposalConfirmed()
    {
        var proposal = FullyAcceptedProposal();

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        Assert.Equal(proposal.Id, eventItem.ProposalId);
        Assert.Equal(proposal.Window, eventItem.Window);
        Assert.Equal(EventStatus.Active, eventItem.Status);
        Assert.Equal(EventProposalStatus.Confirmed, proposal.Status);
    }

    [Fact]
    public void ConfirmingCreatesOneCapacityCounterPerAppointmentType()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.Equal(3, eventItem.Capacities.Count);
        Assert.Equal(
            AppointmentTypeIds.All.OrderBy(id => id),
            eventItem.Capacities.Select(c => c.AppointmentTypeId).OrderBy(id => id));
    }

    [Fact]
    public void EachCounterStartsAtTheHeadcountItsManagerAccepted()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal(10, 6, 8));

        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(8, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    [Fact]
    public void EveryCounterBelongsToTheEventThatOwnsIt()
    {
        var id = Guid.NewGuid();

        var eventItem = Event.CreateFrom(id, FullyAcceptedProposal());

        Assert.All(eventItem.Capacities, c => Assert.Equal(id, c.EventId));
    }

    [Fact]
    public void APartlyAcceptedProposalCannotBeConfirmed()
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);

        var ex = Assert.Throws<DomainException>(() => Event.CreateFrom(Guid.NewGuid(), proposal));
        Assert.Equal("A proposal can only be confirmed once all 3 managers have accepted it.", ex.Message);
    }

    [Fact]
    public void AProposalCannotBeConfirmedTwice()
    {
        var proposal = FullyAcceptedProposal();
        Event.CreateFrom(Guid.NewGuid(), proposal);

        Assert.Throws<DomainException>(() => Event.CreateFrom(Guid.NewGuid(), proposal));
    }

    [Fact]
    public void CapacityForAnUnknownAppointmentTypeIsRejected()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.Throws<DomainException>(() => eventItem.CapacityFor(Guid.NewGuid()));
    }

    [Fact]
    public void SpareCapacityIsCheckedAcrossEveryRequiredType()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.True(eventItem.HasSpareCapacityForAll(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        Assert.True(eventItem.HasSpareCapacityForAll(AppointmentTypeIds.All));
    }
}
