using System.Text.Json.Serialization;
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Domain.Attendees;

namespace EventBooking.Api.Contracts;

/// <summary>Attendee list data plus safe follow-up operations.</summary>
public sealed record AttendeeResourceResponse(
    /// <summary>Gets the stable Attendee identifier.</summary>
    Guid AttendeeId,
    /// <summary>Gets the Attendee full name.</summary>
    string Name,
    /// <summary>Gets the Attendee contact email address.</summary>
    string Email,
    /// <summary>Gets the assigned Attendee Group identifier.</summary>
    Guid AttendeeGroupId,
    /// <summary>Gets the canonical Attendee Group code.</summary>
    string AttendeeGroupCode,
    /// <summary>Gets the Attendee Group display name.</summary>
    string AttendeeGroupName,
    /// <summary>Gets the Appointment Types currently required by the Attendee.</summary>
    IReadOnlyList<AppointmentTypeSummary> RequiredAppointmentTypes,
    /// <summary>Gets where the Attendee sits in the invite and booking lifecycle.</summary>
    AttendeeStatus Status,
    /// <summary>Gets the Coordinator-facing wording for the Attendee status.</summary>
    string StatusDisplay,
    /// <summary>Gets the safe follow-up operations for the Attendee.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one list item into its hypermedia resource.</summary>
    /// <param name="item">The application list item to project.</param>
    /// <returns>The API resource with Attendee workflow links.</returns>
    public static AttendeeResourceResponse From(AttendeeListItem item) =>
        new(item.AttendeeId, item.Name, item.Email, item.AttendeeGroupId,
            item.AttendeeGroupCode, item.AttendeeGroupName,
            item.RequiredAppointmentTypes,
            item.Status, item.StatusDisplay,
            AttendeeLinks.ForAttendee(item.AttendeeId, item.Status));
}

/// <summary>Coordinator-facing delivery outcome for one started recovery invite.</summary>
public sealed record StartRecoveryResourceResponse(
    /// <summary>Gets the new recovery Invite identifier, or empty when awaiting availability.</summary>
    Guid InviteId,
    /// <summary>Gets the recoverable snapshot offered, or awaiting availability.</summary>
    IReadOnlyList<Guid> AppointmentTypeIds,
    /// <summary>Gets whether the post-commit provider attempt completed successfully.</summary>
    bool EmailSent,
    /// <summary>Gets the safe follow-up operations for the recovery invite.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>Coordinator-facing outcome of cancelling one attendee booking.</summary>
public sealed record CancelAttendeeBookingResourceResponse(
    /// <summary>Gets whether this call only previews the cancellation's consequence.</summary>
    bool ConfirmationRequired,
    /// <summary>Gets how many active bookings the attendee holds (preview only).</summary>
    int ActiveBookingCount,
    /// <summary>Gets the cancelled booking identifier (confirmed call only).</summary>
    Guid? CancelledBookingId,
    /// <summary>Gets the safe follow-up operations for the Attendee.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One active Booking a Coordinator may cancel; carries no management token.</summary>
public sealed record AttendeeBookingResourceResponse(
    /// <summary>Gets the Booking identifier used to target a cancellation.</summary>
    Guid BookingId,
    /// <summary>Gets whether this is the original Booking rather than an active recovery Booking.</summary>
    bool IsOriginal,
    /// <summary>Gets the date of the Confirmed Event window the Booking holds.</summary>
    DateOnly EventDate,
    /// <summary>Gets the start of the Confirmed Event window the Booking holds.</summary>
    TimeOnly EventStartTime,
    /// <summary>Gets the end of the Confirmed Event window the Booking holds.</summary>
    TimeOnly EventEndTime,
    /// <summary>Gets the safe follow-up operations for the Booking.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one booking summary into its hypermedia resource.</summary>
    /// <param name="attendeeId">The owning Attendee identifier.</param>
    /// <param name="row">The application booking summary to project.</param>
    /// <returns>The API resource with the cancellation link.</returns>
    public static AttendeeBookingResourceResponse From(Guid attendeeId, AttendeeBookingSummary row) =>
        new(row.BookingId, row.IsOriginal, row.EventDate, row.EventStartTime, row.EventEndTime,
            AttendeeLinks.ForBooking(attendeeId, row.BookingId, row.IsOriginal));
}

/// <summary>Minimum canonical detail for one incomplete appointment type.</summary>
public sealed record OutstandingAppointmentTypeResourceResponse(
    /// <summary>Gets the canonical Appointment Type code.</summary>
    string Code,
    /// <summary>Gets the canonical Appointment Type name.</summary>
    string Name,
    /// <summary>Gets whether recovery can currently be started for this type.</summary>
    bool IsRecoverable);

/// <summary>Coordinator-facing readiness for one attendee.</summary>
public sealed record AttendeeReadinessResourceResponse(
    /// <summary>Gets the stable Attendee identifier.</summary>
    Guid AttendeeId,
    /// <summary>Gets the stable machine-readable readiness reason.</summary>
    string Code,
    /// <summary>Gets the Coordinator-facing explanation.</summary>
    string Display,
    /// <summary>Gets minimum canonical detail for incomplete current Appointment Types.</summary>
    IReadOnlyList<OutstandingAppointmentTypeResourceResponse> OutstandingAppointmentTypes,
    /// <summary>Gets the safe follow-up operations for the Attendee.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>Attendee-facing invite options plus the confirm affordance.</summary>
public sealed record InviteResourceResponse(
    /// <summary>Gets the Invite identifier shown to the Attendee.</summary>
    Guid InviteId,
    /// <summary>Gets the Attendee name shown on the invite.</summary>
    string AttendeeName,
    /// <summary>Gets the Appointment Type names offered by the invite.</summary>
    IReadOnlyList<string> AppointmentTypeNames,
    /// <summary>Gets the usable future appointment options.</summary>
    IReadOnlyList<InviteOptionView> Options,
    /// <summary>Gets whether the invite is a missed-appointment recovery.</summary>
    bool IsRecovery,
    /// <summary>Gets the safe follow-up operations for the invite token.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one invite view into its hypermedia resource.</summary>
    /// <param name="view">The application invite view to project.</param>
    /// <param name="token">The raw invite token used only to build the confirm link.</param>
    /// <returns>The API resource with the confirm link.</returns>
    public static InviteResourceResponse From(InviteView view, string token) =>
        new(view.InviteId, view.AttendeeName, view.AppointmentTypeNames,
            view.Options, view.IsRecovery, AttendeeLinks.ForInviteToken(token));
}

/// <summary>Attendee-facing managed booking plus the cancel affordance.</summary>
public sealed record ManagedBookingResourceResponse(
    /// <summary>Gets the date of the Confirmed Event window the Booking holds.</summary>
    DateOnly Date,
    /// <summary>Gets the start of the Confirmed Event window the Booking holds.</summary>
    TimeOnly StartTime,
    /// <summary>Gets the end of the Confirmed Event window the Booking holds.</summary>
    TimeOnly EndTime,
    /// <summary>Gets the Attendee-facing window description.</summary>
    string Display,
    /// <summary>Gets the Attendee name shown on the booking.</summary>
    string AttendeeName,
    /// <summary>Gets the safe follow-up operations for the manage token.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one booking view into its hypermedia resource.</summary>
    /// <param name="view">The application booking view to project.</param>
    /// <param name="token">The raw manage token used only to build the cancel link.</param>
    /// <returns>The API resource with the cancel link.</returns>
    public static ManagedBookingResourceResponse From(BookingView view, string token) =>
        new(view.Date, view.StartTime, view.EndTime, view.Display,
            view.AttendeeName, AttendeeLinks.ForManageToken(token));
}

/// <summary>Builds Attendee relations from stable operation IDs.</summary>
public static class AttendeeLinks
{
    /// <summary>Builds the workflow links for one Attendee list resource.</summary>
    /// <param name="attendeeId">The stable Attendee identifier.</param>
    /// <param name="status">Where the Attendee sits in the invite and booking lifecycle.</param>
    /// <returns>The safe follow-up operations keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForAttendee(Guid attendeeId, AttendeeStatus status)
    {
        var links = new Dictionary<string, ApiLink>
        {
            ["bookings"] = new($"/api/attendees/{attendeeId}/bookings", "GET", "listAttendeeBookings"),
            ["readiness"] = new($"/api/attendees/{attendeeId}/readiness", "GET", "getAttendeeReadiness"),
            ["audit"] = new($"/api/audit/attendee/{attendeeId}", "GET", "getAttendeeAuditHistory"),
            ["update"] = new($"/api/attendees/{attendeeId}", "PUT", "updateAttendee"),
            ["delete"] = new($"/api/attendees/{attendeeId}", "DELETE", "deleteAttendee"),
            ["emailRetry"] = new($"/api/attendees/{attendeeId}/email-retry", "POST", "retryAttendeeEmail"),
        };
        if (status is AttendeeStatus.NotYetInvited or AttendeeStatus.AwaitingAvailability
            or AttendeeStatus.NoResponseNeedsFollowUp)
        {
            links["invite"] = new($"/api/attendees/{attendeeId}/invite", "POST", "triggerAttendeeInvite");
        }

        return links;
    }

    /// <summary>Builds the cancellation link for one Attendee booking.</summary>
    /// <param name="attendeeId">The owning Attendee identifier.</param>
    /// <param name="bookingId">The Booking identifier used to target a cancellation.</param>
    /// <param name="isOriginal">Whether this is the original Booking; described in the cancel request, not the URL.</param>
    /// <returns>The cancel relation for the booking.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForBooking(Guid attendeeId, Guid bookingId, bool isOriginal) =>
        new Dictionary<string, ApiLink>
        {
            ["cancel"] = new($"/api/attendees/{attendeeId}/bookings/{bookingId}/cancel", "POST", "cancelAttendeeBooking"),
        };

    /// <summary>Builds the workflow links for one readiness response.</summary>
    /// <param name="attendeeId">The stable Attendee identifier.</param>
    /// <param name="hasRecoverableType">Whether any outstanding type can currently be recovered.</param>
    /// <returns>The bookings and readiness relations, plus startRecovery when recoverable.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForReadiness(Guid attendeeId, bool hasRecoverableType)
    {
        var links = new Dictionary<string, ApiLink>
        {
            ["bookings"] = new($"/api/attendees/{attendeeId}/bookings", "GET", "listAttendeeBookings"),
            ["readiness"] = new($"/api/attendees/{attendeeId}/readiness", "GET", "getAttendeeReadiness"),
        };
        if (hasRecoverableType)
        {
            links["startRecovery"] = new($"/api/attendees/{attendeeId}/recovery-invites", "POST", "startRecoveryInvite");
        }

        return links;
    }

    /// <summary>Builds the confirm link for one anonymous invite token.</summary>
    /// <param name="token">The raw invite token embedded only in the confirm href.</param>
    /// <returns>The confirm relation for the invite.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForInviteToken(string token) =>
        new Dictionary<string, ApiLink>
        {
            ["confirm"] = new($"/api/booking/{Uri.EscapeDataString(token)}/confirm", "POST", "confirmBooking"),
        };

    /// <summary>Builds the cancel link for one anonymous manage token.</summary>
    /// <param name="token">The raw manage token embedded only in the cancel href.</param>
    /// <returns>The cancel relation for the managed booking.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForManageToken(string token) =>
        new Dictionary<string, ApiLink>
        {
            ["cancel"] = new($"/api/booking/manage/{Uri.EscapeDataString(token)}/cancel", "POST", "cancelManagedBooking"),
        };
}
