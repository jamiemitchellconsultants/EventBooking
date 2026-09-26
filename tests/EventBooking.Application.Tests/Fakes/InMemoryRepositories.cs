using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.EventGroups;
using EventBooking.Domain.SelfRegistrations;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Locations;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Settings;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Fakes;

/// <summary>Provides proposal state for application tests without database locking.</summary>
public sealed class InMemoryEventProposalRepository : IEventProposalRepository
{
    public List<EventProposal> Items { get; } = [];

    public Task<EventProposal?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(p => p.Id == id));

    /// <summary>Returns the in-memory proposal because this test double has no database row lock.</summary>
    public Task<EventProposal?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(p => p.Id == id));

    public Task<IReadOnlyList<EventProposal>> ListOpenAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EventProposal>>(
            Items.Where(p => p.Status == EventProposalStatus.Open).ToList());

    public void Add(EventProposal proposal) => Items.Add(proposal);
}

/// <summary>
/// The in-memory event store. It answers the eligibility port as well as the repository, because
/// both read the same rows and a fake that split them would let the two disagree about what is in
/// the store. The eligibility rule itself is proved against real SQL in Infrastructure.Tests.
/// </summary>
public sealed class InMemoryEventRepository(
    TransactionOperationLog? operations = null,
    TransactionalEventLockCoordinator? locks = null)
    : IEventRepository, IEventEligibilityQuery
{
    public List<Event> Items { get; } = [];

    /// <summary>Runs once, just before the next event lock is granted, to stage a racing writer.</summary>
    public Func<Task>? BeforeNextLock { get; set; }

    public Task<Event?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        operations?.Record("event-reloaded");
        return Task.FromResult(Items.SingleOrDefault(s => s.Id == id));
    }

    public async Task<Event?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        operations?.Record("event-guard-locked");
        if (BeforeNextLock is { } racingWriter)
        {
            BeforeNextLock = null;
            await racingWriter();
        }

        if (locks is not null)
        {
            await locks.AcquireAsync(id, cancellationToken);
        }

        return Items.SingleOrDefault(s => s.Id == id);
    }

    public Task<IReadOnlyList<Event>> ListActiveAsync(
        DateOnly onOrAfter, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Event>>(
            Items
                .Where(s => s.Status == EventStatus.Active && s.Window.Date >= onOrAfter)
                .ToList());

    public Task<IReadOnlyList<Event>> ListAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Event>>(Items.ToList());

    public Task<IReadOnlyList<Event>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Event>>(
            Items.Where(s => ids.Contains(s.Id)).ToList());

    public Task AddAsync(Event eventItem, CancellationToken cancellationToken)
    {
        Add(eventItem);
        return Task.CompletedTask;
    }

    /// <summary>Seeds the store directly, without the port's asynchronous shape.</summary>
    /// <param name="eventItem">The event to hold.</param>
    public void Add(Event eventItem) => Items.Add(eventItem);

    public Task<IReadOnlyList<Guid>> FindEligibleEventsAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        IReadOnlyCollection<Guid> locationIds,
        IReadOnlyCollection<Guid> excludeEventIds,
        int count,
        DateTimeOffset asOf,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(
            [.. Eligible(requiredAppointmentTypeIds, locationIds, excludeEventIds, asOf)
                .Take(count)]);

    public Task<int> CountEligibleEventsAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        IReadOnlyCollection<Guid> locationIds,
        IReadOnlyCollection<Guid> excludeEventIds,
        DateTimeOffset asOf,
        CancellationToken cancellationToken) =>
        Task.FromResult(
            Eligible(requiredAppointmentTypeIds, locationIds, excludeEventIds, asOf).Count());

    private IEnumerable<Guid> Eligible(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        IReadOnlyCollection<Guid> locationIds,
        IReadOnlyCollection<Guid> excludeEventIds,
        DateTimeOffset asOf) =>
        requiredAppointmentTypeIds.Count == 0 || locationIds.Count == 0
            ? []
            : Items
                .Where(s => s.Status == EventStatus.Active)
                .Where(s => locationIds.Contains(s.LocationId))
                .Where(s => !excludeEventIds.Contains(s.Id))
                .Where(s => StartInstant(s) > asOf)
                // Relational division: a type the event does not list is a type it cannot cover.
                .Where(s => requiredAppointmentTypeIds.Distinct().All(typeId =>
                    s.Capacities.Any(c => c.AppointmentTypeId == typeId && c.RemainingCapacity >= 1)))
                .OrderBy(StartInstant)
                .ThenBy(s => s.Id)
                .Select(s => s.Id);

    private static DateTimeOffset StartInstant(Event eventItem) =>
        eventItem.Window.StartInstant(ProposalFixture.Zones, ProposalFixture.TimeZoneId);
}

/// <summary>
/// There is nothing to lock in memory, so this simply hands back the same capacity objects the
/// event repository holds. The locking itself is proved against a real database in Task 67.
/// </summary>
public sealed class InMemoryEventCapacityRepository(
    InMemoryEventRepository events,
    TransactionOperationLog? operations = null)
    : IEventCapacityRepository
{
    public int LockCallCount { get; private set; }

    public Task<IReadOnlyList<EventCapacity>> LockForUpdateAsync(
        Guid eventId,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        CancellationToken cancellationToken)
    {
        operations?.Record("capacity-locked");
        LockCallCount++;

        var eventItem = events.Items.SingleOrDefault(s => s.Id == eventId);
        if (eventItem is null)
        {
            return Task.FromResult<IReadOnlyList<EventCapacity>>([]);
        }

        var locked = appointmentTypeIds
            .OrderBy(id => id)
            .Select(eventItem.CapacityFor)
            .ToList();

        return Task.FromResult<IReadOnlyList<EventCapacity>>(locked);
    }
}

/// <summary>Provides attendee state and observable lifecycle-lock order for application tests.</summary>
public sealed class InMemoryAttendeeRepository(TransactionOperationLog? operations = null) : IAttendeeRepository
{
    public List<Attendee> Items { get; } = [];

    public Task<Attendee?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(c => c.Id == id));

    /// <summary>Returns the in-memory attendee because this test double has no database row lock.</summary>
    public Task<Attendee?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        operations?.Record("attendee-locked");
        return Task.FromResult(Items.SingleOrDefault(c => c.Id == id));
    }

    public Task<Attendee?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(
            Items.SingleOrDefault(c => string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<Attendee>> ListAsync(
        AttendeeStatus? status, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Attendee>>(
            Items.Where(c => status is null || c.Status == status).ToList());

    public void Add(Attendee attendee) => Items.Add(attendee);

    public void Remove(Attendee attendee) => Items.Remove(attendee);

    public Task<IReadOnlyList<Attendee>> LockByGroupForUpdateAsync(
        Guid groupId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Attendee>>(
            Items.Where(c => c.AttendeeGroupId == groupId).OrderBy(c => c.Id).ToList());
}

/// <summary>Provides a controllable attendee read barrier for concurrency interleaving tests.</summary>
public sealed class BlockingAttendeeRepository : IAttendeeRepository
{
    private Guid? _blockedAttendeeId;
    private TaskCompletionSource<bool>? _blocked;
    private TaskCompletionSource<bool>? _release;

    public List<Attendee> Items { get; } = [];

    public void BlockNextGetFor(Guid attendeeId)
    {
        _blockedAttendeeId = attendeeId;
        _blocked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public Task WaitUntilBlockedAsync() =>
        _blocked?.Task ?? throw new InvalidOperationException("No attendee get is configured to block.");

    public void ReleaseBlockedGet() =>
        (_release ?? throw new InvalidOperationException("No attendee get is configured to block."))
        .TrySetResult(true);

    public async Task<Attendee?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (_blockedAttendeeId == id)
        {
            _blocked!.TrySetResult(true);
            await _release!.Task.WaitAsync(cancellationToken);
            _blockedAttendeeId = null;
        }

        return Items.SingleOrDefault(c => c.Id == id);
    }

    /// <summary>Uses the same controlled read as the lock operation in this in-memory test double.</summary>
    public Task<Attendee?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync(id, cancellationToken);

    public Task<Attendee?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(
            Items.SingleOrDefault(c => string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<Attendee>> ListAsync(
        AttendeeStatus? status,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Attendee>>(
            Items.Where(c => status is null || c.Status == status).ToList());

    public void Add(Attendee attendee) => Items.Add(attendee);

    public void Remove(Attendee attendee) => Items.Remove(attendee);

    public Task<IReadOnlyList<Attendee>> LockByGroupForUpdateAsync(
        Guid groupId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Attendee>>(
            Items.Where(c => c.AttendeeGroupId == groupId).OrderBy(c => c.Id).ToList());
}

/// <summary>Provides invite state and observable invite-lock order for application tests.</summary>
public sealed class InMemoryInviteRepository(TransactionOperationLog? operations = null) : IInviteRepository
{
    public List<Invite> Items { get; } = [];

    public Task<Invite?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(i => i.Id == id));

    /// <summary>Returns the in-memory invite because this test double has no database row lock.</summary>
    public Task<Invite?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        operations?.Record("invite-locked");
        return Task.FromResult(Items.SingleOrDefault(i => i.Id == id));
    }

    /// <summary>Returns the current pending in-memory invite because this double has no row lock.</summary>
    public Task<Invite?> LockPendingForAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(i => i.AttendeeId == attendeeId
            && i.Status == InviteStatus.Pending));

    public Task<Invite?> GetPendingForAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken) =>
        Task.FromResult(
            Items.SingleOrDefault(i => i.AttendeeId == attendeeId && i.Status == InviteStatus.Pending));

    /// <summary>Returns the current pending in-memory invite because this double has no row lock.</summary>
    public Task<Invite?> LockPendingInitialForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        operations?.Record("initial-invite-locked");
        return Task.FromResult(Items.SingleOrDefault(i => i.AttendeeId == attendeeId
            && i.Status == InviteStatus.Pending));
    }

    public Task<IReadOnlyList<Invite>> ListPendingExpiredAsync(
        DateTimeOffset asAt, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Invite>>(
            Items.Where(i => i.Status == InviteStatus.Pending && i.ExpiresAt <= asAt).ToList());

    /// <summary>Returns every pending in-memory invite ordered by ID for lock-order tests.</summary>
    public Task<IReadOnlyList<Invite>> LockPendingListForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        operations?.Record("pending-invites-locked");
        return Task.FromResult<IReadOnlyList<Invite>>(
            Items
                .Where(i => i.AttendeeId == attendeeId && i.Status == InviteStatus.Pending)
                .OrderBy(i => i.Id)
                .ToList());
    }

    public void Add(Invite invite) => Items.Add(invite);
}

/// <summary>Provides booking state and observable booking-lock order for application tests.</summary>
public sealed class InMemoryBookingRepository(TransactionOperationLog? operations = null) : IBookingRepository
{
    public List<Booking> Items { get; } = [];

    public Task<Booking?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(b => b.Id == id));

    /// <summary>Returns the in-memory booking because this test double has no row lock.</summary>
    public Task<Booking?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        operations?.Record("booking-locked");
        return Task.FromResult(Items.SingleOrDefault(b => b.Id == id));
    }

    public Task<Guid?> GetEventIdAsync(Guid id, CancellationToken cancellationToken)
    {
        operations?.Record("booking-event-located");
        return Task.FromResult(Items.SingleOrDefault(b => b.Id == id)?.EventId);
    }

    /// <summary>Returns the attendee identifier of the booking the manage link names.</summary>
    public Task<Guid?> GetAttendeeIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(b => b.Id == id)?.AttendeeId);

    /// <inheritdoc/>
    public Task<Booking?> LockByIdForAttendeeAsync(
        Guid bookingId,
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        operations?.Record("booking-locked");
        return Task.FromResult(
            Items.SingleOrDefault(b => b.Id == bookingId && b.AttendeeId == attendeeId));
    }

    /// <summary>Returns the attendee's active in-memory booking because this double has no row lock.</summary>
    public Task<Booking?> LockActiveForAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken)
    {
        operations?.Record("active-booking-locked");
        return Task.FromResult(Items.SingleOrDefault(b => b.AttendeeId == attendeeId
            && b.Status == BookingStatus.Active));
    }

    public Task<Booking?> GetActiveForAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken) =>
        Task.FromResult(
            Items.SingleOrDefault(b => b.AttendeeId == attendeeId && b.Status == BookingStatus.Active));

    /// <summary>Returns the attendee's active in-memory booking because this double has no row lock.</summary>
    public Task<Booking?> LockActiveOriginalForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        operations?.Record("original-booking-locked");
        return Task.FromResult(Items.SingleOrDefault(b => b.AttendeeId == attendeeId
            && b.Status == BookingStatus.Active
            && b.RecoveryOfBookingId is null));
    }

    /// <summary>Returns attendee identifiers for active in-memory bookings on one eventItem.</summary>
    public Task<IReadOnlyList<Guid>> ListActiveAttendeeIdsForEventAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        operations?.Record("active-attendee-ids-snapshotted");
        return Task.FromResult<IReadOnlyList<Guid>>(
            Items
                .Where(b => b.EventId == eventId && b.Status == BookingStatus.Active)
                .Select(b => b.AttendeeId)
                .ToList());
    }

    public Task<IReadOnlyList<Booking>> ListActiveForEventAsync(
        Guid eventId, CancellationToken cancellationToken)
    {
        operations?.Record("active-bookings-listed");
        return Task.FromResult<IReadOnlyList<Booking>>(
            Items
                .Where(b => b.EventId == eventId && b.Status == BookingStatus.Active)
                .ToList());
    }

    public Task<IReadOnlyList<Booking>> ListActiveRecoveriesAsync(
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Booking>>(
            Items
                .Where(b => b.Status == BookingStatus.Active && b.RecoveryOfBookingId != null)
                .OrderBy(b => b.Id)
                .ToList());

    /// <summary>Returns the original and direct recovery rows in creation and ID order.</summary>
    public Task<IReadOnlyList<Booking>> ListJourneyAsync(
        Guid originalBookingId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Booking>>(
            Items
                .Where(b => b.Id == originalBookingId || b.RecoveryOfBookingId == originalBookingId)
                .OrderBy(b => b.CreatedAt)
                .ThenBy(b => b.Id)
                .ToList());

    /// <summary>Returns the root's active recovery because this double has no row lock.</summary>
    public Task<Booking?> LockActiveRecoveryAsync(
        Guid originalBookingId,
        CancellationToken cancellationToken)
    {
        operations?.Record("active-recovery-locked");
        return Task.FromResult(Items.SingleOrDefault(b =>
            b.RecoveryOfBookingId == originalBookingId && b.Status == BookingStatus.Active));
    }

    public void Add(Booking booking) => Items.Add(booking);

    public Task<Booking?> GetByInviteIdAsync(Guid inviteId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(b => b.InviteId == inviteId));

    public Task<int> CountActiveForAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Count(b => b.AttendeeId == attendeeId && b.Status == BookingStatus.Active));
}

/// <summary>Provides Attendee Group reference data with identifier and code lookups.</summary>
public sealed class InMemoryAttendeeGroupRepository : IAttendeeGroupRepository
{
    /// <summary>Gets the mutable reference-data collection.</summary>
    public List<AttendeeGroup> Items { get; } = [];

    /// <summary>Gets a group by stable identifier.</summary>
    public Task<AttendeeGroup?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(group => group.Id == id));

    /// <summary>Gets a group from a trimmed case-insensitive canonical-code input.</summary>
    public Task<AttendeeGroup?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return Task.FromResult(
            Items.SingleOrDefault(group => string.Equals(group.Code, normalized, StringComparison.Ordinal)));
    }

    /// <summary>Lists active mapped groups ordered by display name.</summary>
    public Task<IReadOnlyList<AttendeeGroup>> ListActiveAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AttendeeGroup>>(
            Items
                .Where(group => group.IsActive && group.Requirements.Count > 0)
                .OrderBy(group => group.Name)
                .ToList());

    public Task<IReadOnlyList<AttendeeGroup>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AttendeeGroup>>(
            Items.OrderBy(group => group.Name).ToList());

    public void Add(AttendeeGroup group) => Items.Add(group);
}

public sealed class InMemoryEventGroupRepository : IEventGroupRepository
{
    /// <summary>Gets the mutable event group collection.</summary>
    public List<EventGroup> Items { get; } = [];

    /// <summary>Gets a group with both membership collections.</summary>
    public Task<EventGroup?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(group => group.Id == id));

    /// <summary>Gets a group with both membership collections, untracked.</summary>
    public Task<EventGroup?> GetUntrackedAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(group => group.Id == id));

    /// <summary>Locks the parent row before any gate or mapping mutation.</summary>
    public Task<EventGroup?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(group => group.Id == id));

    /// <summary>Lists every group ordered by title.</summary>
    public Task<IReadOnlyList<EventGroup>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EventGroup>>(Items.OrderBy(group => group.Title).ToList());

    /// <summary>Lists every group selecting the attendee group, in ascending id order.</summary>
    public Task<IReadOnlyList<EventGroup>> ListContainingAttendeeGroupAsync(
        Guid attendeeGroupId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EventGroup>>(Items
            .Where(group => group.AttendeeGroups.Any(selected => selected.AttendeeGroupId == attendeeGroupId))
            .OrderBy(group => group.Id)
            .ToList());

    /// <summary>Seeds the store directly.</summary>
    /// <param name="group">The event group to hold.</param>
    public void Add(EventGroup group) => Items.Add(group);

    /// <summary>Gets the mutable pending registration collection.</summary>
    public List<PendingRegistration> Registrations { get; } = [];

    /// <summary>Stages a new pending registration for the next save.</summary>
    public void AddRegistration(PendingRegistration registration) => Registrations.Add(registration);

    /// <summary>Finds the pending registration for one event and email address.</summary>
    public Task<PendingRegistration?> FindInFlightAsync(
        Guid eventId, string email, CancellationToken cancellationToken) =>
        Task.FromResult(Registrations.SingleOrDefault(x =>
            x.EventId == eventId && x.Email == email && x.Status == SelfRegistrationStatus.Pending));

    /// <summary>The in-memory store has no concurrent writers to serialise.</summary>
    public Task LockEmailAsync(string email, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>When a test wants the newest link email to look due; null means none recent.</summary>
    public DateTimeOffset? LinkDueAt { get; set; }

    /// <summary>Returns the configured due instant.</summary>
    public Task<DateTimeOffset?> LatestLinkDueAsync(
        string email, DateTimeOffset since, CancellationToken cancellationToken) =>
        Task.FromResult(LinkDueAt);

    /// <summary>Gets one pending registration by its request identifier.</summary>
    public Task<PendingRegistration?> GetRegistrationAsync(
        Guid requestId, CancellationToken cancellationToken) =>
        Task.FromResult(Registrations.SingleOrDefault(x => x.RequestId == requestId));

    /// <summary>Re-reads one pending registration without tracking, for post-lock validation.</summary>
    public Task<PendingRegistration?> GetRegistrationUntrackedAsync(
        Guid requestId, CancellationToken cancellationToken) =>
        Task.FromResult(Registrations.SingleOrDefault(x => x.RequestId == requestId));

    /// <summary>Lists the pending registrations at or past their expiry.</summary>
    public Task<IReadOnlyList<PendingRegistration>> ListExpiredPendingAsync(
        DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PendingRegistration>>([.. Registrations
            .Where(x => x.Status == SelfRegistrationStatus.Pending && x.ExpiresAt <= now)
            .OrderBy(x => x.ExpiresAt)]);

    /// <summary>
    /// Deletes one bounded batch of terminal requests at or past retention. The in-memory
    /// store holds deliveries in a separate repository it cannot reach, so confirmation
    /// email rows are left for the PostgreSQL implementation to remove.
    /// </summary>
    public Task<int> DeleteTerminalBeforeAsync(
        DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var due = Registrations
            .Where(x => (x.Status == SelfRegistrationStatus.Confirmed
                    || x.Status == SelfRegistrationStatus.Expired)
                && x.TerminalAt is { } terminal && terminal <= cutoff)
            .OrderBy(x => x.TerminalAt)
            .Take(500)
            .ToList();
        foreach (var request in due) Registrations.Remove(request);
        return Task.FromResult(due.Count);
    }
}

public sealed class InMemoryAppointmentTypeRepository : IAppointmentTypeRepository
{
    public List<AppointmentType> Items { get; } = AppointmentType.CreateFixedSet().ToList();

    /// <summary>Gets how many reads (list or single) reached the store, for round-trip tests.</summary>
    public int Reads { get; private set; }

    public Task<IReadOnlyList<AppointmentType>> ListAsync(CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult<IReadOnlyList<AppointmentType>>(Items.ToList());
    }

    public Task<AppointmentType?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult(Items.SingleOrDefault(t => t.Id == id));
    }

    public Task<AppointmentType?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return Task.FromResult(
            Items.SingleOrDefault(t => string.Equals(t.Code, normalized, StringComparison.Ordinal)));
    }

    public void Add(AppointmentType type) => Items.Add(type);
}

public sealed class InMemoryLocationRepository : ILocationRepository
{
    public List<Location> Items { get; } = [];

    public Task<Location?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(l => l.Id == id));

    public Task<Location?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return Task.FromResult(
            Items.SingleOrDefault(l => string.Equals(l.Code, normalized, StringComparison.Ordinal)));
    }

    public Task<IReadOnlyList<Location>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Location>>([.. Items.OrderBy(l => l.Name)]);

    public void Add(Location location) => Items.Add(location);
}

public sealed class InMemorySystemSettingsRepository : ISystemSettingsRepository
{
    public SystemSettings Settings { get; } = SystemSettings.CreateDefault();

    public Task<SystemSettings> GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Settings);

    public Task<SystemSettings> LockAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Settings);
}

public sealed class InMemoryStaffAccessProfileRepository :
    IStaffAccessProfileRepository,
    IStaffAccessAuthorizer
{
    // Keep the Task 3 repository members unchanged.
    public List<StaffAccessProfile> Items { get; } = [];

    public Task<StaffAccessProfile?> GetAsync(
        Guid staffUserId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(profile => profile.StaffUserId == staffUserId));

    public Task<IReadOnlyList<StaffAccessProfile>> ListAsync(
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StaffAccessProfile>>(
            Items.OrderBy(profile => profile.StaffUserId).ToList());

    public Task<IReadOnlyList<StaffAccessProfile>> LockAllAsync(
        CancellationToken cancellationToken) =>
        ListAsync(cancellationToken);

    public void Add(StaffAccessProfile profile) => Items.Add(profile);

    public void Remove(StaffAccessProfile profile) => Items.Remove(profile);

    public Task<Result<StaffAccessContext>> AuthorizeAsync(
        Guid staffUserId,
        StaffCapability capability,
        Guid? requiredAppointmentTypeId,
        CancellationToken cancellationToken) =>
        new StaffAccessAuthorizer(this).AuthorizeAsync(
            staffUserId, capability, requiredAppointmentTypeId, cancellationToken);
}

/// <summary>Stores observed staff identities in memory for application tests.</summary>
public sealed class InMemoryStaffIdentityRepository : IStaffIdentityRepository
{
    /// <summary>Gets the mutable identity collection.</summary>
    public List<StaffIdentity> Items { get; } = [];

    /// <inheritdoc />
    public Task<StaffIdentity?> GetByStaffIdAsync(
        StaffId staffId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(identity => identity.StaffId == staffId));

    /// <inheritdoc />
    public Task<IReadOnlyList<StaffIdentity>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StaffIdentity>>(Items.ToList());

    /// <inheritdoc />
    public Task UpsertAsync(
        Guid staffUserId,
        StaffId staffId,
        string? displayName,
        DateTimeOffset lastSeenAt,
        CancellationToken cancellationToken)
    {
        var identity = Items.SingleOrDefault(value => value.StaffUserId == staffUserId);
        if (identity is null)
        {
            Items.Add(StaffIdentity.Create(staffUserId, staffId, displayName, lastSeenAt));
        }
        else
        {
            identity.MarkSeen(displayName, lastSeenAt);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Stores booking appointments in memory for application tests.</summary>
public sealed class InMemoryBookingAppointmentRepository(
    InMemoryBookingRepository bookings,
    TransactionOperationLog? operations = null) : IBookingAppointmentRepository
{
    /// <summary>Gets the mutable test collection.</summary>
    public List<BookingAppointment> Items { get; } = [];

    /// <summary>Adds one appointment to the in-memory collection.</summary>
    /// <param name="appointment">The appointment to track.</param>
    public void Add(BookingAppointment appointment) => Items.Add(appointment);

    /// <summary>Finds lifecycle owner identifiers only when record and trusted type both match.</summary>
    /// <param name="id">The stable appointment-record identifier.</param>
    /// <param name="appointmentTypeId">The trusted appointment-type scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The lifecycle owner identifiers, or null when out of scope.</returns>
    public Task<BookingAppointmentLocator?> FindLocatorInScopeAsync(
        Guid id,
        Guid appointmentTypeId,
        CancellationToken cancellationToken)
    {
        operations?.Record("appointment-located");
        var item = Items.SingleOrDefault(value =>
            value.Id == id && value.AppointmentTypeId == appointmentTypeId);
        if (item is null)
        {
            return Task.FromResult<BookingAppointmentLocator?>(null);
        }

        var booking = bookings.Items.Single(value => value.Id == item.BookingId);
        return Task.FromResult<BookingAppointmentLocator?>(new BookingAppointmentLocator(
            booking.AttendeeId,
            booking.RecoveryOfBookingId ?? booking.Id,
            booking.Id,
            booking.EventId,
            item.AppointmentTypeId));
    }

    /// <summary>Locks and returns one appointment only when record and trusted type both match.</summary>
    /// <param name="id">The stable appointment-record identifier.</param>
    /// <param name="appointmentTypeId">The trusted appointment-type scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The locked appointment, or null when out of scope.</returns>
    public Task<BookingAppointment?> LockForUpdateAsync(
        Guid id,
        Guid appointmentTypeId,
        CancellationToken cancellationToken)
    {
        operations?.Record("appointment-locked");
        return Task.FromResult(Items.SingleOrDefault(value =>
            value.Id == id && value.AppointmentTypeId == appointmentTypeId));
    }

    /// <summary>Lists the immutable Appointment Type snapshot owned by one Booking.</summary>
    public Task<IReadOnlyList<BookingAppointment>> ListForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BookingAppointment>>(Items
            .Where(value => value.BookingId == bookingId)
            .OrderBy(value => value.AppointmentTypeId)
            .ToList());

    /// <summary>Lists the snapshots owned by a whole journey in stable ID order.</summary>
    public Task<IReadOnlyList<BookingAppointment>> ListForBookingsAsync(
        IReadOnlyCollection<Guid> bookingIds,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BookingAppointment>>(Items
            .Where(value => bookingIds.Contains(value.BookingId))
            .OrderBy(value => value.Id)
            .ToList());

    /// <summary>Returns every in-memory appointment for one Booking in stable ID order.</summary>
    public Task<IReadOnlyList<BookingAppointment>> LockForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        operations?.Record("appointments-locked");
        return Task.FromResult<IReadOnlyList<BookingAppointment>>(Items
            .Where(value => value.BookingId == bookingId)
            .OrderBy(value => value.Id)
            .ToList());
    }
}

/// <summary>Provides durable delivery rows for application tests without a database.</summary>
public sealed class InMemoryEmailDeliveryRepository : IEmailDeliveryRepository
{
    /// <summary>The delivery rows in insertion order.</summary>
    public List<EmailLog> Items { get; } = [];

    /// <inheritdoc />
    public Task<EmailLog?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(delivery => delivery.Id == id));

    /// <inheritdoc />
    public Task<EmailLog?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(delivery => delivery.Id == id));

    /// <inheritdoc />
    public Task<EmailLog?> LockLatestForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Items
            .Where(delivery => delivery.AttendeeId == attendeeId)
            .OrderBy(delivery => delivery.Status is EmailStatus.Failed or EmailStatus.Pending ? 0 : 1)
            .ThenByDescending(delivery => delivery.SentAt)
            .ThenByDescending(delivery => delivery.Id)
            .FirstOrDefault());

    /// <inheritdoc />
    public Task<EmailLog?> GetLatestForAttendeeAsync(
        Guid attendeeId,
        EmailTemplate template,
        CancellationToken cancellationToken) =>
        Task.FromResult(Items
            .Where(delivery => delivery.AttendeeId == attendeeId && delivery.TemplateName == template)
            .OrderBy(delivery => delivery.Status is EmailStatus.Failed or EmailStatus.Pending ? 0 : 1)
            .ThenByDescending(delivery => delivery.SentAt)
            .ThenByDescending(delivery => delivery.Id)
            .FirstOrDefault());

    /// <inheritdoc />
    public void Add(EmailLog delivery) => Items.Add(delivery);
}
