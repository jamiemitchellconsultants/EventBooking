# 02c — Ordered row locks, and the harness that proves they do not deadlock, edits 1 (Task 10)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — src/EventBooking.Infrastructure/DependencyInjection.cs — 1/1

<!-- retirement-file: {"id":0,"file":"src/EventBooking.Infrastructure/DependencyInjection.cs","beforeSha":"48b575f386db9da4a8c9ec2e210a84627c12a27abf9e620a3ea7921ba20a8583","afterSha":"6d4e02d900543d233dd7bd2d7d51d28a1d0acf650c1d7ee87f4413288f8cbfcd","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using EventBooking.Infrastructure.Persistence.Repositories;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddEventBookingPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        // Registered as a factory, with a scoped context created from it. Task 51's email sender
        // needs a context of its own that is not tied to the request's unit of work, and this is
        // the pattern that gives it one without a second registration of the context type.
        services.AddDbContextFactory<EventBookingDbContext>((sp, options) => options
            .UseNpgsql(connectionString));
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<EventBookingDbContext>>().CreateDbContext());

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAppointmentTypeRepository, AppointmentTypeRepository>();
        services.AddScoped<ISystemSettingsRepository, SystemSettingsRepository>();
        services.AddScoped<IEventProposalRepository, EventProposalRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IEventCapacityRepository, EventCapacityRepository>();
        services.AddScoped<IAttendeeRepository, AttendeeRepository>();
        services.AddScoped<IAttendeeGroupRepository, AttendeeGroupRepository>();
        services.AddScoped<IInviteRepository, InviteRepository>();
        services.AddScoped<IEmailDeliveryRepository, EmailDeliveryRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IBookingAppointmentRepository, BookingAppointmentRepository>();
        services.AddScoped<IStaffAccessProfileRepository, StaffAccessProfileRepository>();
        services.AddScoped<IStaffIdentityRepository, StaffIdentityRepository>();
        services.AddScoped<IDashboardQueries, DashboardQueries>();
        services.AddScoped<IAuditQueries, AuditQueries>();
        services.AddScoped<IAppointmentWorkspaceQueries, AppointmentWorkspaceQueries>();
        services.AddScoped<IAttendeeReadinessQueries, AttendeeReadinessQueries>();
        services.AddScoped<IAttendeeBookingQueries, AttendeeBookingQueries>();

        return services;
    }

    public static IServiceCollection AddEventBookingInfrastructure(
        this IServiceCollection services,
        string connectionString,
        ClockOptions clock,
        TokenOptions tokens)
    {
        services.AddEventBookingPersistence(connectionString);

        services.AddSingleton(clock);
        services.AddSingleton(tokens);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IEventWindowZones, NodaTimeEventWindowZones>();
        services.AddSingleton<ITokenService, HmacTokenService>();

        services.AddScoped<IEmailSender, LoggingEmailSender>();

        services.AddScoped<IAuditLogger, EfAuditLogger>();

        return services;
    }
}
`````

## after — src/EventBooking.Infrastructure/DependencyInjection.cs — 1/1

<!-- retirement-file: {"id":0,"file":"src/EventBooking.Infrastructure/DependencyInjection.cs","beforeSha":"48b575f386db9da4a8c9ec2e210a84627c12a27abf9e620a3ea7921ba20a8583","afterSha":"6d4e02d900543d233dd7bd2d7d51d28a1d0acf650c1d7ee87f4413288f8cbfcd","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Locking;
using EventBooking.Infrastructure.Persistence.Queries;
using EventBooking.Infrastructure.Persistence.Repositories;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddEventBookingPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        // Registered as a factory, with a scoped context created from it. Task 51's email sender
        // needs a context of its own that is not tied to the request's unit of work, and this is
        // the pattern that gives it one without a second registration of the context type.
        services.AddDbContextFactory<EventBookingDbContext>((sp, options) => options
            .UseNpgsql(connectionString));
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<EventBookingDbContext>>().CreateDbContext());

        // Scoped: one tracker per DbContext, because the order is a property of one connection's
        // transaction, not of the process.
        services.AddScoped<TransactionLocks>();
        services.AddScoped<RowLocks>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAppointmentTypeRepository, AppointmentTypeRepository>();
        services.AddScoped<ISystemSettingsRepository, SystemSettingsRepository>();
        services.AddScoped<IEventProposalRepository, EventProposalRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IEventCapacityRepository, EventCapacityRepository>();
        services.AddScoped<IAttendeeRepository, AttendeeRepository>();
        services.AddScoped<IAttendeeGroupRepository, AttendeeGroupRepository>();
        services.AddScoped<IInviteRepository, InviteRepository>();
        services.AddScoped<IEmailDeliveryRepository, EmailDeliveryRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IBookingAppointmentRepository, BookingAppointmentRepository>();
        services.AddScoped<IStaffAccessProfileRepository, StaffAccessProfileRepository>();
        services.AddScoped<IStaffIdentityRepository, StaffIdentityRepository>();
        services.AddScoped<IDashboardQueries, DashboardQueries>();
        services.AddScoped<IAuditQueries, AuditQueries>();
        services.AddScoped<IAppointmentWorkspaceQueries, AppointmentWorkspaceQueries>();
        services.AddScoped<IAttendeeReadinessQueries, AttendeeReadinessQueries>();
        services.AddScoped<IAttendeeBookingQueries, AttendeeBookingQueries>();

        return services;
    }

    public static IServiceCollection AddEventBookingInfrastructure(
        this IServiceCollection services,
        string connectionString,
        ClockOptions clock,
        TokenOptions tokens)
    {
        services.AddEventBookingPersistence(connectionString);

        services.AddSingleton(clock);
        services.AddSingleton(tokens);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IEventWindowZones, NodaTimeEventWindowZones>();
        services.AddSingleton<ITokenService, HmacTokenService>();

        services.AddScoped<IEmailSender, LoggingEmailSender>();

        services.AddScoped<IAuditLogger, EfAuditLogger>();

        return services;
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Locking/LockLevel.cs — 1/1

<!-- retirement-file: {"id":1,"file":"src/EventBooking.Infrastructure/Persistence/Locking/LockLevel.cs","beforeSha":null,"afterSha":"8e6165146ccd4ef702f2666002f78f49d8725c5e49a5e3d1e5f48e60545172d7","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## after — src/EventBooking.Infrastructure/Persistence/Locking/LockMode.cs — 1/1

<!-- retirement-file: {"id":2,"file":"src/EventBooking.Infrastructure/Persistence/Locking/LockMode.cs","beforeSha":null,"afterSha":"ed45f92280d6ca5eddfe03ea5cbd3d5b6a4722dded359dc359e89bf5df04bf0d","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## after — src/EventBooking.Infrastructure/Persistence/Locking/LockOrderViolationException.cs — 1/1

<!-- retirement-file: {"id":3,"file":"src/EventBooking.Infrastructure/Persistence/Locking/LockOrderViolationException.cs","beforeSha":null,"afterSha":"274a45ac9eb910be69f7f74454d20bbabefb093cefd5955608da56ad600e75c1","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## after — src/EventBooking.Infrastructure/Persistence/Locking/RowLocks.cs — 1/1

<!-- retirement-file: {"id":4,"file":"src/EventBooking.Infrastructure/Persistence/Locking/RowLocks.cs","beforeSha":null,"afterSha":"a1a2e3dd3aa9a067d5dcf884a4e1783e3c9cb9fe61a9b50abc429f038045061f","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## after — src/EventBooking.Infrastructure/Persistence/Locking/TransactionLocks.cs — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Infrastructure/Persistence/Locking/TransactionLocks.cs","beforeSha":null,"afterSha":"bbd1d8a996ee3776c37d97e7ff283a933a6e87049bb8fc01407aaf50bd2a4023","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## before — src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs","beforeSha":"a88d948dd35e76f38cdb2c526816bfd74417b7e79fa6c2a6d9b7d5d2ab6c4847","afterSha":"908ecee4a4799d588f50c70450002ec24933119a626531c4a6c3d17c89281da7","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Settings;
using EventBooking.Domain.Events;
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
public sealed class EventProposalRepository(EventBookingDbContext context) : IEventProposalRepository
{
    public Task<EventProposal?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.EventProposals
            .Include(p => p.Acceptances)
            .Include(p => p.ListedTypes)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <summary>Locks the proposal row and then loads its current acceptance collection.</summary>
    public async Task<EventProposal?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var proposal = (await context.EventProposals
            .FromSqlInterpolated($"SELECT * FROM event_proposal WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken))
            .SingleOrDefault();

        if (proposal is not null)
        {
            await context.Entry(proposal).Collection(item => item.Acceptances).LoadAsync(cancellationToken);
            await context.Entry(proposal).Collection(item => item.ListedTypes).LoadAsync(cancellationToken);
        }

        return proposal;
    }

    public async Task<IReadOnlyList<EventProposal>> ListOpenAsync(CancellationToken cancellationToken) =>
        await context.EventProposals
            .Include(p => p.Acceptances)
            .Include(p => p.ListedTypes)
            .Where(p => p.Status == EventProposalStatus.Open)
            .ToListAsync(cancellationToken);

    public void Add(EventProposal proposal) => context.EventProposals.Add(proposal);
}

public sealed class EventRepository(EventBookingDbContext context) : IEventRepository
{
    public Task<Event?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Events
            .Include(s => s.Capacities)
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<Event?> LockForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var rows = await context.Events
            .FromSqlInterpolated(
                $"SELECT * FROM event WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken);

        var eventItem = rows.SingleOrDefault();
        if (eventItem is not null)
        {
            await context.Entry(eventItem).Collection(item => item.Capacities).LoadAsync(cancellationToken);
        }

        return eventItem;
    }

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

    public void Add(Event eventItem) => context.Events.Add(eventItem);
}

/// <summary>Persists attendees and exposes the lifecycle root row lock.</summary>
public sealed class AttendeeRepository(EventBookingDbContext context) : IAttendeeRepository
{
    public Task<Attendee?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Attendees
            .Include(c => c.Requirements)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    /// <summary>Locks the attendee row and then loads the requirements needed by lifecycle handlers.</summary>
    public async Task<Attendee?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var attendee = (await context.Attendees
            .FromSqlInterpolated($"SELECT * FROM attendee WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken))
            .SingleOrDefault();

        if (attendee is not null)
        {
            await context.Entry(attendee).Collection(item => item.Requirements).LoadAsync(cancellationToken);
        }

        return attendee;
    }

    public Task<Attendee?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        context.Attendees
            .Include(c => c.Requirements)
            .SingleOrDefaultAsync(c => c.Email == email, cancellationToken);

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
`````

## after — src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs","beforeSha":"a88d948dd35e76f38cdb2c526816bfd74417b7e79fa6c2a6d9b7d5d2ab6c4847","afterSha":"908ecee4a4799d588f50c70450002ec24933119a626531c4a6c3d17c89281da7","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Settings;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence.Locking;
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

public sealed class EventRepository(EventBookingDbContext context, RowLocks rowLocks) : IEventRepository
{
    /// <summary>For a test driving one context directly, with a lock tracker of its own.</summary>
    /// <param name="context">The context to read through.</param>
    public EventRepository(EventBookingDbContext context)
        : this(context, new RowLocks(context))
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

    public void Add(Event eventItem) => context.Events.Add(eventItem);
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
            .SingleOrDefaultAsync(c => c.Email == email, cancellationToken);

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
`````
