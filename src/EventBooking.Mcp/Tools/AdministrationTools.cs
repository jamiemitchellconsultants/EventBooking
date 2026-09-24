using System.ComponentModel;
using EventBooking.Api.Auth;
using EventBooking.Application.Access;
using EventBooking.Application.Settings;
using ModelContextProtocol.Server;

namespace EventBooking.Mcp.Tools;

/// <summary>Settings and staff-access scope. There is deliberately no role edit (FR-10.5).</summary>
[McpServerToolType]
public sealed class AdministrationTools
{
    /// <summary>Reads the settings row.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The settings handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The settings.</returns>
    [McpServerTool(
        Name = "get_settings", Title = "Get settings",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads the single settings row and its version.")]
    public async Task<SettingsView> GetSettingsAsync(
        ICallerAccessor caller,
        AdminSettingsHandler handler,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.GetAsync(caller.RequireStaffUserId(), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Updates the settings row.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The save handler.</param>
    /// <param name="inviteExpiryDays">Days an invitation stays usable.</param>
    /// <param name="maxAutoRetryCount">Automatic re-issues before giving up.</param>
    /// <param name="inviteOptionCount">Options offered per invitation.</param>
    /// <param name="expectedVersion">The version the caller read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated settings.</returns>
    [McpServerTool(
        Name = "update_settings", Title = "Update settings",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Updates invite expiry, automatic retry count and option count. Existing invites keep the values they were issued under.")]
    public async Task<SystemSettingsResult> UpdateSettingsAsync(
        ICallerAccessor caller,
        AdminSettingsHandler handler,
        [Description("Days an invitation stays usable, 1 to 60.")] int inviteExpiryDays,
        [Description("Automatic re-issues before giving up, 0 to 10.")] int maxAutoRetryCount,
        [Description("Options offered per invitation, 1 to 5.")] int inviteOptionCount,
        [Description("The version you read.")] long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.SaveAsync(
            new SaveSystemSettingsCommand(
                caller.RequireStaffUserId(), inviteExpiryDays, maxAutoRetryCount,
                inviteOptionCount, expectedVersion),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Lists staff access profiles.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The staff access handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The profiles.</returns>
    [McpServerTool(
        Name = "list_staff_access", Title = "List staff access",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads every profile with its display name, staff number, read-only roles and scope.")]
    public async Task<IReadOnlyList<StaffAccessProfileView>> ListStaffAccessAsync(
        ICallerAccessor caller,
        StaffAccessHandler handler,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.ListAsync(caller.RequireStaffUserId(), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Sets or clears one profile's appointment-type scope.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The scope handler.</param>
    /// <param name="targetStaffUserId">The profile to change.</param>
    /// <param name="appointmentTypeId">The type to scope to, or null to clear.</param>
    /// <param name="expectedVersion">The version the caller read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome, naming any displaced Manager.</returns>
    [McpServerTool(
        Name = "set_staff_access_scope", Title = "Set staff access scope",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Sets or clears one profile's appointment-type scope. Assigning a type that another Manager holds displaces them, and the response names who.")]
    public async Task<object> SetStaffAccessScopeAsync(
        ICallerAccessor caller,
        StaffAccessHandler handler,
        [Description("The staff profile to change.")] Guid targetStaffUserId,
        [Description("The version you read.")] long expectedVersion,
        [Description("The appointment type to scope to, or omit to clear the scope.")]
        Guid? appointmentTypeId = null,
        CancellationToken cancellationToken = default)
    {
        // One tool, two handler operations, exactly as the REST route chooses by the
        // body's shape: a null clears and a value replaces. A clear displaces nobody,
        // so like the route's 204 it answers with nothing to name.
        if (appointmentTypeId is null)
        {
            var cleared = await handler.ClearScopeAsync(
                new ClearStaffAccessProfileScopeCommand(
                    caller.RequireStaffUserId(), targetStaffUserId, expectedVersion),
                cancellationToken);
            cleared.ThrowIfFailure();
            return "Staff access scope cleared.";
        }

        return await handler.ReplaceScopeAsync(
            new ReplaceStaffAccessProfileScopeCommand(
                caller.RequireStaffUserId(), targetStaffUserId, appointmentTypeId,
                expectedVersion),
            cancellationToken).ValueOrThrowAsync();
    }
}
