# 00a — Port source 28 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs","encoding":"utf8","sha256":"a11942dd0959aebadb58d7b9933f68a972edbc6d4822157374aa4928ec170c8a","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Queries;

public sealed class DashboardQueries(EventBookingDbContext context, IClock clock) : IDashboardQueries
{
    public async Task<IReadOnlyList<AwaitingAvailabilityRow>> AwaitingAvailabilityAsync(
        CancellationToken cancellationToken)
    {
        var today = clock.TodayAtHeadOffice;
        var rows = await CandidateRows(CandidateStatus.AwaitingAvailability).ToListAsync(cancellationToken);

        return rows
            .Select(row =>
            {
                var since = clock.DateAtHeadOffice(row.StatusChangedAt);
                return new AwaitingAvailabilityRow(
                    row.Id,
                    row.Name,
                    row.Email,
                    row.AppointmentTypeIds.Select(AppointmentTypeIds.CodeOf)
                        .OrderBy(code => code, StringComparer.Ordinal).ToList(),
                    since,
                    Math.Max(0, today.DayNumber - since.DayNumber));
            })
            .OrderByDescending(row => row.DaysWaiting)
            .ToList();
    }

    public async Task<IReadOnlyList<NoResponseRow>> NoResponseAsync(CancellationToken cancellationToken)
    {
        var rows = await CandidateRows(CandidateStatus.NoResponseNeedsFollowUp).ToListAsync(cancellationToken);

        return rows
            .Select(row => new NoResponseRow(
                row.Id,
                row.Name,
                row.Email,
                row.AppointmentTypeIds.Select(AppointmentTypeIds.CodeOf)
                    .OrderBy(code => code, StringComparer.Ordinal).ToList(),
                clock.DateAtHeadOffice(row.StatusChangedAt)))
            .OrderBy(row => row.GaveUpOn)
            .ToList();
    }

    public async Task<IReadOnlyList<SlotOverviewRow>> SlotsOverviewAsync(CancellationToken cancellationToken)
    {
        // Past slots can no longer be cancelled, so the operations list shows only
        // today and future slots.
        var today = clock.TodayAtHeadOffice;
        var rows = await context.ConfirmedSlots
            .AsNoTracking()
            .Where(slot => slot.Status == ConfirmedSlotStatus.Active && slot.Window.Date >= today)
            .Select(slot => new
            {
                slot.Id,
                slot.Window.Date,
                slot.Window.StartTime,
                Capacities = slot.Capacities.Select(capacity => new
                {
                    capacity.AppointmentTypeId,
                    capacity.TotalHeadcount,
                    capacity.RemainingCapacity,
                }).ToList(),
                ActiveBookings = context.Bookings.Count(booking =>
                    booking.ConfirmedSlotId == slot.Id && booking.Status == BookingStatus.Active),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new SlotOverviewRow(
                row.Id,
                row.Date,
                row.StartTime,
                row.StartTime.Add(SlotWindow.Duration),
                row.Capacities.Select(capacity => new SlotCapacityRow(
                    AppointmentTypeIds.CodeOf(capacity.AppointmentTypeId),
                    capacity.TotalHeadcount,
                    capacity.RemainingCapacity))
                    .OrderBy(capacity => capacity.Code, StringComparer.Ordinal).ToList(),
                row.ActiveBookings))
            .OrderBy(row => row.Date)
            .ThenBy(row => row.StartTime)
            .ToList();
    }

    /// <summary>Returns the latest delivery per candidate and whether its persisted context remains actionable.</summary>
    public async Task<IReadOnlyList<CandidateEmailStatusRow>> LatestEmailStatusAsync(
        CancellationToken cancellationToken)
    {
        // EF Core cannot translate this per-group priority cleanly. An unresolved attempt remains
        // visible ahead of terminal history so one candidate's second message cannot hide it.
        var latest = (await context.EmailLogs
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .GroupBy(delivery => delivery.CandidateId)
            .Select(group => group
                .OrderBy(delivery => delivery.Status is EmailStatus.Failed or EmailStatus.Pending ? 0 : 1)
                .ThenByDescending(delivery => delivery.SentAt)
                .ThenByDescending(delivery => delivery.Id)
                .First())
            .ToList();

        var candidateIds = latest.Select(delivery => delivery.CandidateId).Distinct().ToList();
        var candidateStatuses = await context.Candidates
            .AsNoTracking()
            .Where(candidate => candidateIds.Contains(candidate.Id))
            .ToDictionaryAsync(candidate => candidate.Id, candidate => candidate.Status, cancellationToken);

        var inviteIds = latest.Where(delivery => delivery.InviteId.HasValue)
            .Select(delivery => delivery.InviteId!.Value).Distinct().ToList();
        var retryableInviteIds = await context.Invites
            .AsNoTracking()
            .Where(invite => inviteIds.Contains(invite.Id)
                && invite.Status == InviteStatus.Pending
                && invite.ExpiresAt > clock.UtcNow)
            .Select(invite => invite.Id)
            .ToHashSetAsync(cancellationToken);

        var bookingIds = latest.Where(delivery => delivery.BookingId.HasValue)
            .Select(delivery => delivery.BookingId!.Value).Distinct().ToList();
        var retryableBookingIds = await context.Bookings
            .AsNoTracking()
            .Where(booking => bookingIds.Contains(booking.Id) && booking.Status == BookingStatus.Active)
            .Select(booking => booking.Id)
            .ToHashSetAsync(cancellationToken);

        // Slot-cancellation retry needs the staged booking to exist (it is cancelled, not
        // active), matching RetryEmailHandler's booking lookup.
        var existingBookingIds = await context.Bookings
            .AsNoTracking()
            .Where(booking => bookingIds.Contains(booking.Id))
            .Select(booking => booking.Id)
            .ToHashSetAsync(cancellationToken);

        var slotIds = latest.Where(delivery => delivery.ConfirmedSlotId.HasValue)
            .Select(delivery => delivery.ConfirmedSlotId!.Value).Distinct().ToList();
        var cancelledSlotIds = await context.ConfirmedSlots
            .AsNoTracking()
            .Where(slot => slotIds.Contains(slot.Id) && slot.Status == ConfirmedSlotStatus.Cancelled)
            .Select(slot => slot.Id)
            .ToHashSetAsync(cancellationToken);

        return latest
            .Select(delivery => new CandidateEmailStatusRow(
                delivery.CandidateId,
                delivery.TemplateName,
                delivery.SentAt,
                delivery.Status,
                (delivery.Status is EmailStatus.Failed or EmailStatus.Pending)
                && (delivery.TemplateName switch
                    {
                        EmailTemplate.CandidateInvite or EmailTemplate.CandidateReinvite =>
                            delivery.InviteId is { } inviteId && retryableInviteIds.Contains(inviteId),
                        EmailTemplate.BookingConfirmation =>
                            delivery.BookingId is { } bookingId && retryableBookingIds.Contains(bookingId),
                        EmailTemplate.SlotCancelledRebookingNeeded =>
                            delivery.ConfirmedSlotId is { } slotId
                            && cancelledSlotIds.Contains(slotId)
                            && delivery.BookingId is { } cancellationBookingId
                            && existingBookingIds.Contains(cancellationBookingId)
                            && candidateStatuses.TryGetValue(delivery.CandidateId, out var status)
                            && status is CandidateStatus.Invited or CandidateStatus.AwaitingAvailability,
                        _ => false,
                    })))
            .ToList();
    }

    private IQueryable<CandidateRow> CandidateRows(CandidateStatus status) =>
        context.Candidates
            .AsNoTracking()
            .Where(candidate => candidate.Status == status)
            .Select(candidate => new CandidateRow(
                candidate.Id,
                candidate.Name,
                candidate.Email,
                EF.Property<DateTimeOffset>(candidate, StatusStampingInterceptor.ShadowProperty),
                candidate.Requirements.Select(requirement => requirement.AppointmentTypeId).ToList()));

    private sealed record CandidateRow(
        Guid Id,
        string Name,
        string Email,
        DateTimeOffset StatusChangedAt,
        List<Guid> AppointmentTypeIds);
}
`````

## src/EventBooking.Infrastructure/Persistence/Repositories/BookingAppointmentRepository.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Repositories/BookingAppointmentRepository.cs","encoding":"utf8","sha256":"130ffd963cc60bebaa25259ed4090af5a53a1f262c1a6c6b9205970c21626d59","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Repositories;

/// <summary>Persists and row-locks booking appointments inside trusted appointment-type scope.</summary>
/// <param name="context">The database context.</param>
public sealed class BookingAppointmentRepository(EventBookingDbContext context)
    : IBookingAppointmentRepository
{
    /// <summary>Adds one appointment to the current unit of work.</summary>
    /// <param name="appointment">The appointment to track.</param>
    public void Add(BookingAppointment appointment) => context.BookingAppointments.Add(appointment);

    /// <summary>Finds lifecycle owner identifiers only when record and trusted type both match.</summary>
    /// <param name="id">The stable appointment-record identifier.</param>
    /// <param name="appointmentTypeId">The trusted appointment-type scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The lifecycle owner identifiers, or null when out of scope.</returns>
    public Task<BookingAppointmentLocator?> FindLocatorInScopeAsync(
        Guid id,
        Guid appointmentTypeId,
        CancellationToken cancellationToken) =>
        context.BookingAppointments
            .AsNoTracking()
            .Where(value => value.Id == id && value.AppointmentTypeId == appointmentTypeId)
            .Join(
                context.Bookings,
                value => value.BookingId,
                booking => booking.Id,
                (value, booking) => new BookingAppointmentLocator(
                    booking.CandidateId,
                    booking.RecoveryOfBookingId ?? booking.Id,
                    booking.Id,
                    booking.ConfirmedSlotId,
                    value.AppointmentTypeId))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Locks and returns one appointment only when record and trusted type both match.</summary>
    /// <param name="id">The stable appointment-record identifier.</param>
    /// <param name="appointmentTypeId">The trusted appointment-type scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The locked appointment, or null when out of scope.</returns>
    public Task<BookingAppointment?> LockForUpdateAsync(
        Guid id,
        Guid appointmentTypeId,
        CancellationToken cancellationToken) =>
        context.BookingAppointments
            .FromSqlInterpolated(
                $"""
                SELECT * FROM booking_appointment
                WHERE id = {id} AND appointment_type_id = {appointmentTypeId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Lists the immutable Appointment Type snapshot owned by one Booking.</summary>
    public async Task<IReadOnlyList<BookingAppointment>> ListForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken) =>
        await context.BookingAppointments
            .AsNoTracking()
            .Where(value => value.BookingId == bookingId)
            .OrderBy(value => value.AppointmentTypeId)
            .ToListAsync(cancellationToken);

    /// <summary>Lists the snapshots owned by a whole Booking journey in stable ID order.</summary>
    public async Task<IReadOnlyList<BookingAppointment>> ListForBookingsAsync(
        IReadOnlyCollection<Guid> bookingIds,
        CancellationToken cancellationToken) =>
        await context.BookingAppointments
            .AsNoTracking()
            .Where(value => bookingIds.Contains(value.BookingId))
            .OrderBy(value => value.Id)
            .ToListAsync(cancellationToken);

    /// <summary>Locks every appointment for one Booking in stable ID order.</summary>
    public async Task<IReadOnlyList<BookingAppointment>> LockForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken) =>
        await context.BookingAppointments
            .FromSqlInterpolated(
                $"""
                SELECT * FROM booking_appointment
                WHERE booking_id = {bookingId}
                ORDER BY id FOR UPDATE
                """)
            .ToListAsync(cancellationToken);
}
`````

## src/EventBooking.Infrastructure/Persistence/Repositories/EmployeeGroupRepository.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Repositories/EmployeeGroupRepository.cs","encoding":"utf8","sha256":"263ed4f559421bbcd4fcdba10d99d51e469bf4738cac4f208b4faa9eceafe56a","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.EmployeeGroups;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Repositories;

/// <summary>Reads change-controlled Employee Group reference data with complete mappings.</summary>
public sealed class EmployeeGroupRepository(EventBookingDbContext context) : IEmployeeGroupRepository
{
    /// <summary>Gets a group by stable identifier, including inactive or inconsistent rows.</summary>
    public Task<EmployeeGroup?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.EmployeeGroups
            .Include(group => group.Requirements)
            .SingleOrDefaultAsync(group => group.Id == id, cancellationToken);

    /// <summary>Gets a group from a trimmed case-insensitive canonical-code input.</summary>
    public Task<EmployeeGroup?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return context.EmployeeGroups
            .Include(group => group.Requirements)
            .SingleOrDefaultAsync(group => group.Code == normalized, cancellationToken);
    }

    /// <summary>Lists active mapped groups ordered by display name.</summary>
    public async Task<IReadOnlyList<EmployeeGroup>> ListActiveAsync(CancellationToken cancellationToken) =>
        await context.EmployeeGroups
            .Include(group => group.Requirements)
            .Where(group => group.IsActive && group.Requirements.Any())
            .OrderBy(group => group.Name)
            .ToListAsync(cancellationToken);
}
`````

## src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs","encoding":"utf8","sha256":"449d958266bc5dccbb103f36b85ee91300bcb0e4461fc2494c6fb2df0b98f059","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Settings;
using EventBooking.Domain.Slots;
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
public sealed class SlotProposalRepository(EventBookingDbContext context) : ISlotProposalRepository
{
    public Task<SlotProposal?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.SlotProposals
            .Include(p => p.Acceptances)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <summary>Locks the proposal row and then loads its current acceptance collection.</summary>
    public async Task<SlotProposal?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var proposal = (await context.SlotProposals
            .FromSqlInterpolated($"SELECT * FROM slot_proposal WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken))
            .SingleOrDefault();

        if (proposal is not null)
        {
            await context.Entry(proposal).Collection(item => item.Acceptances).LoadAsync(cancellationToken);
        }

        return proposal;
    }

    public async Task<IReadOnlyList<SlotProposal>> ListOpenAsync(CancellationToken cancellationToken) =>
        await context.SlotProposals
            .Include(p => p.Acceptances)
            .Where(p => p.Status == SlotProposalStatus.Open)
            .ToListAsync(cancellationToken);

    public void Add(SlotProposal proposal) => context.SlotProposals.Add(proposal);
}

public sealed class ConfirmedSlotRepository(EventBookingDbContext context) : IConfirmedSlotRepository
{
    public Task<ConfirmedSlot?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.ConfirmedSlots
            .Include(s => s.Capacities)
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<ConfirmedSlot?> LockForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var rows = await context.ConfirmedSlots
            .FromSqlInterpolated(
                $"SELECT * FROM confirmed_slot WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken);

        var slot = rows.SingleOrDefault();
        if (slot is not null)
        {
            await context.Entry(slot).Collection(item => item.Capacities).LoadAsync(cancellationToken);
        }

        return slot;
    }

    public async Task<IReadOnlyList<ConfirmedSlot>> ListActiveAsync(
        DateOnly onOrAfter,
        CancellationToken cancellationToken) =>
        await context.ConfirmedSlots
            .Include(s => s.Capacities)
            .Where(s => s.Status == ConfirmedSlotStatus.Active && s.Window.Date >= onOrAfter)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ConfirmedSlot>> ListAllAsync(
        CancellationToken cancellationToken) =>
        await context.ConfirmedSlots
            .Include(s => s.Capacities)
            .ToListAsync(cancellationToken);

    public void Add(ConfirmedSlot slot) => context.ConfirmedSlots.Add(slot);
}

/// <summary>Persists candidates and exposes the lifecycle root row lock.</summary>
public sealed class CandidateRepository(EventBookingDbContext context) : ICandidateRepository
{
    public Task<Candidate?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Candidates
            .Include(c => c.Requirements)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    /// <summary>Locks the candidate row and then loads the requirements needed by lifecycle handlers.</summary>
    public async Task<Candidate?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var candidate = (await context.Candidates
            .FromSqlInterpolated($"SELECT * FROM candidate WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken))
            .SingleOrDefault();

        if (candidate is not null)
        {
            await context.Entry(candidate).Collection(item => item.Requirements).LoadAsync(cancellationToken);
        }

        return candidate;
    }

    public Task<Candidate?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        context.Candidates
            .Include(c => c.Requirements)
            .SingleOrDefaultAsync(c => c.Email == email, cancellationToken);

    public async Task<IReadOnlyList<Candidate>> ListAsync(
        CandidateStatus? status,
        CancellationToken cancellationToken) =>
        await context.Candidates
            .Include(c => c.Requirements)
            .Where(c => status == null || c.Status == status)
            .ToListAsync(cancellationToken);

    public void Add(Candidate candidate) => context.Candidates.Add(candidate);

    public void Remove(Candidate candidate) => context.Candidates.Remove(candidate);
}

/// <summary>Persists invite rows and their option collections, including lifecycle locks.</summary>
public sealed class InviteRepository(EventBookingDbContext context) : IInviteRepository
{
    public Task<Invite?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Invites
            .Include(i => i.Options)
            .Include(i => i.Requirements)
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

    /// <summary>Locks the identified invite and loads its offered slot IDs.</summary>
    public async Task<Invite?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        await LockAndLoadOptionsAsync(
            context.Invites.FromSqlInterpolated($"SELECT * FROM invite WHERE id = {id} FOR UPDATE"),
            cancellationToken);

    public Task<Invite?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken) =>
        context.Invites
            .Include(i => i.Options)
            .Include(i => i.Requirements)
            .SingleOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

    public async Task<Invite?> LockByTokenHashForUpdateAsync(
        string tokenHash,
        CancellationToken cancellationToken) =>
        await LockAndLoadOptionsAsync(
            context.Invites.FromSqlInterpolated(
                $"SELECT * FROM invite WHERE token_hash = {tokenHash} FOR UPDATE"),
            cancellationToken);

    /// <summary>Locks the candidate's current pending invite and loads its offered slot IDs.</summary>
    public async Task<Invite?> LockPendingForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken) =>
        await LockAndLoadOptionsAsync(
            context.Invites.FromSqlInterpolated(
                $"SELECT * FROM invite WHERE candidate_id = {candidateId} AND status = {(int)InviteStatus.Pending} FOR UPDATE"),
            cancellationToken);

    public Task<Invite?> GetPendingForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken) =>
        context.Invites
            .Include(i => i.Options)
            .Include(i => i.Requirements)
            .SingleOrDefaultAsync(
                i => i.CandidateId == candidateId && i.Status == InviteStatus.Pending,
                cancellationToken);

    /// <summary>Locks the candidate's pending initial invite and loads its offered slot IDs.</summary>
    public async Task<Invite?> LockPendingInitialForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken) =>
        await LockAndLoadOptionsAsync(
            context.Invites.FromSqlInterpolated(
                $"SELECT * FROM invite WHERE candidate_id = {candidateId} AND status = {(int)InviteStatus.Pending} AND recovery_of_booking_id IS NULL FOR UPDATE"),
            cancellationToken);

    /// <summary>Locks every pending invite for the candidate in ID order with slots loaded.</summary>
    public async Task<IReadOnlyList<Invite>> LockPendingListForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        var pending = await context.Invites.FromSqlInterpolated(
                $"SELECT * FROM invite WHERE candidate_id = {candidateId} AND status = {(int)InviteStatus.Pending} ORDER BY id FOR UPDATE")
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

/// <summary>Persists hash-only candidate email delivery attempts and their safe retry context.</summary>
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
    public async Task<EmailLog?> LockLatestForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        var rows = await context.EmailLogs
            .FromSqlInterpolated($"""
                SELECT * FROM email_log
                WHERE candidate_id = {candidateId}
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

    /// <summary>Reads the latest row for one candidate and template for cancellation recovery.</summary>
    public Task<EmailLog?> GetLatestForCandidateAsync(
        Guid candidateId,
        EmailTemplate template,
        CancellationToken cancellationToken) =>
        context.EmailLogs
            .AsNoTracking()
            .Where(e => e.CandidateId == candidateId && e.TemplateName == template)
            .OrderBy(e => e.Status == EmailStatus.Failed || e.Status == EmailStatus.Pending ? 0 : 1)
            .ThenByDescending(e => e.SentAt)
            .ThenByDescending(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Stages a delivery row on the current context transaction.</summary>
    public void Add(EmailLog delivery) => context.EmailLogs.Add(delivery);
}

/// <summary>Persists booking rows and exposes token and candidate lifecycle locks.</summary>
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

    public Task<Booking?> GetByManageTokenHashAsync(
        string manageTokenHash,
        CancellationToken cancellationToken) =>
        context.Bookings.SingleOrDefaultAsync(
            b => b.ManageTokenHash == manageTokenHash,
            cancellationToken);

    public Task<Guid?> GetConfirmedSlotIdByManageTokenHashAsync(
        string manageTokenHash,
        CancellationToken cancellationToken) =>
        context.Bookings
            .AsNoTracking()
            .Where(b => b.ManageTokenHash == manageTokenHash)
            .Select(b => (Guid?)b.ConfirmedSlotId)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Reads only the candidate ID used to establish cancellation lock order.</summary>
    public Task<Guid?> GetCandidateIdByManageTokenHashAsync(
        string manageTokenHash,
        CancellationToken cancellationToken) =>
        context.Bookings
            .AsNoTracking()
            .Where(b => b.ManageTokenHash == manageTokenHash)
            .Select(b => (Guid?)b.CandidateId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<Booking?> LockByManageTokenHashForUpdateAsync(
        string manageTokenHash,
        CancellationToken cancellationToken)
    {
        var rows = await context.Bookings
            .FromSqlInterpolated(
                $"SELECT * FROM booking WHERE manage_token_hash = {manageTokenHash} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return rows.SingleOrDefault();
    }

    /// <inheritdoc/>
    public async Task<Booking?> LockByIdForCandidateAsync(
        Guid bookingId,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        var rows = await context.Bookings
            .FromSqlInterpolated(
                $"SELECT * FROM booking WHERE id = {bookingId} AND candidate_id = {candidateId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return rows.SingleOrDefault();
    }

    /// <summary>Locks the candidate's active booking, if one remains after the prior locks.</summary>
    public async Task<Booking?> LockActiveForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken) =>
        await LockActiveOriginalForCandidateAsync(candidateId, cancellationToken);

    /// <summary>Locks the candidate's active original booking for lifecycle serialization.</summary>
    public async Task<Booking?> LockActiveOriginalForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        var rows = await context.Bookings
            .FromSqlInterpolated(
                $"SELECT * FROM booking WHERE candidate_id = {candidateId} AND status = {(int)BookingStatus.Active} AND recovery_of_booking_id IS NULL FOR UPDATE")
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

    public Task<Booking?> GetActiveForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken) =>
        context.Bookings.SingleOrDefaultAsync(
            b => b.CandidateId == candidateId && b.Status == BookingStatus.Active,
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ListActiveCandidateIdsForSlotAsync(
        Guid confirmedSlotId,
        CancellationToken cancellationToken) =>
        await context.Bookings
            .AsNoTracking()
            .Where(b => b.ConfirmedSlotId == confirmedSlotId && b.Status == BookingStatus.Active)
            .Select(b => b.CandidateId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Booking>> ListActiveForSlotAsync(
        Guid confirmedSlotId,
        CancellationToken cancellationToken) =>
        await context.Bookings
            .Where(b =>
                b.ConfirmedSlotId == confirmedSlotId && b.Status == BookingStatus.Active)
            .ToListAsync(cancellationToken);

    public void Add(Booking booking) => context.Bookings.Add(booking);
}
`````

## src/EventBooking.Infrastructure/Persistence/Repositories/SlotCapacityRepository.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Repositories/SlotCapacityRepository.cs","encoding":"utf8","sha256":"80ef8d2dff94d2ee336eff8a6606ba76f82abc7d3e5b70c8ed5dce29103ea871","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Slots;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Repositories;

/// <summary>
/// The only raw SQL in the system, and the reason overbooking cannot happen. Read the three notes
/// in Task 47 of the plan before changing a character of the query below.
/// </summary>
public sealed class SlotCapacityRepository(EventBookingDbContext context) : ISlotCapacityRepository
{
    public async Task<IReadOnlyList<SlotCapacity>> LockForUpdateAsync(
        Guid confirmedSlotId,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        CancellationToken cancellationToken)
    {
        // Sorted so that every caller takes the locks in the same order and two concurrent
        // confirmations for different type combinations cannot deadlock.
        var ordered = appointmentTypeIds.OrderBy(id => id).ToArray();

        return await context.SlotCapacities
            .FromSql(
                $"""
                 SELECT confirmed_slot_id, appointment_type_id, total_headcount, remaining_capacity
                 FROM slot_capacity
                 WHERE confirmed_slot_id = {confirmedSlotId}
                   AND appointment_type_id = ANY({ordered})
                 ORDER BY appointment_type_id
                 FOR UPDATE
                 """)
            .ToListAsync(cancellationToken);
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/Repositories/StaffAccessProfileRepository.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Repositories/StaffAccessProfileRepository.cs","encoding":"utf8","sha256":"b8475521b63c464b39c647d7c5686d4884909795ae511560ece2c8245b67a8c4","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Repositories;

public sealed class StaffAccessProfileRepository(EventBookingDbContext context)
    : IStaffAccessProfileRepository
{
    public Task<StaffAccessProfile?> GetAsync(
        Guid staffUserId,
        CancellationToken cancellationToken) =>
        context.StaffAccessProfiles.AsNoTracking().SingleOrDefaultAsync(
            profile => profile.StaffUserId == staffUserId,
            cancellationToken);

    public async Task<IReadOnlyList<StaffAccessProfile>> ListAsync(
        CancellationToken cancellationToken) =>
        await context.StaffAccessProfiles
            .AsNoTracking()
            .OrderBy(profile => profile.StaffUserId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StaffAccessProfile>> LockAllAsync(
        CancellationToken cancellationToken)
    {
        // Serialises profile-set decisions even when a role/type currently has no row to lock.
        // The key is a fixed application constant reserved for staff-access administration.
        await context.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(710071)", cancellationToken);

        return await context.StaffAccessProfiles
            .FromSqlRaw("SELECT * FROM staff_access_profile ORDER BY staff_user_id FOR UPDATE")
            .ToListAsync(cancellationToken);
    }

    public void Add(StaffAccessProfile profile) => context.StaffAccessProfiles.Add(profile);

    public void Remove(StaffAccessProfile profile) => context.StaffAccessProfiles.Remove(profile);
}
`````

## src/EventBooking.Infrastructure/Persistence/Repositories/StaffIdentityRepository.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Repositories/StaffIdentityRepository.cs","encoding":"utf8","sha256":"d9d0782b53e626877527061f1c2113b6aa0f46e838d0dec0bca492a8aa60ec33","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Repositories;

/// <summary>Persists and resolves identity-provider pairs in PostgreSQL.</summary>
public sealed class StaffIdentityRepository(EventBookingDbContext context)
    : IStaffIdentityRepository
{
    /// <inheritdoc />
    public Task<StaffIdentity?> GetByStaffIdAsync(
        StaffId staffId,
        CancellationToken cancellationToken) =>
        context.StaffIdentities.AsNoTracking().SingleOrDefaultAsync(
            identity => identity.StaffId == staffId,
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<StaffIdentity>> ListAsync(
        CancellationToken cancellationToken) =>
        await context.StaffIdentities
            .AsNoTracking()
            .OrderBy(identity => identity.StaffUserId)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task UpsertAsync(
        Guid staffUserId,
        StaffId staffId,
        string? displayName,
        DateTimeOffset lastSeenAt,
        CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO staff_identity (staff_user_id, staff_id, display_name, last_seen_at)
             VALUES ({staffUserId}, {staffId.Value}, {displayName}, {lastSeenAt})
             ON CONFLICT (staff_user_id) DO UPDATE
             SET staff_id = EXCLUDED.staff_id,
                 display_name = EXCLUDED.display_name,
                 last_seen_at = EXCLUDED.last_seen_at
             """,
            cancellationToken);
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/StatusStampingInterceptor.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/StatusStampingInterceptor.cs","encoding":"utf8","sha256":"1fea58d9df85596913c91e0130b8dc9089d81636c548680b16f07f6c6c9ea43d","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Candidates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EventBooking.Infrastructure.Persistence;

/// <summary>
/// Persists a candidate's status timestamp in the same save as the status change.
/// </summary>
public sealed class StatusStampingInterceptor(IClock clock) : SaveChangesInterceptor
{
    public const string ShadowProperty = "StatusChangedAt";

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<Candidate>())
        {
            if (entry.State == EntityState.Added ||
                (entry.State == EntityState.Modified && entry.Property(c => c.Status).IsModified))
            {
                entry.Property<DateTimeOffset>(ShadowProperty).CurrentValue = clock.UtcNow;
            }
        }
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/UnitOfWork.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/UnitOfWork.cs","encoding":"utf8","sha256":"23646c1353020c981c30a23454f394e92ab28b7341b107d538df2402cd788f26","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace EventBooking.Infrastructure.Persistence;

/// <summary>Commits one DbContext unit of work and maps uniqueness backstops to application errors.</summary>
public sealed class UnitOfWork(EventBookingDbContext context) : IUnitOfWork
{
    /// <summary>Saves pending changes or translates a PostgreSQL uniqueness violation.</summary>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new UniqueConstraintViolationException(exception);
        }
    }

    /// <summary>Begins the outer transaction or joins the transaction already owned by the context.</summary>
    public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        // A handler that calls a service which also opens a transaction must not start a second
        // one; the outermost caller owns the commit.
        if (context.Database.CurrentTransaction is not null)
        {
            return new JoinedScope();
        }

        var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        return new EfTransactionScope(transaction);
    }

    private sealed class EfTransactionScope(IDbContextTransaction transaction) : ITransactionScope
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken) =>
            transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }

    private sealed class JoinedScope : ITransactionScope
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
`````

## src/EventBooking.Infrastructure/Time/HeadOfficeOptions.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Time/HeadOfficeOptions.cs","encoding":"utf8","sha256":"a05cc9d3812622a022bd4c7a4aedb7baabe80a235362940ff8e18243c4abde7c","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Infrastructure.Time;

/// <summary>Single site, single zone. An identifier the host operating system recognises.</summary>
public sealed record HeadOfficeOptions(string TimeZoneId);
`````

## src/EventBooking.Infrastructure/Time/SystemClock.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Time/SystemClock.cs","encoding":"utf8","sha256":"d1b22f8467eba89463da711b14dc4e3acaeb7faa2183b12ce4b2b4033362c9ae","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;

namespace EventBooking.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    private readonly TimeZoneInfo _headOffice;

    public SystemClock(HeadOfficeOptions options)
    {
        // Resolved once, at startup: a bad configuration value should stop the host coming up
        // rather than fail the first time somebody reads the date.
        _headOffice = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);
    }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc/>
    public DateTimeOffset NowAtHeadOffice => TimeZoneInfo.ConvertTime(UtcNow, _headOffice);

    public DateOnly TodayAtHeadOffice => DateOnly.FromDateTime(NowAtHeadOffice.DateTime);

    public DateOnly DateAtHeadOffice(DateTimeOffset instant) => LocalDateOf(instant, _headOffice);

    /// <inheritdoc/>
    public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, _headOffice);

    public static DateOnly LocalDateOf(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
}
`````

## src/EventBooking.Infrastructure/Tokens/HmacTokenService.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Tokens/HmacTokenService.cs","encoding":"utf8","sha256":"eab11b15fe7428bd6fb255926c3018ee4db54857233f517ca53dade0ffe99eaf","parts":1,"part":1} -->

`````csharp
using System.Security.Cryptography;
using System.Text;
using EventBooking.Application.Abstractions;

namespace EventBooking.Infrastructure.Tokens;

public sealed class HmacTokenService : ITokenService
{
    private const int MinimumKeyLength = 32;

    /// <summary>
    /// The placeholder signing key shipped in appsettings.json. It passes the length check,
    /// so it is rejected by value: anyone who can read this repository could forge tokens with it.
    /// </summary>
    private const string PlaceholderSigningKey = "replace-this-with-a-real-secret-of-at-least-32-characters";
    private const int NonceBytes = 16;
    private const int GuidLength = 32;
    private const int NonceLength = 22;
    private const int SignatureBytes = 32;
    private const int SignatureLength = 43;
    private const int PayloadLength = GuidLength + 1 + NonceLength;
    private const int TokenLength = PayloadLength + 1 + SignatureLength;

    private readonly byte[] _key;

    public HmacTokenService(TokenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.SigningKey, nameof(options.SigningKey));

        if (options.SigningKey.Length < MinimumKeyLength)
        {
            throw new ArgumentException(
                $"The token signing key must be at least {MinimumKeyLength} characters.",
                nameof(options));
        }

        if (string.Equals(options.SigningKey, PlaceholderSigningKey, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The token signing key is still the placeholder from appsettings.json. Configure Tokens:SigningKey.",
                nameof(options));
        }

        _key = Encoding.UTF8.GetBytes(options.SigningKey);
    }

    public IssuedToken Issue(Guid entityId)
    {
        var nonce = ToBase64Url(RandomNumberGenerator.GetBytes(NonceBytes));
        var payload = $"{entityId:N}.{nonce}";
        var token = $"{payload}.{Sign(payload)}";

        return new IssuedToken(token, Hash(token));
    }

    public bool TryRead(string? token, out Guid entityId)
    {
        entityId = Guid.Empty;

        if (token is null || token.Length != TokenLength)
        {
            return false;
        }

        if (token[GuidLength] != '.' || token[PayloadLength] != '.')
        {
            return false;
        }

        var identifier = token[..GuidLength];
        if (!Guid.TryParseExact(identifier, "N", out var id) ||
            !string.Equals(identifier, id.ToString("N"), StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryDecodeBase64Url(token.AsSpan(GuidLength + 1, NonceLength), NonceBytes, out _) ||
            !TryDecodeBase64Url(token.AsSpan(PayloadLength + 1, SignatureLength), SignatureBytes, out var supplied))
        {
            return false;
        }

        var expected = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(token[..PayloadLength]));

        // Constant time: a timing difference here would leak how much of a guess was right.
        if (!CryptographicOperations.FixedTimeEquals(expected, supplied))
        {
            return false;
        }

        entityId = id;
        return true;
    }

    public string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private string Sign(string payload) =>
        ToBase64Url(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload)));

    /// <summary>Base64 with the two characters that are unsafe in a URL replaced, and no padding.</summary>
    private static string ToBase64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryDecodeBase64Url(ReadOnlySpan<char> value, int expectedByteLength, out byte[] decoded)
    {
        decoded = Array.Empty<byte>();

        if (!IsBase64Url(value))
        {
            return false;
        }

        try
        {
            var base64 = value.ToString().Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
            decoded = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return false;
        }

        return decoded.Length == expectedByteLength &&
            value.SequenceEqual(ToBase64Url(decoded).AsSpan());
    }

    private static bool IsBase64UrlCharacter(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_';

    private static bool IsBase64Url(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!IsBase64UrlCharacter(character))
            {
                return false;
            }
        }

        return true;
    }
}
`````

## src/EventBooking.Infrastructure/Tokens/TokenOptions.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Tokens/TokenOptions.cs","encoding":"utf8","sha256":"6c6e74126cb6f2161445a24e7a93fbba4a3bd0a70fa8feea72b590affe03b22b","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Infrastructure.Tokens;

/// <summary>
/// The secret behind every candidate link. Supplied from configuration; never checked in.
/// Rotating it invalidates every outstanding invite and cancel link, which is the intended
/// emergency behaviour.
/// </summary>
public sealed record TokenOptions(string SigningKey)
{
    public override string ToString() => "TokenOptions { SigningKey = [REDACTED] }";
}
`````

## src/EventBooking.Mcp/appsettings.json — 1/1

<!-- port-file: {"path":"src/EventBooking.Mcp/appsettings.json","encoding":"utf8","sha256":"a137a2b011c42fbad7d3665110e8ddb0f87c7e3258b7d47a10e2f52c67a48e64","parts":1,"part":1} -->

`````text
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "EventBooking": "Host=localhost;Database=eventbooking;Username=postgres;Password=postgres"
  },
  "HeadOffice": {
    "TimeZoneId": "Europe/London",
    "Address": "Corporate HQ, 1 Example Street, London"
  },
  "Tokens": {
    "SigningKey": "replace-this-with-a-real-secret-of-at-least-32-characters"
  },
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://localhost:8081/realms/eventbooking",
      "Audience": "eventbooking-web"
    }
  },
  "Email": {
    "Provider": "Smtp",
    "FromAddress": "recruitment@example.com",
    "FromName": "Recruitment Team",
    "Smtp": {
      "Host": "localhost",
      "Port": 1025
    }
  },
  "Portal": {
    "BaseUrl": "https://localhost:5001",
    "CoordinatorContact": "recruitment@example.com"
  }
}
`````

## src/EventBooking.Mcp/appsettings.Local.json — 1/1

<!-- port-file: {"path":"src/EventBooking.Mcp/appsettings.Local.json","encoding":"utf8","sha256":"b42048a31267f7418519ee1ba7a6f5e444a85f6c5d06f8298fb2d9f9e9a838fb","parts":1,"part":1} -->

`````text
{
  "ConnectionStrings": {
    "EventBooking": "Host=localhost;Database=eventbooking;Username=eventbooking_app;Password=eventbooking_local"
  },
  "HeadOffice": {
    "TimeZoneId": "Europe/London",
    "Address": "1 Example Street, London"
  },
  "Tokens": {
    "SigningKey": "a-local-signing-key-that-is-at-least-32-characters"
  },
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://localhost:8081/realms/eventbooking",
      "Audience": "eventbooking-web"
    }
  },
  "Email": {
    "Provider": "Smtp",
    "FromAddress": "recruitment@example.com",
    "FromName": "Recruitment Team",
    "Smtp": {
      "Host": "localhost",
      "Port": 1025
    }
  },
  "Portal": {
    "BaseUrl": "http://localhost:5002",
    "CoordinatorContact": "recruitment@example.com"
  }
}
`````

## src/EventBooking.Mcp/Dockerfile — 1/1

<!-- port-file: {"path":"src/EventBooking.Mcp/Dockerfile","encoding":"utf8","sha256":"f4a30cba534d149d54a6f8404cbbf14d7b67ec4631eb4fb29cd90942348e63a0","parts":1,"part":1} -->

`````text
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/EventBooking.Mcp/EventBooking.Mcp.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "EventBooking.Mcp.dll"]
`````

## src/EventBooking.Mcp/EventBooking.Mcp.csproj — 1/1

<!-- port-file: {"path":"src/EventBooking.Mcp/EventBooking.Mcp.csproj","encoding":"utf8","sha256":"932be4676cfb86d1f2c56ebe9fdbb3fbb7cfd12d50ca226a61d1e98a01e4b887","parts":1,"part":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <!-- EventBooking.Api is referenced for its endpoint/DI code, not hosted here; its own
         appsettings*.json otherwise collides with this project's identically-named files
         at publish time (NETSDK1152). Both files carry the same keys this project reads. -->
    <ErrorOnDuplicatePublishOutputFiles>false</ErrorOnDuplicatePublishOutputFiles>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\EventBooking.Api\EventBooking.Api.csproj" />
    <ProjectReference Include="..\EventBooking.Application\EventBooking.Application.csproj" />
    <ProjectReference Include="..\EventBooking.Infrastructure\EventBooking.Infrastructure.csproj" />
    <ProjectReference Include="..\EventBooking.Api.Auth\EventBooking.Api.Auth.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="ModelContextProtocol.AspNetCore" />
  </ItemGroup>

</Project>
`````
