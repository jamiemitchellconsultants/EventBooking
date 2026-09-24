namespace EventBooking.Application.Abstractions;

/// <summary>One retained response, replayed for a repeated key.</summary>
/// <param name="RequestHash">The hash of the body the key was first used with.</param>
/// <param name="StatusCode">The status the first call returned.</param>
/// <param name="Body">The body the first call returned.</param>
public sealed record IdempotentResponse(string RequestHash, int StatusCode, string Body);

/// <summary>Retains create-endpoint responses against their Idempotency-Key for 24 hours.</summary>
public interface IIdempotencyStore
{
    /// <summary>The retention window design 05 gives the header.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    /// <summary>Reads a retained response, ignoring rows past the retention window.</summary>
    /// <param name="staffUserId">The calling staff identity.</param>
    /// <param name="route">The route pattern the key was used on.</param>
    /// <param name="key">The caller's key.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The retained response, or null.</returns>
    Task<IdempotentResponse?> TryGetAsync(
        Guid staffUserId, string route, string key, DateTimeOffset now, CancellationToken ct);

/// <summary>Retains one response after replacing an expired row for the same key.</summary>
    /// <param name="staffUserId">The calling staff identity.</param>
    /// <param name="route">The route pattern the key was used on.</param>
    /// <param name="key">The caller's key.</param>
    /// <param name="requestHash">The hash of the request body.</param>
    /// <param name="statusCode">The status returned.</param>
    /// <param name="body">The body returned.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task tracking the write.</returns>
    Task SaveAsync(
        Guid staffUserId, string route, string key, string requestHash,
        int statusCode, string body, DateTimeOffset now, CancellationToken ct);
}
