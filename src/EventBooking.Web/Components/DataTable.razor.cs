using Microsoft.AspNetCore.Components;

namespace EventBooking.Web.Components;

public partial class DataTable<TItem>
{
    [Parameter] public IReadOnlyList<TItem>? Items { get; set; }
    [Parameter, EditorRequired] public Func<TItem, object> RowKey { get; set; } = null!;
    [Parameter, EditorRequired] public IReadOnlyList<TableColumn<TItem>> Columns { get; set; } = [];
    [Parameter] public Func<TItem, IReadOnlyList<TypeCapacity>> Capacities { get; set; } = _ => [];
    [Parameter] public string? NextCursor { get; set; }
    [Parameter] public EventCallback LoadMore { get; set; }
    [Parameter] public string EmptyTitle { get; set; } = "Nothing here yet.";
    [Parameter] public string EmptyHint { get; set; } = "";
    [Parameter] public string EmptyActionLabel { get; set; } = "";
    [Parameter] public EventCallback EmptyAction { get; set; }
    private const int MaximumTypeColumns = 5;
    private IReadOnlyList<string> TypeCodes(IReadOnlyList<TItem> items) =>
        items.SelectMany(Capacities).Select(x => x.TypeCode).Distinct().ToArray();
    private string? CapacityText(TItem item, string typeCode) =>
        Capacities(item).FirstOrDefault(x => x.TypeCode == typeCode) is { } capacity
            ? $"{capacity.TotalHeadcount} total, {capacity.RemainingCapacity} left"
            : null;
}
