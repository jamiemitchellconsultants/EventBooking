# 00b — Vocabulary edits 48 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — src/EventBooking.Infrastructure/Persistence/Queries/AuditQueries.cs — 1/1

<!-- vocabulary-file: {"id":162,"oldPath":"src/EventBooking.Infrastructure/Persistence/Queries/AuditQueries.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Queries/AuditQueries.cs","beforeSha":"95907564895c6ede34d3489cc178379a2d0fbb5983dfb113f50c751f87f8e049","afterSha":"e007227450e6133c1dc6515c9a6de5a3d05f3fc549cdb77e040eca52c94e9a6a","side":"after","part":1,"parts":1} -->

`````csharp
using System.Text;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Audit;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Queries;

/// <summary>Read-side audit queries over the append-only audit log.</summary>
public sealed class AuditQueries(EventBookingDbContext context) : IAuditQueries
{
    private const int MaxPageSize = 200;

    /// <summary>Entries for one audited entity, newest first.</summary>
    public async Task<IReadOnlyList<AuditHistoryRow>> ForEntityAsync(
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken) =>
        await context.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp)
            .Select(a => new AuditHistoryRow(
                a.Timestamp,
                a.EntityType,
                a.EntityId,
                a.Action.ToString(),
                a.ActorType.ToString(),
                a.ActorId,
                a.Details))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Every entry recorded against the attendee record itself and against their invites, bookings,
    /// and booking appointments, newest first.
    /// </summary>
    public async Task<IReadOnlyList<AuditHistoryRow>> ForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var inviteIds = context.Invites
            .Where(i => i.AttendeeId == attendeeId)
            .Select(i => i.Id);

        var bookingIds = context.Bookings
            .Where(b => b.AttendeeId == attendeeId)
            .Select(b => b.Id);

        var bookingAppointmentIds = context.BookingAppointments
            .Where(a => bookingIds.Contains(a.BookingId))
            .Select(a => a.Id);

        return await context.AuditLogs
            .AsNoTracking()
            .Where(a => (a.EntityType == AuditEntityTypes.Attendee && a.EntityId == attendeeId)
                || inviteIds.Contains(a.EntityId)
                || bookingIds.Contains(a.EntityId)
                || bookingAppointmentIds.Contains(a.EntityId))
            .OrderByDescending(a => a.Timestamp)
            .Select(a => new AuditHistoryRow(
                a.Timestamp,
                a.EntityType,
                a.EntityId,
                a.Action.ToString(),
                a.ActorType.ToString(),
                a.ActorId,
                a.Details))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Cross-cutting newest-first keyset-paginated search; malformed cursors restart from newest.</summary>
    public async Task<AuditSearchPage> SearchAsync(
        AuditSearchFilter filter,
        CancellationToken cancellationToken)
    {
        if (filter.AllowedEntityTypes.Count == 0)
        {
            return new AuditSearchPage([], null);
        }

        var pageSize = filter.PageSize <= 0 ? 50 : Math.Min(filter.PageSize, MaxPageSize);
        var allowed = filter.AllowedEntityTypes.ToList();
        var query = context.AuditLogs
            .AsNoTracking()
            .Where(a => allowed.Contains(a.EntityType));

        if (filter.EntityType is not null)
        {
            query = query.Where(a => a.EntityType == filter.EntityType);
        }

        if (filter.From is not null)
        {
            query = query.Where(a => a.Timestamp >= filter.From);
        }

        if (filter.To is not null)
        {
            query = query.Where(a => a.Timestamp <= filter.To);
        }

        if (filter.ActorType is not null)
        {
            query = query.Where(a => a.ActorType.ToString() == filter.ActorType);
        }

        if (filter.Action is not null)
        {
            query = query.Where(a => a.Action.ToString() == filter.Action);
        }

        if (filter.Identifier is not null)
        {
            query = Guid.TryParse(filter.Identifier, out var identifierGuid)
                ? query.Where(a => a.EntityId == identifierGuid || a.ActorId == filter.Identifier)
                : query.Where(a => a.ActorId == filter.Identifier);
        }

        if (TryDecodeCursor(filter.Cursor, out var cursorTimestamp, out var cursorId))
        {
            query = query.Where(a => a.Timestamp < cursorTimestamp
                || (a.Timestamp == cursorTimestamp && a.Id.CompareTo(cursorId) < 0));
        }

        var rows = await query
            .OrderByDescending(a => a.Timestamp)
            .ThenByDescending(a => a.Id)
            .Take(pageSize + 1)
            .Select(a => new
            {
                a.Id,
                a.Timestamp,
                Row = new AuditHistoryRow(
                    a.Timestamp,
                    a.EntityType,
                    a.EntityId,
                    a.Action.ToString(),
                    a.ActorType.ToString(),
                    a.ActorId,
                    a.Details),
            })
            .ToListAsync(cancellationToken);

        var page = rows.Take(pageSize).ToList();
        var nextCursor = rows.Count > pageSize && page.Count > 0
            ? EncodeCursor(page[^1].Timestamp, page[^1].Id)
            : null;

        return new AuditSearchPage([.. page.Select(r => r.Row)], nextCursor);
    }

    private static string EncodeCursor(DateTimeOffset timestamp, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{timestamp.UtcTicks}|{id}"));

    private static bool TryDecodeCursor(string? cursor, out DateTimeOffset timestamp, out Guid id)
    {
        timestamp = default;
        id = Guid.Empty;
        if (string.IsNullOrEmpty(cursor))
        {
            return false;
        }

        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|');
            if (parts.Length != 2 || !long.TryParse(parts[0], out var ticks) || !Guid.TryParse(parts[1], out id))
            {
                return false;
            }

            timestamp = new DateTimeOffset(ticks, TimeSpan.Zero);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Queries/CandidateBookingQueries.cs — 1/1

<!-- vocabulary-file: {"id":163,"oldPath":"src/EventBooking.Infrastructure/Persistence/Queries/CandidateBookingQueries.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Queries/AttendeeBookingQueries.cs","beforeSha":"3898b14c781a4c5192b5a5a37a9b8bfcaefc5afd520f5d0c58fe46c0c4668954","afterSha":"26a36edf4e322ee1e92afd21574f9873b035b5cc5a93eb9c57c903921f813491","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Candidates;
using EventBooking.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Queries;

/// <summary>Projects a candidate's active bookings with their slot windows and nothing else.</summary>
/// <param name="context">The read-only persistence context.</param>
public sealed class CandidateBookingQueries(EventBookingDbContext context) : ICandidateBookingQueries
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CandidateBookingSummary>?> ListActiveForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        var exists = await context.Candidates
            .AsNoTracking()
            .AnyAsync(c => c.Id == candidateId, cancellationToken);
        if (!exists)
        {
            return null;
        }

        var rows = await context.Bookings
            .AsNoTracking()
            .Where(b => b.CandidateId == candidateId && b.Status == BookingStatus.Active)
            .Join(
                context.ConfirmedSlots.AsNoTracking(),
                b => b.ConfirmedSlotId,
                s => s.Id,
                (b, s) => new
                {
                    b.Id,
                    IsOriginal = b.RecoveryOfBookingId == null,
                    s.Window.Date,
                    s.Window.StartTime,
                })
            // The original booking sorts first so the coordinator reads the journey in order.
            .OrderByDescending(r => r.IsOriginal)
            .ThenBy(r => r.Date)
            .ThenBy(r => r.StartTime)
            .ToListAsync(cancellationToken);

        // The window's end is derived by the domain, never stored, so it is computed here rather
        // than projected in SQL.
        return
        [
            .. rows.Select(r => new CandidateBookingSummary(
                r.Id,
                r.IsOriginal,
                r.Date,
                r.StartTime,
                r.StartTime.Add(Domain.Slots.SlotWindow.Duration))),
        ];
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Queries/AttendeeBookingQueries.cs — 1/1

<!-- vocabulary-file: {"id":163,"oldPath":"src/EventBooking.Infrastructure/Persistence/Queries/CandidateBookingQueries.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Queries/AttendeeBookingQueries.cs","beforeSha":"3898b14c781a4c5192b5a5a37a9b8bfcaefc5afd520f5d0c58fe46c0c4668954","afterSha":"26a36edf4e322ee1e92afd21574f9873b035b5cc5a93eb9c57c903921f813491","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Attendees;
using EventBooking.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Queries;

/// <summary>Projects a attendee's active bookings with their event windows and nothing else.</summary>
/// <param name="context">The read-only persistence context.</param>
public sealed class AttendeeBookingQueries(EventBookingDbContext context) : IAttendeeBookingQueries
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendeeBookingSummary>?> ListActiveForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var exists = await context.Attendees
            .AsNoTracking()
            .AnyAsync(c => c.Id == attendeeId, cancellationToken);
        if (!exists)
        {
            return null;
        }

        var rows = await context.Bookings
            .AsNoTracking()
            .Where(b => b.AttendeeId == attendeeId && b.Status == BookingStatus.Active)
            .Join(
                context.Events.AsNoTracking(),
                b => b.EventId,
                s => s.Id,
                (b, s) => new
                {
                    b.Id,
                    IsOriginal = b.RecoveryOfBookingId == null,
                    s.Window.Date,
                    s.Window.StartTime,
                })
            // The original booking sorts first so the coordinator reads the journey in order.
            .OrderByDescending(r => r.IsOriginal)
            .ThenBy(r => r.Date)
            .ThenBy(r => r.StartTime)
            .ToListAsync(cancellationToken);

        // The window's end is derived by the domain, never stored, so it is computed here rather
        // than projected in SQL.
        return
        [
            .. rows.Select(r => new AttendeeBookingSummary(
                r.Id,
                r.IsOriginal,
                r.Date,
                r.StartTime,
                r.StartTime.Add(Domain.Events.EventWindow.Duration))),
        ];
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Queries/CandidateReadinessQueries.cs — 1/1

<!-- vocabulary-file: {"id":164,"oldPath":"src/EventBooking.Infrastructure/Persistence/Queries/CandidateReadinessQueries.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Queries/AttendeeReadinessQueries.cs","beforeSha":"529953feaff3ee1ae1224889b93a9391009fd262496189cda7d4e72ec93f2b98","afterSha":"c3d67115ca345fa5f20449253c124375aa9b2663d564dc3b88587d1cc209aa3d","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Candidates;
using EventBooking.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Queries;

/// <summary>Projects the readiness journey without personal, token, audit, or capacity data.</summary>
/// <param name="context">The read-only persistence context.</param>
public sealed class CandidateReadinessQueries(EventBookingDbContext context) : ICandidateReadinessQueries
{
    /// <inheritdoc />
    public async Task<CandidateReadinessSnapshot?> GetSnapshotAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        var candidate = await context.Candidates
            .AsNoTracking()
            .Where(c => c.Id == candidateId)
            .Select(c => new
            {
                c.Id,
                c.EmployeeGroupId,
                RequirementTypeIds = c.Requirements
                    .Select(r => r.AppointmentTypeId)
                    .ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (candidate is null)
        {
            return null;
        }

        var originalId = await context.Bookings
            .AsNoTracking()
            .Where(b => b.CandidateId == candidateId
                && b.Status == BookingStatus.Active
                && b.RecoveryOfBookingId == null)
            .Select(b => (Guid?)b.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (originalId is null)
        {
            return new CandidateReadinessSnapshot(
                candidate.Id, candidate.EmployeeGroupId, candidate.RequirementTypeIds, null, []);
        }

        var bookings = await context.Bookings
            .AsNoTracking()
            .Where(b => b.Id == originalId.Value || b.RecoveryOfBookingId == originalId.Value)
            .Select(b => new { b.Id, b.Status, b.CreatedAt })
            .ToListAsync(cancellationToken);

        var journeyIds = bookings.Select(b => b.Id).ToList();
        var states = bookings.ToDictionary(b => b.Id);

        var attempts = await context.BookingAppointments
            .AsNoTracking()
            .Where(a => journeyIds.Contains(a.BookingId))
            .Select(a => new { a.Id, a.BookingId, a.AppointmentTypeId, a.Status })
            .ToListAsync(cancellationToken);

        return new CandidateReadinessSnapshot(
            candidate.Id,
            candidate.EmployeeGroupId,
            candidate.RequirementTypeIds,
            originalId,
            attempts
                .Select(a => new CandidateReadinessAttempt(
                    a.Id,
                    a.AppointmentTypeId,
                    a.Status,
                    a.BookingId,
                    states[a.BookingId].Status,
                    states[a.BookingId].CreatedAt))
                .ToList());
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Queries/AttendeeReadinessQueries.cs — 1/1

<!-- vocabulary-file: {"id":164,"oldPath":"src/EventBooking.Infrastructure/Persistence/Queries/CandidateReadinessQueries.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Queries/AttendeeReadinessQueries.cs","beforeSha":"529953feaff3ee1ae1224889b93a9391009fd262496189cda7d4e72ec93f2b98","afterSha":"c3d67115ca345fa5f20449253c124375aa9b2663d564dc3b88587d1cc209aa3d","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Attendees;
using EventBooking.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Queries;

/// <summary>Projects the readiness journey without personal, token, audit, or capacity data.</summary>
/// <param name="context">The read-only persistence context.</param>
public sealed class AttendeeReadinessQueries(EventBookingDbContext context) : IAttendeeReadinessQueries
{
    /// <inheritdoc />
    public async Task<AttendeeReadinessSnapshot?> GetSnapshotAsync(
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var attendee = await context.Attendees
            .AsNoTracking()
            .Where(c => c.Id == attendeeId)
            .Select(c => new
            {
                c.Id,
                c.AttendeeGroupId,
                RequirementTypeIds = c.Requirements
                    .Select(r => r.AppointmentTypeId)
                    .ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (attendee is null)
        {
            return null;
        }

        var originalId = await context.Bookings
            .AsNoTracking()
            .Where(b => b.AttendeeId == attendeeId
                && b.Status == BookingStatus.Active
                && b.RecoveryOfBookingId == null)
            .Select(b => (Guid?)b.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (originalId is null)
        {
            return new AttendeeReadinessSnapshot(
                attendee.Id, attendee.AttendeeGroupId, attendee.RequirementTypeIds, null, []);
        }

        var bookings = await context.Bookings
            .AsNoTracking()
            .Where(b => b.Id == originalId.Value || b.RecoveryOfBookingId == originalId.Value)
            .Select(b => new { b.Id, b.Status, b.CreatedAt })
            .ToListAsync(cancellationToken);

        var journeyIds = bookings.Select(b => b.Id).ToList();
        var states = bookings.ToDictionary(b => b.Id);

        var attempts = await context.BookingAppointments
            .AsNoTracking()
            .Where(a => journeyIds.Contains(a.BookingId))
            .Select(a => new { a.Id, a.BookingId, a.AppointmentTypeId, a.Status })
            .ToListAsync(cancellationToken);

        return new AttendeeReadinessSnapshot(
            attendee.Id,
            attendee.AttendeeGroupId,
            attendee.RequirementTypeIds,
            originalId,
            attempts
                .Select(a => new AttendeeReadinessAttempt(
                    a.Id,
                    a.AppointmentTypeId,
                    a.Status,
                    a.BookingId,
                    states[a.BookingId].Status,
                    states[a.BookingId].CreatedAt))
                .ToList());
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs — 1/1

<!-- vocabulary-file: {"id":165,"oldPath":"src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs","beforeSha":"a11942dd0959aebadb58d7b9933f68a972edbc6d4822157374aa4928ec170c8a","afterSha":"449f91ff62b89c0e75c77605edd2182b4288beaf6b0f34509bdf09006a32c452","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs — 1/1

<!-- vocabulary-file: {"id":165,"oldPath":"src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs","beforeSha":"a11942dd0959aebadb58d7b9933f68a972edbc6d4822157374aa4928ec170c8a","afterSha":"449f91ff62b89c0e75c77605edd2182b4288beaf6b0f34509bdf09006a32c452","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Queries;

public sealed class DashboardQueries(EventBookingDbContext context, IClock clock) : IDashboardQueries
{
    public async Task<IReadOnlyList<AwaitingAvailabilityRow>> AwaitingAvailabilityAsync(
        CancellationToken cancellationToken)
    {
        var today = clock.TodayAtTransitionalLocation;
        var rows = await AttendeeRows(AttendeeStatus.AwaitingAvailability).ToListAsync(cancellationToken);

        return rows
            .Select(row =>
            {
                var since = clock.DateAtTransitionalLocation(row.StatusChangedAt);
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
        var rows = await AttendeeRows(AttendeeStatus.NoResponseNeedsFollowUp).ToListAsync(cancellationToken);

        return rows
            .Select(row => new NoResponseRow(
                row.Id,
                row.Name,
                row.Email,
                row.AppointmentTypeIds.Select(AppointmentTypeIds.CodeOf)
                    .OrderBy(code => code, StringComparer.Ordinal).ToList(),
                clock.DateAtTransitionalLocation(row.StatusChangedAt)))
            .OrderBy(row => row.GaveUpOn)
            .ToList();
    }

    public async Task<IReadOnlyList<EventOverviewRow>> EventsOverviewAsync(CancellationToken cancellationToken)
    {
        // Past events can no longer be cancelled, so the operations list shows only
        // today and future events.
        var today = clock.TodayAtTransitionalLocation;
        var rows = await context.Events
            .AsNoTracking()
            .Where(eventItem => eventItem.Status == EventStatus.Active && eventItem.Window.Date >= today)
            .Select(eventItem => new
            {
                eventItem.Id,
                eventItem.Window.Date,
                eventItem.Window.StartTime,
                Capacities = eventItem.Capacities.Select(capacity => new
                {
                    capacity.AppointmentTypeId,
                    capacity.TotalHeadcount,
                    capacity.RemainingCapacity,
                }).ToList(),
                ActiveBookings = context.Bookings.Count(booking =>
                    booking.EventId == eventItem.Id && booking.Status == BookingStatus.Active),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new EventOverviewRow(
                row.Id,
                row.Date,
                row.StartTime,
                row.StartTime.Add(EventWindow.Duration),
                row.Capacities.Select(capacity => new EventCapacityRow(
                    AppointmentTypeIds.CodeOf(capacity.AppointmentTypeId),
                    capacity.TotalHeadcount,
                    capacity.RemainingCapacity))
                    .OrderBy(capacity => capacity.Code, StringComparer.Ordinal).ToList(),
                row.ActiveBookings))
            .OrderBy(row => row.Date)
            .ThenBy(row => row.StartTime)
            .ToList();
    }

    /// <summary>Returns the latest delivery per attendee and whether its persisted context remains actionable.</summary>
    public async Task<IReadOnlyList<AttendeeEmailStatusRow>> LatestEmailStatusAsync(
        CancellationToken cancellationToken)
    {
        // EF Core cannot translate this per-group priority cleanly. An unresolved attempt remains
        // visible ahead of terminal history so one attendee's second message cannot hide it.
        var latest = (await context.EmailLogs
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .GroupBy(delivery => delivery.AttendeeId)
            .Select(group => group
                .OrderBy(delivery => delivery.Status is EmailStatus.Failed or EmailStatus.Pending ? 0 : 1)
                .ThenByDescending(delivery => delivery.SentAt)
                .ThenByDescending(delivery => delivery.Id)
                .First())
            .ToList();

        var attendeeIds = latest.Select(delivery => delivery.AttendeeId).Distinct().ToList();
        var attendeeStatuses = await context.Attendees
            .AsNoTracking()
            .Where(attendee => attendeeIds.Contains(attendee.Id))
            .ToDictionaryAsync(attendee => attendee.Id, attendee => attendee.Status, cancellationToken);

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

        // Event-cancellation retry needs the staged booking to exist (it is cancelled, not
        // active), matching RetryEmailHandler's booking lookup.
        var existingBookingIds = await context.Bookings
            .AsNoTracking()
            .Where(booking => bookingIds.Contains(booking.Id))
            .Select(booking => booking.Id)
            .ToHashSetAsync(cancellationToken);

        var eventIds = latest.Where(delivery => delivery.EventId.HasValue)
            .Select(delivery => delivery.EventId!.Value).Distinct().ToList();
        var cancelledEventIds = await context.Events
            .AsNoTracking()
            .Where(eventItem => eventIds.Contains(eventItem.Id) && eventItem.Status == EventStatus.Cancelled)
            .Select(eventItem => eventItem.Id)
            .ToHashSetAsync(cancellationToken);

        return latest
            .Select(delivery => new AttendeeEmailStatusRow(
                delivery.AttendeeId,
                delivery.TemplateName,
                delivery.SentAt,
                delivery.Status,
                (delivery.Status is EmailStatus.Failed or EmailStatus.Pending)
                && (delivery.TemplateName switch
                    {
                        EmailTemplate.AttendeeInvite or EmailTemplate.AttendeeReinvite =>
                            delivery.InviteId is { } inviteId && retryableInviteIds.Contains(inviteId),
                        EmailTemplate.BookingConfirmation =>
                            delivery.BookingId is { } bookingId && retryableBookingIds.Contains(bookingId),
                        EmailTemplate.EventCancelledRebookingNeeded =>
                            delivery.EventId is { } eventId
                            && cancelledEventIds.Contains(eventId)
                            && delivery.BookingId is { } cancellationBookingId
                            && existingBookingIds.Contains(cancellationBookingId)
                            && attendeeStatuses.TryGetValue(delivery.AttendeeId, out var status)
                            && status is AttendeeStatus.Invited or AttendeeStatus.AwaitingAvailability,
                        _ => false,
                    })))
            .ToList();
    }

    private IQueryable<AttendeeRow> AttendeeRows(AttendeeStatus status) =>
        context.Attendees
            .AsNoTracking()
            .Where(attendee => attendee.Status == status)
            .Select(attendee => new AttendeeRow(
                attendee.Id,
                attendee.Name,
                attendee.Email,
                EF.Property<DateTimeOffset>(attendee, StatusStampingInterceptor.ShadowProperty),
                attendee.Requirements.Select(requirement => requirement.AppointmentTypeId).ToList()));

    private sealed record AttendeeRow(
        Guid Id,
        string Name,
        string Email,
        DateTimeOffset StatusChangedAt,
        List<Guid> AppointmentTypeIds);
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Repositories/BookingAppointmentRepository.cs — 1/1

<!-- vocabulary-file: {"id":166,"oldPath":"src/EventBooking.Infrastructure/Persistence/Repositories/BookingAppointmentRepository.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Repositories/BookingAppointmentRepository.cs","beforeSha":"130ffd963cc60bebaa25259ed4090af5a53a1f262c1a6c6b9205970c21626d59","afterSha":"39151eb0b087e1f6db9152638497100c49dd10781f66e5c0d1de2e9e5365e5ec","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Infrastructure/Persistence/Repositories/BookingAppointmentRepository.cs — 1/1

<!-- vocabulary-file: {"id":166,"oldPath":"src/EventBooking.Infrastructure/Persistence/Repositories/BookingAppointmentRepository.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Repositories/BookingAppointmentRepository.cs","beforeSha":"130ffd963cc60bebaa25259ed4090af5a53a1f262c1a6c6b9205970c21626d59","afterSha":"39151eb0b087e1f6db9152638497100c49dd10781f66e5c0d1de2e9e5365e5ec","side":"after","part":1,"parts":1} -->

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
                    booking.AttendeeId,
                    booking.RecoveryOfBookingId ?? booking.Id,
                    booking.Id,
                    booking.EventId,
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

## before — src/EventBooking.Infrastructure/Persistence/Repositories/EmployeeGroupRepository.cs — 1/1

<!-- vocabulary-file: {"id":167,"oldPath":"src/EventBooking.Infrastructure/Persistence/Repositories/EmployeeGroupRepository.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Repositories/AttendeeGroupRepository.cs","beforeSha":"263ed4f559421bbcd4fcdba10d99d51e469bf4738cac4f208b4faa9eceafe56a","afterSha":"4ea34ca013d562e773445b119e0f8e9c5e53ecb4bae28cebfa4f1fef647e1504","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Infrastructure/Persistence/Repositories/AttendeeGroupRepository.cs — 1/1

<!-- vocabulary-file: {"id":167,"oldPath":"src/EventBooking.Infrastructure/Persistence/Repositories/EmployeeGroupRepository.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Repositories/AttendeeGroupRepository.cs","beforeSha":"263ed4f559421bbcd4fcdba10d99d51e469bf4738cac4f208b4faa9eceafe56a","afterSha":"4ea34ca013d562e773445b119e0f8e9c5e53ecb4bae28cebfa4f1fef647e1504","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AttendeeGroups;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Repositories;

/// <summary>Reads change-controlled Attendee Group reference data with complete mappings.</summary>
public sealed class AttendeeGroupRepository(EventBookingDbContext context) : IAttendeeGroupRepository
{
    /// <summary>Gets a group by stable identifier, including inactive or inconsistent rows.</summary>
    public Task<AttendeeGroup?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.AttendeeGroups
            .Include(group => group.Requirements)
            .SingleOrDefaultAsync(group => group.Id == id, cancellationToken);

    /// <summary>Gets a group from a trimmed case-insensitive canonical-code input.</summary>
    public Task<AttendeeGroup?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return context.AttendeeGroups
            .Include(group => group.Requirements)
            .SingleOrDefaultAsync(group => group.Code == normalized, cancellationToken);
    }

    /// <summary>Lists active mapped groups ordered by display name.</summary>
    public async Task<IReadOnlyList<AttendeeGroup>> ListActiveAsync(CancellationToken cancellationToken) =>
        await context.AttendeeGroups
            .Include(group => group.Requirements)
            .Where(group => group.IsActive && group.Requirements.Any())
            .OrderBy(group => group.Name)
            .ToListAsync(cancellationToken);
}
`````
