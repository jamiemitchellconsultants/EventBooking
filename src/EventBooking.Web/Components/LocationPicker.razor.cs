using Microsoft.AspNetCore.Components;

namespace EventBooking.Web.Components;

public partial class LocationPicker
{
    [Parameter] public LocationPickerMode Mode { get; set; } = LocationPickerMode.Single;
    [Parameter, EditorRequired] public IReadOnlyList<LocationOption> Options { get; set; } = [];
    [Parameter] public IReadOnlyList<Guid> SelectedIds { get; set; } = [];
    [Parameter] public EventCallback<IReadOnlyList<Guid>> SelectedIdsChanged { get; set; }
    private Task Change(Guid id, bool selected) => SelectedIdsChanged.InvokeAsync(
        selected ? [.. SelectedIds, id] : [.. SelectedIds.Where(x => x != id)]);
    private Task Select(Guid id) => SelectedIdsChanged.InvokeAsync([id]);
}
