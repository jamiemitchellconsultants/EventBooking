using EventBooking.Application.Abstractions;
using EventBooking.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace EventBooking.Application.Notifications;

/// <summary>
/// Coordinates durable pending deliveries and provider attempts. Staging is intentionally
/// synchronous so the caller can save it beside its business state; dispatch starts only after
/// that transaction has committed.
/// </summary>
/// <param name="deliveries">Persists and locks durable delivery rows.</param>
/// <param name="sender">Calls the configured provider transport.</param>
/// <param name="unitOfWork">Owns claim and outcome transactions.</param>
/// <param name="clock">Supplies deterministic claim and outcome timestamps.</param>
/// <param name="logger">Records failures in non-critical post-send audit callbacks.</param>
public sealed class EmailDeliveryService(
    IEmailDeliveryRepository deliveries,
    IEmailSender sender,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<EmailDeliveryService> logger)
{
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(5);
    private DateTimeOffset _lastStagedAt = DateTimeOffset.MinValue;

    /// <summary>
    /// Stages a pending delivery containing only safe context identifiers. The caller owns the
    /// enclosing transaction and must save it before calling <see cref="DispatchAsync"/>.
    /// </summary>
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="template">The template.</param>
    /// <param name="inviteId">The invite id.</param>
    /// <param name="bookingId">The booking id.</param>
    /// <param name="confirmedSlotId">The confirmed slot id.</param>
    /// <param name="after">The after.</param>
    public EmailLog StagePending(
        Guid candidateId,
        EmailTemplate template,
        Guid? inviteId = null,
        Guid? bookingId = null,
        Guid? confirmedSlotId = null,
        DateTimeOffset? after = null)
    {
        var createdAt = clock.UtcNow;
        if (after is not null && createdAt <= after.Value)
        {
            createdAt = after.Value.AddTicks(1);
        }

        if (createdAt <= _lastStagedAt)
        {
            createdAt = _lastStagedAt.AddTicks(1);
        }

        _lastStagedAt = createdAt;
        var delivery = EmailLog.RecordPending(
            Guid.NewGuid(),
            candidateId,
            template,
            createdAt,
            inviteId,
            bookingId,
            confirmedSlotId);

        deliveries.Add(delivery);
        return delivery;
    }

    /// <summary>Claims a newly staged row for the current business transaction's post-commit plan.</summary>
    /// <param name="delivery">The delivery.</param>
    public void ClaimForDispatch(EmailLog delivery)
    {
        if (!delivery.TryClaim(clock.UtcNow, ClaimLease))
        {
            throw new InvalidOperationException("The email delivery is already claimed.");
        }
    }

    /// <summary>
    /// Claims one pending delivery, sends it outside the claim transaction, and records Sent or
    /// Failed in a second durable transaction. A fresh claim held by another worker returns
    /// Pending without calling the transport.
    /// </summary>
    /// <param name="onSent">Optional audit callback staged with the Sent outcome.</param>
    /// <param name="deliveryId">The delivery id.</param>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<EmailStatus> DispatchAsync(
        Guid deliveryId,
        EmailMessage message,
        CancellationToken cancellationToken,
        Action? onSent = null)
    {
        await using (var claimTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken))
        {
            var delivery = await deliveries.LockForUpdateAsync(deliveryId, cancellationToken);
            if (delivery is null)
            {
                await claimTransaction.RollbackAsync(cancellationToken);
                return EmailStatus.Failed;
            }

            if (delivery.Status is EmailStatus.Sent or EmailStatus.Resolved)
            {
                await claimTransaction.CommitAsync(cancellationToken);
                return delivery.Status;
            }

            if (!delivery.TryClaim(clock.UtcNow, ClaimLease))
            {
                await claimTransaction.CommitAsync(cancellationToken);
                return EmailStatus.Pending;
            }

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await claimTransaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await claimTransaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        return await DispatchClaimedAsync(deliveryId, message, cancellationToken, onSent);
    }

    /// <summary>
    /// Sends a row claimed by the current business transaction and records its terminal outcome.
    /// The claim must be committed before this method is called; an uncompleted claim remains
    /// Pending and can be reclaimed after its lease expires.
    /// </summary>
    /// <param name="onSent">Optional audit callback staged with the Sent outcome.</param>
    /// <param name="deliveryId">The delivery id.</param>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<EmailStatus> DispatchClaimedAsync(
        Guid deliveryId,
        EmailMessage message,
        CancellationToken cancellationToken,
        Action? onSent = null)
    {
        bool sent;
        try
        {
            sent = await sender.SendAsync(message with { DeliveryId = deliveryId }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            sent = false;
        }

        await using var resultTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var completed = await deliveries.LockForUpdateAsync(deliveryId, cancellationToken);
        if (completed is not null)
        {
            if (sent)
            {
                completed.MarkSent(clock.UtcNow);
                try
                {
                    onSent?.Invoke();
                }
                catch (Exception exception)
                {
                    // A provider result is already real; an audit callback must not leave the
                    // durable delivery pending and invite a duplicate provider attempt.
                    logger.LogError(
                        exception,
                        "The post-send audit callback failed for email delivery {DeliveryId}.",
                        deliveryId);
                }
            }
            else
            {
                completed.MarkFailed(clock.UtcNow);
            }

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await resultTransaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await resultTransaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        else
        {
            await resultTransaction.RollbackAsync(cancellationToken);
        }

        return sent ? EmailStatus.Sent : EmailStatus.Failed;
    }
}
