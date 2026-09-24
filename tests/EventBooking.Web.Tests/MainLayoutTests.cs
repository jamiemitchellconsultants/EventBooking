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

    [Fact]
    public void SigningInAfterLoadFetchesMeForTheNav()
    {
        // The OIDC login race: the layout can initialise while the provider still
        // reports anonymous. The nav must appear once the sign-in lands, without a reload.
        var auth = new FlipFlopAuthProvider();
        this.AddAuthorization();
        Services.AddSingleton<AuthenticationStateProvider>(auth);
        var handler = new CountingHandler(new MeDto(["Coordinator"], null, null));
        Services.AddSingleton<IMeClient>(new MeClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com"),
        }));
        Services.AddSingleton(new ProductOptions("EventBooking", null, "events@example.com"));

        var cut = RenderTopbar();

        Assert.Equal(0, handler.Calls);
        auth.SignIn("Cory Coordinator");

        // The re-fetch is the layout's own subscription; that a loaded identity
        // turns into nav links is covered by the pre-authorized tests above, since
        // bUnit's authorization doubles do not flip a live AuthorizeView mid-test.
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Calls));
    }

    [Fact]
    public void AFailedIdentityFetchLeavesTheShellUsable()
    {
        // AuthorizationMessageHandler throws (AccessTokenNotAvailableException) rather than
        // returning a response when no token is available yet; the network can throw too.
        this.AddAuthorization().SetAuthorized("Cory Coordinator");
        Services.AddSingleton<IMeClient>(new ThrowingMeClient());
        Services.AddSingleton(new ProductOptions("EventBooking", null, "events@example.com"));

        var cut = RenderTopbar();

        cut.WaitForAssertion(() => Assert.DoesNotContain("Loading…", cut.Markup));
        Assert.Contains("Sign out", cut.Markup);
    }

    [Fact]
    public void AFailedIdentityFetchAfterSignInDoesNotEscapeTheHandler()
    {
        var auth = new FlipFlopAuthProvider();
        this.AddAuthorization();
        Services.AddSingleton<AuthenticationStateProvider>(auth);
        var me = new ThrowingMeClient();
        Services.AddSingleton<IMeClient>(me);
        Services.AddSingleton(new ProductOptions("EventBooking", null, "events@example.com"));
        RenderTopbar();

        auth.SignIn("Cory Coordinator");

        Assert.Equal(1, me.Calls);
    }

    private sealed class ThrowingMeClient : IMeClient
    {
        public int Calls { get; private set; }

        public Task<ApiOutcome<MeDto>> GetAsync(CancellationToken ct)
        {
            Calls++;
            throw new HttpRequestException("offline");
        }
    }

    private void RegisterMe(MeDto me)
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(me),
        });
        Services.AddSingleton<IMeClient>(new MeClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com"),
        }));
        Services.AddSingleton(new ProductOptions("EventBooking", null, "events@example.com"));
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

    private sealed class FlipFlopAuthProvider : AuthenticationStateProvider
    {
        private string? _name;

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(_name is null
                ? new System.Security.Claims.ClaimsPrincipal()
                : new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity(
                        [new System.Security.Claims.Claim(
                            System.Security.Claims.ClaimTypes.Name, _name)],
                        "test"))));

        public void SignIn(string name)
        {
            _name = name;
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }

    private sealed class CountingHandler(MeDto me) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(me),
            });
        }
    }
}
