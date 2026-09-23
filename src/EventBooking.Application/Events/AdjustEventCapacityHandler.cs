using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Events;

/// <summary>Defines adjust event capacity command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="EventId">The event id.</param>
/// <param name="TotalHeadcount">The total headcount.</param>
public sealed record AdjustEventCapacityCommand(
    Guid ManagerUserId,
    Guid EventId,
    int TotalHeadcount);

/// <summary>Defines adjust event capacity outcome for the current use case.</summary>
/// <param name="EventId">The event id.</param>
/// <param name="TotalHeadcount">The total headcount.</param>
/// <param name="RemainingCapacity">The remaining capacity.</param>
public sealed record AdjustEventCapacityOutcome(
    Guid EventId,
    int TotalHeadcount,
    int RemainingCapacity);

/// <summary>Defines adjust event capacity handler for the current use case.</summary>
/// <param name="events">The events.</param>
/// <param name="capacities">The capacities.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
public sealed class AdjustEventCapacityHandler(
    IEventRepository events,
    IEventCapacityRepository capacities,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<AdjustEventCapacityOutcome>> HandleAsync(
        AdjustEventCapacityCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageEventNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(authorized.Error);
        }

        if (command.TotalHeadcount <= 0)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.Validation("totalHeadcount must be greater than zero."));
        }

        await using var transaction =
            await unitOfWork.BeginTransactionAsync(cancellationToken);

        var appointmentTypeId = authorized.Value.AppointmentTypeId!.Value;
        var locked = await capacities.LockForUpdateAsync(
            command.EventId,
            [appointmentTypeId],
            cancellationToken);
        var capacity = locked.SingleOrDefault();

        if (capacity is null)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.NotFound("No such event or capacity for the manager's appointment type."));
        }

        var eventItem = await events.GetAsync(command.EventId, cancellationToken);
        if (eventItem is null)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.NotFound("No such eventItem."));
        }

        if (eventItem.Status != EventStatus.Active)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.Conflict("A cancelled event cannot have its capacity adjusted."));
        }

        if (command.TotalHeadcount < capacity.OccupiedCapacity)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.Conflict(
                    "Headcount cannot be lower than the active-booking count of "
                    + $"{capacity.OccupiedCapacity}."));
        }

        var previousTotal = capacity.TotalHeadcount;
        var previousRemaining = capacity.RemainingCapacity;

        bool changed;
        try
        {
            changed = capacity.AdjustTotalHeadcount(command.TotalHeadcount);
        }
        catch (DomainException exception)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.Validation(exception.Message));
        }

        if (!changed)
        {
            await transaction.CommitAsync(cancellationToken);
            return Success(eventItem.Id, capacity);
        }

        audit.Record(
            AuditEntityTypes.Event,
            eventItem.Id,
            AuditAction.CapacityAdjusted,
            ActorType.Staff,
            command.ManagerUserId.ToString(),
            $"{AppointmentTypeName(appointmentTypeId)} total "
            + $"{previousTotal} -> {capacity.TotalHeadcount}; remaining "
            + $"{previousRemaining} -> {capacity.RemainingCapacity}");

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Success(eventItem.Id, capacity);
    }

    private static Result<AdjustEventCapacityOutcome> Success(
        Guid eventId,
        EventCapacity capacity) =>
        Result<AdjustEventCapacityOutcome>.Success(
            new AdjustEventCapacityOutcome(
                eventId,
                capacity.TotalHeadcount,
                capacity.RemainingCapacity));

    private static string AppointmentTypeName(Guid appointmentTypeId) =>
        Domain.AppointmentTypes.AppointmentTypeIds.NameOf(appointmentTypeId);
}
