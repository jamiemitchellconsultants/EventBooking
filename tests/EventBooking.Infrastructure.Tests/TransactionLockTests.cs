using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class TransactionLockTests(PostgresFixture fixture)
{
    [Fact]
    public async Task EventCancellationCommitsBeforeAWaitingConfirmationCanCreateABooking()
    {
        var scenario = await GivenScenarioAsync(withBooking: false);

        await using var cancellationContext = fixture.NewContext();
        await using var cancellationTransaction =
            await cancellationContext.Database.BeginTransactionAsync();
        var lockedEvent = await new EventRepository(cancellationContext)
            .LockForUpdateAsync(scenario.EventId, CancellationToken.None);
        Assert.NotNull(lockedEvent);

        // This is the authoritative booking read for whole-event cancellation. It deliberately
        // happens after the shared event guard has been taken.
        var activeBookings = await cancellationContext.Bookings
            .Where(b => b.EventId == scenario.EventId && b.Status == BookingStatus.Active)
            .ToListAsync();
        Assert.Empty(activeBookings);

        var waitingBackend = NewBarrier();
        var confirmation = ConfirmAfterEventGuardAsync(scenario, waitingBackend);
        var confirmationPid = await waitingBackend.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitUntilBlockedOnDatabaseLockAsync(cancellationContext, confirmationPid);

        lockedEvent!.CancelBeforeStart();
        await cancellationContext.SaveChangesAsync();
        await cancellationTransaction.CommitAsync();

        await confirmation.WaitAsync(TimeSpan.FromSeconds(10));

        await using var read = fixture.NewContext();
        Assert.Equal(
            EventStatus.Cancelled,
            (await read.Events.SingleAsync(s => s.Id == scenario.EventId)).Status);
        Assert.False(await read.Bookings.AnyAsync(
            b => b.EventId == scenario.EventId && b.Status == BookingStatus.Active));
    }

    [Fact]
    public async Task EventCancellationAndAWaitingBookingCancellationReleaseCapacityExactlyOnce()
    {
        var scenario = await GivenScenarioAsync(withBooking: true);

        await using var eventCancellationContext = fixture.NewContext();
        await using var eventCancellationTransaction =
            await eventCancellationContext.Database.BeginTransactionAsync();
        var lockedEvent = await new EventRepository(eventCancellationContext)
            .LockForUpdateAsync(scenario.EventId, CancellationToken.None);
        Assert.NotNull(lockedEvent);

        // As in the application handler, this authoritative read occurs only after the event lock.
        var activeBooking = await eventCancellationContext.Bookings.SingleAsync(
            b => b.EventId == scenario.EventId && b.Status == BookingStatus.Active);

        var waitingBackend = NewBarrier();
        var individualCancellation = CancelBookingAfterEventGuardAsync(scenario, waitingBackend);
        var cancellationPid = await waitingBackend.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitUntilBlockedOnDatabaseLockAsync(eventCancellationContext, cancellationPid);

        lockedEvent!.CancelBeforeStart();
        activeBooking.Cancel();
        await IncrementCapacityAsync(eventCancellationContext, scenario.EventId);
        await eventCancellationContext.SaveChangesAsync();
        await eventCancellationTransaction.CommitAsync();

        await individualCancellation.WaitAsync(TimeSpan.FromSeconds(10));

        await using var read = fixture.NewContext();
        var capacity = await read.EventCapacities.SingleAsync(
            c => c.EventId == scenario.EventId
                 && c.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting);
        var booking = await read.Bookings.SingleAsync(b => b.Id == scenario.BookingId);

        Assert.Equal(capacity.TotalHeadcount, capacity.RemainingCapacity);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
    }

    [Fact]
    public async Task InviteLockRemainsHeldUntilTheOwningTransactionCommits()
    {
        var scenario = await GivenScenarioAsync(withBooking: false);

        await using var first = fixture.NewContext();
        await using var firstTransaction = await first.Database.BeginTransactionAsync();
        Assert.NotNull(await new InviteRepository(first).LockForUpdateAsync(
            scenario.InviteId, CancellationToken.None));

        var waitingBackend = NewBarrier();
        var secondLock = LockInviteAsync(scenario.InviteId, waitingBackend);
        var secondPid = await waitingBackend.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitUntilBlockedOnDatabaseLockAsync(first, secondPid);

        await firstTransaction.CommitAsync();

        Assert.Equal(scenario.InviteId, await secondLock.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task BookingLockRemainsHeldUntilTheOwningTransactionCommits()
    {
        var scenario = await GivenScenarioAsync(withBooking: true);

        await using var first = fixture.NewContext();
        await using var firstTransaction = await first.Database.BeginTransactionAsync();
        Assert.NotNull(await new BookingRepository(first).LockForUpdateAsync(
            scenario.BookingId, CancellationToken.None));

        var waitingBackend = NewBarrier();
        var secondLock = LockBookingAsync(scenario.BookingId, waitingBackend);
        var secondPid = await waitingBackend.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitUntilBlockedOnDatabaseLockAsync(first, secondPid);

        await firstTransaction.CommitAsync();

        Assert.Equal(scenario.BookingId, await secondLock.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    private async Task ConfirmAfterEventGuardAsync(
        Scenario scenario,
        TaskCompletionSource<int> waitingBackend)
    {
        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        waitingBackend.SetResult(await GetBackendPidAsync(context));

        var eventItem = await new EventRepository(context)
            .LockForUpdateAsync(scenario.EventId, CancellationToken.None);
        Assert.NotNull(eventItem);

        if (eventItem!.Status == EventStatus.Active)
        {
            var invite = await new InviteRepository(context).LockForUpdateAsync(
                scenario.InviteId, CancellationToken.None);
            Assert.NotNull(invite);

            var booking = Booking.Create(
                Guid.NewGuid(),
                invite!,
                scenario.EventId,
                DateTimeOffset.UtcNow);
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();
        }

        await transaction.CommitAsync();
    }

    private async Task CancelBookingAfterEventGuardAsync(
        Scenario scenario,
        TaskCompletionSource<int> waitingBackend)
    {
        await using var context = fixture.NewContext();
        var bookings = new BookingRepository(context);

        // This lookup is intentionally preliminary and untracked. The booking is re-read under a
        // row lock only after this transaction acquires the shared event guard.
        var eventId = await bookings.GetEventIdAsync(
            scenario.BookingId, CancellationToken.None);
        Assert.Equal(scenario.EventId, eventId);

        await using var transaction = await context.Database.BeginTransactionAsync();
        waitingBackend.SetResult(await GetBackendPidAsync(context));

        Assert.NotNull(await new EventRepository(context)
            .LockForUpdateAsync(eventId!.Value, CancellationToken.None));
        var booking = await bookings.LockForUpdateAsync(
            scenario.BookingId, CancellationToken.None);
        Assert.NotNull(booking);

        if (booking!.Status == BookingStatus.Active)
        {
            booking.Cancel();
            await IncrementCapacityAsync(context, scenario.EventId);
            await context.SaveChangesAsync();
        }

        await transaction.CommitAsync();
    }

    private async Task<Guid> LockInviteAsync(
        Guid inviteId,
        TaskCompletionSource<int> waitingBackend)
    {
        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        waitingBackend.SetResult(await GetBackendPidAsync(context));

        var invite = await new InviteRepository(context)
            .LockForUpdateAsync(inviteId, CancellationToken.None);
        await transaction.CommitAsync();
        return Assert.IsType<Invite>(invite).Id;
    }

    private async Task<Guid> LockBookingAsync(
        Guid bookingId,
        TaskCompletionSource<int> waitingBackend)
    {
        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        waitingBackend.SetResult(await GetBackendPidAsync(context));

        var booking = await new BookingRepository(context)
            .LockForUpdateAsync(bookingId, CancellationToken.None);
        await transaction.CommitAsync();
        return Assert.IsType<Booking>(booking).Id;
    }

    private async Task<Scenario> GivenScenarioAsync(bool withBooking)
    {
        await fixture.ResetAsync();

        var proposals = new List<EventProposal>();
        var events = new List<Event>();
        for (var offset = 0; offset < Invite.RequiredOptionCount; offset++)
        {
            var proposal = ProposalFixture.Create(
                Guid.NewGuid(),
                new EventWindow(new DateOnly(2026, 9, 10 + offset), new TimeOnly(9, 0), 240),
                Guid.NewGuid());
            proposal.Accept(
                AppointmentTypeIds.DrugAndAlcoholTesting,
                Guid.NewGuid(),
                1);
            proposal.Accept(
                AppointmentTypeIds.MedicalCheckUp,
                Guid.NewGuid(),
                1);
            proposal.Accept(
                AppointmentTypeIds.UniformFitting,
                Guid.NewGuid(),
                1);
            proposals.Add(proposal);
            events.Add(Event.CreateFrom(Guid.NewGuid(), proposal));
        }

        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            pilots,
            ProposalFixture.Now);
        attendee.MarkInvited(ProposalFixture.Now);

        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            DateTimeOffset.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            events.Select(s => s.Id),
            attendee.RequiredAppointmentTypeIds,
            retryCount: 0);

        Booking? booking = null;
        if (withBooking)
        {
            booking = Booking.Create(
                Guid.NewGuid(), invite, events[0].Id, DateTimeOffset.UtcNow);
            events[0].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
            invite.MarkUsed();
            attendee.MarkBooked(ProposalFixture.Now);
        }

        await using var write = fixture.NewContext();
        write.EventProposals.AddRange(proposals);
        write.Events.AddRange(events);
        write.Attendees.Add(attendee);
        write.Invites.Add(invite);
        if (booking is not null)
        {
            write.Bookings.Add(booking);
        }

        await write.SaveChangesAsync();

        return new Scenario(
            events[0].Id,
            invite.Id,
            booking?.Id ?? Guid.Empty);
    }

    private static async Task IncrementCapacityAsync(
        EventBookingDbContext context,
        Guid eventId)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE event_capacity
             SET remaining_capacity = remaining_capacity + 1
             WHERE event_id = {eventId}
               AND appointment_type_id = {AppointmentTypeIds.DrugAndAlcoholTesting}
             """);
    }

    private static TaskCompletionSource<int> NewBarrier() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task<int> GetBackendPidAsync(EventBookingDbContext context)
    {
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = (NpgsqlTransaction)context.Database.CurrentTransaction!
            .GetDbTransaction();
        command.CommandText = "SELECT pg_backend_pid();";
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task WaitUntilBlockedOnDatabaseLockAsync(
        EventBookingDbContext lockOwner,
        int waitingBackendPid)
    {
        var connection = (NpgsqlConnection)lockOwner.Database.GetDbConnection();

        for (var attempt = 0; attempt < 200; attempt++)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (NpgsqlTransaction)lockOwner.Database.CurrentTransaction!
                .GetDbTransaction();
            command.CommandText =
                "SELECT wait_event_type FROM pg_stat_activity WHERE pid = @waiting_backend_pid;";
            command.Parameters.AddWithValue("waiting_backend_pid", waitingBackendPid);

            if (string.Equals(
                    await command.ExecuteScalarAsync() as string,
                    "Lock",
                    StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.Fail($"PostgreSQL backend {waitingBackendPid} never waited on the row lock.");
    }

    private sealed record Scenario(
        Guid EventId,
        Guid InviteId,
        Guid BookingId);
}
