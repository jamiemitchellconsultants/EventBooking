# 00a — Port source 36 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.Web/Services/AdminClient.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Services/AdminClient.cs","encoding":"utf8","sha256":"53232c565485beec55d39d3fa5d4569bbf3b44c83c8aafac767ce89d38591ece","parts":1,"part":1} -->

`````csharp
using System.Net.Http.Json;
using System.Text;

namespace EventBooking.Web.Services;

/// <summary>One appointment type and the manager assigned to it, when there is one.</summary>
/// <param name="Id">The appointment type identifier.</param>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="Name">The canonical appointment-type name.</param>
/// <param name="ManagerUserId">The assigned manager's provider identity, or null when unassigned.</param>
/// <param name="ManagerStaffId">The manager's enterprise staff number; null until an identity is recorded.</param>
/// <param name="ManagerDisplayName">
/// The manager's name mirrored from the identity provider; null when the identity carries none.
/// </param>
public sealed record AppointmentTypeDto(
    Guid Id,
    string Code,
    string Name,
    Guid? ManagerUserId,
    string? ManagerStaffId = null,
    string? ManagerDisplayName = null);

public sealed record SettingsDto(
    int InviteExpiryDays,
    int MaxAutoRetryCount,
    IReadOnlyList<AppointmentTypeDto> AppointmentTypes);

public sealed class AdminClient(HttpClient http)
{
    public async Task<ApiOutcome<SettingsDto>> GetAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/admin/settings", cancellationToken);
        return await ApiCall.ReadAsync<SettingsDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> UpdateAsync(
        int inviteExpiryDays, int maxAutoRetryCount, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync(
            "/api/admin/settings",
            new { InviteExpiryDays = inviteExpiryDays, MaxAutoRetryCount = maxAutoRetryCount },
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }
}
`````

## src/EventBooking.Web/Services/ApiCall.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Services/ApiCall.cs","encoding":"utf8","sha256":"832ab3a6d4f9def21c57521de9aac1c911f44559f323227c4286ae4b2c808ed9","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace EventBooking.Web.Services;

/// <summary>Reads API responses into success values or safe structured failures.</summary>
public static class ApiCall
{
    private const string Generic = "Something went wrong. Please try again.";
    private const string Forbidden = "You do not have permission to do that.";

    private sealed record ProblemDetailsBody(string? Title, string? Detail, int? Status);
    private sealed record FailureDetails(string Message, string? ErrorCode);

    /// <summary>Reads a response that must contain a JSON body when successful.</summary>
    public static async Task<ApiOutcome<T>> ReadAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;

        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            return value is null
                ? ApiOutcome<T>.Failure(Generic, status)
                : ApiOutcome<T>.Success(value, status);
        }

        var failure = await FailureOf(response, cancellationToken);
        return ApiOutcome<T>.Failure(failure.Message, status, failure.ErrorCode);
    }

    /// <summary>Reads a response whose successful result carries no body.</summary>
    public static async Task<ApiOutcome<bool>> ReadNoContentAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;

        if (response.IsSuccessStatusCode)
        {
            return ApiOutcome<bool>.Success(true, status);
        }

        var failure = await FailureOf(response, cancellationToken);
        return ApiOutcome<bool>.Failure(failure.Message, status, failure.ErrorCode);
    }

    /// <summary>Reads a successful plain-text body plus one response header value.</summary>
    /// <param name="response">The HTTP response to read.</param>
    /// <param name="headerName">The response header to capture, for example Content-Disposition.</param>
    /// <param name="cancellationToken">Cancels reading the body.</param>
    /// <returns>The body text and header value on success, or the parsed problem failure.</returns>
    public static async Task<ApiOutcome<TextWithHeader>> ReadTextWithHeaderAsync(
        HttpResponseMessage response,
        string headerName,
        CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;

        if (!response.IsSuccessStatusCode)
        {
            var failure = await FailureOf(response, cancellationToken);
            return ApiOutcome<TextWithHeader>.Failure(failure.Message, status, failure.ErrorCode);
        }

        // Deliberately not the JSON path: the successful body here is raw text, not JSON.
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var header =
            (response.Content.Headers.TryGetValues(headerName, out var contentValues)
                ? contentValues.FirstOrDefault()
                : null)
            ?? (response.Headers.TryGetValues(headerName, out var values)
                ? values.FirstOrDefault()
                : null)
            ?? string.Empty;

        return ApiOutcome<TextWithHeader>.Success(new TextWithHeader(body, header), status);
    }

    private static async Task<FailureDetails> FailureOf(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return new FailureDetails(Forbidden, "forbidden");
        }

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsBody>(cancellationToken);
            var message = string.IsNullOrWhiteSpace(problem?.Detail) ? Generic : problem.Detail;
            var errorCode = string.IsNullOrWhiteSpace(problem?.Title) ? null : problem.Title;
            return new FailureDetails(message, errorCode);
        }
        catch (JsonException)
        {
            return new FailureDetails(Generic, null);
        }
        catch (NotSupportedException)
        {
            return new FailureDetails(Generic, null);
        }
    }
}

/// <summary>A successful plain-text response body together with one captured response header.</summary>
/// <param name="Text">The raw response body.</param>
/// <param name="Header">The captured header value, or empty when the header was absent.</param>
public sealed record TextWithHeader(string Text, string Header);
`````

## src/EventBooking.Web/Services/ApiOutcome.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Services/ApiOutcome.cs","encoding":"utf8","sha256":"8525a77ef0b02e7c1850c9662f3d30499a1a05915523726dac6c20d523c3d2ab","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Web.Services;

/// <summary>Represents the success value or safe failure returned by an API call.</summary>
/// <typeparam name="T">The successful response body type.</typeparam>
/// <param name="IsSuccess">Whether the API call completed successfully.</param>
/// <param name="Value">The successful response body, or null after failure.</param>
/// <param name="ErrorMessage">The safe user-facing failure message, or null after success.</param>
/// <param name="StatusCode">The HTTP response status code.</param>
/// <param name="ErrorCode">The stable machine-readable problem code, when supplied.</param>
public sealed record ApiOutcome<T>(
    bool IsSuccess,
    T? Value,
    string? ErrorMessage,
    int StatusCode,
    string? ErrorCode = null)
{
    /// <summary>Creates a successful outcome with its response body and HTTP status.</summary>
    public static ApiOutcome<T> Success(T value, int statusCode) =>
        new(true, value, null, statusCode, null);

    /// <summary>Creates a failed outcome with a safe message and optional machine-readable code.</summary>
    public static ApiOutcome<T> Failure(
        string message,
        int statusCode,
        string? errorCode = null) =>
        new(false, default, message, statusCode, errorCode);
}
`````

## src/EventBooking.Web/Services/AppointmentsClient.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Services/AppointmentsClient.cs","encoding":"utf8","sha256":"cbdecefd2bf0b0e4f5ec7435f94349bec77b1c4e06dc2425a87790f278bd2694","parts":1,"part":1} -->

`````csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EventBooking.Web.Services;

/// <summary>Counts scoped booking appointments in each operational state.</summary>
public sealed record AppointmentStatusCountsDto
{
    /// <summary>Gets candidates booked but not checked in for this appointment.</summary>
    public required int Expected { get; init; }

    /// <summary>Gets candidates checked in for this appointment.</summary>
    public required int CheckedIn { get; init; }

    /// <summary>Gets required appointments completed after check-in.</summary>
    public required int Completed { get; init; }

    /// <summary>Gets candidates recorded as not attending this required appointment.</summary>
    public required int NoShow { get; init; }
}

/// <summary>Describes one selectable active slot without candidate rows.</summary>
public sealed record AppointmentSlotSummaryDto
{
    /// <summary>Gets the confirmed slot identifier.</summary>
    public required Guid ConfirmedSlotId { get; init; }

    /// <summary>Gets the slot's head-office calendar date.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Gets the start of the shared four-hour window.</summary>
    public required TimeOnly StartTime { get; init; }

    /// <summary>Gets the derived end of the shared four-hour window.</summary>
    public required TimeOnly EndTime { get; init; }

    /// <summary>Gets scoped counts grouped by independent operational state.</summary>
    public required AppointmentStatusCountsDto Counts { get; init; }
}

/// <summary>Returns the trusted appointment-type name and its selectable active slots.</summary>
public sealed record AppointmentWorkspaceSlotListDto
{
    /// <summary>Gets the fixed appointment-type name for the caller's trusted scope.</summary>
    public required string AppointmentTypeName { get; init; }

    /// <summary>Gets current and upcoming active slots containing scoped active bookings.</summary>
    public required IReadOnlyList<AppointmentSlotSummaryDto> Slots { get; init; }
}

/// <summary>Contains only the fields needed to identify and conduct one booked appointment.</summary>
public sealed record BookingAppointmentRowDto
{
    /// <summary>Gets the stable booking-appointment command identifier.</summary>
    public required Guid BookingAppointmentId { get; init; }

    /// <summary>Gets the candidate name used for primary human identification.</summary>
    public required string CandidateName { get; init; }

    /// <summary>Gets the candidate email used for secondary human identification.</summary>
    public required string CandidateEmail { get; init; }

    /// <summary>Gets this appointment's independent operational status name.</summary>
    public required string Status { get; init; }

    /// <summary>Gets when staff checked the candidate in, or null until check-in.</summary>
    public DateTimeOffset? CheckedInAt { get; init; }

    /// <summary>Gets when staff recorded completion or no-show, or null before an outcome.</summary>
    public DateTimeOffset? OutcomeAt { get; init; }

    /// <summary>Gets the positive concurrency version required by a status command.</summary>
    public required long Version { get; init; }
}

/// <summary>Returns one scoped active slot and only its minimum-data operational rows.</summary>
public sealed record AppointmentSlotDetailDto
{
    /// <summary>Gets the fixed appointment-type name for the caller's trusted scope.</summary>
    public required string AppointmentTypeName { get; init; }

    /// <summary>Gets the selected confirmed slot identifier.</summary>
    public required Guid ConfirmedSlotId { get; init; }

    /// <summary>Gets the slot's head-office calendar date.</summary>
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

    /// <summary>Gets when staff checked the candidate in, or null until check-in.</summary>
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

    /// <summary>Gets the scoped current and upcoming slot list.</summary>
    public async Task<ApiOutcome<AppointmentWorkspaceSlotListDto>> ListSlotsAsync(
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            "/api/appointment-workspace/slots", cancellationToken);
        return await ApiCall.ReadAsync<AppointmentWorkspaceSlotListDto>(response, cancellationToken);
    }

    /// <summary>Gets one selected slot's scoped operational rows.</summary>
    public async Task<ApiOutcome<AppointmentSlotDetailDto>> GetSlotAsync(
        Guid confirmedSlotId,
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"/api/appointment-workspace/slots/{confirmedSlotId}", cancellationToken);
        return await ApiCall.ReadAsync<AppointmentSlotDetailDto>(response, cancellationToken);
    }

    /// <summary>Downloads the scoped roster CSV for one selected slot.</summary>
    /// <param name="confirmedSlotId">The selected slot identifier.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Raw CSV content plus the server-suggested filename, or the parsed failure.</returns>
    public async Task<ApiOutcome<RosterCsvDownload>> GetRosterAsync(
        Guid confirmedSlotId,
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"/api/appointment-workspace/slots/{confirmedSlotId}/roster", cancellationToken);
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

## src/EventBooking.Web/Services/AuditClient.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Services/AuditClient.cs","encoding":"utf8","sha256":"df594bb5624cf8194f081270466ce584bb140a4eb00b1ce1835b36443b2bdab2","parts":1,"part":1} -->

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

## src/EventBooking.Web/Services/BookingClient.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Services/BookingClient.cs","encoding":"utf8","sha256":"a05125e9eae4ee70436f3e8abe3b35b715742b99b4c0b551076aa3ace7f46268","parts":1,"part":1} -->

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

## src/EventBooking.Web/Services/CandidatePresentation.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Services/CandidatePresentation.cs","encoding":"utf8","sha256":"a68fbf448e189fd20bde8e82e5eace23e9e0ccc92eeb11ff2300491bc127f167","parts":1,"part":1} -->

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

## src/EventBooking.Web/Services/CandidatesClient.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Services/CandidatesClient.cs","encoding":"utf8","sha256":"279fadfd14a0ad42c07a6d15eafece9ab0697c6a36b4c4de93dd44fceb69761f","parts":1,"part":1} -->

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

## src/EventBooking.Web/Services/ConfirmedSlotsClient.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Services/ConfirmedSlotsClient.cs","encoding":"utf8","sha256":"63118fb4e9f043e63350443506707774aaeeb7a8029571f424d56414c35126dc","parts":1,"part":1} -->

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

## src/EventBooking.Web/Services/DashboardsClient.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Services/DashboardsClient.cs","encoding":"utf8","sha256":"5dd478529d42d9ef584a296524229aa4ce2524d3e31710fbee127b17c50486c8","parts":1,"part":1} -->

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

## src/EventBooking.Web/Services/HeadOfficePageClock.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Services/HeadOfficePageClock.cs","encoding":"utf8","sha256":"930b85e1388102e6056813b0258e67ccfbe4b7c15fefd67edaa068aae5556921","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Web.Services;

/// <summary>Supplies browser action gating in the configured head-office time zone.</summary>
public sealed class HeadOfficePageClock
{
    private readonly TimeZoneInfo _zone;
    private readonly Func<DateTimeOffset> _utcNow;

    /// <summary>Creates a clock for one IANA or operating-system time-zone identifier.</summary>
    /// <param name="timeZoneId">The configured head-office time-zone identifier.</param>
    /// <param name="utcNow">An optional UTC source used by deterministic tests.</param>
    public HeadOfficePageClock(string timeZoneId, Func<DateTimeOffset>? utcNow = null)
    {
        _zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Gets the current instant represented in the head-office time zone.</summary>
    public DateTimeOffset NowAtHeadOffice => TimeZoneInfo.ConvertTime(_utcNow(), _zone);
}
`````

## src/EventBooking.Web/Services/HeadOfficeTimePresentation.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Services/HeadOfficeTimePresentation.cs","encoding":"utf8","sha256":"80fc84f3b64e7c0dc1fbcf7d4a58e10b4033b40cdb2caa9ac868ccc58b6b264f","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Web.Services;

/// <summary>
/// Formats UTC audit and delivery instants in the configured head-office local time zone.
/// </summary>
public sealed class HeadOfficeTimePresentation
{
    private readonly TimeZoneInfo _timeZone;
    private readonly string _timeZoneId;

    /// <summary>
    /// Creates presentation formatting for the configured IANA or operating-system time-zone identifier.
    /// </summary>
    /// <param name="timeZoneId">The configured head-office time-zone identifier.</param>
    public HeadOfficeTimePresentation(string timeZoneId)
    {
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        _timeZoneId = timeZoneId;
    }

    /// <summary>
    /// Converts a stored instant to head-office local time and includes its offset and configured zone identifier.
    /// </summary>
    /// <param name="timestamp">The stored instant to present without changing its point in time.</param>
    /// <returns>A local date and time with an unambiguous offset and zone identifier.</returns>
    public string Format(DateTimeOffset timestamp) =>
        $"{TimeZoneInfo.ConvertTime(timestamp, _timeZone):yyyy-MM-dd HH:mm zzz} ({_timeZoneId})";
}
`````

## src/EventBooking.Web/Services/MeClient.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Services/MeClient.cs","encoding":"utf8","sha256":"c43a190988d68e4f2a7e478c664a5dd159341a2dfb2b94bcef875f7ac63dcea1","parts":1,"part":1} -->

`````csharp
using System.Net.Http.Json;

namespace EventBooking.Web.Services;

public sealed record MeDto(
    IReadOnlyList<string> Roles,
    Guid? AppointmentTypeId,
    string? AppointmentTypeName);

public sealed class MeClient(HttpClient http)
{
    public async Task<ApiOutcome<MeDto>> GetAsync(CancellationToken cancellationToken)
    {
        var response = await http.GetAsync("/api/me", cancellationToken);
        return await ApiCall.ReadAsync<MeDto>(response, cancellationToken);
    }
}
`````

## src/EventBooking.Web/Services/SlotsClient.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Services/SlotsClient.cs","encoding":"utf8","sha256":"23af9a3e3b9ed2e83834b5eb9c50daf2a2b7039aaf86492d1ac7f44f17f10b5b","parts":1,"part":1} -->

`````csharp
using System.Net.Http.Json;

namespace EventBooking.Web.Services;

public sealed record OpenProposalDto(
    Guid ProposalId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<string> AcceptedByAppointmentTypeNames,
    int? MyAcceptedHeadcount,
    bool AcceptedByMe,
    bool CreatedByMe);

public sealed record ConfirmedSlotDto(
    Guid ConfirmedSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int MyHeadcount,
    int MyRemainingCapacity);

public sealed record AdjustConfirmedCapacityDto(
    Guid ConfirmedSlotId,
    int TotalHeadcount,
    int RemainingCapacity);

public sealed record SlotBoardDto(
    IReadOnlyList<OpenProposalDto> OpenProposals,
    IReadOnlyList<ConfirmedSlotDto> ConfirmedSlots);

/// <summary>Remaining and accepted places for one appointment type within a confirmed slot.</summary>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="TotalHeadcount">The headcount the manager accepted for this appointment type.</param>
/// <param name="RemainingCapacity">The places still free for this appointment type.</param>
public sealed record SlotOperationCapacityDto(string Code, int TotalHeadcount, int RemainingCapacity);

/// <summary>One confirmed slot in the slot-only operations view; carries no candidate data.</summary>
/// <param name="ConfirmedSlotId">The confirmed slot the row describes.</param>
/// <param name="Date">The date of the confirmed window.</param>
/// <param name="StartTime">The start of the confirmed window.</param>
/// <param name="EndTime">The end of the confirmed window.</param>
/// <param name="Capacities">Per-appointment-type capacity for this window.</param>
/// <param name="ActiveBookings">How many active bookings the window currently holds.</param>
public sealed record SlotOperationDto(
    Guid ConfirmedSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<SlotOperationCapacityDto> Capacities,
    int ActiveBookings);

/// <summary>The slot-only operations view returned to administrators and coordinators.</summary>
/// <param name="Slots">Every active confirmed slot, newest data as the server returned it.</param>
public sealed record SlotOperationsDto(IReadOnlyList<SlotOperationDto> Slots);

public sealed class SlotsClient(HttpClient http)
{
    public async Task<ApiOutcome<SlotBoardDto>> GetBoardAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/slots/board", cancellationToken);
        return await ApiCall.ReadAsync<SlotBoardDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<Guid>> ProposeAsync(
        DateOnly date,
        TimeOnly startTime,
        CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            "/api/slots/proposals",
            new { Date = date, StartTime = startTime },
            cancellationToken);

        return await ApiCall.ReadAsync<Guid>(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> AcceptAsync(
        Guid proposalId,
        int headcount,
        CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            $"/api/slots/proposals/{proposalId}/acceptance",
            new { Headcount = headcount },
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> WithdrawAcceptanceAsync(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/slots/proposals/{proposalId}/acceptance",
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> WithdrawProposalAsync(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/slots/proposals/{proposalId}",
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    public async Task<ApiOutcome<AdjustConfirmedCapacityDto>> AdjustCapacityAsync(
        Guid confirmedSlotId,
        int totalHeadcount,
        CancellationToken cancellationToken)
    {
        var response = await http.PutAsJsonAsync(
            $"/api/slots/confirmed/{confirmedSlotId}/capacity",
            new { TotalHeadcount = totalHeadcount },
            cancellationToken);

        return await ApiCall.ReadAsync<AdjustConfirmedCapacityDto>(
            response,
            cancellationToken);
    }

    /// <summary>Loads the slot-only operations view; never touches candidate data.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every active confirmed slot, or the failure the API reported.</returns>
    public async Task<ApiOutcome<SlotOperationsDto>> GetSlotOperationsAsync(
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/slots/operations", cancellationToken);
        return await ApiCall.ReadAsync<SlotOperationsDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> CancelConfirmedSlotAsync(
        Guid slotId,
        bool confirm,
        CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/slots/confirmed/{slotId}?confirm={(confirm ? "true" : "false")}",
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }
}
`````

## src/EventBooking.Web/Services/StaffAccessClient.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Services/StaffAccessClient.cs","encoding":"utf8","sha256":"5683cc75765869d6e28999dd67c8de3e080a5221b1160dfacb91d93897029df1","parts":1,"part":1} -->

`````csharp
using System.Net.Http.Json;

namespace EventBooking.Web.Services;

/// <summary>Describes a staff access profile for administration. Roles are read-only here.</summary>
public sealed record StaffAccessProfileDto(
    Guid StaffUserId,
    IReadOnlyList<string> Roles,
    Guid? AppointmentTypeId,
    string? AppointmentTypeName,
    long Version,
    string? StaffId = null,
    /// <summary>
    /// The name mirrored from the identity provider, or null when the identity carries none.
    /// </summary>
    string? DisplayName = null);

/// <summary>Describes a scope mutation and any displaced manager.</summary>
public sealed record StaffAccessMutationDto(
    StaffAccessProfileDto Profile,
    Guid? FormerManagerStaffUserId);

/// <summary>Calls the staff-access administration API for appointment-type scope only.</summary>
public sealed class StaffAccessClient(HttpClient http)
{
    /// <summary>Lists every current staff access profile.</summary>
    public async Task<ApiOutcome<IReadOnlyList<StaffAccessProfileDto>>> ListAsync(
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/admin/staff-access", cancellationToken);
        return await ApiCall.ReadAsync<IReadOnlyList<StaffAccessProfileDto>>(
            response, cancellationToken);
    }

    /// <summary>Replaces an existing profile's appointment-type scope.</summary>
    public async Task<ApiOutcome<StaffAccessMutationDto>> ReplaceScopeAsync(
        Guid staffUserId,
        Guid? appointmentTypeId,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync(
            $"/api/admin/staff-access/{staffUserId}",
            new { AppointmentTypeId = appointmentTypeId, ExpectedVersion = expectedVersion },
            cancellationToken);
        return await ApiCall.ReadAsync<StaffAccessMutationDto>(response, cancellationToken);
    }

    /// <summary>Clears an existing profile's appointment-type scope.</summary>
    public async Task<ApiOutcome<bool>> ClearScopeAsync(
        Guid staffUserId,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/admin/staff-access/{staffUserId}?expectedVersion={expectedVersion}",
            cancellationToken);
        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }
}
`````

## src/EventBooking.Web/Services/StaffNavigation.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Services/StaffNavigation.cs","encoding":"utf8","sha256":"1ca7d0e8ef863fb8dd144c5c3caaa8269202f5c6a01b30baa2c4f1433ba1279d","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Web.Services;

/// <summary>Describes one staff navigation destination shown for a role combination.</summary>
/// <param name="Href">The link target shown for the permitted role combination.</param>
/// <param name="Label">The visible link text naming the permitted workspace.</param>
/// <param name="Description">The accessible description of what the workspace offers.</param>
public sealed record StaffLink(string Href, string Label, string Description);

/// <summary>Builds the deterministic navigation union permitted by staff roles.</summary>
public static class StaffNavigation
{
    /// <summary>Builds the deterministic union of links permitted by the caller's roles.</summary>
    /// <param name="me">The authenticated staff identity with its assigned roles.</param>
    /// <returns>The ordered links the caller is permitted to open.</returns>
    public static IReadOnlyList<StaffLink> LinksFor(MeDto me)
    {
        var roles = me.Roles.ToHashSet(StringComparer.Ordinal);
        if (roles.Contains("Admin"))
        {
            return
            [
                new("/settings", "System settings", "Configure invitation timing"),
                new("/staff-access", "Staff access", "Set appointment-type scope"),
                new("/confirmed-slots", "Confirmed slots", "Import already agreed slots"),
                new("/audit", "Audit trail", "Search what changed"),
            ];
        }

        var links = new List<StaffLink>();
        if (roles.Contains("Manager"))
        {
            links.Add(new("/slots", "Slot proposals", "Negotiate and confirm shared windows"));
        }

        if (roles.Contains("Manager") || roles.Contains("AppointmentStaff"))
        {
            links.Add(new(
                "/appointments",
                "Appointments",
                "Check candidates in and record appointment outcomes"));
        }

        if (roles.Contains("Coordinator"))
        {
            links.Add(new("/candidates", "Candidates", "Invite and track candidates"));
            links.Add(new("/dashboards", "Dashboards", "Waiting lists and follow-ups"));
            links.Add(new("/confirmed-slots", "Confirmed slots", "Import already agreed slots"));
            links.Add(new("/audit", "Audit trail", "Search what changed"));
        }

        return links;
    }
}
`````
