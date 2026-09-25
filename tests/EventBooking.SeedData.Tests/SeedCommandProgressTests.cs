using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

public sealed class SeedCommandProgressTests
{
    private const string Connection = "Host=localhost;Database=eventbooking";

    private static readonly Func<string, string?> NoEnvironment = _ => null;

    [Fact]
    public async Task ReanchorStepReceivesTheRequestedDateAndTheLineShowsTheDateItReturned()
    {
        var steps = new ScriptedSteps { ReanchorResult = new DateOnly(2026, 11, 2) };
        var options = SeedCliOptions.Parse(
            [Connection, "--demo", "--reanchor", "2026-10-01"], NoEnvironment);
        var output = new StringWriter();

        var exit = await SeedCommand.RunAsync(options, steps, output, default);

        Assert.Equal(0, exit);
        Assert.Equal(new DateOnly(2026, 10, 1), steps.ReanchorRequested);
        Assert.Contains("[seed] Reanchored to 2026-11-02.", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BareReanchorAsksTheStepForTodayByPassingNoDate()
    {
        var steps = new ScriptedSteps();
        var options = SeedCliOptions.Parse([Connection, "--demo", "--reanchor"], NoEnvironment);

        await SeedCommand.RunAsync(options, steps, new StringWriter(), default);

        Assert.True(steps.ReanchorCalled);
        Assert.Null(steps.ReanchorRequested);
    }

    [Fact]
    public async Task WithoutReanchorNoReanchorStepRunsAndNoReanchorLineIsPrinted()
    {
        var steps = new ScriptedSteps();
        var options = SeedCliOptions.Parse([Connection, "--demo"], NoEnvironment);
        var output = new StringWriter();

        await SeedCommand.RunAsync(options, steps, output, default);

        Assert.False(steps.ReanchorCalled);
        Assert.DoesNotContain("Reanchored", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerboseDemoRunEmitsTheStepLinesInOrder()
    {
        var steps = new ScriptedSteps();
        var options = SeedCliOptions.Parse([Connection, "--demo", "--verbose"], NoEnvironment);
        var output = new StringWriter();

        await SeedCommand.RunAsync(options, steps, output, default);

        AssertInOrder(
            output.ToString(),
            "[seed] Applying database roles...",
            "[seed] Applying pending migrations...",
            "[seed] Migrations applied.",
            "[seed] Keycloak provider seed skipped.",
            "[seed] Seeding demo data...",
            "[seed] Sending demo invitations...",
            "Demo seed complete:");
    }

    [Fact]
    public async Task NonVerboseRunEmitsNoStepLines()
    {
        var steps = new ScriptedSteps();
        var options = SeedCliOptions.Parse([Connection, "--demo"], NoEnvironment);
        var output = new StringWriter();

        await SeedCommand.RunAsync(options, steps, output, default);

        var text = output.ToString();
        Assert.DoesNotContain("[seed]", text, StringComparison.Ordinal);
        Assert.Contains("Demo seed complete:", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerboseMigrationOnlyRunReportsRolesAndMigrationsThenStops()
    {
        var steps = new ScriptedSteps();
        var options = SeedCliOptions.Parse([Connection, "--verbose"], NoEnvironment);
        var output = new StringWriter();

        var exit = await SeedCommand.RunAsync(options, steps, output, default);

        Assert.Equal(0, exit);
        AssertInOrder(
            output.ToString(),
            "[seed] Applying database roles...",
            "[seed] Applying pending migrations...",
            "[seed] Migrations applied.",
            "demo data was not requested");
        Assert.DoesNotContain("Seeding demo data", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReturnedKeycloakSummaryPrintsItsCountsAndVerboseSaysConverged()
    {
        var steps = new ScriptedSteps
        {
            Keycloak = new KeycloakSeedSummary(2, 1, 6, 12, new Dictionary<string, Guid>()),
        };
        var options = SeedCliOptions.Parse([Connection, "--demo", "--verbose"], NoEnvironment);
        var output = new StringWriter();

        await SeedCommand.RunAsync(options, steps, output, default);

        var text = output.ToString();
        Assert.Contains(
            "Keycloak seed complete: 2 roles created, 1 mapper writes, 6 users created, 12 role-mapping writes.",
            text, StringComparison.Ordinal);
        Assert.Contains("[seed] Keycloak convergence complete.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("provider seed skipped", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerboseReseedAnnouncesTheRealmRecreationAndResetCompletion()
    {
        var steps = new ScriptedSteps
        {
            Keycloak = new KeycloakSeedSummary(4, 1, 6, 12, new Dictionary<string, Guid>()),
        };
        var options = SeedCliOptions.Parse(
            [Connection, "--demo", "--reseed", "--verbose"],
            key => key == "EVENTBOOKING_ALLOW_RESEED" ? "true" : null);
        var output = new StringWriter();

        await SeedCommand.RunAsync(options, steps, output, default);

        AssertInOrder(
            output.ToString(),
            "[seed] Reseed requested: the Keycloak realm will be deleted and recreated first, if configured.",
            "[seed] Keycloak realm reset and convergence complete.");
    }

    private static void AssertInOrder(string text, params string[] parts)
    {
        var from = 0;
        foreach (var part in parts)
        {
            var at = text.IndexOf(part, from, StringComparison.Ordinal);
            Assert.True(at >= 0, $"Expected '{part}' after position {from} in:\n{text}");
            from = at + part.Length;
        }
    }

    private sealed class ScriptedSteps : ISeedRunSteps
    {
        public bool ReanchorCalled { get; private set; }
        public DateOnly? ReanchorRequested { get; private set; }
        public DateOnly ReanchorResult { get; init; } = new(2026, 9, 25);
        public KeycloakSeedSummary? Keycloak { get; init; }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task ApplyDatabaseRolesAsync(CancellationToken ct) => Task.CompletedTask;
        public Task ApplyMigrationsAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<DateOnly> ReanchorAsync(DateOnly? date, CancellationToken ct)
        {
            ReanchorCalled = true;
            ReanchorRequested = date;
            return Task.FromResult(ReanchorResult);
        }

        public Task<KeycloakSeedSummary?> ConvergeKeycloakAsync(bool recreateRealm, CancellationToken ct) =>
            Task.FromResult(Keycloak);

        public Task<SeedSummary> SeedDemoAsync(bool wipeFirst, CancellationToken ct) =>
            Task.FromResult(SeedSummary.Empty);

        public Task<int> SendDemoInvitationsAsync(CancellationToken ct) => Task.FromResult(1);
        public Task<int> SeedLoadFixtureAsync(string outputPath, CancellationToken ct) => Task.FromResult(0);
    }
}
