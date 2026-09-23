namespace EventBooking.Domain.Events;

/// <summary>What a headcount adjustment did, or why it was refused (FR-3.5, FR-3.6).</summary>
public enum CapacityAdjustmentStatus
{
    /// <summary>The total and the remaining count both moved by the same delta.</summary>
    Adjusted,

    /// <summary>The submitted total equalled the current one: nothing changed, so nothing is audited.</summary>
    Unchanged,

    /// <summary>The submitted total was below the type's active-booking count; nothing changed.</summary>
    BelowActiveBookings,
}

/// <summary>
/// The outcome of a headcount adjustment. A refusal carries the minimum the row would accept and
/// the current server values, which is what FR-3.6 requires the Manager to be told.
/// </summary>
/// <param name="Status">What the adjustment did, or why it was refused.</param>
/// <param name="MinimumTotalHeadcount">The lowest total this row would accept.</param>
/// <param name="TotalHeadcount">The total after the call.</param>
/// <param name="RemainingCapacity">The remaining count after the call.</param>
public readonly record struct CapacityAdjustment(
    CapacityAdjustmentStatus Status,
    int MinimumTotalHeadcount,
    int TotalHeadcount,
    int RemainingCapacity)
{
    /// <summary>Whether anything moved, and so whether there is anything to audit.</summary>
    public bool Changed => Status == CapacityAdjustmentStatus.Adjusted;
}
