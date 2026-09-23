namespace EventBooking.Application.Notifications;

/// <summary>
/// The handful of deployment-specific strings the attendee-facing emails and pages need. Bound
/// from configuration in Task 55 and injected as a singleton.
/// </summary>
/// <param name="BaseUrl">The base url.</param>
/// <param name="TransitionalLocationAddress">The transitional location address.</param>
/// <param name="CoordinatorContact">The coordinator contact.</param>
public sealed record AttendeePortalOptions(
    string BaseUrl,
    string TransitionalLocationAddress,
    string CoordinatorContact);
