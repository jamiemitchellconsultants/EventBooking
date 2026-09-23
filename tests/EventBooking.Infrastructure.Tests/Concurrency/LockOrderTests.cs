using EventBooking.Infrastructure.Persistence.Locking;

namespace EventBooking.Infrastructure.Tests.Concurrency;

/// <summary>
/// The documented order is `Attendee`, `EventProposal`, `Invite`, `Booking`, `Event`,
/// `EventCapacity`. Two transactions that take the same rows in different orders deadlock, and a
/// deadlock is a 500 to whichever attendee PostgreSQL picks. The tracker turns that runtime
/// coin-toss into a failing test.
/// </summary>
public class LockOrderTests
{
    [Fact]
    public void AnAttendeeLockAfterACapacityLockIsALockOrderViolation()
    {
        var locks = new TransactionLocks(enforced: true);
        locks.Enter(LockLevel.EventCapacity);

        var ex = Assert.Throws<LockOrderViolationException>(() => locks.Enter(LockLevel.Attendee));

        Assert.Equal(LockLevel.EventCapacity, ex.Held);
        Assert.Equal(LockLevel.Attendee, ex.Requested);
        Assert.Contains("Attendee", ex.Message, StringComparison.Ordinal);
        Assert.Contains("EventCapacity", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDocumentedOrderIsAccepted()
    {
        var locks = new TransactionLocks(enforced: true);

        locks.Enter(LockLevel.Attendee);
        locks.Enter(LockLevel.EventProposal);
        locks.Enter(LockLevel.Invite);
        locks.Enter(LockLevel.Booking);
        locks.Enter(LockLevel.Event);
        locks.Enter(LockLevel.EventCapacity);

        Assert.Equal(LockLevel.EventCapacity, locks.Highest);
    }

    /// <summary>Several rows at one level is the normal case: three capacity rows, two events.</summary>
    [Fact]
    public void RepeatingTheLevelAlreadyHeldIsAllowed()
    {
        var locks = new TransactionLocks(enforced: true);

        locks.Enter(LockLevel.Event);
        locks.Enter(LockLevel.Event);

        Assert.Equal(LockLevel.Event, locks.Highest);
    }

    [Theory]
    [InlineData(LockLevel.EventProposal, LockLevel.Attendee)]
    [InlineData(LockLevel.Invite, LockLevel.Attendee)]
    [InlineData(LockLevel.Booking, LockLevel.Invite)]
    [InlineData(LockLevel.Event, LockLevel.Booking)]
    [InlineData(LockLevel.Event, LockLevel.EventProposal)]
    [InlineData(LockLevel.Event, LockLevel.Attendee)]
    [InlineData(LockLevel.EventCapacity, LockLevel.Event)]
    public void EveryDescentIsRefused(LockLevel held, LockLevel requested)
    {
        var locks = new TransactionLocks(enforced: true);
        locks.Enter(held);

        Assert.Throws<LockOrderViolationException>(() => locks.Enter(requested));
    }

    /// <summary>
    /// The next transaction starts from nothing. Without the reset, one command's capacity lock
    /// would make every later attendee lock on the same connection look like a violation.
    /// </summary>
    [Fact]
    public void ResettingClearsWhatTheLastTransactionHeld()
    {
        var locks = new TransactionLocks(enforced: true);
        locks.Enter(LockLevel.EventCapacity);

        locks.Reset();

        Assert.Null(locks.Highest);
        locks.Enter(LockLevel.Attendee);
        Assert.Equal(LockLevel.Attendee, locks.Highest);
    }

    /// <summary>
    /// Production still records the order — the guard is what is off, so a released build pays a
    /// comparison rather than an exception on a path a test never reached.
    /// </summary>
    [Fact]
    public void AnUnenforcedTrackerRecordsWithoutThrowing()
    {
        var locks = new TransactionLocks(enforced: false);

        locks.Enter(LockLevel.EventCapacity);
        locks.Enter(LockLevel.Attendee);

        Assert.Equal(LockLevel.EventCapacity, locks.Highest);
    }

    [Fact]
    public void TheGuardIsOnByDefaultInADebugBuild()
    {
#if DEBUG
        Assert.True(new TransactionLocks().Enforced);
#else
        Assert.False(new TransactionLocks().Enforced);
#endif
    }
}
