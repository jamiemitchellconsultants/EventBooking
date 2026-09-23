using EventBooking.Application.Invites;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Tests.Invites;

/// <summary>Verifies recovery selects only current, unsatisfied latest NoShow types.</summary>
public sealed class RecoveryRequirementSelectorTests
{
    /// <summary>Completed, merely outstanding, stale, and already-pending types are excluded.</summary>
    [Fact]
    public void SelectsOnlyRecoverableNoShows()
    {
        var med = AppointmentTypeIds.MedicalCheckUp;
        var dat = AppointmentTypeIds.DrugAndAlcoholTesting;
        var uni = AppointmentTypeIds.UniformFitting;
        var attempts = new[]
        {
            Attempt(med, BookingAppointmentStatus.NoShow, 1),
            Attempt(dat, BookingAppointmentStatus.NoShow, 1),
            Attempt(dat, BookingAppointmentStatus.Completed, 2),
            Attempt(uni, BookingAppointmentStatus.Expected, 1),
            Attempt(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), BookingAppointmentStatus.NoShow, 1),
        };

        var actual = new RecoveryRequirementSelector().Select(
            [med, dat, uni], attempts, [uni]);

        Assert.Equal([med], actual);
    }

    private static RecoveryAttempt Attempt(
        Guid typeId,
        BookingAppointmentStatus status,
        int day) => new(
            Guid.NewGuid(), typeId, status,
            DateTimeOffset.Parse($"2026-09-{day:00}T09:00:00Z"));
}
