using EventBooking.Domain.Access;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines istaff access profile repository for the current use case.</summary>
public interface IStaffAccessProfileRepository
{
    /// <summary>Provides get async within this contract.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<StaffAccessProfile?> GetAsync(Guid staffUserId, CancellationToken cancellationToken);
    /// <summary>Provides list async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<StaffAccessProfile>> ListAsync(CancellationToken cancellationToken);
    /// <summary>Provides lock all async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<StaffAccessProfile>> LockAllAsync(CancellationToken cancellationToken);
    /// <summary>Provides add within this contract.</summary>
    /// <param name="profile">The profile.</param>
    void Add(StaffAccessProfile profile);
    /// <summary>Provides remove within this contract.</summary>
    /// <param name="profile">The profile.</param>
    void Remove(StaffAccessProfile profile);
}
