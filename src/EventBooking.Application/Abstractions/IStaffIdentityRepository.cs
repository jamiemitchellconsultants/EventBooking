using EventBooking.Domain.Access;

namespace EventBooking.Application.Abstractions;

/// <summary>Stores identity-provider pairs learned from validated authenticated tokens.</summary>
public interface IStaffIdentityRepository
{
    /// <summary>Gets the identity addressed by the canonical enterprise staff number.</summary>
    /// <param name="staffId">The staff number to resolve.</param>
    /// <param name="cancellationToken">Stops the database query.</param>
    /// <returns>The matching identity, or null when it has not been observed.</returns>
    Task<StaffIdentity?> GetByStaffIdAsync(
        StaffId staffId,
        CancellationToken cancellationToken);

    /// <summary>Lists all observed identity pairs for staff-access projection.</summary>
    /// <param name="cancellationToken">Stops the database query.</param>
    /// <returns>The observed identities ordered by provider key.</returns>
    Task<IReadOnlyList<StaffIdentity>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Atomically creates or refreshes the mirror row for a provider identity.</summary>
    /// <param name="staffUserId">The provider-assigned identity key.</param>
    /// <param name="staffId">The enterprise staff number carried by its token.</param>
    /// <param name="displayName">
    /// The name carried by the token, or null when it carries none. Overwrites the stored value on
    /// every write, including back to null.
    /// </param>
    /// <param name="lastSeenAt">The approximate observation time.</param>
    /// <param name="cancellationToken">Stops the database command.</param>
    Task UpsertAsync(
        Guid staffUserId,
        StaffId staffId,
        string? displayName,
        DateTimeOffset lastSeenAt,
        CancellationToken cancellationToken);
}
