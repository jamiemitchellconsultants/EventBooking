using EventBooking.Application.Abstractions;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Tests.Fakes;

public sealed record AuditEntry(
    string EntityType, Guid EntityId, AuditAction Action, ActorType ActorType, string? ActorId, string? Details);

public sealed class RecordingAuditLogger : IAuditLogger
{
    public List<AuditEntry> Entries { get; } = [];

    public void Record(
        string entityType,
        Guid entityId,
        AuditAction action,
        ActorType actorType,
        string? actorId,
        string? details = null) =>
        Entries.Add(new AuditEntry(entityType, entityId, action, actorType, actorId, details));

    public bool Contains(AuditAction action) => Entries.Any(e => e.Action == action);
}
