namespace EventBooking.Application.Abstractions;

/// <summary>
/// The candidate set behind the workspace event list: active events at the caller's
/// locations that offer their appointment type and start inside the widened window. The
/// exact end bound is the handler's, because it needs the zone resolver.
/// </summary>
public interface IWorkspaceQueries
{
    /// <summary>Lists the candidate workspace event ids for one appointment type.</summary>
    /// <param name="appointmentTypeId">The caller's appointment type.</param>
    /// <param name="locationId">The location to narrow to, or null for every location.</param>
    /// <param name="from">The widened window start.</param>
    /// <param name="to">The window end.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<IReadOnlyList<Guid>> ListWorkspaceEventIdsAsync(
        Guid appointmentTypeId, Guid? locationId,
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
