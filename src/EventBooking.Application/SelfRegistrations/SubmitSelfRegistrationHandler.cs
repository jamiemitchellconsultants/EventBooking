using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;
using EventBooking.Domain.SelfRegistrations;
using EventBooking.Domain.Time;

namespace EventBooking.Application.SelfRegistrations;

/// <summary>Anonymous self-registration submissions.</summary>
/// <param name="groups">The event groups.</param>
/// <param name="attendeeGroups">The attendee groups.</param>
/// <param name="events">The events.</param>
/// <param name="locations">The locations for future-window checks.</param>
/// <param name="settings">The system settings.</param>
/// <param name="emails">The email outbox.</param>
/// <param name="unitOfWork">The unitOfWork.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
/// <param name="zones">The zones.</param>
/// <param name="correlation">The correlation.</param>
public sealed class SubmitSelfRegistrationHandler(
    IEventGroupRepository groups,
    IAttendeeGroupRepository attendeeGroups,
    IEventRepository events,
    ILocationRepository locations,
    ISystemSettingsRepository settings,
    IEmailDeliveryRepository emails,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock,
    IEventWindowZones zones,
    ICorrelationContext correlation)
{
    private const int LinkCooldownSeconds = 60;

    /// <summary>Submits an anonymous request to join an event.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<SubmitSelfRegistrationResult>> HandleAsync(
        SubmitSelfRegistrationCommand command, CancellationToken ct)
    {
        var group = await groups.GetUntrackedAsync(command.EventGroupId, ct);
        if (group is null) return Result<SubmitSelfRegistrationResult>.Failure(
            Error.NotFound("No such event group."));

        var membership = group.Events.SingleOrDefault(x => x.EventId == command.EventId);
        if (membership is null) return Result<SubmitSelfRegistrationResult>.Failure(
            Error.NotFound("No such event in this event group."));

        if (!group.IsOpen || !membership.IsOpen)
            return Result<SubmitSelfRegistrationResult>.Failure(SelfRegistrationErrors.NotAvailable());

        if (!group.AttendeeGroups.Any(x => x.AttendeeGroupId == command.AttendeeGroupId))
            return Result<SubmitSelfRegistrationResult>.Failure(
                SelfRegistrationErrors.GroupNotSelectable());

        var attendeeGroup = await attendeeGroups.GetAsync(command.AttendeeGroupId, ct);
        if (attendeeGroup is null || !attendeeGroup.IsActive)
            return Result<SubmitSelfRegistrationResult>.Failure(
                SelfRegistrationErrors.GroupNotSelectable());

        var eventItem = await events.GetAsync(command.EventId, ct);
        if (eventItem is null || eventItem.Status != EventStatus.Active
            || await HasStartedAsync(eventItem, ct))
            return Result<SubmitSelfRegistrationResult>.Failure(
                eventItem is null
                    ? Error.NotFound("No such event in this event group.")
                    : SelfRegistrationErrors.NotAvailable());

        var now = clock.UtcNow;
        var expiryHours = (await settings.GetAsync(ct)).PendingRegistrationExpiryHours;

        PendingRegistration registration;
        try
        {
            registration = PendingRegistration.Create(
                Guid.NewGuid(), group.Id, eventItem.Id, attendeeGroup.Id,
                command.Name, command.Email, now, expiryHours);
        }
        catch (DomainException ex)
        {
            return Result<SubmitSelfRegistrationResult>.Failure(Error.Validation(ex.Message));
        }

        bool hasSpace;
        try
        {
            hasSpace = eventItem.HasSpareCapacityForAll(attendeeGroup.RequiredAppointmentTypeIds);
        }
        catch (DomainException)
        {
            return Result<SubmitSelfRegistrationResult>.Failure(SelfRegistrationErrors.NotAvailable());
        }

        if (!hasSpace)
            return Result<SubmitSelfRegistrationResult>.Failure(SelfRegistrationErrors.Full());

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        // The gates are read again under the group lock that publication changes take, so a
        // close that committed first is honoured rather than raced.
        var locked = await groups.LockForUpdateAsync(group.Id, ct);
        var lockedMembership = locked?.Events.SingleOrDefault(x => x.EventId == command.EventId);
        if (locked is null || lockedMembership is null || !locked.IsOpen || !lockedMembership.IsOpen
            || locked.AttendeeGroups.All(x => x.AttendeeGroupId != attendeeGroup.Id))
            return Result<SubmitSelfRegistrationResult>.Failure(SelfRegistrationErrors.NotAvailable());

        await groups.LockEmailAsync(registration.Email, ct);

        // One live request per address, event and selection. A live one is resent; a lapsed one
        // is retired so its replacement can take its place. A live request for a different
        // selection is never touched: the caller has not yet proved they own the address, so
        // they must not be able to invalidate a link that address's owner is holding.
        var existing = await groups.FindInFlightAsync(
            eventItem.Id, registration.Email, group.Id, attendeeGroup.Id, ct);
        if (existing is not null && now >= existing.ExpiresAt)
        {
            existing.Expire(now);
            audit.Record(AuditEntityTypes.SelfRegistration, existing.RequestId,
                AuditAction.SelfRegistrationExpired, ActorType.System, correlation.CorrelationId,
                $"status {existing.Status}");
            await unitOfWork.SaveChangesAsync(ct);
            existing = null;
        }

        var target = existing ?? registration;
        if (existing is null)
        {
            groups.AddRegistration(registration);
            audit.Record(AuditEntityTypes.SelfRegistration, registration.RequestId,
                AuditAction.SelfRegistrationRequested, ActorType.Anonymous,
                correlation.CorrelationId,
                $"correlation {correlation.CorrelationId}; group {group.Id}; event {eventItem.Id}");
        }

        // At most one link per address per cooldown. Repeating a live request adds nothing;
        // a new request's link waits its turn instead of being dropped.
        var busyUntil = await groups.LatestLinkDueAsync(
            registration.Email, now.AddSeconds(-LinkCooldownSeconds * 2), ct);
        var due = busyUntil is { } last ? last.AddSeconds(LinkCooldownSeconds) : now;
        if (existing is null || due <= now)
        {
            var link = SelfRegistrationDelivery.StageLink(
                Guid.NewGuid(), target.RequestId, now, correlation.CorrelationId);
            if (due > now) link.SetNotBefore(due);
            emails.Add(link);
        }

        var requestId = target.RequestId;
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (UniqueConstraintViolationException)
        {
            // A concurrent submission for the same address won the race; report its request.
            await transaction.RollbackAsync(ct);
            requestId = (await groups.FindInFlightAsync(
                eventItem.Id, registration.Email, group.Id, attendeeGroup.Id, ct))?.RequestId
                ?? requestId;
        }

        return Result<SubmitSelfRegistrationResult>.Success(new SubmitSelfRegistrationResult(
            requestId, group.Id, eventItem.Id, attendeeGroup.Id, registration.Name,
            (command.Email ?? string.Empty).Trim(), target.ExpiresAt));
    }

    private async Task<bool> HasStartedAsync(Event eventItem, CancellationToken ct)
    {
        var location = await locations.GetAsync(eventItem.LocationId, ct);
        return location is null
            || eventItem.Window.HasStarted(zones, location.TimeZoneId, clock.UtcNow);
    }
}
