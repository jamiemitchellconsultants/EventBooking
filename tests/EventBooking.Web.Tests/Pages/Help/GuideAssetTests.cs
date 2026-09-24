namespace EventBooking.Web.Tests.Pages.Help;

public sealed class GuideAssetTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "../../../../../src/EventBooking.Web/wwwroot/help"));

    [Theory]
    [InlineData("admin.md", "Locations", "Staff access", "Event operations", "Audit search")]
    [InlineData("coordinator.md", "Attendees", "Dashboards", "Event operations", "Audit search")]
    [InlineData("manager.md", "Negotiation", "Appointment workspace", "headcount", "capacity")]
    [InlineData("appointment-staff.md", "Appointment workspace", "Check in", "Complete", "No-show")]
    [InlineData("attendee.md", "Choose a time", "Manage", "cancel", "coordinator")]
    public void GuideContainsRoleTasks(string file, params string[] required)
    {
        var markdown = File.ReadAllText(Path.Combine(Root, file));
        Assert.StartsWith("# ", markdown);
        Assert.All(required, value => Assert.Contains(value, markdown, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("JointBooking", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("candidate", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("slot", markdown, StringComparison.OrdinalIgnoreCase);
    }
}
