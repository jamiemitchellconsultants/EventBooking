using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Tests.Attendees;

public class SaveAttendeeHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");

    private static readonly AttendeeGroup Pilots = AttendeeGroup.Define(
        AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
    private static readonly AttendeeGroup Engineering = AttendeeGroup.Define(
        AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
        [AppointmentTypeIds.MedicalCheckUp]);

    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemoryBookingRepository _bookings = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private SaveAttendeeHandler Handler => new(
        _attendees,
        _groups,
        new InMemoryInviteRepository(),
        _bookings,
        new StaffAccessAuthorizer(_roles),
        new RecordingAuditLogger(),
        new FakeClock(),
        _unitOfWork,
        new InMemoryAppointmentTypeRepository());

    public SaveAttendeeHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _groups.Items.Add(Pilots);
        _groups.Items.Add(Engineering);
    }

    [Fact]
    public async Task ACoordinatorCanCreateAAttendee()
    {
        var result = await Handler.CreateAsync(
            new CreateAttendeeCommand(
                Coordinator, "Amara Novak", "a.novak@mail.com", AttendeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var attendee = Assert.Single(_attendees.Items);
        Assert.Equal(result.Value, attendee.Id);
        Assert.Equal("a.novak@mail.com", attendee.Email);
        Assert.Equal(AttendeeGroupIds.Pilots, attendee.AttendeeGroupId);
        Assert.Equal(2, attendee.Requirements.Count);
        Assert.Equal(AttendeeStatus.NotYetInvited, attendee.Status);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AManagerCannotCreateAAttendee()
    {
        var result = await Handler.CreateAsync(
            new CreateAttendeeCommand(
                Manager, "Amara Novak", "a.novak@mail.com", AttendeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Theory]
    [InlineData(null, "attendee_group_required")]
    public async Task AnAbsentGroupIsRequiredOnCreate(Guid? groupId, string code)
    {
        var result = await Handler.CreateAsync(
            new CreateAttendeeCommand(Coordinator, "Amara Novak", "a.novak@mail.com", groupId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Error.Code);
        Assert.Empty(_attendees.Items);
    }

    [Fact]
    public async Task AnUnknownGroupIsRejectedOnCreate()
    {
        var result = await Handler.CreateAsync(
            new CreateAttendeeCommand(
                Coordinator, "Amara Novak", "a.novak@mail.com", Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("attendee_group_unknown", result.Error.Code);
        Assert.Empty(_attendees.Items);
    }

    [Fact]
    public async Task ADuplicateEmailIsAConflict()
    {
        _attendees.Add(Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "a.novak@mail.com",
            Pilots,
            ProposalFixture.Now));

        var result = await Handler.CreateAsync(
            new CreateAttendeeCommand(
                Coordinator, "Someone Else", "A.Novak@Mail.com", AttendeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("a.novak@mail.com is already a attendee.", result.Error.Message);
        Assert.Single(_attendees.Items);
    }

    [Fact]
    public async Task DomainValidationSurfacesAsAValidationFailure()
    {
        var result = await Handler.CreateAsync(
            new CreateAttendeeCommand(
                Coordinator, "Amara Novak", "nope", AttendeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("email is not a valid email address.", result.Error.Message);
    }

    [Fact]
    public async Task UpdatingChangesTheDetailsAndTheGroup()
    {
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "a.novak@mail.com",
            Pilots,
            ProposalFixture.Now);
        _attendees.Add(attendee);

        var result = await Handler.UpdateAsync(
            new UpdateAttendeeCommand(
                Coordinator, attendee.Id, "Amara N. Novak", "amara@mail.com",
                AttendeeGroupIds.Engineering),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Amara N. Novak", attendee.Name);
        Assert.Equal("amara@mail.com", attendee.Email);
        Assert.Equal(AttendeeGroupIds.Engineering, attendee.AttendeeGroupId);
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], attendee.RequiredAppointmentTypeIds);
    }

    [Fact]
    public async Task UpdatingToAnotherAttendeesEmailIsAConflict()
    {
        var first = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "a.novak@mail.com",
            Pilots,
            ProposalFixture.Now);
        var second = Attendee.Create(
            Guid.NewGuid(),
            "B. Chen",
            "b.chen@mail.com",
            Pilots,
            ProposalFixture.Now);
        _attendees.Add(first);
        _attendees.Add(second);

        var result = await Handler.UpdateAsync(
            new UpdateAttendeeCommand(
                Coordinator, second.Id, "B. Chen", "a.novak@mail.com",
                AttendeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("b.chen@mail.com", second.Email);
    }

    [Fact]
    public async Task KeepingTheSameEmailOnUpdateIsNotAConflict()
    {
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "a.novak@mail.com",
            Pilots,
            ProposalFixture.Now);
        _attendees.Add(attendee);

        var result = await Handler.UpdateAsync(
            new UpdateAttendeeCommand(
                Coordinator, attendee.Id, "Amara Novak", "a.novak@mail.com",
                AttendeeGroupIds.Engineering),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdatingAnUnknownAttendeeIsNotFound()
    {
        var result = await Handler.UpdateAsync(
            new UpdateAttendeeCommand(
                Coordinator, Guid.NewGuid(), "X", "x@mail.com", AttendeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task UpdatingWithoutAGroupIsRequired()
    {
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "a.novak@mail.com",
            Pilots,
            ProposalFixture.Now);
        _attendees.Add(attendee);

        var result = await Handler.UpdateAsync(
            new UpdateAttendeeCommand(
                Coordinator, attendee.Id, "Amara Novak", "a.novak@mail.com", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("attendee_group_required", result.Error.Code);
        Assert.Equal(AttendeeGroupIds.Pilots, attendee.AttendeeGroupId);
    }
}
