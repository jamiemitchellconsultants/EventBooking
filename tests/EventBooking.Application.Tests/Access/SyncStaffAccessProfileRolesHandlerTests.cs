using System.Text.Json;
using EventBooking.Application.Access;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventBooking.Application.Tests.Access;

public class SyncStaffAccessProfileRolesHandlerTests
{
    private readonly InMemoryStaffAccessProfileRepository _profiles = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private SyncStaffAccessProfileRolesHandler Handler() => new(
        _profiles, _unitOfWork, _audit, NullLogger<SyncStaffAccessProfileRolesHandler>.Instance);

    [Fact]
    public async Task MatchingRolesAreANoOpWrite()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(id, [Role.Coordinator], null));

        var result = await Handler().SyncAsync(id, new HashSet<Role> { Role.Coordinator }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task ANewIdentityWithClaimedRolesGetsAProfileWithNullScope()
    {
        var id = Guid.NewGuid();

        var result = await Handler().SyncAsync(id, new HashSet<Role> { Role.Manager }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsManager);
        Assert.Null(result.AppointmentTypeId);
        Assert.True(_audit.Contains(AuditAction.StaffRolesSynced));
    }

    [Fact]
    public async Task ANewIdentityWithNoClaimedRolesGetsNoProfile()
    {
        var result = await Handler().SyncAsync(Guid.NewGuid(), new HashSet<Role>(), CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task LosingAllClaimedRolesRemovesTheProfile()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(id, [Role.Coordinator], null));

        var result = await Handler().SyncAsync(id, new HashSet<Role>(), CancellationToken.None);

        Assert.Null(result);
        Assert.DoesNotContain(id, _profiles.Items.Select(p => p.StaffUserId));
        Assert.True(_audit.Contains(AuditAction.StaffRolesSynced));
    }

    [Fact]
    public async Task LosingTheScopedRoleClearsScopeButKeepsCoordinator()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            id, [Role.Coordinator, Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting));

        var result = await Handler().SyncAsync(id, new HashSet<Role> { Role.Coordinator }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsCoordinator);
        Assert.False(result.IsManager);
        Assert.Null(result.AppointmentTypeId);
    }

    [Fact]
    public async Task GainingAScopedRoleOnAnExistingProfilePreservesItsExistingScope()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            id, [Role.Manager], AppointmentTypeIds.MedicalCheckUp));

        var result = await Handler().SyncAsync(
            id, new HashSet<Role> { Role.Manager, Role.AppointmentStaff }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(AppointmentTypeIds.MedicalCheckUp, result!.AppointmentTypeId);
    }

    [Fact]
    public async Task RemovingAnUnscopedRolePreservesScopeWhileManagerRemains()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            id, [Role.Coordinator, Role.Manager], AppointmentTypeIds.MedicalCheckUp));

        var result = await Handler().SyncAsync(
            id, new HashSet<Role> { Role.Manager }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(AppointmentTypeIds.MedicalCheckUp, result!.AppointmentTypeId);
        Assert.Equal([Role.Manager], result.Roles);
    }

    [Fact]
    public async Task RemovingOneScopedRolePreservesScopeWhileTheOtherRemains()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            id, [Role.Manager, Role.AppointmentStaff], AppointmentTypeIds.UniformFitting));

        var result = await Handler().SyncAsync(
            id, new HashSet<Role> { Role.AppointmentStaff }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(AppointmentTypeIds.UniformFitting, result!.AppointmentTypeId);
        Assert.Equal([Role.AppointmentStaff], result.Roles);
    }

    [Fact]
    public async Task AChangedRoleSetRecordsExactSystemAuditState()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            id, [Role.Coordinator, Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting));

        await Handler().SyncAsync(
            id, new HashSet<Role> { Role.Coordinator }, CancellationToken.None);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditAction.StaffRolesSynced, entry.Action);
        Assert.Equal(ActorType.System, entry.ActorType);
        Assert.Null(entry.ActorId);
        using var details = JsonDocument.Parse(entry.Details!);
        Assert.Equal(
            ["Manager", "Coordinator"],
            details.RootElement.GetProperty("previous").GetProperty("Roles")
                .EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            details.RootElement.GetProperty("previous").GetProperty("AppointmentTypeId").GetGuid());
        Assert.Equal(
            ["Coordinator"],
            details.RootElement.GetProperty("current").GetProperty("Roles")
                .EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            JsonValueKind.Null,
            details.RootElement.GetProperty("current").GetProperty("AppointmentTypeId").ValueKind);
    }

    [Fact]
    public async Task LosingAllClaimedRolesAsTheLastAdminRefusesAndKeepsTheProfile()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(id, [Role.Admin], null));

        var result = await Handler().SyncAsync(id, new HashSet<Role>(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsAdmin);
        Assert.Contains(id, _profiles.Items.Select(p => p.StaffUserId));
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task DemotingTheLastAdminAwayFromAdminIsRefusedAndKeepsTheProfile()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(id, [Role.Admin], null));

        var result = await Handler().SyncAsync(
            id, new HashSet<Role> { Role.Coordinator }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsAdmin);
        Assert.False(result.IsCoordinator);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task LosingAllClaimedRolesWhenAnotherAdminRemainsStillRemovesTheProfile()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(id, [Role.Admin], null));
        _profiles.Add(StaffAccessProfile.Create(Guid.NewGuid(), [Role.Admin], null));

        var result = await Handler().SyncAsync(id, new HashSet<Role>(), CancellationToken.None);

        Assert.Null(result);
        Assert.DoesNotContain(id, _profiles.Items.Select(p => p.StaffUserId));
    }

    [Fact]
    public async Task AnInvalidClaimedCombinationIsRejectedWithoutOverwritingThePreviousProfile()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(id, [Role.Coordinator], null));

        var result = await Handler().SyncAsync(
            id, new HashSet<Role> { Role.Admin, Role.Manager }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsCoordinator);
        Assert.False(result.IsAdmin);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Empty(_audit.Entries);
    }
}
