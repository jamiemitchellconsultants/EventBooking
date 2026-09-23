using EventBooking.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace EventBooking.Api.Auth;

/// <summary>
/// Records each valid authenticated provider/staff-number pair before staff authorization runs.
/// A bounded process cache avoids placing a database write in front of every request.
/// </summary>
public sealed class StaffIdentityRecorder(RequestDelegate next)
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(15);

    /// <summary>Records a complete identity pair when needed, then continues the request pipeline.</summary>
    /// <param name="context">The current request.</param>
    /// <param name="caller">The validated caller claims.</param>
    /// <param name="identities">The durable identity mirror.</param>
    /// <param name="clock">The application's source of the current instant.</param>
    /// <param name="cache">The process-local record-suppression cache.</param>
    /// <param name="logger">Records identity-provider uniqueness conflicts for operators.</param>
    public async Task InvokeAsync(
        HttpContext context,
        ICallerAccessor caller,
        IStaffIdentityRepository identities,
        IClock clock,
        IMemoryCache cache,
        ILogger<StaffIdentityRecorder> logger)
    {
        var staffUserId = caller.StaffUserId;
        var staffId = caller.StaffId;
        if (staffUserId is not null
            && staffId is not null
            && !cache.TryGetValue(CacheKey(staffUserId.Value), out _))
        {
            try
            {
                await identities.UpsertAsync(
                    staffUserId.Value,
                    staffId,
                    caller.DisplayName,
                    clock.UtcNow,
                    context.RequestAborted);
                cache.Set(CacheKey(staffUserId.Value), true, CacheLifetime);
            }
            catch (PostgresException exception)
                when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                logger.LogWarning(
                    exception,
                    "Staff identity conflict for provider user {StaffUserId} and staff number {StaffId}",
                    staffUserId,
                    staffId.Value);
            }
        }

        await next(context);
    }

    private static string CacheKey(Guid staffUserId) => $"staff-identity:{staffUserId:D}";
}
