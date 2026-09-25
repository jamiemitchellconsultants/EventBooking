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
    public void SignedOutVisitorsCanReachTheAttendeeGuide()
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = RenderHome(meOutcome: null);

        var link = cut.Find("a[href='/help']");
        Assert.Equal("Read the attendee guide", link.TextContent.Trim());
    }

    [Fact]
    public void CoordinatorsSeeTheirWorkSeparatedFromReadOnlyReferenceData()
    {
        this.AddAuthorization().SetAuthorized("Cory Coordinator");

        var cut = RenderHome(ApiOutcome<MeDto>.Success(new MeDto(["Coordinator"], null, null), 200));

        var work = cut.Find("nav[aria-label='Your work']");
        Assert.Contains("Attendees", work.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Locations", work.TextContent, StringComparison.Ordinal);
        var reference = cut.Find("nav[aria-label='Reference data']");
        Assert.Contains("Locations", reference.TextContent, StringComparison.Ordinal);
        Assert.Contains("Read-only", cut.Markup, StringComparison.Ordinal);
        Assert.Single(cut.FindAll("nav[aria-label='Help']"));
    }

    [Fact]
    public void AdminsSeeAnAdministrationGroupForTheReferenceScreens()
    {
        this.AddAuthorization().SetAuthorized("Ari Admin");

        var cut = RenderHome(ApiOutcome<MeDto>.Success(new MeDto(["Admin"], null, null), 200));

        var administration = cut.Find("nav[aria-label='Administration']");
        Assert.Contains("Staff access", administration.TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("nav[aria-label='Reference data']"));
    }

    [Fact]
    public void EveryTileHasATitleAndItsOwnDescriptionInSeparateElements()
    {
        this.AddAuthorization().SetAuthorized("Cory Coordinator");

        var cut = RenderHome(ApiOutcome<MeDto>.Success(new MeDto(["Coordinator"], null, null), 200));

        foreach (var card in cut.FindAll("a.link-card"))
        {
            Assert.NotEmpty(card.QuerySelectorAll(".link-title"));
            Assert.NotEmpty(card.QuerySelectorAll(".landing-sub"));
        }
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
