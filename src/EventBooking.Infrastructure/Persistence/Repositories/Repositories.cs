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
