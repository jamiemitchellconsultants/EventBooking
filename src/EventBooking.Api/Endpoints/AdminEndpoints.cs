using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Settings;
using EventBooking.Application.Events;
using EventBooking.Domain.Access;

namespace EventBooking.Api.Endpoints;

public static class AdminEndpoints
{
    public sealed record UpdateSettingsRequest(
        int InviteExpiryDays,
        int MaxAutoRetryCount,
        int InviteOptionCount,
        long ExpectedVersion);

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("/settings", async (
            ICallerAccessor caller,
            AdminSettingsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.GetAsync(caller.RequireStaffUserId(), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(SettingsResourceResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("getSettings")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapPut("/settings", async (
            UpdateSettingsRequest request,
            ICallerAccessor caller,
            AdminSettingsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.SaveAsync(
                new SaveSystemSettingsCommand(
                    caller.RequireStaffUserId(),
                    request.InviteExpiryDays,
                    request.MaxAutoRetryCount,
                    request.InviteOptionCount,
                    request.ExpectedVersion),
                cancellationToken);

            // The versioned result is projected, not swallowed: the settings page needs the new
            // version to send with its next save, and a caller that never sees it can only ever
            // collide on the second one.
            return result.IsSuccess
                ? Results.Ok(new
                {
                    inviteExpiryDays = result.Value.InviteExpiryDays,
                    maxAutoRetryCount = result.Value.MaxAutoRetryCount,
                    inviteOptionCount = result.Value.InviteOptionCount,
                    version = result.Value.Version,
                })
                : result.ToResponse();
        })
            .WithAgentMetadata("updateSettings")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(409);

        return app;
    }
}
