using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Application.Abstractions;

/// <summary>Reads change-controlled Employee Group reference data with complete mappings.</summary>
public interface IEmployeeGroupRepository
{
    /// <summary>Gets a group by stable identifier, including inactive or inconsistent rows.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EmployeeGroup?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Gets a group from a trimmed case-insensitive canonical-code input.</summary>
    /// <param name="code">The code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EmployeeGroup?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>Lists active mapped groups ordered by display name.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<EmployeeGroup>> ListActiveAsync(CancellationToken cancellationToken);
}
