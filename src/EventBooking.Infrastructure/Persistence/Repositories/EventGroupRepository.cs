using EventBooking.Application.Abstractions;
using EventBooking.Domain.EventGroups;
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
}
