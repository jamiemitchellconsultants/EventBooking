using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Attendees;

/// <summary>Calculates deterministic readiness from one persistence snapshot.</summary>
public sealed class AttendeeReadinessCalculator
{
    /// <summary>Calculates one deterministic result without inferring an Attendee Group.</summary>
    /// <param name="snapshot">The authorized journey projection.</param>
    /// <returns>Ready, or the highest-precedence failure with outstanding types.</returns>
    public AttendeeReadiness Calculate(AttendeeReadinessSnapshot snapshot)
    {
        if (!snapshot.ActiveOriginalBookingId.HasValue)
        {
            return new AttendeeReadiness(
                snapshot.AttendeeId, AttendeeReadinessCode.NoActiveBooking, []);
        }

        var current = snapshot.CurrentRequirementTypeIds;
        if (current.Count == 0
            || current.Distinct().Count() != current.Count
            || current.Any(id => !snapshot.AppointmentTypes.ContainsKey(id)))
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
            var listed = snapshot.AppointmentTypes[typeId];
            outstanding.Add(new OutstandingAppointmentType(
                listed.Code,
                listed.Name,
                latest is not null && latest.Status == BookingAppointmentStatus.NoShow));
        }

        outstanding.Sort((left, right) => string.Compare(left.Code, right.Code, StringComparison.Ordinal));

        return outstanding.Count == 0
            ? new AttendeeReadiness(snapshot.AttendeeId, AttendeeReadinessCode.Ready, [])
            : new AttendeeReadiness(
                snapshot.AttendeeId, AttendeeReadinessCode.AppointmentsOutstanding, outstanding);
    }

    private static AttendeeReadiness Mismatch(AttendeeReadinessSnapshot snapshot) =>
        new(snapshot.AttendeeId, AttendeeReadinessCode.RequirementSnapshotMismatch, []);
}
