# 01 — Seed CLI parity (Tasks 1–4)

[← Overview](00-overview.md) · [Ontology](../../ontology.md)

Tasks 1–4 change only the SeedData console host and its operator documentation; nothing in the
domain, application or infrastructure layers moves. Task 1 teaches the option parser an optional
reanchor date, Task 2 reworks orchestration (the one interface change, verbose step lines, the
reanchor and Keycloak lines), Task 3 adds usage text, help and failure formatting at the entry
point, and Task 4 documents the switches for operators.

> Use superpowers:executing-plans. One task at a time; every task ends with its own commit and push.

### Task 1: Optional reanchor date in the parser

**Files:**
- Modify: `src/EventBooking.SeedData/SeedCommand.cs` (the SeedCliOptions record and its Parse method only)
- Test: `tests/EventBooking.SeedData.Tests/SeedCliOptionsReanchorTests.cs` (Create)

**Interfaces:**

```csharp
namespace EventBooking.SeedData;

public sealed record SeedCliOptions(
    string ConnectionString, bool Demo, bool Reanchor,
    DateOnly? ReanchorDate,   // NEW: null for a bare --reanchor (= today in Europe/London); set for --reanchor yyyy-MM-dd
    bool Reseed, bool Verbose, bool LoadFixture, string? LoadFixturePath)
{
    // Consumes the token directly after --reanchor when it has the shape yyyy-MM-dd; that token is
    // never counted as a connection string. A date-shaped token that is not a real date throws
    // SeedException("--reanchor date must be yyyy-MM-dd (got '<token>').").
    public static SeedCliOptions Parse(string[] args, Func<string, string?> environment);
}
```

- [ ] **Step 1: Write the failing test**

Create `tests/EventBooking.SeedData.Tests/SeedCliOptionsReanchorTests.cs`:

```csharp
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

public sealed class SeedCliOptionsReanchorTests
{
    private const string Connection = "Host=localhost;Database=eventbooking";

    [Fact]
    public void DateAfterReanchorIsCapturedAndIsNotAConnectionString()
    {
        var options = SeedCliOptions.Parse(
            [Connection, "--demo", "--reanchor", "2026-10-01"], _ => null);

        Assert.True(options.Reanchor);
        Assert.Equal(new DateOnly(2026, 10, 1), options.ReanchorDate);
        Assert.Equal(Connection, options.ConnectionString);
    }

    [Fact]
    public void DateWorksWhenTheConnectionStringComesAfterIt()
    {
        var options = SeedCliOptions.Parse(
            ["--demo", "--reanchor", "2026-10-01", Connection], _ => null);

        Assert.Equal(new DateOnly(2026, 10, 1), options.ReanchorDate);
        Assert.Equal(Connection, options.ConnectionString);
    }

    [Fact]
    public void DateWorksWithTheConnectionStringTakenFromTheEnvironment()
    {
        var options = SeedCliOptions.Parse(
            ["--demo", "--reanchor", "2026-10-01"],
            key => key == "ConnectionStrings__EventBooking" ? Connection : null);

        Assert.Equal(new DateOnly(2026, 10, 1), options.ReanchorDate);
        Assert.Equal(Connection, options.ConnectionString);
    }

    [Fact]
    public void BareReanchorLeavesTheDateNull()
    {
        var options = SeedCliOptions.Parse([Connection, "--demo", "--reanchor"], _ => null);

        Assert.True(options.Reanchor);
        Assert.Null(options.ReanchorDate);
    }

    [Fact]
    public void NoReanchorLeavesTheDateNull()
    {
        var options = SeedCliOptions.Parse([Connection, "--demo"], _ => null);

        Assert.False(options.Reanchor);
        Assert.Null(options.ReanchorDate);
    }

    [Theory]
    [InlineData("2026-02-30")]
    [InlineData("2026-13-01")]
    [InlineData("2026-00-10")]
    public void DateShapedButImpossibleDatesAreRejectedWithTheDocumentedMessage(string token)
    {
        var error = Assert.Throws<SeedException>(() => SeedCliOptions.Parse(
            [Connection, "--demo", "--reanchor", token], _ => null));

        Assert.Contains("--reanchor date must be yyyy-MM-dd", error.Message, StringComparison.Ordinal);
        Assert.Contains(token, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATokenOfTheWrongShapeIsAStrayPositionalNotADate()
    {
        var error = Assert.Throws<SeedException>(() => SeedCliOptions.Parse(
            [Connection, "--demo", "--reanchor", "25/09/2026"], _ => null));

        Assert.Contains("exactly one", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReanchorDateWithoutDemoIsRejected()
    {
        var error = Assert.Throws<SeedException>(() => SeedCliOptions.Parse(
            [Connection, "--reanchor", "2026-10-01"], _ => null));

        Assert.Contains("--reanchor requires --demo", error.Message, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test tests/EventBooking.SeedData.Tests --filter "FullyQualifiedName~SeedCliOptionsReanchorTests"
```

Expected: build error, `'SeedCliOptions' does not contain a definition for 'ReanchorDate'`.

- [ ] **Step 3: Implement the parser change**

In `src/EventBooking.SeedData/SeedCommand.cs`, add `using System.Globalization;` as the first line
and replace the whole SeedCliOptions record (everything from `public sealed record SeedCliOptions(`
through its closing `}`) with:

```csharp
public sealed record SeedCliOptions(
    string ConnectionString, bool Demo, bool Reanchor, DateOnly? ReanchorDate, bool Reseed,
    bool Verbose, bool LoadFixture, string? LoadFixturePath)
{
    public static SeedCliOptions Parse(string[] args, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(environment);
        var tokens = args.ToList();
        DateOnly? reanchorDate = null;
        var reanchorIndex = tokens.IndexOf("--reanchor");
        if (reanchorIndex >= 0 && reanchorIndex + 1 < tokens.Count
            && LooksLikeDate(tokens[reanchorIndex + 1]))
        {
            var token = tokens[reanchorIndex + 1];
            if (!DateOnly.TryParseExact(
                    token, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                throw new SeedException($"--reanchor date must be yyyy-MM-dd (got '{token}').");
            reanchorDate = parsed;
            tokens.RemoveAt(reanchorIndex + 1);
        }

        var values = tokens.Where(x => !x.StartsWith('-')).ToList();
        if (values.Count > 1)
            throw new SeedException("Supply exactly one PostgreSQL connection string.");
        var connection = values.SingleOrDefault() ?? environment("ConnectionStrings__EventBooking");
        if (string.IsNullOrWhiteSpace(connection))
            throw new SeedException("A PostgreSQL connection string argument or ConnectionStrings__EventBooking is required.");
        var known = new HashSet<string>(StringComparer.Ordinal)
            { "--demo", "--reanchor", "--reseed", "--verbose", "--load-fixture" };
        var unknown = tokens.Where(x => x.StartsWith('-') && !known.Contains(x)).ToList();
        if (unknown.Count > 0)
            throw new SeedException($"Unknown option '{unknown[0]}'.");
        var demo = tokens.Contains("--demo", StringComparer.Ordinal);
        var reanchor = tokens.Contains("--reanchor", StringComparer.Ordinal);
        var reseed = tokens.Contains("--reseed", StringComparer.Ordinal);
        var load = tokens.Contains("--load-fixture", StringComparer.Ordinal);
        if (reanchor && !demo) throw new SeedException("--reanchor requires --demo.");
        if (reseed && !demo) throw new SeedException("--reseed requires --demo.");
        if (reseed && !string.Equals(environment("EVENTBOOKING_ALLOW_RESEED"), "true", StringComparison.Ordinal))
            throw new SeedException("--reseed requires EVENTBOOKING_ALLOW_RESEED=true.");
        if (load && !demo) throw new SeedException("--load-fixture requires --demo.");
        if (load && !string.Equals(environment("EVENTBOOKING_ENABLE_LOAD_FIXTURE"), "true", StringComparison.Ordinal))
            throw new SeedException("--load-fixture requires EVENTBOOKING_ENABLE_LOAD_FIXTURE=true.");
        var path = load ? environment("EVENTBOOKING_LOAD_FIXTURE_PATH") : null;
        if (load && (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)))
            throw new SeedException("--load-fixture requires an absolute EVENTBOOKING_LOAD_FIXTURE_PATH.");
        return new SeedCliOptions(connection, demo, reanchor, reanchorDate, reseed,
            tokens.Contains("--verbose", StringComparer.Ordinal), load, path);
    }

    // Shape check only (dddd-dd-dd); whether it is a real calendar date is decided by TryParseExact.
    private static bool LooksLikeDate(string token) =>
        token.Length == 10 && token[4] == '-' && token[7] == '-'
        && token.Where((_, index) => index != 4 && index != 7).All(char.IsAsciiDigit);
}
```

Leave ISeedRunSteps and SeedCommand exactly as they are in this task.

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test tests/EventBooking.SeedData.Tests --filter "FullyQualifiedName~SeedCliOptionsReanchorTests|FullyQualifiedName~SeedCommandTests"
```

Expected: all pass (the 8 new tests and the 7 existing SeedCommandTests).

- [ ] **Step 5: Commit and push**

```bash
git add -A
git commit -m "feat(seed): accept an optional yyyy-MM-dd date after --reanchor"
git push
```

### Task 2: Verbose step lines, reanchor date and Keycloak counts

**Files:**
- Modify: `src/EventBooking.SeedData/SeedCommand.cs` (ISeedRunSteps and SeedCommand)
- Modify: `src/EventBooking.SeedData/SeedRunSteps.cs` (the reanchor step)
- Modify: `tests/EventBooking.SeedData.Tests/SeedCommandTests.cs` (one method of the recording double)
- Test: `tests/EventBooking.SeedData.Tests/SeedCommandProgressTests.cs` (Create)

**Interfaces:**

```csharp
namespace EventBooking.SeedData;

public interface ISeedRunSteps : IAsyncDisposable
{
    // ... unchanged members ...

    // REPLACES ReanchorToTodayAsync(ct). Applies "date" (or today in Europe/London when null) as the
    // demo anchor, moves existing demo rows to it, and returns the anchor that was applied.
    Task<DateOnly> ReanchorAsync(DateOnly? date, CancellationToken ct);
}

// SeedCommand.RunAsync keeps its signature. New behaviour:
//  - always writes "[seed] Reanchored to yyyy-MM-dd." after a reanchor step (the date the step returned)
//  - always writes "Keycloak seed complete: N roles created, N mapper writes, N users created,
//    N role-mapping writes." when ConvergeKeycloakAsync returns a summary
//  - when options.Verbose, writes the "[seed] ..." step lines listed in the spec
```

- [ ] **Step 1: Write the failing test**

Create `tests/EventBooking.SeedData.Tests/SeedCommandProgressTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test tests/EventBooking.SeedData.Tests --filter "FullyQualifiedName~SeedCommandProgressTests"
```

Expected: build error, `'ISeedRunSteps' does not contain a definition for 'ReanchorAsync'` (the
double implements a member the interface lacks, and ISeedRunSteps still requires
ReanchorToTodayAsync).

- [ ] **Step 3: Change the interface and the command**

In `src/EventBooking.SeedData/SeedCommand.cs`, replace everything from
`public interface ISeedRunSteps` to the end of the file with:

```csharp
public interface ISeedRunSteps : IAsyncDisposable
{
    Task ApplyDatabaseRolesAsync(CancellationToken ct);
    Task ApplyMigrationsAsync(CancellationToken ct);
    Task<DateOnly> ReanchorAsync(DateOnly? date, CancellationToken ct);
    Task<KeycloakSeedSummary?> ConvergeKeycloakAsync(bool recreateRealm, CancellationToken ct);
    Task<SeedSummary> SeedDemoAsync(bool wipeFirst, CancellationToken ct);
    Task<int> SendDemoInvitationsAsync(CancellationToken ct);
    Task<int> SeedLoadFixtureAsync(string outputPath, CancellationToken ct);
}

public static class SeedCommand
{
    public static async Task<int> RunAsync(
        SeedCliOptions options, ISeedRunSteps steps, TextWriter output, CancellationToken ct)
    {
        async Task Progress(string message)
        {
            if (options.Verbose) await output.WriteLineAsync($"[seed] {message}");
        }

        await Progress("Applying database roles...");
        await steps.ApplyDatabaseRolesAsync(ct);
        await Progress("Applying pending migrations...");
        await steps.ApplyMigrationsAsync(ct);
        await Progress("Migrations applied.");
        if (!options.Demo)
        {
            await output.WriteLineAsync("Database roles and migrations applied; demo data was not requested.");
            return 0;
        }
        if (options.Reanchor)
        {
            var anchor = await steps.ReanchorAsync(options.ReanchorDate, ct);
            await output.WriteLineAsync($"[seed] Reanchored to {anchor:yyyy-MM-dd}.");
        }
        if (options.Reseed)
            await Progress("Reseed requested: the Keycloak realm will be deleted and recreated first, if configured.");
        var keycloak = await steps.ConvergeKeycloakAsync(options.Reseed, ct);
        if (keycloak is null)
        {
            await Progress("Keycloak provider seed skipped.");
        }
        else
        {
            await Progress(options.Reseed
                ? "Keycloak realm reset and convergence complete."
                : "Keycloak convergence complete.");
            await output.WriteLineAsync(
                $"Keycloak seed complete: {keycloak.RolesCreated} roles created, " +
                $"{keycloak.MapperWrites} mapper writes, " +
                $"{keycloak.UsersCreated} users created, " +
                $"{keycloak.RoleMappingWrites} role-mapping writes.");
        }
        await Progress("Seeding demo data...");
        var summary = await steps.SeedDemoAsync(options.Reseed, ct);
        await Progress("Sending demo invitations...");
        var sent = await steps.SendDemoInvitationsAsync(ct);
        await output.WriteLineAsync(
            $"Demo seed complete: {summary.LocationsEnsured} locations, " +
            $"{summary.AppointmentTypesEnsured} appointment types, {summary.AttendeesEnsured} attendees; " +
            $"{sent} invitations sent; Keycloak {(keycloak is null ? "not configured" : "converged")}.");
        if (options.LoadFixture)
        {
            await Progress("Writing load fixture...");
            var count = await steps.SeedLoadFixtureAsync(options.LoadFixturePath!, ct);
            await output.WriteLineAsync($"Load fixture ready: {count} invitations.");
        }
        return 0;
    }
}
```

- [ ] **Step 4: Change the production reanchor step**

In `src/EventBooking.SeedData/SeedRunSteps.cs`, replace the whole ReanchorToTodayAsync method with:

```csharp
    public async Task<DateOnly> ReanchorAsync(DateOnly? date, CancellationToken ct)
    {
        await using var scope = Scope();
        var anchor = date ?? TodayInLondon(scope);
        DemoSeedSpec.OverrideAnchor(anchor);
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>()
            .ReanchorAsync(DemoSeedSpec.Build(), ct);
        return anchor;
    }

    private static DateOnly TodayInLondon(AsyncServiceScope scope)
    {
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var zones = scope.ServiceProvider.GetRequiredService<EventBooking.Domain.Time.IEventWindowZones>();
        return zones.LocalDateOf(clock.UtcNow, "Europe/London");
    }
```

- [ ] **Step 5: Update the existing recording double**

In `tests/EventBooking.SeedData.Tests/SeedCommandTests.cs`, replace the line

```csharp
        public Task ReanchorToTodayAsync(CancellationToken ct) { Calls.Add("reanchor"); return Task.CompletedTask; }
```

with

```csharp
        public Task<DateOnly> ReanchorAsync(DateOnly? date, CancellationToken ct)
        { Calls.Add("reanchor"); return Task.FromResult(date ?? new DateOnly(2026, 9, 25)); }
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test tests/EventBooking.SeedData.Tests --filter "FullyQualifiedName~SeedCommand|FullyQualifiedName~SeedCliOptions"
```

Expected: build succeeds with no warnings; every SeedCommand and SeedCliOptions test passes.

- [ ] **Step 7: Commit and push**

```bash
git add -A
git commit -m "feat(seed): verbose step progress, reanchor confirmation and Keycloak counts"
git push
```

### Task 3: Usage text, help and failure reporting

**Files:**
- Create: `src/EventBooking.SeedData/SeedUsage.cs`
- Modify: `src/EventBooking.SeedData/Program.cs`
- Test: `tests/EventBooking.SeedData.Tests/SeedUsageTests.cs` (Create)

**Interfaces:**

```csharp
namespace EventBooking.SeedData;

public static class SeedUsage
{
    // Full per-switch documentation, including the environment variables each switch depends on.
    public const string Text = "...";

    // True when any argument is --help or -h.
    public static bool IsHelpRequest(IReadOnlyList<string> args);
}

public static class SeedFailureReport
{
    // "Seed failed: <message>", or "Seed failed: <exception.ToString()>" when --verbose is among args.
    public static string Format(Exception exception, IReadOnlyList<string> args);
}
```

- [ ] **Step 1: Write the failing test**

Create `tests/EventBooking.SeedData.Tests/SeedUsageTests.cs`:

```csharp
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

public sealed class SeedUsageTests
{
    [Theory]
    [InlineData("--demo")]
    [InlineData("--reanchor [yyyy-MM-dd]")]
    [InlineData("--reseed")]
    [InlineData("--load-fixture")]
    [InlineData("--verbose")]
    [InlineData("--help")]
    [InlineData("ConnectionStrings__EventBooking")]
    [InlineData("EVENTBOOKING_ALLOW_RESEED=true")]
    [InlineData("EVENTBOOKING_ENABLE_LOAD_FIXTURE=true")]
    [InlineData("EVENTBOOKING_LOAD_FIXTURE_PATH")]
    [InlineData("Keycloak__RealmExportPath")]
    [InlineData("Tokens__SigningKey")]
    [InlineData("Portal__BaseUrl")]
    public void UsageTextDocumentsEverySwitchAndItsEnvironment(string expected)
    {
        Assert.Contains(expected, SeedUsage.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void UsageTextStatesThatMigrateOnlyIsTheDefault()
    {
        Assert.Contains("Without --demo", SeedUsage.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--help", true)]
    [InlineData("-h", true)]
    [InlineData("--demo", false)]
    [InlineData("--helpful", false)]
    public void HelpRequestDetectionMatchesOnlyTheTwoSpellings(string argument, bool expected)
    {
        Assert.Equal(expected, SeedUsage.IsHelpRequest(["Host=x", argument]));
    }

    [Fact]
    public void FailureReportIsMessageOnlyWithoutVerbose()
    {
        var text = SeedFailureReport.Format(Thrown("boom"), ["Host=x", "--demo"]);

        Assert.Equal("Seed failed: boom", text);
    }

    [Fact]
    public void FailureReportIsTheFullExceptionWithVerbose()
    {
        var text = SeedFailureReport.Format(Thrown("boom"), ["Host=x", "--demo", "--verbose"]);

        Assert.StartsWith("Seed failed: System.InvalidOperationException: boom", text, StringComparison.Ordinal);
        Assert.Contains("   at ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseFailuresUnderVerboseAlsoReportTheFullException()
    {
        string[] args = ["Host=x", "--reanchor", "--verbose"];
        var failure = Assert.Throws<SeedException>(() => SeedCliOptions.Parse(args, _ => null));

        var text = SeedFailureReport.Format(failure, args);

        Assert.Contains("--reanchor requires --demo.", text, StringComparison.Ordinal);
        Assert.Contains("SeedException", text, StringComparison.Ordinal);
    }

    private static InvalidOperationException Thrown(string message)
    {
        try
        {
            throw new InvalidOperationException(message);
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test tests/EventBooking.SeedData.Tests --filter "FullyQualifiedName~SeedUsageTests"
```

Expected: build error, `The name 'SeedUsage' does not exist in the current context`.

- [ ] **Step 3: Create the usage and failure types**

Create `src/EventBooking.SeedData/SeedUsage.cs`:

```csharp
namespace EventBooking.SeedData;

public static class SeedUsage
{
    public const string Text = """
        Usage: EventBooking.SeedData <postgres connection string> [--demo [--reanchor [yyyy-MM-dd]] [--reseed] [--load-fixture]] [--verbose]
           or set the ConnectionStrings__EventBooking environment variable.
           Database roles and pending migrations are always applied first, whichever mode runs.
           Without --demo the run stops there: migrations only, nothing is seeded.
           --demo seeds the deterministic demo dataset, converges the Keycloak demo staff (when
           Keycloak is configured) and sends the demo invitations through SMTP.
           --reanchor [yyyy-MM-dd] resolves every demo day offset against the given date (default:
           today in Europe/London) instead of the built-in anchor, and moves existing demo rows to
           match. Requires --demo. Use it when the built-in anchor has gone stale.
           --reseed wipes every domain table, then deletes and recreates the Keycloak realm from
           the file named by Keycloak__RealmExportPath (when Keycloak is configured), then seeds
           fresh. Requires --demo and EVENTBOOKING_ALLOW_RESEED=true.
           --load-fixture writes a load-test fixture manifest. Requires --demo,
           EVENTBOOKING_ENABLE_LOAD_FIXTURE=true and an absolute EVENTBOOKING_LOAD_FIXTURE_PATH.
           --verbose reports per-step progress; a failure prints the full exception.
           --help, -h prints this text.
           Keycloak demo users are converged when Keycloak__BaseUrl, Keycloak__Realm,
           Keycloak__AdminRealm, Keycloak__AdminUsername, Keycloak__AdminPassword and
           Keycloak__DemoPassword are set; otherwise only the database is seeded.
           Demo invitations are sent through SMTP using Portal__BaseUrl, Tokens__SigningKey,
           Email__Smtp__Host and Email__Smtp__Port. Local defaults apply only for a loopback
           Portal__BaseUrl; a non-local portal needs an explicit Tokens__SigningKey and
           Email__Smtp__Host. Match Tokens__SigningKey and Portal__BaseUrl to the running API.
        """;

    public static bool IsHelpRequest(IReadOnlyList<string> args) =>
        args.Any(x => x is "--help" or "-h");
}

public static class SeedFailureReport
{
    public static string Format(Exception exception, IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(args);
        return args.Contains("--verbose")
            ? $"Seed failed: {exception}"
            : $"Seed failed: {exception.Message}";
    }
}
```

- [ ] **Step 4: Replace the entry point**

Replace the entire contents of `src/EventBooking.SeedData/Program.cs` with:

```csharp
// src/EventBooking.SeedData/Program.cs (complete)
using EventBooking.SeedData;

if (SeedUsage.IsHelpRequest(args))
{
    Console.Out.WriteLine(SeedUsage.Text);
    return 0;
}

SeedCliOptions options;
try
{
    options = SeedCliOptions.Parse(args, Environment.GetEnvironmentVariable);
}
catch (SeedException exception)
{
    Console.Error.WriteLine(SeedFailureReport.Format(exception, args));
    Console.Error.WriteLine(SeedUsage.Text);
    return 2;
}

try
{
    await using var steps = SeedRunSteps.Create(options, Environment.GetEnvironmentVariable);
    return await SeedCommand.RunAsync(options, steps, Console.Out, CancellationToken.None);
}
catch (Exception exception)
{
    Console.Error.WriteLine(SeedFailureReport.Format(exception, args));
    return 2;
}
```

- [ ] **Step 5: Run the tests and smoke-test the entry point**

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test tests/EventBooking.SeedData.Tests --filter "FullyQualifiedName~SeedUsageTests"
dotnet run --project src/EventBooking.SeedData -- --help
dotnet run --project src/EventBooking.SeedData -- "Host=x" --reanchor; echo "exit=$?"
```

Expected: build clean; the usage tests pass; `--help` prints the usage text and exits 0; the last
command prints `Seed failed: --reanchor requires --demo.` then the usage text and `exit=2`.

- [ ] **Step 6: Commit and push**

```bash
git add -A
git commit -m "feat(seed): --help usage text and full-exception failure reports under --verbose"
git push
```

### Task 4: Operator documentation

**Files:**
- Modify: `deploy/home-lab/README.md`

**Interfaces:** none (documentation only).

- [ ] **Step 1: Add the Seed switches section**

In `deploy/home-lab/README.md`, insert this section immediately before the line `## Upgrade and rollback`:

````markdown
## Seed switches

The seed job (`docker compose --profile seed run --rm eventbooking-seed [switches]`) always applies
database roles and pending migrations first. Without `--demo` it stops there. The switches match
JointBooking's seed host, with one deliberate inversion: JointBooking seeds by default and uses
`--skip-seed` for migrate-only; EventBooking is migrate-only by default and seeds with `--demo`.

| Switch | Effect |
|---|---|
| `--demo` | Seed the demo dataset, converge the Keycloak demo staff and send the demo invitations. |
| `--reanchor [yyyy-MM-dd]` | Resolve demo dates against the given date (default: today in Europe/London) and move existing demo rows to match. Needs `--demo`. A malformed date fails the run rather than falling back to today. |
| `--reseed` | Destructive: wipe the domain tables and recreate the Keycloak realm. Needs `--demo` and `EVENTBOOKING_ALLOW_RESEED=true`. |
| `--load-fixture` | Write the load-test fixture. Needs `--demo` and the two `EVENTBOOKING_*LOAD_FIXTURE*` variables. |
| `--verbose` | Print a `[seed]` line for every step; a failure prints the full exception. |
| `--help`, `-h` | Print the full usage text. |

To refresh stale demo dates without wiping anything:

```bash
docker compose --profile seed run --rm eventbooking-seed --demo --reanchor 2026-10-01 --verbose
```
````

- [ ] **Step 2: Check the ontology terms**

```bash
git add deploy/home-lab/README.md
node scripts/check-ontology-terms.mjs
```

Expected: `ontology term check: OK`.

- [ ] **Step 3: Commit and push**

```bash
git add -A
git commit -m "docs(deploy): document the seed switches"
git push
```
