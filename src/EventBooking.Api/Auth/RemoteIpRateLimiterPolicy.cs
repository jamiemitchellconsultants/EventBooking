using System.Threading.RateLimiting;
using EventBooking.Api.Endpoints;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace EventBooking.Api.Auth;

/// <summary>
/// The configured per-minute allowances (design 06, design 08). A settable class rather than
/// a record because the limiter policies read it through the options pattern, which binds by
/// property.
/// </summary>
public sealed class RateLimitSettings
{
    /// <summary>Gets or sets the attendee requests allowed per client address.</summary>
    public int AttendeePerMinute { get; set; } = 30;

    /// <summary>Gets or sets the attendee requests allowed per token prefix.</summary>
    public int TokenPerMinute { get; set; } = 10;

    /// <summary>Gets or sets the staff requests allowed per staff identity.</summary>
    public int StaffPerMinute { get; set; } = 300;
}

/// <summary>Applies the anonymous attendee-link allowance independently per client address.</summary>
/// <param name="settings">The configured allowances.</param>
public sealed class RemoteIpRateLimiterPolicy(IOptions<RateLimitSettings> settings)
    : IRateLimiterPolicy<string>
{
    /// <summary>The policy name the attendee routes are decorated with.</summary>
    public const string PolicyName = "attendee-address";

    /// <inheritdoc/>
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected =>
        (context, ct) => RateLimitRejection.WriteAsync(context, PolicyName, ct);

    /// <inheritdoc/>
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // ForwardedHeadersMiddleware runs before the limiter and rewrites RemoteIpAddress only
        // for a request arriving from a known proxy network, so this is the real client
        // address in production and the socket address otherwise. Unknown addresses share one
        // fallback bucket rather than bypassing the limit.
        var client = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter(
            client,
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = settings.Value.AttendeePerMinute,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
            });
    }
}

/// <summary>Applies the address limiter globally only to anonymous attendee-token routes.</summary>
public static class AttendeeRouteRateLimiter
{
    public static RateLimitPartition<string> Partition(HttpContext context, RateLimitSettings settings)
    {
        var path = context.Request.Path;
        if (!path.StartsWithSegments("/api/booking") && !path.StartsWithSegments("/api/manage"))
        {
            return RateLimitPartition.GetNoLimiter("not-an-attendee-route");
        }

        var client = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter(client, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = settings.AttendeePerMinute,
            Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0,
        });
    }
}

/// <summary>
/// The one rejection writer. A 429 is a catalogued failure like any other, so it carries the
/// same problem body, and Retry-After comes from the lease when the limiter supplies it.
/// </summary>
internal static class RateLimitRejection
{
    public static async ValueTask WriteAsync(
        OnRejectedContext context, string policy, CancellationToken ct)
    {
        var shape = ProblemCatalogue.For(ProblemCatalogue.RateLimitedCode);
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var value)
            ? value
            : TimeSpan.FromMinutes(1);

        context.HttpContext.Response.StatusCode = shape.Status;
        context.HttpContext.Response.Headers.RetryAfter =
            ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(
                System.Globalization.CultureInfo.InvariantCulture);

        context.HttpContext.RequestServices
            .GetRequiredService<Observability.EventBookingMetrics>()
            .RateLimited.Add(1, new KeyValuePair<string, object?>("policy", policy));

        await context.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                type = shape.Type,
                title = shape.Title,
                status = shape.Status,
                detail = "Too many requests. Please wait and try again.",
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken: ct);
    }
}
