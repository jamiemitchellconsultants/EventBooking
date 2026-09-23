using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Fakes;

public class FakesSelfTests
{
    [Fact]
    public void TheClockCanBeMoved()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        clock.Advance(TimeSpan.FromDays(5));

        Assert.Equal(new DateOnly(2026, 9, 8), clock.TodayAtHeadOffice);
    }

    [Fact]
    public async Task TheCandidateRepositoryFiltersByStatus()
    {
        var repository = new InMemoryCandidateRepository();
        var uniformOnly = EmployeeGroup.Define(
            Guid.NewGuid(), "UNI_ONLY", "UNI only", true, [AppointmentTypeIds.UniformFitting]);
        var invited = Candidate.Create(
            Guid.NewGuid(), "B. Chen", "b.chen@mail.com", uniformOnly);
        invited.MarkInvited();
        repository.Add(invited);
        repository.Add(Candidate.Create(
            Guid.NewGuid(), "A. Novak", "a.novak@mail.com", uniformOnly));

        var result = await repository.ListAsync(CandidateStatus.Invited, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("b.chen@mail.com", result[0].Email);
    }

    [Fact]
    public async Task TheSlotRepositoryHidesCancelledAndPastSlots()
    {
        var repository = new InMemoryConfirmedSlotRepository();
        repository.Add(SlotFor(new DateOnly(2026, 9, 1)));
        var cancelled = SlotFor(new DateOnly(2026, 9, 20));
        cancelled.Cancel();
        repository.Add(cancelled);
        repository.Add(SlotFor(new DateOnly(2026, 9, 21)));

        var result = await repository.ListActiveAsync(new DateOnly(2026, 9, 3), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 9, 21), result[0].Window.Date);
    }

    [Fact]
    public async Task TheCapacityRepositoryReturnsRowsInAppointmentTypeOrder()
    {
        var slots = new InMemoryConfirmedSlotRepository();
        var slot = SlotFor(new DateOnly(2026, 9, 21));
        slots.Add(slot);
        var capacities = new InMemorySlotCapacityRepository(slots);

        var locked = await capacities
            .LockForUpdateAsync(slot.Id, AppointmentTypeIds.All, CancellationToken.None);

        Assert.Equal(3, locked.Count);
        Assert.Equal(
            locked.Select(c => c.AppointmentTypeId).OrderBy(id => id),
            locked.Select(c => c.AppointmentTypeId));
        Assert.Equal(1, capacities.LockCallCount);
    }

    [Fact]
    public void TheTokenServiceRoundTripsAnIdentifier()
    {
        var service = new FakeTokenService();
        var id = Guid.NewGuid();

        var issued = service.Issue(id);

        Assert.True(service.TryRead(issued.Token, out var read));
        Assert.Equal(id, read);
        Assert.Equal(issued.TokenHash, service.Hash(issued.Token));
        Assert.False(service.TryRead("nonsense", out _));
    }

    [Fact]
    public void TheAuditLoggerRecordsWhatItIsGiven()
    {
        var logger = new RecordingAuditLogger();

        logger.Record(
            AuditEntityTypes.Booking, Guid.NewGuid(), AuditAction.BookingCreated,
            ActorType.CandidateToken, "invite-1", "chose option 2");

        Assert.True(logger.Contains(AuditAction.BookingCreated));
        Assert.Equal("chose option 2", logger.Entries.Single().Details);
    }

    private static ConfirmedSlot SlotFor(DateOnly date)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(date, new TimeOnly(9, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        return ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
    }
}
