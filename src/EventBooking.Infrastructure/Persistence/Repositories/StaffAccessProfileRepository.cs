using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Repositories;

public sealed class StaffAccessProfileRepository(EventBookingDbContext context)
    : IStaffAccessProfileRepository
{
    public Task<StaffAccessProfile?> GetAsync(
        Guid staffUserId,
        CancellationToken cancellationToken) =>
        context.StaffAccessProfiles.AsNoTracking().SingleOrDefaultAsync(
            profile => profile.StaffUserId == staffUserId,
            cancellationToken);

    public async Task<IReadOnlyList<StaffAccessProfile>> ListAsync(
        CancellationToken cancellationToken) =>
        await context.StaffAccessProfiles
            .AsNoTracking()
            .OrderBy(profile => profile.StaffUserId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StaffAccessProfile>> LockAllAsync(
        CancellationToken cancellationToken)
    {
        // Serialises profile-set decisions even when a role/type currently has no row to lock.
        // The key is a fixed application constant reserved for staff-access administration.
        await context.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(710071)", cancellationToken);

        return await context.StaffAccessProfiles
            .FromSqlRaw("SELECT * FROM staff_access_profile ORDER BY staff_user_id FOR UPDATE")
            .ToListAsync(cancellationToken);
    }

    public void Add(StaffAccessProfile profile) => context.StaffAccessProfiles.Add(profile);

    public void Remove(StaffAccessProfile profile) => context.StaffAccessProfiles.Remove(profile);
}
