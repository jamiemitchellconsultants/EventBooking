namespace EventBooking.Application.Notifications;

/// <summary>
/// The handful of deployment-specific strings the candidate-facing emails and pages need. Bound
/// from configuration in Task 55 and injected as a singleton.
/// </summary>
/// <param name="BaseUrl">The base url.</param>
/// <param name="HeadOfficeAddress">The head office address.</param>
/// <param name="CoordinatorContact">The coordinator contact.</param>
public sealed record CandidatePortalOptions(
    string BaseUrl,
    string HeadOfficeAddress,
    string CoordinatorContact);
