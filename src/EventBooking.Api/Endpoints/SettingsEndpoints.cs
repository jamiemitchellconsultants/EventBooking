using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Settings;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps the two settings routes.</summary>
public static class SettingsEndpoints
{
    /// <summary>The settings body design 05 names.</summary>
    /// <param name="InviteExpiryDays">Days an invitation stays usable.</param>
    /// <param name="MaxAutoRetryCount">Automatic re-issues before giving up.</param>
    /// <param name="InviteOptionCount">Options offered per invitation.</param>
    /// <param name="ExpectedVersion">The version the caller read.</param>
    public sealed record UpdateSettingsRequest(
        int InviteExpiryDays, int MaxAutoRetryCount, int InviteOptionCount, long ExpectedVersion);

    /// <summary>Maps the settings routes.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/settings")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
            .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName);

        group.MapGet("/", async (
            ICallerAccessor caller,
            AdminSettingsHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.GetAsync(caller.RequireStaffUserId(), cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(ApiResponses.Settings(result.Value, held));
        })
            .WithAgentMetadata("getSettings")
            .Produces<SettingsResponse>(200)
            .ProducesProblem(403);

        group.MapPut("/", async (
            UpdateSettingsRequest request,
            ICallerAccessor caller,
            AdminSettingsHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.SaveAsync(
                new SaveSystemSettingsCommand(
                    caller.RequireStaffUserId(), request.InviteExpiryDays,
                    request.MaxAutoRetryCount, request.InviteOptionCount,
                    request.ExpectedVersion),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(ApiResponses.Settings(result.Value, held));
        })
            .WithAgentMetadata("updateSettings")
            .Produces<SettingsResponse>(200)
            .ProducesProblem(403)
            .ProducesProblem(409)
            .ProducesProblem(422);

        return app;
    }
}
