using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Slots;

/// <summary>Defines adjust confirmed slot capacity command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="ConfirmedSlotId">The confirmed slot id.</param>
/// <param name="TotalHeadcount">The total headcount.</param>
public sealed record AdjustConfirmedSlotCapacityCommand(
    Guid ManagerUserId,
    Guid ConfirmedSlotId,
    int TotalHeadcount);

/// <summary>Defines adjust confirmed slot capacity outcome for the current use case.</summary>
/// <param name="ConfirmedSlotId">The confirmed slot id.</param>
/// <param name="TotalHeadcount">The total headcount.</param>
/// <param name="RemainingCapacity">The remaining capacity.</param>
public sealed record AdjustConfirmedSlotCapacityOutcome(
    Guid ConfirmedSlotId,
    int TotalHeadcount,
    int RemainingCapacity);

/// <summary>Defines adjust confirmed slot capacity handler for the current use case.</summary>
/// <param name="slots">The slots.</param>
/// <param name="capacities">The capacities.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
public sealed class AdjustConfirmedSlotCapacityHandler(
    IConfirmedSlotRepository slots,
    ISlotCapacityRepository capacities,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<AdjustConfirmedSlotCapacityOutcome>> HandleAsync(
        AdjustConfirmedSlotCapacityCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageSlotNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<AdjustConfirmedSlotCapacityOutcome>.Failure(authorized.Error);
        }

        if (command.TotalHeadcount <= 0)
        {
            return Result<AdjustConfirmedSlotCapacityOutcome>.Failure(
                Error.Validation("totalHeadcount must be greater than zero."));
        }

        await using var transaction =
            await unitOfWork.BeginTransactionAsync(cancellationToken);

        var appointmentTypeId = authorized.Value.AppointmentTypeId!.Value;
        var locked = await capacities.LockForUpdateAsync(
            command.ConfirmedSlotId,
            [appointmentTypeId],
            cancellationToken);
        var capacity = locked.SingleOrDefault();

        if (capacity is null)
        {
            return Result<AdjustConfirmedSlotCapacityOutcome>.Failure(
                Error.NotFound("No such slot or capacity for the manager's appointment type."));
        }

        var slot = await slots.GetAsync(command.ConfirmedSlotId, cancellationToken);
        if (slot is null)
        {
            return Result<AdjustConfirmedSlotCapacityOutcome>.Failure(
                Error.NotFound("No such slot."));
        }

        if (slot.Status != ConfirmedSlotStatus.Active)
        {
            return Result<AdjustConfirmedSlotCapacityOutcome>.Failure(
                Error.Conflict("A cancelled slot cannot have its capacity adjusted."));
        }

        if (command.TotalHeadcount < capacity.OccupiedCapacity)
        {
            return Result<AdjustConfirmedSlotCapacityOutcome>.Failure(
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
            return Result<AdjustConfirmedSlotCapacityOutcome>.Failure(
                Error.Validation(exception.Message));
        }

        if (!changed)
        {
            await transaction.CommitAsync(cancellationToken);
            return Success(slot.Id, capacity);
        }

        audit.Record(
            AuditEntityTypes.ConfirmedSlot,
            slot.Id,
            AuditAction.CapacityAdjusted,
            ActorType.Staff,
            command.ManagerUserId.ToString(),
            $"{AppointmentTypeName(appointmentTypeId)} total "
            + $"{previousTotal} -> {capacity.TotalHeadcount}; remaining "
            + $"{previousRemaining} -> {capacity.RemainingCapacity}");

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Success(slot.Id, capacity);
    }

    private static Result<AdjustConfirmedSlotCapacityOutcome> Success(
        Guid slotId,
        SlotCapacity capacity) =>
        Result<AdjustConfirmedSlotCapacityOutcome>.Success(
            new AdjustConfirmedSlotCapacityOutcome(
                slotId,
                capacity.TotalHeadcount,
                capacity.RemainingCapacity));

    private static string AppointmentTypeName(Guid appointmentTypeId) =>
        Domain.AppointmentTypes.AppointmentTypeIds.NameOf(appointmentTypeId);
}
