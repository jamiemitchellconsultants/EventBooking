using System.Text.Json;
using Microsoft.Playwright;

namespace EventBooking.Web.E2E;

public static class AxeRunner
{
    public static async Task<IReadOnlyList<string>> ViolationsAsync(IPage page, string script)
    {
        await page.AddScriptTagAsync(new PageAddScriptTagOptions { Path = script });
        var result = await page.EvaluateAsync<JsonElement>(
            "async () => await axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a','wcag2aa','wcag21a','wcag21aa'] } })");
        return result.GetProperty("violations").EnumerateArray()
            .Select(v => $"{v.GetProperty("id").GetString()}: {v.GetProperty("help").GetString()}")
            .ToArray();
    }
}
