using EventBooking.Application.Candidates;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Tests.Candidates;

/// <summary>Verifies readiness precedence and latest-attempt semantics.</summary>
public sealed class CandidateReadinessCalculatorTests
{
    private readonly CandidateReadinessCalculator _calculator = new();

    /// <summary>A later Completed recovery attempt supersedes the original NoShow.</summary>
    [Fact]
    public void RecoveryCompletionMakesCandidateReady()
    {
        var candidateId = Guid.NewGuid();
        var original = Guid.NewGuid();
        var recovery = Guid.NewGuid();
        var type = AppointmentTypeIds.MedicalCheckUp;
        var snapshot = new CandidateReadinessSnapshot(
            candidateId,
            Guid.NewGuid(),
            [type],
            original,
            [
                Attempt(type, original, BookingStatus.Active, BookingAppointmentStatus.NoShow, 1),
                Attempt(type, recovery, BookingStatus.Concluded, BookingAppointmentStatus.Completed, 2),
            ]);

        var actual = _calculator.Calculate(snapshot);

        Assert.Equal(CandidateReadinessCode.Ready, actual.Code);
        Assert.Empty(actual.OutstandingAppointmentTypes);
    }

    /// <summary>Any non-cancelled Completed attempt satisfies the type despite a later NoShow.</summary>
    [Fact]
    public void AnyCompletedAttemptControlsOutcome()
    {
        var candidateId = Guid.NewGuid();
        var original = Guid.NewGuid();
        var recovery = Guid.NewGuid();
        var type = AppointmentTypeIds.UniformFitting;
        var snapshot = new CandidateReadinessSnapshot(
            candidateId,
            Guid.NewGuid(),
            [type],
            original,
            [
                Attempt(type, original, BookingStatus.Active, BookingAppointmentStatus.Completed, 1),
                Attempt(type, recovery, BookingStatus.Concluded, BookingAppointmentStatus.NoShow, 2),
            ]);

        var actual = _calculator.Calculate(snapshot);

        Assert.Equal(CandidateReadinessCode.Ready, actual.Code);
        Assert.Empty(actual.OutstandingAppointmentTypes);
    }

    /// <summary>A latest NoShow with no completion is outstanding and recoverable.</summary>
    [Fact]
    public void LatestNoShowIsRecoverable()
    {
        var original = Guid.NewGuid();
        var type = AppointmentTypeIds.MedicalCheckUp;
        var actual = _calculator.Calculate(new CandidateReadinessSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), [type], original,
            [Attempt(type, original, BookingStatus.Active, BookingAppointmentStatus.NoShow, 1)]));

        Assert.Equal(CandidateReadinessCode.AppointmentsOutstanding, actual.Code);
        var outstanding = Assert.Single(actual.OutstandingAppointmentTypes);
        Assert.Equal("MED", outstanding.Code);
        Assert.True(outstanding.IsRecoverable);
    }

    /// <summary>Unassigned reconciliation state wins over missing Booking state.</summary>
    [Fact]
    public void UnassignedGroupHasHighestFailurePrecedence()
    {
        var actual = _calculator.Calculate(new CandidateReadinessSnapshot(
            Guid.NewGuid(), null, [], null, []));

        Assert.Equal(CandidateReadinessCode.EmployeeGroupUnassigned, actual.Code);
    }

    private static CandidateReadinessAttempt Attempt(
        Guid typeId,
        Guid bookingId,
        BookingStatus bookingStatus,
        BookingAppointmentStatus status,
        int day) => new(
            Guid.NewGuid(), typeId, status, bookingId, bookingStatus,
            DateTimeOffset.Parse($"2026-09-{day:00}T09:00:00Z"));
}
