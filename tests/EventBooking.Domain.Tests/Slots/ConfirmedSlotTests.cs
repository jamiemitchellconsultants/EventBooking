using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Domain.Tests.Slots;

public class ConfirmedSlotTests
{
    private static SlotProposal FullyAcceptedProposal(
        int drugAndAlcohol = 10, int medical = 6, int uniform = 8)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
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

        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);

        Assert.Equal(proposal.Id, slot.ProposalId);
        Assert.Equal(proposal.Window, slot.Window);
        Assert.Equal(ConfirmedSlotStatus.Active, slot.Status);
        Assert.Equal(SlotProposalStatus.Confirmed, proposal.Status);
    }

    [Fact]
    public void ConfirmingCreatesOneCapacityCounterPerAppointmentType()
    {
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.Equal(3, slot.Capacities.Count);
        Assert.Equal(
            AppointmentTypeIds.All.OrderBy(id => id),
            slot.Capacities.Select(c => c.AppointmentTypeId).OrderBy(id => id));
    }

    [Fact]
    public void EachCounterStartsAtTheHeadcountItsManagerAccepted()
    {
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal(10, 6, 8));

        Assert.Equal(10, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
        Assert.Equal(10, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(6, slot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(8, slot.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    [Fact]
    public void EveryCounterBelongsToTheSlotThatOwnsIt()
    {
        var id = Guid.NewGuid();

        var slot = ConfirmedSlot.CreateFrom(id, FullyAcceptedProposal());

        Assert.All(slot.Capacities, c => Assert.Equal(id, c.ConfirmedSlotId));
    }

    [Fact]
    public void APartlyAcceptedProposalCannotBeConfirmed()
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);

        var ex = Assert.Throws<DomainException>(() => ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal));
        Assert.Equal("A proposal can only be confirmed once all 3 managers have accepted it.", ex.Message);
    }

    [Fact]
    public void AProposalCannotBeConfirmedTwice()
    {
        var proposal = FullyAcceptedProposal();
        ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);

        Assert.Throws<DomainException>(() => ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal));
    }

    [Fact]
    public void CapacityForAnUnknownAppointmentTypeIsRejected()
    {
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.Throws<DomainException>(() => slot.CapacityFor(Guid.NewGuid()));
    }

    [Fact]
    public void SpareCapacityIsCheckedAcrossEveryRequiredType()
    {
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.True(slot.HasSpareCapacityForAll(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        Assert.True(slot.HasSpareCapacityForAll(AppointmentTypeIds.All));
    }
}
