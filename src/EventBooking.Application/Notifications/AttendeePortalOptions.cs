namespace EventBooking.Application.Notifications;

/// <summary>
/// The handful of deployment-specific strings the attendee-facing emails and pages need. Bound
/// from configuration at startup and injected as a singleton. Addresses belong to a
/// <c>Location</c>, not to the deployment.
/// </summary>
/// <param name="BaseUrl">The base url.</param>
/// <param name="CoordinatorContact">The coordinator contact.</param>
public sealed record AttendeePortalOptions(
    string BaseUrl,
    string CoordinatorContact);
