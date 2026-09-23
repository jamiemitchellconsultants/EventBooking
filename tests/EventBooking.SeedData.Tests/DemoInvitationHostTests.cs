using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using EventBooking.Application;
using EventBooking.Application.Bookings;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EventBooking.SeedData.Tests;

/// <summary>Tests real seed-process configuration, exit codes and MIME delivery.</summary>
public sealed class DemoInvitationHostTests : IAsyncLifetime
{
    private const string SigningKey = "host-test-signing-key-at-least-32-characters";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly LoopbackSmtpReceiver _smtp = new();

    /// <inheritdoc/>
    public Task InitializeAsync() => _postgres.StartAsync();
    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await _smtp.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>The console sends clickable HTML/text emails and a second invocation sends none.</summary>
    [Fact]
    public async Task ConsoleSeedSendsUsableMailAndRerunDoesNotDuplicate()
    {
        var first = await RunAsync(false);
        Assert.True(first.ExitCode == 0, first.Output);
        Assert.Equal(5, _smtp.Messages.Count);
        using var services = VerificationServices();
        using var scope = services.CreateScope();
        var view = scope.ServiceProvider.GetRequiredService<ViewInviteHandler>();
        foreach (var message in _smtp.Messages)
        {
            Assert.NotNull(message.TextBody);
            Assert.NotNull(message.HtmlBody);
            var token = Regex.Match(message.TextBody,
                @"https://host-demo\.example\.test/book/([^\s]+)").Groups[1].Value;
            Assert.NotEmpty(token);
            Assert.Contains($"href=\"https://host-demo.example.test/book/{token}\"", message.HtmlBody);
            var result = await view.HandleAsync(new ViewInviteQuery(token), default);
            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value.Options.Count);
            Assert.DoesNotContain(token, first.Output);
        }
        Assert.DoesNotContain(SigningKey, first.Output);
        var second = await RunAsync(false);
        Assert.True(second.ExitCode == 0, second.Output);
        Assert.Equal(5, _smtp.Messages.Count);
    }

    /// <summary>Migration-only bypasses even invalid email settings and creates no demo Attendees.</summary>
    [Fact]
    public async Task MigrationOnlyDoesNotReadEmailConfiguration()
    {
        var result = await RunAsync(true, new Dictionary<string, string?>
        {
            ["Tokens__SigningKey"] = "invalid",
            ["Portal__BaseUrl"] = "invalid",
            ["Email__Smtp__Port"] = "invalid",
        });
        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Empty(_smtp.Messages);
        using var services = VerificationServices();
        using var scope = services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.Equal(0, await database.Attendees.CountAsync());
        Assert.NotEmpty(await database.Database.GetAppliedMigrationsAsync());
    }

    /// <summary>SMTP rejection yields a failed durable delivery and a nonzero process exit.</summary>
    [Fact]
    public async Task ConsoleFailureCanResumeAfterSmtpRecovery()
    {
        _smtp.RejectMessages = true;
        var failed = await RunAsync(false);
        Assert.Equal(2, failed.ExitCode);
        Assert.Contains("delivery failed", failed.Output);
        Assert.Empty(_smtp.Messages);
        using (var services = VerificationServices())
        using (var scope = services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            Assert.Equal(1, await database.EmailLogs.CountAsync(e => e.Status == EmailStatus.Failed));
        }
        _smtp.RejectMessages = false;
        var resumed = await RunAsync(false);
        Assert.True(resumed.ExitCode == 0, resumed.Output);
        Assert.Equal(5, _smtp.Messages.Count);
    }

    private ServiceProvider VerificationServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBookingInfrastructure(_postgres.GetConnectionString(),
            new ClockOptions("Europe/London"), new TokenOptions(SigningKey));
        services.AddSingleton<EventBooking.Domain.Time.IEventWindowZones>(
            new EventBooking.Infrastructure.Time.NodaTimeEventWindowZones());
        services.AddEventBookingApplication(new AttendeePortalOptions(
            "https://host-demo.example.test", "help@example.com"));
        return services.BuildServiceProvider();
    }

    private async Task<(int ExitCode, string Output)> RunAsync(
        bool skipSeed, Dictionary<string, string?>? overrides = null)
    {
        var root = RepoRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "run", "--no-build", "--configuration", configuration,
            "--project", "src/EventBooking.SeedData", "--", _postgres.GetConnectionString() })
            start.ArgumentList.Add(argument);
        // Never inherit developer Keycloak credentials or an external SMTP destination.
        foreach (var key in start.Environment.Keys.Where(key =>
            key.StartsWith("Keycloak__", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("Email__", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("Tokens__", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("Portal__", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("Clock__", StringComparison.OrdinalIgnoreCase)).ToArray())
            start.Environment.Remove(key);
        start.Environment["Tokens__SigningKey"] = SigningKey;
        start.Environment["Portal__BaseUrl"] = "https://host-demo.example.test/";
        start.Environment["Email__Smtp__Host"] = "127.0.0.1";
        start.Environment["Email__Smtp__Port"] = _smtp.Port.ToString(CultureInfo.InvariantCulture);
        if (skipSeed) start.ArgumentList.Add("--skip-seed");
        else
        {
            start.ArgumentList.Add("--reanchor");
            start.ArgumentList.Add(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")).DateTime)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        if (overrides is not null)
            foreach (var (key, value) in overrides) start.Environment[key] = value;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Seed did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException("Seed process exceeded 90 seconds.");
        }
        return (process.ExitCode, await stdout + await stderr);
    }

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EventBooking.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Cannot locate repository root.");
    }
}
