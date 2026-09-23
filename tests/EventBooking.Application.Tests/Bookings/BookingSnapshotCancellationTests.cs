using EventBooking.Application.Bookings;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies cancellation returns capacity only for the addressed Booking snapshot.</summary>
public sealed class BookingSnapshotCancellationTests
{
    /// <summary>A one-type Booking returns only that type even when Attendee now has three.</summary>
    [Fact]
    public async Task CancellationUsesBookingAppointmentsInsteadOfAttendeeRequirements()
    {
        var eventItem = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 10, 10), new TimeOnly(9, 0), 240),
            new Dictionary<Guid, int>
            {
                [AppointmentTypeIds.DrugAndAlcoholTesting] = 5,
                [AppointmentTypeIds.MedicalCheckUp] = 5,
                [AppointmentTypeIds.UniformFitting] = 5,
            });
        eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group, ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            "hash",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, "manage", DateTimeOffset.UtcNow);
        var appointments = new InMemoryBookingAppointmentRepository(new InMemoryBookingRepository());
        appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp));
        var events = new InMemoryEventRepository();
        events.Items.Add(eventItem);
        var capacities = new InMemoryEventCapacityRepository(events);
        var canceller = new BookingCanceller(
            appointments, capacities, new RecordingAuditLogger());

        var result = await canceller.CancelLockedAsync(
            booking, eventItem, EventBooking.Domain.Audit.ActorType.System, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], result.Value);
        Assert.Equal(5, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(5, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(5, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    /// <summary>A Booking addressing a dropped capacity row fails with the conflict code.</summary>
    [Fact]
    public async Task CancellationForDroppedTypeFailsLikeFullCapacity()
    {
        var eventItem = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 10, 10), new TimeOnly(9, 0), 240),
            new Dictionary<Guid, int>
            {
                [AppointmentTypeIds.DrugAndAlcoholTesting] = 5,
                [AppointmentTypeIds.MedicalCheckUp] = 5,
                [AppointmentTypeIds.UniformFitting] = 5,
            });
        eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT", "Ground Ops", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group, ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            "hash",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp],
            0);
        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, "manage", DateTimeOffset.UtcNow);
        var bookings = new InMemoryBookingRepository();
        bookings.Add(booking);
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting));
        appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp));
        var capacities = new RowDroppingCapacityRepository(
            eventItem, AppointmentTypeIds.MedicalCheckUp);
        var canceller = new BookingCanceller(
            appointments, capacities, new RecordingAuditLogger());

        var result = await canceller.CancelLockedAsync(
            booking, eventItem, ActorType.System, null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingStatus.Active, booking.Status);
        Assert.Equal(4, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(4, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
    }

    /// <summary>Simulates the capacity store losing one row the aggregate still references.</summary>
    private sealed class RowDroppingCapacityRepository(Event eventItem, Guid droppedTypeId)
        : EventBooking.Application.Abstractions.IEventCapacityRepository
    {
        public Task<IReadOnlyList<EventCapacity>> LockForUpdateAsync(
            Guid eventId,
            IReadOnlyCollection<Guid> appointmentTypeIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EventCapacity>>(
                appointmentTypeIds
                    .Where(id => id != droppedTypeId)
                    .OrderBy(id => id)
                    .Select(eventItem.CapacityFor)
                    .ToList());
    }
}
