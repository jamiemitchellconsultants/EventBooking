using Microsoft.AspNetCore.Components;

namespace EventBooking.Web.Components;

public partial class TypePicker
{
    [Parameter, EditorRequired] public IReadOnlyList<TypeOption> Options { get; set; } = [];
    [Parameter] public Guid? CallerTypeId { get; set; }
    [Parameter] public IReadOnlyList<Guid> SelectedIds { get; set; } = [];
    [Parameter] public EventCallback<IReadOnlyList<Guid>> SelectedIdsChanged { get; set; }
    private Task Change(Guid id, bool selected) => SelectedIdsChanged.InvokeAsync(
        selected ? [.. SelectedIds, id] : [.. SelectedIds.Where(x => x != id)]);
}
