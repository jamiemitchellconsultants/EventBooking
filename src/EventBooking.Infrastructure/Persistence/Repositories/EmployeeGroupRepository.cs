using EventBooking.Application.Abstractions;
using EventBooking.Domain.EmployeeGroups;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Repositories;

/// <summary>Reads change-controlled Employee Group reference data with complete mappings.</summary>
public sealed class EmployeeGroupRepository(EventBookingDbContext context) : IEmployeeGroupRepository
{
    /// <summary>Gets a group by stable identifier, including inactive or inconsistent rows.</summary>
    public Task<EmployeeGroup?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.EmployeeGroups
            .Include(group => group.Requirements)
            .SingleOrDefaultAsync(group => group.Id == id, cancellationToken);

    /// <summary>Gets a group from a trimmed case-insensitive canonical-code input.</summary>
    public Task<EmployeeGroup?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return context.EmployeeGroups
            .Include(group => group.Requirements)
            .SingleOrDefaultAsync(group => group.Code == normalized, cancellationToken);
    }

    /// <summary>Lists active mapped groups ordered by display name.</summary>
    public async Task<IReadOnlyList<EmployeeGroup>> ListActiveAsync(CancellationToken cancellationToken) =>
        await context.EmployeeGroups
            .Include(group => group.Requirements)
            .Where(group => group.IsActive && group.Requirements.Any())
            .OrderBy(group => group.Name)
            .ToListAsync(cancellationToken);
}
