namespace EventBooking.Web.Services;

// Scoped, which in WebAssembly means one instance for the signed-in session: the layout, the
// pages and the clients that need the caller's links share one successful /api/me read instead
// of each re-fetching (and re-running the server's role sync). A failure is not remembered, so
// the next caller retries.
public sealed class MeClient(HttpClient http) : IMeClient
{
    private ApiOutcome<MeDto>? _success;

    public async Task<ApiOutcome<MeDto>> GetAsync(CancellationToken ct)
    {
        if (_success is not null) return _success;
        var outcome = await ApiCall.ReadAsync<MeDto>(await http.GetAsync("/api/me", ct), ct);
        if (outcome.IsSuccess) _success = outcome;
        return outcome;
    }
}
public interface IMeClient { Task<ApiOutcome<MeDto>> GetAsync(CancellationToken ct); }
public sealed record IdempotencySubmission(string Key, bool IsRetry)
{
    public static IdempotencySubmission Start() => new(Guid.NewGuid().ToString("N"), false);
    public IdempotencySubmission AsRetry() => this with { IsRetry = true };
}

// One user submission's Idempotency-Key. Sending the same payload again is a retry and keeps
// the key, so a request whose response was lost is replayed rather than repeated; a different
// payload is an intentional edit and starts a new key, as does completing the submission.
public sealed class PendingSubmission
{
    private IdempotencySubmission? _current;
    private object? _payload;

    public IdempotencySubmission For(object payload)
    {
        _current = _current is not null && Equals(_payload, payload)
            ? _current.AsRetry()
            : IdempotencySubmission.Start();
        _payload = payload;
        return _current;
    }

    public void Complete()
    {
        _current = null;
        _payload = null;
    }
}
