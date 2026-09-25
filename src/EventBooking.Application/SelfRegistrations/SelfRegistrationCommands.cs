namespace EventBooking.Application.SelfRegistrations;

/// <summary>Submits an anonymous request to join an event.</summary>
/// <param name="EventGroupId">The event group id.</param>
/// <param name="EventId">The requested event id.</param>
/// <param name="AttendeeGroupId">The requested attendee group id.</param>
/// <param name="Name">The requester's name.</param>
/// <param name="Email">The requester's email.</param>
public sealed record SubmitSelfRegistrationCommand(
    Guid EventGroupId, Guid EventId, Guid AttendeeGroupId, string? Name, string? Email);

/// <summary>The submitted request echo with its confirmation token.</summary>
/// <param name="RequestId">The request id.</param>
/// <param name="EventGroupId">The event group id.</param>
/// <param name="EventId">The requested event id.</param>
/// <param name="AttendeeGroupId">The requested attendee group id.</param>
/// <param name="Name">The requester's name as submitted.</param>
/// <param name="Email">The requester's email as submitted.</param>
/// <param name="ExpiresAt">When the request stops being confirmable.</param>
/// <param name="ConfirmationToken">The signed confirmation token.</param>
public sealed record SubmitSelfRegistrationResult(
    Guid RequestId, Guid EventGroupId, Guid EventId, Guid AttendeeGroupId,
    string Name, string Email, DateTimeOffset ExpiresAt, string ConfirmationToken);
