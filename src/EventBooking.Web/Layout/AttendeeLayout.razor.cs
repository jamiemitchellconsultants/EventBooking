using Microsoft.AspNetCore.Components;
using EventBooking.Web.Services;

namespace EventBooking.Web.Layout;

public partial class AttendeeLayout
{
    [Inject] private ProductOptions Product { get; set; } = default!;

}
