using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Slots;

/// <summary>Defines propose slot command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
public sealed record ProposeSlotCommand(Guid ManagerUserId, DateOnly Date, TimeOnly StartTime);

/// <summary>Creates one future open proposal inside a transaction protected by a database backstop.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
public sealed class ProposeSlotHandler(
    ISlotProposalRepository proposals,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock)
{
    /// <summary>Creates the requested proposal or returns a stable conflict for its open window.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<Guid>> HandleAsync(
        ProposeSlotCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageSlotNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<Guid>.Failure(authorized.Error);
        }

        SlotWindow window;
        try
        {
            window = new SlotWindow(command.Date, command.StartTime);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Failure(Error.Validation(ex.Message));
        }

        if (!window.StartsAfter(clock.TodayAtHeadOffice))
        {
            return Result<Guid>.Failure(Error.Validation("A slot must be proposed for a future date."));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var open = await proposals.ListOpenAsync(cancellationToken);
        if (open.Any(p => p.Window == window))
        {
            return Result<Guid>.Failure(Error.Conflict("An open proposal already exists for that window."));
        }

        var id = Guid.NewGuid();

        SlotProposal proposal;
        try
        {
            proposal = SlotProposal.Create(id, window, command.ManagerUserId);
            proposals.Add(proposal);

            audit.Record(
                AuditEntityTypes.SlotProposal,
                id,
                AuditAction.ProposalCreated,
                ActorType.Staff,
                command.ManagerUserId.ToString(),
                window.ToString());

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<Guid>.Failure(Error.Validation(ex.Message));
        }
        catch (UniqueConstraintViolationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<Guid>.Failure(Error.Conflict("An open proposal already exists for that window."));
        }

        return Result<Guid>.Success(id);
    }
}
