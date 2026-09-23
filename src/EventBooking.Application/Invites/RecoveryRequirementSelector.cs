using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Invites;

/// <summary>One non-cancelled Booking Appointment considered for recovery eligibility.</summary>
/// <param name="BookingAppointmentId">The stable appointment-record identifier.</param>
/// <param name="AppointmentTypeId">The required appointment type delivered by the attempt.</param>
/// <param name="Status">The attempt's independent operational status.</param>
/// <param name="BookingCreatedAt">When the parent Booking was created, ordering repeat attempts.</param>
public sealed record RecoveryAttempt(
    Guid BookingAppointmentId,
    Guid AppointmentTypeId,
    BookingAppointmentStatus Status,
    DateTimeOffset BookingCreatedAt);

/// <summary>Selects the current requirement types whose latest attempt is an unsatisfied no-show.</summary>
public sealed class RecoveryRequirementSelector
{
    /// <summary>Returns every current type whose latest attempt is NoShow and none is Completed.</summary>
    /// <param name="currentRequirementTypeIds">The candidate's current derived requirement set.</param>
    /// <param name="attempts">Non-cancelled attempts across the journey, in any order.</param>
    /// <param name="typesAlreadyPendingRecovery">Types a pending recovery already covers.</param>
    /// <returns>The recoverable type identifiers in stable order.</returns>
    public IReadOnlyList<Guid> Select(
        IReadOnlyCollection<Guid> currentRequirementTypeIds,
        IReadOnlyCollection<RecoveryAttempt> attempts,
        IReadOnlyCollection<Guid> typesAlreadyPendingRecovery)
    {
        var current = currentRequirementTypeIds.ToHashSet();
        var pending = typesAlreadyPendingRecovery.ToHashSet();

        return attempts
            .Where(attempt => current.Contains(attempt.AppointmentTypeId))
            .Where(attempt => !pending.Contains(attempt.AppointmentTypeId))
            .GroupBy(attempt => attempt.AppointmentTypeId)
            .Where(group => !group.Any(attempt => attempt.Status == BookingAppointmentStatus.Completed))
            .Where(group => Latest(group).Status == BookingAppointmentStatus.NoShow)
            .Select(group => group.Key)
            .Order()
            .ToList();
    }

    private static RecoveryAttempt Latest(IEnumerable<RecoveryAttempt> attempts) =>
        attempts
            .OrderBy(attempt => attempt.BookingCreatedAt)
            .ThenBy(attempt => attempt.BookingAppointmentId)
            .Last();
}
