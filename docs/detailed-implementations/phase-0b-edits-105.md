# 00b — Vocabulary edits 105 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — tests/EventBooking.Mcp.Tests/StaffIdentityMcpTests.cs — 1/1

<!-- vocabulary-file: {"id":360,"oldPath":"tests/EventBooking.Mcp.Tests/StaffIdentityMcpTests.cs","newPath":"tests/EventBooking.Mcp.Tests/StaffIdentityMcpTests.cs","beforeSha":"4f3c7387623185197b6486d30b3e3d8c09e43cab6001674237351f68652555e8","afterSha":"8671f8a6ade813304c4548c33e35d435d92a8e74308ceefec022adb07664bac4","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EventBooking.Mcp.Tests;

/// <summary>Covers identity recording at the authenticated MCP boundary.</summary>
[Collection("mcp")]
public sealed class StaffIdentityMcpTests(McpFactory factory)
{
    /// <summary>A valid first sighting is recorded before an unassigned caller is forbidden.</summary>
    [Fact]
    public async Task ValidFirstSighting_IsRecordedBeforeAuthorization()
    {
        var originalStaffUserId = factory.SignedInAs;
        var originalStaffIdClaim = factory.StaffIdClaim;
        var staffUserId = Guid.NewGuid();
        try
        {
            factory.SignedInAs = staffUserId;
            factory.StaffIdClaim = "u234567";

            var response = await PostToolsListAsync();

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var identity = await factory.FindIdentityAsync(staffUserId);
            Assert.Equal("U234567", identity?.StaffId.Value);
        }
        finally
        {
            factory.SignedInAs = originalStaffUserId;
            factory.StaffIdClaim = originalStaffIdClaim;
        }
    }

    /// <summary>An absent or malformed claim is never recorded.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("not-a-staff-id")]
    public async Task InvalidFirstSighting_IsNotRecorded(string? staffIdClaim)
    {
        var originalStaffUserId = factory.SignedInAs;
        var originalStaffIdClaim = factory.StaffIdClaim;
        var staffUserId = Guid.NewGuid();
        try
        {
            factory.SignedInAs = staffUserId;
            factory.StaffIdClaim = staffIdClaim;

            var response = await PostToolsListAsync();

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Null(await factory.FindIdentityAsync(staffUserId));
        }
        finally
        {
            factory.SignedInAs = originalStaffUserId;
            factory.StaffIdClaim = originalStaffIdClaim;
        }
    }

    private async Task<HttpResponseMessage> PostToolsListAsync()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    id = "1",
                    method = "tools/list",
                }),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return await client.SendAsync(request);
    }
}
`````

## after — tests/EventBooking.Mcp.Tests/StaffIdentityMcpTests.cs — 1/1

<!-- vocabulary-file: {"id":360,"oldPath":"tests/EventBooking.Mcp.Tests/StaffIdentityMcpTests.cs","newPath":"tests/EventBooking.Mcp.Tests/StaffIdentityMcpTests.cs","beforeSha":"4f3c7387623185197b6486d30b3e3d8c09e43cab6001674237351f68652555e8","afterSha":"8671f8a6ade813304c4548c33e35d435d92a8e74308ceefec022adb07664bac4","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EventBooking.Mcp.Tests;

/// <summary>Covers identity recording at the authenticated MCP boundary.</summary>
[Collection("mcp")]
public sealed class StaffIdentityMcpTests(McpFactory factory)
{
    /// <summary>A valid first sighting is recorded before an unassigned caller is forbidden.</summary>
    [Fact]
    public async Task ValidFirstSighting_IsRecordedBeforeAuthorization()
    {
        var originalStaffUserId = factory.SignedInAs;
        var originalStaffIdClaim = factory.StaffIdClaim;
        var originalRolesClaim = factory.RolesClaim;
        var staffUserId = Guid.NewGuid();
        try
        {
            factory.SignedInAs = staffUserId;
            factory.StaffIdClaim = "u234567";
            factory.RolesClaim = [];

            var response = await PostToolsListAsync();

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var identity = await factory.FindIdentityAsync(staffUserId);
            Assert.Equal("U234567", identity?.StaffId.Value);
        }
        finally
        {
            factory.SignedInAs = originalStaffUserId;
            factory.StaffIdClaim = originalStaffIdClaim;
            factory.RolesClaim = originalRolesClaim;
        }
    }

    /// <summary>An absent or malformed claim is never recorded.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("not-a-staff-id")]
    public async Task InvalidFirstSighting_IsNotRecorded(string? staffIdClaim)
    {
        var originalStaffUserId = factory.SignedInAs;
        var originalStaffIdClaim = factory.StaffIdClaim;
        var staffUserId = Guid.NewGuid();
        try
        {
            factory.SignedInAs = staffUserId;
            factory.StaffIdClaim = staffIdClaim;

            var response = await PostToolsListAsync();

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Null(await factory.FindIdentityAsync(staffUserId));
        }
        finally
        {
            factory.SignedInAs = originalStaffUserId;
            factory.StaffIdClaim = originalStaffIdClaim;
        }
    }

    private async Task<HttpResponseMessage> PostToolsListAsync()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    id = "1",
                    method = "tools/list",
                }),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return await client.SendAsync(request);
    }
}
`````

## before — tests/EventBooking.SeedData.Tests/DemoEmailOptionsTests.cs — 1/1

<!-- vocabulary-file: {"id":361,"oldPath":"tests/EventBooking.SeedData.Tests/DemoEmailOptionsTests.cs","newPath":"tests/EventBooking.SeedData.Tests/DemoEmailOptionsTests.cs","beforeSha":"e20af69a6b684713502a73f06debd5fdeab763f326fb3c6f4988c2aaf8dbfa13","afterSha":"48269ffd91f1ae74135aa22b2be4bde20df221452789de52b210ec67ba7a43af","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Tokens;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>Checks configuration needed for demo links and SMTP delivery.</summary>
public sealed class DemoEmailOptionsTests
{
    /// <summary>Default tokens are readable with the local API's configured key.</summary>
    [Fact]
    public void DefaultsMatchLocalApiAndMailpit()
    {
        var options = DemoEmailOptions.From(_ => null);
        var seed = new HmacTokenService(options.Tokens);
        var api = new HmacTokenService(new TokenOptions(
            "a-local-signing-key-that-is-at-least-32-characters"));
        var id = Guid.NewGuid();
        Assert.True(api.TryRead(seed.Issue(id).Token, out var read));
        Assert.Equal(id, read);
        Assert.Equal("http://localhost:5002", options.Portal.BaseUrl);
        Assert.Equal("localhost", options.Smtp.Host);
        Assert.Equal(1025, options.Smtp.Port);
        Assert.Equal(EmailProvider.Smtp, options.Sender.Provider);
        Assert.Equal("Europe/London", options.HeadOffice.TimeZoneId);
    }

    /// <summary>Explicit deployment settings drive usable links and email transport.</summary>
    [Fact]
    public void OverridesUseConfiguredKeyAndNormalizePortal()
    {
        var values = Complete();
        values["Portal__BaseUrl"] = "https://demo.example.test/portal/";
        values["Email__Smtp__Port"] = "2525";
        values["Email__FromAddress"] = "demo@example.com";
        values["Email__FromName"] = "Demo recruitment";
        values["HeadOffice__Address"] = "Demo office";
        values["Portal__CoordinatorContact"] = "help@example.com";
        values["HeadOffice__TimeZoneId"] = "UTC";
        var options = DemoEmailOptions.From(values.GetValueOrDefault);
        var token = new HmacTokenService(options.Tokens).Issue(Guid.NewGuid()).Token;
        var api = new HmacTokenService(new TokenOptions(values["Tokens__SigningKey"]!));
        Assert.True(api.TryRead(token, out _));
        Assert.Equal("https://demo.example.test/portal", options.Portal.BaseUrl);
        Assert.Equal("mailpit", options.Smtp.Host);
        Assert.Equal(2525, options.Smtp.Port);
        Assert.Equal("demo@example.com", options.Sender.FromAddress);
        Assert.Equal("Demo recruitment", options.Sender.FromName);
        Assert.Equal("Demo office", options.Portal.HeadOfficeAddress);
        Assert.Equal("help@example.com", options.Portal.CoordinatorContact);
        Assert.Equal("UTC", options.HeadOffice.TimeZoneId);
        Assert.DoesNotContain(values["Tokens__SigningKey"]!, options.ToString());
    }

    /// <summary>Non-local links never fall back to the local API's development secret or SMTP host.</summary>
    [Theory]
    [InlineData("Tokens__SigningKey")]
    [InlineData("Email__Smtp__Host")]
    public void NonLocalPortalRequiresExplicitSetting(string missing)
    {
        var values = Complete();
        values.Remove(missing);
        var error = Assert.Throws<SeedException>(() =>
            DemoEmailOptions.From(values.GetValueOrDefault));
        Assert.Contains(missing, error.Message);
    }

    /// <summary>Invalid values fail by setting name without exposing the supplied value.</summary>
    [Theory]
    [InlineData("Portal__BaseUrl", "not-a-url")]
    [InlineData("Portal__BaseUrl", "ftp://demo.example.test")]
    [InlineData("Portal__BaseUrl", "https://user:secret@demo.example.test")]
    [InlineData("Portal__BaseUrl", "https://demo.example.test?secret=x")]
    [InlineData("Portal__BaseUrl", "https://demo.example.test#fragment")]
    [InlineData("Tokens__SigningKey", "short-secret")]
    [InlineData("Email__Smtp__Port", "0")]
    [InlineData("Email__Smtp__Port", "65536")]
    [InlineData("Email__Smtp__Port", "invalid-port")]
    [InlineData("Email__Smtp__Host", " ")]
    [InlineData("Email__FromAddress", "not-an-email")]
    [InlineData("HeadOffice__TimeZoneId", "missing/timezone")]
    public void InvalidConfigurationFailsWithoutValueDisclosure(string key, string value)
    {
        var values = Complete();
        values[key] = value;
        var error = Assert.Throws<SeedException>(() =>
            DemoEmailOptions.From(values.GetValueOrDefault));
        Assert.Contains(key, error.Message);
        if (!string.IsNullOrWhiteSpace(value) && value.Length > 1)
            Assert.DoesNotContain(value, error.Message);
    }

    private static Dictionary<string, string?> Complete() => new()
    {
        ["Portal__BaseUrl"] = "https://demo.example.test",
        ["Tokens__SigningKey"] = "a-test-signing-key-with-at-least-32-characters",
        ["Email__Smtp__Host"] = "mailpit",
    };
}
`````

## after — tests/EventBooking.SeedData.Tests/DemoEmailOptionsTests.cs — 1/1

<!-- vocabulary-file: {"id":361,"oldPath":"tests/EventBooking.SeedData.Tests/DemoEmailOptionsTests.cs","newPath":"tests/EventBooking.SeedData.Tests/DemoEmailOptionsTests.cs","beforeSha":"e20af69a6b684713502a73f06debd5fdeab763f326fb3c6f4988c2aaf8dbfa13","afterSha":"48269ffd91f1ae74135aa22b2be4bde20df221452789de52b210ec67ba7a43af","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Tokens;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>Checks configuration needed for demo links and SMTP delivery.</summary>
public sealed class DemoEmailOptionsTests
{
    /// <summary>Default tokens are readable with the local API's configured key.</summary>
    [Fact]
    public void DefaultsMatchLocalApiAndMailpit()
    {
        var options = DemoEmailOptions.From(_ => null);
        var seed = new HmacTokenService(options.Tokens);
        var api = new HmacTokenService(new TokenOptions(
            "a-local-signing-key-that-is-at-least-32-characters"));
        var id = Guid.NewGuid();
        Assert.True(api.TryRead(seed.Issue(id).Token, out var read));
        Assert.Equal(id, read);
        Assert.Equal("http://localhost:5002", options.Portal.BaseUrl);
        Assert.Equal("localhost", options.Smtp.Host);
        Assert.Equal(1025, options.Smtp.Port);
        Assert.Equal(EmailProvider.Smtp, options.Sender.Provider);
        Assert.Equal("Europe/London", options.TransitionalLocation.TimeZoneId);
    }

    /// <summary>Explicit deployment settings drive usable links and email transport.</summary>
    [Fact]
    public void OverridesUseConfiguredKeyAndNormalizePortal()
    {
        var values = Complete();
        values["Portal__BaseUrl"] = "https://demo.example.test/portal/";
        values["Email__Smtp__Port"] = "2525";
        values["Email__FromAddress"] = "demo@example.com";
        values["Email__FromName"] = "Demo recruitment";
        values["TransitionalLocation__Address"] = "Demo office";
        values["Portal__CoordinatorContact"] = "help@example.com";
        values["TransitionalLocation__TimeZoneId"] = "UTC";
        var options = DemoEmailOptions.From(values.GetValueOrDefault);
        var token = new HmacTokenService(options.Tokens).Issue(Guid.NewGuid()).Token;
        var api = new HmacTokenService(new TokenOptions(values["Tokens__SigningKey"]!));
        Assert.True(api.TryRead(token, out _));
        Assert.Equal("https://demo.example.test/portal", options.Portal.BaseUrl);
        Assert.Equal("mailpit", options.Smtp.Host);
        Assert.Equal(2525, options.Smtp.Port);
        Assert.Equal("demo@example.com", options.Sender.FromAddress);
        Assert.Equal("Demo recruitment", options.Sender.FromName);
        Assert.Equal("Demo office", options.Portal.TransitionalLocationAddress);
        Assert.Equal("help@example.com", options.Portal.CoordinatorContact);
        Assert.Equal("UTC", options.TransitionalLocation.TimeZoneId);
        Assert.DoesNotContain(values["Tokens__SigningKey"]!, options.ToString());
    }

    /// <summary>Non-local links never fall back to the local API's development secret or SMTP host.</summary>
    [Theory]
    [InlineData("Tokens__SigningKey")]
    [InlineData("Email__Smtp__Host")]
    public void NonLocalPortalRequiresExplicitSetting(string missing)
    {
        var values = Complete();
        values.Remove(missing);
        var error = Assert.Throws<SeedException>(() =>
            DemoEmailOptions.From(values.GetValueOrDefault));
        Assert.Contains(missing, error.Message);
    }

    /// <summary>Invalid values fail by setting name without exposing the supplied value.</summary>
    [Theory]
    [InlineData("Portal__BaseUrl", "not-a-url")]
    [InlineData("Portal__BaseUrl", "ftp://demo.example.test")]
    [InlineData("Portal__BaseUrl", "https://user:secret@demo.example.test")]
    [InlineData("Portal__BaseUrl", "https://demo.example.test?secret=x")]
    [InlineData("Portal__BaseUrl", "https://demo.example.test#fragment")]
    [InlineData("Tokens__SigningKey", "short-secret")]
    [InlineData("Email__Smtp__Port", "0")]
    [InlineData("Email__Smtp__Port", "65536")]
    [InlineData("Email__Smtp__Port", "invalid-port")]
    [InlineData("Email__Smtp__Host", " ")]
    [InlineData("Email__FromAddress", "not-an-email")]
    [InlineData("TransitionalLocation__TimeZoneId", "missing/timezone")]
    public void InvalidConfigurationFailsWithoutValueDisclosure(string key, string value)
    {
        var values = Complete();
        values[key] = value;
        var error = Assert.Throws<SeedException>(() =>
            DemoEmailOptions.From(values.GetValueOrDefault));
        Assert.Contains(key, error.Message);
        if (!string.IsNullOrWhiteSpace(value) && value.Length > 1)
            Assert.DoesNotContain(value, error.Message);
    }

    private static Dictionary<string, string?> Complete() => new()
    {
        ["Portal__BaseUrl"] = "https://demo.example.test",
        ["Tokens__SigningKey"] = "a-test-signing-key-with-at-least-32-characters",
        ["Email__Smtp__Host"] = "mailpit",
    };
}
`````

## before — tests/EventBooking.SeedData.Tests/DemoInvitationHostTests.cs — 1/1

<!-- vocabulary-file: {"id":362,"oldPath":"tests/EventBooking.SeedData.Tests/DemoInvitationHostTests.cs","newPath":"tests/EventBooking.SeedData.Tests/DemoInvitationHostTests.cs","beforeSha":"91200f314edcdc05573cb4404522f8eeb77a4abf534a7e7f1ff7086abc284404","afterSha":"eeb23a440bf5566f6fd5c014438d4e9c031e92f334f22fa2ebaca195314db776","side":"before","part":1,"parts":1} -->

`````csharp
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

    /// <summary>Migration-only bypasses even invalid email settings and creates no demo Candidates.</summary>
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
        Assert.Equal(0, await database.Candidates.CountAsync());
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
            new HeadOfficeOptions("Europe/London"), new TokenOptions(SigningKey));
        services.AddEventBookingApplication(new CandidatePortalOptions(
            "https://host-demo.example.test", "Demo office", "help@example.com"));
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
            || key.StartsWith("HeadOffice__", StringComparison.OrdinalIgnoreCase)).ToArray())
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
`````

## after — tests/EventBooking.SeedData.Tests/DemoInvitationHostTests.cs — 1/1

<!-- vocabulary-file: {"id":362,"oldPath":"tests/EventBooking.SeedData.Tests/DemoInvitationHostTests.cs","newPath":"tests/EventBooking.SeedData.Tests/DemoInvitationHostTests.cs","beforeSha":"91200f314edcdc05573cb4404522f8eeb77a4abf534a7e7f1ff7086abc284404","afterSha":"eeb23a440bf5566f6fd5c014438d4e9c031e92f334f22fa2ebaca195314db776","side":"after","part":1,"parts":1} -->

`````csharp
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
            new TransitionalLocationOptions("Europe/London"), new TokenOptions(SigningKey));
        services.AddEventBookingApplication(new AttendeePortalOptions(
            "https://host-demo.example.test", "Demo office", "help@example.com"));
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
            || key.StartsWith("TransitionalLocation__", StringComparison.OrdinalIgnoreCase)).ToArray())
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
`````

## before — tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs — 1/1

<!-- vocabulary-file: {"id":363,"oldPath":"tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs","newPath":"tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs","beforeSha":"db1a4cd08679cf3af5b6250b3227d13aaefeb786b0a003b589175e80e7707256","afterSha":"f98030ff70cc26d1cccaa8d8f2e5651d80413638f547f9231368e326de6c64d7","side":"before","part":1,"parts":1} -->

`````csharp
using System.Text.RegularExpressions;
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;
using EventBooking.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EventBooking.SeedData.Tests;

/// <summary>Exercises real seeded links, delivery recovery and candidate lifecycle preservation.</summary>
[Collection("seed-anchor")]
public sealed class DemoInvitationSeederTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly CapturingTransport _mail = new();
    private readonly DemoClock _clock = new();
    private ServiceProvider _services = null!;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        DemoSeedSpec.OverrideAnchor(_clock.TodayAtHeadOffice);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBookingInfrastructure(_postgres.GetConnectionString(),
            new HeadOfficeOptions("Europe/London"),
            new TokenOptions("test-seed-and-api-share-this-signing-key"));
        services.AddEventBookingApplication(new CandidatePortalOptions(
            "https://demo.example.test", "Demo office", "help@example.com"));
        services.AddSingleton<IClock>(_clock);
        services.AddSingleton<IEmailTransport>(_mail);
        services.AddScoped<DemoSeeder>();
        services.AddScoped<DemoInvitationSeeder>();
        _services = services.BuildServiceProvider();
        using var scope = _services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<EventBookingDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().RunAsync(default);
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        DemoSeedSpec.OverrideAnchor(null);
        if (_services is not null) await _services.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>All five delivered HTML/text links open future options; one can book and be managed.</summary>
    [Fact]
    public async Task FreshSeedProducesFiveUsableLinksAndARealBooking()
    {
        Assert.Equal(5, await SeedAsync());
        Assert.Equal(5, _mail.Messages.Count);
        Assert.Equal(Enumerable.Range(1, 5).Select(i => $"demo-candidate-{i:000}@example.com"),
            _mail.Messages.Select(m => m.ToAddress));
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.Equal(100, await db.Candidates.CountAsync());
        Assert.Equal(5, await db.Candidates.CountAsync(c => c.Status == CandidateStatus.Invited));
        // Journey seeds write booking rows directly without transitioning Candidate
        // status, so only the 5 newly invited candidates leave NotYetInvited.
        Assert.Equal(95, await db.Candidates.CountAsync(c => c.Status == CandidateStatus.NotYetInvited));
        Assert.Equal(5, await db.Invites.CountAsync(i => i.Status == InviteStatus.Pending));
        Assert.Equal(5, await db.EmailLogs.CountAsync(e => e.Status == EmailStatus.Sent));
        Assert.Equal(12, await db.ConfirmedSlots.CountAsync());
        Assert.Equal(5, await db.SlotProposals.CountAsync());
        Assert.Equal(5, await db.Candidates.Where(c => c.Status == CandidateStatus.Invited)
            .Select(c => c.EmployeeGroupId).Distinct().CountAsync());
        var view = scope.ServiceProvider.GetRequiredService<ViewInviteHandler>();
        foreach (var message in _mail.Messages.ToArray())
        {
            var token = Token(message);
            Assert.Contains($"href=\"https://demo.example.test/book/{token}\"", message.HtmlBody);
            var result = await view.HandleAsync(new ViewInviteQuery(token), default);
            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value.Options.Count);
            Assert.All(result.Value.Options, option => Assert.True(option.Date > _clock.TodayAtHeadOffice));
            var candidate = await db.Candidates.Include(c => c.Requirements)
                .SingleAsync(c => c.Id == message.CandidateId);
            var invite = await db.Invites.Include(i => i.Requirements)
                .SingleAsync(i => i.Id == result.Value.InviteId);
            Assert.Equal(candidate.RequiredAppointmentTypeIds.Order(), invite.RequiredAppointmentTypeIds.Order());
            Assert.NotEqual(token, invite.TokenHash);
        }
        var first = Token(_mail.Messages[0]);
        var offered = await view.HandleAsync(new ViewInviteQuery(first), default);
        var booking = await scope.ServiceProvider.GetRequiredService<ConfirmBookingHandler>()
            .HandleAsync(new ConfirmBookingCommand(first, offered.Value.Options[0].ConfirmedSlotId), default);
        Assert.True(booking.IsSuccess);
        Assert.Equal("Sent", booking.Value.DeliveryStatus);
        Assert.True((await scope.ServiceProvider.GetRequiredService<ViewBookingHandler>()
            .HandleAsync(new ViewBookingQuery(booking.Value.ManageToken), default)).IsSuccess);
        Assert.False((await view.HandleAsync(new ViewInviteQuery(first), default)).IsSuccess);
        Assert.Equal(EmailTemplate.BookingConfirmation, _mail.Messages[^1].Template);
    }

    /// <summary>A rerun preserves successful tokens, capacities and an already-consumed invitation.</summary>
    [Fact]
    public async Task RerunDoesNotResendOrUndoBooking()
    {
        await SeedAsync();
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var first = Token(_mail.Messages[0]);
        var offered = await scope.ServiceProvider.GetRequiredService<ViewInviteHandler>()
            .HandleAsync(new ViewInviteQuery(first), default);
        var booked = await scope.ServiceProvider.GetRequiredService<ConfirmBookingHandler>()
            .HandleAsync(new ConfirmBookingCommand(first, offered.Value.Options[0].ConfirmedSlotId), default);
        Assert.True(booked.IsSuccess);
        var hashes = await db.Invites.AsNoTracking().OrderBy(i => i.Id).Select(i => i.TokenHash).ToListAsync();
        var capacities = await db.ConfirmedSlots.AsNoTracking().Include(s => s.Capacities)
            .OrderBy(s => s.Id).ToListAsync();
        var before = capacities.SelectMany(s => s.Capacities.OrderBy(c => c.AppointmentTypeId))
            .Select(c => c.RemainingCapacity).ToArray();
        Assert.Equal(0, await SeedAsync());
        Assert.Equal(6, _mail.Messages.Count);
        Assert.Equal(hashes, await db.Invites.AsNoTracking().OrderBy(i => i.Id).Select(i => i.TokenHash).ToListAsync());
        var after = await db.ConfirmedSlots.AsNoTracking().Include(s => s.Capacities)
            .OrderBy(s => s.Id).ToListAsync();
        Assert.Equal(before, after.SelectMany(s => s.Capacities.OrderBy(c => c.AppointmentTypeId))
            .Select(c => c.RemainingCapacity).ToArray());
    }

    /// <summary>Provider failure leaves a durable attempt that the next run retries once.</summary>
    [Fact]
    public async Task FailedDeliveryResumesWithoutDuplicatingSuccesses()
    {
        _mail.FailOnAttempt = 3;
        await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var failed = await db.EmailLogs.AsNoTracking().SingleAsync(e => e.Status == EmailStatus.Failed);
        var oldHash = await db.Invites.Where(i => i.Id == failed.InviteId).Select(i => i.TokenHash).SingleAsync();
        Assert.Equal(2, _mail.Messages.Count);
        _mail.FailOnAttempt = null;
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(3, await SeedAsync());
        Assert.Equal(5, _mail.Messages.Count);
        Assert.Equal(5, _mail.Messages.Select(m => m.CandidateId).Distinct().Count());
        Assert.Equal(5, await db.Invites.CountAsync(i => i.Status == InviteStatus.Pending));
        Assert.Equal(1, await db.EmailLogs.CountAsync(e => e.Status == EmailStatus.Resolved));
        Assert.Equal(5, await db.EmailLogs.CountAsync(e => e.Status == EmailStatus.Sent));
        Assert.NotEqual(oldHash, await db.Invites.Where(i => i.Id == failed.InviteId)
            .Select(i => i.TokenHash).SingleAsync());
        Assert.Equal(0, await SeedAsync());
    }

    /// <summary>Resetting database state creates new invitations and invalidates old raw links.</summary>
    [Fact]
    public async Task ReseedCreatesFreshLinks()
    {
        await SeedAsync();
        var oldToken = Token(_mail.Messages[0]);
        using (var scope = _services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<DemoSeeder>().ReseedAsync(default);
        Assert.Equal(5, await SeedAsync());
        using var verify = _services.CreateScope();
        var view = verify.ServiceProvider.GetRequiredService<ViewInviteHandler>();
        Assert.False((await view.HandleAsync(new ViewInviteQuery(oldToken), default)).IsSuccess);
        Assert.True((await view.HandleAsync(new ViewInviteQuery(Token(_mail.Messages[5])), default)).IsSuccess);
    }

    /// <summary>Expired history is not replaced even when demo dates are moved forward.</summary>
    [Fact]
    public async Task ExpiredInvitationsArePreserved()
    {
        await SeedAsync();
        _clock.Advance(TimeSpan.FromDays(30));
        DemoSeedSpec.OverrideAnchor(_clock.TodayAtHeadOffice);
        Assert.Equal(0, await SeedAsync());
        Assert.Equal(5, _mail.Messages.Count);
        using var scope = _services.CreateScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<ViewInviteHandler>()
            .HandleAsync(new ViewInviteQuery(Token(_mail.Messages[0])), default)).IsSuccess);
    }

    /// <summary>Stale windows fail before issuing a misleading invitation.</summary>
    [Fact]
    public async Task StaleAnchorFailsBeforeSending()
    {
        _clock.Advance(TimeSpan.FromDays(4));
        var error = await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        Assert.Contains("--reanchor", error.Message);
        Assert.Empty(_mail.Messages);
    }

    /// <summary>A live claim is not stolen, but an expired claim can be recovered on a later run.</summary>
    [Fact]
    public async Task PendingClaimUsesExistingLease()
    {
        await SeedAsync();
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var prior = await db.EmailLogs.SingleAsync(e => e.CandidateId == _mail.Messages[0].CandidateId);
            var pending = EmailLog.RecordPending(Guid.NewGuid(), prior.CandidateId,
                EmailTemplate.CandidateInvite, _clock.UtcNow.AddSeconds(1), prior.InviteId);
            Assert.True(pending.TryClaim(_clock.UtcNow, TimeSpan.FromMinutes(5)));
            db.EmailLogs.Add(pending);
            await db.SaveChangesAsync();
        }
        var error = await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        Assert.Contains("already being delivered", error.Message);
        Assert.Equal(5, _mail.Messages.Count);
        _clock.Advance(TimeSpan.FromMinutes(6));
        Assert.Equal(1, await SeedAsync());
        Assert.Equal(6, _mail.Messages.Count);
    }

    /// <summary>A pending invitation lacking its delivery record is not reported as sent.</summary>
    [Fact]
    public async Task MissingDeliveryFailsVisibly()
    {
        await SeedAsync();
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            await db.EmailLogs.Where(e => e.CandidateId == _mail.Messages[0].CandidateId).ExecuteDeleteAsync();
        }
        var error = await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        Assert.Contains("no matching delivery", error.Message);
        Assert.Equal(5, _mail.Messages.Count);
    }

    /// <summary>An old failure cannot hide a successful Coordinator replacement for the same Candidate.</summary>
    [Fact]
    public async Task CoordinatorReplacementPreservesItsSuccessfulDelivery()
    {
        _mail.FailOnAttempt = 1;
        await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        _mail.FailOnAttempt = null;
        _clock.Advance(TimeSpan.FromSeconds(1));
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var candidate = await db.Candidates.SingleAsync(c => c.Email == "demo-candidate-001@example.com");
            var issued = await scope.ServiceProvider
                .GetRequiredService<EventBooking.Application.Invites.TriggerInviteHandler>()
                .HandleAsync(new EventBooking.Application.Invites.TriggerInviteCommand(
                    DemoSeedSpec.CoordinatorUserId(), candidate.Id), default);
            Assert.True(issued.IsSuccess);
            Assert.True(issued.Value.EmailSent);
        }
        var replacementToken = Token(Assert.Single(_mail.Messages));
        Assert.Equal(4, await SeedAsync());
        Assert.Equal(5, _mail.Messages.Count);
        using var verify = _services.CreateScope();
        Assert.True((await verify.ServiceProvider.GetRequiredService<ViewInviteHandler>()
            .HandleAsync(new ViewInviteQuery(replacementToken), default)).IsSuccess);
        Assert.Equal(0, await SeedAsync());
    }

    /// <summary>Unrelated outstanding work is not retried or token-rotated by demo seeding.</summary>
    [Fact]
    public async Task AnotherOutstandingTemplateRequiresCoordinatorReview()
    {
        _mail.FailOnAttempt = 1;
        await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        _mail.FailOnAttempt = null;
        string originalHash;
        Guid inviteId;
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var prior = await db.EmailLogs.SingleAsync(e => e.Status == EmailStatus.Failed);
            inviteId = prior.InviteId!.Value;
            originalHash = await db.Invites.Where(i => i.Id == inviteId).Select(i => i.TokenHash).SingleAsync();
            var other = EmailLog.RecordPending(Guid.NewGuid(), prior.CandidateId,
                EmailTemplate.CandidateReinvite, _clock.UtcNow.AddSeconds(1), inviteId);
            db.EmailLogs.Add(other);
            await db.SaveChangesAsync();
        }
        var error = await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        Assert.Contains("Another outstanding delivery", error.Message);
        Assert.Empty(_mail.Messages);
        using var verify = _services.CreateScope();
        var database = verify.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.Equal(originalHash, await database.Invites.Where(i => i.Id == inviteId)
            .Select(i => i.TokenHash).SingleAsync());
    }

    private async Task<int> SeedAsync()
    {
        using var scope = _services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DemoInvitationSeeder>().RunAsync(default);
    }

    private static string Token(EmailMessage message) =>
        Regex.Match(message.TextBody, @"https://demo\.example\.test/book/([^\s]+)").Groups[1].Value;

    private sealed class CapturingTransport : IEmailTransport
    {
        /// <summary>Messages accepted by the external provider boundary.</summary>
        public List<EmailMessage> Messages { get; } = [];
        /// <summary>Optional one-based provider attempt to fail.</summary>
        public int? FailOnAttempt { get; set; }
        private int _attempt;
        /// <inheritdoc/>
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (++_attempt == FailOnAttempt) throw new IOException("Test SMTP failure.");
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class DemoClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow { get; private set; } = new(2030, 1, 7, 12, 0, 0, TimeSpan.Zero);
        /// <inheritdoc/>
        public DateTimeOffset NowAtHeadOffice => TimeZoneInfo.ConvertTime(UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/London"));
        /// <inheritdoc/>
        public DateOnly TodayAtHeadOffice => DateAtHeadOffice(UtcNow);
        /// <inheritdoc/>
        public DateOnly DateAtHeadOffice(DateTimeOffset instant) => DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")).DateTime);
        /// <inheritdoc/>
        public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(
            instant, TimeZoneInfo.FindSystemTimeZoneById("Europe/London"));
        /// <summary>Moves the observation clock without modifying persisted data.</summary>
        public void Advance(TimeSpan elapsed) => UtcNow += elapsed;
    }
}
`````

## after — tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs — 1/1

<!-- vocabulary-file: {"id":363,"oldPath":"tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs","newPath":"tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs","beforeSha":"db1a4cd08679cf3af5b6250b3227d13aaefeb786b0a003b589175e80e7707256","afterSha":"f98030ff70cc26d1cccaa8d8f2e5651d80413638f547f9231368e326de6c64d7","side":"after","part":1,"parts":1} -->

`````csharp
using System.Text.RegularExpressions;
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;
using EventBooking.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EventBooking.SeedData.Tests;

/// <summary>Exercises real seeded links, delivery recovery and attendee lifecycle preservation.</summary>
[Collection("seed-anchor")]
public sealed class DemoInvitationSeederTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly CapturingTransport _mail = new();
    private readonly DemoClock _clock = new();
    private ServiceProvider _services = null!;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        DemoSeedSpec.OverrideAnchor(_clock.TodayAtTransitionalLocation);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBookingInfrastructure(_postgres.GetConnectionString(),
            new TransitionalLocationOptions("Europe/London"),
            new TokenOptions("test-seed-and-api-share-this-signing-key"));
        services.AddEventBookingApplication(new AttendeePortalOptions(
            "https://demo.example.test", "Demo office", "help@example.com"));
        services.AddSingleton<IClock>(_clock);
        services.AddSingleton<IEmailTransport>(_mail);
        services.AddScoped<DemoSeeder>();
        services.AddScoped<DemoInvitationSeeder>();
        _services = services.BuildServiceProvider();
        using var scope = _services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<EventBookingDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().RunAsync(default);
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        DemoSeedSpec.OverrideAnchor(null);
        if (_services is not null) await _services.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>All five delivered HTML/text links open future options; one can book and be managed.</summary>
    [Fact]
    public async Task FreshSeedProducesFiveUsableLinksAndARealBooking()
    {
        Assert.Equal(5, await SeedAsync());
        Assert.Equal(5, _mail.Messages.Count);
        Assert.Equal(Enumerable.Range(1, 5).Select(i => $"demo-attendee-{i:000}@example.com"),
            _mail.Messages.Select(m => m.ToAddress));
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.Equal(100, await db.Attendees.CountAsync());
        Assert.Equal(5, await db.Attendees.CountAsync(c => c.Status == AttendeeStatus.Invited));
        // Journey seeds write booking rows directly without transitioning Attendee
        // status, so only the 5 newly invited attendees leave NotYetInvited.
        Assert.Equal(95, await db.Attendees.CountAsync(c => c.Status == AttendeeStatus.NotYetInvited));
        Assert.Equal(5, await db.Invites.CountAsync(i => i.Status == InviteStatus.Pending));
        Assert.Equal(5, await db.EmailLogs.CountAsync(e => e.Status == EmailStatus.Sent));
        Assert.Equal(12, await db.Events.CountAsync());
        Assert.Equal(5, await db.EventProposals.CountAsync());
        Assert.Equal(5, await db.Attendees.Where(c => c.Status == AttendeeStatus.Invited)
            .Select(c => c.AttendeeGroupId).Distinct().CountAsync());
        var view = scope.ServiceProvider.GetRequiredService<ViewInviteHandler>();
        foreach (var message in _mail.Messages.ToArray())
        {
            var token = Token(message);
            Assert.Contains($"href=\"https://demo.example.test/book/{token}\"", message.HtmlBody);
            var result = await view.HandleAsync(new ViewInviteQuery(token), default);
            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value.Options.Count);
            Assert.All(result.Value.Options, option => Assert.True(option.Date > _clock.TodayAtTransitionalLocation));
            var attendee = await db.Attendees.Include(c => c.Requirements)
                .SingleAsync(c => c.Id == message.AttendeeId);
            var invite = await db.Invites.Include(i => i.Requirements)
                .SingleAsync(i => i.Id == result.Value.InviteId);
            Assert.Equal(attendee.RequiredAppointmentTypeIds.Order(), invite.RequiredAppointmentTypeIds.Order());
            Assert.NotEqual(token, invite.TokenHash);
        }
        var first = Token(_mail.Messages[0]);
        var offered = await view.HandleAsync(new ViewInviteQuery(first), default);
        var booking = await scope.ServiceProvider.GetRequiredService<ConfirmBookingHandler>()
            .HandleAsync(new ConfirmBookingCommand(first, offered.Value.Options[0].EventId), default);
        Assert.True(booking.IsSuccess);
        Assert.Equal("Sent", booking.Value.DeliveryStatus);
        Assert.True((await scope.ServiceProvider.GetRequiredService<ViewBookingHandler>()
            .HandleAsync(new ViewBookingQuery(booking.Value.ManageToken), default)).IsSuccess);
        Assert.False((await view.HandleAsync(new ViewInviteQuery(first), default)).IsSuccess);
        Assert.Equal(EmailTemplate.BookingConfirmation, _mail.Messages[^1].Template);
    }

    /// <summary>A rerun preserves successful tokens, capacities and an already-consumed invitation.</summary>
    [Fact]
    public async Task RerunDoesNotResendOrUndoBooking()
    {
        await SeedAsync();
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var first = Token(_mail.Messages[0]);
        var offered = await scope.ServiceProvider.GetRequiredService<ViewInviteHandler>()
            .HandleAsync(new ViewInviteQuery(first), default);
        var booked = await scope.ServiceProvider.GetRequiredService<ConfirmBookingHandler>()
            .HandleAsync(new ConfirmBookingCommand(first, offered.Value.Options[0].EventId), default);
        Assert.True(booked.IsSuccess);
        var hashes = await db.Invites.AsNoTracking().OrderBy(i => i.Id).Select(i => i.TokenHash).ToListAsync();
        var capacities = await db.Events.AsNoTracking().Include(s => s.Capacities)
            .OrderBy(s => s.Id).ToListAsync();
        var before = capacities.SelectMany(s => s.Capacities.OrderBy(c => c.AppointmentTypeId))
            .Select(c => c.RemainingCapacity).ToArray();
        Assert.Equal(0, await SeedAsync());
        Assert.Equal(6, _mail.Messages.Count);
        Assert.Equal(hashes, await db.Invites.AsNoTracking().OrderBy(i => i.Id).Select(i => i.TokenHash).ToListAsync());
        var after = await db.Events.AsNoTracking().Include(s => s.Capacities)
            .OrderBy(s => s.Id).ToListAsync();
        Assert.Equal(before, after.SelectMany(s => s.Capacities.OrderBy(c => c.AppointmentTypeId))
            .Select(c => c.RemainingCapacity).ToArray());
    }

    /// <summary>Provider failure leaves a durable attempt that the next run retries once.</summary>
    [Fact]
    public async Task FailedDeliveryResumesWithoutDuplicatingSuccesses()
    {
        _mail.FailOnAttempt = 3;
        await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var failed = await db.EmailLogs.AsNoTracking().SingleAsync(e => e.Status == EmailStatus.Failed);
        var oldHash = await db.Invites.Where(i => i.Id == failed.InviteId).Select(i => i.TokenHash).SingleAsync();
        Assert.Equal(2, _mail.Messages.Count);
        _mail.FailOnAttempt = null;
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(3, await SeedAsync());
        Assert.Equal(5, _mail.Messages.Count);
        Assert.Equal(5, _mail.Messages.Select(m => m.AttendeeId).Distinct().Count());
        Assert.Equal(5, await db.Invites.CountAsync(i => i.Status == InviteStatus.Pending));
        Assert.Equal(1, await db.EmailLogs.CountAsync(e => e.Status == EmailStatus.Resolved));
        Assert.Equal(5, await db.EmailLogs.CountAsync(e => e.Status == EmailStatus.Sent));
        Assert.NotEqual(oldHash, await db.Invites.Where(i => i.Id == failed.InviteId)
            .Select(i => i.TokenHash).SingleAsync());
        Assert.Equal(0, await SeedAsync());
    }

    /// <summary>Resetting database state creates new invitations and invalidates old raw links.</summary>
    [Fact]
    public async Task ReseedCreatesFreshLinks()
    {
        await SeedAsync();
        var oldToken = Token(_mail.Messages[0]);
        using (var scope = _services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<DemoSeeder>().ReseedAsync(default);
        Assert.Equal(5, await SeedAsync());
        using var verify = _services.CreateScope();
        var view = verify.ServiceProvider.GetRequiredService<ViewInviteHandler>();
        Assert.False((await view.HandleAsync(new ViewInviteQuery(oldToken), default)).IsSuccess);
        Assert.True((await view.HandleAsync(new ViewInviteQuery(Token(_mail.Messages[5])), default)).IsSuccess);
    }

    /// <summary>Expired history is not replaced even when demo dates are moved forward.</summary>
    [Fact]
    public async Task ExpiredInvitationsArePreserved()
    {
        await SeedAsync();
        _clock.Advance(TimeSpan.FromDays(30));
        DemoSeedSpec.OverrideAnchor(_clock.TodayAtTransitionalLocation);
        Assert.Equal(0, await SeedAsync());
        Assert.Equal(5, _mail.Messages.Count);
        using var scope = _services.CreateScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<ViewInviteHandler>()
            .HandleAsync(new ViewInviteQuery(Token(_mail.Messages[0])), default)).IsSuccess);
    }

    /// <summary>Stale windows fail before issuing a misleading invitation.</summary>
    [Fact]
    public async Task StaleAnchorFailsBeforeSending()
    {
        _clock.Advance(TimeSpan.FromDays(4));
        var error = await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        Assert.Contains("--reanchor", error.Message);
        Assert.Empty(_mail.Messages);
    }

    /// <summary>A live claim is not stolen, but an expired claim can be recovered on a later run.</summary>
    [Fact]
    public async Task PendingClaimUsesExistingLease()
    {
        await SeedAsync();
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var prior = await db.EmailLogs.SingleAsync(e => e.AttendeeId == _mail.Messages[0].AttendeeId);
            var pending = EmailLog.RecordPending(Guid.NewGuid(), prior.AttendeeId,
                EmailTemplate.AttendeeInvite, _clock.UtcNow.AddSeconds(1), prior.InviteId);
            Assert.True(pending.TryClaim(_clock.UtcNow, TimeSpan.FromMinutes(5)));
            db.EmailLogs.Add(pending);
            await db.SaveChangesAsync();
        }
        var error = await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        Assert.Contains("already being delivered", error.Message);
        Assert.Equal(5, _mail.Messages.Count);
        _clock.Advance(TimeSpan.FromMinutes(6));
        Assert.Equal(1, await SeedAsync());
        Assert.Equal(6, _mail.Messages.Count);
    }

    /// <summary>A pending invitation lacking its delivery record is not reported as sent.</summary>
    [Fact]
    public async Task MissingDeliveryFailsVisibly()
    {
        await SeedAsync();
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            await db.EmailLogs.Where(e => e.AttendeeId == _mail.Messages[0].AttendeeId).ExecuteDeleteAsync();
        }
        var error = await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        Assert.Contains("no matching delivery", error.Message);
        Assert.Equal(5, _mail.Messages.Count);
    }

    /// <summary>An old failure cannot hide a successful Coordinator replacement for the same Attendee.</summary>
    [Fact]
    public async Task CoordinatorReplacementPreservesItsSuccessfulDelivery()
    {
        _mail.FailOnAttempt = 1;
        await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        _mail.FailOnAttempt = null;
        _clock.Advance(TimeSpan.FromSeconds(1));
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var attendee = await db.Attendees.SingleAsync(c => c.Email == "demo-attendee-001@example.com");
            var issued = await scope.ServiceProvider
                .GetRequiredService<EventBooking.Application.Invites.TriggerInviteHandler>()
                .HandleAsync(new EventBooking.Application.Invites.TriggerInviteCommand(
                    DemoSeedSpec.CoordinatorUserId(), attendee.Id), default);
            Assert.True(issued.IsSuccess);
            Assert.True(issued.Value.EmailSent);
        }
        var replacementToken = Token(Assert.Single(_mail.Messages));
        Assert.Equal(4, await SeedAsync());
        Assert.Equal(5, _mail.Messages.Count);
        using var verify = _services.CreateScope();
        Assert.True((await verify.ServiceProvider.GetRequiredService<ViewInviteHandler>()
            .HandleAsync(new ViewInviteQuery(replacementToken), default)).IsSuccess);
        Assert.Equal(0, await SeedAsync());
    }

    /// <summary>Unrelated outstanding work is not retried or token-rotated by demo seeding.</summary>
    [Fact]
    public async Task AnotherOutstandingTemplateRequiresCoordinatorReview()
    {
        _mail.FailOnAttempt = 1;
        await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        _mail.FailOnAttempt = null;
        string originalHash;
        Guid inviteId;
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var prior = await db.EmailLogs.SingleAsync(e => e.Status == EmailStatus.Failed);
            inviteId = prior.InviteId!.Value;
            originalHash = await db.Invites.Where(i => i.Id == inviteId).Select(i => i.TokenHash).SingleAsync();
            var other = EmailLog.RecordPending(Guid.NewGuid(), prior.AttendeeId,
                EmailTemplate.AttendeeReinvite, _clock.UtcNow.AddSeconds(1), inviteId);
            db.EmailLogs.Add(other);
            await db.SaveChangesAsync();
        }
        var error = await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        Assert.Contains("Another outstanding delivery", error.Message);
        Assert.Empty(_mail.Messages);
        using var verify = _services.CreateScope();
        var database = verify.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.Equal(originalHash, await database.Invites.Where(i => i.Id == inviteId)
            .Select(i => i.TokenHash).SingleAsync());
    }

    private async Task<int> SeedAsync()
    {
        using var scope = _services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DemoInvitationSeeder>().RunAsync(default);
    }

    private static string Token(EmailMessage message) =>
        Regex.Match(message.TextBody, @"https://demo\.example\.test/book/([^\s]+)").Groups[1].Value;

    private sealed class CapturingTransport : IEmailTransport
    {
        /// <summary>Messages accepted by the external provider boundary.</summary>
        public List<EmailMessage> Messages { get; } = [];
        /// <summary>Optional one-based provider attempt to fail.</summary>
        public int? FailOnAttempt { get; set; }
        private int _attempt;
        /// <inheritdoc/>
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (++_attempt == FailOnAttempt) throw new IOException("Test SMTP failure.");
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class DemoClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow { get; private set; } = new(2030, 1, 7, 12, 0, 0, TimeSpan.Zero);
        /// <inheritdoc/>
        public DateTimeOffset NowAtTransitionalLocation => TimeZoneInfo.ConvertTime(UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/London"));
        /// <inheritdoc/>
        public DateOnly TodayAtTransitionalLocation => DateAtTransitionalLocation(UtcNow);
        /// <inheritdoc/>
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")).DateTime);
        /// <inheritdoc/>
        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(
            instant, TimeZoneInfo.FindSystemTimeZoneById("Europe/London"));
        /// <summary>Moves the observation clock without modifying persisted data.</summary>
        public void Advance(TimeSpan elapsed) => UtcNow += elapsed;
    }
}
`````
