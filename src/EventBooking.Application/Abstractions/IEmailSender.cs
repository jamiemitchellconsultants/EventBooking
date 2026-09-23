using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Abstractions;

/// <summary>
/// A fully rendered attendee email held in memory only for the provider call. The optional
/// delivery identifier correlates the provider attempt with its hash-only durable record.
/// </summary>
/// <param name="AttendeeId">The attendee receiving the message.</param>
/// <param name="ToAddress">The attendee's email address.</param>
/// <param name="ToName">The attendee's display name.</param>
/// <param name="Template">The attendee-facing template.</param>
/// <param name="Subject">The rendered subject.</param>
/// <param name="TextBody">The rendered plain-text body, including any raw token only in memory.</param>
/// <param name="HtmlBody">The rendered HTML body, including any raw token only in memory.</param>
/// <param name="DeliveryId">The safe durable delivery identifier, when dispatched through the coordinator.</param>
public sealed record EmailMessage(
    Guid AttendeeId,
    string ToAddress,
    string ToName,
    EmailTemplate Template,
    string Subject,
    string TextBody,
    string HtmlBody,
    Guid? DeliveryId = null);

/// <summary>Provider-facing port for one rendered attendee email.</summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends the message and returns false instead of throwing when the provider rejects it. A
    /// direct, uncoordinated call writes its own email-log row; a coordinated call carries a
    /// delivery identifier whose existing durable row is completed by the delivery service.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
