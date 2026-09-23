using System.ComponentModel;
using EventBooking.Api.Auth;
using EventBooking.Application.Access;
using EventBooking.Application.Settings;
using EventBooking.Domain.Access;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace EventBooking.Mcp.Tools;

/// <summary>System settings, staff access, and caller identity for admins.</summary>
[McpServerToolType]
public sealed class AdminTools
{
    /// <summary>Reads the current system settings.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The settings handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The system settings.</returns>
    [McpServerTool(Name = "get_settings", Title = "Get settings", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Read the admin-configurable system settings. Caller must be an admin.")]
    public async Task<SettingsView> GetSettingsAsync(
        ICallerAccessor caller,
        AdminSettingsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.GetAsync(caller.RequireStaffUserId(), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Updates the system settings.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The settings handler.</param>
    /// <param name="inviteExpiryDays">The invite expiry window in days.</param>
    /// <param name="maxAutoRetryCount">The maximum number of times an unanswered invite is automatically re-issued.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "update_settings", Title = "Update settings", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Update the invite expiry window, the re-issue limit and the number of options per invite. Caller must be an admin; overwrites all three.")]
    public async Task<SystemSettingsResult> UpdateSettingsAsync(
        ICallerAccessor caller,
        AdminSettingsHandler handler,
        [Description("Invite expiry window in days.")] int inviteExpiryDays,
        [Description("Maximum number of times an unanswered invite is automatically re-issued.")] int maxAutoRetryCount,
        [Description("How many event options each invite offers, 1 to 5.")] int inviteOptionCount,
        [Description("The version last read, for optimistic concurrency.")] long expectedVersion,
        CancellationToken cancellationToken)
    {
        var result = await handler.SaveAsync(
            new SaveSystemSettingsCommand(
                caller.RequireStaffUserId(), inviteExpiryDays, maxAutoRetryCount,
                inviteOptionCount, expectedVersion),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Lists every staff access profile.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The staff access handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The staff access profiles with scalar staff numbers.</returns>
    [McpServerTool(Name = "list_staff_access", Title = "List staff access", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List every staff access profile with staff number, roles, scope, provider key, and version. Caller must be an admin.")]
    public async Task<IReadOnlyList<StaffAccessToolProfile>> ListStaffAccessAsync(
        ICallerAccessor caller,
        StaffAccessHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ListAsync(caller.RequireStaffUserId(), cancellationToken);
        return result.ValueOrThrow().Select(profile => profile.ToToolView()).ToList();
    }

    /// <summary>Replaces one existing staff profile's appointment-type scope.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The staff access handler.</param>
    /// <param name="staffId">The target staff number, or null for historic fallback.</param>
    /// <param name="historicStaffUserId">A historic provider key whose listed staff number is null.</param>
    /// <param name="appointmentTypeId">The appointment-type scope to set, or null to clear it.</param>
    /// <param name="expectedVersion">The version last observed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The mutation view.</returns>
    [McpServerTool(Name = "replace_staff_access_scope", Title = "Replace staff access scope", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Atomically replace one existing staff member's appointment-type scope. Caller must be an admin; overwrites the scope. Select by staffId; use historicStaffUserId only for a listed profile whose staffId is null.")]
    public async Task<StaffAccessToolMutation> ReplaceStaffAccessScopeAsync(
        ICallerAccessor caller,
        StaffAccessHandler handler,
        [Description("The target staff number, or null when using historicStaffUserId.")] string? staffId,
        [Description("A listed provider key whose staffId is null, or null when using staffId.")] Guid? historicStaffUserId,
        [Description("The appointment-type scope to set, or null to clear it. This is a real write, not a no-op check.")] Guid? appointmentTypeId,
        [Description("Version last observed.")] long expectedVersion,
        CancellationToken cancellationToken)
    {
        var actorStaffUserId = caller.RequireStaffUserId();
        var target = await ResolveTargetAsync(
            actorStaffUserId, handler, staffId, historicStaffUserId, cancellationToken);
        var result = await handler.ReplaceScopeAsync(
            new ReplaceStaffAccessProfileScopeCommand(
                actorStaffUserId, target.StaffUserId, appointmentTypeId, expectedVersion),
            cancellationToken);
        var mutation = result.ValueOrThrow();
        return new StaffAccessToolMutation
        {
            Profile = mutation.Profile.ToToolView(target.StaffId),
            FormerManagerStaffUserId = mutation.FormerManagerStaffUserId,
        };
    }

    /// <summary>Clears one existing staff profile's appointment-type scope.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The staff access handler.</param>
    /// <param name="staffId">The target staff number, or null for historic fallback.</param>
    /// <param name="historicStaffUserId">A historic provider key whose listed staff number is null.</param>
    /// <param name="expectedVersion">The version last observed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "clear_staff_access_scope", Title = "Clear staff access scope", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Clear one staff member's appointment-type scope. Select by staffId; use historicStaffUserId only for a listed profile whose staffId is null.")]
    public async Task<string> ClearStaffAccessScopeAsync(
        ICallerAccessor caller,
        StaffAccessHandler handler,
        [Description("The target staff number, or null when using historicStaffUserId.")] string? staffId,
        [Description("A listed provider key whose staffId is null, or null when using staffId.")] Guid? historicStaffUserId,
        [Description("Version last observed.")] long expectedVersion,
        CancellationToken cancellationToken)
    {
        var actorStaffUserId = caller.RequireStaffUserId();
        var target = await ResolveTargetAsync(
            actorStaffUserId, handler, staffId, historicStaffUserId, cancellationToken);
        var result = await handler.ClearScopeAsync(
            new ClearStaffAccessProfileScopeCommand(
                actorStaffUserId, target.StaffUserId, expectedVersion),
            cancellationToken);
        result.ThrowIfFailure();
        return "Staff access scope cleared.";
    }

    /// <summary>Returns the caller's validated staff number, roles, and scope.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The identity handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The caller's access view, including the validated staff number.</returns>
    [McpServerTool(Name = "get_my_access", Title = "Get my access", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Return your validated staff number, roles, and appointment type scope. Available to any signed-in staff member.")]
    public async Task<MyAccessToolView> GetMyAccessAsync(
        ICallerAccessor caller,
        MeHandler handler,
        CancellationToken cancellationToken)
    {
        var view = await handler.GetAsync(
            caller.RequireStaffUserId(), caller.RequireStaffId(), caller.Roles, cancellationToken);
        return view.ToToolView();
    }

    private static async Task<ResolvedStaffTarget> ResolveTargetAsync(
        Guid actorStaffUserId,
        StaffAccessHandler handler,
        string? staffId,
        Guid? historicStaffUserId,
        CancellationToken cancellationToken)
    {
        var hasStaffId = staffId is not null;
        var hasHistoricStaffUserId = historicStaffUserId is not null;
        if (hasStaffId == hasHistoricStaffUserId)
        {
            throw new McpException(
                "Provide exactly one of staffId or historicStaffUserId.");
        }

        if (hasStaffId)
        {
            var resolved = await handler.ResolveIdentityAsync(
                actorStaffUserId, staffId!, cancellationToken);
            return new ResolvedStaffTarget(
                resolved.ValueOrThrow(), staffId!.Trim().ToUpperInvariant());
        }

        var profiles = (await handler.ListAsync(actorStaffUserId, cancellationToken)).ValueOrThrow();
        var historic = profiles.SingleOrDefault(
            profile => profile.StaffUserId == historicStaffUserId);
        if (historic is null)
        {
            throw new McpException("No such historic staff profile.");
        }

        if (historic.StaffId is not null)
        {
            throw new McpException(
                "Use staffId for a profile with an observed staff number.");
        }

        return new ResolvedStaffTarget(historic.StaffUserId, null);
    }

    private sealed record ResolvedStaffTarget(Guid StaffUserId, string? StaffId);
}
