using EventBooking.Application.Access;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventBooking.Application.Tests.Access;

public class MeHandlerTests
{
    [Fact]
    public async Task AnUnassignedCallerGetsAnEmptyRoleSet()
    {
        var me = await Handler(new InMemoryStaffAccessProfileRepository())
            .GetAsync(Guid.NewGuid(), new StaffId("U123456"), new HashSet<Role>(), CancellationToken.None);

        Assert.Equal(new StaffId("U123456"), me.StaffId);
        Assert.Empty(me.Roles);
        Assert.Null(me.AppointmentTypeId);
        Assert.Null(me.AppointmentTypeName);
    }

    [Fact]
    public async Task CombinedRolesAreOrderedAndTheSharedScopeIsNamed()
    {
        var staffUserId = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staffUserId,
            [Role.AppointmentStaff, Role.Coordinator, Role.Manager],
            AppointmentTypeIds.DrugAndAlcoholTesting));

        var me = await Handler(profiles).GetAsync(
            staffUserId,
            new StaffId("N654321"),
            new HashSet<Role> { Role.AppointmentStaff, Role.Coordinator, Role.Manager },
            CancellationToken.None);

        Assert.Equal(new StaffId("N654321"), me.StaffId);
        Assert.Equal(
            [Role.Manager, Role.Coordinator, Role.AppointmentStaff],
            me.Roles);
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, me.AppointmentTypeId);
        Assert.Equal("Drug & Alcohol Testing", me.AppointmentTypeName);
    }

    private static MeHandler Handler(InMemoryStaffAccessProfileRepository profiles) =>
        new(
            new InMemoryAppointmentTypeRepository(),
            new SyncStaffAccessProfileRolesHandler(
                profiles,
                new FakeUnitOfWork(),
                new RecordingAuditLogger(),
                NullLogger<SyncStaffAccessProfileRolesHandler>.Instance));
}
