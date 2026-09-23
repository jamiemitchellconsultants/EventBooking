using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventCancellationTests
{
    private static Event ActiveEvent()
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 11), new TimeOnly(13, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        return Event.CreateFrom(Guid.NewGuid(), proposal);
    }

    [Fact]
    public void CancellingMarksTheEventCancelled()
    {
        var eventItem = ActiveEvent();

        eventItem.Cancel();

        Assert.Equal(EventStatus.Cancelled, eventItem.Status);
    }

    [Fact]
    public void ACancelledEventOffersNoSpareCapacityEvenWhenItsCountersAreFull()
    {
        var eventItem = ActiveEvent();
        Assert.True(eventItem.HasSpareCapacityForAll(AppointmentTypeIds.All));

        eventItem.Cancel();

        Assert.False(eventItem.HasSpareCapacityForAll(AppointmentTypeIds.All));
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
    }

    [Fact]
    public void CancellingTwiceIsRejected()
    {
        var eventItem = ActiveEvent();
        eventItem.Cancel();

        var ex = Assert.Throws<DomainException>(() => eventItem.Cancel());
        Assert.Equal("This event has already been cancelled.", ex.Message);
    }
}
