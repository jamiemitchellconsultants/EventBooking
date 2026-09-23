using EventBooking.Api.OpenApi;

namespace EventBooking.Api.Tests;

/// <summary>Locks operation identity and MCP parity invariants before endpoint decoration.</summary>
public sealed class AgentOperationCatalogTests
{
    [Fact]
    public void CatalogHasUniqueCompleteEntries()
    {
        var operations = AgentOperationCatalog.All.Values.ToList();
        Assert.NotEmpty(operations);
        Assert.Equal(operations.Count, operations.Select(x => x.OperationId).Distinct().Count());
        Assert.Equal(operations.Count, operations.Select(x => $"{x.Method} {x.Route}").Distinct().Count());
        Assert.All(operations, operation =>
        {
            Assert.Matches("^[a-z][A-Za-z0-9]+$", operation.OperationId);
            Assert.True((operation.McpTool is null) ^ (operation.ExclusionReason is null));
            Assert.False(operation.Hints.OpenWorld);
        });
        var staff = operations.Where(x => x.McpTool is not null).ToList();
        Assert.Equal(35, staff.Count);
        Assert.Equal(staff.Count, staff.Select(x => x.McpTool).Distinct().Count());
    }

    [Fact]
    public void CatalogContainsTheEightNewParityMappings()
    {
        var names = AgentOperationCatalog.All.Values.Select(x => x.McpTool).ToHashSet();
        Assert.Contains("get_event_operations", names);
        Assert.Contains("start_recovery_invite", names);
        Assert.Contains("cancel_recovery_invite", names);
        Assert.Contains("list_attendee_bookings", names);
        Assert.Contains("cancel_attendee_booking", names);
        Assert.Contains("get_attendee_readiness", names);
        Assert.Contains("search_audit", names);
        Assert.Contains("export_appointment_roster", names);
    }
}
