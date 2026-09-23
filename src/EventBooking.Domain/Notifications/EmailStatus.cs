namespace EventBooking.Domain.Notifications;

/// <summary>Durable outcome of one candidate email delivery attempt.</summary>
public enum EmailStatus
{
    /// <summary>The provider accepted the message for this delivery attempt.</summary>
    Sent = 1,

    /// <summary>The provider rejected the message or the transport reported a failure.</summary>
    Failed = 2,

    /// <summary>The state change committed and the delivery still needs an attempt or retry.</summary>
    Pending = 3,

    /// <summary>A later durable retry attempt superseded this failed or pending attempt.</summary>
    Resolved = 4,
}
