using EventBooking.Application.Abstractions;
using EventBooking.Domain.AttendeeGroups;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Repositories;

/// <summary>Reads change-controlled Attendee Group reference data with complete mappings.</summary>
public sealed class AttendeeGroupRepository(EventBookingDbContext context) : IAttendeeGroupRepository
{
    /// <summary>Gets a group by stable identifier, including inactive or inconsistent rows.</summary>
    public Task<AttendeeGroup?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.AttendeeGroups
            .Include(group => group.Requirements)
            .SingleOrDefaultAsync(group => group.Id == id, cancellationToken);

    /// <summary>Gets a group from a trimmed case-insensitive canonical-code input.</summary>
    public Task<AttendeeGroup?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return context.AttendeeGroups
            .Include(group => group.Requirements)
            .SingleOrDefaultAsync(group => group.Code == normalized, cancellationToken);
    }

    /// <summary>Lists active mapped groups ordered by display name.</summary>
    public async Task<IReadOnlyList<AttendeeGroup>> ListActiveAsync(CancellationToken cancellationToken) =>
        await context.AttendeeGroups
            .Include(group => group.Requirements)
            .Where(group => group.IsActive && group.Requirements.Any())
            .OrderBy(group => group.Name)
            .ToListAsync(cancellationToken);

    /// <summary>Lists every group, inactive included, ordered by display name.</summary>
    public async Task<IReadOnlyList<AttendeeGroup>> ListAsync(CancellationToken cancellationToken) =>
        await context.AttendeeGroups
            .Include(group => group.Requirements)
            .OrderBy(group => group.Name)
            .ToListAsync(cancellationToken);

    /// <summary>Stages a new attendee group for the next save.</summary>
    public void Add(AttendeeGroup group) => context.AttendeeGroups.Add(group);
}
