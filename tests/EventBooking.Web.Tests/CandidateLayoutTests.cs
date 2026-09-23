using System.Reflection;
using Bunit;
using EventBooking.Web.Layout;
using EventBooking.Web.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace EventBooking.Web.Tests;

/// <summary>
/// Regression coverage for the anonymous candidate shell, which must not expose staff sign-in controls.
/// </summary>
public class CandidateLayoutTests : BunitContext
{
    /// <summary>
    /// Verifies that both candidate routes compile with the dedicated anonymous layout.
    /// </summary>
    [Fact]
    public void CandidatePagesCompileWithTheCandidateLayout()
    {
        Assert.Equal(typeof(CandidateLayout), GetLayoutType(typeof(Book)));
        Assert.Equal(typeof(CandidateLayout), GetLayoutType(typeof(ManageBooking)));
    }

    /// <summary>
    /// Verifies that the candidate shell retains branding and its body without staff authentication controls.
    /// </summary>
    [Fact]
    public void AnonymousCandidateLayoutShowsBrandAndBodyWithoutAuthenticationControls()
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = RenderLayout();

        Assert.Equal("EventBooking", cut.Find("header.topbar a.brand").TextContent.Trim());
        Assert.Equal("/", cut.Find("header.topbar a.brand").GetAttribute("href"));
        Assert.Contains("Candidate content", cut.Markup);
        Assert.DoesNotContain("authentication/login", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authentication/logout", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sign in", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sign out", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    private static Type? GetLayoutType(Type pageType) =>
        pageType.GetCustomAttribute<LayoutAttribute>()?.LayoutType;

    private IRenderedComponent<CascadingAuthenticationState> RenderLayout()
    {
        RenderFragment layout = builder =>
        {
            builder.OpenComponent<CandidateLayout>(0);
            builder.AddAttribute(1, "Body", (RenderFragment)(body => body.AddContent(0, "Candidate content")));
            builder.CloseComponent();
        };

        return Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent(layout));
    }
}
