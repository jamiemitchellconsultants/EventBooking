using EventBooking.Application.Abstractions;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventBooking.Infrastructure.Email;

/// <summary>
/// Sends through the transport and records one email log row either way. Returns false rather than
/// throwing: a bounced confirmation must never roll back a good booking.
/// </summary>
public sealed class LoggingEmailSender(
    IEmailTransport transport,
    IDbContextFactory<EventBookingDbContext> contextFactory,
    IClock clock,
    ILogger<LoggingEmailSender> logger) : IEmailSender
{
    /// <summary>
    /// Sends through the configured transport and returns a provider outcome. Coordinated durable
    /// deliveries already own their <see cref="EmailLog"/> row and therefore are not logged twice.
    /// </summary>
    public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var status = EmailStatus.Sent;

        try
        {
            await transport.SendAsync(message, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            status = EmailStatus.Failed;
            logger.LogError(
                ex,
                "Sending the {Template} email to attendee {AttendeeId} failed.",
                message.Template,
                message.AttendeeId);
        }

        if (message.DeliveryId is null)
        {
            await RecordAsync(message, status, cancellationToken);
        }

        return status == EmailStatus.Sent;
    }

    private async Task RecordAsync(
        EmailMessage message,
        EmailStatus status,
        CancellationToken cancellationToken)
    {
        try
        {
            // A context of its own: the confirmation email is sent after its booking transaction
            // has already committed, so there is no unit of work left to save this row on.
            await using var context = contextFactory.CreateDbContext();

            context.EmailLogs.Add(EmailLog.Record(
                Guid.NewGuid(), message.AttendeeId, message.Template, clock.UtcNow, status));

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Losing the audit row is bad, but not as bad as failing the caller's operation for it.
            logger.LogWarning(ex, "Writing the email log row failed.");
        }
    }
}
