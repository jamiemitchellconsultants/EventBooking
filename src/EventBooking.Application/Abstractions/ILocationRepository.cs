using EventBooking.Domain.Locations;

namespace EventBooking.Application.Abstractions;

/// <summary>Reads and stages Admin-managed locations.</summary>
public interface ILocationRepository
{
    /// <summary>Gets a location by identifier, including inactive rows.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Location?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Gets a location from a trimmed case-insensitive canonical-code input.</summary>
    /// <param name="code">The code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Location?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>Lists every location, inactive included.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Location>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Stages a new location for the next save.</summary>
    /// <param name="location">The location.</param>
    void Add(Location location);
}
