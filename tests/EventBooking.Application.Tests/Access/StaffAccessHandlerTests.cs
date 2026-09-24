using System.Text.Json;
using EventBooking.Application.Access;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Tests.Access;

public class StaffAccessHandlerTests
{
    private static readonly Guid Admin = Guid.NewGuid();
    private readonly InMemoryStaffAccessProfileRepository _profiles = new();
    private readonly InMemoryStaffIdentityRepository _identities = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    public StaffAccessHandlerTests()
    {
        _profiles.Add(StaffAccessProfile.Create(Admin, [Role.Admin], null));
    }

    [Fact]
    public async Task AdminSetsScopeOnAnExistingScopedRoleProfile()
    {
        var target = Guid.NewGuid();
        var profile = StaffAccessProfile.Create(
            target, [Role.Coordinator, Role.Manager], null); // transitional shape
        _profiles.Add(profile);
        var rolesBefore = profile.Roles.OrderBy(value => value).ToArray();

        var result = await Handler().ReplaceScopeAsync(
            new ReplaceStaffAccessProfileScopeCommand(
                Admin, target, AppointmentTypeIds.DrugAndAlcoholTesting, ExpectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, result.Value.Profile.AppointmentTypeId);
        Assert.Equal(rolesBefore, result.Value.Profile.Roles.OrderBy(value => value));
        var audit = Assert.Single(_audit.Entries, value => value.EntityId == target);
        Assert.Equal(AuditAction.StaffAccessChanged, audit.Action);
        using var details = JsonDocument.Parse(audit.Details!);
        Assert.Equal(
            details.RootElement.GetProperty("previous").GetProperty("Roles").GetRawText(),
            details.RootElement.GetProperty("current").GetProperty("Roles").GetRawText());
    }

    [Fact]
    public async Task AssigningScopeToANewManagerDisplacesTheFormerManagerOfThatType()
    {
        var former = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            former, [Role.Manager], AppointmentTypeIds.MedicalCheckUp));
        var incoming = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(incoming, [Role.Manager], null));

        var result = await Handler().ReplaceScopeAsync(
            new ReplaceStaffAccessProfileScopeCommand(
                Admin, incoming, AppointmentTypeIds.MedicalCheckUp, ExpectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(former, result.Value.FormerManagerStaffUserId);
        var formerProfile = _profiles.Items.Single(value => value.StaffUserId == former);
        Assert.True(formerProfile.IsManager);
        Assert.Null(formerProfile.AppointmentTypeId);
    }

    [Fact]
    public async Task DisplacingAManagerWhoIsAlsoAppointmentStaffStillClearsTheirSharedScope()
    {
        // Roles are identity-provider-owned in this handler, so a displaced Manager who is also
        // AppointmentStaff cannot keep the appointment-type scope: it is one shared field, and
        // preserving it would let them keep passing IsManager + AppointmentTypeId checks for the
        // type they were just displaced from.
        var former = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            former, [Role.Manager, Role.AppointmentStaff], AppointmentTypeIds.MedicalCheckUp));
        var incoming = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(incoming, [Role.Manager], null));

        var result = await Handler().ReplaceScopeAsync(
            new ReplaceStaffAccessProfileScopeCommand(
                Admin, incoming, AppointmentTypeIds.MedicalCheckUp, ExpectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(former, result.Value.FormerManagerStaffUserId);
        var formerProfile = _profiles.Items.Single(value => value.StaffUserId == former);
        Assert.True(formerProfile.IsManager);
        Assert.True(formerProfile.IsAppointmentStaff);
        Assert.Null(formerProfile.AppointmentTypeId);
    }

    [Fact]
    public async Task AStaleVersionCannotOverwriteANewerScope()
    {
        var target = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            target, [Role.Manager], AppointmentTypeIds.UniformFitting));

        var result = await Handler().ReplaceScopeAsync(
            new ReplaceStaffAccessProfileScopeCommand(
                Admin, target, AppointmentTypeIds.DrugAndAlcoholTesting, ExpectedVersion: 99),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task AnUnknownTargetReturnsNotFound()
    {
        var result = await Handler().ReplaceScopeAsync(
            new ReplaceStaffAccessProfileScopeCommand(
                Admin, Guid.NewGuid(), AppointmentTypeIds.UniformFitting, ExpectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task AManagedTypeCannotBeAbandonedByMovingItsManagerDirectly()
    {
        var target = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            target, [Role.Manager], AppointmentTypeIds.MedicalCheckUp));

        var result = await Handler().ReplaceScopeAsync(
            new ReplaceStaffAccessProfileScopeCommand(
                Admin, target, AppointmentTypeIds.UniformFitting, ExpectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task SettingScopeOnAProfileWithNoScopedRoleIsRejected()
    {
        var target = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(target, [Role.Coordinator], null));

        var result = await Handler().ReplaceScopeAsync(
            new ReplaceStaffAccessProfileScopeCommand(
                Admin, target, AppointmentTypeIds.UniformFitting, ExpectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ClearingScopeOnAnAppointmentStaffProfileSucceeds()
    {
        var target = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            target, [Role.AppointmentStaff], AppointmentTypeIds.UniformFitting));

        var result = await Handler().ClearScopeAsync(
            new ClearStaffAccessProfileScopeCommand(Admin, target, ExpectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var profile = _profiles.Items.Single(p => p.StaffUserId == target);
        Assert.True(profile.IsAppointmentStaff);
        Assert.Equal([Role.AppointmentStaff], profile.Roles);
        Assert.Null(profile.AppointmentTypeId);
        Assert.Equal(2, profile.Version);

        var audit = Assert.Single(_audit.Entries);
        Assert.Equal(AuditAction.StaffAccessChanged, audit.Action);
        Assert.Equal(ActorType.Staff, audit.ActorType);
        Assert.Equal(Admin.ToString(), audit.ActorId);
        using var details = JsonDocument.Parse(audit.Details!);
        Assert.Equal(
            details.RootElement.GetProperty("previous").GetProperty("Roles").GetRawText(),
            details.RootElement.GetProperty("current").GetProperty("Roles").GetRawText());
        Assert.Equal(
            AppointmentTypeIds.UniformFitting,
            details.RootElement.GetProperty("previous").GetProperty("AppointmentTypeId").GetGuid());
        Assert.Equal(
            JsonValueKind.Null,
            details.RootElement.GetProperty("current").GetProperty("AppointmentTypeId").ValueKind);
    }

    [Fact]
    public async Task ClearingScopeOnACurrentManagerIsBlocked()
    {
        var target = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            target, [Role.Manager], AppointmentTypeIds.UniformFitting));

        var result = await Handler().ClearScopeAsync(
            new ClearStaffAccessProfileScopeCommand(Admin, target, ExpectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ClearingAnAlreadyNullScopeIsRejected()
    {
        var target = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(target, [Role.Manager], null));

        var result = await Handler().ClearScopeAsync(
            new ClearStaffAccessProfileScopeCommand(Admin, target, ExpectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ACoordinatorCannotListOrMutateProfiles()
    {
        var coordinator = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(coordinator, [Role.Coordinator], null));
        var target = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(target, [Role.Coordinator], null));
        var handler = Handler();

        var list = await handler.ListAsync(coordinator, CancellationToken.None);
        var replace = await handler.ReplaceScopeAsync(
            new ReplaceStaffAccessProfileScopeCommand(
                coordinator, target, AppointmentTypeIds.UniformFitting, 1),
            CancellationToken.None);

        Assert.True(list.IsFailure);
        Assert.True(replace.IsFailure);
    }

    [Fact]
    public async Task StaffAccessListCarriesKnownStaffNumbersAndLeavesUnknownOnesNull()
    {
        var known = Guid.NewGuid();
        var unknown = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(known, Role.Coordinator, null));
        _profiles.Add(StaffAccessProfile.Create(unknown, Role.Coordinator, null));
        await _identities.UpsertAsync(
            known,
            new StaffId("u123456"),
            null,
            DateTimeOffset.Parse("2026-09-08T10:00:00Z"),
            CancellationToken.None);

        var result = await Handler().ListAsync(Admin, CancellationToken.None);

        Assert.Equal(new StaffId("U123456"),
            result.Value.Single(profile => profile.StaffUserId == known).StaffId);
        Assert.Null(result.Value.Single(profile => profile.StaffUserId == unknown).StaffId);
    }

    [Fact]
    public async Task AdminResolvesARecordedStaffNumberToItsProviderKey()
    {
        var target = Guid.NewGuid();
        await _identities.UpsertAsync(
            target,
            new StaffId("N654321"),
            null,
            DateTimeOffset.Parse("2026-09-08T10:00:00Z"),
            CancellationToken.None);

        var result = await Handler().ResolveIdentityAsync(
            Admin, "n654321", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(target, result.Value);
    }

    private StaffAccessHandler Handler() => new(
        _profiles,
        _identities,
        new StaffAccessAuthorizer(_profiles),
        _unitOfWork,
        _audit,
        new InMemoryAppointmentTypeRepository());

    /// <summary>Verifies an observed name reaches the listing while an unobserved one stays null.</summary>
    [Fact]
    public async Task ListAsyncPopulatesDisplayNameWhenKnown()
    {
        var named = Guid.NewGuid();
        var unnamed = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(named, Role.Coordinator, null));
        _profiles.Add(StaffAccessProfile.Create(unnamed, Role.Coordinator, null));
        await _identities.UpsertAsync(
            named,
            new StaffId("U000002"),
            "Dana Datson",
            DateTimeOffset.Parse("2026-09-08T10:00:00Z"),
            CancellationToken.None);
        await _identities.UpsertAsync(
            unnamed,
            new StaffId("U000003"),
            null,
            DateTimeOffset.Parse("2026-09-08T10:00:00Z"),
            CancellationToken.None);

        var result = await Handler().ListAsync(Admin, CancellationToken.None);

        var first = result.Value.Single(profile => profile.StaffUserId == named);
        var second = result.Value.Single(profile => profile.StaffUserId == unnamed);
        Assert.Equal("Dana Datson", first.DisplayName);
        Assert.Equal(new StaffId("U000002"), first.StaffId);
        Assert.Null(second.DisplayName);
        Assert.Equal(new StaffId("U000003"), second.StaffId);
    }
}
