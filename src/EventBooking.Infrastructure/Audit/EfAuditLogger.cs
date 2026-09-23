using EventBooking.Application.Abstractions;
using EventBooking.Domain.Audit;
using EventBooking.Infrastructure.Persistence;

namespace EventBooking.Infrastructure.Audit;

/// <summary>
/// Stages the audit row on the caller's own context, so it commits with the change it describes.
/// </summary>
public sealed class EfAuditLogger(EventBookingDbContext context, IClock clock) : IAuditLogger
{
    public void Record(
        string entityType,
        Guid entityId,
        AuditAction action,
        ActorType actorType,
        string? actorId,
        string? details = null)
    {
        context.AuditLogs.Add(AuditLog.Record(
            Guid.NewGuid(), entityType, entityId, action, actorType, actorId, clock.UtcNow, details));
    }
}
