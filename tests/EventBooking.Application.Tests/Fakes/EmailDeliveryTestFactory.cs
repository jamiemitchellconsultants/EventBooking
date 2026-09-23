using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventBooking.Application.Tests.Fakes;

/// <summary>Builds the durable delivery coordinator used by application tests.</summary>
public static class EmailDeliveryTestFactory
{
    /// <summary>Creates a coordinator backed by the supplied delivery repository and fakes.</summary>
    public static EmailDeliveryService Create(
        IEmailDeliveryRepository deliveries,
        IEmailSender sender,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<EmailDeliveryService>? logger = null) =>
        new(deliveries, sender, unitOfWork, clock, logger ?? NullLogger<EmailDeliveryService>.Instance);
}
