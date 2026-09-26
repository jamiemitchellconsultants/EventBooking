using Microsoft.AspNetCore.Components;

namespace EventBooking.Web.Components;

public partial class Banner
{
    [Parameter] public BannerVariant Variant { get; set; } = BannerVariant.Info;
    [Parameter, EditorRequired] public string Message { get; set; } = "";
    [Parameter] public EventCallback Retry { get; set; }
}
