using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Abstractions;

/// <summary>Persistence port for the durable attendee-email delivery projection.</summary>
public interface IEmailDeliveryRepository
{
    /// <summary>Loads one delivery without taking a database lock.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EmailLog?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads one delivery while holding its PostgreSQL row lock.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EmailLog?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads the newest unresolved delivery, or latest terminal row, while holding its row lock.</summary>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EmailLog?> LockLatestForAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken);

    /// <summary>Loads the newest unresolved delivery, or latest terminal row, for one template.</summary>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="template">The template.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EmailLog?> GetLatestForAttendeeAsync(
        Guid attendeeId,
        EmailTemplate template,
        CancellationToken cancellationToken);

    /// <summary>Stages a delivery row for the caller's current transaction.</summary>
    /// <param name="delivery">The delivery.</param>
    void Add(EmailLog delivery);
}
