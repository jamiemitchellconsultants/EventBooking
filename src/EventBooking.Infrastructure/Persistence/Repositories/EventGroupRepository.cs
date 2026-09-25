using EventBooking.Application.Abstractions;
using EventBooking.Domain.EventGroups;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.SelfRegistrations;
using EventBooking.Infrastructure.Persistence.Locking;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Repositories;

/// <summary>Persists Event Group publication aggregates with their membership collections.</summary>
public sealed class EventGroupRepository(EventBookingDbContext context, RowLocks rowLocks)
    : IEventGroupRepository
{
    /// <summary>For a test driving one context directly, with a lock tracker of its own.</summary>
    /// <param name="context">The context to read through.</param>
    public EventGroupRepository(EventBookingDbContext context)
        : this(context, new RowLocks(context))
    {
    }

    /// <summary>Gets a group with both membership collections.</summary>
    public Task<EventGroup?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.EventGroups
            .Include(group => group.AttendeeGroups)
            .Include(group => group.Events)
            .SingleOrDefaultAsync(group => group.Id == id, cancellationToken);

    /// <summary>Locks the parent Event Group row before loading gate and selected-group data.</summary>
    public Task<EventGroup?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        rowLocks.LockEventGroupAsync(id, cancellationToken);

    /// <summary>Lists every group with both membership collections, ordered by title.</summary>
    public async Task<IReadOnlyList<EventGroup>> ListAsync(CancellationToken cancellationToken) =>
        await context.EventGroups
            .Include(group => group.AttendeeGroups)
            .Include(group => group.Events)
            .OrderBy(group => group.Title)
            .ToListAsync(cancellationToken);

    /// <summary>Lists every group selecting the attendee group, in ascending id order.</summary>
    public async Task<IReadOnlyList<EventGroup>> ListContainingAttendeeGroupAsync(
        Guid attendeeGroupId, CancellationToken cancellationToken) =>
        await context.EventGroups
            .Include(group => group.AttendeeGroups)
            .Include(group => group.Events)
            .Where(group => group.AttendeeGroups.Any(selected => selected.AttendeeGroupId == attendeeGroupId))
            .OrderBy(group => group.Id)
            .ToListAsync(cancellationToken);

    /// <summary>Stages a new event group for the next save.</summary>
    public void Add(EventGroup group) => context.EventGroups.Add(group);

    /// <summary>Stages a new pending registration for the next save.</summary>
    public void AddRegistration(PendingRegistration registration) =>
        context.PendingRegistrations.Add(registration);

    /// <summary>Finds the pending registration for one event and email address.</summary>
    public Task<PendingRegistration?> FindInFlightAsync(
        Guid eventId, string email, CancellationToken cancellationToken) =>
        context.PendingRegistrations.SingleOrDefaultAsync(
            x => x.EventId == eventId && x.Email == email
                && x.Status == SelfRegistrationStatus.Pending,
            cancellationToken);

    /// <summary>Gets one pending registration by its request identifier.</summary>
    public Task<PendingRegistration?> GetRegistrationAsync(
        Guid requestId, CancellationToken cancellationToken) =>
        context.PendingRegistrations.SingleOrDefaultAsync(
            x => x.RequestId == requestId, cancellationToken);

    /// <summary>Re-reads one pending registration without tracking, for post-lock validation.</summary>
    public Task<PendingRegistration?> GetRegistrationUntrackedAsync(
        Guid requestId, CancellationToken cancellationToken) =>
        context.PendingRegistrations.AsNoTracking().SingleOrDefaultAsync(
            x => x.RequestId == requestId, cancellationToken);

    /// <summary>Lists the pending registrations at or past their expiry.</summary>
    public async Task<IReadOnlyList<PendingRegistration>> ListExpiredPendingAsync(
        DateTimeOffset now, CancellationToken cancellationToken) =>
        await context.PendingRegistrations
            .Where(x => x.Status == SelfRegistrationStatus.Pending && x.ExpiresAt <= now)
            .OrderBy(x => x.ExpiresAt)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Deletes one bounded batch of terminal requests at or past retention, with the
    /// confirmation email rows for the bookings those requests created. Stale email rows
    /// only: a recent rebooking of the same event keeps its own delivery rows.
    /// </summary>
    public async Task<int> DeleteTerminalBeforeAsync(
        DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var due = await context.PendingRegistrations
            .Where(x => (x.Status == SelfRegistrationStatus.Confirmed
                    || x.Status == SelfRegistrationStatus.Expired)
                && x.TerminalAt != null && x.TerminalAt <= cutoff)
            .OrderBy(x => x.TerminalAt)
            .Take(500)
            .ToListAsync(cancellationToken);
        if (due.Count == 0) return 0;

        var confirmed = due.Where(x => x.Status == SelfRegistrationStatus.Confirmed).ToList();
        if (confirmed.Count > 0)
        {
            var emails = confirmed.Select(x => x.Email).ToList();
            var events = confirmed.Select(x => x.EventId).ToList();
            var attendeeIds = await context.Attendees
                .Where(x => emails.Contains(x.Email))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            var bookingIds = await context.Bookings
                .Where(x => attendeeIds.Contains(x.AttendeeId) && events.Contains(x.EventId))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            var deliveries = await context.EmailLogs
                .Where(x => x.TemplateName == EmailTemplate.SelfRegistrationConfirmation
                    && x.BookingId != null && bookingIds.Contains(x.BookingId.Value)
                    && x.SentAt <= cutoff)
                .ToListAsync(cancellationToken);
            context.EmailLogs.RemoveRange(deliveries);
        }

        context.PendingRegistrations.RemoveRange(due);
        return due.Count;
    }
}
