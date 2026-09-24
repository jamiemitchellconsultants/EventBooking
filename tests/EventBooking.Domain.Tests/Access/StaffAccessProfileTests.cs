using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.Access;

public class StaffAccessProfileTests
{
    public static TheoryData<Role[], Guid?> ValidShapes => new()
    {
        { [Role.Admin], null },
        { [Role.Coordinator], null },
        { [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting },
        { [Role.AppointmentStaff], AppointmentTypeIds.MedicalCheckUp },
        { [Role.Coordinator, Role.Manager], AppointmentTypeIds.UniformFitting },
        { [Role.Coordinator, Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting },
        { [Role.Manager, Role.AppointmentStaff], AppointmentTypeIds.MedicalCheckUp },
        { [Role.Coordinator, Role.Manager, Role.AppointmentStaff], AppointmentTypeIds.UniformFitting },
    };

    [Theory]
    [MemberData(nameof(ValidShapes))]
    public void EveryApprovedShapeIsAccepted(Role[] roles, Guid? scope)
    {
        var profile = StaffAccessProfile.Create(Guid.NewGuid(), roles, scope);

        Assert.True(roles.ToHashSet().SetEquals(profile.Roles));
        Assert.Equal(scope, profile.AppointmentTypeId);
        Assert.Equal(1, profile.Version);
        Assert.True(profile.IsValid());
    }

    [Theory]
    [MemberData(nameof(InvalidShapes))]
    public void EveryForbiddenShapeIsRejected(Role[] roles, Guid? scope)
    {
        Assert.Throws<DomainException>(() =>
            StaffAccessProfile.Create(Guid.NewGuid(), roles, scope));
    }

    public static TheoryData<Role[], Guid?> InvalidShapes => new()
    {
        { [], null },
        { [Role.Admin, Role.Coordinator], null },
        { [Role.Admin, Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting },
        { [Role.Coordinator], AppointmentTypeIds.DrugAndAlcoholTesting },
        { [(Role)999], null },
    };

    [Fact]
    public void AdminManagedScopeIsAccepted()
    {
        var managed = Guid.NewGuid();

        var profile = StaffAccessProfile.Create(Guid.NewGuid(), [Role.Manager], managed);

        Assert.Equal(managed, profile.AppointmentTypeId);
        Assert.True(profile.IsValid());
    }

    [Fact]
    public void EmptyStaffIdentifierIsRejected()
    {
        Assert.Throws<DomainException>(() =>
            StaffAccessProfile.Create(Guid.Empty, [Role.Coordinator], null));
    }

    public static TheoryData<Role[], Guid?> TransitionalShapesWithoutScope => new()
    {
        { [Role.Manager], null },
        { [Role.AppointmentStaff], null },
        { [Role.Coordinator, Role.Manager], null },
        { [Role.Coordinator, Role.AppointmentStaff], null },
        { [Role.Manager, Role.AppointmentStaff], null },
        { [Role.Coordinator, Role.Manager, Role.AppointmentStaff], null },
    };

    [Theory]
    [MemberData(nameof(TransitionalShapesWithoutScope))]
    public void EveryTransitionalShapeIsAcceptedWithNullScope(Role[] roles, Guid? scope)
    {
        var profile = StaffAccessProfile.Create(Guid.NewGuid(), roles, scope);

        Assert.True(roles.ToHashSet().SetEquals(profile.Roles));
        Assert.Null(profile.AppointmentTypeId);
        Assert.True(profile.IsValid());
    }

    [Fact]
    public void ScopeIsStillForbiddenWithoutAScopedRole()
    {
        Assert.Throws<DomainException>(() => StaffAccessProfile.Create(
            Guid.NewGuid(), [Role.Coordinator], AppointmentTypeIds.DrugAndAlcoholTesting));
    }

    [Fact]
    public void ReplacingAProfileChangesTheWholeShapeAndAdvancesVersion()
    {
        var profile = StaffAccessProfile.Create(Guid.NewGuid(), [Role.Coordinator], null);

        profile.Replace(
            [Role.Coordinator, Role.Manager, Role.AppointmentStaff],
            AppointmentTypeIds.MedicalCheckUp);

        Assert.True(new HashSet<Role>
        {
            Role.Manager,
            Role.Coordinator,
            Role.AppointmentStaff,
        }.SetEquals(profile.Roles));
        Assert.Equal(AppointmentTypeIds.MedicalCheckUp, profile.AppointmentTypeId);
        Assert.Equal(2, profile.Version);
    }

    [Fact]
    public void RejectedReplacementLeavesTheOldProfileUntouched()
    {
        var profile = StaffAccessProfile.Create(Guid.NewGuid(), [Role.Coordinator], null);

        Assert.Throws<DomainException>(() =>
            profile.Replace([Role.Admin, Role.Coordinator], null));

        Assert.True(new HashSet<Role> { Role.Coordinator }.SetEquals(profile.Roles));
        Assert.Null(profile.AppointmentTypeId);
        Assert.Equal(1, profile.Version);
    }

    [Fact]
    public void RemovingManagerPreservesAppointmentStaffAndItsSharedScope()
    {
        var profile = StaffAccessProfile.Create(
            Guid.NewGuid(),
            [Role.Coordinator, Role.Manager, Role.AppointmentStaff],
            AppointmentTypeIds.DrugAndAlcoholTesting);

        var empty = profile.RemoveManagerRole();

        Assert.False(empty);
        Assert.True(new HashSet<Role>
        {
            Role.Coordinator,
            Role.AppointmentStaff,
        }.SetEquals(profile.Roles));
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, profile.AppointmentTypeId);
        Assert.Equal(2, profile.Version);
    }

    [Fact]
    public void RemovingTheOnlyManagerReportsThatTheProfileIsEmpty()
    {
        var profile = StaffAccessProfile.Create(
            Guid.NewGuid(), [Role.Manager], AppointmentTypeIds.UniformFitting);

        Assert.True(profile.RemoveManagerRole());
        Assert.Empty(profile.Roles);
        Assert.Null(profile.AppointmentTypeId);
        Assert.Equal(2, profile.Version);
    }
}
