using Microsoft.AspNetCore.Components;
using EventBooking.Web.Services;

namespace EventBooking.Web.Components;

public partial class EventTime
{
    [Parameter, EditorRequired] public EventTimeDto Value { get; set; } = null!;
    [Parameter] public string? LocationName { get; set; }
    [Parameter] public bool ShowLocation { get; set; }
}
