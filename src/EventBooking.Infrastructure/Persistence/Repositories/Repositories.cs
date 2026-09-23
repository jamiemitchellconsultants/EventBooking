using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Settings;
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Persistence.Locking;
using EventBooking.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Repositories;

public sealed class AppointmentTypeRepository(EventBookingDbContext context) : IAppointmentTypeRepository
{
    public async Task<IReadOnlyList<AppointmentType>> ListAsync(CancellationToken cancellationToken) =>
        await context.AppointmentTypes.OrderBy(t => t.Code).ToListAsync(cancellationToken);

    public Task<AppointmentType?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.AppointmentTypes.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
}

public sealed class SystemSettingsRepository(EventBookingDbContext context) : ISystemSettingsRepository
{
    public async Task<SystemSettings> GetAsync(CancellationToken cancellationToken) =>
        await context.SystemSettings.SingleAsync(cancellationToken);
}

/// <summary>Persists proposals and exposes their PostgreSQL lifecycle row lock.</summary>
public sealed class EventProposalRepository(EventBookingDbContext context, RowLocks rowLocks)
    : IEventProposalRepository
{
    /// <summary>For a test driving one context directly, with a lock tracker of its own.</summary>
    /// <param name="context">The context to read through.</param>
    public EventProposalRepository(EventBookingDbContext context)
        : this(context, new RowLocks(context))
    {
    }

    public Task<EventProposal?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        LoadAsync(id, LockMode.None, cancellationToken);

    /// <summary>Locks the proposal row and then loads its current acceptance collection.</summary>
    public Task<EventProposal?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        LoadAsync(id, LockMode.Update, cancellationToken);

    /// <summary>Loads one proposal under the requested lock mode.</summary>
    /// <param name="id">The proposal id.</param>
    /// <param name="mode">How to lock the row.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<EventProposal?> LoadAsync(
        Guid id,
        LockMode mode,
        CancellationToken cancellationToken) =>
        mode switch
        {
            LockMode.None => await context.EventProposals
                .Include(p => p.Acceptances)
                .Include(p => p.ListedTypes)
                .SingleOrDefaultAsync(p => p.Id == id, cancellationToken),
            LockMode.Update => await rowLocks.LockProposalAsync(id, cancellationToken),
            LockMode.SkipLocked => await rowLocks.SkipLockedProposalAsync(id, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

    public async Task<IReadOnlyList<EventProposal>> ListOpenAsync(CancellationToken cancellationToken) =>
        await context.EventProposals
            .Include(p => p.Acceptances)
            .Include(p => p.ListedTypes)
            .Where(p => p.Status == EventProposalStatus.Open)
            .ToListAsync(cancellationToken);

    public void Add(EventProposal proposal) => context.EventProposals.Add(proposal);
}

public sealed class EventRepository(
    EventBookingDbContext context,
    RowLocks rowLocks,
    IEventWindowZones zones) : IEventRepository
{
    /// <summary>For a test driving one context directly, with a lock tracker of its own.</summary>
    /// <param name="context">The context to read through.</param>
    public EventRepository(EventBookingDbContext context)
        : this(context, new RowLocks(context), new NodaTimeEventWindowZones())
    {
    }

    /// <summary>For a test that cares which zone rules the start instant is computed under.</summary>
    /// <param name="context">The context to read through.</param>
    /// <param name="zones">The zone abstraction.</param>
    public EventRepository(EventBookingDbContext context, IEventWindowZones zones)
        : this(context, new RowLocks(context), zones)
    {
    }

    public Task<Event?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        LoadAsync(id, LockMode.None, cancellationToken);

    public Task<Event?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        LoadAsync(id, LockMode.Update, cancellationToken);

    /// <summary>Loads one event under the requested lock mode.</summary>
    /// <param name="id">The event id.</param>
    /// <param name="mode">How to lock the row.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Event?> LoadAsync(
        Guid id,
        LockMode mode,
        CancellationToken cancellationToken) =>
        mode switch
        {
            LockMode.None => await context.Events
                .Include(s => s.Capacities)
                .SingleOrDefaultAsync(s => s.Id == id, cancellationToken),
            LockMode.Update => await rowLocks.LockEventAsync(id, cancellationToken),
            LockMode.SkipLocked => await rowLocks.SkipLockedEventAsync(id, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

    /// <summary>Locks several events in ascending id order, whatever order the caller named them in.</summary>
    /// <param name="ids">The event ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task<IReadOnlyList<Event>> LockForUpdateAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken) =>
        rowLocks.LockEventsAsync(ids, cancellationToken);

    public async Task<IReadOnlyList<Event>> ListActiveAsync(
        DateOnly onOrAfter,
        CancellationToken cancellationToken) =>
        await context.Events
            .Include(s => s.Capacities)
            .Where(s => s.Status == EventStatus.Active && s.Window.Date >= onOrAfter)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Event>> ListAllAsync(
        CancellationToken cancellationToken) =>
        await context.Events
            .Include(s => s.Capacities)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Event>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return [];
        }

        return await context.Events
            .Include(s => s.Capacities)
            .Where(s => ids.Contains(s.Id))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Adds the event and writes its derived start instant in the same transaction, computed from
    /// the window and the location's own zone. The column is not nullable, and a row inserted
    /// without it would be an event the eligibility query silently never sees.
    /// </summary>
    /// <param name="eventItem">The eventItem.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task AddAsync(Event eventItem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventItem);

        var timeZoneId = await context.Locations
            .Where(location => location.Id == eventItem.LocationId)
            .Select(location => location.TimeZoneId)
            .SingleOrDefaultAsync(cancellationToken);

        context.Events.Add(eventItem);
        EventStartInstants.Stamp(
            context,
            eventItem,
            EventStartInstants.Required(timeZoneId, eventItem.LocationId),
            zones);
    }
}

/// <summary>Persists attendees and exposes the lifecycle root row lock.</summary>
public sealed class AttendeeRepository(EventBookingDbContext context, RowLocks rowLocks)
    : IAttendeeRepository
{
    /// <summary>For a test driving one context directly, with a lock tracker of its own.</summary>
    /// <param name="context">The context to read through.</param>
    public AttendeeRepository(EventBookingDbContext context)
        : this(context, new RowLocks(context))
    {
    }

    public Task<Attendee?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        LoadAsync(id, LockMode.None, cancellationToken);

    /// <summary>Locks the attendee row and then loads the requirements needed by lifecycle handlers.</summary>
    public Task<Attendee?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        LoadAsync(id, LockMode.Update, cancellationToken);

    /// <summary>Loads one attendee under the requested lock mode.</summary>
    /// <param name="id">The attendee id.</param>
    /// <param name="mode">How to lock the row.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Attendee?> LoadAsync(
        Guid id,
        LockMode mode,
        CancellationToken cancellationToken) =>
        mode switch
        {
            LockMode.None => await context.Attendees
                .Include(c => c.Requirements)
                .SingleOrDefaultAsync(c => c.Id == id, cancellationToken),
            LockMode.Update => await rowLocks.LockAttendeeAsync(id, cancellationToken),
            LockMode.SkipLocked => await rowLocks.SkipLockedAttendeeAsync(id, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

    public Task<Attendee?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        context.Attendees
            .Include(c => c.Requirements)
            // Case-insensitive to match the unique index on lower(email): one mailbox, whatever
            // case the caller was given. The lower() call is what lets PostgreSQL use that index.
            .SingleOrDefaultAsync(
                c => c.Email.ToLower() == email.ToLower(), cancellationToken);

    public async Task<IReadOnlyList<Attendee>> ListAsync(
        AttendeeStatus? status,
        CancellationToken cancellationToken) =>
        await context.Attendees
            .Include(c => c.Requirements)
            .Where(c => status == null || c.Status == status)
            .ToListAsync(cancellationToken);

    public void Add(Attendee attendee) => context.Attendees.Add(attendee);

    public void Remove(Attendee attendee) => context.Attendees.Remove(attendee);
}

/// <summary>Persists invite rows and their option collections, including lifecycle locks.</summary>
public sealed class InviteRepository(EventBookingDbContext context) : IInviteRepository
{
    public Task<Invite?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Invites
            .Include(i => i.Options)
            .Include(i => i.Requirements)
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

    /// <summary>Locks the identified invite and loads its offered event IDs.</summary>
    public async Task<Invite?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        await LockAndLoadOptionsAsync(
            context.Invites.FromSqlInterpolated($"SELECT * FROM invite WHERE id = {id} FOR UPDATE"),
            cancellationToken);

    /// <summary>Locks the attendee's current pending invite and loads its offered event IDs.</summary>
    public async Task<Invite?> LockPendingForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken) =>
        await LockAndLoadOptionsAsync(
            context.Invites.FromSqlInterpolated(
                $"SELECT * FROM invite WHERE attendee_id = {attendeeId} AND status = {(int)InviteStatus.Pending} FOR UPDATE"),
            cancellationToken);

    public Task<Invite?> GetPendingForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken) =>
        context.Invites
            .Include(i => i.Options)
            .Include(i => i.Requirements)
            .SingleOrDefaultAsync(
                i => i.AttendeeId == attendeeId && i.Status == InviteStatus.Pending,
                cancellationToken);

    /// <summary>Locks the attendee's pending initial invite and loads its offered event IDs.</summary>
    public async Task<Invite?> LockPendingInitialForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken) =>
        await LockAndLoadOptionsAsync(
            context.Invites.FromSqlInterpolated(
                $"SELECT * FROM invite WHERE attendee_id = {attendeeId} AND status = {(int)InviteStatus.Pending} AND recovery_of_booking_id IS NULL FOR UPDATE"),
            cancellationToken);

    /// <summary>Locks every pending invite for the attendee in ID order with events loaded.</summary>
    public async Task<IReadOnlyList<Invite>> LockPendingListForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var pending = await context.Invites.FromSqlInterpolated(
                $"SELECT * FROM invite WHERE attendee_id = {attendeeId} AND status = {(int)InviteStatus.Pending} ORDER BY id FOR UPDATE")
            .ToListAsync(cancellationToken);

        foreach (var invite in pending)
        {
            await context.Entry(invite).Collection(item => item.Options).LoadAsync(cancellationToken);
            await context.Entry(invite).Collection(item => item.Requirements).LoadAsync(cancellationToken);
        }

        return pending;
    }

    public async Task<IReadOnlyList<Invite>> ListPendingExpiredAsync(
        DateTimeOffset asAt,
        CancellationToken cancellationToken) =>
        await context.Invites
            .Include(i => i.Options)
            .Include(i => i.Requirements)
            .Where(i => i.Status == InviteStatus.Pending && i.ExpiresAt <= asAt)
            .ToListAsync(cancellationToken);

    public void Add(Invite invite) => context.Invites.Add(invite);

    private async Task<Invite?> LockAndLoadOptionsAsync(
        IQueryable<Invite> query,
        CancellationToken cancellationToken)
    {
        var invite = (await query.ToListAsync(cancellationToken)).SingleOrDefault();
        if (invite is not null)
        {
            await context.Entry(invite).Collection(item => item.Options).LoadAsync(cancellationToken);
            await context.Entry(invite).Collection(item => item.Requirements).LoadAsync(cancellationToken);
        }

        return invite;
    }
}

/// <summary>Persists hash-only attendee email delivery attempts and their safe retry context.</summary>
public sealed class EmailDeliveryRepository(EventBookingDbContext context) : IEmailDeliveryRepository
{
    /// <summary>Loads one delivery without taking a row lock.</summary>
    public Task<EmailLog?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.EmailLogs.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);

    /// <summary>Locks one delivery row for the claim or outcome transition.</summary>
    public async Task<EmailLog?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var rows = await context.EmailLogs
            .FromSqlInterpolated($"SELECT * FROM email_log WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return rows.SingleOrDefault();
    }

    /// <summary>Locks the newest unresolved delivery, or the newest terminal row when none remain.</summary>
    public async Task<EmailLog?> LockLatestForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var rows = await context.EmailLogs
            .FromSqlInterpolated($"""
                SELECT * FROM email_log
                WHERE attendee_id = {attendeeId}
                ORDER BY CASE
                    WHEN status IN ({(int)EmailStatus.Failed}, {(int)EmailStatus.Pending}) THEN 0
                    ELSE 1
                END, sent_at DESC, id DESC
                LIMIT 1
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken);

        return rows.SingleOrDefault();
    }

    /// <summary>Reads the latest row for one attendee and template for cancellation recovery.</summary>
    public Task<EmailLog?> GetLatestForAttendeeAsync(
        Guid attendeeId,
        EmailTemplate template,
        CancellationToken cancellationToken) =>
        context.EmailLogs
            .AsNoTracking()
            .Where(e => e.AttendeeId == attendeeId && e.TemplateName == template)
            .OrderBy(e => e.Status == EmailStatus.Failed || e.Status == EmailStatus.Pending ? 0 : 1)
            .ThenByDescending(e => e.SentAt)
            .ThenByDescending(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Stages a delivery row on the current context transaction.</summary>
    public void Add(EmailLog delivery) => context.EmailLogs.Add(delivery);
}

/// <summary>Persists booking rows and exposes token and attendee lifecycle locks.</summary>
public sealed class BookingRepository(EventBookingDbContext context) : IBookingRepository
{
    public Task<Booking?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Bookings.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

    /// <summary>Locks one booking row before rotating its management-token hash.</summary>
    public async Task<Booking?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var rows = await context.Bookings
            .FromSqlInterpolated($"SELECT * FROM booking WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return rows.SingleOrDefault();
    }

    public Task<Guid?> GetEventIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Bookings
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => (Guid?)b.EventId)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Reads only the attendee ID used to establish cancellation lock order.</summary>
    public Task<Guid?> GetAttendeeIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Bookings
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => (Guid?)b.AttendeeId)
            .SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<Booking?> LockByIdForAttendeeAsync(
        Guid bookingId,
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var rows = await context.Bookings
            .FromSqlInterpolated(
                $"SELECT * FROM booking WHERE id = {bookingId} AND attendee_id = {attendeeId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return rows.SingleOrDefault();
    }

    /// <summary>Locks the attendee's active booking, if one remains after the prior locks.</summary>
    public async Task<Booking?> LockActiveForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken) =>
        await LockActiveOriginalForAttendeeAsync(attendeeId, cancellationToken);

    /// <summary>Locks the attendee's active original booking for lifecycle serialization.</summary>
    public async Task<Booking?> LockActiveOriginalForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var rows = await context.Bookings
            .FromSqlInterpolated(
                $"SELECT * FROM booking WHERE attendee_id = {attendeeId} AND status = {(int)BookingStatus.Active} AND recovery_of_booking_id IS NULL FOR UPDATE")
            .ToListAsync(cancellationToken);

        return rows.SingleOrDefault();
    }

    /// <summary>Lists the original and all direct recovery bookings in creation and ID order.</summary>
    public async Task<IReadOnlyList<Booking>> ListJourneyAsync(
        Guid originalBookingId,
        CancellationToken cancellationToken) =>
        await context.Bookings
            .AsNoTracking()
            .Where(b => b.Id == originalBookingId || b.RecoveryOfBookingId == originalBookingId)
            .OrderBy(b => b.CreatedAt)
            .ThenBy(b => b.Id)
            .ToListAsync(cancellationToken);

    /// <summary>Locks the root's active recovery booking for lifecycle serialization.</summary>
    public async Task<Booking?> LockActiveRecoveryAsync(
        Guid originalBookingId,
        CancellationToken cancellationToken)
    {
        var rows = await context.Bookings
            .FromSqlInterpolated(
                $"SELECT * FROM booking WHERE recovery_of_booking_id = {originalBookingId} AND status = {(int)BookingStatus.Active} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return rows.SingleOrDefault();
    }

    public Task<Booking?> GetActiveForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken) =>
        context.Bookings.SingleOrDefaultAsync(
            b => b.AttendeeId == attendeeId && b.Status == BookingStatus.Active,
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ListActiveAttendeeIdsForEventAsync(
        Guid eventId,
        CancellationToken cancellationToken) =>
        await context.Bookings
            .AsNoTracking()
            .Where(b => b.EventId == eventId && b.Status == BookingStatus.Active)
            .Select(b => b.AttendeeId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Booking>> ListActiveForEventAsync(
        Guid eventId,
        CancellationToken cancellationToken) =>
        await context.Bookings
            .Where(b =>
                b.EventId == eventId && b.Status == BookingStatus.Active)
            .ToListAsync(cancellationToken);

    public void Add(Booking booking) => context.Bookings.Add(booking);
}
