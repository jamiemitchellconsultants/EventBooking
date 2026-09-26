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
            CallerCapabilities capabilities,
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
            string? appointmentTypeCode = null;
            string? appointmentTypeName = null;
            if (view.ScopeAppointmentTypeId is not null)
            {
                var type = await appointmentTypes.GetAsync(
                    view.ScopeAppointmentTypeId.Value, cancellationToken);
                appointmentTypeCode = type?.Code;
                appointmentTypeName = type?.Name;
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(CurrentStaffResponse.From(
                view.DisplayName,
                view.StaffId,
                view.Roles,
                view.ScopeAppointmentTypeId,
                appointmentTypeCode,
                appointmentTypeName,
                view.Capabilities,
                view.Problem,
                held));
        }).AllowAnonymous()
            .WithAgentMetadata("getMyAccess")
            .Produces<CurrentStaffResponse>(200);

        return app;
    }
}

/// <summary>The signed-in staff identity plus self and collection entry affordances.</summary>
public sealed record CurrentStaffResponse(
    /// <summary>Gets the human-readable name from the token, or null when it carries none.</summary>
    string? DisplayName,
    /// <summary>Gets the caller's enterprise staff number, or null until recorded.</summary>
    string? StaffId,
    /// <summary>Gets the caller's current role names.</summary>
    IReadOnlyList<string> Roles,
    /// <summary>Gets the caller's scoped appointment-type identifier, or null when unscoped.</summary>
    Guid? ScopeAppointmentTypeId,
    /// <summary>Gets the caller's scoped appointment-type code, or null when unscoped.</summary>
    string? ScopeAppointmentTypeCode,
    /// <summary>Gets the caller's scoped appointment-type name, or null when unscoped.</summary>
    string? ScopeAppointmentTypeName,
    /// <summary>Gets the capability names the caller's profile grants.</summary>
    IReadOnlyList<string> Capabilities,
    /// <summary>Gets the no-role explanation, or null for a full view.</summary>
    string? Problem,
    /// <summary>Gets the self, role-relevant collection entry, and capability-gated collection mutation affordances. Links are discoverability hints, not authorization.</summary>
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Builds the identity resource with self plus role-relevant entry links.</summary>
    /// <param name="displayName">The human-readable name from the token, or null.</param>
    /// <param name="staffId">The caller's enterprise staff number, or null until recorded.</param>
    /// <param name="roles">The caller's current role names.</param>
    /// <param name="scopeAppointmentTypeId">The caller's scoped appointment-type identifier, or null.</param>
    /// <param name="scopeAppointmentTypeCode">The caller's scoped appointment-type code, or null.</param>
    /// <param name="scopeAppointmentTypeName">The caller's scoped appointment-type name, or null.</param>
    /// <param name="capabilities">The capability names the caller's profile grants.</param>
    /// <param name="problem">The no-role explanation, or null for a full view.</param>
    /// <param name="held">The capability names the caller's profile grants now.</param>
    /// <returns>The API resource with identity links.</returns>
    public static CurrentStaffResponse From(
        string? displayName,
        string? staffId,
        IReadOnlyList<string> roles,
        Guid? scopeAppointmentTypeId,
        string? scopeAppointmentTypeCode,
        string? scopeAppointmentTypeName,
        IReadOnlyList<string> capabilities,
        string? problem,
        IReadOnlySet<string> held)
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

        // Mutation affordances live here — not on rows — so an empty collection page still
        // offers create without testing a role. Each is gated by the capability its route
        // demands, exactly as the row links are.
        var mutations = CallerLinks.For(
            held,
            new LinkCandidate(
                "createLocation", "createLocation", "/api/locations",
                nameof(StaffCapability.ManageReferenceData)),
            new LinkCandidate(
                "createAppointmentType", "createAppointmentType", "/api/appointment-types",
                nameof(StaffCapability.ManageReferenceData)),
            new LinkCandidate(
                "createAttendeeGroup", "createAttendeeGroup", "/api/attendee-groups",
                nameof(StaffCapability.ManageReferenceData)),
            new LinkCandidate(
                "createEventGroup", "createEventGroup", "/api/event-groups",
                nameof(StaffCapability.ManageEventGroups)),
            new LinkCandidate(
                "proposeEvent", "proposeEvent", "/api/event-proposals",
                nameof(StaffCapability.ManageEventNegotiation)),
            new LinkCandidate(
                "createAttendee", "createAttendee", "/api/attendees",
                nameof(StaffCapability.ManageAttendees)),
            new LinkCandidate(
                "importAttendees", "importAttendees", "/api/attendees/import",
                nameof(StaffCapability.ManageAttendees)));
        foreach (var (relation, link) in mutations)
        {
            links[relation] = link;
        }

        return new CurrentStaffResponse(
            displayName, staffId, roles, scopeAppointmentTypeId, scopeAppointmentTypeCode,
            scopeAppointmentTypeName, capabilities, problem, links);
    }
}
