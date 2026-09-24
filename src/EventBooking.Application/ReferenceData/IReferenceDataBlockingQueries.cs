using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Locations;

namespace EventBooking.Application.ReferenceData;

/// <summary>
/// Blocking counts behind the in-use refusals. Counts are exact, not estimates.
/// </summary>
public interface IReferenceDataBlockingQueries
{
    /// <summary>Counts open proposals and future active events at one location.</summary>
    /// <param name="locationId">The location id.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<LocationUsage> LocationUsageAsync(Guid locationId, CancellationToken ct);

    /// <summary>Counts proposals, events and mapped groups using one appointment type.</summary>
    /// <param name="typeId">The type id.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<AppointmentTypeUsage> AppointmentTypeUsageAsync(Guid typeId, CancellationToken ct);

    /// <summary>Counts every member of one attendee group.</summary>
    /// <param name="groupId">The group id.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<int> AttendeeGroupMemberCountAsync(Guid groupId, CancellationToken ct);

    /// <summary>Counts members holding an active original booking.</summary>
    /// <param name="groupId">The group id.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<int> AttendeeGroupBlockingMemberCountAsync(Guid groupId, CancellationToken ct);
}
