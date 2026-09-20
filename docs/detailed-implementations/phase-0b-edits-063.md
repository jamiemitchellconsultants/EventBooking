# 00b — Vocabulary edits 63 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — src/EventBooking.Web/Services/AppointmentsClient.cs — 1/1

<!-- vocabulary-file: {"id":204,"oldPath":"src/EventBooking.Web/Services/AppointmentsClient.cs","newPath":"src/EventBooking.Web/Services/AppointmentsClient.cs","beforeSha":"cbdecefd2bf0b0e4f5ec7435f94349bec77b1c4e06dc2425a87790f278bd2694","afterSha":"b5fceaefbe29787d10f7bbfc1a5db7f06f5b79413a1a93af7f6e8bfb42634423","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EventBooking.Web.Services;

/// <summary>Counts scoped booking appointments in each operational state.</summary>
public sealed record AppointmentStatusCountsDto
{
    /// <summary>Gets attendees booked but not checked in for this appointment.</summary>
    public required int Expected { get; init; }

    /// <summary>Gets attendees checked in for this appointment.</summary>
    public required int CheckedIn { get; init; }

    /// <summary>Gets required appointments completed after check-in.</summary>
    public required int Completed { get; init; }

    /// <summary>Gets attendees recorded as not attending this required appointment.</summary>
    public required int NoShow { get; init; }
}

/// <summary>Describes one selectable active event without attendee rows.</summary>
public sealed record AppointmentEventSummaryDto
{
    /// <summary>Gets the event identifier.</summary>
    public required Guid EventId { get; init; }

    /// <summary>Gets the event's transitional-location calendar date.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Gets the start of the shared four-hour window.</summary>
    public required TimeOnly StartTime { get; init; }

    /// <summary>Gets the derived end of the shared four-hour window.</summary>
    public required TimeOnly EndTime { get; init; }

    /// <summary>Gets scoped counts grouped by independent operational state.</summary>
    public required AppointmentStatusCountsDto Counts { get; init; }
}

/// <summary>Returns the trusted appointment-type name and its selectable active events.</summary>
public sealed record AppointmentWorkspaceEventListDto
{
    /// <summary>Gets the fixed appointment-type name for the caller's trusted scope.</summary>
    public required string AppointmentTypeName { get; init; }

    /// <summary>Gets current and upcoming active events containing scoped active bookings.</summary>
    public required IReadOnlyList<AppointmentEventSummaryDto> Events { get; init; }
}

/// <summary>Contains only the fields needed to identify and conduct one booked appointment.</summary>
public sealed record BookingAppointmentRowDto
{
    /// <summary>Gets the stable booking-appointment command identifier.</summary>
    public required Guid BookingAppointmentId { get; init; }

    /// <summary>Gets the attendee name used for primary human identification.</summary>
    public required string AttendeeName { get; init; }

    /// <summary>Gets the attendee email used for secondary human identification.</summary>
    public required string AttendeeEmail { get; init; }

    /// <summary>Gets this appointment's independent operational status name.</summary>
    public required string Status { get; init; }

    /// <summary>Gets when staff checked the attendee in, or null until check-in.</summary>
    public DateTimeOffset? CheckedInAt { get; init; }

    /// <summary>Gets when staff recorded completion or no-show, or null before an outcome.</summary>
    public DateTimeOffset? OutcomeAt { get; init; }

    /// <summary>Gets the positive concurrency version required by a status command.</summary>
    public required long Version { get; init; }
}

/// <summary>Returns one scoped active event and only its minimum-data operational rows.</summary>
public sealed record AppointmentEventDetailDto
{
    /// <summary>Gets the fixed appointment-type name for the caller's trusted scope.</summary>
    public required string AppointmentTypeName { get; init; }

    /// <summary>Gets the selected event identifier.</summary>
    public required Guid EventId { get; init; }

    /// <summary>Gets the event's transitional-location calendar date.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Gets the start of the shared four-hour window.</summary>
    public required TimeOnly StartTime { get; init; }

    /// <summary>Gets the derived end of the shared four-hour window.</summary>
    public required TimeOnly EndTime { get; init; }

    /// <summary>Gets scoped active-booking appointment rows ordered for staff identification.</summary>
    public required IReadOnlyList<BookingAppointmentRowDto> Appointments { get; init; }
}

/// <summary>Returns only the changed appointment row state needed by the workspace.</summary>
public sealed record BookingAppointmentUpdateDto
{
    /// <summary>Gets the stable booking-appointment identifier.</summary>
    public required Guid BookingAppointmentId { get; init; }

    /// <summary>Gets this appointment's current independent operational status name.</summary>
    public required string Status { get; init; }

    /// <summary>Gets when staff checked the attendee in, or null until check-in.</summary>
    public DateTimeOffset? CheckedInAt { get; init; }

    /// <summary>Gets when staff recorded completion or no-show, or null before an outcome.</summary>
    public DateTimeOffset? OutcomeAt { get; init; }

    /// <summary>Gets the current positive concurrency version.</summary>
    public required long Version { get; init; }
}

/// <summary>The downloaded roster: raw CSV content plus the server-suggested filename.</summary>
public sealed record RosterCsvDownload
{
    /// <summary>Gets the raw CSV response body, header row first.</summary>
    public required string Content { get; init; }

    /// <summary>Gets the filename taken from the response's content-disposition header.</summary>
    public required string FileName { get; init; }
}

/// <summary>Calls the minimum-data appointment-workspace HTTP surface.</summary>
public sealed class AppointmentsClient(HttpClient http)
{
    /// <summary>Identifies a stale booking-appointment version that requires a detail refresh.</summary>
    public const string VersionConflictErrorCode = "appointment_version_conflict";

    /// <summary>Gets the scoped current and upcoming event list.</summary>
    public async Task<ApiOutcome<AppointmentWorkspaceEventListDto>> ListEventsAsync(
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            "/api/appointment-workspace/events", cancellationToken);
        return await ApiCall.ReadAsync<AppointmentWorkspaceEventListDto>(response, cancellationToken);
    }

    /// <summary>Gets one selected event's scoped operational rows.</summary>
    public async Task<ApiOutcome<AppointmentEventDetailDto>> GetEventAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"/api/appointment-workspace/events/{eventId}", cancellationToken);
        return await ApiCall.ReadAsync<AppointmentEventDetailDto>(response, cancellationToken);
    }

    /// <summary>Downloads the scoped roster CSV for one selected eventItem.</summary>
    /// <param name="eventId">The selected event identifier.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Raw CSV content plus the server-suggested filename, or the parsed failure.</returns>
    public async Task<ApiOutcome<RosterCsvDownload>> GetRosterAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"/api/appointment-workspace/events/{eventId}/roster", cancellationToken);
        var outcome = await ApiCall.ReadTextWithHeaderAsync(
            response, "Content-Disposition", cancellationToken);

        return outcome is { IsSuccess: true, Value: not null }
            ? ApiOutcome<RosterCsvDownload>.Success(
                new RosterCsvDownload
                {
                    Content = outcome.Value.Text,
                    FileName = FileNameOf(outcome.Value.Header),
                },
                outcome.StatusCode)
            : ApiOutcome<RosterCsvDownload>.Failure(
                outcome.ErrorMessage ?? "Something went wrong. Please try again.",
                outcome.StatusCode,
                outcome.ErrorCode);
    }

    /// <summary>Reads the filename from a content-disposition value, starred form preferred.</summary>
    private static string FileNameOf(string disposition)
    {
        const string Fallback = "roster.csv";
        if (string.IsNullOrWhiteSpace(disposition))
        {
            return Fallback;
        }

        if (!ContentDispositionHeaderValue.TryParse(disposition, out var parsed))
        {
            return Fallback;
        }

        // The starred form is preferred: it carries the percent-encoded UTF-8 name.
        var name = parsed.FileNameStar ?? parsed.FileName;
        return string.IsNullOrWhiteSpace(name) ? Fallback : name.Trim('"');
    }

    /// <summary>Requests one status using the row's last observed version.</summary>
    public async Task<ApiOutcome<BookingAppointmentUpdateDto>> UpdateStatusAsync(
        Guid bookingAppointmentId,
        string status,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync(
            $"/api/appointment-workspace/appointments/{bookingAppointmentId}/status",
            new { Status = status, ExpectedVersion = expectedVersion }, cancellationToken);
        return await ApiCall.ReadAsync<BookingAppointmentUpdateDto>(response, cancellationToken);
    }
}
`````

## before — src/EventBooking.Web/Services/AuditClient.cs — 1/1

<!-- vocabulary-file: {"id":205,"oldPath":"src/EventBooking.Web/Services/AuditClient.cs","newPath":"src/EventBooking.Web/Services/AuditClient.cs","beforeSha":"df594bb5624cf8194f081270466ce584bb140a4eb00b1ce1835b36443b2bdab2","afterSha":"2c353f29649c99e71b1eac03f422223201940652be1e15b2c0146f232691d10e","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Web.Services;

public sealed record AuditRowDto(
    DateTimeOffset Timestamp,
    string EntityType,
    Guid EntityId,
    string Action,
    string ActorType,
    string? ActorId,
    string? Details);

/// <summary>Filter criteria for the cross-cutting audit search.</summary>
/// <param name="From">Inclusive lower bound on the recorded timestamp, or null.</param>
/// <param name="To">Inclusive upper bound on the recorded timestamp, or null.</param>
/// <param name="ActorType">Actor type name to match, or null for any.</param>
/// <param name="Action">Audit action name to match, or null for any.</param>
/// <param name="Identifier">Free-text identifier matched exactly against entity id or actor id.</param>
/// <param name="EntityType">Optional single entity type within the caller's allowed bucket.</param>
/// <param name="Cursor">Opaque keyset cursor, or null for the newest page.</param>
/// <param name="PageSize">Rows per page.</param>
public sealed record AuditSearchFilterDto(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? ActorType,
    string? Action,
    string? Identifier,
    string? EntityType,
    string? Cursor,
    int PageSize);

/// <summary>One page of audit search results, newest first.</summary>
/// <param name="Rows">The result rows in newest-first order.</param>
/// <param name="NextCursor">Opaque cursor for the following page, or null when exhausted.</param>
public sealed record AuditSearchPageDto(
    List<AuditRowDto> Rows,
    string? NextCursor);

public sealed class AuditClient(HttpClient http)
{
    public async Task<ApiOutcome<List<AuditRowDto>>> ForSlotAsync(
        Guid slotId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"/api/audit/slot/{slotId}", cancellationToken);
        return await ApiCall.ReadAsync<List<AuditRowDto>>(response, cancellationToken);
    }

    public async Task<ApiOutcome<List<AuditRowDto>>> ForCandidateAsync(
        Guid candidateId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"/api/audit/candidate/{candidateId}", cancellationToken);
        return await ApiCall.ReadAsync<List<AuditRowDto>>(response, cancellationToken);
    }

    /// <summary>Searches the audit log newest-first with keyset pagination.</summary>
    /// <param name="filter">The search criteria; absent criteria are omitted from the query string.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The requested page, or the failure the API reported.</returns>
    public async Task<ApiOutcome<AuditSearchPageDto>> SearchAsync(
        AuditSearchFilterDto filter, CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (filter.From is not null)
        {
            query.Add($"from={Uri.EscapeDataString(filter.From.Value.ToString("O"))}");
        }

        if (filter.To is not null)
        {
            query.Add($"to={Uri.EscapeDataString(filter.To.Value.ToString("O"))}");
        }

        if (filter.ActorType is not null)
        {
            query.Add($"actorType={Uri.EscapeDataString(filter.ActorType)}");
        }

        if (filter.Action is not null)
        {
            query.Add($"action={Uri.EscapeDataString(filter.Action)}");
        }

        if (filter.Identifier is not null)
        {
            query.Add($"identifier={Uri.EscapeDataString(filter.Identifier)}");
        }

        if (filter.EntityType is not null)
        {
            query.Add($"entityType={Uri.EscapeDataString(filter.EntityType)}");
        }

        if (filter.Cursor is not null)
        {
            query.Add($"cursor={Uri.EscapeDataString(filter.Cursor)}");
        }

        query.Add($"pageSize={filter.PageSize}");

        using var response = await http.GetAsync(
            $"/api/audit/search?{string.Join("&", query)}", cancellationToken);
        return await ApiCall.ReadAsync<AuditSearchPageDto>(response, cancellationToken);
    }
}
`````

## after — src/EventBooking.Web/Services/AuditClient.cs — 1/1

<!-- vocabulary-file: {"id":205,"oldPath":"src/EventBooking.Web/Services/AuditClient.cs","newPath":"src/EventBooking.Web/Services/AuditClient.cs","beforeSha":"df594bb5624cf8194f081270466ce584bb140a4eb00b1ce1835b36443b2bdab2","afterSha":"2c353f29649c99e71b1eac03f422223201940652be1e15b2c0146f232691d10e","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Web.Services;

public sealed record AuditRowDto(
    DateTimeOffset Timestamp,
    string EntityType,
    Guid EntityId,
    string Action,
    string ActorType,
    string? ActorId,
    string? Details);

/// <summary>Filter criteria for the cross-cutting audit search.</summary>
/// <param name="From">Inclusive lower bound on the recorded timestamp, or null.</param>
/// <param name="To">Inclusive upper bound on the recorded timestamp, or null.</param>
/// <param name="ActorType">Actor type name to match, or null for any.</param>
/// <param name="Action">Audit action name to match, or null for any.</param>
/// <param name="Identifier">Free-text identifier matched exactly against entity id or actor id.</param>
/// <param name="EntityType">Optional single entity type within the caller's allowed bucket.</param>
/// <param name="Cursor">Opaque keyset cursor, or null for the newest page.</param>
/// <param name="PageSize">Rows per page.</param>
public sealed record AuditSearchFilterDto(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? ActorType,
    string? Action,
    string? Identifier,
    string? EntityType,
    string? Cursor,
    int PageSize);

/// <summary>One page of audit search results, newest first.</summary>
/// <param name="Rows">The result rows in newest-first order.</param>
/// <param name="NextCursor">Opaque cursor for the following page, or null when exhausted.</param>
public sealed record AuditSearchPageDto(
    List<AuditRowDto> Rows,
    string? NextCursor);

public sealed class AuditClient(HttpClient http)
{
    public async Task<ApiOutcome<List<AuditRowDto>>> ForEventAsync(
        Guid eventId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"/api/audit/event/{eventId}", cancellationToken);
        return await ApiCall.ReadAsync<List<AuditRowDto>>(response, cancellationToken);
    }

    public async Task<ApiOutcome<List<AuditRowDto>>> ForAttendeeAsync(
        Guid attendeeId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"/api/audit/attendee/{attendeeId}", cancellationToken);
        return await ApiCall.ReadAsync<List<AuditRowDto>>(response, cancellationToken);
    }

    /// <summary>Searches the audit log newest-first with keyset pagination.</summary>
    /// <param name="filter">The search criteria; absent criteria are omitted from the query string.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The requested page, or the failure the API reported.</returns>
    public async Task<ApiOutcome<AuditSearchPageDto>> SearchAsync(
        AuditSearchFilterDto filter, CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (filter.From is not null)
        {
            query.Add($"from={Uri.EscapeDataString(filter.From.Value.ToString("O"))}");
        }

        if (filter.To is not null)
        {
            query.Add($"to={Uri.EscapeDataString(filter.To.Value.ToString("O"))}");
        }

        if (filter.ActorType is not null)
        {
            query.Add($"actorType={Uri.EscapeDataString(filter.ActorType)}");
        }

        if (filter.Action is not null)
        {
            query.Add($"action={Uri.EscapeDataString(filter.Action)}");
        }

        if (filter.Identifier is not null)
        {
            query.Add($"identifier={Uri.EscapeDataString(filter.Identifier)}");
        }

        if (filter.EntityType is not null)
        {
            query.Add($"entityType={Uri.EscapeDataString(filter.EntityType)}");
        }

        if (filter.Cursor is not null)
        {
            query.Add($"cursor={Uri.EscapeDataString(filter.Cursor)}");
        }

        query.Add($"pageSize={filter.PageSize}");

        using var response = await http.GetAsync(
            $"/api/audit/search?{string.Join("&", query)}", cancellationToken);
        return await ApiCall.ReadAsync<AuditSearchPageDto>(response, cancellationToken);
    }
}
`````

## before — src/EventBooking.Web/Services/BookingClient.cs — 1/1

<!-- vocabulary-file: {"id":206,"oldPath":"src/EventBooking.Web/Services/BookingClient.cs","newPath":"src/EventBooking.Web/Services/BookingClient.cs","beforeSha":"a05125e9eae4ee70436f3e8abe3b35b715742b99b4c0b551076aa3ace7f46268","afterSha":"2c13d2480e709d053c6ac735fa14fff363f1e22cf21a50652c3894c5e20a270b","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Json;

namespace EventBooking.Web.Services;

public sealed record InviteOptionDto(
    Guid ConfirmedSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Display);

public sealed record InviteDto(
    Guid InviteId,
    string CandidateName,
    IReadOnlyList<string> AppointmentTypeNames,
    IReadOnlyList<InviteOptionDto> Options,
    bool IsRecovery = false);

/// <summary>Candidate-facing booking confirmation including the actual email outcome.</summary>
/// <param name="BookingId">The active booking identifier.</param>
/// <param name="Date">The confirmed slot date.</param>
/// <param name="StartTime">The confirmed slot start time.</param>
/// <param name="EndTime">The derived four-hour end time.</param>
/// <param name="ManageToken">The raw management token used by the candidate page.</param>
/// <param name="DeliveryStatus">The durable confirmation-email outcome.</param>
/// <param name="HeadOfficeAddress">
/// The head-office address the API configured for candidate emails; empty when none is set.
/// </param>
public sealed record ConfirmedBookingDto(
    Guid BookingId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string ManageToken,
    string DeliveryStatus = "Pending",
    string HeadOfficeAddress = "");

public sealed record BookingDto(
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Display,
    string CandidateName);

/// <summary>Candidate-facing cancellation result and replacement-delivery outcome.</summary>
/// <param name="Reinvited">Whether a replacement invite was created.</param>
/// <param name="InviteCreated">The explicit replacement-invite creation state.</param>
/// <param name="DeliveryStatus">The provider outcome, or null when no replacement was requested.</param>
/// <param name="DeliveryId">The durable replacement delivery identifier, when available.</param>
public sealed record CancelOutcomeDto(
    bool Reinvited,
    bool InviteCreated = false,
    string? DeliveryStatus = null,
    Guid? DeliveryId = null);

/// <summary>Deployment strings the candidate pages need. Bound from the app's own settings file.</summary>
/// <param name="CoordinatorContact">The recruitment contact candidates are told to reach when a link fails.</param>
/// <remarks>
/// The head-office address is deliberately absent: it arrives on the booking confirmation from the
/// API, so the page and the confirmation email cannot name different addresses.
/// </remarks>
public sealed record CandidatePageOptions(string CoordinatorContact);

/// <summary>
/// Talks to the anonymous candidate routes. This client is deliberately constructed from the plain
/// named HTTP client: candidates authorise with their URL token, never an Entra ID access token.
/// </summary>
public sealed class BookingClient(HttpClient http)
{
    public const string ClientName = "EventBooking.Anonymous";

    public async Task<ApiOutcome<InviteDto>> GetInviteAsync(
        string token, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"/api/booking/{Uri.EscapeDataString(token)}", cancellationToken);

        return await ApiCall.ReadAsync<InviteDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<ConfirmedBookingDto>> ConfirmAsync(
        string token, Guid slotId, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            $"/api/booking/{Uri.EscapeDataString(token)}/confirm",
            new { ConfirmedSlotId = slotId },
            cancellationToken);

        return await ApiCall.ReadAsync<ConfirmedBookingDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<BookingDto>> GetBookingAsync(
        string manageToken, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"/api/booking/manage/{Uri.EscapeDataString(manageToken)}", cancellationToken);

        return await ApiCall.ReadAsync<BookingDto>(response, cancellationToken);
    }

    /// <summary>Cancels or rebooks through the anonymous manage-token route.</summary>
    public async Task<ApiOutcome<CancelOutcomeDto>> CancelAsync(
        string manageToken, bool rebook, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            $"/api/booking/manage/{Uri.EscapeDataString(manageToken)}/cancel",
            new { Rebook = rebook },
            cancellationToken);

        var outcome = await ApiCall.ReadAsync<CancelOutcomeDto>(response, cancellationToken);

        return outcome;
    }
}
`````

## after — src/EventBooking.Web/Services/BookingClient.cs — 1/1

<!-- vocabulary-file: {"id":206,"oldPath":"src/EventBooking.Web/Services/BookingClient.cs","newPath":"src/EventBooking.Web/Services/BookingClient.cs","beforeSha":"a05125e9eae4ee70436f3e8abe3b35b715742b99b4c0b551076aa3ace7f46268","afterSha":"2c13d2480e709d053c6ac735fa14fff363f1e22cf21a50652c3894c5e20a270b","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Json;

namespace EventBooking.Web.Services;

public sealed record InviteOptionDto(
    Guid EventId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Display);

public sealed record InviteDto(
    Guid InviteId,
    string AttendeeName,
    IReadOnlyList<string> AppointmentTypeNames,
    IReadOnlyList<InviteOptionDto> Options,
    bool IsRecovery = false);

/// <summary>Attendee-facing booking confirmation including the actual email outcome.</summary>
/// <param name="BookingId">The active booking identifier.</param>
/// <param name="Date">The event date.</param>
/// <param name="StartTime">The event start time.</param>
/// <param name="EndTime">The derived four-hour end time.</param>
/// <param name="ManageToken">The raw management token used by the attendee page.</param>
/// <param name="DeliveryStatus">The durable confirmation-email outcome.</param>
/// <param name="TransitionalLocationAddress">
/// The transitional-location address the API configured for attendee emails; empty when none is set.
/// </param>
public sealed record ConfirmedBookingDto(
    Guid BookingId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string ManageToken,
    string DeliveryStatus = "Pending",
    string TransitionalLocationAddress = "");

public sealed record BookingDto(
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Display,
    string AttendeeName);

/// <summary>Attendee-facing cancellation result and replacement-delivery outcome.</summary>
/// <param name="Reinvited">Whether a replacement invite was created.</param>
/// <param name="InviteCreated">The explicit replacement-invite creation state.</param>
/// <param name="DeliveryStatus">The provider outcome, or null when no replacement was requested.</param>
/// <param name="DeliveryId">The durable replacement delivery identifier, when available.</param>
public sealed record CancelOutcomeDto(
    bool Reinvited,
    bool InviteCreated = false,
    string? DeliveryStatus = null,
    Guid? DeliveryId = null);

/// <summary>Deployment strings the attendee pages need. Bound from the app's own settings file.</summary>
/// <param name="CoordinatorContact">The recruitment contact attendees are told to reach when a link fails.</param>
/// <remarks>
/// The transitional-location address is deliberately absent: it arrives on the booking confirmation from the
/// API, so the page and the confirmation email cannot name different addresses.
/// </remarks>
public sealed record AttendeePageOptions(string CoordinatorContact);

/// <summary>
/// Talks to the anonymous attendee routes. This client is deliberately constructed from the plain
/// named HTTP client: attendees authorise with their URL token, never an Entra ID access token.
/// </summary>
public sealed class BookingClient(HttpClient http)
{
    public const string ClientName = "EventBooking.Anonymous";

    public async Task<ApiOutcome<InviteDto>> GetInviteAsync(
        string token, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"/api/booking/{Uri.EscapeDataString(token)}", cancellationToken);

        return await ApiCall.ReadAsync<InviteDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<ConfirmedBookingDto>> ConfirmAsync(
        string token, Guid eventId, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            $"/api/booking/{Uri.EscapeDataString(token)}/confirm",
            new { EventId = eventId },
            cancellationToken);

        return await ApiCall.ReadAsync<ConfirmedBookingDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<BookingDto>> GetBookingAsync(
        string manageToken, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"/api/booking/manage/{Uri.EscapeDataString(manageToken)}", cancellationToken);

        return await ApiCall.ReadAsync<BookingDto>(response, cancellationToken);
    }

    /// <summary>Cancels or rebooks through the anonymous manage-token route.</summary>
    public async Task<ApiOutcome<CancelOutcomeDto>> CancelAsync(
        string manageToken, bool rebook, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            $"/api/booking/manage/{Uri.EscapeDataString(manageToken)}/cancel",
            new { Rebook = rebook },
            cancellationToken);

        var outcome = await ApiCall.ReadAsync<CancelOutcomeDto>(response, cancellationToken);

        return outcome;
    }
}
`````

## before — src/EventBooking.Web/Services/CandidatePresentation.cs — 1/1

<!-- vocabulary-file: {"id":207,"oldPath":"src/EventBooking.Web/Services/CandidatePresentation.cs","newPath":"src/EventBooking.Web/Services/AttendeePresentation.cs","beforeSha":"a68fbf448e189fd20bde8e82e5eace23e9e0ccc92eeb11ff2300491bc127f167","afterSha":"2cbbf2117cf275acca6932ebef8a837bf13e64486007f1572e325861cc461404","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Web.Services;

/// <summary>Maps the API's raw candidate status value to the candidate-list visual state.</summary>
internal static class CandidatePresentation
{
    // CandidateStatus is serialized as its documented numeric value across the API boundary.
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

    /// <summary>Maps the API's readiness code to the candidate-list badge style.</summary>
    internal static string ReadinessCssClass(string code) => code switch
    {
        "Ready" => "status-success",
        "EmployeeGroupUnassigned" or "RequirementSnapshotMismatch" or "AppointmentsOutstanding" => "status-warning",
        "NoActiveBooking" => "status-neutral",
        _ => "status-neutral",
    };

    /// <summary>Maps the API's readiness code to its badge glyph.</summary>
    internal static string ReadinessIcon(string code) => code switch
    {
        "Ready" => "✓",
        "EmployeeGroupUnassigned" => "!",
        "NoActiveBooking" => "○",
        "RequirementSnapshotMismatch" => "≠",
        "AppointmentsOutstanding" => "•",
        _ => "?",
    };
}
`````

## after — src/EventBooking.Web/Services/AttendeePresentation.cs — 1/1

<!-- vocabulary-file: {"id":207,"oldPath":"src/EventBooking.Web/Services/CandidatePresentation.cs","newPath":"src/EventBooking.Web/Services/AttendeePresentation.cs","beforeSha":"a68fbf448e189fd20bde8e82e5eace23e9e0ccc92eeb11ff2300491bc127f167","afterSha":"2cbbf2117cf275acca6932ebef8a837bf13e64486007f1572e325861cc461404","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## before — src/EventBooking.Web/Services/CandidatesClient.cs — 1/1

<!-- vocabulary-file: {"id":208,"oldPath":"src/EventBooking.Web/Services/CandidatesClient.cs","newPath":"src/EventBooking.Web/Services/AttendeesClient.cs","beforeSha":"279fadfd14a0ad42c07a6d15eafece9ab0697c6a36b4c4de93dd44fceb69761f","afterSha":"232ed7e56281f88f80ce96b6427d449e7ac3e0d26d8f1d72976960f825fefd1d","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Json;
using System.Text;

namespace EventBooking.Web.Services;

public sealed record AppointmentTypeSummaryDto(string Code, string Name);

public sealed record EmployeeGroupOptionDto(
    Guid EmployeeGroupId,
    string Code,
    string Name,
    IReadOnlyList<AppointmentTypeSummaryDto> RequiredAppointmentTypes);

public sealed record CandidateDto(
    Guid CandidateId,
    string Name,
    string Email,
    Guid? EmployeeGroupId,
    string? EmployeeGroupCode,
    string? EmployeeGroupName,
    bool RequiresEmployeeGroupReconciliation,
    IReadOnlyList<AppointmentTypeSummaryDto> RequiredAppointmentTypes,
    int Status,
    string StatusDisplay);

public sealed record ImportErrorDto(int LineNumber, string Message);

public sealed record ImportOutcomeDto(
    bool Accepted,
    int ImportedCount,
    IReadOnlyList<ImportErrorDto> Errors);

/// <summary>Reports the durable result of a template-aware email retry.</summary>
/// <param name="DeliveryStatus">The provider outcome of the replacement attempt.</param>
/// <param name="DeliveryId">The new durable delivery identifier.</param>
public sealed record EmailRetryDto(string DeliveryStatus, Guid DeliveryId);

/// <summary>Minimum canonical detail for one incomplete appointment type.</summary>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="Name">The canonical appointment-type name.</param>
/// <param name="IsRecoverable">Whether recovery can currently be started for this type.</param>
public sealed record OutstandingAppointmentTypeDto(string Code, string Name, bool IsRecoverable);

/// <summary>Durable delivery outcome for one started recovery invite.</summary>
/// <param name="InviteId">The new recovery Invite identifier, or empty when awaiting availability.</param>
/// <param name="AppointmentTypeIds">The recoverable snapshot offered, or awaiting availability.</param>
/// <param name="EmailSent">Whether the post-commit provider attempt completed successfully.</param>
public sealed record RecoveryInviteOutcomeDto(
    Guid InviteId,
    IReadOnlyList<Guid> AppointmentTypeIds,
    bool EmailSent);

/// <summary>One active booking a coordinator may cancel; carries no management token.</summary>
/// <param name="BookingId">The booking identifier used to target a cancellation.</param>
/// <param name="IsOriginal">True for the original booking; false for an active recovery booking.</param>
/// <param name="SlotDate">The date of the confirmed window the booking holds.</param>
/// <param name="SlotStartTime">The start of the confirmed window the booking holds.</param>
/// <param name="SlotEndTime">The end of the confirmed window the booking holds.</param>
public sealed record CandidateBookingDto(
    Guid BookingId,
    bool IsOriginal,
    DateOnly SlotDate,
    TimeOnly SlotStartTime,
    TimeOnly SlotEndTime);

/// <summary>Coordinator-facing outcome of cancelling one candidate booking.</summary>
/// <param name="Reinvited">Whether a replacement invite was created for the candidate.</param>
/// <param name="InviteCreated">The explicit replacement-invite creation state.</param>
/// <param name="DeliveryStatus">The provider outcome, or Unavailable when no replacement invite exists.</param>
/// <param name="DeliveryId">The durable replacement delivery identifier, when one was staged.</param>
public sealed record CancelCandidateBookingDto(
    bool Reinvited,
    bool InviteCreated,
    string? DeliveryStatus,
    Guid? DeliveryId);

/// <summary>Coordinator-facing readiness for one candidate.</summary>
/// <param name="CandidateId">The stable candidate identifier.</param>
/// <param name="Code">The stable machine-readable readiness reason.</param>
/// <param name="Display">The Coordinator-facing explanation.</param>
/// <param name="OutstandingAppointmentTypes">Incomplete current appointment types.</param>
public sealed record CandidateReadinessDto(
    Guid CandidateId,
    string Code,
    string Display,
    IReadOnlyList<OutstandingAppointmentTypeDto> OutstandingAppointmentTypes);

public sealed class CandidatesClient(HttpClient http)
{
    public async Task<ApiOutcome<List<CandidateDto>>> ListAsync(
        int? status, string? search, CancellationToken cancellationToken)
    {
        var parameters = new List<string>();
        if (status is not null)
        {
            parameters.Add($"status={status.Value}");
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            parameters.Add($"search={Uri.EscapeDataString(search)}");
        }

        var route = parameters.Count == 0
            ? "/api/candidates"
            : $"/api/candidates?{string.Join("&", parameters)}";

        using var response = await http.GetAsync(route, cancellationToken);
        return await ApiCall.ReadAsync<List<CandidateDto>>(response, cancellationToken);
    }

    public async Task<ApiOutcome<List<EmployeeGroupOptionDto>>> ListGroupsAsync(
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/employee-groups", cancellationToken);
        return await ApiCall.ReadAsync<List<EmployeeGroupOptionDto>>(response, cancellationToken);
    }

    public async Task<ApiOutcome<Guid>> CreateAsync(
        string name, string email, Guid? employeeGroupId, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            "/api/candidates",
            new { Name = name, Email = email, EmployeeGroupId = employeeGroupId },
            cancellationToken);

        return await ApiCall.ReadAsync<Guid>(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> UpdateAsync(
        Guid id, string name, string email, Guid? employeeGroupId, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync(
            $"/api/candidates/{id}",
            new { Name = name, Email = email, EmployeeGroupId = employeeGroupId },
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> DeleteAsync(
        Guid id, bool confirm, CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/candidates/{id}?confirm={(confirm ? "true" : "false")}", cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    public async Task<ApiOutcome<ImportOutcomeDto>> ImportAsync(
        string csv, CancellationToken cancellationToken)
    {
        using var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        using var response = await http.PostAsync("/api/candidates/import", content, cancellationToken);

        return await ApiCall.ReadAsync<ImportOutcomeDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> TriggerInviteAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync($"/api/candidates/{id}/invite", null, cancellationToken);
        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    /// <summary>Retries the latest failed or pending delivery using its server-side template.</summary>
    public async Task<ApiOutcome<EmailRetryDto>> RetryEmailAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync($"/api/candidates/{id}/email-retry", null, cancellationToken);
        return await ApiCall.ReadAsync<EmailRetryDto>(response, cancellationToken);
    }

    /// <summary>Starts one recovery invite for the candidate's missed appointments.</summary>
    public async Task<ApiOutcome<RecoveryInviteOutcomeDto>> StartRecoveryAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(
            $"/api/candidates/{candidateId}/recovery-invites", null, cancellationToken);
        return await ApiCall.ReadAsync<RecoveryInviteOutcomeDto>(response, cancellationToken);
    }

    /// <summary>Cancels one pending recovery invite without touching bookings.</summary>
    public async Task<ApiOutcome<bool>> CancelRecoveryAsync(
        Guid candidateId,
        Guid inviteId,
        CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/candidates/{candidateId}/recovery-invites/{inviteId}", cancellationToken);
        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    /// <summary>Lists the candidate's active bookings for the cancellation workflow.</summary>
    /// <param name="candidateId">The candidate whose bookings are listed.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The active bookings, or the failure the API reported.</returns>
    public async Task<ApiOutcome<List<CandidateBookingDto>>> GetBookingsAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(
            $"/api/candidates/{candidateId}/bookings", cancellationToken);
        return await ApiCall.ReadAsync<List<CandidateBookingDto>>(response, cancellationToken);
    }

    /// <summary>
    /// Cancels one of the candidate's active bookings. Requesting a replacement invite is valid
    /// only for the original booking; the API refuses it for a recovery booking.
    /// </summary>
    /// <param name="candidateId">The candidate the booking belongs to.</param>
    /// <param name="bookingId">The booking to cancel.</param>
    /// <param name="rebook">Whether to issue a replacement invite.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The cancellation outcome, or the failure the API reported.</returns>
    public async Task<ApiOutcome<CancelCandidateBookingDto>> CancelBookingAsync(
        Guid candidateId,
        Guid bookingId,
        bool rebook,
        CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(
            $"/api/candidates/{candidateId}/bookings/{bookingId}/cancel",
            new { Rebook = rebook },
            cancellationToken);
        return await ApiCall.ReadAsync<CancelCandidateBookingDto>(response, cancellationToken);
    }

    /// <summary>Gets internal readiness for a visible Candidate.</summary>
    public async Task<ApiOutcome<CandidateReadinessDto>> GetReadinessAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(
            $"/api/candidates/{candidateId}/readiness", cancellationToken);
        return await ApiCall.ReadAsync<CandidateReadinessDto>(response, cancellationToken);
    }
}
`````

## after — src/EventBooking.Web/Services/AttendeesClient.cs — 1/1

<!-- vocabulary-file: {"id":208,"oldPath":"src/EventBooking.Web/Services/CandidatesClient.cs","newPath":"src/EventBooking.Web/Services/AttendeesClient.cs","beforeSha":"279fadfd14a0ad42c07a6d15eafece9ab0697c6a36b4c4de93dd44fceb69761f","afterSha":"232ed7e56281f88f80ce96b6427d449e7ac3e0d26d8f1d72976960f825fefd1d","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Json;
using System.Text;

namespace EventBooking.Web.Services;

public sealed record AppointmentTypeSummaryDto(string Code, string Name);

public sealed record AttendeeGroupOptionDto(
    Guid AttendeeGroupId,
    string Code,
    string Name,
    IReadOnlyList<AppointmentTypeSummaryDto> RequiredAppointmentTypes);

public sealed record AttendeeDto(
    Guid AttendeeId,
    string Name,
    string Email,
    Guid? AttendeeGroupId,
    string? AttendeeGroupCode,
    string? AttendeeGroupName,
    bool RequiresAttendeeGroupReconciliation,
    IReadOnlyList<AppointmentTypeSummaryDto> RequiredAppointmentTypes,
    int Status,
    string StatusDisplay);

public sealed record ImportErrorDto(int LineNumber, string Message);

public sealed record ImportOutcomeDto(
    bool Accepted,
    int ImportedCount,
    IReadOnlyList<ImportErrorDto> Errors);

/// <summary>Reports the durable result of a template-aware email retry.</summary>
/// <param name="DeliveryStatus">The provider outcome of the replacement attempt.</param>
/// <param name="DeliveryId">The new durable delivery identifier.</param>
public sealed record EmailRetryDto(string DeliveryStatus, Guid DeliveryId);

/// <summary>Minimum canonical detail for one incomplete appointment type.</summary>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="Name">The canonical appointment-type name.</param>
/// <param name="IsRecoverable">Whether recovery can currently be started for this type.</param>
public sealed record OutstandingAppointmentTypeDto(string Code, string Name, bool IsRecoverable);

/// <summary>Durable delivery outcome for one started recovery invite.</summary>
/// <param name="InviteId">The new recovery Invite identifier, or empty when awaiting availability.</param>
/// <param name="AppointmentTypeIds">The recoverable snapshot offered, or awaiting availability.</param>
/// <param name="EmailSent">Whether the post-commit provider attempt completed successfully.</param>
public sealed record RecoveryInviteOutcomeDto(
    Guid InviteId,
    IReadOnlyList<Guid> AppointmentTypeIds,
    bool EmailSent);

/// <summary>One active booking a coordinator may cancel; carries no management token.</summary>
/// <param name="BookingId">The booking identifier used to target a cancellation.</param>
/// <param name="IsOriginal">True for the original booking; false for an active recovery booking.</param>
/// <param name="EventDate">The date of the confirmed window the booking holds.</param>
/// <param name="EventStartTime">The start of the confirmed window the booking holds.</param>
/// <param name="EventEndTime">The end of the confirmed window the booking holds.</param>
public sealed record AttendeeBookingDto(
    Guid BookingId,
    bool IsOriginal,
    DateOnly EventDate,
    TimeOnly EventStartTime,
    TimeOnly EventEndTime);

/// <summary>Coordinator-facing outcome of cancelling one attendee booking.</summary>
/// <param name="Reinvited">Whether a replacement invite was created for the attendee.</param>
/// <param name="InviteCreated">The explicit replacement-invite creation state.</param>
/// <param name="DeliveryStatus">The provider outcome, or Unavailable when no replacement invite exists.</param>
/// <param name="DeliveryId">The durable replacement delivery identifier, when one was staged.</param>
public sealed record CancelAttendeeBookingDto(
    bool Reinvited,
    bool InviteCreated,
    string? DeliveryStatus,
    Guid? DeliveryId);

/// <summary>Coordinator-facing readiness for one attendee.</summary>
/// <param name="AttendeeId">The stable attendee identifier.</param>
/// <param name="Code">The stable machine-readable readiness reason.</param>
/// <param name="Display">The Coordinator-facing explanation.</param>
/// <param name="OutstandingAppointmentTypes">Incomplete current appointment types.</param>
public sealed record AttendeeReadinessDto(
    Guid AttendeeId,
    string Code,
    string Display,
    IReadOnlyList<OutstandingAppointmentTypeDto> OutstandingAppointmentTypes);

public sealed class AttendeesClient(HttpClient http)
{
    public async Task<ApiOutcome<List<AttendeeDto>>> ListAsync(
        int? status, string? search, CancellationToken cancellationToken)
    {
        var parameters = new List<string>();
        if (status is not null)
        {
            parameters.Add($"status={status.Value}");
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            parameters.Add($"search={Uri.EscapeDataString(search)}");
        }

        var route = parameters.Count == 0
            ? "/api/attendees"
            : $"/api/attendees?{string.Join("&", parameters)}";

        using var response = await http.GetAsync(route, cancellationToken);
        return await ApiCall.ReadAsync<List<AttendeeDto>>(response, cancellationToken);
    }

    public async Task<ApiOutcome<List<AttendeeGroupOptionDto>>> ListGroupsAsync(
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/attendee-groups", cancellationToken);
        return await ApiCall.ReadAsync<List<AttendeeGroupOptionDto>>(response, cancellationToken);
    }

    public async Task<ApiOutcome<Guid>> CreateAsync(
        string name, string email, Guid? attendeeGroupId, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            "/api/attendees",
            new { Name = name, Email = email, AttendeeGroupId = attendeeGroupId },
            cancellationToken);

        return await ApiCall.ReadAsync<Guid>(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> UpdateAsync(
        Guid id, string name, string email, Guid? attendeeGroupId, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync(
            $"/api/attendees/{id}",
            new { Name = name, Email = email, AttendeeGroupId = attendeeGroupId },
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> DeleteAsync(
        Guid id, bool confirm, CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/attendees/{id}?confirm={(confirm ? "true" : "false")}", cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    public async Task<ApiOutcome<ImportOutcomeDto>> ImportAsync(
        string csv, CancellationToken cancellationToken)
    {
        using var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        using var response = await http.PostAsync("/api/attendees/import", content, cancellationToken);

        return await ApiCall.ReadAsync<ImportOutcomeDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> TriggerInviteAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync($"/api/attendees/{id}/invite", null, cancellationToken);
        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    /// <summary>Retries the latest failed or pending delivery using its server-side template.</summary>
    public async Task<ApiOutcome<EmailRetryDto>> RetryEmailAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync($"/api/attendees/{id}/email-retry", null, cancellationToken);
        return await ApiCall.ReadAsync<EmailRetryDto>(response, cancellationToken);
    }

    /// <summary>Starts one recovery invite for the attendee's missed appointments.</summary>
    public async Task<ApiOutcome<RecoveryInviteOutcomeDto>> StartRecoveryAsync(
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(
            $"/api/attendees/{attendeeId}/recovery-invites", null, cancellationToken);
        return await ApiCall.ReadAsync<RecoveryInviteOutcomeDto>(response, cancellationToken);
    }

    /// <summary>Cancels one pending recovery invite without touching bookings.</summary>
    public async Task<ApiOutcome<bool>> CancelRecoveryAsync(
        Guid attendeeId,
        Guid inviteId,
        CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/attendees/{attendeeId}/recovery-invites/{inviteId}", cancellationToken);
        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    /// <summary>Lists the attendee's active bookings for the cancellation workflow.</summary>
    /// <param name="attendeeId">The attendee whose bookings are listed.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The active bookings, or the failure the API reported.</returns>
    public async Task<ApiOutcome<List<AttendeeBookingDto>>> GetBookingsAsync(
        Guid attendeeId,
        CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(
            $"/api/attendees/{attendeeId}/bookings", cancellationToken);
        return await ApiCall.ReadAsync<List<AttendeeBookingDto>>(response, cancellationToken);
    }

    /// <summary>
    /// Cancels one of the attendee's active bookings. Requesting a replacement invite is valid
    /// only for the original booking; the API refuses it for a recovery booking.
    /// </summary>
    /// <param name="attendeeId">The attendee the booking belongs to.</param>
    /// <param name="bookingId">The booking to cancel.</param>
    /// <param name="rebook">Whether to issue a replacement invite.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The cancellation outcome, or the failure the API reported.</returns>
    public async Task<ApiOutcome<CancelAttendeeBookingDto>> CancelBookingAsync(
        Guid attendeeId,
        Guid bookingId,
        bool rebook,
        CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(
            $"/api/attendees/{attendeeId}/bookings/{bookingId}/cancel",
            new { Rebook = rebook },
            cancellationToken);
        return await ApiCall.ReadAsync<CancelAttendeeBookingDto>(response, cancellationToken);
    }

    /// <summary>Gets internal readiness for a visible Attendee.</summary>
    public async Task<ApiOutcome<AttendeeReadinessDto>> GetReadinessAsync(
        Guid attendeeId,
        CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(
            $"/api/attendees/{attendeeId}/readiness", cancellationToken);
        return await ApiCall.ReadAsync<AttendeeReadinessDto>(response, cancellationToken);
    }
}
`````

## before — src/EventBooking.Web/Services/ConfirmedSlotsClient.cs — 1/1

<!-- vocabulary-file: {"id":209,"oldPath":"src/EventBooking.Web/Services/ConfirmedSlotsClient.cs","newPath":"src/EventBooking.Web/Services/EventOperationsClient.cs","beforeSha":"63118fb4e9f043e63350443506707774aaeeb7a8029571f424d56414c35126dc","afterSha":"edb776e0de9414fcdf661460c0ce44ea365dee98e2d985d2366289df5c6f5d10","side":"before","part":1,"parts":1} -->

`````csharp
using System.Text;

namespace EventBooking.Web.Services;

public sealed record SlotImportErrorDto(int LineNumber, string Message);

public sealed record SlotImportOutcomeDto(
    bool Accepted,
    int ImportedCount,
    IReadOnlyList<SlotImportErrorDto> Errors);

public sealed class ConfirmedSlotsClient(HttpClient http)
{
    public async Task<ApiOutcome<SlotImportOutcomeDto>> ImportAsync(
        string csv,
        CancellationToken cancellationToken)
    {
        using var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        using var response = await http.PostAsync(
            "/api/confirmed-slots/import", content, cancellationToken);
        return await ApiCall.ReadAsync<SlotImportOutcomeDto>(response, cancellationToken);
    }
}
`````

## after — src/EventBooking.Web/Services/EventOperationsClient.cs — 1/1

<!-- vocabulary-file: {"id":209,"oldPath":"src/EventBooking.Web/Services/ConfirmedSlotsClient.cs","newPath":"src/EventBooking.Web/Services/EventOperationsClient.cs","beforeSha":"63118fb4e9f043e63350443506707774aaeeb7a8029571f424d56414c35126dc","afterSha":"edb776e0de9414fcdf661460c0ce44ea365dee98e2d985d2366289df5c6f5d10","side":"after","part":1,"parts":1} -->

`````csharp
using System.Text;

namespace EventBooking.Web.Services;

public sealed record EventImportErrorDto(int LineNumber, string Message);

public sealed record EventImportOutcomeDto(
    bool Accepted,
    int ImportedCount,
    IReadOnlyList<EventImportErrorDto> Errors);

public sealed class EventOperationsClient(HttpClient http)
{
    public async Task<ApiOutcome<EventImportOutcomeDto>> ImportAsync(
        string csv,
        CancellationToken cancellationToken)
    {
        using var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        using var response = await http.PostAsync(
            "/api/events/import", content, cancellationToken);
        return await ApiCall.ReadAsync<EventImportOutcomeDto>(response, cancellationToken);
    }
}
`````

## before — src/EventBooking.Web/Services/DashboardsClient.cs — 1/1

<!-- vocabulary-file: {"id":210,"oldPath":"src/EventBooking.Web/Services/DashboardsClient.cs","newPath":"src/EventBooking.Web/Services/DashboardsClient.cs","beforeSha":"5dd478529d42d9ef584a296524229aa4ce2524d3e31710fbee127b17c50486c8","afterSha":"93784643d93f190e15300099506719e2f065847e8c520aeb566ea4ac46a79758","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Web.Services;

public sealed record AwaitingRowDto(
    Guid CandidateId,
    string Name,
    string Email,
    IReadOnlyList<string> RequiredCodes,
    DateOnly WaitingSince,
    int DaysWaiting);

public sealed record NoResponseRowDto(
    Guid CandidateId,
    string Name,
    string Email,
    IReadOnlyList<string> RequiredCodes,
    DateOnly GaveUpOn);

public sealed record SlotCapacityRowDto(string Code, int TotalHeadcount, int RemainingCapacity);

public sealed record SlotRowDto(
    Guid ConfirmedSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<SlotCapacityRowDto> Capacities,
    int ActiveBookings);

/// <summary>The latest candidate delivery status projected for staff pages.</summary>
/// <param name="CandidateId">The candidate whose delivery is shown.</param>
/// <param name="TemplateDisplay">Human-readable template name.</param>
/// <param name="SentAt">The latest attempt or pending timestamp.</param>
/// <param name="Status">The durable delivery status.</param>
/// <param name="CanRetry">Whether current server-side state permits a retry.</param>
public sealed record CandidateEmailStatusDto(
    Guid CandidateId,
    string TemplateDisplay,
    DateTimeOffset SentAt,
    string Status,
    bool CanRetry);

public sealed record DashboardsDto(
    IReadOnlyList<AwaitingRowDto> AwaitingAvailability,
    IReadOnlyList<NoResponseRowDto> NoResponse,
    IReadOnlyList<SlotRowDto> Slots,
    IReadOnlyList<CandidateEmailStatusDto> EmailStatuses);

public sealed class DashboardsClient(HttpClient http)
{
    public async Task<ApiOutcome<DashboardsDto>> GetAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/dashboards", cancellationToken);
        return await ApiCall.ReadAsync<DashboardsDto>(response, cancellationToken);
    }
}
`````
