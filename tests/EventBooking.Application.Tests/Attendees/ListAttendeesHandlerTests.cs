using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Tests.Attendees;

public class ListAttendeesHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();

    private ListAttendeesHandler Handler => new(
        _attendees, _groups, new StaffAccessAuthorizer(_roles));

    public ListAttendeesHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]));
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT",
            "Ground Operations Agent", true, [AppointmentTypeIds.MedicalCheckUp]));

        _attendees.Add(Attendee.Create(
            Guid.NewGuid(), "A. Novak", "a.novak@mail.com",
            _groups.Items.Single(group => group.Id == AttendeeGroupIds.CabinCrew)));

        var chen = Attendee.Create(
            Guid.NewGuid(), "B. Chen", "b.chen@mail.com",
            _groups.Items.Single(group => group.Id == AttendeeGroupIds.GroundOperationsAgent));
        chen.MarkInvited();
        _attendees.Add(chen);

        var diallo = Attendee.Create(
            Guid.NewGuid(), "C. Diallo", "c.diallo@mail.com",
            _groups.Items.Single(group => group.Id == AttendeeGroupIds.GroundOperationsAgent));
        diallo.MarkAwaitingAvailability();
        _attendees.Add(diallo);
    }

    [Fact]
    public async Task EveryAttendeeIsListedWithGroupAndDisplayStatus()
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Coordinator, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);

        var novak = result.Value.Single(c => c.Email == "a.novak@mail.com");
        Assert.Equal(AttendeeGroupIds.CabinCrew, novak.AttendeeGroupId);
        Assert.Equal("CABIN_CREW", novak.AttendeeGroupCode);
        Assert.Equal("Cabin Crew", novak.AttendeeGroupName);
        Assert.Equal(
            new[] { "DAT", "MED", "UNI" },
            novak.RequiredAppointmentTypes.Select(summary => summary.Code));
        Assert.Equal(AttendeeStatus.NotYetInvited, novak.Status);
        Assert.Equal("Not yet invited", novak.StatusDisplay);

        var diallo = result.Value.Single(c => c.Email == "c.diallo@mail.com");
        Assert.Equal(AttendeeGroupIds.GroundOperationsAgent, diallo.AttendeeGroupId);
        Assert.Equal("GROUND_OPERATIONS_AGENT", diallo.AttendeeGroupCode);
        Assert.Equal("Ground Operations Agent", diallo.AttendeeGroupName);

        Assert.Equal(
            "Invited (pending response)",
            result.Value.Single(c => c.Email == "b.chen@mail.com").StatusDisplay);
        Assert.Equal(
            "Awaiting availability",
            result.Value.Single(c => c.Email == "c.diallo@mail.com").StatusDisplay);
    }

    [Fact]
    public async Task TheListIsOrderedByName()
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Coordinator, null, null), CancellationToken.None);

        Assert.Equal(new[] { "A. Novak", "B. Chen", "C. Diallo" }, result.Value.Select(c => c.Name));
    }

    [Fact]
    public async Task FilteringByStatusNarrowsTheList()
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Coordinator, AttendeeStatus.Invited, null), CancellationToken.None);

        Assert.Equal("b.chen@mail.com", Assert.Single(result.Value).Email);
    }

    [Theory]
    [InlineData("diallo")]
    [InlineData("DIALLO")]
    [InlineData("c.diallo@mail.com")]
    public async Task SearchMatchesNameOrEmailCaseInsensitively(string search)
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Coordinator, null, search), CancellationToken.None);

        Assert.Equal("C. Diallo", Assert.Single(result.Value).Name);
    }

    [Fact]
    public async Task AnEmptySearchIsIgnored()
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Coordinator, null, "   "), CancellationToken.None);

        Assert.Equal(3, result.Value.Count);
    }

    [Fact]
    public async Task ANonCoordinatorIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Guid.NewGuid(), null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }
}
