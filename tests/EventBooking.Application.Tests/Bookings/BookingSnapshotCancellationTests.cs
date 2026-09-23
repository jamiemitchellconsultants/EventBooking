using EventBooking.Application.Bookings;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies cancellation returns capacity only for the addressed Booking snapshot.</summary>
public sealed class BookingSnapshotCancellationTests
{
    /// <summary>A one-type Booking returns only that type even when Candidate now has three.</summary>
    [Fact]
    public async Task CancellationUsesBookingAppointmentsInsteadOfCandidateRequirements()
    {
        var slot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 10, 10), new TimeOnly(9, 0)),
            new Dictionary<Guid, int>
            {
                [AppointmentTypeIds.DrugAndAlcoholTesting] = 5,
                [AppointmentTypeIds.MedicalCheckUp] = 5,
                [AppointmentTypeIds.UniformFitting] = 5,
            });
        slot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();
        var group = EmployeeGroup.Define(
            EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]);
        var candidate = Candidate.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, "hash", DateTimeOffset.UtcNow.AddDays(1),
            [slot.Id, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        var booking = Booking.Create(Guid.NewGuid(), invite, slot.Id, "manage", DateTimeOffset.UtcNow);
        var appointments = new InMemoryBookingAppointmentRepository(new InMemoryBookingRepository());
        appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp));
        var slots = new InMemoryConfirmedSlotRepository();
        slots.Items.Add(slot);
        var capacities = new InMemorySlotCapacityRepository(slots);
        var canceller = new BookingCanceller(
            appointments, capacities, new RecordingAuditLogger());

        var result = await canceller.CancelLockedAsync(
            booking, slot, EventBooking.Domain.Audit.ActorType.System, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], result.Value);
        Assert.Equal(5, slot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(5, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(5, slot.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    /// <summary>A Booking addressing a dropped capacity row fails with the conflict code.</summary>
    [Fact]
    public async Task CancellationForDroppedTypeFailsLikeFullCapacity()
    {
        var slot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 10, 10), new TimeOnly(9, 0)),
            new Dictionary<Guid, int>
            {
                [AppointmentTypeIds.DrugAndAlcoholTesting] = 5,
                [AppointmentTypeIds.MedicalCheckUp] = 5,
                [AppointmentTypeIds.UniformFitting] = 5,
            });
        slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        slot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();
        var group = EmployeeGroup.Define(
            EmployeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT", "Ground Ops", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        var candidate = Candidate.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, "hash", DateTimeOffset.UtcNow.AddDays(1),
            [slot.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp], 0);
        var booking = Booking.Create(Guid.NewGuid(), invite, slot.Id, "manage", DateTimeOffset.UtcNow);
        var bookings = new InMemoryBookingRepository();
        bookings.Add(booking);
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting));
        appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp));
        var capacities = new RowDroppingCapacityRepository(
            slot, AppointmentTypeIds.MedicalCheckUp);
        var canceller = new BookingCanceller(
            appointments, capacities, new RecordingAuditLogger());

        var result = await canceller.CancelLockedAsync(
            booking, slot, ActorType.System, null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingStatus.Active, booking.Status);
        Assert.Equal(4, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(4, slot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
    }

    /// <summary>Simulates the capacity store losing one row the aggregate still references.</summary>
    private sealed class RowDroppingCapacityRepository(ConfirmedSlot slot, Guid droppedTypeId)
        : EventBooking.Application.Abstractions.ISlotCapacityRepository
    {
        public Task<IReadOnlyList<SlotCapacity>> LockForUpdateAsync(
            Guid confirmedSlotId,
            IReadOnlyCollection<Guid> appointmentTypeIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SlotCapacity>>(
                appointmentTypeIds
                    .Where(id => id != droppedTypeId)
                    .OrderBy(id => id)
                    .Select(slot.CapacityFor)
                    .ToList());
    }
}
