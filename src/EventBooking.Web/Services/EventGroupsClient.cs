using System.Net.Http.Json;

namespace EventBooking.Web.Services;

public sealed record EventGroupDto(Guid Id, string Title, string Description,
    bool IsOpen, long Version, IReadOnlyList<Guid> AttendeeGroupIds,
    IReadOnlyList<Guid> AppointmentTypeIds, IReadOnlyList<EventGroupEventDto> Events);
public sealed record EventGroupEventDto(Guid EventId, bool IsOpen);

public interface IEventGroupsClient
{
    Task<ApiOutcome<PageDto<EventGroupDto>>> ListAsync(CancellationToken ct);
    Task<ApiOutcome<EventGroupDto>> CreateAsync(string title, string description,
        IReadOnlyList<Guid> groupIds, IdempotencySubmission submission, CancellationToken ct);
    Task<ApiOutcome<EventGroupDto>> UpdateAsync(EventGroupDto value, CancellationToken ct);
    Task<ApiOutcome<EventGroupDto>> SetEventOpenAsync(Guid groupId, Guid eventId,
        bool open, long expectedVersion, CancellationToken ct);
    Task<ApiOutcome<EventGroupDto>> AddEventAsync(Guid groupId, Guid eventId,
        long expectedVersion, CancellationToken ct);
    Task<ApiOutcome<EventGroupDto>> RemoveEventAsync(Guid groupId, Guid eventId,
        long expectedVersion, CancellationToken ct);
}

public sealed class EventGroupsClient(HttpClient http) : IEventGroupsClient
{
    public Task<ApiOutcome<PageDto<EventGroupDto>>> ListAsync(CancellationToken ct) =>
        Get<PageDto<EventGroupDto>>("/api/event-groups", ct);

    public Task<ApiOutcome<EventGroupDto>> CreateAsync(string title, string description,
        IReadOnlyList<Guid> groupIds, IdempotencySubmission submission, CancellationToken ct) =>
        Send<EventGroupDto>(HttpMethod.Post, "/api/event-groups",
            new { title, description, attendeeGroupIds = groupIds }, submission, ct);

    public Task<ApiOutcome<EventGroupDto>> UpdateAsync(EventGroupDto value, CancellationToken ct) =>
        Send<EventGroupDto>(HttpMethod.Put, $"/api/event-groups/{value.Id}",
            new
            {
                value.Title, value.Description, attendeeGroupIds = value.AttendeeGroupIds,
                value.IsOpen, expectedVersion = value.Version,
            }, null, ct);

    public Task<ApiOutcome<EventGroupDto>> SetEventOpenAsync(Guid groupId, Guid eventId,
        bool open, long expectedVersion, CancellationToken ct) =>
        Send<EventGroupDto>(new HttpMethod("PATCH"),
            $"/api/event-groups/{groupId}/events/{eventId}",
            new { isOpen = open, expectedVersion }, null, ct);

    public Task<ApiOutcome<EventGroupDto>> AddEventAsync(Guid groupId, Guid eventId,
        long expectedVersion, CancellationToken ct) =>
        Send<EventGroupDto>(HttpMethod.Put,
            $"/api/event-groups/{groupId}/events/{eventId}",
            new { expectedVersion }, null, ct);

    public Task<ApiOutcome<EventGroupDto>> RemoveEventAsync(Guid groupId, Guid eventId,
        long expectedVersion, CancellationToken ct) =>
        Delete<EventGroupDto>(
            $"/api/event-groups/{groupId}/events/{eventId}?expectedVersion={expectedVersion}", ct);

    private async Task<ApiOutcome<T>> Get<T>(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct);
        return await ApiCall.ReadAsync<T>(response, ct);
    }

    private async Task<ApiOutcome<T>> Send<T>(
        HttpMethod method, string path, object body, IdempotencySubmission? submission,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        if (submission is not null) request.Headers.Add("Idempotency-Key", submission.Key);
        using var response = await http.SendAsync(request, ct);
        return await ApiCall.ReadAsync<T>(response, ct);
    }

    private async Task<ApiOutcome<T>> Delete<T>(string path, CancellationToken ct)
    {
        using var response = await http.DeleteAsync(path, ct);
        return await ApiCall.ReadAsync<T>(response, ct);
    }
}
