using System.Diagnostics;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace EventBooking.Web.E2E;

public sealed class WebHostFixture : IAsyncLifetime
{
    private string? _publish;
    private WebApplication? _app;
    public string BaseUrl { get; private set; } = string.Empty;
    public string AxeScript { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var root = RepositoryRoot();
        _publish = Path.Combine(Path.GetTempPath(), "eventbooking-web-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_publish);
        await RunAsync("dotnet", ["publish", "src/EventBooking.Web/EventBooking.Web.csproj", "-c", "Release", "-o", _publish, "-p:EventBookingE2E=true"], root);
        await RunAsync("npm", ["ci", "--prefix", "tests/EventBooking.Web.E2E"], root);
        AxeScript = Path.Combine(root, "tests", "EventBooking.Web.E2E", "node_modules", "axe-core", "axe.min.js");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = Path.Combine(_publish, "wwwroot"),
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _app = builder.Build();
        E2EApiStub.Map(_app);
        // Blazor's ICU data ships as .dat, which Kestrel's default content-type map does
        // not serve. This host exists only to serve the published test bundle, so unknown
        // file types are safe to serve as octet-stream.
        _app.UseStaticFiles(new StaticFileOptions { ServeUnknownFileTypes = true });
        _app.MapFallbackToFile("index.html");
        await _app.StartAsync();
        BaseUrl = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null) await _app.DisposeAsync();
        if (_publish is not null && Directory.Exists(_publish)) Directory.Delete(_publish, true);
    }

    private static async Task RunAsync(string file, IReadOnlyList<string> arguments, string directory)
    {
        var start = new ProcessStartInfo(file)
        {
            WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {file}.");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException((await process.StandardError.ReadToEndAsync()) + (await process.StandardOutput.ReadToEndAsync()));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EventBooking.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
