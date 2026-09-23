using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Events;
using EventBooking.Domain.Invites;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Locking;
using EventBooking.Infrastructure.Persistence.Repositories;
using EventBooking.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>
/// Deleting an attendee whose recovery booking is on a later event than their original must take
/// both events in ascending id order, like staff cancellation does — recovery event first would
/// acquire the same two rows in the opposite order and deadlock against it.
/// </summary>
[Collection("postgres")]
public sealed class AttendeeDeletionLockOrderTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);

    // Fixed so the recovery event sorts after the original, deterministically.
    private static readonly Guid OriginalEventId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid RecoveryEventId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task DeletingAnAttendeeWithBookingsOnTwoEventsTakesBothEventsInOrder()
    {
        var attendeeId = await GivenAttendeeWithOriginalAndRecoveryBookingsAsync();

        await using var context = fixture.NewContext();
        var locks = new TransactionLocks(enforced: true);
        var rowLocks = new RowLocks(context, locks);
        var zones = new NodaTimeEventWindowZones();
        var clock = new FixedClock(Now);
        var audit = new EfAuditLogger(context, clock);
        var handler = new DeleteAttendeeHandler(
            new AttendeeRepository(context, rowLocks),
            new InviteRepository(context),
            new BookingRepository(context),
            new EventRepository(context, rowLocks, zones),
            new AllowAllAuthorizer(),
            new BookingCanceller(
                new BookingAppointmentRepository(context),
                new EventCapacityRepository(rowLocks),
                audit),
            audit,
            new UnitOfWork(context, locks));

        var result = await handler.HandleAsync(
            new DeleteAttendeeCommand(Guid.NewGuid(), attendeeId, true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var verify = fixture.NewContext();
        Assert.Null(await verify.Attendees.SingleOrDefaultAsync(a => a.Id == attendeeId));
        var bookings = await verify.Bookings.AsNoTracking().ToListAsync();
        Assert.Equal(2, bookings.Count);
        Assert.All(bookings, b => Assert.Equal(BookingStatus.Cancelled, b.Status));
        foreach (var eventId in new[] { OriginalEventId, RecoveryEventId })
        {
            var remaining = await verify.EventCapacities
                .Where(c => c.EventId == eventId
                    && c.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting)
                .Select(c => c.RemainingCapacity)
                .SingleAsync();
            Assert.Equal(10, remaining);
        }
    }

    private async Task<Guid> GivenAttendeeWithOriginalAndRecoveryBookingsAsync()
    {
        await fixture.ResetAsync();

        await using var write = fixture.NewContext();
        var pilots = write.AttendeeGroups
            .Include(g => g.Requirements)
            .Single(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots, ProposalFixture.Now);
        write.Attendees.Add(attendee);

        var originalEvent = GivenEvent(write, OriginalEventId, new TimeOnly(9, 0));
        var recoveryEvent = GivenEvent(write, RecoveryEventId, new TimeOnly(11, 0));

        var initial = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            Now.AddDays(1),
            [ProposalFixture.LocationId],
            [OriginalEventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting],
            0);
        var original = Booking.Create(Guid.NewGuid(), initial, OriginalEventId, Now);
        initial.MarkUsed();
        write.Invites.Add(initial);
        write.Bookings.Add(original);
        write.BookingAppointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), original.Id, AppointmentTypeIds.DrugAndAlcoholTesting));
        originalEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();

        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            attendee.Id,
            original.Id,
            Now.AddDays(2),
            ProposalFixture.LocationId,
            null,
            [RecoveryEventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, RecoveryEventId, Now.AddHours(1));
        recoveryInvite.MarkUsed();
        write.Invites.Add(recoveryInvite);
        write.Bookings.Add(recovery);
        write.BookingAppointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting));
        recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();

        await write.SaveChangesAsync();
        return attendee.Id;
    }

    private static Event GivenEvent(EventBookingDbContext write, Guid eventId, TimeOnly start)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), start, 240),
            ProposalFixture.LocationId);
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);

        var eventItem = Event.CreateFrom(eventId, proposal);
        write.EventProposals.Add(proposal);
        write.Events.Add(eventItem);
        return eventItem;
    }

    private sealed class AllowAllAuthorizer : IStaffAccessAuthorizer
    {
        public Task<Result<StaffAccessContext>> AuthorizeAsync(
            Guid staffUserId,
            StaffCapability capability,
            Guid? requiredAppointmentTypeId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result<StaffAccessContext>.Success(
                new StaffAccessContext(
                    staffUserId, new HashSet<Role> { Role.Coordinator }, null)));
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;

        public DateTimeOffset NowAtTransitionalLocation => now;

        public DateOnly TodayAtTransitionalLocation => DateOnly.FromDateTime(now.UtcDateTime);

        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) =>
            instant.ToUniversalTime();
    }
}
