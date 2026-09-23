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
