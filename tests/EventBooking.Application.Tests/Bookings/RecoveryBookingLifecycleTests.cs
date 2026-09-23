using EventBooking.Application.Bookings;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Specifies locked recovery snapshot validation before capacity mutation.</summary>
public sealed class RecoveryBookingLifecycleTests
{
    /// <summary>A snapshot that ceased to be recoverable fails before confirmation.</summary>
    [Fact]
    public void CompletedTypeMakesPendingRecoverySnapshotStale()
    {
        var attendeeId = Guid.NewGuid();
        var originalId = Guid.NewGuid();
        var recoveryEvent = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), attendeeId, originalId, "recovery", DateTimeOffset.UtcNow.AddDays(2),
            [recoveryEvent, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        var attempts = new[]
        {
            new EventBooking.Application.Invites.RecoveryAttempt(
                Guid.NewGuid(), AppointmentTypeIds.MedicalCheckUp,
                BookingAppointmentStatus.NoShow, DateTimeOffset.UtcNow.AddDays(-2)),
            new EventBooking.Application.Invites.RecoveryAttempt(
                Guid.NewGuid(), AppointmentTypeIds.MedicalCheckUp,
                BookingAppointmentStatus.Completed, DateTimeOffset.UtcNow.AddDays(-1)),
        };

        var result = new RecoveryConfirmationValidator().Validate(
            recoveryInvite,
            [AppointmentTypeIds.MedicalCheckUp],
            attempts,
            []);

        Assert.True(result.IsFailure);
        Assert.Equal("recovery_state_changed", result.Error.Code);
    }
}
