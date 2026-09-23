using EventBooking.Domain.Common;

namespace EventBooking.Domain.Audit;

/// <summary>An append-only record of one state change. Never updated, never deleted.</summary>
public sealed class AuditLog
{
    private AuditLog()
    {
        EntityType = string.Empty;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines entity type for the current use case.</summary>
    public string EntityType { get; private set; }

    /// <summary>Defines entity id for the current use case.</summary>
    public Guid EntityId { get; private set; }

    /// <summary>Defines action for the current use case.</summary>
    public AuditAction Action { get; private set; }

    /// <summary>Defines actor type for the current use case.</summary>
    public ActorType ActorType { get; private set; }

    /// <summary>The Entra object id for staff, the invite or booking id for a attendee token,
    /// null for the system.</summary>
    public string? ActorId { get; private set; }

    /// <summary>Defines timestamp for the current use case.</summary>
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>Defines details for the current use case.</summary>
    public string? Details { get; private set; }

    /// <summary>Defines record for the current use case.</summary>
    /// <param name="id">The id.</param>
    /// <param name="entityType">The entity type.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="action">The action.</param>
    /// <param name="actorType">The actor type.</param>
    /// <param name="actorId">The actor id.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <param name="details">The details.</param>
    public static AuditLog Record(
        Guid id,
        string entityType,
        Guid entityId,
        AuditAction action,
        ActorType actorType,
        string? actorId,
        DateTimeOffset timestamp,
        string? details)
    {
        Guard.Against(
            !AuditEntityTypes.All.Contains(entityType),
            $"{entityType} is not an audited entity type.");
        Guard.Against(entityId == Guid.Empty, "entityId must not be empty.");

        if (actorType != ActorType.System)
        {
            Guard.NotBlank(actorId, "actorId");
        }

        return new AuditLog
        {
            Id = id,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ActorType = actorType,
            ActorId = actorId,
            Timestamp = timestamp,
            Details = details,
        };
    }
}
