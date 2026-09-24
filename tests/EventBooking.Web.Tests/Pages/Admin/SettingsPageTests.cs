using System.Net;
using Bunit;
using EventBooking.Web.Pages;

namespace EventBooking.Web.Tests.Pages.Admin;

public sealed class SettingsPageTests : BunitContext
{
    [Fact]
    public void AllThreeFutureInviteSettingsAreBounded()
    {
        AdminPageFixture.Register(Services, AdminPageFixture.Json(HttpStatusCode.OK,
            """{"inviteExpiryDays":7,"maxAutoRetryCount":2,"inviteOptionCount":3,"version":1,"_links":{"update":{"href":"/api/settings","method":"PUT","operationId":"updateSettings"}}}"""));
        var cut = Render<Settings>();
        cut.WaitForElement("#invite-expiry-days");
        Assert.Equal("1", cut.Find("#invite-expiry-days").GetAttribute("min"));
        Assert.Equal("60", cut.Find("#invite-expiry-days").GetAttribute("max"));
        Assert.Equal("0", cut.Find("#max-auto-retries").GetAttribute("min"));
        Assert.Equal("10", cut.Find("#max-auto-retries").GetAttribute("max"));
        Assert.Equal("1", cut.Find("#invite-option-count").GetAttribute("min"));
        Assert.Equal("5", cut.Find("#invite-option-count").GetAttribute("max"));
        Assert.Contains("future invitations only", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }
}
