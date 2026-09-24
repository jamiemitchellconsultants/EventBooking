using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Tests.Attendees;

/// <summary>Verifies Attendee application inputs resolve and derive Attendee Group mappings.</summary>
public sealed class AttendeeAttendeeGroupFlowTests
{
    private static readonly Guid Coordinator = Guid.NewGuid();
    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemoryStaffAccessProfileRepository _profiles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    /// <summary>Initializes one Coordinator and the approved reference groups.</summary>
    public AttendeeAttendeeGroupFlowTests()
    {
        _profiles.Add(StaffAccessProfile.Create(Coordinator, [Role.Coordinator], null));
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
    }

    /// <summary>Create stores the group and derives requirements without requirement input.</summary>
    [Fact]
    public async Task CreateDerivesThePersistedGroupMapping()
    {
        var handler = new SaveAttendeeHandler(
            _attendees,
            _groups,
            new InMemoryInviteRepository(),
            new InMemoryBookingRepository(),
            new StaffAccessAuthorizer(_profiles),
            new RecordingAuditLogger(),
            new FakeClock(),
            _unitOfWork,
            new InMemoryAppointmentTypeRepository());

        var result = await handler.CreateAsync(
            new CreateAttendeeCommand(
                Coordinator, "Amara Novak", "amara@example.com", AttendeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var attendee = Assert.Single(_attendees.Items);
        Assert.Equal(AttendeeGroupIds.Pilots, attendee.AttendeeGroupId);
        Assert.Equal(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            attendee.RequiredAppointmentTypeIds);
    }

    /// <summary>
    /// The audit detail names the old and new requirements from one read of the types, not a
    /// read per requirement set.
    /// </summary>
    [Fact]
    public async Task AGroupChangeReadsTheAppointmentTypesOnce()
    {
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
            [AppointmentTypeIds.MedicalCheckUp]));
        var types = new InMemoryAppointmentTypeRepository();
        var handler = new SaveAttendeeHandler(
            _attendees,
            _groups,
            new InMemoryInviteRepository(),
            new InMemoryBookingRepository(),
            new StaffAccessAuthorizer(_profiles),
            new RecordingAuditLogger(),
            new FakeClock(),
            _unitOfWork,
            types);
        var created = await handler.CreateAsync(
            new CreateAttendeeCommand(
                Coordinator, "Amara Novak", "amara@example.com", AttendeeGroupIds.Pilots),
            CancellationToken.None);
        var before = types.Reads;

        var updated = await handler.UpdateAsync(
            new UpdateAttendeeCommand(
                Coordinator, created.Value, "Amara Novak", "amara@example.com",
                AttendeeGroupIds.Engineering),
            CancellationToken.None);

        Assert.True(updated.IsSuccess);
        Assert.Equal(1, types.Reads - before);
    }

    /// <summary>Unknown and absent groups return stable validation without saving.</summary>
    [Theory]
    [InlineData(null, "attendee_group_required")]
    public async Task InvalidGroupDoesNotCreateAttendee(Guid? groupId, string code)
    {
        var handler = new SaveAttendeeHandler(
            _attendees,
            _groups,
            new InMemoryInviteRepository(),
            new InMemoryBookingRepository(),
            new StaffAccessAuthorizer(_profiles),
            new RecordingAuditLogger(),
            new FakeClock(),
            _unitOfWork,
            new InMemoryAppointmentTypeRepository());

        var result = await handler.CreateAsync(
            new CreateAttendeeCommand(Coordinator, "Amara", "amara@example.com", groupId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Error.Code);
        Assert.Empty(_attendees.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    /// <summary>The new CSV contract carries one case-insensitive Attendee Group code.</summary>
    [Fact]
    public void CsvParsesAttendeeGroupRatherThanAppointmentTypes()
    {
        var parsed = AttendeeCsvParser.Parse(
            "name,email,attendee_group\nAmara Novak,amara@example.com, pilots ");

        Assert.Empty(parsed.Errors);
        Assert.Equal("PILOTS", Assert.Single(parsed.Rows).AttendeeGroupCode);
        var old = AttendeeCsvParser.Parse(
            "name,email,appointment_types\nAmara Novak,amara@example.com,DAT;UNI");
        Assert.Equal(1, Assert.Single(old.Errors).LineNumber);
    }
}
