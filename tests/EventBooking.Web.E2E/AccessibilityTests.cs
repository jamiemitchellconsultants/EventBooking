using Microsoft.Playwright;

namespace EventBooking.Web.E2E;

public sealed class AccessibilityTests : IClassFixture<WebHostFixture>, IAsyncLifetime
{
    private readonly WebHostFixture _host;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    public AccessibilityTests(WebHostFixture host) => _host = host;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
    }

    public static TheoryData<string, string, string, string?, int, int> Routes()
    {
        var data = new TheoryData<string, string, string, string?, int, int>();
        foreach (var route in RouteManifest.All)
        {
            data.Add(route.Name, route.Path, route.FixtureState, route.SetupAction, 390, 844);
            data.Add(route.Name, route.Path, route.FixtureState, route.SetupAction, 1440, 900);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task RouteHasNoAxeViolations(
        string name, string path, string fixtureState, string? setupAction, int width, int height)
    {
        await using var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { ViewportSize = new ViewportSize { Width = width, Height = height } });
        var host = new Uri(_host.BaseUrl);
        await context.AddCookiesAsync([new Cookie
        {
            Name = E2EApiStub.StateCookie, Value = fixtureState,
            Domain = host.Host, Path = "/", HttpOnly = true, SameSite = SameSiteAttribute.Lax,
        }]);
        var page = await context.NewPageAsync();
        await page.GotoAsync(_host.BaseUrl + path, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator("#app").WaitForAsync();
        // #app exists in the static shell; .app-shell exists only after Blazor boots and
        // renders, so without this wait axe would scan the loading splash and the gate
        // would pass on an empty page.
        await page.Locator(".app-shell").WaitForAsync();
        await RouteSetup.ApplyAsync(page, setupAction);
        var violations = await AxeRunner.ViolationsAsync(page, _host.AxeScript);
        Assert.True(violations.Count == 0, $"{name} at {width}x{height}: {string.Join(Environment.NewLine, violations)}");
    }
}
