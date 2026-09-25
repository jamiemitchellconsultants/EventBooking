using EventBooking.Application.Common;

namespace EventBooking.Application.SelfRegistrations;

/// <summary>Self-registration failures with a stable public shape.</summary>
public static class SelfRegistrationErrors
{
    /// <summary>The group or membership gate is closed.</summary>
    public static Error NotOpen() =>
        Error.Validation("Registration is not open for this group or event.");

    /// <summary>The event is gone, cancelled or already started.</summary>
    public static Error EventUnavailable() =>
        Error.Validation("The event is no longer available for registration.");

    /// <summary>The group choice is not selectable in this group.</summary>
    public static Error GroupNotSelectable() =>
        Error.Validation("The attendee group cannot be selected in this event group.");

    /// <summary>The same email already has a different in-flight request for this event.</summary>
    public static Error DuplicateRequest() =>
        Error.Validation("This email already has a different pending request for this event.");

    /// <summary>The confirmation link names nothing confirmable.</summary>
    public static Error LinkInvalid() =>
        Error.NotFound("The confirmation link is invalid or has expired.");
}
