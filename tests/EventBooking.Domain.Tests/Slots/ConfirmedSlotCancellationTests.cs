using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Domain.Tests.Slots;

public class ConfirmedSlotCancellationTests
{
    private static ConfirmedSlot ActiveSlot()
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 11), new TimeOnly(13, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        return ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
    }

    [Fact]
    public void CancellingMarksTheSlotCancelled()
    {
        var slot = ActiveSlot();

        slot.Cancel();

        Assert.Equal(ConfirmedSlotStatus.Cancelled, slot.Status);
    }

    [Fact]
    public void ACancelledSlotOffersNoSpareCapacityEvenWhenItsCountersAreFull()
    {
        var slot = ActiveSlot();
        Assert.True(slot.HasSpareCapacityForAll(AppointmentTypeIds.All));

        slot.Cancel();

        Assert.False(slot.HasSpareCapacityForAll(AppointmentTypeIds.All));
        Assert.Equal(10, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
    }

    [Fact]
    public void CancellingTwiceIsRejected()
    {
        var slot = ActiveSlot();
        slot.Cancel();

        var ex = Assert.Throws<DomainException>(() => slot.Cancel());
        Assert.Equal("This slot has already been cancelled.", ex.Message);
    }
}
