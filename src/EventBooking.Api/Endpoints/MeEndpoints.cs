using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Access;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps the authenticated caller self-description route.</summary>
public static class MeEndpoints
{
    /// <summary>Adds the authenticated-only caller route to the application.</summary>
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", async (
            ICallerAccessor caller,
            MeHandler handler,
            CancellationToken cancellationToken) =>
        {
            var me = await handler.GetAsync(
                caller.RequireStaffUserId(), caller.StaffId, caller.Roles, cancellationToken);
            return Results.Ok(MeResourceResponse.From(
                me.StaffId?.Value,
                me.Roles.Select(role => role.ToString()).ToList(),
                me.AppointmentTypeId,
                me.AppointmentTypeName));
        }).RequireAuthorization(AuthenticationExtensions.AuthenticatedPolicy)
            .WithAgentMetadata("getMyAccess")
            .Produces(200)
            .ProducesProblem(403);

        return app;
    }
}
