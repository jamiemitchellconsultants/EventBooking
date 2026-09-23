using System.Net;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class AuthorizationMatrixTests(ApiFactory factory)
{
    public static TheoryData<Role, string, HttpStatusCode> Matrix => new()
    {
        { Role.Admin, "/api/candidates", HttpStatusCode.Forbidden },
        { Role.Admin, "/api/dashboards", HttpStatusCode.Forbidden },
        { Role.Admin, $"/api/audit/candidate/{Guid.NewGuid()}", HttpStatusCode.Forbidden },
        { Role.Admin, "/api/admin/settings", HttpStatusCode.OK },
        { Role.Coordinator, "/api/candidates", HttpStatusCode.OK },
        { Role.Coordinator, "/api/dashboards", HttpStatusCode.OK },
        { Role.Coordinator, "/api/admin/settings", HttpStatusCode.Forbidden },
        { Role.Manager, "/api/slots/board", HttpStatusCode.OK },
        { Role.AppointmentStaff, "/api/slots/board", HttpStatusCode.Forbidden },
        { Role.AppointmentStaff, "/api/candidates", HttpStatusCode.Forbidden },
    };

    public static TheoryData<Role[], string, HttpStatusCode> CombinedMatrix => new()
    {
        { [Role.Coordinator, Role.Manager], "/api/candidates", HttpStatusCode.OK },
        { [Role.Coordinator, Role.Manager], "/api/slots/board", HttpStatusCode.OK },
        { [Role.Coordinator, Role.AppointmentStaff], "/api/candidates", HttpStatusCode.OK },
        { [Role.Coordinator, Role.AppointmentStaff], "/api/slots/board", HttpStatusCode.Forbidden },
        { [Role.Manager, Role.AppointmentStaff], "/api/slots/board", HttpStatusCode.OK },
        { [Role.Manager, Role.AppointmentStaff], "/api/candidates", HttpStatusCode.Forbidden },
        { [Role.Coordinator, Role.Manager, Role.AppointmentStaff], "/api/candidates", HttpStatusCode.OK },
        { [Role.Coordinator, Role.Manager, Role.AppointmentStaff], "/api/slots/board", HttpStatusCode.OK },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task SingleRoleEndpointMatrix(
        Role role,
        string route,
        HttpStatusCode expected)
    {
        Guid? scope = role is Role.Manager or Role.AppointmentStaff
            ? AppointmentTypeIds.DrugAndAlcoholTesting
            : null;
        factory.SignedInAs = await factory.GivenStaffAsync(role, scope);

        var response = await factory.CreateClient().GetAsync(route);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(CombinedMatrix))]
    public async Task CombinedProfileEndpointMatrix(
        Role[] roles,
        string route,
        HttpStatusCode expected)
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            roles,
            AppointmentTypeIds.DrugAndAlcoholTesting);

        var response = await factory.CreateClient().GetAsync(route);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task UnassignedAndAnonymousCallersRemainProtected()
    {
        factory.SignedInAs = Guid.NewGuid();
        factory.RolesClaim = [];
        var unassigned = await factory.CreateClient().GetAsync("/api/candidates");

        factory.SignedInAs = null;
        var anonymous = await factory.CreateClient().GetAsync("/api/candidates");

        Assert.Equal(HttpStatusCode.Forbidden, unassigned.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }
}
