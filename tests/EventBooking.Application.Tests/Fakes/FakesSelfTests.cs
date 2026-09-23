using EventBooking.Application.Abstractions;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Fakes;

public class FakesSelfTests
{
    [Fact]
    public void TheClockCanBeMoved()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        clock.Advance(TimeSpan.FromDays(5));

        Assert.Equal(new DateOnly(2026, 9, 8), clock.TodayAtTransitionalLocation);
    }

    [Fact]
    public async Task TheAttendeeRepositoryFiltersByStatus()
    {
        var repository = new InMemoryAttendeeRepository();
        var uniformOnly = AttendeeGroup.Define(
            Guid.NewGuid(), "UNI_ONLY", "UNI only", true, [AppointmentTypeIds.UniformFitting]);
        var invited = Attendee.Create(
            Guid.NewGuid(),
            "B. Chen",
            "b.chen@mail.com",
            uniformOnly,
            ProposalFixture.Now);
        invited.MarkInvited(ProposalFixture.Now);
        repository.Add(invited);
        repository.Add(Attendee.Create(
            Guid.NewGuid(),
            "A. Novak",
            "a.novak@mail.com",
            uniformOnly,
            ProposalFixture.Now));

        var result = await repository.ListAsync(AttendeeStatus.Invited, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("b.chen@mail.com", result[0].Email);
    }

    [Fact]
    public async Task TheEventRepositoryHidesCancelledAndPastEvents()
    {
        var repository = new InMemoryEventRepository();
        repository.Add(EventFor(new DateOnly(2026, 9, 1)));
        var cancelled = EventFor(new DateOnly(2026, 9, 20));
        cancelled.CancelBeforeStart();
        repository.Add(cancelled);
        repository.Add(EventFor(new DateOnly(2026, 9, 21)));

        var result = await repository.ListActiveAsync(new DateOnly(2026, 9, 3), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 9, 21), result[0].Window.Date);
    }

    [Fact]
    public async Task TheCapacityRepositoryReturnsRowsInAppointmentTypeOrder()
    {
        var events = new InMemoryEventRepository();
        var eventItem = EventFor(new DateOnly(2026, 9, 21));
        events.Add(eventItem);
        var capacities = new InMemoryEventCapacityRepository(events);

        var locked = await capacities
            .LockForUpdateAsync(eventItem.Id, AppointmentTypeIds.All, CancellationToken.None);

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

        var issued = service.Issue(TokenPurpose.Book, id, 2);

        Assert.True(service.TryRead(issued, out var read));
        Assert.Equal(new TokenReference(TokenPurpose.Book, id, 2), read);
        Assert.NotEqual(issued, service.Issue(TokenPurpose.Manage, id, 2));
        Assert.False(service.TryRead("nonsense", out _));
    }

    [Fact]
    public void TheAuditLoggerRecordsWhatItIsGiven()
    {
        var logger = new RecordingAuditLogger();

        logger.Record(
            AuditEntityTypes.Booking, Guid.NewGuid(), AuditAction.BookingCreated,
            ActorType.AttendeeToken, "invite-1", "chose option 2");

        Assert.True(logger.Contains(AuditAction.BookingCreated));
        Assert.Equal("chose option 2", logger.Entries.Single().Details);
    }

    private static Event EventFor(DateOnly date)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        return Event.CreateFrom(Guid.NewGuid(), proposal);
    }
}
