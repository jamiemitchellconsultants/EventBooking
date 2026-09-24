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

/// <summary>The signed-in staff identity plus self and collection entry affordances.</summary>
public sealed record MeResourceResponse(
    /// <summary>Gets the caller's enterprise staff number, or null until recorded.</summary>
    string? StaffId,
    /// <summary>Gets the caller's current role names.</summary>
    IReadOnlyList<string> Roles,
    /// <summary>Gets the caller's scoped appointment-type identifier, or null when unscoped.</summary>
    Guid? AppointmentTypeId,
    /// <summary>Gets the caller's scoped appointment-type name, or null when unscoped.</summary>
    string? AppointmentTypeName,
    /// <summary>Gets the capability names the caller's profile grants.</summary>
    IReadOnlyList<string> Capabilities,
    /// <summary>Gets the no-role explanation, or null for a full view.</summary>
    string? Problem,
    /// <summary>Gets the self and role-relevant collection entry affordances. Links are discoverability hints, not authorization.</summary>
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Builds the identity resource with self plus role-relevant entry links.</summary>
    /// <param name="staffId">The caller's enterprise staff number, or null until recorded.</param>
    /// <param name="roles">The caller's current role names.</param>
    /// <param name="appointmentTypeId">The caller's scoped appointment-type identifier, or null.</param>
    /// <param name="appointmentTypeName">The caller's scoped appointment-type name, or null.</param>
    /// <param name="capabilities">The capability names the caller's profile grants.</param>
    /// <param name="problem">The no-role explanation, or null for a full view.</param>
    /// <returns>The API resource with identity links.</returns>
    public static MeResourceResponse From(
        string? staffId,
        IReadOnlyList<string> roles,
        Guid? appointmentTypeId,
        string? appointmentTypeName,
        IReadOnlyList<string> capabilities,
        string? problem)
    {
        var links = new Dictionary<string, ApiLink>
        {
            ["self"] = new("/api/me", "GET", "getMyAccess"),
        };
        if (roles.Contains("Coordinator") || roles.Contains("Admin"))
        {
            links["attendees"] = new("/api/attendees", "GET", "listAttendees");
            links["dashboards"] = new("/api/dashboards", "GET", "getDashboards");
            links["audit"] = new("/api/audit", "GET", "searchAudit");
        }

        if (roles.Contains("Manager"))
        {
            links["eventProposals"] = new("/api/event-proposals", "GET", "listEventProposals");
            links["events"] = new("/api/events", "GET", "listEvents");
        }

        if (roles.Contains("AppointmentStaff") || roles.Contains("Manager"))
        {
            links["appointmentEvents"] = new(
                "/api/appointment-workspace/events", "GET", "listWorkspaceEvents");
        }

        if (roles.Contains("Admin"))
        {
            links["settings"] = new("/api/settings", "GET", "getSettings");
            links["staffAccess"] = new("/api/staff-access", "GET", "listStaffAccess");
        }

        return new MeResourceResponse(
            staffId, roles, appointmentTypeId, appointmentTypeName, capabilities, problem, links);
    }
}
