using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using Microsoft.Extensions.Options;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps the caller self-description route.</summary>
public static class MeEndpoints
{
    /// <summary>Adds the caller route to the application. Anonymous callers land in the
    /// no-role view instead of a 401, so the response explains the problem.</summary>
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", async (
            ICallerAccessor caller,
            MeHandler handler,
            SyncStaffAccessProfileRolesHandler sync,
            IAppointmentTypeRepository appointmentTypes,
            IOptions<AuthClaimOptions> claims,
            CancellationToken cancellationToken) =>
        {
            // /api/me carries no staff requirement, so it reconciles the token's roles
            // itself: without this sync a reduced role set would linger for callers that
            // only ever call /api/me, and a first-time caller would never gain a profile.
            if (caller.StaffUserId is { } staffUserId)
            {
                await sync.SyncAsync(staffUserId, caller.Roles, cancellationToken);
            }

            var me = await handler.HandleAsync(
                caller.StaffUserId ?? Guid.Empty,
                caller.StaffId?.Value,
                caller.DisplayName,
                caller.Roles,
                claims.Value.StaffIdPattern,
                cancellationToken);
            var view = me.Value;
            string? appointmentTypeName = null;
            if (view.ScopeAppointmentTypeId is not null)
            {
                var type = await appointmentTypes.GetAsync(
                    view.ScopeAppointmentTypeId.Value, cancellationToken);
                appointmentTypeName = type?.Name;
            }

            return Results.Ok(MeResourceResponse.From(
                view.StaffId,
                view.Roles,
                view.ScopeAppointmentTypeId,
                appointmentTypeName,
                view.Capabilities,
                view.Problem));
        }).AllowAnonymous()
            .WithAgentMetadata("getMyAccess")
            .Produces(200);

        return app;
    }
}
