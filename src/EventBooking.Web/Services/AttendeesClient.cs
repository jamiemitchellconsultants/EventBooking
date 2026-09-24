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
    string Status,
    string StatusDisplay,
    string GroupCode,
    string Readiness,
    IReadOnlyList<string> RequiredTypeCodes,
    string? LatestDeliveryStatus,
    string Cursor);

public sealed record AttendeeListDto(
    IReadOnlyList<AttendeeDto> Items,
    string? NextCursor);

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
/// <param name="RecoveryInviteId">The newly issued recovery invite identifier.</param>
/// <param name="LocationIds">The locations the recovery invite covers.</param>
/// <param name="RecoverableTypeIds">The recoverable snapshot the recovery invite offers.</param>
public sealed record RecoveryInviteOutcomeDto(
    Guid RecoveryInviteId,
    IReadOnlyList<Guid> LocationIds,
    IReadOnlyList<Guid> RecoverableTypeIds);

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
/// <param name="ConfirmationRequired">Whether this call only previews the consequence.</param>
/// <param name="ActiveBookingCount">How many active bookings the attendee holds (preview only).</param>
/// <param name="CancelledBookingId">The cancelled booking identifier (confirmed call only).</param>
public sealed record CancelAttendeeBookingDto(
    bool ConfirmationRequired,
    int ActiveBookingCount,
    Guid? CancelledBookingId);

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
    public async Task<ApiOutcome<AttendeeListDto>> ListAsync(
        string? cursor,
        int? limit,
        string? status,
        Guid? attendeeGroupId,
        string? readiness,
        string? search,
        CancellationToken cancellationToken)
    {
        var parameters = new List<string>();
        if (!string.IsNullOrEmpty(cursor))
        {
            parameters.Add($"cursor={Uri.EscapeDataString(cursor)}");
        }
        if (limit is not null)
        {
            parameters.Add($"limit={limit.Value}");
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            parameters.Add($"status={Uri.EscapeDataString(status)}");
        }
        if (attendeeGroupId is not null)
        {
            parameters.Add($"attendeeGroupId={attendeeGroupId.Value:D}");
        }
        if (!string.IsNullOrWhiteSpace(readiness))
        {
            parameters.Add($"readiness={Uri.EscapeDataString(readiness)}");
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            parameters.Add($"search={Uri.EscapeDataString(search)}");
        }

        var route = parameters.Count == 0
            ? "/api/attendees"
            : $"/api/attendees?{string.Join("&", parameters)}";

        using var response = await http.GetAsync(route, cancellationToken);
        return await ApiCall.ReadAsync<AttendeeListDto>(response, cancellationToken);
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

    /// <summary>Cancels one of the attendee's active bookings, previewing before confirming.</summary>
    /// <param name="attendeeId">The attendee the booking belongs to.</param>
    /// <param name="bookingId">The booking to cancel.</param>
    /// <param name="confirm">Whether this call carries the confirmation.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The cancellation outcome, or the failure the API reported.</returns>
    public async Task<ApiOutcome<CancelAttendeeBookingDto>> CancelBookingAsync(
        Guid attendeeId,
        Guid bookingId,
        bool confirm,
        CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(
            $"/api/attendees/{attendeeId}/bookings/{bookingId}/cancel?confirm={(confirm ? "true" : "false")}",
            new { },
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
