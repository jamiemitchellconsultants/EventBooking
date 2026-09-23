using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Abstractions;

/// <summary>The latest delivery projection for one candidate, including whether its context remains retryable.</summary>
/// <param name="CandidateId">The candidate whose latest delivery is projected.</param>
/// <param name="TemplateName">The server-owned template of the delivery.</param>
/// <param name="SentAt">The latest attempt or pending timestamp.</param>
/// <param name="Status">The durable delivery status.</param>
/// <param name="CanRetry">Whether current domain state still permits this delivery to be regenerated.</param>
public sealed record CandidateEmailStatusRow(
    Guid CandidateId,
    EmailTemplate TemplateName,
    DateTimeOffset SentAt,
    EmailStatus Status,
    bool CanRetry);

/// <summary>Defines awaiting availability row for the current use case.</summary>
/// <param name="CandidateId">The candidate id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="RequiredCodes">The required codes.</param>
/// <param name="WaitingSince">The waiting since.</param>
/// <param name="DaysWaiting">The days waiting.</param>
public sealed record AwaitingAvailabilityRow(
    Guid CandidateId,
    string Name,
    string Email,
    IReadOnlyList<string> RequiredCodes,
    DateOnly WaitingSince,
    int DaysWaiting);

/// <summary>Defines no response row for the current use case.</summary>
/// <param name="CandidateId">The candidate id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="RequiredCodes">The required codes.</param>
/// <param name="GaveUpOn">The gave up on.</param>
public sealed record NoResponseRow(
    Guid CandidateId,
    string Name,
    string Email,
    IReadOnlyList<string> RequiredCodes,
    DateOnly GaveUpOn);

/// <summary>Defines slot capacity row for the current use case.</summary>
/// <param name="Code">The code.</param>
/// <param name="TotalHeadcount">The total headcount.</param>
/// <param name="RemainingCapacity">The remaining capacity.</param>
public sealed record SlotCapacityRow(string Code, int TotalHeadcount, int RemainingCapacity);

/// <summary>Defines slot overview row for the current use case.</summary>
/// <param name="ConfirmedSlotId">The confirmed slot id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="Capacities">The capacities.</param>
/// <param name="ActiveBookings">The active bookings.</param>
public sealed record SlotOverviewRow(
    Guid ConfirmedSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<SlotCapacityRow> Capacities,
    int ActiveBookings);

/// <summary>
/// The dashboard read side. Implementations query and project directly, returning no entities and
/// offering no writes.
/// </summary>
public interface IDashboardQueries
{
    /// <summary>Provides awaiting availability async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<AwaitingAvailabilityRow>> AwaitingAvailabilityAsync(
        CancellationToken cancellationToken);

    /// <summary>Provides no response async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<NoResponseRow>> NoResponseAsync(CancellationToken cancellationToken);

    /// <summary>Provides slots overview async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<SlotOverviewRow>> SlotsOverviewAsync(CancellationToken cancellationToken);

    /// <summary>The latest EmailLog row per candidate that has ever had one written.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<CandidateEmailStatusRow>> LatestEmailStatusAsync(CancellationToken cancellationToken);
}
