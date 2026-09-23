using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Events;

/// <summary>Defines propose event command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="Headcount">The proposing Manager's own headcount (FR-2.1).</param>
public sealed record ProposeEventCommand(
    Guid ManagerUserId, DateOnly Date, TimeOnly StartTime, int Headcount = 1);

/// <summary>Creates one future open proposal inside a transaction protected by a database backstop.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
/// <param name="zones">The zone abstraction that interprets the window at its location.</param>
public sealed class ProposeEventHandler(
    IEventProposalRepository proposals,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock,
    IEventWindowZones zones)
{
    /// <summary>Creates the requested proposal or returns a stable conflict for its open window.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<Guid>> HandleAsync(
        ProposeEventCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageEventNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<Guid>.Failure(authorized.Error);
        }


        // A scoped role with no scope is granted nothing (FR-10.7): a proposal must name the
        // proposing appointment type.
        if (authorized.Value.AppointmentTypeId is not { } proposerAppointmentTypeId)
        {
            return Result<Guid>.Failure(Error.Forbidden("Proposing needs an assigned appointment type."));
        }

        EventWindow window;
        try
        {
            window = new EventWindow(command.Date, command.StartTime, 240);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Failure(Error.Validation(ex.Message));
        }

        if (!window.StartsAfter(clock.TodayAtTransitionalLocation))
        {
            return Result<Guid>.Failure(Error.Validation("A event must be proposed for a future date."));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var open = await proposals.ListOpenAsync(cancellationToken);
        if (open.Any(p => p.Window == window))
        {
            return Result<Guid>.Failure(Error.Conflict("An open proposal already exists for that window."));
        }

        var id = Guid.NewGuid();

        EventProposal proposal;
        try
        {
            // The listed set is still the predecessor's three types, and the location is the
            // transitional one, until Task 13 puts both on the command, the API and the MCP tool.
            proposal = EventProposal.Propose(
                id,
                TransitionalLocation.Id,
                locationIsActive: true,
                TransitionalLocation.TimeZoneId,
                window,
                zones,
                clock.UtcNow,
                [.. AppointmentTypeIds.All.Select(typeId => new ProposableAppointmentType(
                    typeId, AppointmentTypeIds.CodeOf(typeId), true, true))],
                proposerAppointmentTypeId,
                command.ManagerUserId,
                command.Headcount);
            proposals.Add(proposal);

            audit.Record(
                AuditEntityTypes.EventProposal,
                id,
                AuditAction.ProposalCreated,
                ActorType.Staff,
                command.ManagerUserId.ToString(),
                window.ToString());

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (ProposalValidationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<Guid>.Failure(Error.Validation(string.Join("; ", ex.Failures)));
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
