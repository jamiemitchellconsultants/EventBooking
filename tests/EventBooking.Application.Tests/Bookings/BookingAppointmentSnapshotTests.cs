using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies booking confirmation snapshots required operational appointments atomically.</summary>
public sealed class BookingAppointmentSnapshotTests
{
    /// <summary>Verifies one Expected appointment is created for each current candidate requirement.</summary>
    [Fact]
    public async Task ConfirmationCreatesOneAppointmentPerRequirementBeforeTheSingleSave()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero));
        var tokens = new FakeTokenService();
        var candidates = new InMemoryCandidateRepository();
        var pilots = EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var candidate = Candidate.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            pilots);
        candidates.Add(candidate);
        candidate.MarkInvited();

        var slots = new InMemoryConfirmedSlotRepository();
        var selected = AddSlot(slots, new DateOnly(2026, 9, 8));
        var second = AddSlot(slots, new DateOnly(2026, 9, 9));
        var third = AddSlot(slots, new DateOnly(2026, 9, 10));
        var inviteId = Guid.NewGuid();
        var token = tokens.Issue(inviteId);
        var invites = new InMemoryInviteRepository();
        invites.Add(Invite.CreateInitial(
            inviteId,
            candidate.Id,
            token.TokenHash,
            clock.UtcNow.AddDays(4),
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
            candidates,
            slots,
            bookings,
            appointments,
            new InMemorySlotCapacityRepository(slots),
            new EligibleSlotFinder(slots, clock),
            tokens,
            deliveries,
            new RecordingAuditLogger(),
            unitOfWork,
            clock,
            new CandidatePortalOptions(
                "https://booking.example.com", "Head office", "help@example.com"));

        var result = await handler.HandleAsync(
            new ConfirmBookingCommand(token.Token, selected.Id),
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
            candidate.RequiredAppointmentTypeIds.OrderBy(value => value),
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
        var candidates = new InMemoryCandidateRepository();
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com",
            EmployeeGroup.Define(
                Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting]));
        candidates.Add(candidate);
        candidate.MarkInvited();

        var slots = new InMemoryConfirmedSlotRepository();
        var selected = AddSlot(slots, new DateOnly(2026, 9, 8));
        var inviteId = Guid.NewGuid();
        var token = tokens.Issue(inviteId);
        var invites = new InMemoryInviteRepository();
        invites.Add(Invite.CreateInitial(
            inviteId, candidate.Id, token.TokenHash, clock.UtcNow.AddDays(4),
            [selected.Id, AddSlot(slots, new DateOnly(2026, 9, 9)).Id,
                AddSlot(slots, new DateOnly(2026, 9, 10)).Id],
            snapshot, 0));

        var bookings = new InMemoryBookingRepository();
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ConfirmBookingHandler(
            invites,
            candidates,
            slots,
            bookings,
            appointments,
            new InMemorySlotCapacityRepository(slots),
            new EligibleSlotFinder(slots, clock),
            tokens,
            EmailDeliveryTestFactory.Create(
                new InMemoryEmailDeliveryRepository(),
                new RecordingEmailSender(),
                new FakeUnitOfWork(),
                clock),
            new RecordingAuditLogger(),
            unitOfWork,
            clock,
            new CandidatePortalOptions(
                "https://booking.example.com", "Head office", "help@example.com"));

        // Each snapshot size needs its matching group so confirmation proceeds.
        var group = snapshot.Count switch
        {
            1 => EmployeeGroup.Define(
                EmployeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            2 => EmployeeGroup.Define(
                EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
            _ => EmployeeGroup.Define(
                EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                    AppointmentTypeIds.UniformFitting]),
        };
        candidate.AssignEmployeeGroup(group);

        var result = await handler.HandleAsync(
            new ConfirmBookingCommand(token.Token, selected.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            snapshot.OrderBy(value => value),
            appointments.Items.Select(value => value.AppointmentTypeId).OrderBy(value => value));
    }

    private static ConfirmedSlot AddSlot(
        InMemoryConfirmedSlotRepository slots,
        DateOnly date)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(date, new TimeOnly(9, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
        slots.Add(slot);
        return slot;
    }
}
