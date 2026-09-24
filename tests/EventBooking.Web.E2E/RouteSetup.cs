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
            ["submit-proposal"] = async page =>
            {
                await page.Locator("[data-action='propose']").ClickAsync();
                await page.Locator("[data-action='submit-proposal']").ClickAsync();
            },
            ["save-capacity"] = async page =>
            {
                await page.Locator("input[name='totalHeadcount']").First.FillAsync("1");
                await page.Locator("[data-action='save-capacity']").First.ClickAsync();
            },
            ["begin-event-cancel"] = page => page.Locator("[data-action='cancel-event']").First.ClickAsync(),
            ["check-in-conflict"] = page => page.Locator("[data-action='check-in']").First.ClickAsync(),
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
