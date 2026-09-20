# 00b — Vocabulary edits 49 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs — 1/1

<!-- vocabulary-file: {"id":168,"oldPath":"src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs","beforeSha":"449d958266bc5dccbb103f36b85ee91300bcb0e4461fc2494c6fb2df0b98f059","afterSha":"a6359c56dc083d47da83f91464c38c1accf7955260b283a0f3b43a4511db6e8e","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs — 1/1

<!-- vocabulary-file: {"id":168,"oldPath":"src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs","beforeSha":"449d958266bc5dccbb103f36b85ee91300bcb0e4461fc2494c6fb2df0b98f059","afterSha":"a6359c56dc083d47da83f91464c38c1accf7955260b283a0f3b43a4511db6e8e","side":"after","part":1,"parts":1} -->

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
        }

        return proposal;
    }

    public async Task<IReadOnlyList<EventProposal>> ListOpenAsync(CancellationToken cancellationToken) =>
        await context.EventProposals
            .Include(p => p.Acceptances)
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

    public Task<Booking?> GetByManageTokenHashAsync(
        string manageTokenHash,
        CancellationToken cancellationToken) =>
        context.Bookings.SingleOrDefaultAsync(
            b => b.ManageTokenHash == manageTokenHash,
            cancellationToken);

    public Task<Guid?> GetEventIdByManageTokenHashAsync(
        string manageTokenHash,
        CancellationToken cancellationToken) =>
        context.Bookings
            .AsNoTracking()
            .Where(b => b.ManageTokenHash == manageTokenHash)
            .Select(b => (Guid?)b.EventId)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Reads only the attendee ID used to establish cancellation lock order.</summary>
    public Task<Guid?> GetAttendeeIdByManageTokenHashAsync(
        string manageTokenHash,
        CancellationToken cancellationToken) =>
        context.Bookings
            .AsNoTracking()
            .Where(b => b.ManageTokenHash == manageTokenHash)
            .Select(b => (Guid?)b.AttendeeId)
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

## before — src/EventBooking.Infrastructure/Persistence/Repositories/SlotCapacityRepository.cs — 1/1

<!-- vocabulary-file: {"id":169,"oldPath":"src/EventBooking.Infrastructure/Persistence/Repositories/SlotCapacityRepository.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Repositories/EventCapacityRepository.cs","beforeSha":"80ef8d2dff94d2ee336eff8a6606ba76f82abc7d3e5b70c8ed5dce29103ea871","afterSha":"cf5f2e3a83f6fb493a95e7d427d644a7bb5f9dd592ba802be0ed2f225bcd4446","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Infrastructure/Persistence/Repositories/EventCapacityRepository.cs — 1/1

<!-- vocabulary-file: {"id":169,"oldPath":"src/EventBooking.Infrastructure/Persistence/Repositories/SlotCapacityRepository.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Repositories/EventCapacityRepository.cs","beforeSha":"80ef8d2dff94d2ee336eff8a6606ba76f82abc7d3e5b70c8ed5dce29103ea871","afterSha":"cf5f2e3a83f6fb493a95e7d427d644a7bb5f9dd592ba802be0ed2f225bcd4446","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Repositories;

/// <summary>
/// The only raw SQL in the system, and the reason overbooking cannot happen. Read the three notes
/// in Task 47 of the plan before changing a character of the query below.
/// </summary>
public sealed class EventCapacityRepository(EventBookingDbContext context) : IEventCapacityRepository
{
    public async Task<IReadOnlyList<EventCapacity>> LockForUpdateAsync(
        Guid eventId,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        CancellationToken cancellationToken)
    {
        // Sorted so that every caller takes the locks in the same order and two concurrent
        // confirmations for different type combinations cannot deadlock.
        var ordered = appointmentTypeIds.OrderBy(id => id).ToArray();

        return await context.EventCapacities
            .FromSql(
                $"""
                 SELECT event_id, appointment_type_id, total_headcount, remaining_capacity
                 FROM event_capacity
                 WHERE event_id = {eventId}
                   AND appointment_type_id = ANY({ordered})
                 ORDER BY appointment_type_id
                 FOR UPDATE
                 """)
            .ToListAsync(cancellationToken);
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/StatusStampingInterceptor.cs — 1/1

<!-- vocabulary-file: {"id":170,"oldPath":"src/EventBooking.Infrastructure/Persistence/StatusStampingInterceptor.cs","newPath":"src/EventBooking.Infrastructure/Persistence/StatusStampingInterceptor.cs","beforeSha":"1fea58d9df85596913c91e0130b8dc9089d81636c548680b16f07f6c6c9ea43d","afterSha":"da364cd16a43547e8e35a254711763d8305896238551e7b9183d7fe25910126a","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Infrastructure/Persistence/StatusStampingInterceptor.cs — 1/1

<!-- vocabulary-file: {"id":170,"oldPath":"src/EventBooking.Infrastructure/Persistence/StatusStampingInterceptor.cs","newPath":"src/EventBooking.Infrastructure/Persistence/StatusStampingInterceptor.cs","beforeSha":"1fea58d9df85596913c91e0130b8dc9089d81636c548680b16f07f6c6c9ea43d","afterSha":"da364cd16a43547e8e35a254711763d8305896238551e7b9183d7fe25910126a","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Attendees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EventBooking.Infrastructure.Persistence;

/// <summary>
/// Persists a attendee's status timestamp in the same save as the status change.
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

        foreach (var entry in context.ChangeTracker.Entries<Attendee>())
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

## before — src/EventBooking.Infrastructure/Time/HeadOfficeOptions.cs — 1/1

<!-- vocabulary-file: {"id":171,"oldPath":"src/EventBooking.Infrastructure/Time/HeadOfficeOptions.cs","newPath":"src/EventBooking.Infrastructure/Time/TransitionalLocationOptions.cs","beforeSha":"a05cc9d3812622a022bd4c7a4aedb7baabe80a235362940ff8e18243c4abde7c","afterSha":"0044d6db9a5d1a5ae505e8f5ea1d9d1bd42ede7408e7d7cae369bf8284d4fb8f","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Infrastructure.Time;

/// <summary>Single site, single zone. An identifier the host operating system recognises.</summary>
public sealed record HeadOfficeOptions(string TimeZoneId);
`````

## after — src/EventBooking.Infrastructure/Time/TransitionalLocationOptions.cs — 1/1

<!-- vocabulary-file: {"id":171,"oldPath":"src/EventBooking.Infrastructure/Time/HeadOfficeOptions.cs","newPath":"src/EventBooking.Infrastructure/Time/TransitionalLocationOptions.cs","beforeSha":"a05cc9d3812622a022bd4c7a4aedb7baabe80a235362940ff8e18243c4abde7c","afterSha":"0044d6db9a5d1a5ae505e8f5ea1d9d1bd42ede7408e7d7cae369bf8284d4fb8f","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Infrastructure.Time;

/// <summary>Single site, single zone. An identifier the host operating system recognises.</summary>
public sealed record TransitionalLocationOptions(string TimeZoneId);
`````

## before — src/EventBooking.Infrastructure/Time/SystemClock.cs — 1/1

<!-- vocabulary-file: {"id":172,"oldPath":"src/EventBooking.Infrastructure/Time/SystemClock.cs","newPath":"src/EventBooking.Infrastructure/Time/SystemClock.cs","beforeSha":"d1b22f8467eba89463da711b14dc4e3acaeb7faa2183b12ce4b2b4033362c9ae","afterSha":"9b0c6876b720fc8bb6b42618d7a12bfe5a025b1ba30a50fab63005aa908de176","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Infrastructure/Time/SystemClock.cs — 1/1

<!-- vocabulary-file: {"id":172,"oldPath":"src/EventBooking.Infrastructure/Time/SystemClock.cs","newPath":"src/EventBooking.Infrastructure/Time/SystemClock.cs","beforeSha":"d1b22f8467eba89463da711b14dc4e3acaeb7faa2183b12ce4b2b4033362c9ae","afterSha":"9b0c6876b720fc8bb6b42618d7a12bfe5a025b1ba30a50fab63005aa908de176","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;

namespace EventBooking.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    private readonly TimeZoneInfo _transitionalLocation;

    public SystemClock(TransitionalLocationOptions options)
    {
        // Resolved once, at startup: a bad configuration value should stop the host coming up
        // rather than fail the first time somebody reads the date.
        _transitionalLocation = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);
    }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc/>
    public DateTimeOffset NowAtTransitionalLocation => TimeZoneInfo.ConvertTime(UtcNow, _transitionalLocation);

    public DateOnly TodayAtTransitionalLocation => DateOnly.FromDateTime(NowAtTransitionalLocation.DateTime);

    public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => LocalDateOf(instant, _transitionalLocation);

    /// <inheritdoc/>
    public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, _transitionalLocation);

    public static DateOnly LocalDateOf(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
}
`````

## before — src/EventBooking.Infrastructure/Tokens/TokenOptions.cs — 1/1

<!-- vocabulary-file: {"id":173,"oldPath":"src/EventBooking.Infrastructure/Tokens/TokenOptions.cs","newPath":"src/EventBooking.Infrastructure/Tokens/TokenOptions.cs","beforeSha":"6c6e74126cb6f2161445a24e7a93fbba4a3bd0a70fa8feea72b590affe03b22b","afterSha":"2c65c402e2c386ac6220a6d9f22b60543c99257f4d48a58f43d5f0aded743feb","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Infrastructure/Tokens/TokenOptions.cs — 1/1

<!-- vocabulary-file: {"id":173,"oldPath":"src/EventBooking.Infrastructure/Tokens/TokenOptions.cs","newPath":"src/EventBooking.Infrastructure/Tokens/TokenOptions.cs","beforeSha":"6c6e74126cb6f2161445a24e7a93fbba4a3bd0a70fa8feea72b590affe03b22b","afterSha":"2c65c402e2c386ac6220a6d9f22b60543c99257f4d48a58f43d5f0aded743feb","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Infrastructure.Tokens;

/// <summary>
/// The secret behind every attendee link. Supplied from configuration; never checked in.
/// Rotating it invalidates every outstanding invite and cancel link, which is the intended
/// emergency behaviour.
/// </summary>
public sealed record TokenOptions(string SigningKey)
{
    public override string ToString() => "TokenOptions { SigningKey = [REDACTED] }";
}
`````

## before — src/EventBooking.Mcp/Program.cs — 1/1

<!-- vocabulary-file: {"id":174,"oldPath":"src/EventBooking.Mcp/Program.cs","newPath":"src/EventBooking.Mcp/Program.cs","beforeSha":"2aa6fc371014e561b87920d73f493887ba8c7a36faf6685dd8a0f1be7a2c8fe0","afterSha":"33eca6cc0a5aeaafa0ede6dc95100a616e2f47f05b554c86c7c7b2d642ad9316","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Api;
using EventBooking.Api.Auth;
using EventBooking.Application;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using EventBooking.Mcp.Tools;

var builder = WebApplication.CreateBuilder(args);


var (connectionString, headOffice, tokens, email, portal) =
    EventBookingConfiguration.Read(builder.Configuration);

builder.Services.AddEventBookingInfrastructure(connectionString, headOffice, tokens);

var smtpHost = builder.Configuration["Email:Smtp:Host"]
    ?? throw new InvalidOperationException(
        "Email:Smtp:Host is required when Email:Provider is Smtp.");
var smtpPort = int.TryParse(builder.Configuration["Email:Smtp:Port"], out var port)
    ? port
    : throw new InvalidOperationException(
        "Email:Smtp:Port must be a valid integer when Email:Provider is Smtp.");

builder.Services.AddLocalEmailTransport(email, new SmtpOptions(smtpHost, smtpPort));

builder.Services.AddEventBookingApplication(portal);
builder.Services.AddEventBookingAuthentication(builder.Configuration);

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<SlotTools>()
    .WithTools<CandidateTools>()
    .WithTools<AdminTools>()
    .WithTools<OperationsTools>();

var app = builder.Build();

app.UseAuthentication();
app.UseMiddleware<StaffIdentityRecorder>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

// Same bearer tokens and the same staff policy as the API: every tool call runs
// as the signed-in staff identity, and each handler re-checks its capability.
app.MapMcp("/mcp").RequireAuthorization(AuthenticationExtensions.StaffPolicy);

app.Run();

/// <summary>Named so the integration test factory can start this host.</summary>
public partial class Program;
`````

## after — src/EventBooking.Mcp/Program.cs — 1/1

<!-- vocabulary-file: {"id":174,"oldPath":"src/EventBooking.Mcp/Program.cs","newPath":"src/EventBooking.Mcp/Program.cs","beforeSha":"2aa6fc371014e561b87920d73f493887ba8c7a36faf6685dd8a0f1be7a2c8fe0","afterSha":"33eca6cc0a5aeaafa0ede6dc95100a616e2f47f05b554c86c7c7b2d642ad9316","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Api;
using EventBooking.Api.Auth;
using EventBooking.Application;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using EventBooking.Mcp.Tools;

var builder = WebApplication.CreateBuilder(args);


var (connectionString, transitionalLocation, tokens, email, portal) =
    EventBookingConfiguration.Read(builder.Configuration);

builder.Services.AddEventBookingInfrastructure(connectionString, transitionalLocation, tokens);

var smtpHost = builder.Configuration["Email:Smtp:Host"]
    ?? throw new InvalidOperationException(
        "Email:Smtp:Host is required when Email:Provider is Smtp.");
var smtpPort = int.TryParse(builder.Configuration["Email:Smtp:Port"], out var port)
    ? port
    : throw new InvalidOperationException(
        "Email:Smtp:Port must be a valid integer when Email:Provider is Smtp.");

builder.Services.AddLocalEmailTransport(email, new SmtpOptions(smtpHost, smtpPort));

builder.Services.AddEventBookingApplication(portal);
builder.Services.AddEventBookingAuthentication(builder.Configuration);

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<EventTools>()
    .WithTools<AttendeeTools>()
    .WithTools<AdminTools>()
    .WithTools<OperationsTools>();

var app = builder.Build();

app.UseAuthentication();
app.UseMiddleware<StaffIdentityRecorder>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

// Same bearer tokens and the same staff policy as the API: every tool call runs
// as the signed-in staff identity, and each handler re-checks its capability.
app.MapMcp("/mcp").RequireAuthorization(AuthenticationExtensions.StaffPolicy);

app.Run();

/// <summary>Named so the integration test factory can start this host.</summary>
public partial class Program;
`````
