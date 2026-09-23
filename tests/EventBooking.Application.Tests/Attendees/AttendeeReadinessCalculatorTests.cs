using EventBooking.Application.Attendees;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Tests.Attendees;

/// <summary>Verifies readiness precedence and latest-attempt semantics.</summary>
public sealed class AttendeeReadinessCalculatorTests
{
    private readonly AttendeeReadinessCalculator _calculator = new();

    /// <summary>A later Completed recovery attempt supersedes the original NoShow.</summary>
    [Fact]
    public void RecoveryCompletionMakesAttendeeReady()
    {
        var attendeeId = Guid.NewGuid();
        var original = Guid.NewGuid();
        var recovery = Guid.NewGuid();
        var type = AppointmentTypeIds.MedicalCheckUp;
        var snapshot = new AttendeeReadinessSnapshot(
            attendeeId,
            Guid.NewGuid(),
            [type],
            original,
            [
                Attempt(type, original, BookingStatus.Active, BookingAppointmentStatus.NoShow, 1),
                Attempt(type, recovery, BookingStatus.Concluded, BookingAppointmentStatus.Completed, 2),
            ]);

        var actual = _calculator.Calculate(snapshot);

        Assert.Equal(AttendeeReadinessCode.Ready, actual.Code);
        Assert.Empty(actual.OutstandingAppointmentTypes);
    }

    /// <summary>Any non-cancelled Completed attempt satisfies the type despite a later NoShow.</summary>
    [Fact]
    public void AnyCompletedAttemptControlsOutcome()
    {
        var attendeeId = Guid.NewGuid();
        var original = Guid.NewGuid();
        var recovery = Guid.NewGuid();
        var type = AppointmentTypeIds.UniformFitting;
        var snapshot = new AttendeeReadinessSnapshot(
            attendeeId,
            Guid.NewGuid(),
            [type],
            original,
            [
                Attempt(type, original, BookingStatus.Active, BookingAppointmentStatus.Completed, 1),
                Attempt(type, recovery, BookingStatus.Concluded, BookingAppointmentStatus.NoShow, 2),
            ]);

        var actual = _calculator.Calculate(snapshot);

        Assert.Equal(AttendeeReadinessCode.Ready, actual.Code);
        Assert.Empty(actual.OutstandingAppointmentTypes);
    }

    /// <summary>A latest NoShow with no completion is outstanding and recoverable.</summary>
    [Fact]
    public void LatestNoShowIsRecoverable()
    {
        var original = Guid.NewGuid();
        var type = AppointmentTypeIds.MedicalCheckUp;
        var actual = _calculator.Calculate(new AttendeeReadinessSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), [type], original,
            [Attempt(type, original, BookingStatus.Active, BookingAppointmentStatus.NoShow, 1)]));

        Assert.Equal(AttendeeReadinessCode.AppointmentsOutstanding, actual.Code);
        var outstanding = Assert.Single(actual.OutstandingAppointmentTypes);
        Assert.Equal("MED", outstanding.Code);
        Assert.True(outstanding.IsRecoverable);
    }

    /// <summary>A required group without an active original booking is not ready.</summary>
    [Fact]
    public void AssignedGroupWithoutBookingIsNotReady()
    {
        var actual = _calculator.Calculate(new AttendeeReadinessSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), [], null, []));

        Assert.Equal(AttendeeReadinessCode.NoActiveBooking, actual.Code);
    }

    private static AttendeeReadinessAttempt Attempt(
        Guid typeId,
        Guid bookingId,
        BookingStatus bookingStatus,
        BookingAppointmentStatus status,
        int day) => new(
            Guid.NewGuid(), typeId, status, bookingId, bookingStatus,
            DateTimeOffset.Parse($"2026-09-{day:00}T09:00:00Z"));
}
