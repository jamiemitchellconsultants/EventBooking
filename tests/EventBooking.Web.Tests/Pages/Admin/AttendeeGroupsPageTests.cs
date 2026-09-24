using System.Net;
using Bunit;
using EventBooking.Web.Pages.Admin;

namespace EventBooking.Web.Tests.Pages.Admin;

public sealed class AttendeeGroupsPageTests : BunitContext
{
    [Fact]
    public void RequirementsChangeNamesAffectedMembersBeforeSave()
    {
        AdminPageFixture.Register(Services,
            AdminPageFixture.Json(HttpStatusCode.OK,
                """{"items":[{"id":"10000000-0000-0000-0000-000000000001","code":"FIELD","name":"Field staff","isActive":true,"version":1,"requirementTypeIds":[],"memberCount":17,"_links":{"update":{"href":"/api/attendee-groups/10000000-0000-0000-0000-000000000001","method":"PUT","operationId":"updateAttendeeGroup"}}}],"nextCursor":null}"""),
            AdminPageFixture.Json(HttpStatusCode.OK,
                """{"items":[{"id":"20000000-0000-0000-0000-000000000002","code":"FIT","name":"Fitting","isActive":true,"version":1,"hasManager":true,"managerDisplayName":"F. Manager","_links":{}}],"nextCursor":null}"""));
        var cut = Render<AttendeeGroups>();
        cut.WaitForElement("button[data-action='edit']");
        cut.Find("button[data-action='edit']").Click();
        cut.Find("input[type='checkbox']:not([disabled])").Change(true);
        cut.Find("button[data-action='save']").Click();
        Assert.Contains("This changes requirements for 17 attendees and replaces their pending invitations.", cut.Markup);
    }
}
