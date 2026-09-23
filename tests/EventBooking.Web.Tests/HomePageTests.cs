using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the landing page links out to the in-app Help guide.</summary>
public class HomePageTests : BunitContext
{
    [Fact]
    public void AssignedStaffSeeAHelpCardAmongTheirWorkspaceLinks()
    {
        this.AddAuthorization().SetAuthorized("Cory Coordinator");

        var cut = RenderHome(ApiOutcome<MeDto>.Success(new MeDto(["Coordinator"], null, null), 200));

        Assert.Contains("href=\"/help\"", cut.Markup);
    }

    [Fact]
    public void UnassignedStaffSeeNoHelpCard()
    {
        this.AddAuthorization().SetAuthorized("New Starter");

        var cut = RenderHome(ApiOutcome<MeDto>.Success(new MeDto([], null, null), 200));

        Assert.DoesNotContain("href=\"/help\"", cut.Markup);
    }

    [Fact]
    public void SignedOutVisitorsCanReachTheCandidateGuide()
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = RenderHome(meOutcome: null);

        var link = cut.Find("a[href='/help']");
        Assert.Equal("Read the candidate guide", link.TextContent.Trim());
    }

    private IRenderedComponent<CascadingAuthenticationState> RenderHome(ApiOutcome<MeDto>? meOutcome)
    {
        RenderFragment homeWithMeOutcome = builder =>
        {
            builder.OpenComponent<CascadingValue<ApiOutcome<MeDto>?>>(0);
            builder.AddAttribute(1, "Value", meOutcome);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<Home>(0);
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        };

        return Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent(homeWithMeOutcome));
    }
}
