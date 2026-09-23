using EventBooking.Domain.Settings;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines isystem settings repository for the current use case.</summary>
public interface ISystemSettingsRepository
{
    /// <summary>Returns the single seeded settings row. Never null.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<SystemSettings> GetAsync(CancellationToken cancellationToken);
}
