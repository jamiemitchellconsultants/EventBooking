using EventBooking.Application.Access;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Tests.Access;

public sealed class AuthorizationMatrixTests
{
    private static readonly Guid TypeId = Guid.Parse("b0000001-0000-0000-0000-000000000001");

    public static IEnumerable<object[]> Grants() =>
        CapabilityMatrix.Grants.Select(g =>
            new object[] { g.Capability, new[] { Enum.Parse<Role>(g.Role) } });

    [Theory]
    [MemberData(nameof(Grants))]
    public async Task Every_table_row_authorizes(string capabilityName, Role[] roles)
    {
        var fixture = AccessFixture.Create();
        var user = Guid.NewGuid();
        var scope = roles.Any(NeedsScope) ? TypeId : (Guid?)null;
        fixture.Profiles.Items.Add(StaffAccessProfile.Create(user, roles.ToList(), scope));
        var token = new AccessToken { StaffUserId = user, Roles = roles.ToList() };
        var capability = Enum.Parse<StaffCapability>(capabilityName);

        var result = await fixture.AuthorizeAsync(token, capability);

        Assert.True(result.IsSuccess);
    }

    public static IEnumerable<object[]> Denials() =>
        CapabilityMatrix.AllCapabilities
            .SelectMany(capability => AllRoles()
                .Where(role => !CapabilityMatrix.Grants.Any(g =>
                    g.Capability == capability && g.Role == role.ToString()))
                .Select(role => new object[] { capability, new[] { role } }));

    [Theory]
    [MemberData(nameof(Denials))]
    public async Task Cells_outside_the_table_are_forbidden(string capabilityName, Role[] roles)
    {
        var fixture = AccessFixture.Create();
        var user = Guid.NewGuid();
        var scope = roles.Any(NeedsScope) ? TypeId : (Guid?)null;
        fixture.Profiles.Items.Add(StaffAccessProfile.Create(user, roles.ToList(), scope));
        var token = new AccessToken { StaffUserId = user, Roles = roles.ToList() };
        var capability = Enum.Parse<StaffCapability>(capabilityName);

        var result = await fixture.AuthorizeAsync(token, capability);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private static bool NeedsScope(Role role) =>
        role is Role.Manager or Role.AppointmentStaff;

    private static IEnumerable<Role> AllRoles() =>
        Enum.GetValues<Role>().Where(r => r is Role.Admin or Role.Coordinator or Role.Manager or Role.AppointmentStaff);
}
