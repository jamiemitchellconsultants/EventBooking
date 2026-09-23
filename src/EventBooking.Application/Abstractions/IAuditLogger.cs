using EventBooking.Domain.Audit;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines iaudit logger for the current use case.</summary>
public interface IAuditLogger
{
    /// <summary>
    /// Stages one audit row on the current unit of work. Synchronous and void by design: the entry
    /// commits with the change it describes, or not at all.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="action">The action.</param>
    /// <param name="actorType">The actor type.</param>
    /// <param name="actorId">The actor id.</param>
    /// <param name="details">The details.</param>
    void Record(
        string entityType,
        Guid entityId,
        AuditAction action,
        ActorType actorType,
        string? actorId,
        string? details = null);
}
