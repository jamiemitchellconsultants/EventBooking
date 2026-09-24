using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace EventBooking.Api.Auth;

/// <summary>
/// Defence in depth on the staff surface: 300 requests a minute per identity. Partitioned on
/// the provider key rather than the address, because staff share office addresses and an
/// address-shaped limit would punish a whole floor for one script.
/// </summary>
/// <param name="settings">The configured allowances.</param>
public sealed class StaffRateLimiterPolicy(IOptions<RateLimitSettings> settings)
    : IRateLimiterPolicy<string>
{
    /// <summary>The policy name the staff route groups are decorated with.</summary>
    public const string PolicyName = "staff";

    /// <inheritdoc/>
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected =>
        (context, ct) => RateLimitRejection.WriteAsync(context, PolicyName, ct);

    /// <inheritdoc/>
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var staffUserId = HttpContextCallerAccessor.StaffUserIdOf(httpContext.User);
        return staffUserId is null
            ? RateLimitPartition.GetNoLimiter("anonymous")
            : RateLimitPartition.GetSlidingWindowLimiter(
                staffUserId.Value.ToString(),
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = settings.Value.StaffPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0,
                });
    }
}
