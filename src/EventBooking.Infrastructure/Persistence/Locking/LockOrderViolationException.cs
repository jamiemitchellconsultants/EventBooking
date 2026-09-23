namespace EventBooking.Infrastructure.Persistence.Locking;

/// <summary>
/// Thrown when a transaction asks for a lock below one it already holds. It is a defect in the
/// calling command, not a runtime condition: the alternative is a deadlock that appears only when
/// two particular commands overlap in production.
/// </summary>
public sealed class LockOrderViolationException(LockLevel held, LockLevel requested)
    : InvalidOperationException(
        $"This transaction already holds a {held} lock, so it cannot now take a {requested} lock. " +
        "The order is Attendee, EventProposal, Event, EventCapacity: take every lock the command " +
        "needs in that order, before the first write.")
{
    /// <summary>Gets the highest level this transaction already held.</summary>
    public LockLevel Held { get; } = held;

    /// <summary>Gets the level that was asked for.</summary>
    public LockLevel Requested { get; } = requested;
}
