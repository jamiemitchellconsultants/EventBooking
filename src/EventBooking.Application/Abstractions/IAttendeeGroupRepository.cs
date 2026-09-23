using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Abstractions;

/// <summary>Reads change-controlled Attendee Group reference data with complete mappings.</summary>
public interface IAttendeeGroupRepository
{
    /// <summary>Gets a group by stable identifier, including inactive or inconsistent rows.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AttendeeGroup?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Gets a group from a trimmed case-insensitive canonical-code input.</summary>
    /// <param name="code">The code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AttendeeGroup?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>Lists active mapped groups ordered by display name.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<AttendeeGroup>> ListActiveAsync(CancellationToken cancellationToken);
}
