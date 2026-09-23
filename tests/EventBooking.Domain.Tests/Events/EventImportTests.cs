using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventImportTests
{
    private static readonly EventWindow Window = new(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));

    private static Dictionary<Guid, int> FullHeadcounts(int dat = 10, int med = 6, int uni = 8) => new()
    {
        [AppointmentTypeIds.DrugAndAlcoholTesting] = dat,
        [AppointmentTypeIds.MedicalCheckUp] = med,
        [AppointmentTypeIds.UniformFitting] = uni,
    };

    [Fact]
    public void AnImportedEventHasNoProposalAndIsActive()
    {
        var eventItem = Event.CreateImported(Guid.NewGuid(), Window, FullHeadcounts());

        Assert.Null(eventItem.ProposalId);
        Assert.Equal(Window, eventItem.Window);
        Assert.Equal(EventStatus.Active, eventItem.Status);
    }

    [Fact]
    public void EachAppointmentTypeGetsItsOwnHeadcountAsBothTotalAndRemaining()
    {
        var eventItem = Event.CreateImported(Guid.NewGuid(), Window, FullHeadcounts(10, 6, 8));

        Assert.Equal(3, eventItem.Capacities.Count);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(8, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
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
            () => Event.CreateImported(Guid.NewGuid(), Window, incomplete));
    }

    [Fact]
    public void AnExtraAppointmentTypeIsRejected()
    {
        var headcounts = FullHeadcounts();
        headcounts.Add(Guid.NewGuid(), 4);

        Assert.Throws<DomainException>(
            () => Event.CreateImported(Guid.NewGuid(), Window, headcounts));
    }

    [Fact]
    public void ANonPositiveHeadcountIsRejected()
    {
        Assert.Throws<DomainException>(
            () => Event.CreateImported(Guid.NewGuid(), Window, FullHeadcounts(dat: 0)));
    }

    [Fact]
    public void TwoImportedEventsAreIndependentEntities()
    {
        var first = Event.CreateImported(Guid.NewGuid(), Window, FullHeadcounts());
        var second = Event.CreateImported(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 11), new TimeOnly(13, 0)), FullHeadcounts());

        Assert.NotEqual(first.Id, second.Id);
        Assert.Null(first.ProposalId);
        Assert.Null(second.ProposalId);
    }
}
