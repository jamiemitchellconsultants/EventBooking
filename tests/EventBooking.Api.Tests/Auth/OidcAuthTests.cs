using System.Net;
using EventBooking.Api.Auth;
using EventBooking.Domain.Access;

namespace EventBooking.Api.Tests.Auth;

[Collection("api")]
public class OidcAuthTests(ApiFactory factory)
{
    [Fact]
    public async Task Missing_staff_id_is_forbidden_except_me()
    {
        var signedInAs = factory.SignedInAs;
        var staffIdClaim = factory.StaffIdClaim;
        var rolesClaim = factory.RolesClaim;
        try
        {
            factory.SignedInAs = Guid.NewGuid();
            factory.StaffIdClaim = null;
            factory.RolesClaim = ["Coordinator"];
            var client = factory.CreateClient();

            var attendees = await client.GetAsync("/api/attendees");
            Assert.Equal(HttpStatusCode.Forbidden, attendees.StatusCode);

            var me = await client.GetAsync("/api/me");
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
            Assert.Contains("staff_id", await me.Content.ReadAsStringAsync());
        }
        finally
        {
            factory.SignedInAs = signedInAs;
            factory.StaffIdClaim = staffIdClaim;
            factory.RolesClaim = rolesClaim;
        }
    }

    [Fact]
    public async Task Malformed_staff_id_behaves_like_missing()
    {
        var signedInAs = factory.SignedInAs;
        var staffIdClaim = factory.StaffIdClaim;
        var rolesClaim = factory.RolesClaim;
        try
        {
            factory.SignedInAs = Guid.NewGuid();
            factory.StaffIdClaim = "not valid!!";
            factory.RolesClaim = ["Coordinator"];
            var client = factory.CreateClient();

            var attendees = await client.GetAsync("/api/attendees");
            Assert.Equal(HttpStatusCode.Forbidden, attendees.StatusCode);
        }
        finally
        {
            factory.SignedInAs = signedInAs;
            factory.StaffIdClaim = staffIdClaim;
            factory.RolesClaim = rolesClaim;
        }
    }

    [Fact]
    // Synchronous: this asserts option binding and pattern parsing, nothing awaits, and
    // an async method with no await is CS1998 against -warnaserror.
    public void Claim_names_and_pattern_come_from_configuration()
    {
        var options = new AuthClaimOptions();
        Assert.Equal("staff_id", options.StaffIdClaim);
        Assert.Equal("name", options.NameClaim);
        Assert.Equal("roles", options.RolesClaim);
        Assert.Equal(StaffId.DefaultPattern, options.StaffIdPattern);

        var configured = new AuthClaimOptions
        {
            StaffIdClaim = "employee_no",
            NameClaim = "display_name",
            RolesClaim = "app_roles",
            StaffIdPattern = "^[UN][0-9]{6}$",
        };
        Assert.Equal("employee_no", configured.StaffIdClaim);
        Assert.True(StaffId.TryParse("U123456", out _, configured.StaffIdPattern));
        Assert.False(StaffId.TryParse("A10023", out _, configured.StaffIdPattern));
    }
}
