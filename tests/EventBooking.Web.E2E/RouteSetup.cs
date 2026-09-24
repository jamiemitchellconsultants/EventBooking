using Microsoft.Playwright;

namespace EventBooking.Web.E2E;

public static class RouteSetup
{
    private static readonly IReadOnlyDictionary<string, Func<IPage, Task>> Actions =
        new Dictionary<string, Func<IPage, Task>>(StringComparer.Ordinal)
        {
            ["locations-edit"] = page => page.Locator("[data-action='edit']").First.ClickAsync(),
            ["locations-save"] = async page =>
            {
                await page.Locator("[data-action='edit']").First.ClickAsync();
                await page.Locator("input[name='name']").FillAsync("Unsaved London name");
                await page.Locator("[data-action='save']").ClickAsync();
            },
            ["group-confirm"] = async page =>
            {
                await page.Locator("[data-action='edit']").First.ClickAsync();
                await page.Locator("[data-action='save']").ClickAsync();
            },
        };

    public static async Task ApplyAsync(IPage page, string? action)
    {
        if (action is null) return;
        if (!Actions.TryGetValue(action, out var apply))
            throw new InvalidOperationException($"Unknown E2E setup action '{action}'.");
        await apply(page);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
