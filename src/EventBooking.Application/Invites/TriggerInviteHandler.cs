using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Invites;

/// <summary>A null staff identity means the system triggered this, not a person.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="CandidateId">The candidate id.</param>
public sealed record TriggerInviteCommand(Guid? StaffUserId, Guid CandidateId);

/// <summary>Issues an invite while serializing all transitions for the candidate lifecycle.</summary>
/// <param name="deliveries">Dispatches the staged invite after commit.</param>
/// <param name="candidates">The candidates.</param>
/// <param name="access">The access.</param>
/// <param name="issuer">The issuer.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class TriggerInviteHandler(
    ICandidateRepository candidates,
    IStaffAccessAuthorizer access,
    InviteIssuer issuer,
    EmailDeliveryService deliveries,
    IUnitOfWork unitOfWork)
{
    /// <summary>Issues or replaces a candidate invite under the candidate row lock.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<InviteIssueResult>> HandleAsync(
        TriggerInviteCommand command,
        CancellationToken cancellationToken)
    {
        var actorType = ActorType.System;
        string? actorId = null;

        if (command.StaffUserId is not null)
        {
            var authorized = await access.AuthorizeAsync(
                command.StaffUserId.Value,
                StaffCapability.ManageCandidates,
                null,
                cancellationToken);
            if (authorized.IsFailure)
            {
                return Result<InviteIssueResult>.Failure(authorized.Error);
            }

            actorType = ActorType.Staff;
            actorId = command.StaffUserId.Value.ToString();
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var candidate = await candidates.LockForUpdateAsync(command.CandidateId, cancellationToken);
        if (candidate is null)
        {
            return Result<InviteIssueResult>.Failure(Error.NotFound("No such candidate."));
        }

        if (candidate.Status == CandidateStatus.Booked)
        {
            return Result<InviteIssueResult>.Failure(Error.Conflict("This candidate is already booked."));
        }

        InviteIssueResult issued;
        try
        {
            // A manually triggered invite starts the retry count again: a coordinator pressing
            // "Re-invite Now" means start over, not continue chasing.
            var issueResult = await issuer.IssueInitialAsync(
                candidate, 0, actorType, actorId, isReinvite: false, cancellationToken);
            if (issueResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<InviteIssueResult>.Failure(issueResult.Error);
            }

            issued = issueResult.Value;
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<InviteIssueResult>.Failure(Error.Validation(ex.Message));
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<InviteIssueResult>.Failure(
                Error.Conflict("This candidate already has a pending invite."));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        if (issued.DispatchPlan is { } plan)
        {
            var status = await deliveries.DispatchClaimedAsync(
                plan.DeliveryId, plan.Message, cancellationToken, plan.OnSent);

            issued = issued with
            {
                EmailSent = status == EmailStatus.Sent,
                DeliveryStatus = status.ToString(),
            };
        }

        return Result<InviteIssueResult>.Success(issued);
    }
}
