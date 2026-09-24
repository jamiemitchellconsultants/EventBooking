using Microsoft.Playwright;

namespace EventBooking.Web.E2E;

public static class RouteSetup
{
    private static readonly IReadOnlyDictionary<string, Func<IPage, Task>> Actions =
        new Dictionary<string, Func<IPage, Task>>(StringComparer.Ordinal);

    public static async Task ApplyAsync(IPage page, string? action)
    {
        if (action is null) return;
        if (!Actions.TryGetValue(action, out var apply))
            throw new InvalidOperationException($"Unknown E2E setup action '{action}'.");
        await apply(page);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
