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
