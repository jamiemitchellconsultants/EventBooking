using System.Net;
using System.Net.Http.Json;
using Bunit;
using Bunit.TestDoubles;
using EventBooking.Web.Layout;
using EventBooking.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

public class MainLayoutTests : BunitContext
{
    [Fact]
    public void AnonymousTopbarShowsSignInLink()
    {
        this.AddAuthorization().SetNotAuthorized();
        RegisterMe(new MeDto([], null, null));

        var cut = RenderTopbar();

        var link = cut.Find("header.topbar a[href='authentication/login']");
        Assert.Equal("Sign in", link.TextContent.Trim());
        Assert.DoesNotContain("staff-nav", cut.Markup);
    }

    [Fact]
    public void AuthenticatedTopbarSignsOutThroughInPageNavigation()
    {
        this.AddAuthorization().SetAuthorized("Manny Manager");
        RegisterMe(new MeDto([], null, null));

        var cut = RenderTopbar();

        cut.Find("header.topbar button").Click();

        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        Assert.EndsWith("authentication/logout", navigation.Uri);
        Assert.DoesNotContain("authentication/login", cut.Markup);
    }

    [Fact]
    public void AuthenticatedTopbarShowsOnlyTheCallersPermittedNavLinks()
    {
        this.AddAuthorization().SetAuthorized("Manny Manager");
        RegisterMe(new MeDto(["Manager"], Guid.NewGuid(), "Medical Check-up"));

        var cut = RenderTopbar();

        cut.WaitForAssertion(() => Assert.Contains("staff-nav", cut.Markup));
        Assert.Contains("href=\"/events/negotiate\"", cut.Markup);
        Assert.DoesNotContain("href=\"/attendees\"", cut.Markup);
    }

    [Fact]
    public void AnyAssignedRoleGetsAHelpLinkToTheirGuide()
    {
        this.AddAuthorization().SetAuthorized("Cory Coordinator");
        RegisterMe(new MeDto(["Coordinator"], null, null));

        var cut = RenderTopbar();

        cut.WaitForAssertion(() => Assert.Contains("href=\"/help\"", cut.Markup));
    }

    [Fact]
    public void UnassignedStaffSeeNoNavBar()
    {
        this.AddAuthorization().SetAuthorized("New Starter");
        RegisterMe(new MeDto([], null, null));

        var cut = RenderTopbar();

        Assert.DoesNotContain("staff-nav", cut.Markup);
    }

    private void RegisterMe(MeDto me)
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(me),
        });
        Services.AddSingleton(new MeClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com"),
        }));
    }

    private IRenderedComponent<CascadingAuthenticationState> RenderTopbar()
    {
        RenderFragment layout = builder =>
        {
            builder.OpenComponent<MainLayout>(0);
            builder.CloseComponent();
        };

        return Render<CascadingAuthenticationState>(
            parameters => parameters.AddChildContent(layout));
    }

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}
