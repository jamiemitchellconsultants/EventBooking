using System.ComponentModel;
using EventBooking.Api.Auth;
using EventBooking.Application.Access;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace EventBooking.Mcp.Tools;

/// <summary>The signed-in staff member's own access.</summary>
[McpServerToolType]
public sealed class IdentityTools
{
    /// <summary>Reads the caller's identity, roles, scope and capabilities.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The identity handler.</param>
    /// <param name="sync">The role reconciler the REST route also runs.</param>
    /// <param name="claims">The configured claim names and staff-number pattern.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The caller's access.</returns>
    [McpServerTool(
        Name = "get_my_access", Title = "Get my access",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads display name, staff number, roles, scope and granted capabilities (FR-10.8). Requires staff auth; no capability, and answers even when the staff number claim is missing or malformed.")]
    public async Task<StaffMeView> GetMyAccessAsync(
        ICallerAccessor caller,
        MeHandler handler,
        SyncStaffAccessProfileRolesHandler sync,
        IOptions<AuthClaimOptions> claims,
        CancellationToken cancellationToken)
    {
        // The REST route reconciles the token's roles before reading, so a reduced role
        // set never lingers and a first-time caller gains a profile. The tool does the
        // same, or the two surfaces would answer one caller differently.
        await sync.SyncAsync(
            caller.RequireStaffUserId(), caller.Roles, cancellationToken);
        var result = await handler.HandleAsync(
            caller.RequireStaffUserId(), caller.StaffId?.Value, caller.DisplayName,
            caller.Roles, claims.Value.StaffIdPattern, cancellationToken);
        return result.ValueOrThrow();
    }
}
