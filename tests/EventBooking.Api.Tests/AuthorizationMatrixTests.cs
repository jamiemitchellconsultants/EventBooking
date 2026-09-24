using System.Net;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class AuthorizationMatrixTests(ApiFactory factory)
{
    public static TheoryData<Role, string, HttpStatusCode> Matrix => new()
    {
        { Role.Admin, "/api/attendees", HttpStatusCode.Forbidden },
        { Role.Admin, "/api/dashboards", HttpStatusCode.Forbidden },
        { Role.Admin, $"/api/audit/attendees/{Guid.NewGuid()}", HttpStatusCode.Forbidden },
        { Role.Admin, "/api/settings", HttpStatusCode.OK },
        { Role.Admin, "/api/event-proposals", HttpStatusCode.Forbidden },
        { Role.Coordinator, "/api/attendees", HttpStatusCode.OK },
        { Role.Coordinator, "/api/dashboards", HttpStatusCode.OK },
        { Role.Coordinator, "/api/settings", HttpStatusCode.Forbidden },
        { Role.Coordinator, "/api/event-proposals", HttpStatusCode.Forbidden },
        { Role.Manager, "/api/event-proposals", HttpStatusCode.OK },
        { Role.AppointmentStaff, "/api/event-proposals", HttpStatusCode.Forbidden },
        { Role.AppointmentStaff, "/api/attendees", HttpStatusCode.Forbidden },
    };

    public static TheoryData<Role[], string, HttpStatusCode> CombinedMatrix => new()
    {
        { [Role.Coordinator, Role.Manager], "/api/attendees", HttpStatusCode.OK },
        { [Role.Coordinator, Role.Manager], "/api/event-proposals", HttpStatusCode.OK },
        { [Role.Coordinator, Role.AppointmentStaff], "/api/attendees", HttpStatusCode.OK },
        { [Role.Coordinator, Role.AppointmentStaff], "/api/event-proposals", HttpStatusCode.Forbidden },
        { [Role.Manager, Role.AppointmentStaff], "/api/event-proposals", HttpStatusCode.OK },
        { [Role.Manager, Role.AppointmentStaff], "/api/attendees", HttpStatusCode.Forbidden },
        { [Role.Coordinator, Role.Manager, Role.AppointmentStaff], "/api/attendees", HttpStatusCode.OK },
        { [Role.Coordinator, Role.Manager, Role.AppointmentStaff], "/api/event-proposals", HttpStatusCode.OK },
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
        var unassigned = await factory.CreateClient().GetAsync("/api/attendees");

        factory.SignedInAs = null;
        var anonymous = await factory.CreateClient().GetAsync("/api/attendees");

        Assert.Equal(HttpStatusCode.Forbidden, unassigned.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }
}
