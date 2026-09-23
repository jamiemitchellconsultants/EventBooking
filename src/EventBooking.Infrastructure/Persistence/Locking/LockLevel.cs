namespace EventBooking.Infrastructure.Persistence.Locking;

/// <summary>
/// The documented row-lock order, ascending. Every command takes its locks in this order, so two
/// commands whose row sets overlap wait for each other instead of deadlocking (design 01 — lock
/// ordering; FR-3.3). The numbers are the order itself, not identifiers: nothing persists them.
/// </summary>
public enum LockLevel
{
    /// <summary>The lifecycle root. Anything that changes what an attendee is doing starts here.</summary>
    Attendee = 1,

    /// <summary>The negotiation root.</summary>
    EventProposal = 2,

    /// <summary>One event, locked by id. Several events are locked in ascending id order.</summary>
    Event = 3,

    /// <summary>The capacity rows, locked last and in (event id, appointment type id) order.</summary>
    EventCapacity = 4,
}
