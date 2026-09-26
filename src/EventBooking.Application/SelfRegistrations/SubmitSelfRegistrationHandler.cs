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
    /// <summary>Submits an anonymous request to join an event.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<SubmitSelfRegistrationResult>> HandleAsync(
        SubmitSelfRegistrationCommand command, CancellationToken ct)
    {
        var group = await groups.GetAsync(command.EventGroupId, ct);
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

        // An address that already has a request in flight gets a fresh copy of the email for
        // that request and the same neutral receipt: neither the reply nor the token reveals
        // whether the address was already known.
        var existing = await groups.FindInFlightAsync(eventItem.Id, registration.Email, ct);
        var target = existing ?? registration;

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        if (existing is null)
        {
            groups.AddRegistration(registration);
            audit.Record(AuditEntityTypes.SelfRegistration, registration.RequestId,
                AuditAction.SelfRegistrationRequested, ActorType.Anonymous,
                correlation.CorrelationId,
                $"correlation {correlation.CorrelationId}; group {group.Id}; event {eventItem.Id}");
        }

        emails.Add(SelfRegistrationDelivery.StageLink(
            Guid.NewGuid(), target.RequestId, now, correlation.CorrelationId));
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (UniqueConstraintViolationException)
        {
            // A concurrent submission for the same address won the race; the receipt is the same.
            await transaction.RollbackAsync(ct);
        }

        return Result<SubmitSelfRegistrationResult>.Success(new SubmitSelfRegistrationResult(
            target.RequestId, group.Id, eventItem.Id, attendeeGroup.Id, registration.Name,
            (command.Email ?? string.Empty).Trim(), target.ExpiresAt));
    }

    private async Task<bool> HasStartedAsync(Event eventItem, CancellationToken ct)
    {
        var location = await locations.GetAsync(eventItem.LocationId, ct);
        return location is null
            || eventItem.Window.HasStarted(zones, location.TimeZoneId, clock.UtcNow);
    }
}
