using EventBooking.Domain.Attendees;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Locking;

/// <summary>
/// Every row lock the application takes, in one place, so the order is a property of this class
/// rather than of how each handler happens to be written. Each helper records its level with the
/// transaction's <see cref="TransactionLocks"/> before issuing the statement, so a command that
/// descends fails in a test instead of deadlocking in production.
/// </summary>
/// <param name="context">The context whose connection holds the transaction.</param>
/// <param name="locks">The tracker for the current transaction.</param>
public sealed class RowLocks(EventBookingDbContext context, TransactionLocks locks)
{
    /// <summary>
    /// For a test or a tool driving one context directly. The tracker is its own, so the order is
    /// checked within this instance and not across a unit of work it does not share.
    /// </summary>
    /// <param name="context">The context whose connection holds the transaction.</param>
    public RowLocks(EventBookingDbContext context)
        : this(context, new TransactionLocks())
    {
    }

    /// <summary>Locks one attendee and loads the requirements lifecycle handlers read.</summary>
    /// <param name="id">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Attendee?> LockAttendeeAsync(Guid id, CancellationToken cancellationToken)
    {
        locks.Enter(LockLevel.Attendee);

        var attendee = (await context.Attendees
            .FromSqlInterpolated($"SELECT * FROM attendee WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken))
            .SingleOrDefault();

        if (attendee is not null)
        {
            await context.Entry(attendee).Collection(item => item.Requirements)
                .LoadAsync(cancellationToken);
        }

        return attendee;
    }

    /// <summary>
    /// Takes the attendee row only if it is free, and returns null when another transaction holds
    /// it. For work a second worker may simply pick up instead — never for a row the caller has to
    /// be certain about, where null would be read as "no such attendee".
    /// </summary>
    /// <param name="id">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Attendee?> SkipLockedAttendeeAsync(Guid id, CancellationToken cancellationToken)
    {
        locks.Enter(LockLevel.Attendee);

        var attendee = (await context.Attendees
            .FromSqlInterpolated(
                $"SELECT * FROM attendee WHERE id = {id} FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken))
            .SingleOrDefault();

        if (attendee is not null)
        {
            await context.Entry(attendee).Collection(item => item.Requirements)
                .LoadAsync(cancellationToken);
        }

        return attendee;
    }

    /// <summary>
    /// Locks every member of one attendee group in ascending id order and loads each member's
    /// requirements. The group-requirement replacement takes these before re-deriving, so two
    /// concurrent replacements of any groups serialize on the shared rows in the same order.
    /// </summary>
    /// <param name="groupId">The group id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<IReadOnlyList<Attendee>> LockAttendeesByGroupAsync(
        Guid groupId, CancellationToken cancellationToken)
    {
        locks.Enter(LockLevel.Attendee);

        var members = await context.Attendees
            .FromSql(
                $"""
                 SELECT * FROM attendee
                 WHERE attendee_group_id = {groupId}
                 ORDER BY id
                 FOR UPDATE
                 """)
            .ToListAsync(cancellationToken);

        foreach (var member in members)
        {
            await context.Entry(member).Collection(item => item.Requirements)
                .LoadAsync(cancellationToken);
        }

        return members;
    }

    /// <summary>Locks one proposal and loads its acceptances and listed types.</summary>
    /// <param name="id">The proposal id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<EventProposal?> LockProposalAsync(Guid id, CancellationToken cancellationToken)
    {
        locks.Enter(LockLevel.EventProposal);

        var proposal = (await context.EventProposals
            .FromSqlInterpolated($"SELECT * FROM event_proposal WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken))
            .SingleOrDefault();

        if (proposal is not null)
        {
            await context.Entry(proposal).Collection(item => item.Acceptances)
                .LoadAsync(cancellationToken);
            await context.Entry(proposal).Collection(item => item.ListedTypes)
                .LoadAsync(cancellationToken);
        }

        return proposal;
    }

    /// <summary>Takes the proposal row only if it is free, returning null when it is held.</summary>
    /// <param name="id">The proposal id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<EventProposal?> SkipLockedProposalAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        locks.Enter(LockLevel.EventProposal);

        var proposal = (await context.EventProposals
            .FromSqlInterpolated(
                $"SELECT * FROM event_proposal WHERE id = {id} FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken))
            .SingleOrDefault();

        if (proposal is not null)
        {
            await context.Entry(proposal).Collection(item => item.Acceptances)
                .LoadAsync(cancellationToken);
            await context.Entry(proposal).Collection(item => item.ListedTypes)
                .LoadAsync(cancellationToken);
        }

        return proposal;
    }

    /// <summary>
    /// Records an invite lock the invite repository is about to take. The SQL stays in the
    /// repository with its option loading; the order is recorded here, through the same tracker
    /// as every other level, so a descent fails in a test instead of deadlocking in production.
    /// </summary>
    public void EnterInvite() => locks.Enter(LockLevel.Invite);

    /// <summary>
    /// Records a booking lock the booking repository is about to take. The SQL stays in the
    /// repository; the order is recorded here, through the same tracker as every other level.
    /// </summary>
    public void EnterBooking() => locks.Enter(LockLevel.Booking);

    /// <summary>Takes the event row only if it is free, returning null when it is held.</summary>
    /// <param name="id">The event id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Event?> SkipLockedEventAsync(Guid id, CancellationToken cancellationToken)
    {
        locks.Enter(LockLevel.Event);

        var eventItem = (await context.Events
            .FromSqlInterpolated($"SELECT * FROM event WHERE id = {id} FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken))
            .SingleOrDefault();

        if (eventItem is not null)
        {
            await context.Entry(eventItem).Collection(item => item.Capacities)
                .LoadAsync(cancellationToken);
        }

        return eventItem;
    }

    /// <summary>Locks one event by id, with its capacity rows loaded.</summary>
    /// <param name="id">The event id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Event?> LockEventAsync(Guid id, CancellationToken cancellationToken) =>
        (await LockEventsAsync([id], cancellationToken)).SingleOrDefault();

    /// <summary>
    /// Locks events in ascending id order, whatever order the caller listed them in. A command
    /// that touches two events — a cancellation cascading onto a recovery booking, say — must not
    /// take them in the order its request happened to name.
    /// </summary>
    /// <param name="ids">The event ids, in any order and with any duplicates.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<IReadOnlyList<Event>> LockEventsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var ordered = ids.Distinct().Order().ToArray();
        if (ordered.Length == 0)
        {
            return [];
        }

        locks.Enter(LockLevel.Event);

        var events = await context.Events
            .FromSql(
                $"""
                 SELECT * FROM event
                 WHERE id = ANY({ordered})
                 ORDER BY id
                 FOR UPDATE
                 """)
            .ToListAsync(cancellationToken);

        foreach (var eventItem in events)
        {
            await context.Entry(eventItem).Collection(item => item.Capacities)
                .LoadAsync(cancellationToken);
        }

        return events;
    }

    /// <summary>Locks one event's capacity rows for the supplied appointment types.</summary>
    /// <param name="eventId">The event id.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task<IReadOnlyList<EventCapacity>> LockCapacitiesAsync(
        Guid eventId,
        IEnumerable<Guid> appointmentTypeIds,
        CancellationToken cancellationToken) =>
        LockCapacitiesAsync(
            appointmentTypeIds.Select(typeId => new EventCapacityKey(eventId, typeId)),
            cancellationToken);

    /// <summary>
    /// Locks capacity rows in (event id, appointment type id) order, using the domain's own
    /// ordering function. One statement per event, events ascending, rows within an event ordered
    /// by type: the same total order the domain names, taken one event at a time.
    /// </summary>
    /// <param name="keys">The rows to lock, in any order and with any duplicates.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<IReadOnlyList<EventCapacity>> LockCapacitiesAsync(
        IEnumerable<EventCapacityKey> keys,
        CancellationToken cancellationToken)
    {
        var ordered = Event.CapacityLockOrder(keys);
        if (ordered.Count == 0)
        {
            return [];
        }

        locks.Enter(LockLevel.EventCapacity);

        var rows = new List<EventCapacity>(ordered.Count);
        foreach (var group in ordered.GroupBy(key => key.EventId))
        {
            var eventId = group.Key;
            var typeIds = group.Select(key => key.AppointmentTypeId).ToArray();

            rows.AddRange(await context.EventCapacities
                .FromSql(
                    $"""
                     SELECT event_id, appointment_type_id, total_headcount, remaining_capacity
                     FROM event_capacity
                     WHERE event_id = {eventId}
                       AND appointment_type_id = ANY({typeIds})
                     ORDER BY appointment_type_id
                     FOR UPDATE
                     """)
                .ToListAsync(cancellationToken));
        }

        return rows;
    }
}
