using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class StaffAccessEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task AdminCanListSetScopeAndClearScope()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var target = await factory.GivenStaffAsync(
            [Role.Coordinator, Role.Manager], null);
        factory.RolesClaim = ["Admin"];
        var client = factory.CreateClient();

        var list = await client.GetFromJsonAsync<List<ProfileResponse>>(
            "/api/admin/staff-access");
        Assert.Contains(list!, profile => profile.StaffUserId == target);

        var set = await client.PutAsJsonAsync(
            $"/api/admin/staff-access/{target}",
            new
            {
                AppointmentTypeId = AppointmentTypeIds.MedicalCheckUp,
                ExpectedVersion = 1L,
            });
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        var setBody = await set.Content.ReadFromJsonAsync<MutationResponse>();
        Assert.Equal(AppointmentTypeIds.MedicalCheckUp, setBody!.Profile.AppointmentTypeId);

        var stale = await client.PutAsJsonAsync(
            $"/api/admin/staff-access/{target}",
            new
            {
                AppointmentTypeId = AppointmentTypeIds.UniformFitting,
                ExpectedVersion = 1L,
            });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task PutRejectsABodyContainingARolesField()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var target = await factory.GivenStaffAsync(Role.Manager, AppointmentTypeIds.MedicalCheckUp);
        factory.RolesClaim = ["Admin"];

        var response = await factory.CreateClient().PutAsJsonAsync(
            $"/api/admin/staff-access/{target}",
            new { Roles = new[] { "Manager" }, AppointmentTypeId = AppointmentTypeIds.MedicalCheckUp, ExpectedVersion = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteClearsScopeButKeepsTheProfileAndItsRoles()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var target = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.UniformFitting);
        factory.RolesClaim = ["Admin"];
        var client = factory.CreateClient();

        var response = await client.DeleteAsync(
            $"/api/admin/staff-access/{target}?expectedVersion=1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var list = await client.GetFromJsonAsync<List<ProfileResponse>>(
            "/api/admin/staff-access");
        var profile = list!.Single(item => item.StaffUserId == target);
        Assert.Null(profile.AppointmentTypeId);
        Assert.Equal(["AppointmentStaff"], profile.Roles);
    }

    [Fact]
    public async Task DeleteOnAnAlreadyNullScopeIsRejected()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var target = await factory.GivenStaffAsync([Role.Manager], null);
        factory.RolesClaim = ["Admin"];

        var response = await factory.CreateClient().DeleteAsync(
            $"/api/admin/staff-access/{target}?expectedVersion=1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoordinatorCannotReadStaffAccess()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        factory.RolesClaim = ["Coordinator"];

        var response = await factory.CreateClient().GetAsync("/api/admin/staff-access");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    /// <summary>Verifies an observed name reaches the listing and an unobserved one stays absent.</summary>
    [Fact]
    public async Task StaffAccessListReturnsDisplayNameWhenKnown()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var named = await factory.GivenStaffAsync(Role.Coordinator);
        var unnamed = await factory.GivenStaffAsync(Role.Coordinator);
        factory.RolesClaim = ["Admin"];
        await factory.GivenIdentityAsync(named, "U000021", "Dana Datson");
        await factory.GivenIdentityAsync(unnamed, "U000022");
        var client = factory.CreateClient();

        var list = await client.GetFromJsonAsync<List<ProfileResponse>>("/api/admin/staff-access");

        Assert.NotNull(list);
        var first = list!.Single(profile => profile.StaffUserId == named);
        var second = list.Single(profile => profile.StaffUserId == unnamed);
        Assert.Equal("Dana Datson", first.DisplayName);
        Assert.Equal("U000021", first.StaffId);
        Assert.Null(second.DisplayName);
        Assert.Equal("U000022", second.StaffId);
        factory.RolesClaim = [];
    }

    private sealed record ProfileResponse(
        Guid StaffUserId,
        string? StaffId,
        IReadOnlyList<string> Roles,
        Guid? AppointmentTypeId,
        string? AppointmentTypeName,
        long Version,
        string? DisplayName = null);

    private sealed record MutationResponse(
        ProfileResponse Profile,
        Guid? FormerManagerStaffUserId);
}
