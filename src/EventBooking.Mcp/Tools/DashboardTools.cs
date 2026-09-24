using System.ComponentModel;
using EventBooking.Api.Auth;
using EventBooking.Application.Dashboards;
using ModelContextProtocol.Server;

namespace EventBooking.Mcp.Tools;

/// <summary>The coordinator dashboard.</summary>
[McpServerToolType]
public sealed class DashboardTools
{
    /// <summary>Reads the dashboard tabs.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The dashboards handler.</param>
    /// <param name="locationId">The location filter, or null for every site.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The dashboard.</returns>
    [McpServerTool(
        Name = "get_dashboards", Title = "Get dashboards",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads the three tabs and their counts (FR-13), optionally filtered by location.")]
    public async Task<DashboardsView> GetDashboardsAsync(
        ICallerAccessor caller,
        GetDashboardsHandler handler,
        [Description("Narrow to one location.")] Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new GetDashboardsQuery(caller.RequireStaffUserId(), locationId),
            cancellationToken);
        return result.ValueOrThrow();
    }
}
