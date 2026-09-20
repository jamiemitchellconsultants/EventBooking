# 01e — Location-restricted invites and closed attendee transitions, edits 16 (Task 8)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs — 1/1

<!-- retirement-file: {"id":40,"file":"tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs","beforeSha":"60822ce2675d9cacb618415e5cd2affbead984dfbc2bf8d321a2ad47fb0e0896","afterSha":"fc749f2f08c6321f619d478e4ce99f83d4935808f5eefdd52605c009af221f1c","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Attendees;

public class DeleteAttendeeHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryBookingRepository _bookings = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryBookingAppointmentRepository _appointments;

    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly Attendee _attendee;

    private DeleteAttendeeHandler Handler => new(
        _attendees, _invites, _bookings, _events, _roles,
        new BookingCanceller(_appointments, new InMemoryEventCapacityRepository(_events), _audit),
        _audit,
        _unitOfWork);

    public DeleteAttendeeHandlerTests()
    {
        _appointments = new InMemoryBookingAppointmentRepository(_bookings);
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
            AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        _attendees.Add(_attendee);
    }

    [Fact]
    public async Task AAttendeeWithNothingOutstandingIsDeletedOutright()
    {
        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_attendees.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnconfirmedDeleteOfAAttendeeWithABookingIsRefusedWithAWarning()
    {
        GiveTheAttendeeABooking();

        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "Deleting this attendee will cancel 1 booking and 1 pending invite, and free the capacity they hold. Confirm to proceed.",
            result.Error.Message);
        Assert.Single(_attendees.Items);
    }

    [Fact]
    public async Task AConfirmedDeleteVoidsTheBookingAndGivesTheCapacityBack()
    {
        var eventItem = GiveTheAttendeeABooking();
        Assert.Equal(9, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_attendees.Items);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(8, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(BookingStatus.Cancelled, _bookings.Items.Single().Status);
        Assert.Equal(InviteStatus.Superseded, _invites.Items.Single().Status);
        Assert.True(_audit.Contains(AuditAction.BookingCancelled));
        Assert.True(_audit.Contains(AuditAction.CapacityIncremented));
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AConfirmedDeleteCascadesToTheActiveRecoveryBookingAndAuditsTheDeletion()
    {
        var eventItem = GiveTheAttendeeABooking();
        var recoveryEvent = GiveTheAttendeeARecoveryBooking();
        Assert.Equal(9, recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_attendees.Items);
        Assert.Equal(2, _bookings.Items.Count);
        Assert.All(_bookings.Items, booking => Assert.Equal(BookingStatus.Cancelled, booking.Status));
        Assert.Equal(10, recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.True(_audit.Contains(AuditAction.AttendeeDeleted));
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AnUnknownAttendeeIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task ANonCoordinatorIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Guid.NewGuid(), _attendee.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private Event GiveTheAttendeeARecoveryBooking()
    {
        var original = _bookings.Items.Single();
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 11), new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var recoveryEvent = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(recoveryEvent);

        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), _attendee.Id, original.Id, "recovery-hash", Now.AddDays(4),
            [recoveryEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(recoveryInvite);

        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoveryEvent.Id, "recovery-manage-hash", Now);
        _bookings.Add(recovery);
        recoveryInvite.MarkUsed();

        recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting));

        return recoveryEvent;
    }

    private Event GiveTheAttendeeABooking()
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(eventItem);

        var invite = Invite.CreateInitial(
            Guid.NewGuid(), _attendee.Id, "hash", Now.AddDays(4),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()], _attendee.RequiredAppointmentTypeIds, 0);
        _invites.Add(invite);

        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, "manage-hash", Now);
        _bookings.Add(booking);

        foreach (var typeId in _attendee.RequiredAppointmentTypeIds)
        {
            eventItem.CapacityFor(typeId).Decrement();
            _appointments.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
        }

        return eventItem;
    }
}
`````

## after — tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs — 1/1

<!-- retirement-file: {"id":40,"file":"tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs","beforeSha":"60822ce2675d9cacb618415e5cd2affbead984dfbc2bf8d321a2ad47fb0e0896","afterSha":"fc749f2f08c6321f619d478e4ce99f83d4935808f5eefdd52605c009af221f1c","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Attendees;

public class DeleteAttendeeHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryBookingRepository _bookings = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryBookingAppointmentRepository _appointments;

    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly Attendee _attendee;

    private DeleteAttendeeHandler Handler => new(
        _attendees, _invites, _bookings, _events, _roles,
        new BookingCanceller(_appointments, new InMemoryEventCapacityRepository(_events), _audit),
        _audit,
        _unitOfWork);

    public DeleteAttendeeHandlerTests()
    {
        _appointments = new InMemoryBookingAppointmentRepository(_bookings);
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "a.novak@mail.com",
            AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
            ProposalFixture.Now);
        _attendees.Add(_attendee);
    }

    [Fact]
    public async Task AAttendeeWithNothingOutstandingIsDeletedOutright()
    {
        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_attendees.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnconfirmedDeleteOfAAttendeeWithABookingIsRefusedWithAWarning()
    {
        GiveTheAttendeeABooking();

        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "Deleting this attendee will cancel 1 booking and 1 pending invite, and free the capacity they hold. Confirm to proceed.",
            result.Error.Message);
        Assert.Single(_attendees.Items);
    }

    [Fact]
    public async Task AConfirmedDeleteVoidsTheBookingAndGivesTheCapacityBack()
    {
        var eventItem = GiveTheAttendeeABooking();
        Assert.Equal(9, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_attendees.Items);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(8, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(BookingStatus.Cancelled, _bookings.Items.Single().Status);
        Assert.Equal(InviteStatus.Superseded, _invites.Items.Single().Status);
        Assert.True(_audit.Contains(AuditAction.BookingCancelled));
        Assert.True(_audit.Contains(AuditAction.CapacityIncremented));
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AConfirmedDeleteCascadesToTheActiveRecoveryBookingAndAuditsTheDeletion()
    {
        var eventItem = GiveTheAttendeeABooking();
        var recoveryEvent = GiveTheAttendeeARecoveryBooking();
        Assert.Equal(9, recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_attendees.Items);
        Assert.Equal(2, _bookings.Items.Count);
        Assert.All(_bookings.Items, booking => Assert.Equal(BookingStatus.Cancelled, booking.Status));
        Assert.Equal(10, recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.True(_audit.Contains(AuditAction.AttendeeDeleted));
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AnUnknownAttendeeIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task ANonCoordinatorIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Guid.NewGuid(), _attendee.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private Event GiveTheAttendeeARecoveryBooking()
    {
        var original = _bookings.Items.Single();
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 11), new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var recoveryEvent = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(recoveryEvent);

        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            _attendee.Id,
            original.Id,
            "recovery-hash",
            Now.AddDays(4),
            ProposalFixture.LocationId,
            null,
            [recoveryEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(recoveryInvite);

        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoveryEvent.Id, "recovery-manage-hash", Now);
        _bookings.Add(recovery);
        recoveryInvite.MarkUsed();

        recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting));

        return recoveryEvent;
    }

    private Event GiveTheAttendeeABooking()
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(eventItem);

        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            _attendee.Id,
            "hash",
            Now.AddDays(4),
            [ProposalFixture.LocationId],
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            _attendee.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);

        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, "manage-hash", Now);
        _bookings.Add(booking);

        foreach (var typeId in _attendee.RequiredAppointmentTypeIds)
        {
            eventItem.CapacityFor(typeId).Decrement();
            _appointments.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
        }

        return eventItem;
    }
}
`````

## before — tests/EventBooking.Application.Tests/Attendees/ImportAttendeesHandlerTests.cs — 1/1

<!-- retirement-file: {"id":41,"file":"tests/EventBooking.Application.Tests/Attendees/ImportAttendeesHandlerTests.cs","beforeSha":"8a57c7c930523e8fdfa170d070bebf3c06b70e5e30975cff72b9b71494ea99fa","afterSha":"19100779a59394d90c98413539c3c304ff4613004e65347929abbeb23c835873","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Common;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Tests.Attendees;

public class ImportAttendeesHandlerTests
{
    private const string Header = "name,email,attendee_group";
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Admin = Guid.Parse("c0000008-0000-0000-0000-000000000008");
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");

    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private ImportAttendeesHandler Handler => new(
        _attendees, _groups, new StaffAccessAuthorizer(_roles), _unitOfWork);

    public ImportAttendeesHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]));
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
            [AppointmentTypeIds.MedicalCheckUp]));
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
    }

    private Task<Result<AttendeeImportOutcome>> Import(string csv, Guid? actor = null) =>
        Handler.HandleAsync(
            new ImportAttendeesCommand(actor ?? Coordinator, csv), CancellationToken.None);

    [Fact]
    public async Task AGoodFileCreatesOneAttendeePerRow()
    {
        var result = await Import(
            $"""
             {Header}
             Amara Novak,a.novak@mail.com,CABIN_CREW
             B. Chen,b.chen@mail.com,engineering
             """);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Accepted);
        Assert.Equal(2, result.Value.ImportedCount);
        Assert.Empty(result.Value.Errors);

        Assert.Equal(2, _attendees.Items.Count);
        var novak = _attendees.Items.Single(c => c.Email == "a.novak@mail.com");
        Assert.Equal("Amara Novak", novak.Name);
        Assert.Equal(AttendeeStatus.NotYetInvited, novak.Status);
        Assert.Equal(AttendeeGroupIds.CabinCrew, novak.AttendeeGroupId);
        Assert.Equal(3, novak.Requirements.Count);
        var chen = _attendees.Items.Single(c => c.Email == "b.chen@mail.com");
        Assert.Equal(AttendeeGroupIds.Engineering, chen.AttendeeGroupId);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnAdminCannotImport()
    {
        var result = await Import($"{Header}\nAmara Novak,a.novak@mail.com,PILOTS", Admin);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Empty(_attendees.Items);
    }

    [Fact]
    public async Task AManagerCannotImport()
    {
        var result = await Import($"{Header}\nAmara Novak,a.novak@mail.com,PILOTS", Manager);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal("This staff profile cannot perform this operation.", result.Error.Message);
        Assert.Empty(_attendees.Items);
    }

    [Fact]
    public async Task OneBadRowRejectsTheWholeFile()
    {
        var result = await Import(
            $"""
             {Header}
             Amara Novak,a.novak@mail.com,PILOTS
             B. Chen,b.chen@mail.com,UNKNOWN_GROUP
             """);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Accepted);
        Assert.Equal(0, result.Value.ImportedCount);
        var error = Assert.Single(result.Value.Errors);
        Assert.Equal(3, error.LineNumber);
        Assert.Equal("UNKNOWN_GROUP is not a known attendee group code.", error.Message);
        Assert.Empty(_attendees.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ABadEmailIsCaughtByTheDomainAndReportedAgainstItsLine()
    {
        var result = await Import(
            $"""
             {Header}
             Amara Novak,a.novak@mail.com,PILOTS
             B. Chen,not-an-email,ENGINEERING
             """);

        Assert.False(result.Value.Accepted);
        var error = Assert.Single(result.Value.Errors);
        Assert.Equal(3, error.LineNumber);
        Assert.Equal("email is not a valid email address.", error.Message);
        Assert.Empty(_attendees.Items);
    }

    [Fact]
    public async Task AnEmailThatAlreadyExistsIsReportedAgainstItsLine()
    {
        _attendees.Add(Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
            _groups.Items.Single(group => group.Id == AttendeeGroupIds.Engineering)));

        var result = await Import($"{Header}\nAmara N,a.novak@mail.com,PILOTS");

        Assert.False(result.Value.Accepted);
        var error = Assert.Single(result.Value.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Equal("a.novak@mail.com is already a attendee.", error.Message);
        Assert.Single(_attendees.Items);
    }

    [Fact]
    public async Task AnEmptyUploadIsReportedNotCrashed()
    {
        var result = await Import("");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Accepted);
        Assert.Equal("The file is empty.", Assert.Single(result.Value.Errors).Message);
    }

    [Fact]
    public async Task EveryBadRowIsListedTogether()
    {
        var result = await Import(
            $"""
             {Header}
             ,a.novak@mail.com,PILOTS
             B. Chen,also-not-an-email,ENGINEERING
             C. Diallo,c.diallo@mail.com,CABIN_CREW
             """);

        Assert.False(result.Value.Accepted);
        Assert.Equal(2, result.Value.Errors.Count);
        Assert.Equal([2, 3], result.Value.Errors.Select(e => e.LineNumber));
    }
}
`````

## after — tests/EventBooking.Application.Tests/Attendees/ImportAttendeesHandlerTests.cs — 1/1

<!-- retirement-file: {"id":41,"file":"tests/EventBooking.Application.Tests/Attendees/ImportAttendeesHandlerTests.cs","beforeSha":"8a57c7c930523e8fdfa170d070bebf3c06b70e5e30975cff72b9b71494ea99fa","afterSha":"19100779a59394d90c98413539c3c304ff4613004e65347929abbeb23c835873","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Common;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Tests.Attendees;

public class ImportAttendeesHandlerTests
{
    private const string Header = "name,email,attendee_group";
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Admin = Guid.Parse("c0000008-0000-0000-0000-000000000008");
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");

    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private ImportAttendeesHandler Handler => new(
        _attendees, _groups, new StaffAccessAuthorizer(_roles), new FakeClock(), _unitOfWork);

    public ImportAttendeesHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]));
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
            [AppointmentTypeIds.MedicalCheckUp]));
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
    }

    private Task<Result<AttendeeImportOutcome>> Import(string csv, Guid? actor = null) =>
        Handler.HandleAsync(
            new ImportAttendeesCommand(actor ?? Coordinator, csv), CancellationToken.None);

    [Fact]
    public async Task AGoodFileCreatesOneAttendeePerRow()
    {
        var result = await Import(
            $"""
             {Header}
             Amara Novak,a.novak@mail.com,CABIN_CREW
             B. Chen,b.chen@mail.com,engineering
             """);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Accepted);
        Assert.Equal(2, result.Value.ImportedCount);
        Assert.Empty(result.Value.Errors);

        Assert.Equal(2, _attendees.Items.Count);
        var novak = _attendees.Items.Single(c => c.Email == "a.novak@mail.com");
        Assert.Equal("Amara Novak", novak.Name);
        Assert.Equal(AttendeeStatus.NotYetInvited, novak.Status);
        Assert.Equal(AttendeeGroupIds.CabinCrew, novak.AttendeeGroupId);
        Assert.Equal(3, novak.Requirements.Count);
        var chen = _attendees.Items.Single(c => c.Email == "b.chen@mail.com");
        Assert.Equal(AttendeeGroupIds.Engineering, chen.AttendeeGroupId);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnAdminCannotImport()
    {
        var result = await Import($"{Header}\nAmara Novak,a.novak@mail.com,PILOTS", Admin);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Empty(_attendees.Items);
    }

    [Fact]
    public async Task AManagerCannotImport()
    {
        var result = await Import($"{Header}\nAmara Novak,a.novak@mail.com,PILOTS", Manager);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal("This staff profile cannot perform this operation.", result.Error.Message);
        Assert.Empty(_attendees.Items);
    }

    [Fact]
    public async Task OneBadRowRejectsTheWholeFile()
    {
        var result = await Import(
            $"""
             {Header}
             Amara Novak,a.novak@mail.com,PILOTS
             B. Chen,b.chen@mail.com,UNKNOWN_GROUP
             """);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Accepted);
        Assert.Equal(0, result.Value.ImportedCount);
        var error = Assert.Single(result.Value.Errors);
        Assert.Equal(3, error.LineNumber);
        Assert.Equal("UNKNOWN_GROUP is not a known attendee group code.", error.Message);
        Assert.Empty(_attendees.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ABadEmailIsCaughtByTheDomainAndReportedAgainstItsLine()
    {
        var result = await Import(
            $"""
             {Header}
             Amara Novak,a.novak@mail.com,PILOTS
             B. Chen,not-an-email,ENGINEERING
             """);

        Assert.False(result.Value.Accepted);
        var error = Assert.Single(result.Value.Errors);
        Assert.Equal(3, error.LineNumber);
        Assert.Equal("email is not a valid email address.", error.Message);
        Assert.Empty(_attendees.Items);
    }

    [Fact]
    public async Task AnEmailThatAlreadyExistsIsReportedAgainstItsLine()
    {
        _attendees.Add(Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "a.novak@mail.com",
            _groups.Items.Single(group => group.Id == AttendeeGroupIds.Engineering),
            ProposalFixture.Now));

        var result = await Import($"{Header}\nAmara N,a.novak@mail.com,PILOTS");

        Assert.False(result.Value.Accepted);
        var error = Assert.Single(result.Value.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Equal("a.novak@mail.com is already a attendee.", error.Message);
        Assert.Single(_attendees.Items);
    }

    [Fact]
    public async Task AnEmptyUploadIsReportedNotCrashed()
    {
        var result = await Import("");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Accepted);
        Assert.Equal("The file is empty.", Assert.Single(result.Value.Errors).Message);
    }

    [Fact]
    public async Task EveryBadRowIsListedTogether()
    {
        var result = await Import(
            $"""
             {Header}
             ,a.novak@mail.com,PILOTS
             B. Chen,also-not-an-email,ENGINEERING
             C. Diallo,c.diallo@mail.com,CABIN_CREW
             """);

        Assert.False(result.Value.Accepted);
        Assert.Equal(2, result.Value.Errors.Count);
        Assert.Equal([2, 3], result.Value.Errors.Select(e => e.LineNumber));
    }
}
`````

## before — tests/EventBooking.Application.Tests/Attendees/ListAttendeesHandlerTests.cs — 1/1

<!-- retirement-file: {"id":42,"file":"tests/EventBooking.Application.Tests/Attendees/ListAttendeesHandlerTests.cs","beforeSha":"6b80452bae44ef392a3464a64c109a2331576c1a407fe3efb57cae375a6f696f","afterSha":"9781ba4b7fc46c09ac56112168f11ba3faf30a1b7ea61fb712faae43a5c34ea1","side":"before","part":1,"parts":1} -->

`````csharp
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
`````

## after — tests/EventBooking.Application.Tests/Attendees/ListAttendeesHandlerTests.cs — 1/1

<!-- retirement-file: {"id":42,"file":"tests/EventBooking.Application.Tests/Attendees/ListAttendeesHandlerTests.cs","beforeSha":"6b80452bae44ef392a3464a64c109a2331576c1a407fe3efb57cae375a6f696f","afterSha":"9781ba4b7fc46c09ac56112168f11ba3faf30a1b7ea61fb712faae43a5c34ea1","side":"after","part":1,"parts":1} -->

`````csharp
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
            Guid.NewGuid(),
            "A. Novak",
            "a.novak@mail.com",
            _groups.Items.Single(group => group.Id == AttendeeGroupIds.CabinCrew),
            ProposalFixture.Now));

        var chen = Attendee.Create(
            Guid.NewGuid(),
            "B. Chen",
            "b.chen@mail.com",
            _groups.Items.Single(group => group.Id == AttendeeGroupIds.GroundOperationsAgent),
            ProposalFixture.Now);
        chen.MarkInvited(ProposalFixture.Now);
        _attendees.Add(chen);

        var diallo = Attendee.Create(
            Guid.NewGuid(),
            "C. Diallo",
            "c.diallo@mail.com",
            _groups.Items.Single(group => group.Id == AttendeeGroupIds.GroundOperationsAgent),
            ProposalFixture.Now);
        diallo.MarkAwaitingAvailability(ProposalFixture.Now);
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
`````

## before — tests/EventBooking.Application.Tests/Attendees/SaveAttendeeHandlerTests.cs — 1/1

<!-- retirement-file: {"id":43,"file":"tests/EventBooking.Application.Tests/Attendees/SaveAttendeeHandlerTests.cs","beforeSha":"5392acf8b31232db9bd7432036aa629a4b79d09372c416b52b29fa8d066ba30d","afterSha":"0c410c89b38a16d15b27d155f956ef1d98e622e89a75b4af044d9e172951c4ac","side":"before","part":1,"parts":1} -->

`````csharp
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
        _unitOfWork);

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
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", Pilots));

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
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", Pilots);
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
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", Pilots);
        var second = Attendee.Create(
            Guid.NewGuid(), "B. Chen", "b.chen@mail.com", Pilots);
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
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", Pilots);
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
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", Pilots);
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
`````
