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

    /// <summary>What an anonymous submission sees for a closed gate or unavailable event.</summary>
    public static Error NotAvailable() =>
        Error.NotFound("Registration is not open for this group or event.");

    /// <summary>The selected group has no spare capacity for its required types.</summary>
    public static Error Full() =>
        Error.CapacityExhausted("There is no space left for this attendee group.");

    /// <summary>The confirmation link names nothing confirmable.</summary>
    public static Error LinkInvalid() =>
        Error.NotFound("The confirmation link is invalid or has expired.");
}
