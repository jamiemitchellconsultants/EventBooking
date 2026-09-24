using System.Net.Http.Json;

namespace EventBooking.Web.Services;

public sealed record InviteOptionDto(
    Guid EventId, string LocationName, string Address, EventTimeDto Time);
public sealed record InviteDto(
    Guid InviteId, string AttendeeName, IReadOnlyList<string> AppointmentTypeNames,
    IReadOnlyList<InviteOptionDto> Options, bool IsRecovery,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record ConfirmBookingOutcomeDto(Guid BookingId, string ManageToken);
public sealed record ManagedBookingDto(
    string AttendeeName, string LocationName, string Address, EventTimeDto Time,
    IReadOnlyList<string> AppointmentTypeNames,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record CancelBookingOutcomeDto(string Outcome, Guid? InviteId);

/// <summary>Deployment strings the attendee pages need. Bound from the app's own settings file.</summary>
/// <param name="CoordinatorContact">The recruitment contact attendees are told to reach when a link fails.</param>
public sealed record AttendeePageOptions(string CoordinatorContact);

public interface IBookingClient
{
    Task<ApiOutcome<InviteDto>> ViewInviteAsync(string token, CancellationToken ct);
    Task<ApiOutcome<ConfirmBookingOutcomeDto>> ConfirmAsync(
        string token, Guid eventId, IdempotencySubmission submission, CancellationToken ct);
    Task<ApiOutcome<ManagedBookingDto>> ViewManagedAsync(string token, CancellationToken ct);
    Task<ApiOutcome<CancelBookingOutcomeDto>> CancelAsync(
        string token, bool requestNewTime, IdempotencySubmission submission, CancellationToken ct);
}

/// <summary>
/// Talks to the anonymous attendee routes. This client is deliberately constructed from the plain
/// named HTTP client: attendees authorise with their URL token, never an access token.
/// </summary>
public sealed class BookingClient(HttpClient http) : IBookingClient
{
    public const string ClientName = "EventBooking.Anonymous";

    public async Task<ApiOutcome<InviteDto>> ViewInviteAsync(string token, CancellationToken ct)
    {
        using var response = await http.GetAsync($"/api/booking/{Uri.EscapeDataString(token)}", ct);
        return await ApiCall.ReadAsync<InviteDto>(response, ct);
    }

    public async Task<ApiOutcome<ConfirmBookingOutcomeDto>> ConfirmAsync(
        string token, Guid eventId, IdempotencySubmission submission, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/booking/{Uri.EscapeDataString(token)}/confirm")
        {
            Content = JsonContent.Create(new { eventId }),
        };
        request.Headers.Add("Idempotency-Key", submission.Key);
        using var response = await http.SendAsync(request, ct);
        return await ApiCall.ReadAsync<ConfirmBookingOutcomeDto>(response, ct);
    }

    public async Task<ApiOutcome<ManagedBookingDto>> ViewManagedAsync(string token, CancellationToken ct)
    {
        using var response = await http.GetAsync($"/api/manage/{Uri.EscapeDataString(token)}", ct);
        return await ApiCall.ReadAsync<ManagedBookingDto>(response, ct);
    }

    public async Task<ApiOutcome<CancelBookingOutcomeDto>> CancelAsync(
        string token, bool requestNewTime, IdempotencySubmission submission, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/manage/{Uri.EscapeDataString(token)}/cancel")
        {
            Content = JsonContent.Create(new { requestNewTime }),
        };
        request.Headers.Add("Idempotency-Key", submission.Key);
        using var response = await http.SendAsync(request, ct);
        return await ApiCall.ReadAsync<CancelBookingOutcomeDto>(response, ct);
    }
}
