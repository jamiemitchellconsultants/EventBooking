using EventBooking.Application.Access;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Access;

public class StaffAccessAuthorizerTests
{
    private readonly InMemoryStaffAccessProfileRepository _profiles = new();

    [Theory]
    [InlineData(Role.Admin, StaffCapability.ManageSettings, true)]
    [InlineData(Role.Admin, StaffCapability.ManageCandidates, false)]
    [InlineData(Role.Admin, StaffCapability.ViewCandidateDashboards, false)]
    [InlineData(Role.Admin, StaffCapability.ImportConfirmedSlots, true)]
    [InlineData(Role.Coordinator, StaffCapability.ManageCandidates, true)]
    [InlineData(Role.Coordinator, StaffCapability.ImportConfirmedSlots, true)]
    [InlineData(Role.Coordinator, StaffCapability.ManageSettings, false)]
    [InlineData(Role.Manager, StaffCapability.ManageSlotNegotiation, true)]
    [InlineData(Role.Manager, StaffCapability.ConductAppointments, true)]
    [InlineData(Role.AppointmentStaff, StaffCapability.ConductAppointments, true)]
    [InlineData(Role.AppointmentStaff, StaffCapability.CancelConfirmedSlot, false)]
    [InlineData(Role.Admin, StaffCapability.ViewSlotOperations, true)]
    [InlineData(Role.Coordinator, StaffCapability.ViewSlotOperations, true)]
    public async Task SingleRoleCapabilitiesMatchTheMatrix(
        Role role,
        StaffCapability capability,
        bool expected)
    {
        var staffUserId = Add(role);
        var result = await Authorizer().AuthorizeAsync(
            staffUserId, capability, null, CancellationToken.None);

        Assert.Equal(expected, result.IsSuccess);
    }

    [Fact]
    public async Task CombinedCoordinatorManagerGetsTheUnionAndTrustedScope()
    {
        var staffUserId = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            staffUserId,
            [Role.Coordinator, Role.Manager],
            AppointmentTypeIds.MedicalCheckUp));

        var candidate = await Authorizer().AuthorizeAsync(
            staffUserId, StaffCapability.ManageCandidates, null, CancellationToken.None);
        var manager = await Authorizer().AuthorizeAsync(
            staffUserId,
            StaffCapability.ManageSlotNegotiation,
            AppointmentTypeIds.MedicalCheckUp,
            CancellationToken.None);

        Assert.True(candidate.IsSuccess);
        Assert.True(manager.IsSuccess);
        Assert.Equal(AppointmentTypeIds.MedicalCheckUp, manager.Value.AppointmentTypeId);
    }

    [Fact]
    public async Task AScopedCapabilityRejectsAnotherAppointmentType()
    {
        var manager = Add(Role.Manager);

        var result = await Authorizer().AuthorizeAsync(
            manager,
            StaffCapability.ManageSlotNegotiation,
            AppointmentTypeIds.UniformFitting,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public async Task AnUnassignedIdentityIsDenied()
    {
        var result = await Authorizer().AuthorizeAsync(
            Guid.NewGuid(), StaffCapability.ViewSlotOperations, null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Theory]
    [InlineData(StaffCapability.ManageSettings)]
    [InlineData(StaffCapability.ManageStaffAccess)]
    [InlineData(StaffCapability.ImportConfirmedSlots)]
    [InlineData(StaffCapability.ManageCandidates)]
    [InlineData(StaffCapability.ViewCandidateDashboards)]
    [InlineData(StaffCapability.ViewCandidateAudit)]
    [InlineData(StaffCapability.ViewSlotAudit)]
    [InlineData(StaffCapability.ManageSlotNegotiation)]
    [InlineData(StaffCapability.ViewSlotOperations)]
    [InlineData(StaffCapability.CancelConfirmedSlot)]
    [InlineData(StaffCapability.ConductAppointments)]
    public async Task AScopedCapabilityIsDeniedWhenScopeIsNull(StaffCapability capability)
    {
        var profile = StaffAccessProfile.Create(Guid.NewGuid(), [Role.Manager], null);
        var repository = new InMemoryStaffAccessProfileRepository();
        repository.Items.Add(profile);
        var authorizer = new StaffAccessAuthorizer(repository);

        var result = await authorizer.AuthorizeAsync(
            profile.StaffUserId, capability, requiredAppointmentTypeId: null, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task AnUnscopedCoordinatorCapabilityIsStillGrantedWhenScopeIsNull()
    {
        var profile = StaffAccessProfile.Create(
            Guid.NewGuid(), [Role.Coordinator, Role.Manager], null);
        var repository = new InMemoryStaffAccessProfileRepository();
        repository.Items.Add(profile);
        var authorizer = new StaffAccessAuthorizer(repository);

        var result = await authorizer.AuthorizeAsync(
            profile.StaffUserId,
            StaffCapability.ManageCandidates,
            requiredAppointmentTypeId: null,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    private Guid Add(Role role)
    {
        var id = Guid.NewGuid();
        Guid? scope = role is Role.Manager or Role.AppointmentStaff
            ? AppointmentTypeIds.DrugAndAlcoholTesting
            : null;
        _profiles.Add(StaffAccessProfile.Create(id, role, scope));
        return id;
    }

    private StaffAccessAuthorizer Authorizer() => new(_profiles);
}
