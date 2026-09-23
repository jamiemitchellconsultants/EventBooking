using EventBooking.Application.Access;
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Tests.Events;

public class SharedEventAuthorizationTests
{
    private const string Csv =
        "date,startTime,DAT,MED,UNI\n2026-09-10,09:00,10,6,8";

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.Coordinator)]
    public async Task AdminAndCoordinatorCanImportEvents(Role role)
    {
        var user = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(user, [role], null));
        var events = new InMemoryEventRepository();
        var handler = new ImportEventsHandler(
            events,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger(),
            new FakeClock());

        var result = await handler.HandleAsync(
            new ImportEventsCommand(user, Csv), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Accepted);
        Assert.Single(events.Items);
    }

    [Fact]
    public async Task AppointmentStaffCannotImportEvents()
    {
        var user = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            user,
            [Role.AppointmentStaff],
            Domain.AppointmentTypes.AppointmentTypeIds.DrugAndAlcoholTesting));
        var events = new InMemoryEventRepository();
        var handler = new ImportEventsHandler(
            events,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger(),
            new FakeClock());

        var result = await handler.HandleAsync(
            new ImportEventsCommand(user, Csv), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(events.Items);
    }
}
