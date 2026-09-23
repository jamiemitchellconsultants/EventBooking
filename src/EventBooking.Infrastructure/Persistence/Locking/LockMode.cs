namespace EventBooking.Infrastructure.Persistence.Locking;

/// <summary>How a repository read should lock the rows it returns.</summary>
public enum LockMode
{
    /// <summary>No lock. The result is a snapshot and must not be used as authority for a write.</summary>
    None = 0,

    /// <summary>`FOR UPDATE`: wait for any conflicting lock, then hold the row until commit.</summary>
    Update = 1,

    /// <summary>
    /// `FOR UPDATE SKIP LOCKED`: take what is free and leave the rest. For work a second worker
    /// may simply process instead, never for a row the caller has to be certain about.
    /// </summary>
    SkipLocked = 2,
}
