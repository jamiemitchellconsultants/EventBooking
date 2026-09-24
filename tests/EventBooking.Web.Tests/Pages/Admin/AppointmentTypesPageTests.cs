using System.Net;
using Bunit;
using EventBooking.Web.Pages.Admin;

namespace EventBooking.Web.Tests.Pages.Admin;

public sealed class AppointmentTypesPageTests : BunitContext
{
    [Fact]
    public void ManagerlessTypeHasVisibleAssignmentGuidance()
    {
        AdminPageFixture.Register(Services, AdminPageFixture.Json(HttpStatusCode.OK,
            """{"items":[{"id":"10000000-0000-0000-0000-000000000001","code":"ESC","name":"Escort briefing","isActive":true,"version":1,"hasManager":false,"managerDisplayName":null,"_links":{}}],"nextCursor":null}"""));
        var cut = Render<AppointmentTypes>();
        cut.WaitForAssertion(() => Assert.Contains("No Manager assigned", cut.Markup));
        Assert.Empty(cut.FindAll("[data-action='edit']"));
    }
}
