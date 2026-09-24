using System.Reflection;
using Bunit;
using EventBooking.Web.Layout;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>
/// Regression coverage for the anonymous attendee shell, which must not expose staff sign-in controls.
/// </summary>
public class AttendeeLayoutTests : BunitContext
{
    /// <summary>
    /// Verifies that both attendee routes compile with the dedicated anonymous layout.
    /// </summary>
    [Fact]
    public void AttendeePagesCompileWithTheAttendeeLayout()
    {
        Assert.Equal(typeof(AttendeeLayout), GetLayoutType(typeof(Book)));
        Assert.Equal(typeof(AttendeeLayout), GetLayoutType(typeof(ManageBooking)));
    }

    /// <summary>
    /// Verifies that the attendee shell retains branding and its body without staff authentication controls.
    /// </summary>
    [Fact]
    public void AnonymousAttendeeLayoutShowsBrandAndBodyWithoutAuthenticationControls()
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = RenderLayout();

        Assert.Equal("EventBooking", cut.Find("header.topbar a.brand").TextContent.Trim());
        Assert.Equal("/", cut.Find("header.topbar a.brand").GetAttribute("href"));
        Assert.Contains("Attendee content", cut.Markup);
        Assert.DoesNotContain("authentication/login", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authentication/logout", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sign in", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sign out", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    private static Type? GetLayoutType(Type pageType) =>
        pageType.GetCustomAttribute<LayoutAttribute>()?.LayoutType;

    private IRenderedComponent<CascadingAuthenticationState> RenderLayout()
    {
        Services.AddSingleton(new ProductOptions("EventBooking", null, "events@example.com"));
        RenderFragment layout = builder =>
        {
            builder.OpenComponent<AttendeeLayout>(0);
            builder.AddAttribute(1, "Body", (RenderFragment)(body => body.AddContent(0, "Attendee content")));
            builder.CloseComponent();
        };

        return Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent(layout));
    }
}
