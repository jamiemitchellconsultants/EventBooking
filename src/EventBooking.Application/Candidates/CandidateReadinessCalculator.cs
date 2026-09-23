using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Candidates;

/// <summary>Calculates deterministic readiness from one persistence snapshot.</summary>
public sealed class CandidateReadinessCalculator
{
    /// <summary>Calculates one deterministic result without inferring an Employee Group.</summary>
    /// <param name="snapshot">The authorized journey projection.</param>
    /// <returns>Ready, or the highest-precedence failure with outstanding types.</returns>
    public CandidateReadiness Calculate(CandidateReadinessSnapshot snapshot)
    {
        if (!snapshot.EmployeeGroupId.HasValue)
        {
            return new CandidateReadiness(
                snapshot.CandidateId, CandidateReadinessCode.EmployeeGroupUnassigned, []);
        }

        if (!snapshot.ActiveOriginalBookingId.HasValue)
        {
            return new CandidateReadiness(
                snapshot.CandidateId, CandidateReadinessCode.NoActiveBooking, []);
        }

        var current = snapshot.CurrentRequirementTypeIds;
        if (current.Count == 0
            || current.Distinct().Count() != current.Count
            || current.Any(id => !AppointmentTypeIds.All.Contains(id)))
        {
            return Mismatch(snapshot);
        }

        var live = snapshot.Attempts
            .Where(attempt => attempt.BookingStatus != BookingStatus.Cancelled)
            .ToList();

        var originalTypes = live
            .Where(attempt => attempt.BookingId == snapshot.ActiveOriginalBookingId.Value)
            .Select(attempt => attempt.AppointmentTypeId)
            .Distinct()
            .Order()
            .ToList();

        if (!originalTypes.SequenceEqual(current.Order())
            || live.Any(attempt => !originalTypes.Contains(attempt.AppointmentTypeId)))
        {
            return Mismatch(snapshot);
        }

        var outstanding = new List<OutstandingAppointmentType>();
        foreach (var typeId in current.Order())
        {
            var attempts = live
                .Where(attempt => attempt.AppointmentTypeId == typeId)
                .OrderBy(attempt => attempt.BookingCreatedAt)
                .ThenBy(attempt => attempt.BookingAppointmentId)
                .ToList();

            if (attempts.Any(attempt => attempt.Status == BookingAppointmentStatus.Completed))
            {
                continue;
            }

            var latest = attempts.Count == 0 ? null : attempts[^1];
            outstanding.Add(new OutstandingAppointmentType(
                AppointmentTypeIds.CodeOf(typeId),
                AppointmentTypeIds.NameOf(typeId),
                latest is not null && latest.Status == BookingAppointmentStatus.NoShow));
        }

        outstanding.Sort((left, right) => string.Compare(left.Code, right.Code, StringComparison.Ordinal));

        return outstanding.Count == 0
            ? new CandidateReadiness(snapshot.CandidateId, CandidateReadinessCode.Ready, [])
            : new CandidateReadiness(
                snapshot.CandidateId, CandidateReadinessCode.AppointmentsOutstanding, outstanding);
    }

    private static CandidateReadiness Mismatch(CandidateReadinessSnapshot snapshot) =>
        new(snapshot.CandidateId, CandidateReadinessCode.RequirementSnapshotMismatch, []);
}
