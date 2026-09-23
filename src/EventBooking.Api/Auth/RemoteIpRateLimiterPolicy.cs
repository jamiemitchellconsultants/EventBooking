using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace EventBooking.Api.Auth;

/// <summary>Applies the anonymous candidate-link allowance independently per client address.</summary>
public sealed class RemoteIpRateLimiterPolicy : IRateLimiterPolicy<string>
{
    /// <inheritdoc/>
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

    /// <inheritdoc/>
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        // ForwardedHeadersMiddleware runs before the limiter, so RemoteIpAddress is already the
        // real client address when behind a proxy. Unknown addresses share one fallback bucket
        // rather than bypassing the limit.
        var client = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(client, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    }
}
