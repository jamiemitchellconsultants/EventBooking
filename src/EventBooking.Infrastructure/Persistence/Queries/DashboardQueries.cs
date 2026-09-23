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
