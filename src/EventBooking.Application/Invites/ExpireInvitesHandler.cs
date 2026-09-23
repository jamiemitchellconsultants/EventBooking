using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Invites;

/// <summary>Defines invite sweep summary for the current use case.</summary>
/// <param name="Expired">The expired.</param>
/// <param name="ReIssued">The re issued.</param>
/// <param name="FlaggedForFollowUp">The flagged for follow up.</param>
public sealed record InviteSweepSummary(int Expired, int ReIssued, int FlaggedForFollowUp);

/// <summary>
/// The scheduled half of the invite engine. Expiry is never triggered by a candidate opening a
/// link — an invite nobody ever opens has to expire too.
/// </summary>
/// <param name="deliveries">Dispatches staged re-invites after each commit.</param>
/// <param name="invites">The invites.</param>
/// <param name="candidates">The candidates.</param>
/// <param name="settings">The settings.</param>
/// <param name="issuer">The issuer.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="clock">The clock.</param>
public sealed class ExpireInvitesHandler(
    IInviteRepository invites,
    ICandidateRepository candidates,
    ISystemSettingsRepository settings,
    InviteIssuer issuer,
    EmailDeliveryService deliveries,
    IAuditLogger audit,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    /// <summary>Expires and optionally replaces each due invite under its candidate lifecycle lock.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<InviteSweepSummary> HandleAsync(CancellationToken cancellationToken)
    {
        var configuration = await settings.GetAsync(cancellationToken);
        var due = await invites.ListPendingExpiredAsync(clock.UtcNow, cancellationToken);

        var expired = 0;
        var reIssued = 0;
        var flagged = 0;

        foreach (var dueInvite in due)
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

            // The candidate is the lifecycle root. The due-list row is only a work hint and is
            // re-read under the candidate and invite locks before any transition is made.
            var candidate = await candidates.LockForUpdateAsync(dueInvite.CandidateId, cancellationToken);
            if (candidate is null)
            {
                continue;
            }

            var invite = await invites.LockForUpdateAsync(dueInvite.Id, cancellationToken);
            if (invite is null || !invite.IsUsableAt(clock.UtcNow) && invite.Status != Domain.Invites.InviteStatus.Pending)
            {
                await transaction.CommitAsync(cancellationToken);
                continue;
            }

            if (invite.RecoveryOfBookingId.HasValue)
            {
                if (invite.ExpiresAt > clock.UtcNow)
                {
                    await transaction.CommitAsync(cancellationToken);
                    continue;
                }

                invite.MarkExpired();
                expired++;

                audit.Record(
                    AuditEntityTypes.Invite,
                    invite.Id,
                    AuditAction.InviteExpired,
                    ActorType.System,
                    null,
                    $"retry {invite.RetryCount}");

                try
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }

                continue;
            }

            if (invite.ExpiresAt > clock.UtcNow || candidate.Status == Domain.Candidates.CandidateStatus.Booked)
            {
                await transaction.CommitAsync(cancellationToken);
                continue;
            }

            invite.MarkExpired();
            expired++;

            audit.Record(
                AuditEntityTypes.Invite,
                invite.Id,
                AuditAction.InviteExpired,
                ActorType.System,
                null,
                $"retry {invite.RetryCount}");

            if (invite.RetryCount >= configuration.MaxAutoRetryCount)
            {
                candidate.MarkNoResponse();
                flagged++;
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                continue;
            }

            var issueResult = await issuer.IssueInitialAsync(
                candidate,
                invite.RetryCount + 1,
                ActorType.System,
                null,
                isReinvite: true,
                cancellationToken);

            if (issueResult.IsFailure)
            {
                if (candidate.Status == Domain.Candidates.CandidateStatus.Invited)
                {
                    candidate.MarkNoResponse();
                    flagged++;
                    audit.Record(
                        AuditEntityTypes.Invite,
                        invite.Id,
                        AuditAction.InviteExpired,
                        ActorType.System,
                        null,
                        "re-issue failed");
                }

                try
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }

                continue;
            }

            var issued = issueResult.Value;
            if (issued.Invited)
            {
                reIssued++;
            }

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            if (issued.DispatchPlan is { } plan)
            {
                await deliveries.DispatchClaimedAsync(
                    plan.DeliveryId, plan.Message, cancellationToken, plan.OnSent);
            }
        }

        return new InviteSweepSummary(expired, reIssued, flagged);
    }
}
