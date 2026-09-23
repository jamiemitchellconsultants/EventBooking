using EventBooking.Application.Access;
using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Tests.Slots;

public class SharedSlotAuthorizationTests
{
    private const string Csv =
        "date,startTime,DAT,MED,UNI\n2026-09-10,09:00,10,6,8";

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.Coordinator)]
    public async Task AdminAndCoordinatorCanImportConfirmedSlots(Role role)
    {
        var user = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(user, [role], null));
        var slots = new InMemoryConfirmedSlotRepository();
        var handler = new ImportConfirmedSlotsHandler(
            slots,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger(),
            new FakeClock());

        var result = await handler.HandleAsync(
            new ImportConfirmedSlotsCommand(user, Csv), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Accepted);
        Assert.Single(slots.Items);
    }

    [Fact]
    public async Task AppointmentStaffCannotImportConfirmedSlots()
    {
        var user = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            user,
            [Role.AppointmentStaff],
            Domain.AppointmentTypes.AppointmentTypeIds.DrugAndAlcoholTesting));
        var slots = new InMemoryConfirmedSlotRepository();
        var handler = new ImportConfirmedSlotsHandler(
            slots,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger(),
            new FakeClock());

        var result = await handler.HandleAsync(
            new ImportConfirmedSlotsCommand(user, Csv), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(slots.Items);
    }
}
