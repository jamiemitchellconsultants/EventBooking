using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace EventBooking.Web.Services;

public sealed record PublicAttendeeGroupDto(
    [property: JsonPropertyName("attendeeGroupId")] Guid Id,
    string Name, string Description);
public sealed record PublicEventDto(Guid Id, string LocationName, string Address,
    DateOnly Date, TimeOnly StartTime, int DurationMinutes,
    IReadOnlyList<string> AppointmentTypeNames,
    IReadOnlyList<Guid> AvailableAttendeeGroupIds);
public sealed record PublicEventGroupDto(Guid Id, string Title, string Description,
    IReadOnlyList<PublicAttendeeGroupDto> AttendeeGroups,
    IReadOnlyList<PublicEventDto> Events);
public sealed record SelfRegistrationSummaryDto(Guid EventGroupId, Guid EventId,
    string EventGroupTitle, string LocationName, DateOnly Date, TimeOnly StartTime,
    string AttendeeGroupName);
public sealed record ConfirmSelfRegistrationOutcomeDto(Guid BookingId);
public interface IPublicEventGroupsClient
{
    Task<ApiOutcome<PublicEventGroupDto>> GetGroupAsync(Guid id, CancellationToken ct);
    Task<ApiOutcome<PublicEventDto>> GetEventAsync(Guid groupId, Guid eventId, CancellationToken ct);
    Task<ApiOutcome<bool>> RequestAsync(Guid groupId, Guid eventId, string name,
        string email, Guid attendeeGroupId, CancellationToken ct);
    Task<ApiOutcome<SelfRegistrationSummaryDto>> ViewConfirmationAsync(string token, CancellationToken ct);
    Task<ApiOutcome<ConfirmSelfRegistrationOutcomeDto>> ConfirmAsync(
        string token, IdempotencySubmission submission, CancellationToken ct);
}

/// <summary>
/// Talks to the anonymous public event-group routes. Built from the plain named HTTP client:
/// registrants authorise with nothing, never a staff access token.
/// </summary>
public sealed class PublicEventGroupsClient(HttpClient http) : IPublicEventGroupsClient
{
    public const string ClientName = "EventBooking.Public";

    public async Task<ApiOutcome<PublicEventGroupDto>> GetGroupAsync(Guid id, CancellationToken ct)
    {
        using var response = await http.GetAsync($"/api/public/event-groups/{id:D}", ct);
        var outcome = await ApiCall.ReadAsync<WireGroup>(response, ct);
        return outcome.IsSuccess && outcome.Value is not null
            ? ApiOutcome<PublicEventGroupDto>.Success(outcome.Value.Map(), outcome.StatusCode)
            : ApiOutcome<PublicEventGroupDto>.Failure(outcome.Problem!);
    }

    public async Task<ApiOutcome<PublicEventDto>> GetEventAsync(
        Guid groupId, Guid eventId, CancellationToken ct)
    {
        var group = await GetGroupAsync(groupId, ct);
        if (!group.IsSuccess || group.Value is null)
            return group.IsSuccess
                ? ApiOutcome<PublicEventDto>.Failure("Something went wrong. Please try again.")
                : ApiOutcome<PublicEventDto>.Failure(group.Problem!);
        var match = group.Value.Events.SingleOrDefault(x => x.Id == eventId);
        return match is null
            ? ApiOutcome<PublicEventDto>.Failure(new ApiProblem(
                "not_found", "Not found", 404, "No such event in this group.",
                [], null, null, null, null))
            : ApiOutcome<PublicEventDto>.Success(match, group.StatusCode);
    }

    public async Task<ApiOutcome<bool>> RequestAsync(Guid groupId, Guid eventId, string name,
        string email, Guid attendeeGroupId, CancellationToken ct)
    {
        using var response = await http.PostAsync(
            $"/api/public/event-groups/{groupId:D}/events/{eventId:D}/registrations",
            JsonContent.Create(new { name, email, attendeeGroupId }), ct);
        return await ApiCall.ReadNoContentAsync(response, ct);
    }

    public async Task<ApiOutcome<SelfRegistrationSummaryDto>> ViewConfirmationAsync(
        string token, CancellationToken ct)
    {
        using var response = await http.GetAsync(
            $"/api/public/event-groups/confirm/{Uri.EscapeDataString(token)}", ct);
        return await ApiCall.ReadAsync<SelfRegistrationSummaryDto>(response, ct);
    }

    public async Task<ApiOutcome<ConfirmSelfRegistrationOutcomeDto>> ConfirmAsync(
        string token, IdempotencySubmission submission, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/public/event-groups/confirm/{Uri.EscapeDataString(token)}");
        request.Headers.Add("Idempotency-Key", submission.Key);
        using var response = await http.SendAsync(request, ct);
        return await ApiCall.ReadAsync<ConfirmSelfRegistrationOutcomeDto>(response, ct);
    }

    private sealed record WireTime(
        DateOnly Date, TimeOnly StartTime, int DurationMinutes);
    private sealed record WireEvent(
        [property: JsonPropertyName("eventId")] Guid Id,
        string LocationName, string Address, WireTime EventTime,
        IReadOnlyList<string> AppointmentTypeCodes,
        IReadOnlyList<Guid>? AvailableAttendeeGroupIds);
    private sealed record WireGroup(
        Guid Id, string Title, string Description,
        IReadOnlyList<PublicAttendeeGroupDto> AttendeeGroups,
        IReadOnlyList<WireEvent> Events)
    {
        public PublicEventGroupDto Map() => new(
            Id, Title, Description,
            AttendeeGroups,
            [.. Events.Select(x => new PublicEventDto(
                x.Id, x.LocationName, x.Address,
                x.EventTime.Date, x.EventTime.StartTime, x.EventTime.DurationMinutes,
                x.AppointmentTypeCodes,
                x.AvailableAttendeeGroupIds ?? []))]);
    }
}
