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
    /// <summary>Gets where the Attendee sits in the invite and booking lifecycle.</summary>
    string Status,
    /// <summary>Gets the Coordinator-facing wording for the Attendee status.</summary>
    string StatusDisplay,
    /// <summary>Gets the canonical Attendee Group code.</summary>
    string GroupCode,
    /// <summary>Gets the readiness label for the Attendee.</summary>
    string Readiness,
    /// <summary>Gets the required type codes, on awaiting-availability rows only.</summary>
    IReadOnlyList<string> RequiredTypeCodes,
    /// <summary>Gets the latest delivery status, or null when never invited.</summary>
    string? LatestDeliveryStatus,
    /// <summary>Gets the row's page cursor.</summary>
    string Cursor,
    /// <summary>Gets the latest delivery's id, which the retry operation needs.</summary>
    Guid? LatestDeliveryId,
    /// <summary>Gets the safe follow-up operations for the Attendee.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one list item into its hypermedia resource.</summary>
    /// <param name="item">The application list item to project.</param>
    /// <returns>The API resource with Attendee workflow links.</returns>
    public static AttendeeResourceResponse From(AttendeeListItem item)
    {
        var status = Enum.Parse<AttendeeStatus>(item.Status);
        return new(
            item.AttendeeId, item.Name, item.Email,
            item.Status, DisplayOf(status),
            item.GroupCode,
            item.Readiness,
            item.RequiredTypeCodes,
            item.LatestDeliveryStatus,
            item.Cursor,
            item.LatestDeliveryId,
            AttendeeLinks.ForAttendee(item.AttendeeId, status));
    }

    /// <summary>The Coordinator-facing wording for each status.</summary>
    /// <param name="status">The status.</param>
    public static string DisplayOf(AttendeeStatus status) => status switch
    {
        AttendeeStatus.NotYetInvited => "Not yet invited",
        AttendeeStatus.AwaitingAvailability => "Awaiting availability",
        AttendeeStatus.Invited => "Invited (pending response)",
        AttendeeStatus.Booked => "Booked",
        AttendeeStatus.NoResponseNeedsFollowUp => "No response - needs follow-up",
        _ => status.ToString(),
    };
}

/// <summary>One attendee-list page with conditional next-page affordance.</summary>
public sealed record AttendeeListResourceResponse(
    /// <summary>Gets the attendee rows for the requested page.</summary>
    IReadOnlyList<AttendeeResourceResponse> Items,
    /// <summary>Gets the opaque cursor for the following page, or null when exhausted.</summary>
    string? NextCursor,
    /// <summary>Gets the self relation plus next while paging continues.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one attendee-list page into its hypermedia resource.</summary>
    /// <param name="page">The application list page to project.</param>
    /// <param name="currentQuery">The raw incoming query string, used verbatim for the self href.</param>
    /// <returns>The API resource with list links.</returns>
    public static AttendeeListResourceResponse From(AttendeeListView page, string currentQuery) =>
        new(page.Items.Select(AttendeeResourceResponse.From).ToList(),
            page.NextCursor,
            AttendeeLinks.ForAttendeeList(currentQuery, page.NextCursor));
}

/// <summary>Coordinator-facing delivery outcome for one started recovery invite.</summary>
public sealed record StartRecoveryResourceResponse(
    /// <summary>Gets the newly issued recovery invite identifier.</summary>
    Guid RecoveryInviteId,
    /// <summary>Gets the locations the recovery invite covers.</summary>
    IReadOnlyList<Guid> LocationIds,
    /// <summary>Gets the recoverable snapshot the recovery invite offers.</summary>
    IReadOnlyList<Guid> RecoverableTypeIds,
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

    /// <summary>Builds the self relation from the incoming query plus a next relation while paging continues.</summary>
    /// <param name="currentQuery">The raw incoming query string, used verbatim for the self href.</param>
    /// <param name="nextCursor">The opaque cursor for the following page, or null when exhausted.</param>
    /// <returns>The attendee-list affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForAttendeeList(string currentQuery, string? nextCursor)
    {
        var links = new Dictionary<string, ApiLink>
        {
            ["self"] = new($"/api/attendees{currentQuery}", "GET", "listAttendees"),
        };
        if (nextCursor is not null)
        {
            links["next"] = new(BuildListNextHref(currentQuery, nextCursor), "GET", "listAttendees");
        }

        return links;
    }

    private static string BuildListNextHref(string currentQuery, string nextCursor)
    {
        var parameters = new List<(string Name, string? Value)>();
        var seenCursor = false;
        if (currentQuery.Length > 1)
        {
            foreach (var pair in currentQuery[1..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var index = pair.IndexOf('=');
                var name = index < 0 ? pair : pair[..index];
                var value = index < 0 ? null : pair[(index + 1)..];
                if (string.Equals(name, "cursor", StringComparison.OrdinalIgnoreCase))
                {
                    seenCursor = true;
                    parameters.Add((name, Uri.EscapeDataString(nextCursor)));
                }
                else
                {
                    parameters.Add((name, value));
                }
            }
        }

        if (!seenCursor)
        {
            parameters.Add(("cursor", Uri.EscapeDataString(nextCursor)));
        }

        return "/api/attendees?" + string.Join(
            "&", parameters.Select(p => p.Value is null ? p.Name : $"{p.Name}={p.Value}"));
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
