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
        var attendeeId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var initialInvite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendeeId,
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        var original = Booking.Create(
            Guid.NewGuid(), initialInvite, eventId, DateTimeOffset.UtcNow);
        var recoveryEvent = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            attendeeId,
            original.Id,
            DateTimeOffset.UtcNow.AddDays(2),
            ProposalFixture.LocationId,
            null,
            [recoveryEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);

        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoveryEvent, DateTimeOffset.UtcNow.AddHours(1));
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
        var attendeeId = Guid.NewGuid();
        var rootEvent = Guid.NewGuid();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(),
            attendeeId,
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [rootEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        var root = Booking.Create(Guid.NewGuid(), initial, rootEvent, DateTimeOffset.UtcNow);
        var firstEvent = Guid.NewGuid();
        var firstInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            attendeeId,
            root.Id,
            DateTimeOffset.UtcNow.AddDays(1),
            ProposalFixture.LocationId,
            null,
            [firstEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);
        var first = Booking.CreateRecovery(
            Guid.NewGuid(), firstInvite, root, firstEvent, DateTimeOffset.UtcNow);
        var secondEvent = Guid.NewGuid();
        var secondInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            attendeeId,
            root.Id,
            DateTimeOffset.UtcNow.AddDays(1),
            ProposalFixture.LocationId,
            null,
            [secondEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);

        Assert.Throws<EventBooking.Domain.Common.DomainException>(() => Booking.CreateRecovery(
            Guid.NewGuid(), secondInvite, first, secondEvent, DateTimeOffset.UtcNow));
    }
}
