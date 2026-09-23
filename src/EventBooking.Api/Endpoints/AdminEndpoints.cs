using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Settings;
using EventBooking.Application.Slots;
using EventBooking.Domain.Access;

namespace EventBooking.Api.Endpoints;

public static class AdminEndpoints
{
    public sealed record UpdateSettingsRequest(int InviteExpiryDays, int MaxAutoRetryCount);

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
            (await handler.UpdateAsync(
                new UpdateSettingsCommand(
                    caller.RequireStaffUserId(), request.InviteExpiryDays, request.MaxAutoRetryCount),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("updateSettings")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(409);

        return app;
    }
}
