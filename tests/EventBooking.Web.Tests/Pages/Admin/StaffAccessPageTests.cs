using System.Net;
using Bunit;
using EventBooking.Web.Pages;

namespace EventBooking.Web.Tests.Pages.Admin;

public sealed class StaffAccessPageTests : BunitContext
{
    [Fact]
    public void SavedConfirmationNamesDisplacedManager()
    {
        // The scope link is the API's camelCase `setScope` relation, not `set-scope`.
        AdminPageFixture.Register(Services, AdminPageFixture.Json(HttpStatusCode.OK,
            """{"items":[{"id":"20000000-0000-0000-0000-000000000002","code":"MED","name":"Medical check","isActive":true,"version":1,"hasManager":true,"managerDisplayName":"Sam Patel","_links":{}}],"nextCursor":null}"""));
        AdminPageFixture.RegisterStaff(Services,
            AdminPageFixture.Json(HttpStatusCode.OK,
                """{"items":[{"staffUserId":"10000000-0000-0000-0000-000000000001","staffId":"M1","displayName":"Morgan Lee","roles":["Manager"],"appointmentTypeId":null,"version":1,"_links":{"setScope":{"href":"/api/staff-access/10000000-0000-0000-0000-000000000001/scope","method":"PUT","operationId":"setStaffAccessScope"}}}],"nextCursor":null}"""),
            AdminPageFixture.Json(HttpStatusCode.OK,
                """{"targetStaffUserId":"10000000-0000-0000-0000-000000000001","appointmentTypeId":"20000000-0000-0000-0000-000000000002","displacedManagerDisplayName":"Sam Patel"}"""));
        var cut = Render<StaffAccess>();
        cut.WaitForElement("button[data-action='save-scope']");
        cut.Find("button[data-action='save-scope']").Click();
        cut.WaitForAssertion(() => Assert.Contains("Sam Patel is no longer the Manager", cut.Markup));
    }
}
