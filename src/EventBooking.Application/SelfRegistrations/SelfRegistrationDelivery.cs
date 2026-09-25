using EventBooking.Domain.Notifications;

namespace EventBooking.Application.SelfRegistrations;

/// <summary>Stages the self-registration confirmation email.</summary>
public static class SelfRegistrationDelivery
{
    /// <summary>Stages a pending confirmation delivery keyed to the new booking.</summary>
    /// <param name="deliveryId">The new delivery identifier.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="bookingId">The new booking identifier.</param>
    /// <param name="now">The confirmation instant.</param>
    /// <param name="correlationId">The identifier of the work staging the row.</param>
    public static EmailLog StageConfirmation(
        Guid deliveryId, Guid attendeeId, Guid bookingId, DateTimeOffset now, string correlationId)
    {
        var delivery = EmailLog.RecordPending(
            deliveryId, attendeeId, EmailTemplate.SelfRegistrationConfirmation, now,
            bookingId: bookingId);
        delivery.StampCorrelation(correlationId);
        delivery.SetNotBefore(JitteredRetryAtUtc(now, deliveryId));
        return delivery;
    }

    /// <summary>Spreads first dispatch deterministically from the delivery identifier.</summary>
    /// <param name="now">The confirmation instant.</param>
    /// <param name="deliveryId">The delivery identifier.</param>
    public static DateTimeOffset JitteredRetryAtUtc(DateTimeOffset now, Guid deliveryId)
    {
        var bytes = deliveryId.ToByteArray();
        var seconds = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 30;
        return now.AddSeconds(seconds);
    }
}
