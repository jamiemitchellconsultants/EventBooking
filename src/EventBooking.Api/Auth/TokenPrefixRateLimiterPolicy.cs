using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace EventBooking.Api.Auth;

/// <summary>
/// Applies the per-link allowance, partitioned on a prefix of the attendee token rather than
/// the whole token: a prefix is enough to separate one link from another, and it keeps the
/// full token out of the limiter's key space, which is memory that outlives the request.
/// </summary>
/// <param name="settings">The configured allowances.</param>
public sealed class TokenPrefixRateLimiterPolicy(IOptions<RateLimitSettings> settings)
    : IRateLimiterPolicy<string>
{
    /// <summary>The policy name the attendee routes are decorated with.</summary>
    public const string PolicyName = "attendee-token";

    /// <summary>The number of leading token characters the partition key uses.</summary>
    public const int PrefixLength = 12;

    /// <inheritdoc/>
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected =>
        (context, ct) => RateLimitRejection.WriteAsync(context, PolicyName, ct);

    /// <inheritdoc/>
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var token = httpContext.Request.RouteValues.TryGetValue("token", out var value)
            ? value?.ToString()
            : null;
        var prefix = string.IsNullOrEmpty(token)
            ? "none"
            : token[..Math.Min(PrefixLength, token.Length)];

        return RateLimitPartition.GetSlidingWindowLimiter(
            prefix,
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = settings.Value.TokenPerMinute,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
            });
    }
}
