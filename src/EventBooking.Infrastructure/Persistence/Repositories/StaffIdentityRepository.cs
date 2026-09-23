using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Repositories;

/// <summary>Persists and resolves identity-provider pairs in PostgreSQL.</summary>
public sealed class StaffIdentityRepository(EventBookingDbContext context)
    : IStaffIdentityRepository
{
    /// <inheritdoc />
    public Task<StaffIdentity?> GetByStaffIdAsync(
        StaffId staffId,
        CancellationToken cancellationToken) =>
        context.StaffIdentities.AsNoTracking().SingleOrDefaultAsync(
            identity => identity.StaffId == staffId,
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<StaffIdentity>> ListAsync(
        CancellationToken cancellationToken) =>
        await context.StaffIdentities
            .AsNoTracking()
            .OrderBy(identity => identity.StaffUserId)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task UpsertAsync(
        Guid staffUserId,
        StaffId staffId,
        string? displayName,
        DateTimeOffset lastSeenAt,
        CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO staff_identity (staff_user_id, staff_id, display_name, last_seen_at)
             VALUES ({staffUserId}, {staffId.Value}, {displayName}, {lastSeenAt})
             ON CONFLICT (staff_user_id) DO UPDATE
             SET staff_id = EXCLUDED.staff_id,
                 display_name = EXCLUDED.display_name,
                 last_seen_at = EXCLUDED.last_seen_at
             """,
            cancellationToken);
    }
}
