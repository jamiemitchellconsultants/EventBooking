using EventBooking.Domain.Notifications;

namespace EventBooking.Application.SelfRegistrations;

/// <summary>Stages self-registration emails.</summary>
public static class SelfRegistrationDelivery
{
    /// <summary>Stages the confirmation-link delivery for a pending request.</summary>
    /// <param name="deliveryId">The new delivery identifier.</param>
    /// <param name="requestId">The pending request the link confirms.</param>
    /// <param name="now">The submission instant.</param>
    /// <param name="correlationId">The identifier of the work staging the row.</param>
    public static EmailLog StageLink(
        Guid deliveryId, Guid requestId, DateTimeOffset now, string correlationId)
    {
        var delivery = EmailLog.RecordPendingSelfRegistration(deliveryId, requestId, now);
        delivery.StampCorrelation(correlationId);
        return delivery;
    }

    /// <summary>Stages the usual booking confirmation, which carries the manage link.</summary>
    /// <param name="deliveryId">The new delivery identifier.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="bookingId">The new booking identifier.</param>
    /// <param name="now">The confirmation instant.</param>
    /// <param name="correlationId">The identifier of the work staging the row.</param>
    public static EmailLog StageBookingConfirmation(
        Guid deliveryId, Guid attendeeId, Guid bookingId, DateTimeOffset now, string correlationId)
    {
        var delivery = EmailLog.RecordPending(
            deliveryId, attendeeId, EmailTemplate.BookingConfirmation, now, bookingId: bookingId);
        delivery.StampCorrelation(correlationId);
        return delivery;
    }
}
