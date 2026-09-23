using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies booking confirmation snapshots required operational appointments atomically.</summary>
public sealed class BookingAppointmentSnapshotTests
{
    /// <summary>Verifies one Expected appointment is created for each current attendee requirement.</summary>
    [Fact]
    public async Task ConfirmationCreatesOneAppointmentPerRequirementBeforeTheSingleSave()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero));
        var tokens = new FakeTokenService();
        var attendees = new InMemoryAttendeeRepository();
        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            pilots,
            ProposalFixture.Now);
        attendees.Add(attendee);
        attendee.MarkInvited(ProposalFixture.Now);

        var events = new InMemoryEventRepository();
        var selected = AddEvent(events, new DateOnly(2026, 9, 8));
        var second = AddEvent(events, new DateOnly(2026, 9, 9));
        var third = AddEvent(events, new DateOnly(2026, 9, 10));
        var inviteId = Guid.NewGuid();
        var token = tokens.Issue(TokenPurpose.Book, inviteId, Invite.InitialTokenVersion);
        var invites = new InMemoryInviteRepository();
        invites.Add(Invite.CreateInitial(
            inviteId,
            attendee.Id,
            clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            [selected.Id, second.Id, third.Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            0));

        var bookings = new InMemoryBookingRepository();
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        var unitOfWork = new FakeUnitOfWork();
        var deliveries = EmailDeliveryTestFactory.Create(
            new InMemoryEmailDeliveryRepository(),
            new RecordingEmailSender(),
            new FakeUnitOfWork(),
            clock);
        var handler = new ConfirmBookingHandler(
            invites,
            attendees,
            events,
            bookings,
            appointments,
            new InMemoryEventCapacityRepository(events),
            new EligibleEventFinder(events, clock),
            tokens,
            deliveries,
            new RecordingAuditLogger(),
            unitOfWork,
            clock,
            new AttendeePortalOptions(
                "https://booking.example.com", "help@example.com"));

        var result = await handler.HandleAsync(
            new ConfirmBookingCommand(token, selected.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, appointments.Items.Count);
        Assert.All(appointments.Items, value =>
        {
            Assert.Equal(result.Value.BookingId, value.BookingId);
            Assert.Equal(BookingAppointmentStatus.Expected, value.Status);
            Assert.Equal(1, value.Version);
        });
        Assert.Equal(
            attendee.RequiredAppointmentTypeIds.OrderBy(value => value),
            appointments.Items.Select(value => value.AppointmentTypeId).OrderBy(value => value));
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(1, unitOfWork.CommitCount);
    }

    /// <summary>Verifies one-, two-, and three-type snapshots each produce their exact set.</summary>
    [Theory]
    [InlineData("MED")]
    [InlineData("DAT,UNI")]
    [InlineData("DAT,MED,UNI")]
    public async Task ConfirmationCreatesTheExactSnapshotSet(string codes)
    {
        var byCode = new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            ["DAT"] = AppointmentTypeIds.DrugAndAlcoholTesting,
            ["MED"] = AppointmentTypeIds.MedicalCheckUp,
            ["UNI"] = AppointmentTypeIds.UniformFitting,
        };
        var snapshot = codes.Split(',').Select(code => byCode[code]).ToList();

        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero));
        var tokens = new FakeTokenService();
        var attendees = new InMemoryAttendeeRepository();
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            AttendeeGroup.Define(
                Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting]),
            ProposalFixture.Now);
        attendees.Add(attendee);
        attendee.MarkInvited(ProposalFixture.Now);

        var events = new InMemoryEventRepository();
        var selected = AddEvent(events, new DateOnly(2026, 9, 8));
        var inviteId = Guid.NewGuid();
        var token = tokens.Issue(TokenPurpose.Book, inviteId, Invite.InitialTokenVersion);
        var invites = new InMemoryInviteRepository();
        invites.Add(Invite.CreateInitial(
            inviteId,
            attendee.Id,
            clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            [selected.Id, AddEvent(events, new DateOnly(2026, 9, 9)).Id,
                AddEvent(events, new DateOnly(2026, 9, 10)).Id],
            snapshot,
            0));

        var bookings = new InMemoryBookingRepository();
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ConfirmBookingHandler(
            invites,
            attendees,
            events,
            bookings,
            appointments,
            new InMemoryEventCapacityRepository(events),
            new EligibleEventFinder(events, clock),
            tokens,
            EmailDeliveryTestFactory.Create(
                new InMemoryEmailDeliveryRepository(),
                new RecordingEmailSender(),
                new FakeUnitOfWork(),
                clock),
            new RecordingAuditLogger(),
            unitOfWork,
            clock,
            new AttendeePortalOptions(
                "https://booking.example.com", "help@example.com"));

        // Each snapshot size needs its matching group so confirmation proceeds.
        var group = snapshot.Count switch
        {
            1 => AttendeeGroup.Define(
                AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            2 => AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
            _ => AttendeeGroup.Define(
                AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                    AppointmentTypeIds.UniformFitting]),
        };
        attendee.AssignAttendeeGroup(group);

        var result = await handler.HandleAsync(
            new ConfirmBookingCommand(token, selected.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            snapshot.OrderBy(value => value),
            appointments.Items.Select(value => value.AppointmentTypeId).OrderBy(value => value));
    }

    private static Event AddEvent(
        InMemoryEventRepository events,
        DateOnly date)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        events.Add(eventItem);
        return eventItem;
    }
}
