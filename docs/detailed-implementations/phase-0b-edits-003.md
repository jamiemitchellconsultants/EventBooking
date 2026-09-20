# 00b — Vocabulary edits 3 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — src/EventBooking.Api/Contracts/CandidateHypermediaResponses.cs — 1/1

<!-- vocabulary-file: {"id":8,"oldPath":"src/EventBooking.Api/Contracts/CandidateHypermediaResponses.cs","newPath":"src/EventBooking.Api/Contracts/AttendeeHypermediaResponses.cs","beforeSha":"dea80421ae35f8e85f4a58856e39c64ac94c74773e654b409bf4cadb87386668","afterSha":"f50d69349627b5dd929f2255d87c3d9dd1144e636ac495c9aa5e754a5620b556","side":"before","part":1,"parts":1} -->

`````csharp
using System.Text.Json.Serialization;
using EventBooking.Application.Bookings;
using EventBooking.Application.Candidates;
using EventBooking.Domain.Candidates;

namespace EventBooking.Api.Contracts;

/// <summary>Candidate list data plus safe follow-up operations.</summary>
public sealed record CandidateResourceResponse(
    /// <summary>Gets the stable Candidate identifier.</summary>
    Guid CandidateId,
    /// <summary>Gets the Candidate full name.</summary>
    string Name,
    /// <summary>Gets the Candidate contact email address.</summary>
    string Email,
    /// <summary>Gets the assigned Employee Group identifier, when assigned.</summary>
    Guid? EmployeeGroupId,
    /// <summary>Gets the canonical Employee Group code, when assigned.</summary>
    string? EmployeeGroupCode,
    /// <summary>Gets the Employee Group display name, when assigned.</summary>
    string? EmployeeGroupName,
    /// <summary>Gets whether the Candidate needs legacy Employee Group reconciliation.</summary>
    bool RequiresEmployeeGroupReconciliation,
    /// <summary>Gets the Appointment Types currently required by the Candidate.</summary>
    IReadOnlyList<AppointmentTypeSummary> RequiredAppointmentTypes,
    /// <summary>Gets where the Candidate sits in the invite and booking lifecycle.</summary>
    CandidateStatus Status,
    /// <summary>Gets the Coordinator-facing wording for the Candidate status.</summary>
    string StatusDisplay,
    /// <summary>Gets the safe follow-up operations for the Candidate.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one list item into its hypermedia resource.</summary>
    /// <param name="item">The application list item to project.</param>
    /// <returns>The API resource with Candidate workflow links.</returns>
    public static CandidateResourceResponse From(CandidateListItem item) =>
        new(item.CandidateId, item.Name, item.Email, item.EmployeeGroupId,
            item.EmployeeGroupCode, item.EmployeeGroupName,
            item.RequiresEmployeeGroupReconciliation, item.RequiredAppointmentTypes,
            item.Status, item.StatusDisplay,
            CandidateLinks.ForCandidate(item.CandidateId, item.Status));
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

/// <summary>Coordinator-facing outcome of cancelling one candidate booking.</summary>
public sealed record CancelCandidateBookingResourceResponse(
    /// <summary>Gets whether a replacement Invite was created for the Candidate.</summary>
    bool Reinvited,
    /// <summary>Gets the explicit replacement-invite creation state.</summary>
    bool InviteCreated,
    /// <summary>Gets the provider outcome, or Unavailable when no replacement Invite exists.</summary>
    string? DeliveryStatus,
    /// <summary>Gets the durable replacement delivery identifier, when one was staged.</summary>
    Guid? DeliveryId,
    /// <summary>Gets the safe follow-up operations for the Candidate.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One active Booking a Coordinator may cancel; carries no management token.</summary>
public sealed record CandidateBookingResourceResponse(
    /// <summary>Gets the Booking identifier used to target a cancellation.</summary>
    Guid BookingId,
    /// <summary>Gets whether this is the original Booking rather than an active recovery Booking.</summary>
    bool IsOriginal,
    /// <summary>Gets the date of the Confirmed Slot window the Booking holds.</summary>
    DateOnly SlotDate,
    /// <summary>Gets the start of the Confirmed Slot window the Booking holds.</summary>
    TimeOnly SlotStartTime,
    /// <summary>Gets the end of the Confirmed Slot window the Booking holds.</summary>
    TimeOnly SlotEndTime,
    /// <summary>Gets the safe follow-up operations for the Booking.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one booking summary into its hypermedia resource.</summary>
    /// <param name="candidateId">The owning Candidate identifier.</param>
    /// <param name="row">The application booking summary to project.</param>
    /// <returns>The API resource with the cancellation link.</returns>
    public static CandidateBookingResourceResponse From(Guid candidateId, CandidateBookingSummary row) =>
        new(row.BookingId, row.IsOriginal, row.SlotDate, row.SlotStartTime, row.SlotEndTime,
            CandidateLinks.ForBooking(candidateId, row.BookingId, row.IsOriginal));
}

/// <summary>Minimum canonical detail for one incomplete appointment type.</summary>
public sealed record OutstandingAppointmentTypeResourceResponse(
    /// <summary>Gets the canonical Appointment Type code.</summary>
    string Code,
    /// <summary>Gets the canonical Appointment Type name.</summary>
    string Name,
    /// <summary>Gets whether recovery can currently be started for this type.</summary>
    bool IsRecoverable);

/// <summary>Coordinator-facing readiness for one candidate.</summary>
public sealed record CandidateReadinessResourceResponse(
    /// <summary>Gets the stable Candidate identifier.</summary>
    Guid CandidateId,
    /// <summary>Gets the stable machine-readable readiness reason.</summary>
    string Code,
    /// <summary>Gets the Coordinator-facing explanation.</summary>
    string Display,
    /// <summary>Gets minimum canonical detail for incomplete current Appointment Types.</summary>
    IReadOnlyList<OutstandingAppointmentTypeResourceResponse> OutstandingAppointmentTypes,
    /// <summary>Gets the safe follow-up operations for the Candidate.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>Candidate-facing invite options plus the confirm affordance.</summary>
public sealed record InviteResourceResponse(
    /// <summary>Gets the Invite identifier shown to the Candidate.</summary>
    Guid InviteId,
    /// <summary>Gets the Candidate name shown on the invite.</summary>
    string CandidateName,
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
        new(view.InviteId, view.CandidateName, view.AppointmentTypeNames,
            view.Options, view.IsRecovery, CandidateLinks.ForInviteToken(token));
}

/// <summary>Candidate-facing managed booking plus the cancel affordance.</summary>
public sealed record ManagedBookingResourceResponse(
    /// <summary>Gets the date of the Confirmed Slot window the Booking holds.</summary>
    DateOnly Date,
    /// <summary>Gets the start of the Confirmed Slot window the Booking holds.</summary>
    TimeOnly StartTime,
    /// <summary>Gets the end of the Confirmed Slot window the Booking holds.</summary>
    TimeOnly EndTime,
    /// <summary>Gets the Candidate-facing window description.</summary>
    string Display,
    /// <summary>Gets the Candidate name shown on the booking.</summary>
    string CandidateName,
    /// <summary>Gets the safe follow-up operations for the manage token.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one booking view into its hypermedia resource.</summary>
    /// <param name="view">The application booking view to project.</param>
    /// <param name="token">The raw manage token used only to build the cancel link.</param>
    /// <returns>The API resource with the cancel link.</returns>
    public static ManagedBookingResourceResponse From(BookingView view, string token) =>
        new(view.Date, view.StartTime, view.EndTime, view.Display,
            view.CandidateName, CandidateLinks.ForManageToken(token));
}

/// <summary>Builds Candidate relations from stable operation IDs.</summary>
public static class CandidateLinks
{
    /// <summary>Builds the workflow links for one Candidate list resource.</summary>
    /// <param name="candidateId">The stable Candidate identifier.</param>
    /// <param name="status">Where the Candidate sits in the invite and booking lifecycle.</param>
    /// <returns>The safe follow-up operations keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForCandidate(Guid candidateId, CandidateStatus status)
    {
        var links = new Dictionary<string, ApiLink>
        {
            ["bookings"] = new($"/api/candidates/{candidateId}/bookings", "GET", "listCandidateBookings"),
            ["readiness"] = new($"/api/candidates/{candidateId}/readiness", "GET", "getCandidateReadiness"),
            ["audit"] = new($"/api/audit/candidate/{candidateId}", "GET", "getCandidateAuditHistory"),
            ["update"] = new($"/api/candidates/{candidateId}", "PUT", "updateCandidate"),
            ["delete"] = new($"/api/candidates/{candidateId}", "DELETE", "deleteCandidate"),
            ["emailRetry"] = new($"/api/candidates/{candidateId}/email-retry", "POST", "retryCandidateEmail"),
        };
        if (status is CandidateStatus.NotYetInvited or CandidateStatus.AwaitingAvailability
            or CandidateStatus.NoResponseNeedsFollowUp)
        {
            links["invite"] = new($"/api/candidates/{candidateId}/invite", "POST", "triggerCandidateInvite");
        }

        return links;
    }

    /// <summary>Builds the cancellation link for one Candidate booking.</summary>
    /// <param name="candidateId">The owning Candidate identifier.</param>
    /// <param name="bookingId">The Booking identifier used to target a cancellation.</param>
    /// <param name="isOriginal">Whether this is the original Booking; described in the cancel request, not the URL.</param>
    /// <returns>The cancel relation for the booking.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForBooking(Guid candidateId, Guid bookingId, bool isOriginal) =>
        new Dictionary<string, ApiLink>
        {
            ["cancel"] = new($"/api/candidates/{candidateId}/bookings/{bookingId}/cancel", "POST", "cancelCandidateBooking"),
        };

    /// <summary>Builds the workflow links for one readiness response.</summary>
    /// <param name="candidateId">The stable Candidate identifier.</param>
    /// <param name="hasRecoverableType">Whether any outstanding type can currently be recovered.</param>
    /// <returns>The bookings and readiness relations, plus startRecovery when recoverable.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForReadiness(Guid candidateId, bool hasRecoverableType)
    {
        var links = new Dictionary<string, ApiLink>
        {
            ["bookings"] = new($"/api/candidates/{candidateId}/bookings", "GET", "listCandidateBookings"),
            ["readiness"] = new($"/api/candidates/{candidateId}/readiness", "GET", "getCandidateReadiness"),
        };
        if (hasRecoverableType)
        {
            links["startRecovery"] = new($"/api/candidates/{candidateId}/recovery-invites", "POST", "startRecoveryInvite");
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

<!-- vocabulary-file: {"id":8,"oldPath":"src/EventBooking.Api/Contracts/CandidateHypermediaResponses.cs","newPath":"src/EventBooking.Api/Contracts/AttendeeHypermediaResponses.cs","beforeSha":"dea80421ae35f8e85f4a58856e39c64ac94c74773e654b409bf4cadb87386668","afterSha":"f50d69349627b5dd929f2255d87c3d9dd1144e636ac495c9aa5e754a5620b556","side":"after","part":1,"parts":1} -->

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

## before — src/EventBooking.Api/Contracts/OperationsHypermediaResponses.cs — 1/1

<!-- vocabulary-file: {"id":9,"oldPath":"src/EventBooking.Api/Contracts/OperationsHypermediaResponses.cs","newPath":"src/EventBooking.Api/Contracts/OperationsHypermediaResponses.cs","beforeSha":"c0bd039a17b410963c76d1bff3f8e6fe854bab30d50eb9f2f25bb905b6f5281c","afterSha":"72e252efbe22bf25d00c75215519f22e74c28b8d8f3b5a21562bf20f8ba8d6fb","side":"before","part":1,"parts":1} -->

`````csharp
using System.Text.Json.Serialization;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Dashboards;
using EventBooking.Domain.Audit;

namespace EventBooking.Api.Contracts;

/// <summary>One slot-overview row plus its audit and cancel affordances.</summary>
public sealed record SlotOverviewResourceResponse(
    /// <summary>Gets the stable confirmed slot identifier.</summary>
    Guid ConfirmedSlotId,
    /// <summary>Gets the slot window calendar date.</summary>
    DateOnly Date,
    /// <summary>Gets the start of the slot window.</summary>
    TimeOnly StartTime,
    /// <summary>Gets the end of the slot window.</summary>
    TimeOnly EndTime,
    /// <summary>Gets the per-appointment-type capacity rows.</summary>
    IReadOnlyList<SlotCapacityRow> Capacities,
    /// <summary>Gets the aggregate active booking count.</summary>
    int ActiveBookings,
    /// <summary>Gets the audit and cancel affordances for the slot.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one slot-overview row into its hypermedia resource.</summary>
    /// <param name="row">The application slot-overview row to project.</param>
    /// <returns>The API resource with slot-overview links.</returns>
    public static SlotOverviewResourceResponse From(SlotOverviewRow row) =>
        new(row.ConfirmedSlotId, row.Date, row.StartTime, row.EndTime,
            row.Capacities, row.ActiveBookings,
            new Dictionary<string, ApiLink>
            {
                ["audit"] = new($"/api/audit/slot/{row.ConfirmedSlotId}", "GET", "getSlotAuditHistory"),
                ["cancel"] = new($"/api/slots/confirmed/{row.ConfirmedSlotId}", "DELETE", "cancelConfirmedSlot"),
            });
}

/// <summary>Slot-only operations view plus the collection self affordance.</summary>
public sealed record SlotOperationsResourceResponse(
    /// <summary>Gets one row per confirmed slot with capacity and booking counts.</summary>
    IReadOnlyList<SlotOverviewResourceResponse> Slots,
    /// <summary>Gets the collection affordances.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one slot operations view into its hypermedia resource.</summary>
    /// <param name="view">The application slot operations view to project.</param>
    /// <returns>The API resource with operations and row links.</returns>
    public static SlotOperationsResourceResponse From(SlotOperationsView view) =>
        new(view.Slots.Select(SlotOverviewResourceResponse.From).ToList(),
            new Dictionary<string, ApiLink>
            {
                ["self"] = new("/api/slots/operations", "GET", "getSlotOperations"),
                ["board"] = new("/api/slots/board", "GET", "getSlotBoard"),
            });
}

/// <summary>Coordinator dashboards plus entry affordances for related collections.</summary>
public sealed record DashboardResourceResponse(
    /// <summary>Gets the candidates waiting for availability.</summary>
    IReadOnlyList<AwaitingAvailabilityRow> AwaitingAvailability,
    /// <summary>Gets the candidates needing follow-up after no response.</summary>
    IReadOnlyList<NoResponseRow> NoResponse,
    /// <summary>Gets the slot overview rows with capacity and booking counts.</summary>
    IReadOnlyList<SlotOverviewResourceResponse> Slots,
    /// <summary>Gets the latest email delivery state per candidate.</summary>
    IReadOnlyList<CandidateEmailStatusView> EmailStatuses,
    /// <summary>Gets the dashboard self and collection entry affordances.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one dashboards view into its hypermedia resource.</summary>
    /// <param name="view">The application dashboards view to project.</param>
    /// <returns>The API resource with dashboard links.</returns>
    public static DashboardResourceResponse From(DashboardsView view) =>
        new(view.AwaitingAvailability, view.NoResponse,
            view.Slots.Select(SlotOverviewResourceResponse.From).ToList(),
            view.EmailStatuses,
            new Dictionary<string, ApiLink>
            {
                ["self"] = new("/api/dashboards", "GET", "getDashboards"),
                ["candidates"] = new("/api/candidates", "GET", "listCandidates"),
                ["slotOperations"] = new("/api/slots/operations", "GET", "getSlotOperations"),
                ["audit"] = new("/api/audit/search", "GET", "searchAudit"),
            });
}

/// <summary>One audit history row plus a link when it addresses a real candidate or slot route.</summary>
public sealed record AuditHistoryResourceResponse(
    /// <summary>Gets when the audited action was recorded.</summary>
    DateTimeOffset Timestamp,
    /// <summary>Gets the audited entity type name.</summary>
    string EntityType,
    /// <summary>Gets the audited entity identifier.</summary>
    Guid EntityId,
    /// <summary>Gets the audited action name.</summary>
    string Action,
    /// <summary>Gets the actor type name.</summary>
    string ActorType,
    /// <summary>Gets the actor identifier, or null for system actions.</summary>
    string? ActorId,
    /// <summary>Gets the human-readable details, or null when none were recorded.</summary>
    string? Details,
    /// <summary>Gets the history link when the row addresses a real route, otherwise empty.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    private static readonly IReadOnlySet<string> CandidateBucket =
        new HashSet<string>(StringComparer.Ordinal)
        {
            AuditEntityTypes.Candidate, AuditEntityTypes.Invite,
            AuditEntityTypes.Booking, AuditEntityTypes.BookingAppointment,
        };

    /// <summary>Projects one audit history row, linking only to real candidate or slot routes.</summary>
    /// <param name="row">The application audit history row to project.</param>
    /// <returns>The API resource with a history link or an empty link dictionary.</returns>
    public static AuditHistoryResourceResponse From(AuditHistoryRow row)
    {
        IReadOnlyDictionary<string, ApiLink> links = row.EntityType switch
        {
            AuditEntityTypes.ConfirmedSlot => new Dictionary<string, ApiLink>
            {
                ["history"] = new($"/api/audit/slot/{row.EntityId}", "GET", "getSlotAuditHistory"),
            },
            _ when CandidateBucket.Contains(row.EntityType) => new Dictionary<string, ApiLink>
            {
                ["history"] = new($"/api/audit/candidate/{row.EntityId}", "GET", "getCandidateAuditHistory"),
            },
            _ => new Dictionary<string, ApiLink>(),
        };
        return new AuditHistoryResourceResponse(
            row.Timestamp, row.EntityType, row.EntityId, row.Action,
            row.ActorType, row.ActorId, row.Details, links);
    }
}

/// <summary>One audit search page with conditional next-page affordance.</summary>
public sealed record AuditSearchResourceResponse(
    /// <summary>Gets the newest-first audit rows for the requested page.</summary>
    IReadOnlyList<AuditHistoryResourceResponse> Rows,
    /// <summary>Gets the opaque cursor for the following page, or null when exhausted.</summary>
    string? NextCursor,
    /// <summary>Gets the self relation plus next while paging continues.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one audit search page into its hypermedia resource.</summary>
    /// <param name="page">The application search page to project.</param>
    /// <param name="currentQuery">The raw incoming query string, used verbatim for the self href.</param>
    /// <returns>The API resource with search links.</returns>
    public static AuditSearchResourceResponse From(AuditSearchPage page, string currentQuery) =>
        new(page.Rows.Select(AuditHistoryResourceResponse.From).ToList(),
            page.NextCursor,
            StaffResourceLinks.ForAuditSearch(currentQuery, page.NextCursor));
}
`````

## after — src/EventBooking.Api/Contracts/OperationsHypermediaResponses.cs — 1/1

<!-- vocabulary-file: {"id":9,"oldPath":"src/EventBooking.Api/Contracts/OperationsHypermediaResponses.cs","newPath":"src/EventBooking.Api/Contracts/OperationsHypermediaResponses.cs","beforeSha":"c0bd039a17b410963c76d1bff3f8e6fe854bab30d50eb9f2f25bb905b6f5281c","afterSha":"72e252efbe22bf25d00c75215519f22e74c28b8d8f3b5a21562bf20f8ba8d6fb","side":"after","part":1,"parts":1} -->

`````csharp
using System.Text.Json.Serialization;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Dashboards;
using EventBooking.Domain.Audit;

namespace EventBooking.Api.Contracts;

/// <summary>One event-overview row plus its audit and cancel affordances.</summary>
public sealed record EventOverviewResourceResponse(
    /// <summary>Gets the stable event identifier.</summary>
    Guid EventId,
    /// <summary>Gets the event window calendar date.</summary>
    DateOnly Date,
    /// <summary>Gets the start of the event window.</summary>
    TimeOnly StartTime,
    /// <summary>Gets the end of the event window.</summary>
    TimeOnly EndTime,
    /// <summary>Gets the per-appointment-type capacity rows.</summary>
    IReadOnlyList<EventCapacityRow> Capacities,
    /// <summary>Gets the aggregate active booking count.</summary>
    int ActiveBookings,
    /// <summary>Gets the audit and cancel affordances for the eventItem.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one event-overview row into its hypermedia resource.</summary>
    /// <param name="row">The application event-overview row to project.</param>
    /// <returns>The API resource with event-overview links.</returns>
    public static EventOverviewResourceResponse From(EventOverviewRow row) =>
        new(row.EventId, row.Date, row.StartTime, row.EndTime,
            row.Capacities, row.ActiveBookings,
            new Dictionary<string, ApiLink>
            {
                ["audit"] = new($"/api/audit/event/{row.EventId}", "GET", "getEventAuditHistory"),
                ["cancel"] = new($"/api/events/{row.EventId}", "DELETE", "cancelEvent"),
            });
}

/// <summary>Event-only operations view plus the collection self affordance.</summary>
public sealed record EventOperationsResourceResponse(
    /// <summary>Gets one row per event with capacity and booking counts.</summary>
    IReadOnlyList<EventOverviewResourceResponse> Events,
    /// <summary>Gets the collection affordances.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one event operations view into its hypermedia resource.</summary>
    /// <param name="view">The application event operations view to project.</param>
    /// <returns>The API resource with operations and row links.</returns>
    public static EventOperationsResourceResponse From(EventOperationsView view) =>
        new(view.Events.Select(EventOverviewResourceResponse.From).ToList(),
            new Dictionary<string, ApiLink>
            {
                ["self"] = new("/api/events/operations", "GET", "getEventOperations"),
                ["board"] = new("/api/events/board", "GET", "getEventBoard"),
            });
}

/// <summary>Coordinator dashboards plus entry affordances for related collections.</summary>
public sealed record DashboardResourceResponse(
    /// <summary>Gets the attendees waiting for availability.</summary>
    IReadOnlyList<AwaitingAvailabilityRow> AwaitingAvailability,
    /// <summary>Gets the attendees needing follow-up after no response.</summary>
    IReadOnlyList<NoResponseRow> NoResponse,
    /// <summary>Gets the event overview rows with capacity and booking counts.</summary>
    IReadOnlyList<EventOverviewResourceResponse> Events,
    /// <summary>Gets the latest email delivery state per attendee.</summary>
    IReadOnlyList<AttendeeEmailStatusView> EmailStatuses,
    /// <summary>Gets the dashboard self and collection entry affordances.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one dashboards view into its hypermedia resource.</summary>
    /// <param name="view">The application dashboards view to project.</param>
    /// <returns>The API resource with dashboard links.</returns>
    public static DashboardResourceResponse From(DashboardsView view) =>
        new(view.AwaitingAvailability, view.NoResponse,
            view.Events.Select(EventOverviewResourceResponse.From).ToList(),
            view.EmailStatuses,
            new Dictionary<string, ApiLink>
            {
                ["self"] = new("/api/dashboards", "GET", "getDashboards"),
                ["attendees"] = new("/api/attendees", "GET", "listAttendees"),
                ["eventOperations"] = new("/api/events/operations", "GET", "getEventOperations"),
                ["audit"] = new("/api/audit/search", "GET", "searchAudit"),
            });
}

/// <summary>One audit history row plus a link when it addresses a real attendee or event route.</summary>
public sealed record AuditHistoryResourceResponse(
    /// <summary>Gets when the audited action was recorded.</summary>
    DateTimeOffset Timestamp,
    /// <summary>Gets the audited entity type name.</summary>
    string EntityType,
    /// <summary>Gets the audited entity identifier.</summary>
    Guid EntityId,
    /// <summary>Gets the audited action name.</summary>
    string Action,
    /// <summary>Gets the actor type name.</summary>
    string ActorType,
    /// <summary>Gets the actor identifier, or null for system actions.</summary>
    string? ActorId,
    /// <summary>Gets the human-readable details, or null when none were recorded.</summary>
    string? Details,
    /// <summary>Gets the history link when the row addresses a real route, otherwise empty.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    private static readonly IReadOnlySet<string> AttendeeBucket =
        new HashSet<string>(StringComparer.Ordinal)
        {
            AuditEntityTypes.Attendee, AuditEntityTypes.Invite,
            AuditEntityTypes.Booking, AuditEntityTypes.BookingAppointment,
        };

    /// <summary>Projects one audit history row, linking only to real attendee or event routes.</summary>
    /// <param name="row">The application audit history row to project.</param>
    /// <returns>The API resource with a history link or an empty link dictionary.</returns>
    public static AuditHistoryResourceResponse From(AuditHistoryRow row)
    {
        IReadOnlyDictionary<string, ApiLink> links = row.EntityType switch
        {
            AuditEntityTypes.Event => new Dictionary<string, ApiLink>
            {
                ["history"] = new($"/api/audit/event/{row.EntityId}", "GET", "getEventAuditHistory"),
            },
            _ when AttendeeBucket.Contains(row.EntityType) => new Dictionary<string, ApiLink>
            {
                ["history"] = new($"/api/audit/attendee/{row.EntityId}", "GET", "getAttendeeAuditHistory"),
            },
            _ => new Dictionary<string, ApiLink>(),
        };
        return new AuditHistoryResourceResponse(
            row.Timestamp, row.EntityType, row.EntityId, row.Action,
            row.ActorType, row.ActorId, row.Details, links);
    }
}

/// <summary>One audit search page with conditional next-page affordance.</summary>
public sealed record AuditSearchResourceResponse(
    /// <summary>Gets the newest-first audit rows for the requested page.</summary>
    IReadOnlyList<AuditHistoryResourceResponse> Rows,
    /// <summary>Gets the opaque cursor for the following page, or null when exhausted.</summary>
    string? NextCursor,
    /// <summary>Gets the self relation plus next while paging continues.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one audit search page into its hypermedia resource.</summary>
    /// <param name="page">The application search page to project.</param>
    /// <param name="currentQuery">The raw incoming query string, used verbatim for the self href.</param>
    /// <returns>The API resource with search links.</returns>
    public static AuditSearchResourceResponse From(AuditSearchPage page, string currentQuery) =>
        new(page.Rows.Select(AuditHistoryResourceResponse.From).ToList(),
            page.NextCursor,
            StaffResourceLinks.ForAuditSearch(currentQuery, page.NextCursor));
}
`````

## before — src/EventBooking.Api/Contracts/SlotHypermediaResponses.cs — 1/1

<!-- vocabulary-file: {"id":10,"oldPath":"src/EventBooking.Api/Contracts/SlotHypermediaResponses.cs","newPath":"src/EventBooking.Api/Contracts/EventHypermediaResponses.cs","beforeSha":"39ef63a66194388f66b47063cca4f5d0ef15b65b201278f5c3f08e3c225cd25f","afterSha":"5bf4f60e28645fa07318e7f1fc3276ec9d7b9c510fef577162bbe4b20eeec9b9","side":"before","part":1,"parts":1} -->

`````csharp
using System.Text.Json.Serialization;
using EventBooking.Application.Slots;

namespace EventBooking.Api.Contracts;

/// <summary>Central factories for staff-workspace affordances.</summary>
public static class StaffResourceLinks
{
    /// <summary>Builds the self and update relations for the settings resource.</summary>
    /// <returns>The settings affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForSettings() =>
        new Dictionary<string, ApiLink>
        {
            ["self"] = new("/api/admin/settings", "GET", "getSettings"),
            ["update"] = new("/api/admin/settings", "PUT", "updateSettings"),
        };

    /// <summary>Builds the replace and clear relations for one staff access profile.</summary>
    /// <param name="staffUserId">The staff identity the relations target.</param>
    /// <param name="version">The observed version, supplied as a body or query input rather than embedded in the href.</param>
    /// <returns>The staff-access affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForStaffAccess(Guid staffUserId, long version) =>
        new Dictionary<string, ApiLink>
        {
            ["replace"] = new($"/api/admin/staff-access/{staffUserId}", "PUT", "replaceStaffAccessScope"),
            ["clear"] = new($"/api/admin/staff-access/{staffUserId}", "DELETE", "clearStaffAccessScope"),
        };

    /// <summary>Builds the entry relations for the slot board collection.</summary>
    /// <returns>The slot board affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForSlotBoard() =>
        new Dictionary<string, ApiLink>
        {
            ["self"] = new("/api/slots/board", "GET", "getSlotBoard"),
            ["propose"] = new("/api/slots/proposals", "POST", "proposeSlot"),
            ["import"] = new("/api/confirmed-slots/import", "POST", "importConfirmedSlots"),
        };

    /// <summary>Builds the conditional workflow links for one open slot proposal.</summary>
    /// <param name="proposalId">The proposal identifier the relations target.</param>
    /// <param name="acceptedByMe">Whether the caller already accepted; gates the withdrawAcceptance relation.</param>
    /// <param name="createdByMe">Whether the caller created the proposal; gates the withdrawProposal relation.</param>
    /// <returns>The proposal affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForProposal(Guid proposalId, bool acceptedByMe, bool createdByMe)
    {
        var links = new Dictionary<string, ApiLink>
        {
            ["accept"] = new($"/api/slots/proposals/{proposalId}/acceptance", "POST", "acceptProposal"),
        };
        if (acceptedByMe)
        {
            links["withdrawAcceptance"] = new($"/api/slots/proposals/{proposalId}/acceptance", "DELETE", "withdrawAcceptance");
        }

        if (createdByMe)
        {
            links["withdrawProposal"] = new($"/api/slots/proposals/{proposalId}", "DELETE", "withdrawProposal");
        }

        return links;
    }

    /// <summary>Builds the management relations for one confirmed slot.</summary>
    /// <param name="confirmedSlotId">The confirmed slot identifier the relations target.</param>
    /// <returns>The confirmed-slot affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForConfirmedSlot(Guid confirmedSlotId) =>
        new Dictionary<string, ApiLink>
        {
            ["adjustCapacity"] = new($"/api/slots/confirmed/{confirmedSlotId}/capacity", "PUT", "adjustConfirmedSlotCapacity"),
            ["cancel"] = new($"/api/slots/confirmed/{confirmedSlotId}", "DELETE", "cancelConfirmedSlot"),
            ["audit"] = new($"/api/audit/slot/{confirmedSlotId}", "GET", "getSlotAuditHistory"),
            ["appointmentDetail"] = new($"/api/appointment-workspace/slots/{confirmedSlotId}", "GET", "getAppointmentSlot"),
            ["roster"] = new($"/api/appointment-workspace/slots/{confirmedSlotId}/roster", "GET", "exportAppointmentRoster"),
        };

    /// <summary>Builds the self relation from the incoming query plus a next relation while paging continues.</summary>
    /// <param name="currentQuery">The raw incoming query string, used verbatim for the self href.</param>
    /// <param name="nextCursor">The opaque cursor for the following page, or null when exhausted.</param>
    /// <returns>The audit-search affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForAuditSearch(string currentQuery, string? nextCursor)
    {
        var links = new Dictionary<string, ApiLink>
        {
            ["self"] = new($"/api/audit/search{currentQuery}", "GET", "searchAudit"),
        };
        if (nextCursor is not null)
        {
            links["next"] = new(BuildAuditNextHref(currentQuery, nextCursor), "GET", "searchAudit");
        }

        return links;
    }

    /// <summary>Builds the detail and roster relations for one appointment-workspace slot.</summary>
    /// <param name="confirmedSlotId">The confirmed slot identifier the relations target.</param>
    /// <returns>The appointment-slot affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForAppointmentSlot(Guid confirmedSlotId) =>
        new Dictionary<string, ApiLink>
        {
            ["detail"] = new($"/api/appointment-workspace/slots/{confirmedSlotId}", "GET", "getAppointmentSlot"),
            ["roster"] = new($"/api/appointment-workspace/slots/{confirmedSlotId}/roster", "GET", "exportAppointmentRoster"),
        };

    /// <summary>Builds the status-update relation for one booking appointment.</summary>
    /// <param name="bookingAppointmentId">The booking appointment identifier the relation targets.</param>
    /// <returns>The booking-appointment affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForBookingAppointment(Guid bookingAppointmentId) =>
        new Dictionary<string, ApiLink>
        {
            ["updateStatus"] = new($"/api/appointment-workspace/appointments/{bookingAppointmentId}/status", "PUT", "updateAppointmentStatus"),
        };

    private static string BuildAuditNextHref(string currentQuery, string nextCursor)
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

        return "/api/audit/search?" + string.Join("&", parameters.Select(pair =>
            pair.Value is null ? pair.Name : $"{pair.Name}={pair.Value}"));
    }
}

/// <summary>One open slot proposal plus the caller's conditional workflow operations.</summary>
public sealed record OpenProposalResourceResponse(
    /// <summary>Gets the stable slot proposal identifier.</summary>
    Guid ProposalId,
    /// <summary>Gets the proposal window calendar date.</summary>
    DateOnly Date,
    /// <summary>Gets the start of the proposal window.</summary>
    TimeOnly StartTime,
    /// <summary>Gets the end of the proposal window.</summary>
    TimeOnly EndTime,
    /// <summary>Gets the appointment-type names that already accepted this proposal.</summary>
    IReadOnlyList<string> AcceptedByAppointmentTypeNames,
    /// <summary>Gets the caller's accepted headcount, or null when not accepted by the caller.</summary>
    int? MyAcceptedHeadcount,
    /// <summary>Gets whether the caller already accepted this proposal.</summary>
    bool AcceptedByMe,
    /// <summary>Gets whether the caller created this proposal.</summary>
    bool CreatedByMe,
    /// <summary>Gets the conditional workflow operations for the proposal.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one open proposal view into its hypermedia resource.</summary>
    /// <param name="view">The application proposal view to project.</param>
    /// <returns>The API resource with conditional proposal links.</returns>
    public static OpenProposalResourceResponse From(OpenProposalView view) =>
        new(view.ProposalId, view.Date, view.StartTime, view.EndTime,
            view.AcceptedByAppointmentTypeNames, view.MyAcceptedHeadcount,
            view.AcceptedByMe, view.CreatedByMe,
            StaffResourceLinks.ForProposal(view.ProposalId, view.AcceptedByMe, view.CreatedByMe));
}

/// <summary>One confirmed slot plus its management operations.</summary>
public sealed record ManagerConfirmedSlotResourceResponse(
    /// <summary>Gets the stable confirmed slot identifier.</summary>
    Guid ConfirmedSlotId,
    /// <summary>Gets the slot window calendar date.</summary>
    DateOnly Date,
    /// <summary>Gets the start of the slot window.</summary>
    TimeOnly StartTime,
    /// <summary>Gets the end of the slot window.</summary>
    TimeOnly EndTime,
    /// <summary>Gets the caller's total headcount on this slot.</summary>
    int MyHeadcount,
    /// <summary>Gets the caller's remaining capacity on this slot.</summary>
    int MyRemainingCapacity,
    /// <summary>Gets the management operations for the confirmed slot.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one confirmed slot view into its hypermedia resource.</summary>
    /// <param name="view">The application confirmed slot view to project.</param>
    /// <returns>The API resource with confirmed-slot links.</returns>
    public static ManagerConfirmedSlotResourceResponse From(ManagerConfirmedSlotView view) =>
        new(view.ConfirmedSlotId, view.Date, view.StartTime, view.EndTime,
            view.MyHeadcount, view.MyRemainingCapacity,
            StaffResourceLinks.ForConfirmedSlot(view.ConfirmedSlotId));
}

/// <summary>Manager-scoped slot board plus board-level affordances.</summary>
public sealed record SlotBoardResourceResponse(
    /// <summary>Gets the open proposals visible to the caller.</summary>
    IReadOnlyList<OpenProposalResourceResponse> OpenProposals,
    /// <summary>Gets the active confirmed slots visible to the caller.</summary>
    IReadOnlyList<ManagerConfirmedSlotResourceResponse> ConfirmedSlots,
    /// <summary>Gets the board-level affordances.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one slot board into its hypermedia resource.</summary>
    /// <param name="board">The application slot board to project.</param>
    /// <returns>The API resource with board and row links.</returns>
    public static SlotBoardResourceResponse From(ManagerSlotBoard board) =>
        new(board.OpenProposals.Select(OpenProposalResourceResponse.From).ToList(),
            board.ConfirmedSlots.Select(ManagerConfirmedSlotResourceResponse.From).ToList(),
            StaffResourceLinks.ForSlotBoard());
}
`````

## after — src/EventBooking.Api/Contracts/EventHypermediaResponses.cs — 1/1

<!-- vocabulary-file: {"id":10,"oldPath":"src/EventBooking.Api/Contracts/SlotHypermediaResponses.cs","newPath":"src/EventBooking.Api/Contracts/EventHypermediaResponses.cs","beforeSha":"39ef63a66194388f66b47063cca4f5d0ef15b65b201278f5c3f08e3c225cd25f","afterSha":"5bf4f60e28645fa07318e7f1fc3276ec9d7b9c510fef577162bbe4b20eeec9b9","side":"after","part":1,"parts":1} -->

`````csharp
using System.Text.Json.Serialization;
using EventBooking.Application.Events;

namespace EventBooking.Api.Contracts;

/// <summary>Central factories for staff-workspace affordances.</summary>
public static class StaffResourceLinks
{
    /// <summary>Builds the self and update relations for the settings resource.</summary>
    /// <returns>The settings affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForSettings() =>
        new Dictionary<string, ApiLink>
        {
            ["self"] = new("/api/admin/settings", "GET", "getSettings"),
            ["update"] = new("/api/admin/settings", "PUT", "updateSettings"),
        };

    /// <summary>Builds the replace and clear relations for one staff access profile.</summary>
    /// <param name="staffUserId">The staff identity the relations target.</param>
    /// <param name="version">The observed version, supplied as a body or query input rather than embedded in the href.</param>
    /// <returns>The staff-access affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForStaffAccess(Guid staffUserId, long version) =>
        new Dictionary<string, ApiLink>
        {
            ["replace"] = new($"/api/admin/staff-access/{staffUserId}", "PUT", "replaceStaffAccessScope"),
            ["clear"] = new($"/api/admin/staff-access/{staffUserId}", "DELETE", "clearStaffAccessScope"),
        };

    /// <summary>Builds the entry relations for the event board collection.</summary>
    /// <returns>The event board affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForEventBoard() =>
        new Dictionary<string, ApiLink>
        {
            ["self"] = new("/api/events/board", "GET", "getEventBoard"),
            ["propose"] = new("/api/event-proposals", "POST", "proposeEvent"),
            ["import"] = new("/api/events/import", "POST", "importEvents"),
        };

    /// <summary>Builds the conditional workflow links for one open event proposal.</summary>
    /// <param name="proposalId">The proposal identifier the relations target.</param>
    /// <param name="acceptedByMe">Whether the caller already accepted; gates the withdrawAcceptance relation.</param>
    /// <param name="createdByMe">Whether the caller created the proposal; gates the withdrawProposal relation.</param>
    /// <returns>The proposal affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForProposal(Guid proposalId, bool acceptedByMe, bool createdByMe)
    {
        var links = new Dictionary<string, ApiLink>
        {
            ["accept"] = new($"/api/event-proposals/{proposalId}/acceptance", "POST", "acceptProposal"),
        };
        if (acceptedByMe)
        {
            links["withdrawAcceptance"] = new($"/api/event-proposals/{proposalId}/acceptance", "DELETE", "withdrawAcceptance");
        }

        if (createdByMe)
        {
            links["withdrawProposal"] = new($"/api/event-proposals/{proposalId}", "DELETE", "withdrawProposal");
        }

        return links;
    }

    /// <summary>Builds the management relations for one eventItem.</summary>
    /// <param name="eventId">The event identifier the relations target.</param>
    /// <returns>The event affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForEvent(Guid eventId) =>
        new Dictionary<string, ApiLink>
        {
            ["adjustCapacity"] = new($"/api/events/{eventId}/capacity", "PUT", "adjustEventCapacity"),
            ["cancel"] = new($"/api/events/{eventId}", "DELETE", "cancelEvent"),
            ["audit"] = new($"/api/audit/event/{eventId}", "GET", "getEventAuditHistory"),
            ["appointmentDetail"] = new($"/api/appointment-workspace/events/{eventId}", "GET", "getAppointmentEvent"),
            ["roster"] = new($"/api/appointment-workspace/events/{eventId}/roster", "GET", "exportAppointmentRoster"),
        };

    /// <summary>Builds the self relation from the incoming query plus a next relation while paging continues.</summary>
    /// <param name="currentQuery">The raw incoming query string, used verbatim for the self href.</param>
    /// <param name="nextCursor">The opaque cursor for the following page, or null when exhausted.</param>
    /// <returns>The audit-search affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForAuditSearch(string currentQuery, string? nextCursor)
    {
        var links = new Dictionary<string, ApiLink>
        {
            ["self"] = new($"/api/audit/search{currentQuery}", "GET", "searchAudit"),
        };
        if (nextCursor is not null)
        {
            links["next"] = new(BuildAuditNextHref(currentQuery, nextCursor), "GET", "searchAudit");
        }

        return links;
    }

    /// <summary>Builds the detail and roster relations for one appointment-workspace eventItem.</summary>
    /// <param name="eventId">The event identifier the relations target.</param>
    /// <returns>The appointment-event affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForAppointmentEvent(Guid eventId) =>
        new Dictionary<string, ApiLink>
        {
            ["detail"] = new($"/api/appointment-workspace/events/{eventId}", "GET", "getAppointmentEvent"),
            ["roster"] = new($"/api/appointment-workspace/events/{eventId}/roster", "GET", "exportAppointmentRoster"),
        };

    /// <summary>Builds the status-update relation for one booking appointment.</summary>
    /// <param name="bookingAppointmentId">The booking appointment identifier the relation targets.</param>
    /// <returns>The booking-appointment affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForBookingAppointment(Guid bookingAppointmentId) =>
        new Dictionary<string, ApiLink>
        {
            ["updateStatus"] = new($"/api/appointment-workspace/appointments/{bookingAppointmentId}/status", "PUT", "updateAppointmentStatus"),
        };

    private static string BuildAuditNextHref(string currentQuery, string nextCursor)
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

        return "/api/audit/search?" + string.Join("&", parameters.Select(pair =>
            pair.Value is null ? pair.Name : $"{pair.Name}={pair.Value}"));
    }
}

/// <summary>One open event proposal plus the caller's conditional workflow operations.</summary>
public sealed record OpenProposalResourceResponse(
    /// <summary>Gets the stable event proposal identifier.</summary>
    Guid ProposalId,
    /// <summary>Gets the proposal window calendar date.</summary>
    DateOnly Date,
    /// <summary>Gets the start of the proposal window.</summary>
    TimeOnly StartTime,
    /// <summary>Gets the end of the proposal window.</summary>
    TimeOnly EndTime,
    /// <summary>Gets the appointment-type names that already accepted this proposal.</summary>
    IReadOnlyList<string> AcceptedByAppointmentTypeNames,
    /// <summary>Gets the caller's accepted headcount, or null when not accepted by the caller.</summary>
    int? MyAcceptedHeadcount,
    /// <summary>Gets whether the caller already accepted this proposal.</summary>
    bool AcceptedByMe,
    /// <summary>Gets whether the caller created this proposal.</summary>
    bool CreatedByMe,
    /// <summary>Gets the conditional workflow operations for the proposal.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one open proposal view into its hypermedia resource.</summary>
    /// <param name="view">The application proposal view to project.</param>
    /// <returns>The API resource with conditional proposal links.</returns>
    public static OpenProposalResourceResponse From(OpenProposalView view) =>
        new(view.ProposalId, view.Date, view.StartTime, view.EndTime,
            view.AcceptedByAppointmentTypeNames, view.MyAcceptedHeadcount,
            view.AcceptedByMe, view.CreatedByMe,
            StaffResourceLinks.ForProposal(view.ProposalId, view.AcceptedByMe, view.CreatedByMe));
}

/// <summary>One event plus its management operations.</summary>
public sealed record ManagerEventResourceResponse(
    /// <summary>Gets the stable event identifier.</summary>
    Guid EventId,
    /// <summary>Gets the event window calendar date.</summary>
    DateOnly Date,
    /// <summary>Gets the start of the event window.</summary>
    TimeOnly StartTime,
    /// <summary>Gets the end of the event window.</summary>
    TimeOnly EndTime,
    /// <summary>Gets the caller's total headcount on this eventItem.</summary>
    int MyHeadcount,
    /// <summary>Gets the caller's remaining capacity on this eventItem.</summary>
    int MyRemainingCapacity,
    /// <summary>Gets the management operations for the eventItem.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one event view into its hypermedia resource.</summary>
    /// <param name="view">The application event view to project.</param>
    /// <returns>The API resource with event links.</returns>
    public static ManagerEventResourceResponse From(ManagerEventView view) =>
        new(view.EventId, view.Date, view.StartTime, view.EndTime,
            view.MyHeadcount, view.MyRemainingCapacity,
            StaffResourceLinks.ForEvent(view.EventId));
}

/// <summary>Manager-scoped event board plus board-level affordances.</summary>
public sealed record EventBoardResourceResponse(
    /// <summary>Gets the open proposals visible to the caller.</summary>
    IReadOnlyList<OpenProposalResourceResponse> OpenProposals,
    /// <summary>Gets the active events visible to the caller.</summary>
    IReadOnlyList<ManagerEventResourceResponse> Events,
    /// <summary>Gets the board-level affordances.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one event board into its hypermedia resource.</summary>
    /// <param name="board">The application event board to project.</param>
    /// <returns>The API resource with board and row links.</returns>
    public static EventBoardResourceResponse From(ManagerEventBoard board) =>
        new(board.OpenProposals.Select(OpenProposalResourceResponse.From).ToList(),
            board.Events.Select(ManagerEventResourceResponse.From).ToList(),
            StaffResourceLinks.ForEventBoard());
}
`````

## before — src/EventBooking.Api/Endpoints/AdminEndpoints.cs — 1/1

<!-- vocabulary-file: {"id":11,"oldPath":"src/EventBooking.Api/Endpoints/AdminEndpoints.cs","newPath":"src/EventBooking.Api/Endpoints/AdminEndpoints.cs","beforeSha":"35befaaff4fcd738eb18c6cce18eea3d778df6241b23cff2f27da411a0efe348","afterSha":"4a7ea3f838b0ffee7fa24e5f3de7824210f170f20afbe4b7c5bff30c186607c8","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Settings;
using EventBooking.Application.Slots;
using EventBooking.Domain.Access;

namespace EventBooking.Api.Endpoints;

public static class AdminEndpoints
{
    public sealed record UpdateSettingsRequest(int InviteExpiryDays, int MaxAutoRetryCount);

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("/settings", async (
            ICallerAccessor caller,
            AdminSettingsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.GetAsync(caller.RequireStaffUserId(), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(SettingsResourceResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("getSettings")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapPut("/settings", async (
            UpdateSettingsRequest request,
            ICallerAccessor caller,
            AdminSettingsHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.UpdateAsync(
                new UpdateSettingsCommand(
                    caller.RequireStaffUserId(), request.InviteExpiryDays, request.MaxAutoRetryCount),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("updateSettings")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(409);

        return app;
    }
}
`````

## after — src/EventBooking.Api/Endpoints/AdminEndpoints.cs — 1/1

<!-- vocabulary-file: {"id":11,"oldPath":"src/EventBooking.Api/Endpoints/AdminEndpoints.cs","newPath":"src/EventBooking.Api/Endpoints/AdminEndpoints.cs","beforeSha":"35befaaff4fcd738eb18c6cce18eea3d778df6241b23cff2f27da411a0efe348","afterSha":"4a7ea3f838b0ffee7fa24e5f3de7824210f170f20afbe4b7c5bff30c186607c8","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Settings;
using EventBooking.Application.Events;
using EventBooking.Domain.Access;

namespace EventBooking.Api.Endpoints;

public static class AdminEndpoints
{
    public sealed record UpdateSettingsRequest(int InviteExpiryDays, int MaxAutoRetryCount);

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("/settings", async (
            ICallerAccessor caller,
            AdminSettingsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.GetAsync(caller.RequireStaffUserId(), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(SettingsResourceResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("getSettings")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapPut("/settings", async (
            UpdateSettingsRequest request,
            ICallerAccessor caller,
            AdminSettingsHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.UpdateAsync(
                new UpdateSettingsCommand(
                    caller.RequireStaffUserId(), request.InviteExpiryDays, request.MaxAutoRetryCount),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("updateSettings")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(409);

        return app;
    }
}
`````

## before — src/EventBooking.Api/Endpoints/ApiDiscoveryEndpoints.cs — 1/1

<!-- vocabulary-file: {"id":12,"oldPath":"src/EventBooking.Api/Endpoints/ApiDiscoveryEndpoints.cs","newPath":"src/EventBooking.Api/Endpoints/ApiDiscoveryEndpoints.cs","beforeSha":"088436ea6ee088c8a425af4430731647f040e7d9659f494212f6acf90a5f5f29","afterSha":"07fbacedd058bb2b7d2ad4ecb0295aa58bcba15ed8f72659eb857bb5202773b9","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps the anonymous API discovery root.</summary>
public static class ApiDiscoveryEndpoints
{
    /// <summary>Maps the anonymous <c>GET /api</c> entry document.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapApiDiscoveryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api", () =>
        {
            var links = new Dictionary<string, ApiLink>(StringComparer.Ordinal)
            {
                ["self"] = Link("getApiIndex"),
                ["openapi"] = Link("getOpenApiDocument"),
                ["swagger"] = Link("getSwaggerUi"),
                ["health"] = Link("getHealth"),
                ["me"] = Link("getMyAccess"),
                ["slotBoard"] = Link("getSlotBoard"),
                ["slotOperations"] = Link("getSlotOperations"),
                ["candidates"] = Link("listCandidates"),
                ["employeeGroups"] = Link("listEmployeeGroups"),
                ["settings"] = Link("getSettings"),
                ["staffAccess"] = Link("listStaffAccess"),
                ["dashboards"] = Link("getDashboards"),
                ["audit"] = Link("searchAudit"),
                ["appointments"] = Link("listAppointmentSlots"),
            };
            return Results.Ok(new ApiDiscoveryResponse("EventBooking API", "v1", links));
        }).AllowAnonymous()
            .WithAgentMetadata("getApiIndex")
            .Produces(200);
        return app;
    }

    private static ApiLink Link(string operationId)
    {
        var operation = AgentOperationCatalog.Get(operationId);
        return new ApiLink(operation.Route, operation.Method, operation.OperationId);
    }
}
`````
