namespace EventBooking.Web.Services;

public sealed class MeClient(HttpClient http) : IMeClient
{
    public async Task<ApiOutcome<MeDto>> GetAsync(CancellationToken ct) =>
        await ApiCall.ReadAsync<MeDto>(await http.GetAsync("/api/me", ct), ct);
}
public interface IMeClient { Task<ApiOutcome<MeDto>> GetAsync(CancellationToken ct); }
public sealed record IdempotencySubmission(string Key, bool IsRetry)
{
    public static IdempotencySubmission Start() => new(Guid.NewGuid().ToString("N"), false);
    public IdempotencySubmission AsRetry() => this with { IsRetry = true };
}
