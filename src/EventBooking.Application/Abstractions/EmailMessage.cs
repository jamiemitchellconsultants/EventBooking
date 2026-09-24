using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Abstractions;

/// <summary>
/// A fully rendered attendee email held in memory only for the provider call. The optional
/// delivery identifier correlates the provider attempt with its durable outbox row.
/// </summary>
/// <param name="AttendeeId">The attendee receiving the message.</param>
/// <param name="ToAddress">The attendee's email address.</param>
/// <param name="ToName">The attendee's display name.</param>
/// <param name="Template">The attendee-facing template.</param>
/// <param name="Subject">The rendered subject.</param>
/// <param name="TextBody">The rendered plain-text body, including any raw token only in memory.</param>
/// <param name="HtmlBody">The rendered HTML body, including any raw token only in memory.</param>
/// <param name="DeliveryId">The durable outbox row this attempt completes.</param>
public sealed record EmailMessage(
    Guid AttendeeId,
    string ToAddress,
    string ToName,
    EmailTemplate Template,
    string Subject,
    string TextBody,
    string HtmlBody,
    Guid? DeliveryId = null);
