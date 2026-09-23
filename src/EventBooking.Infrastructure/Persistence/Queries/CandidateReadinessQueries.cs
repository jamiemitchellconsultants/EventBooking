using EventBooking.Application.Abstractions;
using EventBooking.Application.Candidates;
using EventBooking.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Queries;

/// <summary>Projects the readiness journey without personal, token, audit, or capacity data.</summary>
/// <param name="context">The read-only persistence context.</param>
public sealed class CandidateReadinessQueries(EventBookingDbContext context) : ICandidateReadinessQueries
{
    /// <inheritdoc />
    public async Task<CandidateReadinessSnapshot?> GetSnapshotAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        var candidate = await context.Candidates
            .AsNoTracking()
            .Where(c => c.Id == candidateId)
            .Select(c => new
            {
                c.Id,
                c.EmployeeGroupId,
                RequirementTypeIds = c.Requirements
                    .Select(r => r.AppointmentTypeId)
                    .ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (candidate is null)
        {
            return null;
        }

        var originalId = await context.Bookings
            .AsNoTracking()
            .Where(b => b.CandidateId == candidateId
                && b.Status == BookingStatus.Active
                && b.RecoveryOfBookingId == null)
            .Select(b => (Guid?)b.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (originalId is null)
        {
            return new CandidateReadinessSnapshot(
                candidate.Id, candidate.EmployeeGroupId, candidate.RequirementTypeIds, null, []);
        }

        var bookings = await context.Bookings
            .AsNoTracking()
            .Where(b => b.Id == originalId.Value || b.RecoveryOfBookingId == originalId.Value)
            .Select(b => new { b.Id, b.Status, b.CreatedAt })
            .ToListAsync(cancellationToken);

        var journeyIds = bookings.Select(b => b.Id).ToList();
        var states = bookings.ToDictionary(b => b.Id);

        var attempts = await context.BookingAppointments
            .AsNoTracking()
            .Where(a => journeyIds.Contains(a.BookingId))
            .Select(a => new { a.Id, a.BookingId, a.AppointmentTypeId, a.Status })
            .ToListAsync(cancellationToken);

        return new CandidateReadinessSnapshot(
            candidate.Id,
            candidate.EmployeeGroupId,
            candidate.RequirementTypeIds,
            originalId,
            attempts
                .Select(a => new CandidateReadinessAttempt(
                    a.Id,
                    a.AppointmentTypeId,
                    a.Status,
                    a.BookingId,
                    states[a.BookingId].Status,
                    states[a.BookingId].CreatedAt))
                .ToList());
    }
}
