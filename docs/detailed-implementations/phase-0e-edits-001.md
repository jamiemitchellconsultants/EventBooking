# 00e — Require an attendee group, edits 1 (Task 3c)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — src/EventBooking.Api/Contracts/AttendeeHypermediaResponses.cs — 1/1

<!-- retirement-file: {"id":0,"file":"src/EventBooking.Api/Contracts/AttendeeHypermediaResponses.cs","beforeSha":"f50d69349627b5dd929f2255d87c3d9dd1144e636ac495c9aa5e754a5620b556","afterSha":"646d490b3076350d7a84370a7190c37c2aad612e57dbcdd4c3b4162cff1a1a9b","side":"before","part":1,"parts":1} -->

`````csharp
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
    /// <summary>Gets the assigned Attendee Group identifier, when assigned.</summary>
    Guid? AttendeeGroupId,
    /// <summary>Gets the canonical Attendee Group code, when assigned.</summary>
    string? AttendeeGroupCode,
    /// <summary>Gets the Attendee Group display name, when assigned.</summary>
    string? AttendeeGroupName,
    /// <summary>Gets whether the Attendee needs legacy Attendee Group reconciliation.</summary>
    bool RequiresAttendeeGroupReconciliation,
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
            item.RequiresAttendeeGroupReconciliation, item.RequiredAppointmentTypes,
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
    /// <summary>Gets whether a replacement Invite was created for the Attendee.</summary>
    bool Reinvited,
    /// <summary>Gets the explicit replacement-invite creation state.</summary>
    bool InviteCreated,
    /// <summary>Gets the provider outcome, or Unavailable when no replacement Invite exists.</summary>
    string? DeliveryStatus,
    /// <summary>Gets the durable replacement delivery identifier, when one was staged.</summary>
    Guid? DeliveryId,
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
`````

## after — src/EventBooking.Api/Contracts/AttendeeHypermediaResponses.cs — 1/1

<!-- retirement-file: {"id":0,"file":"src/EventBooking.Api/Contracts/AttendeeHypermediaResponses.cs","beforeSha":"f50d69349627b5dd929f2255d87c3d9dd1144e636ac495c9aa5e754a5620b556","afterSha":"646d490b3076350d7a84370a7190c37c2aad612e57dbcdd4c3b4162cff1a1a9b","side":"after","part":1,"parts":1} -->

`````csharp
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
    /// <summary>Gets whether a replacement Invite was created for the Attendee.</summary>
    bool Reinvited,
    /// <summary>Gets the explicit replacement-invite creation state.</summary>
    bool InviteCreated,
    /// <summary>Gets the provider outcome, or Unavailable when no replacement Invite exists.</summary>
    string? DeliveryStatus,
    /// <summary>Gets the durable replacement delivery identifier, when one was staged.</summary>
    Guid? DeliveryId,
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
`````

## before — src/EventBooking.Api/Endpoints/AttendeeEndpoints.cs — 1/1

<!-- retirement-file: {"id":1,"file":"src/EventBooking.Api/Endpoints/AttendeeEndpoints.cs","beforeSha":"75370e684349a157997a5f93724224ca005b9d53d19e9adbaf6c4ef43178b8c0","afterSha":"ba960666746272a984565af5bd2baa8308bd2110dbff20fd0e452becab24bb24","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Attendees;
using System.Text;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps authorized attendee and delivery-management endpoints.</summary>
public static class AttendeeEndpoints
{
    private const int MaxImportBytes = 1_048_576;
    private const int MaxImportDataRows = 10_000;

    /// <summary>Attendee fields accepted by create and update operations.</summary>
    public sealed record SaveAttendeeRequest(string? Name, string? Email, Guid? AttendeeGroupId);

    /// <summary>The coordinator's choice when cancelling one attendee booking.</summary>
    /// <param name="Rebook">
    /// Whether to issue a replacement invite. Valid only for an original booking; requesting it
    /// for a recovery booking is refused as a conflict.
    /// </param>
    public sealed record CancelAttendeeBookingRequest(bool Rebook);

    /// <summary>Registers attendee CRUD, invite, and template-aware retry routes.</summary>
    public static IEndpointRouteBuilder MapAttendeeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/attendees")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("/", async (
            AttendeeStatus? status,
            string? search,
            ICallerAccessor caller,
            ListAttendeesHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new ListAttendeesQuery(caller.RequireStaffUserId(), status, search), cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            return Results.Ok(result.Value.Select(AttendeeResourceResponse.From).ToList());
        })
            .WithAgentMetadata("listAttendees")
            .Produces(200)
            .ProducesProblem(403);

        group.MapPost("/", async (
            SaveAttendeeRequest request,
            ICallerAccessor caller,
            SaveAttendeeHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.CreateAsync(
                new CreateAttendeeCommand(
                    caller.RequireStaffUserId(), request.Name, request.Email, request.AttendeeGroupId),
                cancellationToken))
                .ToCreated(id => $"/api/attendees/{id}"))
            .WithAgentMetadata("createAttendee")
            .Produces<Guid>(201)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(409);

        group.MapPut("/{id:guid}", async (
            Guid id,
            SaveAttendeeRequest request,
            ICallerAccessor caller,
            SaveAttendeeHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.UpdateAsync(
                new UpdateAttendeeCommand(
                    caller.RequireStaffUserId(), id, request.Name, request.Email, request.AttendeeGroupId),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("updateAttendee")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            bool? confirm,
            ICallerAccessor caller,
            DeleteAttendeeHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new DeleteAttendeeCommand(caller.RequireStaffUserId(), id, confirm ?? false),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("deleteAttendee")
            .Produces(204)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPost("/import", async (
            HttpRequest httpRequest,
            ICallerAccessor caller,
            ImportAttendeesHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (!HasCsvContentType(httpRequest.ContentType))
            {
                return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
            }

            if (httpRequest.ContentLength > MaxImportBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            var csv = await ReadAtMostAsync(httpRequest.Body, cancellationToken);
            if (csv is null)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            if (CountDataRows(csv) > MaxImportDataRows)
            {
                return Result.Failure(Error.Validation(
                    $"The import contains more than {MaxImportDataRows:N0} data rows.")).ToResponse();
            }

            return (await handler.HandleAsync(
                new ImportAttendeesCommand(caller.RequireStaffUserId(), csv), cancellationToken))
                .ToResponse();
        })
            .WithAgentMetadata("importAttendees")
            .Accepts<string>("text/csv")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .Produces(413)
            .Produces(415);

        group.MapPost("/{id:guid}/invite", async (
            Guid id,
            ICallerAccessor caller,
            TriggerInviteHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new TriggerInviteCommand(caller.RequireStaffUserId(), id), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("triggerAttendeeInvite")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPost("/{id:guid}/email-retry", async (
            Guid id,
            ICallerAccessor caller,
            RetryEmailHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new RetryEmailCommand(caller.RequireStaffUserId(), id), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("retryAttendeeEmail")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPost("/{attendeeId:guid}/recovery-invites", async (
            Guid attendeeId,
            ICallerAccessor caller,
            StartRecoveryHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new StartRecoveryCommand(caller.RequireStaffUserId(), attendeeId),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var outcome = result.Value;
            return Results.Ok(new StartRecoveryResourceResponse(
                outcome.InviteId,
                outcome.AppointmentTypeIds,
                outcome.EmailSent,
                new Dictionary<string, ApiLink>()));
        })
            .WithAgentMetadata("startRecoveryInvite")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapDelete("/{attendeeId:guid}/recovery-invites/{inviteId:guid}", async (
            Guid attendeeId,
            Guid inviteId,
            ICallerAccessor caller,
            CancelRecoveryInviteHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new CancelRecoveryInviteCommand(
                     caller.RequireStaffUserId(), attendeeId, inviteId),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("cancelRecoveryInvite")
            .Produces(204)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPost("/{attendeeId:guid}/bookings/{bookingId:guid}/cancel", async (
            Guid attendeeId,
            Guid bookingId,
            CancelAttendeeBookingRequest request,
            ICallerAccessor caller,
            CancelAttendeeBookingHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CancelAttendeeBookingCommand(
                    caller.RequireStaffUserId(), attendeeId, bookingId, request.Rebook),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var outcome = result.Value;
            return Results.Ok(new CancelAttendeeBookingResourceResponse(
                outcome.Reinvited,
                outcome.InviteCreated,
                outcome.DeliveryStatus,
                outcome.DeliveryId,
                AttendeeLinks.ForAttendee(attendeeId, AttendeeStatus.Booked)));
        })
            .WithAgentMetadata("cancelAttendeeBooking")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapGet("/{attendeeId:guid}/bookings", async (
            Guid attendeeId,
            ICallerAccessor caller,
            GetAttendeeBookingsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetAttendeeBookingsQuery(caller.RequireStaffUserId(), attendeeId),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            return Results.Ok(result.Value
                .Select(row => AttendeeBookingResourceResponse.From(attendeeId, row))
                .ToList());
        })
            .WithAgentMetadata("listAttendeeBookings")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapGet("/{attendeeId:guid}/readiness", async (
            Guid attendeeId,
            ICallerAccessor caller,
            GetAttendeeReadinessHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetAttendeeReadinessQuery(caller.RequireStaffUserId(), attendeeId),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var readiness = result.Value;
            return Results.Ok(new AttendeeReadinessResourceResponse(
                readiness.AttendeeId,
                readiness.Code.ToString(),
                DisplayFor(readiness.Code),
                readiness.OutstandingAppointmentTypes
                    .Select(type => new OutstandingAppointmentTypeResourceResponse(
                        type.Code,
                        type.Name,
                        type.IsRecoverable))
                    .ToList(),
                AttendeeLinks.ForReadiness(
                    attendeeId,
                    readiness.OutstandingAppointmentTypes.Any(type => type.IsRecoverable))));
        })
            .WithAgentMetadata("getAttendeeReadiness")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        return app;
    }

    /// <summary>Gets the Coordinator-facing display wording for a readiness code.</summary>
    /// <param name="code">The readiness code to describe.</param>
    /// <returns>The display wording shared by REST and MCP transports.</returns>
    public static string DisplayForTool(AttendeeReadinessCode code) => DisplayFor(code);

    private static string DisplayFor(AttendeeReadinessCode code) => code switch
    {
        AttendeeReadinessCode.Ready => "Ready",
        AttendeeReadinessCode.AttendeeGroupUnassigned => "Needs attendee group",
        AttendeeReadinessCode.NoActiveBooking => "No active booking",
        AttendeeReadinessCode.RequirementSnapshotMismatch => "Requirements changed",
        AttendeeReadinessCode.AppointmentsOutstanding => "Appointments outstanding",
        _ => code.ToString(),
    };

    /// <summary>Registers the read-only Attendee Group reference route.</summary>
    public static IEndpointRouteBuilder MapAttendeeGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/attendee-groups")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("/", async (
            ICallerAccessor caller,
            ListAttendeeGroupsHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new ListAttendeeGroupsQuery(caller.RequireStaffUserId()), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("listAttendeeGroups")
            .Produces(200)
            .ProducesProblem(403);

        return app;
    }

    private static bool HasCsvContentType(string? contentType) =>
        string.Equals(
            contentType?.Split(';', 2)[0].Trim(),
            "text/csv",
            StringComparison.OrdinalIgnoreCase);

    private static async Task<string?> ReadAtMostAsync(Stream body, CancellationToken cancellationToken)
    {
        var chunk = new byte[81_920];
        await using var buffer = new MemoryStream();

        while (true)
        {
            var read = await body.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaxImportBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        buffer.Position = 0;
        using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static int CountDataRows(string csv)
    {
        using var reader = new StringReader(csv);
        _ = reader.ReadLine();

        var count = 0;
        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line) && ++count > MaxImportDataRows)
            {
                return count;
            }
        }

        return count;
    }
}
`````

## after — src/EventBooking.Api/Endpoints/AttendeeEndpoints.cs — 1/1

<!-- retirement-file: {"id":1,"file":"src/EventBooking.Api/Endpoints/AttendeeEndpoints.cs","beforeSha":"75370e684349a157997a5f93724224ca005b9d53d19e9adbaf6c4ef43178b8c0","afterSha":"ba960666746272a984565af5bd2baa8308bd2110dbff20fd0e452becab24bb24","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Attendees;
using System.Text;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps authorized attendee and delivery-management endpoints.</summary>
public static class AttendeeEndpoints
{
    private const int MaxImportBytes = 1_048_576;
    private const int MaxImportDataRows = 10_000;

    /// <summary>Attendee fields accepted by create and update operations.</summary>
    public sealed record SaveAttendeeRequest(string? Name, string? Email, Guid? AttendeeGroupId);

    /// <summary>The coordinator's choice when cancelling one attendee booking.</summary>
    /// <param name="Rebook">
    /// Whether to issue a replacement invite. Valid only for an original booking; requesting it
    /// for a recovery booking is refused as a conflict.
    /// </param>
    public sealed record CancelAttendeeBookingRequest(bool Rebook);

    /// <summary>Registers attendee CRUD, invite, and template-aware retry routes.</summary>
    public static IEndpointRouteBuilder MapAttendeeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/attendees")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("/", async (
            AttendeeStatus? status,
            string? search,
            ICallerAccessor caller,
            ListAttendeesHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new ListAttendeesQuery(caller.RequireStaffUserId(), status, search), cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            return Results.Ok(result.Value.Select(AttendeeResourceResponse.From).ToList());
        })
            .WithAgentMetadata("listAttendees")
            .Produces(200)
            .ProducesProblem(403);

        group.MapPost("/", async (
            SaveAttendeeRequest request,
            ICallerAccessor caller,
            SaveAttendeeHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.CreateAsync(
                new CreateAttendeeCommand(
                    caller.RequireStaffUserId(), request.Name, request.Email, request.AttendeeGroupId),
                cancellationToken))
                .ToCreated(id => $"/api/attendees/{id}"))
            .WithAgentMetadata("createAttendee")
            .Produces<Guid>(201)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(409);

        group.MapPut("/{id:guid}", async (
            Guid id,
            SaveAttendeeRequest request,
            ICallerAccessor caller,
            SaveAttendeeHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.UpdateAsync(
                new UpdateAttendeeCommand(
                    caller.RequireStaffUserId(), id, request.Name, request.Email, request.AttendeeGroupId),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("updateAttendee")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            bool? confirm,
            ICallerAccessor caller,
            DeleteAttendeeHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new DeleteAttendeeCommand(caller.RequireStaffUserId(), id, confirm ?? false),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("deleteAttendee")
            .Produces(204)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPost("/import", async (
            HttpRequest httpRequest,
            ICallerAccessor caller,
            ImportAttendeesHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (!HasCsvContentType(httpRequest.ContentType))
            {
                return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
            }

            if (httpRequest.ContentLength > MaxImportBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            var csv = await ReadAtMostAsync(httpRequest.Body, cancellationToken);
            if (csv is null)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            if (CountDataRows(csv) > MaxImportDataRows)
            {
                return Result.Failure(Error.Validation(
                    $"The import contains more than {MaxImportDataRows:N0} data rows.")).ToResponse();
            }

            return (await handler.HandleAsync(
                new ImportAttendeesCommand(caller.RequireStaffUserId(), csv), cancellationToken))
                .ToResponse();
        })
            .WithAgentMetadata("importAttendees")
            .Accepts<string>("text/csv")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .Produces(413)
            .Produces(415);

        group.MapPost("/{id:guid}/invite", async (
            Guid id,
            ICallerAccessor caller,
            TriggerInviteHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new TriggerInviteCommand(caller.RequireStaffUserId(), id), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("triggerAttendeeInvite")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPost("/{id:guid}/email-retry", async (
            Guid id,
            ICallerAccessor caller,
            RetryEmailHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new RetryEmailCommand(caller.RequireStaffUserId(), id), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("retryAttendeeEmail")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPost("/{attendeeId:guid}/recovery-invites", async (
            Guid attendeeId,
            ICallerAccessor caller,
            StartRecoveryHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new StartRecoveryCommand(caller.RequireStaffUserId(), attendeeId),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var outcome = result.Value;
            return Results.Ok(new StartRecoveryResourceResponse(
                outcome.InviteId,
                outcome.AppointmentTypeIds,
                outcome.EmailSent,
                new Dictionary<string, ApiLink>()));
        })
            .WithAgentMetadata("startRecoveryInvite")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapDelete("/{attendeeId:guid}/recovery-invites/{inviteId:guid}", async (
            Guid attendeeId,
            Guid inviteId,
            ICallerAccessor caller,
            CancelRecoveryInviteHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new CancelRecoveryInviteCommand(
                     caller.RequireStaffUserId(), attendeeId, inviteId),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("cancelRecoveryInvite")
            .Produces(204)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPost("/{attendeeId:guid}/bookings/{bookingId:guid}/cancel", async (
            Guid attendeeId,
            Guid bookingId,
            CancelAttendeeBookingRequest request,
            ICallerAccessor caller,
            CancelAttendeeBookingHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CancelAttendeeBookingCommand(
                    caller.RequireStaffUserId(), attendeeId, bookingId, request.Rebook),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var outcome = result.Value;
            return Results.Ok(new CancelAttendeeBookingResourceResponse(
                outcome.Reinvited,
                outcome.InviteCreated,
                outcome.DeliveryStatus,
                outcome.DeliveryId,
                AttendeeLinks.ForAttendee(attendeeId, AttendeeStatus.Booked)));
        })
            .WithAgentMetadata("cancelAttendeeBooking")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapGet("/{attendeeId:guid}/bookings", async (
            Guid attendeeId,
            ICallerAccessor caller,
            GetAttendeeBookingsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetAttendeeBookingsQuery(caller.RequireStaffUserId(), attendeeId),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            return Results.Ok(result.Value
                .Select(row => AttendeeBookingResourceResponse.From(attendeeId, row))
                .ToList());
        })
            .WithAgentMetadata("listAttendeeBookings")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapGet("/{attendeeId:guid}/readiness", async (
            Guid attendeeId,
            ICallerAccessor caller,
            GetAttendeeReadinessHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetAttendeeReadinessQuery(caller.RequireStaffUserId(), attendeeId),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var readiness = result.Value;
            return Results.Ok(new AttendeeReadinessResourceResponse(
                readiness.AttendeeId,
                readiness.Code.ToString(),
                DisplayFor(readiness.Code),
                readiness.OutstandingAppointmentTypes
                    .Select(type => new OutstandingAppointmentTypeResourceResponse(
                        type.Code,
                        type.Name,
                        type.IsRecoverable))
                    .ToList(),
                AttendeeLinks.ForReadiness(
                    attendeeId,
                    readiness.OutstandingAppointmentTypes.Any(type => type.IsRecoverable))));
        })
            .WithAgentMetadata("getAttendeeReadiness")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        return app;
    }

    /// <summary>Gets the Coordinator-facing display wording for a readiness code.</summary>
    /// <param name="code">The readiness code to describe.</param>
    /// <returns>The display wording shared by REST and MCP transports.</returns>
    public static string DisplayForTool(AttendeeReadinessCode code) => DisplayFor(code);

    private static string DisplayFor(AttendeeReadinessCode code) => code switch
    {
        AttendeeReadinessCode.Ready => "Ready",
        AttendeeReadinessCode.NoActiveBooking => "No active booking",
        AttendeeReadinessCode.RequirementSnapshotMismatch => "Requirements changed",
        AttendeeReadinessCode.AppointmentsOutstanding => "Appointments outstanding",
        _ => code.ToString(),
    };

    /// <summary>Registers the read-only Attendee Group reference route.</summary>
    public static IEndpointRouteBuilder MapAttendeeGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/attendee-groups")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("/", async (
            ICallerAccessor caller,
            ListAttendeeGroupsHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new ListAttendeeGroupsQuery(caller.RequireStaffUserId()), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("listAttendeeGroups")
            .Produces(200)
            .ProducesProblem(403);

        return app;
    }

    private static bool HasCsvContentType(string? contentType) =>
        string.Equals(
            contentType?.Split(';', 2)[0].Trim(),
            "text/csv",
            StringComparison.OrdinalIgnoreCase);

    private static async Task<string?> ReadAtMostAsync(Stream body, CancellationToken cancellationToken)
    {
        var chunk = new byte[81_920];
        await using var buffer = new MemoryStream();

        while (true)
        {
            var read = await body.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaxImportBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        buffer.Position = 0;
        using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static int CountDataRows(string csv)
    {
        using var reader = new StringReader(csv);
        _ = reader.ReadLine();

        var count = 0;
        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line) && ++count > MaxImportDataRows)
            {
                return count;
            }
        }

        return count;
    }
}
`````

## before — src/EventBooking.Api/Endpoints/ResultResponses.cs — 1/1

<!-- retirement-file: {"id":2,"file":"src/EventBooking.Api/Endpoints/ResultResponses.cs","beforeSha":"937f172d465e394c852e365168d84c934d2eaa45662045837a751fcd226e4a34","afterSha":"fafda5f874f6c48280e889c03b90f3b1a0e7e70d7d0694b112bff73d43366f0e","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Common;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps application results to the API's HTTP response contract.</summary>
public static class ResultResponses
{
    /// <summary>Returns the HTTP status associated with a machine-readable application error code.</summary>
    public static int StatusCodeFor(string errorCode) => errorCode switch
    {
        "validation" => StatusCodes.Status400BadRequest,
        "attendee_group_required" => StatusCodes.Status400BadRequest,
        "attendee_group_unknown" => StatusCodes.Status400BadRequest,
        "attendee_group_inactive" => StatusCodes.Status400BadRequest,
        "attendee_group_unmapped" => StatusCodes.Status409Conflict,
        "forbidden" => StatusCodes.Status403Forbidden,
        "not_found" => StatusCodes.Status404NotFound,
        "conflict" => StatusCodes.Status409Conflict,
        Error.AppointmentVersionConflictCode => StatusCodes.Status409Conflict,
        Error.AttendeeGroupActiveBookingConflictCode => StatusCodes.Status409Conflict,
        Error.AttendeeReconciliationRequiredCode => StatusCodes.Status409Conflict,
        Error.AttendeeRequirementSnapshotMismatchCode => StatusCodes.Status409Conflict,
        Error.RecoveryNotAvailableCode => StatusCodes.Status409Conflict,
        Error.RecoveryAlreadyPendingCode => StatusCodes.Status409Conflict,
        Error.RecoveryStateChangedCode => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>Returns no content for success or problem details for failure.</summary>
    public static IResult ToResponse(this Result result) =>
        result.IsSuccess ? Results.NoContent() : Problem(result.Error);

    /// <summary>Returns the result value for success or problem details for failure.</summary>
    public static IResult ToResponse<T>(this Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);

    /// <summary>Returns a created result for success or problem details for failure.</summary>
    public static IResult ToCreated<T>(this Result<T> result, Func<T, string> location) =>
        result.IsSuccess
            ? Results.Created(location(result.Value), result.Value)
            : Problem(result.Error);

    private static IResult Problem(Error error) =>
        Results.Problem(
            detail: error.Message,
            statusCode: StatusCodeFor(error.Code),
            title: error.Code);
}
`````

## after — src/EventBooking.Api/Endpoints/ResultResponses.cs — 1/1

<!-- retirement-file: {"id":2,"file":"src/EventBooking.Api/Endpoints/ResultResponses.cs","beforeSha":"937f172d465e394c852e365168d84c934d2eaa45662045837a751fcd226e4a34","afterSha":"fafda5f874f6c48280e889c03b90f3b1a0e7e70d7d0694b112bff73d43366f0e","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Common;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps application results to the API's HTTP response contract.</summary>
public static class ResultResponses
{
    /// <summary>Returns the HTTP status associated with a machine-readable application error code.</summary>
    public static int StatusCodeFor(string errorCode) => errorCode switch
    {
        "validation" => StatusCodes.Status400BadRequest,
        "attendee_group_required" => StatusCodes.Status400BadRequest,
        "attendee_group_unknown" => StatusCodes.Status400BadRequest,
        "attendee_group_inactive" => StatusCodes.Status400BadRequest,
        "attendee_group_unmapped" => StatusCodes.Status409Conflict,
        "forbidden" => StatusCodes.Status403Forbidden,
        "not_found" => StatusCodes.Status404NotFound,
        "conflict" => StatusCodes.Status409Conflict,
        Error.AppointmentVersionConflictCode => StatusCodes.Status409Conflict,
        Error.AttendeeGroupActiveBookingConflictCode => StatusCodes.Status409Conflict,
        Error.AttendeeRequirementSnapshotMismatchCode => StatusCodes.Status409Conflict,
        Error.RecoveryNotAvailableCode => StatusCodes.Status409Conflict,
        Error.RecoveryAlreadyPendingCode => StatusCodes.Status409Conflict,
        Error.RecoveryStateChangedCode => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>Returns no content for success or problem details for failure.</summary>
    public static IResult ToResponse(this Result result) =>
        result.IsSuccess ? Results.NoContent() : Problem(result.Error);

    /// <summary>Returns the result value for success or problem details for failure.</summary>
    public static IResult ToResponse<T>(this Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);

    /// <summary>Returns a created result for success or problem details for failure.</summary>
    public static IResult ToCreated<T>(this Result<T> result, Func<T, string> location) =>
        result.IsSuccess
            ? Results.Created(location(result.Value), result.Value)
            : Problem(result.Error);

    private static IResult Problem(Error error) =>
        Results.Problem(
            detail: error.Message,
            statusCode: StatusCodeFor(error.Code),
            title: error.Code);
}
`````
