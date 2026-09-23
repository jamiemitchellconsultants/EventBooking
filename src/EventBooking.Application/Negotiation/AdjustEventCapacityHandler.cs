using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Negotiation;

/// <summary>Replaces the total headcount for the calling manager's own type on an event.</summary>
/// <param name="StaffUserId">The adjusting manager.</param>
/// <param name="EventId">The event identifier.</param>
/// <param name="TotalHeadcount">The new positive total covering every active booking.</param>
public sealed record AdjustEventCapacityCommand(
    Guid StaffUserId,
    Guid EventId,
    int TotalHeadcount);

/// <summary>The capacity row after adjusting.</summary>
/// <param name="EventId">The event identifier.</param>
/// <param name="TotalHeadcount">The total after the call.</param>
/// <param name="RemainingCapacity">The remaining count after the call.</param>
/// <param name="Changed">Whether anything moved.</param>
public sealed record AdjustEventCapacityOutcome(
    Guid EventId,
    int TotalHeadcount,
    int RemainingCapacity,
    bool Changed);

/// <summary>Adjusts the caller's own capacity row; another type's row is unreachable.</summary>
/// <param name="events">The event repository.</param>
/// <param name="capacities">The event capacity repository.</param>
/// <param name="access">The staff access authorizer.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit logger.</param>
/// <param name="types">The appointment type repository.</param>
public sealed class AdjustEventCapacityHandler(
    IEventRepository events,
    IEventCapacityRepository capacities,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IAppointmentTypeRepository types)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<AdjustEventCapacityOutcome>> HandleAsync(
        AdjustEventCapacityCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageEventNegotiation,
            null,
            ct);
        if (authorized.IsFailure)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(authorized.Error);
        }

        if (authorized.Value.AppointmentTypeId is not { } appointmentTypeId)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.Forbidden("Adjusting capacity needs an assigned appointment type."));
        }

        if (command.TotalHeadcount <= 0)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.Validation("totalHeadcount must be greater than zero."));
        }

        await using var transaction =
            await unitOfWork.BeginTransactionAsync(ct);

        var eventItem = await events.GetAsync(command.EventId, ct);
        if (eventItem is null)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.NotFound("No such event."));
        }

        // The command carries no type id: the scope is the only type the caller can act
        // for, and an event that does not list it is another type's business, which is a
        // permission answer rather than a missing row.
        if (!eventItem.Capacities.Any(c => c.AppointmentTypeId == appointmentTypeId))
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.Forbidden("You can only adjust capacity for your own appointment type."));
        }

        var locked = await capacities.LockForUpdateAsync(
            command.EventId,
            [appointmentTypeId],
            ct);
        var capacity = locked.SingleOrDefault();

        if (capacity is null)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.NotFound("No such event or capacity for the manager's appointment type."));
        }

        if (eventItem.Status != EventStatus.Active)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.Conflict("A cancelled event cannot have its capacity adjusted."));
        }

        var previousTotal = capacity.TotalHeadcount;
        var previousRemaining = capacity.RemainingCapacity;

        CapacityAdjustment adjustment;
        try
        {
            // The occupied count on the locked row is this type's active-booking count: every
            // active booking requiring the type holds exactly one place on it.
            adjustment = capacity.AdjustTotalHeadcount(
                command.TotalHeadcount, capacity.OccupiedCapacity);
        }
        catch (DomainException ex)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.Validation(ex.Message));
        }

        if (adjustment.Status == CapacityAdjustmentStatus.BelowActiveBookings)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(Error.CapacityBelowBookings(
                $"Headcount cannot be lower than the active-booking count of {adjustment.MinimumTotalHeadcount}.",
                adjustment.MinimumTotalHeadcount, capacity.TotalHeadcount, capacity.RemainingCapacity));
        }

        if (!adjustment.Changed)
        {
            await transaction.CommitAsync(ct);
            return Success(eventItem.Id, capacity, false);
        }

        audit.Record(
            AuditEntityTypes.Event,
            eventItem.Id,
            AuditAction.CapacityAdjusted,
            ActorType.Staff,
            command.StaffUserId.ToString(),
            $"{await TypeCodeAsync(appointmentTypeId, ct)} total "
            + $"{previousTotal} -> {capacity.TotalHeadcount}; remaining "
            + $"{previousRemaining} -> {capacity.RemainingCapacity}");

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Success(eventItem.Id, capacity, true);
    }

    private static Result<AdjustEventCapacityOutcome> Success(
        Guid eventId,
        EventCapacity capacity,
        bool changed) =>
        Result<AdjustEventCapacityOutcome>.Success(
            new AdjustEventCapacityOutcome(
                eventId,
                capacity.TotalHeadcount,
                capacity.RemainingCapacity,
                changed));

    private async Task<string> TypeCodeAsync(Guid appointmentTypeId, CancellationToken ct) =>
        (await types.GetAsync(appointmentTypeId, ct))?.Code ?? appointmentTypeId.ToString();
}
