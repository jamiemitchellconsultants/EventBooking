// tests/EventBooking.Api.Tests/Documentation/RunbookContractTests.cs (complete)
using Xunit;

namespace EventBooking.Api.Tests.Documentation;

public sealed class RunbookContractTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "../../../../../"));

    private static string Read(string path) => File.ReadAllText(Path.Combine(Root, path));

    [Fact]
    public void Local_runbook_keeps_the_three_ordered_Task_29_commands()
    {
        var demo = Read("docs/runbooks/demo.md");
        var infrastructure = demo.IndexOf(
            "docker compose up --detach --build --wait postgres keycloak mailpit",
            StringComparison.Ordinal);
        var seed = demo.IndexOf(
            "docker compose --profile seed run --rm seed --demo --reanchor",
            StringComparison.Ordinal);
        var apps = demo.IndexOf(
            "docker compose up --detach --build --wait api mcp web",
            StringComparison.Ordinal);

        Assert.True(infrastructure >= 0 && seed > infrastructure && apps > seed);
        Assert.Contains("http://localhost:5001/health/ready", demo);
        Assert.Contains("http://localhost:5002", demo);
        Assert.Contains("http://localhost:8025", demo);
    }

    [Fact]
    public void Demo_identifies_real_seed_users_and_future_date_limit()
    {
        var demo = Read("docs/runbooks/demo.md");
        foreach (var user in new[] { "admin", "coordinator", "coordinator.med", "manager.fit",
                     "manager.ind", "manager.lab", "appointment.med", "appointment.unscoped" })
            Assert.Contains(user, demo);
        Assert.Contains("EventBooking1!", demo);
        Assert.Contains("demo.attendee.01@example.test", demo);
        Assert.Contains("local date", demo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Operator_guide_preserves_safe_recovery_and_release_procedure()
    {
        var guide = Read("deploy/home-lab/README.md");
        Assert.Contains("EVENTBOOKING_IMAGE_TAG", guide);
        Assert.Contains("--profile backup", guide);
        Assert.Contains("pg_restore", guide);
        Assert.Contains("fresh volume", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not run a down migration", guide);
        Assert.Contains("/health/ready", guide);
    }

    [Fact]
    public void Entry_point_and_design_match_six_managed_and_unmanaged_types()
    {
        var readme = Read("README.md");
        var design = Read("docs/design/07-deployment.md");
        Assert.Contains("docs/runbooks/demo.md", readme);
        Assert.Contains("docs/design/README.md", readme);
        Assert.Contains("LAB", design);
        Assert.Contains("6 `AppointmentType`s", design);
        Assert.Contains("--load-fixture", design);
        Assert.Contains("EVENTBOOKING_ENABLE_LOAD_FIXTURE", design);
        Assert.Contains("ESC is active but has no Manager", design);
        Assert.Contains("DOC is inactive", design);
    }
}
