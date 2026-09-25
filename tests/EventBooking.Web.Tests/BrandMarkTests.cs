using Bunit;
using EventBooking.Web.Services;
using EventBooking.Web.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>The brand mark sits beside a link that already names the product, so it must not repeat it.</summary>
public class BrandMarkTests : BunitContext
{
    [Fact]
    public void WithoutALogoTheBrandMarkRendersNothingSoTheNameAppearsOnce()
    {
        Services.AddSingleton(new ProductOptions("EventBooking", null, "events@example.com"));

        var cut = Render<BrandMark>();

        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void WithALogoTheImageIsDecorativeBecauseTheAdjacentLinkNamesTheProduct()
    {
        Services.AddSingleton(new ProductOptions("EventBooking", "/logo.png", "events@example.com"));

        var cut = Render<BrandMark>();

        var image = cut.Find("img.brand-mark");
        Assert.Equal("/logo.png", image.GetAttribute("src"));
        Assert.Equal(string.Empty, image.GetAttribute("alt"));
    }
}
