using EventBooking.Domain.Settings;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines isystem settings repository for the current use case.</summary>
public interface ISystemSettingsRepository
{
    /// <summary>Returns the single seeded settings row. Never null.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<SystemSettings> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Locks the single seeded settings row for update inside the caller's transaction, so two
    /// concurrent saves serialize instead of racing the version check.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<SystemSettings> LockAsync(CancellationToken cancellationToken);
}
