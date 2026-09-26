using Microsoft.AspNetCore.Components;
using EventBooking.Web.Services;

namespace EventBooking.Web.Shared;

public partial class BrandMark
{
    [Inject] private ProductOptions Product { get; set; } = default!;

}
