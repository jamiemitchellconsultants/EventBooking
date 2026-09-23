using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Bookings;

/// <summary>Verifies every recovery points directly to an immutable active journey root.</summary>
public sealed class RecoveryBookingTests
{
    /// <summary>A recovery Booking copies the original root and can conclude and reopen.</summary>
    [Fact]
    public void RecoveryLifecyclePreservesOriginalRoot()
    {
        var candidateId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var initialInvite = Invite.CreateInitial(
            Guid.NewGuid(), candidateId, "initial", DateTimeOffset.UtcNow.AddDays(1),
            [slotId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        var original = Booking.Create(
            Guid.NewGuid(), initialInvite, slotId, "manage-original", DateTimeOffset.UtcNow);
        var recoverySlot = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), candidateId, original.Id, "recovery", DateTimeOffset.UtcNow.AddDays(2),
            [recoverySlot, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);

        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoverySlot,
            "manage-recovery", DateTimeOffset.UtcNow.AddHours(1));
        recovery.Conclude();
        recovery.Reopen();

        Assert.Null(original.RecoveryOfBookingId);
        Assert.Equal(BookingStatus.Active, original.Status);
        Assert.Equal(original.Id, recovery.RecoveryOfBookingId);
        Assert.Equal(BookingStatus.Active, recovery.Status);
    }

    /// <summary>A recovery cannot point at another recovery Booking.</summary>
    [Fact]
    public void RecoveryChainIsRejected()
    {
        var candidateId = Guid.NewGuid();
        var rootSlot = Guid.NewGuid();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(), candidateId, "initial", DateTimeOffset.UtcNow.AddDays(1),
            [rootSlot, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        var root = Booking.Create(Guid.NewGuid(), initial, rootSlot, "root", DateTimeOffset.UtcNow);
        var firstSlot = Guid.NewGuid();
        var firstInvite = Invite.CreateRecovery(
            Guid.NewGuid(), candidateId, root.Id, "first", DateTimeOffset.UtcNow.AddDays(1),
            [firstSlot, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        var first = Booking.CreateRecovery(
            Guid.NewGuid(), firstInvite, root, firstSlot, "first-manage", DateTimeOffset.UtcNow);
        var secondSlot = Guid.NewGuid();
        var secondInvite = Invite.CreateRecovery(
            Guid.NewGuid(), candidateId, root.Id, "second", DateTimeOffset.UtcNow.AddDays(1),
            [secondSlot, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);

        Assert.Throws<EventBooking.Domain.Common.DomainException>(() => Booking.CreateRecovery(
            Guid.NewGuid(), secondInvite, first, secondSlot, "second-manage", DateTimeOffset.UtcNow));
    }
}
