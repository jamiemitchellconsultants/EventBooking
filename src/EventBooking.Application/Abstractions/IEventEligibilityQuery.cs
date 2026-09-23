namespace EventBooking.Application.Abstractions;

/// <summary>
/// Which events a attendee may be offered. Relational division: an event qualifies only if it
/// covers every required appointment type with at least one place left, and an event that does not
/// list a required type at all is never a candidate however much room its other types have.
///
/// The rule is executed in the database, in one statement, because the alternative is loading
/// every active event and its capacity rows into memory to filter them there (design 04 — invite
/// selection). The query only proposes candidates: capacity is re-checked under lock at booking
/// time, so a stale option can never overbook.
/// </summary>
public interface IEventEligibilityQuery
{
    /// <summary>
    /// The eligible events, earliest first by start instant and then by identifier, at most
    /// <paramref name="count"/> of them.
    /// </summary>
    /// <param name="requiredAppointmentTypeIds">Every type the attendee needs; duplicates collapse.</param>
    /// <param name="locationIds">The locations the caller will offer; an event elsewhere is not a candidate.</param>
    /// <param name="excludeEventIds">Events the caller has already offered or ruled out.</param>
    /// <param name="count">The most identifiers to return.</param>
    /// <param name="asOf">The instant to judge "still to come" against.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Guid>> FindEligibleEventsAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        IReadOnlyCollection<Guid> locationIds,
        IReadOnlyCollection<Guid> excludeEventIds,
        int count,
        DateTimeOffset asOf,
        CancellationToken cancellationToken);

    /// <summary>
    /// How many events the same filters match, with no limit. The invite dialog shows the number
    /// before it shows the options, and counting in the database avoids fetching rows to discard.
    /// </summary>
    /// <param name="requiredAppointmentTypeIds">Every type the attendee needs; duplicates collapse.</param>
    /// <param name="locationIds">The locations the caller will offer.</param>
    /// <param name="excludeEventIds">Events the caller has already offered or ruled out.</param>
    /// <param name="asOf">The instant to judge "still to come" against.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<int> CountEligibleEventsAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        IReadOnlyCollection<Guid> locationIds,
        IReadOnlyCollection<Guid> excludeEventIds,
        DateTimeOffset asOf,
        CancellationToken cancellationToken);
}
