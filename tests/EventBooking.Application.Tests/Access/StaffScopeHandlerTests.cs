using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Tests.Access;

public sealed class StaffScopeHandlerTests
{
    private static readonly Guid MedType = Guid.Parse("b0000001-0000-0000-0000-000000000001");
    private static readonly Guid Admin = Guid.Parse("a0000001-0000-0000-0000-000000000001");

    private static (InMemoryStaffAccessProfileRepository Profiles,
        InMemoryStaffIdentityRepository Identities, FakeUnitOfWork UnitOfWork,
        RecordingAuditLogger Audit) Parts() => (new(), new(), new(), new());

    [Fact]
    public async Task Assigning_med_to_jo_clears_sam_and_names_him()
    {
        var (profiles, identities, unitOfWork, audit) = Parts();
        var sam = Guid.NewGuid();
        var jo = Guid.NewGuid();
        profiles.Items.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
        profiles.Items.Add(StaffAccessProfile.Create(sam, Role.Manager, MedType));
        profiles.Items.Add(StaffAccessProfile.Create(jo, Role.Manager, null));
        await identities.UpsertAsync(sam, new StaffId("S100"), "Sam", DateTimeOffset.UtcNow,
            CancellationToken.None);
        var handler = new StaffScopeHandler(profiles, identities, unitOfWork, audit, profiles);

        var result = await handler.HandleAsync(
            new SetStaffScopeCommand(Admin, jo, MedType, 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Sam", result.Value.DisplacedManagerDisplayName);
        Assert.DoesNotContain(profiles.Items, p => p.StaffUserId == sam);
        Assert.Equal(MedType, profiles.Items.Single(p => p.StaffUserId == jo).AppointmentTypeId);
        Assert.Contains(audit.Entries, e =>
            e.Action == Domain.Audit.AuditAction.StaffAccessChanged);
    }

    [Fact]
    public async Task Clearing_the_only_med_manager_is_allowed()
    {
        var (profiles, identities, unitOfWork, audit) = Parts();
        var sam = Guid.NewGuid();
        profiles.Items.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
        profiles.Items.Add(StaffAccessProfile.Create(sam, Role.Manager, MedType));
        var handler = new StaffScopeHandler(profiles, identities, unitOfWork, audit, profiles);

        var result = await handler.HandleAsync(
            new SetStaffScopeCommand(Admin, sam, null, 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.DisplacedManagerDisplayName);
        Assert.Null(profiles.Items.Single(p => p.StaffUserId == sam).AppointmentTypeId);
    }

    [Fact]
    public async Task Stale_version_returns_current_state()
    {
        var (profiles, identities, unitOfWork, audit) = Parts();
        var jo = Guid.NewGuid();
        profiles.Items.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
        profiles.Items.Add(StaffAccessProfile.Create(jo, Role.Manager, null));
        var handler = new StaffScopeHandler(profiles, identities, unitOfWork, audit, profiles);

        var result = await handler.HandleAsync(
            new SetStaffScopeCommand(Admin, jo, MedType, 99), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("version-conflict", result.Error.Code);
        Assert.Equal(1L, result.Error.Data!["currentVersion"]);
    }
}
