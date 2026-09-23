using EventBooking.Domain.Common;

namespace EventBooking.Domain.Notifications;

/// <summary>
/// A durable record of one attendee email delivery attempt. Its identity and safe context are
/// immutable while claim and outcome fields transition; raw tokens, URLs, and bodies never belong
/// in this record.
/// </summary>
public sealed class EmailLog
{
    private EmailLog()
    {
    }

    /// <summary>The durable identifier of this delivery attempt.</summary>
    public Guid Id { get; private set; }

    /// <summary>The attendee who is the recipient of this delivery attempt.</summary>
    public Guid AttendeeId { get; private set; }

    /// <summary>The attendee-facing template this attempt renders.</summary>
    public EmailTemplate TemplateName { get; private set; }

    /// <summary>The timestamp of the current or most recent attempt, supplied by <c>IClock</c>.</summary>
    public DateTimeOffset SentAt { get; private set; }

    /// <summary>The durable provider outcome, including <see cref="EmailStatus.Pending"/>.</summary>
    public EmailStatus Status { get; private set; }

    /// <summary>The invite context used by invite and re-invite templates, when applicable.</summary>
    public Guid? InviteId { get; private set; }

    /// <summary>The booking context used by a booking-confirmation template, when applicable.</summary>
    public Guid? BookingId { get; private set; }

    /// <summary>The event context used by a cancellation template, when applicable.</summary>
    public Guid? EventId { get; private set; }

    /// <summary>The in-progress claim timestamp used to prevent duplicate concurrent sends.</summary>
    public DateTimeOffset? ClaimedAt { get; private set; }

    /// <summary>Creates a legacy email attempt without a regeneration context.</summary>
    /// <param name="id">The id.</param>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="templateName">The template name.</param>
    /// <param name="sentAt">The sent at.</param>
    /// <param name="status">The status.</param>
    public static EmailLog Record(
        Guid id,
        Guid attendeeId,
        EmailTemplate templateName,
        DateTimeOffset sentAt,
        EmailStatus status)
        => PendingOrRecorded(id, attendeeId, templateName, sentAt, status, null, null, null);

    /// <summary>
    /// Creates a pending delivery with only safe context identifiers. The caller saves it in the
    /// same business transaction as the state change that caused the notification.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="templateName">The template name.</param>
    /// <param name="createdAt">The created at.</param>
    /// <param name="inviteId">The invite id.</param>
    /// <param name="bookingId">The booking id.</param>
    /// <param name="eventId">The event id.</param>
    public static EmailLog RecordPending(
        Guid id,
        Guid attendeeId,
        EmailTemplate templateName,
        DateTimeOffset createdAt,
        Guid? inviteId = null,
        Guid? bookingId = null,
        Guid? eventId = null)
        => PendingOrRecorded(
            id,
            attendeeId,
            templateName,
            createdAt,
            EmailStatus.Pending,
            inviteId,
            bookingId,
            eventId);

    /// <summary>Claims a pending delivery unless another worker holds a fresh claim.</summary>
    /// <param name="now">The now.</param>
    /// <param name="lease">The lease.</param>
    public bool TryClaim(DateTimeOffset now, TimeSpan lease)
    {
        if (Status is EmailStatus.Sent or EmailStatus.Resolved)
        {
            return false;
        }

        if (ClaimedAt is not null && now - ClaimedAt.Value < lease)
        {
            return false;
        }

        ClaimedAt = now;
        return true;
    }

    /// <summary>Marks the claimed delivery as successfully sent.</summary>
    /// <param name="sentAt">The sent at.</param>
    public void MarkSent(DateTimeOffset sentAt)
    {
        Status = EmailStatus.Sent;
        SentAt = sentAt > SentAt ? sentAt : SentAt;
        ClaimedAt = null;
    }

    /// <summary>Marks the claimed delivery as failed while retaining it for staff retry.</summary>
    /// <param name="failedAt">The failed at.</param>
    public void MarkFailed(DateTimeOffset failedAt)
    {
        Status = EmailStatus.Failed;
        SentAt = failedAt > SentAt ? failedAt : SentAt;
        ClaimedAt = null;
    }

    /// <summary>Marks an outstanding attempt as superseded by a newer durable retry attempt.</summary>
    /// <param name="resolvedAt">The resolved at.</param>
    public void MarkResolved(DateTimeOffset resolvedAt)
    {
        Guard.Against(
            Status is EmailStatus.Sent or EmailStatus.Resolved,
            "Only an outstanding email delivery can be resolved.");
        Status = EmailStatus.Resolved;
        SentAt = resolvedAt > SentAt ? resolvedAt : SentAt;
        ClaimedAt = null;
    }

    private static EmailLog PendingOrRecorded(
        Guid id,
        Guid attendeeId,
        EmailTemplate templateName,
        DateTimeOffset sentAt,
        EmailStatus status,
        Guid? inviteId,
        Guid? bookingId,
        Guid? eventId)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(attendeeId == Guid.Empty, "attendeeId must not be empty.");

        return new EmailLog
        {
            Id = id,
            AttendeeId = attendeeId,
            TemplateName = templateName,
            SentAt = sentAt,
            Status = status,
            InviteId = inviteId,
            BookingId = bookingId,
            EventId = eventId,
        };
    }
}
