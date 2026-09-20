# 00a — Port source 64 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs","encoding":"utf8","sha256":"73708a52d04ef0c6cc4684d621a89698f72e3e91d554e3a6a8c73ff6bf501178","parts":1,"part":1} -->

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

## tests/EventBooking.Application.Tests/Fakes/RecordingAuditLogger.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Fakes/RecordingAuditLogger.cs","encoding":"utf8","sha256":"0fb9a3d07c277efd18f73801c1ba1c839b47c7f8f17481bc614bf90bba4bfc01","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Tests.Fakes;

public sealed record AuditEntry(
    string EntityType, Guid EntityId, AuditAction Action, ActorType ActorType, string? ActorId, string? Details);

public sealed class RecordingAuditLogger : IAuditLogger
{
    public List<AuditEntry> Entries { get; } = [];

    public void Record(
        string entityType,
        Guid entityId,
        AuditAction action,
        ActorType actorType,
        string? actorId,
        string? details = null) =>
        Entries.Add(new AuditEntry(entityType, entityId, action, actorType, actorId, details));

    public bool Contains(AuditAction action) => Entries.Any(e => e.Action == action);
}
`````

## tests/EventBooking.Application.Tests/Fakes/RecordingEmailSender.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Fakes/RecordingEmailSender.cs","encoding":"utf8","sha256":"78cfbbd44c43ee04b1df4920e6c9926dc64c46a575c4829199a327d5929246ae","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Tests.Fakes;

public sealed class RecordingEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    /// <summary>Set to make the next send report failure, as a bounced or rejected address would.</summary>
    public bool FailNextSend { get; set; }

    public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (FailNextSend)
        {
            FailNextSend = false;
            return Task.FromResult(false);
        }

        Sent.Add(message);
        return Task.FromResult(true);
    }

    public EmailMessage LastOf(EmailTemplate template) =>
        Sent.Last(m => m.Template == template);
}
`````

## tests/EventBooking.Application.Tests/Invites/EligibleSlotFinderTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Invites/EligibleSlotFinderTests.cs","encoding":"utf8","sha256":"18bb2f6f6cb4b2ecb32a36dae620e3729a83665b940912beef0a6699ea5a127b","parts":1,"part":1} -->

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

## tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs","encoding":"utf8","sha256":"ff0980f71cb2c734b871de74168fb998666f1ed6c3ef8f2b1ad2d60a77c76ae9","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Invites;

public class ExpireInvitesHandlerTests
{
    private static readonly CandidatePortalOptions Portal = new(
        "https://booking.example.com", "Corporate HQ", "recruitment@corp.com");

    private readonly InMemoryCandidateRepository _candidates = new();
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryEmployeeGroupRepository _groups = new();
    private readonly InMemoryConfirmedSlotRepository _slots = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Candidate _candidate;

    private ExpireInvitesHandler Handler => new(
        _invites,
        _candidates,
        _settings,
        new InviteIssuer(
            _invites, _groups, new EligibleSlotFinder(_slots, _clock), _settings,
            new FakeTokenService(), EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
            _audit, _clock, Portal),
        EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
        _audit,
        _unitOfWork,
        _clock);

    public ExpireInvitesHandlerTests()
    {
        var pilots = EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        _groups.Items.Add(pilots);
        _candidate = Candidate.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
        _candidates.Add(_candidate);
        AddThreeSlots();
    }

    [Fact]
    public async Task AnInviteThatHasNotExpiredIsLeftAlone()
    {
        GivePendingInvite(expiresInDays: 4, retryCount: 0);

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(0, summary.Expired);
        Assert.Equal(InviteStatus.Pending, _invites.Items.Single().Status);
    }

    [Fact]
    public async Task AnExpiredInviteUnderTheCeilingIsReIssuedWithTheCountIncremented()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(1, summary.Expired);
        Assert.Equal(1, summary.ReIssued);
        Assert.Equal(0, summary.FlaggedForFollowUp);

        Assert.Equal(2, _invites.Items.Count);
        Assert.Equal(InviteStatus.Expired, _invites.Items[0].Status);
        Assert.Equal(InviteStatus.Pending, _invites.Items[1].Status);
        Assert.Equal(1, _invites.Items[1].RetryCount);
        Assert.Equal(CandidateStatus.Invited, _candidate.Status);
    }

    [Fact]
    public async Task TheReIssuedInviteUsesTheReminderTemplate()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);

        await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(EmailTemplate.CandidateReinvite, _email.Sent.Single().Template);
    }

    [Fact]
    public async Task AtTheCeilingTheCandidateIsFlaggedForFollowUpAndNothingIsSent()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: _settings.Settings.MaxAutoRetryCount);

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(1, summary.Expired);
        Assert.Equal(0, summary.ReIssued);
        Assert.Equal(1, summary.FlaggedForFollowUp);

        Assert.Single(_invites.Items);
        Assert.Equal(InviteStatus.Expired, _invites.Items.Single().Status);
        Assert.Equal(CandidateStatus.NoResponseNeedsFollowUp, _candidate.Status);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task RepeatedSweepsDoNotChaseACandidateWhoIsAlreadyFlagged()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: _settings.Settings.MaxAutoRetryCount);
        await Handler.HandleAsync(CancellationToken.None);

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(0, summary.Expired);
        Assert.Equal(CandidateStatus.NoResponseNeedsFollowUp, _candidate.Status);
    }

    [Fact]
    public async Task AReIssueWithNoEligibleSlotsFlagsAwaitingAvailabilityInstead()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);
        _slots.Items.Clear();

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(1, summary.Expired);
        Assert.Equal(0, summary.ReIssued);
        Assert.Equal(CandidateStatus.AwaitingAvailability, _candidate.Status);
    }

    [Fact]
    public async Task AFailedReIssueFlagsTheCandidateForFollowUp()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);
        _groups.Items.Clear();
        _groups.Items.Add(EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]));

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(1, summary.Expired);
        Assert.Equal(0, summary.ReIssued);
        Assert.Equal(1, summary.FlaggedForFollowUp);
        Assert.Equal(InviteStatus.Expired, _invites.Items.Single().Status);
        Assert.Equal(CandidateStatus.NoResponseNeedsFollowUp, _candidate.Status);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task ExpiryIsAudited()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);

        await Handler.HandleAsync(CancellationToken.None);

        var expiry = Assert.Single(_audit.Entries, e => e.Action == AuditAction.InviteExpired);
        Assert.Equal(ActorType.System, expiry.ActorType);
        Assert.Null(expiry.ActorId);
    }

    /// <summary>Verifies expiry persists the business change and delivery result once each.</summary>
    [Fact]
    public async Task TheWholeSweepSavesOnce()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);

        await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnExpiredRecoveryInviteLeavesTheCandidateBooked()
    {
        _candidate.MarkInvited();
        _candidate.MarkBooked();
        _invites.Add(Invite.CreateRecovery(
            Guid.NewGuid(),
            _candidate.Id,
            Guid.NewGuid(),
            $"hash-{Guid.NewGuid():N}",
            _clock.UtcNow.AddDays(-1),
            _slots.Items.Take(3).Select(s => s.Id),
            [AppointmentTypeIds.DrugAndAlcoholTesting]));

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(1, summary.Expired);
        Assert.Equal(0, summary.ReIssued);
        Assert.Equal(0, summary.FlaggedForFollowUp);
        Assert.Equal(InviteStatus.Expired, _invites.Items.Single().Status);
        Assert.Equal(CandidateStatus.Booked, _candidate.Status);
        Assert.Single(_invites.Items);
        Assert.Empty(_email.Sent);
        Assert.True(_audit.Contains(AuditAction.InviteExpired));
    }

    private void GivePendingInvite(int expiresInDays, int retryCount)
    {
        _candidate.MarkInvited();
        _invites.Add(Invite.CreateInitial(
            Guid.NewGuid(),
            _candidate.Id,
            $"hash-{Guid.NewGuid():N}",
            _clock.UtcNow.AddDays(expiresInDays),
            _slots.Items.Take(3).Select(s => s.Id),
            _candidate.RequiredAppointmentTypeIds,
            retryCount));
    }

    private void AddThreeSlots()
    {
        foreach (var day in new[] { 10, 12, 14 })
        {
            var proposal = SlotProposal.Create(
                Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0)),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            _slots.Add(ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal));
        }
    }
}
`````

## tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs","encoding":"utf8","sha256":"74f0150aac27942f172c4f6acc8788f670602de43d59d99411804427a0648753","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Invites;

public class InviteIssuerTests
{
    private static readonly CandidatePortalOptions Portal = new(
        "https://booking.example.com", "Corporate HQ", "recruitment@corp.com");

    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryEmployeeGroupRepository _groups = new();
    private readonly InMemoryConfirmedSlotRepository _slots = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly FakeTokenService _tokens = new();
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Candidate _candidate;

    public InviteIssuerTests()
    {
        var pilots = EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        _groups.Items.Add(pilots);
        _candidate = Candidate.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
    }

    private InviteIssuer Issuer => new(
        _invites,
        _groups,
        new EligibleSlotFinder(_slots, _clock),
        _settings,
        _tokens,
        EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
        _audit,
        _clock,
        Portal);

    private async Task<InviteIssueResult> Issue(int retryCount = 0, bool isReinvite = false)
    {
        var outcome = await Issuer.IssueInitialAsync(
            _candidate, retryCount, ActorType.System, null, isReinvite, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        var result = outcome.Value;
        if (result.DispatchPlan is not { } plan)
        {
            return result;
        }

        var status = await EmailDeliveryTestFactory.Create(
                _deliveries, _email, _unitOfWork, _clock)
            .DispatchClaimedAsync(plan.DeliveryId, plan.Message, CancellationToken.None, plan.OnSent);
        return result with
        {
            EmailSent = status == EmailStatus.Sent,
            DeliveryStatus = status.ToString(),
        };
    }

    [Fact]
    public async Task ThreeEligibleSlotsProduceAnInviteWithThreeOptions()
    {
        AddSlot(new DateOnly(2026, 9, 10));
        AddSlot(new DateOnly(2026, 9, 12));
        AddSlot(new DateOnly(2026, 9, 14));

        var result = await Issue();

        Assert.True(result.Invited);
        Assert.True(result.EmailSent);

        var invite = Assert.Single(_invites.Items);
        Assert.Equal(result.InviteId, invite.Id);
        Assert.Equal(_candidate.Id, invite.CandidateId);
        Assert.Equal(3, invite.Options.Count);
        Assert.Equal(InviteStatus.Pending, invite.Status);
        Assert.Equal(0, invite.RetryCount);
        Assert.Equal(CandidateStatus.Invited, _candidate.Status);
    }

    [Fact]
    public async Task TheExpiryComesFromSystemSettings()
    {
        AddThreeSlots();

        await Issue();

        Assert.Equal(
            _clock.UtcNow.AddDays(_settings.Settings.InviteExpiryDays),
            _invites.Items.Single().ExpiresAt);
    }

    [Fact]
    public async Task OnlyTheTokenHashIsStoredAndTheLinkCarriesTheToken()
    {
        AddThreeSlots();

        var result = await Issue();

        var invite = _invites.Items.Single();
        var expected = _tokens.Issue(result.InviteId!.Value);
        Assert.Equal(expected.TokenHash, invite.TokenHash);
        Assert.DoesNotContain(expected.Token, invite.TokenHash);
        Assert.Contains($"https://booking.example.com/book/{expected.Token}", _email.Sent.Single().TextBody);
    }

    [Fact]
    public async Task FewerThanThreeEligibleSlotsMeansNoInviteAndNoEmail()
    {
        AddSlot(new DateOnly(2026, 9, 10));
        AddSlot(new DateOnly(2026, 9, 12));

        var result = await Issue();

        Assert.False(result.Invited);
        Assert.Null(result.InviteId);
        Assert.False(result.EmailSent);
        Assert.Empty(_invites.Items);
        Assert.Empty(_email.Sent);
        Assert.Equal(CandidateStatus.AwaitingAvailability, _candidate.Status);
    }

    [Fact]
    public async Task APendingInviteIsSupersededByTheNewOne()
    {
        AddThreeSlots();
        var old = Invite.CreateInitial(
            Guid.NewGuid(), _candidate.Id, "old-hash", _clock.UtcNow.AddDays(4),
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            _candidate.RequiredAppointmentTypeIds, 0);
        _invites.Add(old);
        _candidate.MarkInvited();

        await Issue();

        Assert.Equal(InviteStatus.Superseded, old.Status);
        Assert.Equal(2, _invites.Items.Count);
    }

    [Fact]
    public async Task TheRetryCountIsCarriedOntoTheNewInvite()
    {
        AddThreeSlots();

        await Issue(retryCount: 2);

        Assert.Equal(2, _invites.Items.Single().RetryCount);
    }

    [Fact]
    public async Task AReInviteUsesTheReminderTemplate()
    {
        AddThreeSlots();

        await Issue(isReinvite: true);

        Assert.Equal(EmailTemplate.CandidateReinvite, _email.Sent.Single().Template);
    }

    [Fact]
    public async Task AFailedSendStillLeavesTheInviteInPlace()
    {
        AddThreeSlots();
        _email.FailNextSend = true;

        var result = await Issue();

        Assert.True(result.Invited);
        Assert.False(result.EmailSent);
        Assert.Single(_invites.Items);
        Assert.True(_audit.Contains(AuditAction.InviteCreated));
        Assert.False(_audit.Contains(AuditAction.InviteSent));
    }

    [Fact]
    public async Task ASuccessfulIssueWritesBothAuditEntries()
    {
        AddThreeSlots();

        await Issue();

        Assert.True(_audit.Contains(AuditAction.InviteCreated));
        Assert.True(_audit.Contains(AuditAction.InviteSent));
        Assert.All(
            _audit.Entries,
            e => Assert.Equal(AuditEntityTypes.Invite, e.EntityType));
        var sent = Assert.Single(_audit.Entries, e => e.Action == AuditAction.InviteSent);
        Assert.StartsWith("invite ", sent.Details, StringComparison.Ordinal);
        Assert.DoesNotContain("@", sent.Details, StringComparison.Ordinal);
    }

    private void AddThreeSlots()
    {
        AddSlot(new DateOnly(2026, 9, 10));
        AddSlot(new DateOnly(2026, 9, 12));
        AddSlot(new DateOnly(2026, 9, 14));
    }

    private void AddSlot(DateOnly date)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(date, new TimeOnly(9, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        _slots.Add(ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal));
    }
}
`````
