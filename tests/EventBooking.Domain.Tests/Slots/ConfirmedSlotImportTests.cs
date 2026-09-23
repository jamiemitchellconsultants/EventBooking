using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Domain.Tests.Slots;

public class ConfirmedSlotImportTests
{
    private static readonly SlotWindow Window = new(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));

    private static Dictionary<Guid, int> FullHeadcounts(int dat = 10, int med = 6, int uni = 8) => new()
    {
        [AppointmentTypeIds.DrugAndAlcoholTesting] = dat,
        [AppointmentTypeIds.MedicalCheckUp] = med,
        [AppointmentTypeIds.UniformFitting] = uni,
    };

    [Fact]
    public void AnImportedSlotHasNoProposalAndIsActive()
    {
        var slot = ConfirmedSlot.CreateImported(Guid.NewGuid(), Window, FullHeadcounts());

        Assert.Null(slot.ProposalId);
        Assert.Equal(Window, slot.Window);
        Assert.Equal(ConfirmedSlotStatus.Active, slot.Status);
    }

    [Fact]
    public void EachAppointmentTypeGetsItsOwnHeadcountAsBothTotalAndRemaining()
    {
        var slot = ConfirmedSlot.CreateImported(Guid.NewGuid(), Window, FullHeadcounts(10, 6, 8));

        Assert.Equal(3, slot.Capacities.Count);
        Assert.Equal(10, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
        Assert.Equal(10, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(6, slot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(8, slot.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    [Fact]
    public void AMissingAppointmentTypeIsRejected()
    {
        var incomplete = new Dictionary<Guid, int>
        {
            [AppointmentTypeIds.DrugAndAlcoholTesting] = 10,
            [AppointmentTypeIds.MedicalCheckUp] = 6,
        };

        Assert.Throws<DomainException>(
            () => ConfirmedSlot.CreateImported(Guid.NewGuid(), Window, incomplete));
    }

    [Fact]
    public void AnExtraAppointmentTypeIsRejected()
    {
        var headcounts = FullHeadcounts();
        headcounts.Add(Guid.NewGuid(), 4);

        Assert.Throws<DomainException>(
            () => ConfirmedSlot.CreateImported(Guid.NewGuid(), Window, headcounts));
    }

    [Fact]
    public void ANonPositiveHeadcountIsRejected()
    {
        Assert.Throws<DomainException>(
            () => ConfirmedSlot.CreateImported(Guid.NewGuid(), Window, FullHeadcounts(dat: 0)));
    }

    [Fact]
    public void TwoImportedSlotsAreIndependentEntities()
    {
        var first = ConfirmedSlot.CreateImported(Guid.NewGuid(), Window, FullHeadcounts());
        var second = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, 11), new TimeOnly(13, 0)), FullHeadcounts());

        Assert.NotEqual(first.Id, second.Id);
        Assert.Null(first.ProposalId);
        Assert.Null(second.ProposalId);
    }
}
