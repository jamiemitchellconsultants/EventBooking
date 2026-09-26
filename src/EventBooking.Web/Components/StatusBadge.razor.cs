using Microsoft.AspNetCore.Components;

namespace EventBooking.Web.Components;

public partial class StatusBadge
{
    [Parameter, EditorRequired] public string Value { get; set; } = "";
    [Parameter, EditorRequired] public string Display { get; set; } = "";
    private static string ClassFor(string value) =>
        $"status-{value.ToLowerInvariant().Replace(" ", "", StringComparison.Ordinal)}";
}
