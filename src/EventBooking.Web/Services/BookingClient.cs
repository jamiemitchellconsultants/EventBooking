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

/// <summary>Attendee-facing booking confirmation. The page shows the chosen option's window.</summary>
/// <param name="BookingId">The active booking identifier.</param>
/// <param name="ManageToken">The raw management token used by the attendee page.</param>
public sealed record ConfirmedBookingDto(
    Guid BookingId,
    string ManageToken);

public sealed record BookingDto(
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Display,
    string AttendeeName);

/// <summary>Attendee-facing cancellation result: cancelled, reinvited, noEligibleEvents or reinvitePending.</summary>
/// <param name="Outcome">Which of the four outcomes happened.</param>
/// <param name="InviteId">The fresh or still-pending invite, when there is one.</param>
public sealed record CancelOutcomeDto(
    string Outcome,
    Guid? InviteId = null);

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
            new { RequestNewTime = rebook },
            cancellationToken);

        var outcome = await ApiCall.ReadAsync<CancelOutcomeDto>(response, cancellationToken);

        return outcome;
    }
}
