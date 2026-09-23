namespace EventBooking.Web.Services;

/// <summary>Maps the API's raw attendee status value to the attendee-list visual state.</summary>
internal static class AttendeePresentation
{
    // AttendeeStatus is serialized as its documented numeric value across the API boundary.
    private const int NotYetInvited = 1;
    private const int AwaitingAvailability = 2;
    private const int Invited = 3;
    private const int Booked = 4;
    private const int NoResponseNeedsFollowUp = 5;

    internal static string StatusCssClass(int rawStatus) => rawStatus switch
    {
        NotYetInvited => "status-new",
        AwaitingAvailability or NoResponseNeedsFollowUp => "status-warning",
        Booked => "status-success",
        Invited => "status-neutral",
        _ => "status-neutral",
    };

    /// <summary>Maps the API's readiness code to the attendee-list badge style.</summary>
    internal static string ReadinessCssClass(string code) => code switch
    {
        "Ready" => "status-success",
        "AttendeeGroupUnassigned" or "RequirementSnapshotMismatch" or "AppointmentsOutstanding" => "status-warning",
        "NoActiveBooking" => "status-neutral",
        _ => "status-neutral",
    };

    /// <summary>Maps the API's readiness code to its badge glyph.</summary>
    internal static string ReadinessIcon(string code) => code switch
    {
        "Ready" => "✓",
        "AttendeeGroupUnassigned" => "!",
        "NoActiveBooking" => "○",
        "RequirementSnapshotMismatch" => "≠",
        "AppointmentsOutstanding" => "•",
        _ => "?",
    };
}
