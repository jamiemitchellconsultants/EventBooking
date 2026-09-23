namespace EventBooking.Infrastructure.Persistence.Locking;

/// <summary>
/// Records the highest <see cref="LockLevel"/> the current transaction has taken, and refuses a
/// descent. Scoped to one DbContext, and reset by the unit of work whenever a transaction begins
/// or ends: without the reset, one command's capacity lock would make every later attendee lock on
/// the same connection look like a violation.
/// </summary>
public sealed class TransactionLocks
{
    /// <summary>
    /// The guard is on in a Debug build, which is what tests run. A released build still records
    /// the level — the comparison is free — but does not turn a lock order it has never seen in a
    /// test into a 500 for the attendee who happened to hit it.
    /// </summary>
    public const bool EnforcedByDefault =
#if DEBUG
        true;
#else
        false;
#endif

    /// <summary>Creates a tracker enforcing the order according to the build.</summary>
    public TransactionLocks()
        : this(EnforcedByDefault)
    {
    }

    /// <summary>Creates a tracker, overriding whether a descent throws.</summary>
    /// <param name="enforced">True to throw on a descent; false to record it and carry on.</param>
    public TransactionLocks(bool enforced) => Enforced = enforced;

    /// <summary>Gets whether a descent throws.</summary>
    public bool Enforced { get; }

    /// <summary>Gets the highest level taken since the last reset, or null if none has been.</summary>
    public LockLevel? Highest { get; private set; }

    /// <summary>Records a lock about to be taken, refusing one below the level already held.</summary>
    /// <param name="level">The level being taken.</param>
    public void Enter(LockLevel level)
    {
        if (Highest is { } held && level < held)
        {
            if (Enforced)
            {
                throw new LockOrderViolationException(held, level);
            }

            return;
        }

        Highest = level;
    }

    /// <summary>Forgets what the last transaction held.</summary>
    public void Reset() => Highest = null;
}
