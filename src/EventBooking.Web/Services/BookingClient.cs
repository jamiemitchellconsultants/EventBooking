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
