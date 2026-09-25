// tests/EventBooking.SeedData.Tests/SeedCommandTests.cs (complete)
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

public sealed class SeedCommandTests
{
    private const string Connection = "Host=localhost;Database=eventbooking";

    [Fact]
    public async Task NoFlagAppliesRolesAndMigrationsOnly()
    {
        var steps = new RecordingSteps();
        var options = SeedCliOptions.Parse([Connection], _ => null);

        var exit = await SeedCommand.RunAsync(options, steps, TextWriter.Null, default);

        Assert.Equal(0, exit);
        Assert.Equal(["roles", "migrations"], steps.Calls);
    }

    [Theory]
    [InlineData("--unknown")]
    [InlineData("--reanchor")]
    [InlineData("--reseed")]
    public void InvalidOrDemoOnlyArgumentsAreRejectedBeforeStepsExist(string option)
    {
        var error = Assert.Throws<SeedException>(() =>
            SeedCliOptions.Parse([Connection, option], _ => null));

        Assert.Contains(option, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReseedWithoutExactEnvironmentGuardIsRejected()
    {
        var calls = 0;
        string? Read(string key)
        {
            calls++;
            return key == "EVENTBOOKING_ALLOW_RESEED" ? "TRUE" : null;
        }

        var error = Assert.Throws<SeedException>(() =>
            SeedCliOptions.Parse([Connection, "--demo", "--reseed"], Read));

        Assert.Contains("EVENTBOOKING_ALLOW_RESEED=true", error.Message);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task DemoReanchorAndGuardedReseedRunInSafeOrder()
    {
        var steps = new RecordingSteps();
        var options = SeedCliOptions.Parse(
            [Connection, "--demo", "--reanchor", "--reseed"],
            key => key == "EVENTBOOKING_ALLOW_RESEED" ? "true" : null);

        var exit = await SeedCommand.RunAsync(options, steps, TextWriter.Null, default);

        Assert.Equal(0, exit);
        Assert.Equal(
            ["roles", "migrations", "reanchor", "keycloak:reset", "seed:wipe", "invitations"],
            steps.Calls);
    }

    private sealed class RecordingSteps : ISeedRunSteps
    {
        public List<string> Calls { get; } = [];
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task ApplyDatabaseRolesAsync(CancellationToken ct) { Calls.Add("roles"); return Task.CompletedTask; }
        public Task ApplyMigrationsAsync(CancellationToken ct) { Calls.Add("migrations"); return Task.CompletedTask; }
        public Task ReanchorToTodayAsync(CancellationToken ct) { Calls.Add("reanchor"); return Task.CompletedTask; }
        public Task<KeycloakSeedSummary?> ConvergeKeycloakAsync(bool recreateRealm, CancellationToken ct)
        { Calls.Add(recreateRealm ? "keycloak:reset" : "keycloak"); return Task.FromResult<KeycloakSeedSummary?>(null); }
        public Task<SeedSummary> SeedDemoAsync(bool wipeFirst, CancellationToken ct)
        { Calls.Add(wipeFirst ? "seed:wipe" : "seed"); return Task.FromResult(SeedSummary.Empty); }
        public Task<int> SendDemoInvitationsAsync(CancellationToken ct)
        { Calls.Add("invitations"); return Task.FromResult(1); }
        public Task<int> SeedLoadFixtureAsync(string outputPath, CancellationToken ct)
        {
            Calls.Add($"load:{outputPath}");
            return Task.FromResult(500);
        }
    }
}
