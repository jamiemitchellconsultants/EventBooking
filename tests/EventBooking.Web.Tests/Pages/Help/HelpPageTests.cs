using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace EventBooking.Web.Tests.Pages.Help;

public sealed class HelpPageTests : BunitContext
{
    [Fact]
    public void SignedInUserSeesEveryHeldRoleAndContents()
    {
        Services.AddSingleton<IUserGuideCatalog>(new FakeCatalog());
        Services.AddSingleton<AuthenticationStateProvider>(new StateProvider("Coordinator", "Manager"));
        var cut = Render<EventBooking.Web.Pages.Help>();
        cut.WaitForAssertion(() => Assert.Contains("Contents", cut.Markup));
        Assert.Contains("Coordinator guide", cut.Markup);
        Assert.Contains("Manager guide", cut.Markup);
        Assert.DoesNotContain("Admin guide", cut.Markup);
        Assert.DoesNotContain("Attendee guide", cut.Markup);
    }

    [Fact]
    public void AnonymousUserSeesOnlyAttendeeGuide()
    {
        Services.AddSingleton<IUserGuideCatalog>(new FakeCatalog());
        Services.AddSingleton<AuthenticationStateProvider>(new StateProvider());
        var cut = Render<EventBooking.Web.Pages.Help>();
        cut.WaitForAssertion(() => Assert.Contains("Attendee guide", cut.Markup));
        Assert.DoesNotContain("Coordinator guide", cut.Markup);
        Assert.DoesNotContain("Admin guide", cut.Markup);
    }

    private sealed class FakeCatalog : IUserGuideCatalog
    {
        public Task<IReadOnlyList<UserGuide>> ForAsync(bool authenticated, IReadOnlyCollection<string> roles, CancellationToken ct) => Task.FromResult<IReadOnlyList<UserGuide>>(authenticated ? roles.Select(r => new UserGuide(r, $"{r} guide", $"help/{r.ToLowerInvariant()}.md")).ToArray() : [new("Attendee", "Attendee guide", "help/attendee.md")]);
        public Task<string> ReadMarkdownAsync(UserGuide guide, CancellationToken ct) => Task.FromResult($"# {guide.Title}\n\nUse this guide.");
    }

    private sealed class StateProvider(params string[] roles) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = roles.Length == 0 ? new ClaimsIdentity() : new ClaimsIdentity(roles.Select(x => new Claim(ClaimTypes.Role, x)), "test");
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }
}
