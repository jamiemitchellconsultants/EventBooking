using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the in-app Help page shows the right bundled guide for the caller.</summary>
public class HelpPageTests : BunitContext
{
    [Fact]
    public void SignedOutVisitorSeesTheAttendeeGuide()
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = RenderHelp(meOutcome: null);

        Assert.Contains("Attendee guide", cut.Markup);
        Assert.Contains("personal links sent to your email", cut.Markup);
    }

    [Fact]
    public void ManagerSeesEveryGuideNotJustTheManagerGuide()
    {
        this.AddAuthorization().SetAuthorized("Manny Manager");

        var cut = RenderHelp(ApiOutcome<MeDto>.Success(new MeDto(["Manager"], Guid.NewGuid(), "Medical Check-up"), 200));

        Assert.Contains("Admin guide", cut.Markup);
        Assert.Contains("Manager guide", cut.Markup);
        Assert.Contains("Appointment staff guide", cut.Markup);
        Assert.Contains("Coordinator guide", cut.Markup);
        Assert.Contains("Attendee guide", cut.Markup);
    }

    [Fact]
    public void MultiGuideStaffGetAJumpLinkToEveryGuide()
    {
        this.AddAuthorization().SetAuthorized("Manny Manager");

        var cut = RenderHelp(ApiOutcome<MeDto>.Success(new MeDto(["Manager"], Guid.NewGuid(), "Medical Check-up"), 200));

        var toc = cut.Find("nav.guide-toc");
        var links = toc.QuerySelectorAll("a");
        Assert.Equal(
            ["/help#admin", "/help#manager", "/help#appointmentstaff", "/help#coordinator", "/help#attendee"],
            links.Select(link => link.GetAttribute("href")));
    }

    [Fact]
    public void GuidesDoNotRepeatTheNowRedundantBackToAllGuidesLink()
    {
        this.AddAuthorization().SetAuthorized("Manny Manager");

        var cut = RenderHelp(ApiOutcome<MeDto>.Success(new MeDto(["Manager"], Guid.NewGuid(), "Medical Check-up"), 200));

        Assert.DoesNotContain("All user guides", cut.Markup);
    }

    [Fact]
    public void CombinedRoleStaffSeeEveryGuide()
    {
        this.AddAuthorization().SetAuthorized("Cory Coordinator");

        var cut = RenderHelp(ApiOutcome<MeDto>.Success(new MeDto(["Manager", "Coordinator"], Guid.NewGuid(), "Uniform Fitting"), 200));

        Assert.Contains("Manager guide", cut.Markup);
        Assert.Contains("Coordinator guide", cut.Markup);
        Assert.Contains("Attendee guide", cut.Markup);
    }

    [Fact]
    public void UnassignedStaffAreToldToAskAnAdministrator()
    {
        this.AddAuthorization().SetAuthorized("New Starter");

        var cut = RenderHelp(ApiOutcome<MeDto>.Success(new MeDto([], null, null), 200));

        Assert.Contains("not been assigned a role yet", cut.Markup);
    }

    private IRenderedComponent<CascadingAuthenticationState> RenderHelp(ApiOutcome<MeDto>? meOutcome)
    {
        RenderFragment helpWithMeOutcome = builder =>
        {
            builder.OpenComponent<CascadingValue<ApiOutcome<MeDto>?>>(0);
            builder.AddAttribute(1, "Value", meOutcome);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<Help>(0);
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        };

        return Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent(helpWithMeOutcome));
    }
}
