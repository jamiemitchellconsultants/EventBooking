using EventBooking.Application.Access;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Tests.Access;

public sealed class RoleSyncTests
{
    [Fact]
    public async Task Missing_staff_id_is_forbidden_and_writes_no_identity()
    {
        var fixture = AccessFixture.Create();
        var token = new AccessToken { StaffIdValue = null };

        var result = await fixture.AuthorizeAsync(token, StaffCapability.ManageAttendees);
        var me = await new MeHandler(fixture.Profiles)
            .HandleAsync(token.StaffUserId, token.StaffIdValue, token.DisplayName,
                token.Roles.ToHashSet(), token.StaffIdPattern, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Contains("staff_id", me.Value.Problem);
        Assert.Empty(fixture.Identities.Items);
    }

    [Fact]
    public async Task Malformed_staff_id_behaves_like_missing()
    {
        var fixture = AccessFixture.Create();
        var token = new AccessToken { StaffIdValue = "not valid!!" };

        var result = await fixture.AuthorizeAsync(token, StaffCapability.ManageAttendees);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Empty(fixture.Identities.Items);
    }

    [Fact]
    public async Task Role_drift_syncs_then_authorizes_same_request()
    {
        var fixture = AccessFixture.Create();
        var user = Guid.NewGuid();
        fixture.Profiles.Items.Add(StaffAccessProfile.Create(user, Role.Coordinator, null));
        // Admin is exclusive, so drift lands on the single-Admin set rather than a
        // combined set the domain would reject.
        var token = new AccessToken
        {
            StaffUserId = user,
            Roles = [Role.Admin],
        };

        var result = await fixture.AuthorizeAsync(token, StaffCapability.ManageReferenceData);

        Assert.True(result.IsSuccess);
        Assert.Contains(fixture.Audit.Entries,
            e => e.Action == Domain.Audit.AuditAction.StaffRolesSynced
                && e.ActorType == Domain.Audit.ActorType.System);
    }

    [Fact]
    public async Task Removing_last_admin_is_refused_and_logged()
    {
        var fixture = AccessFixture.Create();
        var sam = Guid.NewGuid();
        fixture.Profiles.Items.Add(StaffAccessProfile.Create(sam, Role.Admin, null));
        var token = new AccessToken { StaffUserId = sam, Roles = [Role.Coordinator] };

        var result = await fixture.AuthorizeAsync(token, StaffCapability.ManageAttendees);

        Assert.True(result.IsFailure);
        Assert.True(fixture.Logs.Exists(l => l.Contains("last remaining Admin")));
        Assert.True(fixture.Profiles.Items.Single(p => p.StaffUserId == sam).IsAdmin);
    }

    [Fact]
    public async Task Token_with_no_roles_removes_the_profile()
    {
        var fixture = AccessFixture.Create();
        var user = Guid.NewGuid();
        fixture.Profiles.Items.Add(StaffAccessProfile.Create(user, Role.Coordinator, null));
        var token = new AccessToken { StaffUserId = user, Roles = [] };

        var result = await fixture.AuthorizeAsync(token, StaffCapability.ManageAttendees);

        Assert.True(result.IsFailure);
        Assert.Empty(fixture.Profiles.Items);
    }

    [Fact]
    public async Task Null_scoped_manager_gets_no_capability()
    {
        var fixture = AccessFixture.Create();
        var user = Guid.NewGuid();
        fixture.Profiles.Items.Add(StaffAccessProfile.Create(user, Role.Manager, null));
        var token = new AccessToken { StaffUserId = user, Roles = [Role.Manager] };

        var result = await fixture.AuthorizeAsync(token, StaffCapability.ManageEventNegotiation);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }
}
