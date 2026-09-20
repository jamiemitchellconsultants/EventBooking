# 00b — Vocabulary edits 82 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — tests/EventBooking.Application.Tests/Fakes/InMemoryQueries.cs — 1/1

<!-- vocabulary-file: {"id":277,"oldPath":"tests/EventBooking.Application.Tests/Fakes/InMemoryQueries.cs","newPath":"tests/EventBooking.Application.Tests/Fakes/InMemoryQueries.cs","beforeSha":"2bb45fbcb6ba2e137a741ddcea9d08d1090929d31463756a6a215670407db4ca","afterSha":"5557e7d04631425ddc8535d240abc8d28dbc884f53ee68f3a92bf2527ced962b","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Attendees;

namespace EventBooking.Application.Tests.Fakes;

/// <summary>Serves one canned readiness snapshot while counting query executions.</summary>
public sealed class InMemoryQueries : IAttendeeReadinessQueries
{
    /// <summary>Gets the snapshot returned for any attendee.</summary>
    public AttendeeReadinessSnapshot? Snapshot { get; set; }

    /// <summary>Gets how many times the snapshot was requested.</summary>
    public int QueryCount { get; private set; }

    /// <inheritdoc />
    public Task<AttendeeReadinessSnapshot?> GetSnapshotAsync(
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        QueryCount++;
        return Task.FromResult(Snapshot);
    }
}


/// <summary>Returns a fixed active-booking listing for the staff cancellation workflow.</summary>
public sealed class InMemoryAttendeeBookingQueries : IAttendeeBookingQueries
{
    /// <summary>Gets the rows returned for any attendee; null stands for an unknown attendee.</summary>
    public IReadOnlyList<AttendeeBookingSummary>? Rows { get; set; } = [];

    /// <summary>Gets how many times the listing was requested.</summary>
    public int QueryCount { get; private set; }

    /// <inheritdoc />
    public Task<IReadOnlyList<AttendeeBookingSummary>?> ListActiveForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        QueryCount++;
        return Task.FromResult(Rows);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs — 1/1

<!-- vocabulary-file: {"id":278,"oldPath":"tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs","newPath":"tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs","beforeSha":"73708a52d04ef0c6cc4684d621a89698f72e3e91d554e3a6a8c73ff6bf501178","afterSha":"161bd7aca981215ceab2a223801fd7b48544ba059c3de57d5c571185265b0330","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Settings;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Fakes;

/// <summary>Provides proposal state for application tests without database locking.</summary>
public sealed class InMemorySlotProposalRepository : ISlotProposalRepository
{
    public List<SlotProposal> Items { get; } = [];

    public Task<SlotProposal?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(p => p.Id == id));

    /// <summary>Returns the in-memory proposal because this test double has no database row lock.</summary>
    public Task<SlotProposal?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(p => p.Id == id));

    public Task<IReadOnlyList<SlotProposal>> ListOpenAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SlotProposal>>(
            Items.Where(p => p.Status == SlotProposalStatus.Open).ToList());

    public void Add(SlotProposal proposal) => Items.Add(proposal);
}

public sealed class InMemoryConfirmedSlotRepository(
    TransactionOperationLog? operations = null,
    TransactionalSlotLockCoordinator? locks = null)
    : IConfirmedSlotRepository
{
    public List<ConfirmedSlot> Items { get; } = [];

    public Task<ConfirmedSlot?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        operations?.Record("slot-reloaded");
        return Task.FromResult(Items.SingleOrDefault(s => s.Id == id));
    }

    public async Task<ConfirmedSlot?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        operations?.Record("slot-guard-locked");
        if (locks is not null)
        {
            await locks.AcquireAsync(id, cancellationToken);
        }

        return Items.SingleOrDefault(s => s.Id == id);
    }

    public Task<IReadOnlyList<ConfirmedSlot>> ListActiveAsync(
        DateOnly onOrAfter, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ConfirmedSlot>>(
            Items
                .Where(s => s.Status == ConfirmedSlotStatus.Active && s.Window.Date >= onOrAfter)
                .ToList());

    public Task<IReadOnlyList<ConfirmedSlot>> ListAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ConfirmedSlot>>(Items.ToList());

    public void Add(ConfirmedSlot slot) => Items.Add(slot);
}

/// <summary>
/// There is nothing to lock in memory, so this simply hands back the same capacity objects the
/// slot repository holds. The locking itself is proved against a real database in Task 67.
/// </summary>
public sealed class InMemorySlotCapacityRepository(
    InMemoryConfirmedSlotRepository slots,
    TransactionOperationLog? operations = null)
    : ISlotCapacityRepository
{
    public int LockCallCount { get; private set; }

    public Task<IReadOnlyList<SlotCapacity>> LockForUpdateAsync(
        Guid confirmedSlotId,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        CancellationToken cancellationToken)
    {
        operations?.Record("capacity-locked");
        LockCallCount++;

        var slot = slots.Items.SingleOrDefault(s => s.Id == confirmedSlotId);
        if (slot is null)
        {
            return Task.FromResult<IReadOnlyList<SlotCapacity>>([]);
        }

        var locked = appointmentTypeIds
            .OrderBy(id => id)
            .Select(slot.CapacityFor)
            .ToList();

        return Task.FromResult<IReadOnlyList<SlotCapacity>>(locked);
    }
}

/// <summary>Provides candidate state and observable lifecycle-lock order for application tests.</summary>
public sealed class InMemoryCandidateRepository(TransactionOperationLog? operations = null) : ICandidateRepository
{
    public List<Candidate> Items { get; } = [];

    public Task<Candidate?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(c => c.Id == id));

    /// <summary>Returns the in-memory candidate because this test double has no database row lock.</summary>
    public Task<Candidate?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        operations?.Record("candidate-locked");
        return Task.FromResult(Items.SingleOrDefault(c => c.Id == id));
    }

    public Task<Candidate?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(
            Items.SingleOrDefault(c => string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<Candidate>> ListAsync(
        CandidateStatus? status, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Candidate>>(
            Items.Where(c => status is null || c.Status == status).ToList());

    public void Add(Candidate candidate) => Items.Add(candidate);

    public void Remove(Candidate candidate) => Items.Remove(candidate);
}

/// <summary>Provides a controllable candidate read barrier for concurrency interleaving tests.</summary>
public sealed class BlockingCandidateRepository : ICandidateRepository
{
    private Guid? _blockedCandidateId;
    private TaskCompletionSource<bool>? _blocked;
    private TaskCompletionSource<bool>? _release;

    public List<Candidate> Items { get; } = [];

    public void BlockNextGetFor(Guid candidateId)
    {
        _blockedCandidateId = candidateId;
        _blocked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public Task WaitUntilBlockedAsync() =>
        _blocked?.Task ?? throw new InvalidOperationException("No candidate get is configured to block.");

    public void ReleaseBlockedGet() =>
        (_release ?? throw new InvalidOperationException("No candidate get is configured to block."))
        .TrySetResult(true);

    public async Task<Candidate?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (_blockedCandidateId == id)
        {
            _blocked!.TrySetResult(true);
            await _release!.Task.WaitAsync(cancellationToken);
            _blockedCandidateId = null;
        }

        return Items.SingleOrDefault(c => c.Id == id);
    }

    /// <summary>Uses the same controlled read as the lock operation in this in-memory test double.</summary>
    public Task<Candidate?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync(id, cancellationToken);

    public Task<Candidate?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(
            Items.SingleOrDefault(c => string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<Candidate>> ListAsync(
        CandidateStatus? status,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Candidate>>(
            Items.Where(c => status is null || c.Status == status).ToList());

    public void Add(Candidate candidate) => Items.Add(candidate);

    public void Remove(Candidate candidate) => Items.Remove(candidate);
}

/// <summary>Provides invite state and observable invite-lock order for application tests.</summary>
public sealed class InMemoryInviteRepository(TransactionOperationLog? operations = null) : IInviteRepository
{
    public List<Invite> Items { get; } = [];

    public Task<Invite?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(i => i.Id == id));

    /// <summary>Returns the in-memory invite because this test double has no database row lock.</summary>
    public Task<Invite?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(i => i.Id == id));

    public Task<Invite?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(i => i.TokenHash == tokenHash));

    public Task<Invite?> LockByTokenHashForUpdateAsync(
        string tokenHash,
        CancellationToken cancellationToken)
    {
        operations?.Record("invite-locked");
        return Task.FromResult(Items.SingleOrDefault(i => i.TokenHash == tokenHash));
    }

    /// <summary>Returns the current pending in-memory invite because this double has no row lock.</summary>
    public Task<Invite?> LockPendingForCandidateAsync(Guid candidateId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(i => i.CandidateId == candidateId
            && i.Status == InviteStatus.Pending));

    public Task<Invite?> GetPendingForCandidateAsync(Guid candidateId, CancellationToken cancellationToken) =>
        Task.FromResult(
            Items.SingleOrDefault(i => i.CandidateId == candidateId && i.Status == InviteStatus.Pending));

    /// <summary>Returns the current pending in-memory invite because this double has no row lock.</summary>
    public Task<Invite?> LockPendingInitialForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        operations?.Record("initial-invite-locked");
        return Task.FromResult(Items.SingleOrDefault(i => i.CandidateId == candidateId
            && i.Status == InviteStatus.Pending));
    }

    public Task<IReadOnlyList<Invite>> ListPendingExpiredAsync(
        DateTimeOffset asAt, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Invite>>(
            Items.Where(i => i.Status == InviteStatus.Pending && i.ExpiresAt <= asAt).ToList());

    /// <summary>Returns every pending in-memory invite ordered by ID for lock-order tests.</summary>
    public Task<IReadOnlyList<Invite>> LockPendingListForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        operations?.Record("pending-invites-locked");
        return Task.FromResult<IReadOnlyList<Invite>>(
            Items
                .Where(i => i.CandidateId == candidateId && i.Status == InviteStatus.Pending)
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

    public Task<Booking?> GetByManageTokenHashAsync(
        string manageTokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(b => b.ManageTokenHash == manageTokenHash));

    public Task<Guid?> GetConfirmedSlotIdByManageTokenHashAsync(
        string manageTokenHash,
        CancellationToken cancellationToken)
    {
        operations?.Record("booking-slot-located");
        return Task.FromResult(
            Items.SingleOrDefault(b => b.ManageTokenHash == manageTokenHash)?.ConfirmedSlotId);
    }

    /// <summary>Returns the candidate identifier associated with the supplied test manage token.</summary>
    public Task<Guid?> GetCandidateIdByManageTokenHashAsync(
        string manageTokenHash,
        CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(b => b.ManageTokenHash == manageTokenHash)?.CandidateId);

    public Task<Booking?> LockByManageTokenHashForUpdateAsync(
        string manageTokenHash,
        CancellationToken cancellationToken)
    {
        operations?.Record("booking-locked");
        return Task.FromResult(Items.SingleOrDefault(b => b.ManageTokenHash == manageTokenHash));
    }

    /// <inheritdoc/>
    public Task<Booking?> LockByIdForCandidateAsync(
        Guid bookingId,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        operations?.Record("booking-locked");
        return Task.FromResult(
            Items.SingleOrDefault(b => b.Id == bookingId && b.CandidateId == candidateId));
    }

    /// <summary>Returns the candidate's active in-memory booking because this double has no row lock.</summary>
    public Task<Booking?> LockActiveForCandidateAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        operations?.Record("active-booking-locked");
        return Task.FromResult(Items.SingleOrDefault(b => b.CandidateId == candidateId
            && b.Status == BookingStatus.Active));
    }

    public Task<Booking?> GetActiveForCandidateAsync(Guid candidateId, CancellationToken cancellationToken) =>
        Task.FromResult(
            Items.SingleOrDefault(b => b.CandidateId == candidateId && b.Status == BookingStatus.Active));

    /// <summary>Returns the candidate's active in-memory booking because this double has no row lock.</summary>
    public Task<Booking?> LockActiveOriginalForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        operations?.Record("original-booking-locked");
        return Task.FromResult(Items.SingleOrDefault(b => b.CandidateId == candidateId
            && b.Status == BookingStatus.Active
            && b.RecoveryOfBookingId is null));
    }

    /// <summary>Returns candidate identifiers for active in-memory bookings on one slot.</summary>
    public Task<IReadOnlyList<Guid>> ListActiveCandidateIdsForSlotAsync(
        Guid confirmedSlotId,
        CancellationToken cancellationToken)
    {
        operations?.Record("active-candidate-ids-snapshotted");
        return Task.FromResult<IReadOnlyList<Guid>>(
            Items
                .Where(b => b.ConfirmedSlotId == confirmedSlotId && b.Status == BookingStatus.Active)
                .Select(b => b.CandidateId)
                .ToList());
    }

    public Task<IReadOnlyList<Booking>> ListActiveForSlotAsync(
        Guid confirmedSlotId, CancellationToken cancellationToken)
    {
        operations?.Record("active-bookings-listed");
        return Task.FromResult<IReadOnlyList<Booking>>(
            Items
                .Where(b => b.ConfirmedSlotId == confirmedSlotId && b.Status == BookingStatus.Active)
                .ToList());
    }

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
}

/// <summary>Provides Employee Group reference data with identifier and code lookups.</summary>
public sealed class InMemoryEmployeeGroupRepository : IEmployeeGroupRepository
{
    /// <summary>Gets the mutable reference-data collection.</summary>
    public List<EmployeeGroup> Items { get; } = [];

    /// <summary>Gets a group by stable identifier.</summary>
    public Task<EmployeeGroup?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(group => group.Id == id));

    /// <summary>Gets a group from a trimmed case-insensitive canonical-code input.</summary>
    public Task<EmployeeGroup?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return Task.FromResult(
            Items.SingleOrDefault(group => string.Equals(group.Code, normalized, StringComparison.Ordinal)));
    }

    /// <summary>Lists active mapped groups ordered by display name.</summary>
    public Task<IReadOnlyList<EmployeeGroup>> ListActiveAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EmployeeGroup>>(
            Items
                .Where(group => group.IsActive && group.Requirements.Count > 0)
                .OrderBy(group => group.Name)
                .ToList());
}

public sealed class InMemoryAppointmentTypeRepository : IAppointmentTypeRepository
{
    public List<AppointmentType> Items { get; } = AppointmentType.CreateFixedSet().ToList();

    public Task<IReadOnlyList<AppointmentType>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AppointmentType>>(Items.ToList());

    public Task<AppointmentType?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(t => t.Id == id));
}

public sealed class InMemorySystemSettingsRepository : ISystemSettingsRepository
{
    public SystemSettings Settings { get; } = SystemSettings.CreateDefault();

    public Task<SystemSettings> GetAsync(CancellationToken cancellationToken) =>
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
            booking.CandidateId,
            booking.RecoveryOfBookingId ?? booking.Id,
            booking.Id,
            booking.ConfirmedSlotId,
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
    public Task<EmailLog?> LockLatestForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Items
            .Where(delivery => delivery.CandidateId == candidateId)
            .OrderBy(delivery => delivery.Status is EmailStatus.Failed or EmailStatus.Pending ? 0 : 1)
            .ThenByDescending(delivery => delivery.SentAt)
            .ThenByDescending(delivery => delivery.Id)
            .FirstOrDefault());

    /// <inheritdoc />
    public Task<EmailLog?> GetLatestForCandidateAsync(
        Guid candidateId,
        EmailTemplate template,
        CancellationToken cancellationToken) =>
        Task.FromResult(Items
            .Where(delivery => delivery.CandidateId == candidateId && delivery.TemplateName == template)
            .OrderBy(delivery => delivery.Status is EmailStatus.Failed or EmailStatus.Pending ? 0 : 1)
            .ThenByDescending(delivery => delivery.SentAt)
            .ThenByDescending(delivery => delivery.Id)
            .FirstOrDefault());

    /// <inheritdoc />
    public void Add(EmailLog delivery) => Items.Add(delivery);
}
`````

## after — tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs — 1/1

<!-- vocabulary-file: {"id":278,"oldPath":"tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs","newPath":"tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs","beforeSha":"73708a52d04ef0c6cc4684d621a89698f72e3e91d554e3a6a8c73ff6bf501178","afterSha":"161bd7aca981215ceab2a223801fd7b48544ba059c3de57d5c571185265b0330","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
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

public sealed class InMemoryEventRepository(
    TransactionOperationLog? operations = null,
    TransactionalEventLockCoordinator? locks = null)
    : IEventRepository
{
    public List<Event> Items { get; } = [];

    public Task<Event?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        operations?.Record("event-reloaded");
        return Task.FromResult(Items.SingleOrDefault(s => s.Id == id));
    }

    public async Task<Event?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        operations?.Record("event-guard-locked");
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

    public void Add(Event eventItem) => Items.Add(eventItem);
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
}

/// <summary>Provides invite state and observable invite-lock order for application tests.</summary>
public sealed class InMemoryInviteRepository(TransactionOperationLog? operations = null) : IInviteRepository
{
    public List<Invite> Items { get; } = [];

    public Task<Invite?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(i => i.Id == id));

    /// <summary>Returns the in-memory invite because this test double has no database row lock.</summary>
    public Task<Invite?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(i => i.Id == id));

    public Task<Invite?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(i => i.TokenHash == tokenHash));

    public Task<Invite?> LockByTokenHashForUpdateAsync(
        string tokenHash,
        CancellationToken cancellationToken)
    {
        operations?.Record("invite-locked");
        return Task.FromResult(Items.SingleOrDefault(i => i.TokenHash == tokenHash));
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

    public Task<Booking?> GetByManageTokenHashAsync(
        string manageTokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(b => b.ManageTokenHash == manageTokenHash));

    public Task<Guid?> GetEventIdByManageTokenHashAsync(
        string manageTokenHash,
        CancellationToken cancellationToken)
    {
        operations?.Record("booking-event-located");
        return Task.FromResult(
            Items.SingleOrDefault(b => b.ManageTokenHash == manageTokenHash)?.EventId);
    }

    /// <summary>Returns the attendee identifier associated with the supplied test manage token.</summary>
    public Task<Guid?> GetAttendeeIdByManageTokenHashAsync(
        string manageTokenHash,
        CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(b => b.ManageTokenHash == manageTokenHash)?.AttendeeId);

    public Task<Booking?> LockByManageTokenHashForUpdateAsync(
        string manageTokenHash,
        CancellationToken cancellationToken)
    {
        operations?.Record("booking-locked");
        return Task.FromResult(Items.SingleOrDefault(b => b.ManageTokenHash == manageTokenHash));
    }

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
}

public sealed class InMemoryAppointmentTypeRepository : IAppointmentTypeRepository
{
    public List<AppointmentType> Items { get; } = AppointmentType.CreateFixedSet().ToList();

    public Task<IReadOnlyList<AppointmentType>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AppointmentType>>(Items.ToList());

    public Task<AppointmentType?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(t => t.Id == id));
}

public sealed class InMemorySystemSettingsRepository : ISystemSettingsRepository
{
    public SystemSettings Settings { get; } = SystemSettings.CreateDefault();

    public Task<SystemSettings> GetAsync(CancellationToken cancellationToken) =>
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
`````

## before — tests/EventBooking.Application.Tests/Invites/EligibleSlotFinderTests.cs — 1/1

<!-- vocabulary-file: {"id":279,"oldPath":"tests/EventBooking.Application.Tests/Invites/EligibleSlotFinderTests.cs","newPath":"tests/EventBooking.Application.Tests/Invites/EligibleEventFinderTests.cs","beforeSha":"18bb2f6f6cb4b2ecb32a36dae620e3729a83665b940912beef0a6699ea5a127b","afterSha":"3ea0c0cdc8615edc1afe5618e9bbe1dfb752b57764111c74dba23beaa3436e39","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Invites;

public class EligibleSlotFinderTests
{
    private static readonly Guid[] NeedsTwo =
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting];

    private readonly InMemoryConfirmedSlotRepository _slots = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private EligibleSlotFinder Finder => new(_slots, _clock);

    [Fact]
    public async Task TheThreeEarliestQualifyingSlotsAreReturnedInOrder()
    {
        AddSlot(new DateOnly(2026, 9, 14));
        AddSlot(new DateOnly(2026, 9, 10));
        AddSlot(new DateOnly(2026, 9, 12));
        AddSlot(new DateOnly(2026, 9, 16));

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Equal(
            new[] { new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 14) },
            result.Select(s => s.Window.Date));
    }

    [Fact]
    public async Task TwoWindowsOnOneDayAreOrderedByStartTime()
    {
        AddSlot(new DateOnly(2026, 9, 10), startHour: 13);
        AddSlot(new DateOnly(2026, 9, 10), startHour: 9);

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Equal(
            new[] { new TimeOnly(9, 0), new TimeOnly(13, 0) },
            result.Select(s => s.Window.StartTime));
    }

    [Fact]
    public async Task ASlotFullInOneRequiredTypeIsNotEligibleEvenIfTheOthersHaveRoom()
    {
        var slot = AddSlot(new DateOnly(2026, 9, 10), drugAndAlcoholHeadcount: 1);
        slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ASlotFullOnlyInATypeTheCandidateDoesNotNeedIsStillEligible()
    {
        var slot = AddSlot(new DateOnly(2026, 9, 10), medicalHeadcount: 1);
        slot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Single(result);
    }

    [Fact]
    public async Task CancelledSlotsAreNeverEligible()
    {
        var slot = AddSlot(new DateOnly(2026, 9, 10));
        slot.Cancel();

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task TodaysAndPastWindowsAreNeverEligible()
    {
        AddSlot(new DateOnly(2026, 9, 3));
        AddSlot(new DateOnly(2026, 9, 1));

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExcludedSlotsAreSkipped()
    {
        var first = AddSlot(new DateOnly(2026, 9, 10));
        AddSlot(new DateOnly(2026, 9, 12));

        var result = await Finder.FindAsync(NeedsTwo, 3, [first.Id], CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 9, 12), Assert.Single(result).Window.Date);
    }

    [Fact]
    public async Task FewerQualifyingSlotsThanAskedForReturnsWhatThereIs()
    {
        AddSlot(new DateOnly(2026, 9, 10));
        AddSlot(new DateOnly(2026, 9, 12));

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Equal(2, result.Count);
    }

    private ConfirmedSlot AddSlot(
        DateOnly date,
        int startHour = 9,
        int drugAndAlcoholHeadcount = 10,
        int medicalHeadcount = 6,
        int uniformHeadcount = 8)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(date, new TimeOnly(startHour, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), drugAndAlcoholHeadcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), medicalHeadcount);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), uniformHeadcount);

        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
        _slots.Add(slot);
        return slot;
    }
}
`````
