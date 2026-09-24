namespace EventBooking.Web.Services;

/// <summary>Maps the API's raw attendee status value to the attendee-list visual state.</summary>
internal static class AttendeePresentation
{
    // AttendeeStatus is serialized as its name across the API boundary.
    internal static string StatusCssClass(string status) => status switch
    {
        "NotYetInvited" => "status-new",
        "AwaitingAvailability" or "NoResponseNeedsFollowUp" => "status-warning",
        "Booked" => "status-success",
        "Invited" => "status-neutral",
        _ => "status-neutral",
    };

    /// <summary>Maps the API's readiness code to the attendee-list badge style.</summary>
    internal static string ReadinessCssClass(string code) => code switch
    {
        "Ready" => "status-success",
        "RequirementSnapshotMismatch" or "AppointmentsOutstanding" => "status-warning",
        "NoActiveBooking" => "status-neutral",
        _ => "status-neutral",
    };

    /// <summary>Maps the API's readiness code to its badge glyph.</summary>
    internal static string ReadinessIcon(string code) => code switch
    {
        "Ready" => "✓",
        "NoActiveBooking" => "○",
        "RequirementSnapshotMismatch" => "≠",
        "AppointmentsOutstanding" => "•",
        _ => "?",
    };
}
