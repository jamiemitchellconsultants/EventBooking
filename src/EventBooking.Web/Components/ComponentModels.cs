using Microsoft.AspNetCore.Components;

namespace EventBooking.Web.Components;

public sealed record TypeOption(
    Guid Id, string Code, string Name, bool IsActive, bool HasManager);
public sealed record LocationOption(
    Guid Id, string Name, string Address, string ZoneAbbreviation, bool IsActive);
public sealed record TypeCapacity(
    Guid TypeId, string TypeCode, string TypeName, int TotalHeadcount, int RemainingCapacity);
public sealed record TableColumn<T>(
    string Title, RenderFragment<T> Cell, bool IsNumeric = false, string? TypeCode = null)
{
    public TableColumn(string title, Func<T, string?> text)
        : this(title, item => builder => builder.AddContent(0, text(item))) { }
}
