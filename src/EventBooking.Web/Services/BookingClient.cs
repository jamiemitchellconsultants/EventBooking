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
