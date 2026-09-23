using EventBooking.Application.Appointments;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Tests.Appointments;

/// <summary>Verifies recovery Booking status follows only its own appointment outcomes.</summary>
public sealed class RecoveryBookingOutcomeCoordinatorTests
{
    /// <summary>All terminal outcomes conclude a recovery while leaving its root untouched.</summary>
    [Fact]
    public void TerminalRecoveryAppointmentsConcludeRecoveryOnly()
    {
        var staff = Guid.NewGuid();
        var originalEvent = Guid.NewGuid();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "initial",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [originalEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        var original = Booking.Create(
            Guid.NewGuid(), initial, originalEvent, "original", DateTimeOffset.UtcNow);
        var recoveryEvent = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            original.AttendeeId,
            original.Id,
            "recovery",
            DateTimeOffset.UtcNow.AddDays(2),
            ProposalFixture.LocationId,
            null,
            [recoveryEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);
        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoveryEvent, "manage", DateTimeOffset.UtcNow);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.MedicalCheckUp);
        appointment.TransitionTo(
            BookingAppointmentStatus.NoShow, staff, DateTimeOffset.UtcNow,
            checkInAllowed: false, noShowAllowed: true);

        var changed = new RecoveryBookingOutcomeCoordinator()
            .Synchronize(recovery, [appointment], laterRecoveryExists: false);

        Assert.True(changed);
        Assert.Equal(BookingStatus.Concluded, recovery.Status);
        Assert.Equal(BookingStatus.Active, original.Status);
    }

    /// <summary>A corrected non-terminal appointment cannot reopen behind a later recovery.</summary>
    [Fact]
    public void LaterRecoveryPreventsReopen()
    {
        var originalEvent = Guid.NewGuid();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "initial",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [originalEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        var original = Booking.Create(Guid.NewGuid(), initial, originalEvent, "root", DateTimeOffset.UtcNow);
        var recoveryEvent = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            original.AttendeeId,
            original.Id,
            "recovery",
            DateTimeOffset.UtcNow.AddDays(1),
            ProposalFixture.LocationId,
            null,
            [recoveryEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);
        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoveryEvent, "manage", DateTimeOffset.UtcNow);
        recovery.Conclude();
        var expected = BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.MedicalCheckUp);

        Assert.Throws<EventBooking.Domain.Common.DomainException>(() =>
            new RecoveryBookingOutcomeCoordinator()
                .Synchronize(recovery, [expected], laterRecoveryExists: true));
    }
}
