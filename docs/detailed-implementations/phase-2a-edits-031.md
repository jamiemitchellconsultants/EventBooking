# 02a — Deterministic attendee links and the token version counter, edits 31 (Task 9a)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs — 1/1

<!-- retirement-file: {"id":75,"file":"tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs","beforeSha":"5cc7c9d2b3ce5dc5db7b57049fbef4a750e8b265a2fd9250de9d210e83b73f9a","afterSha":"207f33c77449a07ea91a025e93d50575c2ebe8ce128b5951ee00b6516f03408a","side":"before","part":1,"parts":1} -->

`````csharp
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
        Assert.NotNull(await new InviteRepository(first).LockByTokenHashForUpdateAsync(
            scenario.InviteTokenHash, CancellationToken.None));

        var waitingBackend = NewBarrier();
        var secondLock = LockInviteAsync(scenario.InviteTokenHash, waitingBackend);
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
        Assert.NotNull(await new BookingRepository(first).LockByManageTokenHashForUpdateAsync(
            scenario.ManageTokenHash, CancellationToken.None));

        var waitingBackend = NewBarrier();
        var secondLock = LockBookingAsync(scenario.ManageTokenHash, waitingBackend);
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
            var invite = await new InviteRepository(context).LockByTokenHashForUpdateAsync(
                scenario.InviteTokenHash, CancellationToken.None);
            Assert.NotNull(invite);

            var booking = Booking.Create(
                Guid.NewGuid(),
                invite!,
                scenario.EventId,
                "confirmation-manage-hash",
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
        var eventId = await bookings.GetEventIdByManageTokenHashAsync(
            scenario.ManageTokenHash, CancellationToken.None);
        Assert.Equal(scenario.EventId, eventId);

        await using var transaction = await context.Database.BeginTransactionAsync();
        waitingBackend.SetResult(await GetBackendPidAsync(context));

        Assert.NotNull(await new EventRepository(context)
            .LockForUpdateAsync(eventId!.Value, CancellationToken.None));
        var booking = await bookings.LockByManageTokenHashForUpdateAsync(
            scenario.ManageTokenHash, CancellationToken.None);
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
        string tokenHash,
        TaskCompletionSource<int> waitingBackend)
    {
        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        waitingBackend.SetResult(await GetBackendPidAsync(context));

        var invite = await new InviteRepository(context)
            .LockByTokenHashForUpdateAsync(tokenHash, CancellationToken.None);
        await transaction.CommitAsync();
        return Assert.IsType<Invite>(invite).Id;
    }

    private async Task<Guid> LockBookingAsync(
        string tokenHash,
        TaskCompletionSource<int> waitingBackend)
    {
        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        waitingBackend.SetResult(await GetBackendPidAsync(context));

        var booking = await new BookingRepository(context)
            .LockByManageTokenHashForUpdateAsync(tokenHash, CancellationToken.None);
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

        const string inviteTokenHash = "invite-token-hash";
        const string manageTokenHash = "manage-token-hash";
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            inviteTokenHash,
            DateTimeOffset.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            events.Select(s => s.Id),
            attendee.RequiredAppointmentTypeIds,
            retryCount: 0);

        Booking? booking = null;
        if (withBooking)
        {
            booking = Booking.Create(
                Guid.NewGuid(), invite, events[0].Id, manageTokenHash, DateTimeOffset.UtcNow);
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
            inviteTokenHash,
            booking?.Id ?? Guid.Empty,
            manageTokenHash);
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
        string InviteTokenHash,
        Guid BookingId,
        string ManageTokenHash);
}
`````

## after — tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs — 1/1

<!-- retirement-file: {"id":75,"file":"tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs","beforeSha":"5cc7c9d2b3ce5dc5db7b57049fbef4a750e8b265a2fd9250de9d210e83b73f9a","afterSha":"207f33c77449a07ea91a025e93d50575c2ebe8ce128b5951ee00b6516f03408a","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## before — tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs — 1/1

<!-- retirement-file: {"id":76,"file":"tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs","beforeSha":"1cb06c3cbc6bc48687f3ac719a70ee2571ede4ce3f52bb40b2ec5f52d71b215a","afterSha":"5c81de9eb5a3fe78f2fc361e9ddbb6abfa521e3d179a0b49989d106dd984368d","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Appointments;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Mcp.Tests;

/// <summary>Seeds booked and no-show attendee scenarios for MCP parity tests.</summary>
public static class McpScenarioSeeder
{
    /// <summary>Seeds a attendee holding one active original booking plus spare future events.</summary>
    /// <param name="factory">The MCP host providing services and identity helpers.</param>
    /// <returns>The seeded attendee and booking identifiers.</returns>
    public static async Task<(Guid AttendeeId, Guid BookingId)> GivenBookedAttendeeAsync(McpFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;

        var bookedEvent = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today.AddDays(30), new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var spareEvents = new[] { new TimeOnly(11, 0), new TimeOnly(13, 0), new TimeOnly(15, 0) }
            .Select(start => EventFixture.Create(
                Guid.NewGuid(), new EventWindow(today.AddDays(31), start, 240),
                AppointmentTypeIds.All.ToDictionary(id => id, _ => 20)))
            .ToList();

        var group = context.AttendeeGroups
            .Include(g => g.Requirements)
            .Single(g => g.Id == AttendeeGroupIds.GroundOperationsAgent);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Alex Morgan",
            $"alex-{Guid.NewGuid():N}@example.com",
            group,
            ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [bookedEvent.Id, spareEvents[0].Id, spareEvents[1].Id],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, bookedEvent.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);

        attendee.MarkInvited(ProposalFixture.Now);
        invite.MarkUsed();
        attendee.MarkBooked(ProposalFixture.Now);

        context.AddRange(bookedEvent);
        context.AddRange(spareEvents);
        context.AddRange(attendee, invite, booking);
        foreach (var typeId in attendee.RequiredAppointmentTypeIds)
        {
            context.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
            bookedEvent.CapacityFor(typeId).Decrement();
        }

        await context.SaveChangesAsync();
        return (attendee.Id, booking.Id);
    }

    /// <summary>Seeds a booked attendee with one no-show appointment for recovery tests.</summary>
    /// <param name="factory">The MCP host providing services and identity helpers.</param>
    /// <returns>The seeded attendee and booking identifiers.</returns>
    public static async Task<(Guid AttendeeId, Guid BookingId)> GivenAttendeeWithNoShowAsync(McpFactory factory)
    {
        Guid attendeeId;
        Guid appointmentId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;
            var bookedEvent = EventFixture.Create(
                Guid.NewGuid(), new EventWindow(today.AddDays(-1), new TimeOnly(9, 0), 240),
                AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
            var spareEvents = new[]
            {
                new TimeOnly(11, 0),
                new TimeOnly(13, 0),
                new TimeOnly(15, 0),
            }
            .Select(start => EventFixture.Create(
                Guid.NewGuid(), new EventWindow(today.AddDays(2), start, 240),
                AppointmentTypeIds.All.ToDictionary(id => id, _ => 20)))
            .ToList();
            var group = context.AttendeeGroups
                .Include(g => g.Requirements)
                .Single(g => g.Id == AttendeeGroupIds.GroundOperationsAgent);
            var attendee = Attendee.Create(
                Guid.NewGuid(),
                "Alex Morgan",
                $"alex-{Guid.NewGuid():N}@example.com",
                group,
                ProposalFixture.Now);
            var invite = Invite.CreateInitial(
                Guid.NewGuid(),
                attendee.Id,
                $"invite-{Guid.NewGuid():N}",
                DateTimeOffset.UtcNow.AddDays(1),
                [ProposalFixture.LocationId],
                [bookedEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
                attendee.RequiredAppointmentTypeIds,
                0);
            var booking = Booking.Create(
                Guid.NewGuid(), invite, bookedEvent.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
            var appointment = BookingAppointment.Create(
                Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp);
            context.AddRange(bookedEvent);
            context.AddRange(spareEvents);
            context.AddRange(attendee, booking, appointment);
            await context.SaveChangesAsync();
            attendeeId = attendee.Id;
            appointmentId = appointment.Id;
        }

        var staffUserId = await factory.GivenStaffAsync([Role.AppointmentStaff], AppointmentTypeIds.MedicalCheckUp);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<UpdateBookingAppointmentStatusHandler>();
            var result = await handler.HandleAsync(
                new UpdateBookingAppointmentStatusCommand
                {
                    StaffUserId = staffUserId,
                    BookingAppointmentId = appointmentId,
                    Status = BookingAppointmentStatus.NoShow,
                    ExpectedVersion = 1,
                },
                CancellationToken.None);
            Assert.True(result.IsSuccess, result.Error.Message);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var bookingId = await context.BookingAppointments
                .Where(a => a.Id == appointmentId)
                .Select(a => a.BookingId)
                .SingleAsync();
            return (attendeeId, bookingId);
        }
    }

    /// <summary>Seeds one active event with a single scoped booking appointment for roster tests.</summary>
    /// <param name="factory">The MCP host providing services and identity helpers.</param>
    /// <param name="appointmentTypeId">The appointment type scoping the seeded workspace.</param>
    /// <returns>The seeded event identifier.</returns>
    public static async Task<Guid> GivenAppointmentWorkspaceAsync(McpFactory factory, Guid appointmentTypeId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;
        var eventItem = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today, new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var groupId = appointmentTypeId == AppointmentTypeIds.MedicalCheckUp
            ? AttendeeGroupIds.GroundOperationsAgent
            : AttendeeGroupIds.Pilots;
        var group = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == groupId);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Alex Morgan",
            $"alex-{Guid.NewGuid():N}@example.com",
            group,
            ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);
        context.AddRange(eventItem, attendee, booking, appointment);
        await context.SaveChangesAsync();
        return eventItem.Id;
    }
}
`````

## after — tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs — 1/1

<!-- retirement-file: {"id":76,"file":"tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs","beforeSha":"1cb06c3cbc6bc48687f3ac719a70ee2571ede4ce3f52bb40b2ec5f52d71b215a","afterSha":"5c81de9eb5a3fe78f2fc361e9ddbb6abfa521e3d179a0b49989d106dd984368d","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Appointments;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Mcp.Tests;

/// <summary>Seeds booked and no-show attendee scenarios for MCP parity tests.</summary>
public static class McpScenarioSeeder
{
    /// <summary>Seeds a attendee holding one active original booking plus spare future events.</summary>
    /// <param name="factory">The MCP host providing services and identity helpers.</param>
    /// <returns>The seeded attendee and booking identifiers.</returns>
    public static async Task<(Guid AttendeeId, Guid BookingId)> GivenBookedAttendeeAsync(McpFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;

        var bookedEvent = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today.AddDays(30), new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var spareEvents = new[] { new TimeOnly(11, 0), new TimeOnly(13, 0), new TimeOnly(15, 0) }
            .Select(start => EventFixture.Create(
                Guid.NewGuid(), new EventWindow(today.AddDays(31), start, 240),
                AppointmentTypeIds.All.ToDictionary(id => id, _ => 20)))
            .ToList();

        var group = context.AttendeeGroups
            .Include(g => g.Requirements)
            .Single(g => g.Id == AttendeeGroupIds.GroundOperationsAgent);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Alex Morgan",
            $"alex-{Guid.NewGuid():N}@example.com",
            group,
            ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [bookedEvent.Id, spareEvents[0].Id, spareEvents[1].Id],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, bookedEvent.Id, DateTimeOffset.UtcNow);

        attendee.MarkInvited(ProposalFixture.Now);
        invite.MarkUsed();
        attendee.MarkBooked(ProposalFixture.Now);

        context.AddRange(bookedEvent);
        context.AddRange(spareEvents);
        context.AddRange(attendee, invite, booking);
        foreach (var typeId in attendee.RequiredAppointmentTypeIds)
        {
            context.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
            bookedEvent.CapacityFor(typeId).Decrement();
        }

        await context.SaveChangesAsync();
        return (attendee.Id, booking.Id);
    }

    /// <summary>Seeds a booked attendee with one no-show appointment for recovery tests.</summary>
    /// <param name="factory">The MCP host providing services and identity helpers.</param>
    /// <returns>The seeded attendee and booking identifiers.</returns>
    public static async Task<(Guid AttendeeId, Guid BookingId)> GivenAttendeeWithNoShowAsync(McpFactory factory)
    {
        Guid attendeeId;
        Guid appointmentId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;
            var bookedEvent = EventFixture.Create(
                Guid.NewGuid(), new EventWindow(today.AddDays(-1), new TimeOnly(9, 0), 240),
                AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
            var spareEvents = new[]
            {
                new TimeOnly(11, 0),
                new TimeOnly(13, 0),
                new TimeOnly(15, 0),
            }
            .Select(start => EventFixture.Create(
                Guid.NewGuid(), new EventWindow(today.AddDays(2), start, 240),
                AppointmentTypeIds.All.ToDictionary(id => id, _ => 20)))
            .ToList();
            var group = context.AttendeeGroups
                .Include(g => g.Requirements)
                .Single(g => g.Id == AttendeeGroupIds.GroundOperationsAgent);
            var attendee = Attendee.Create(
                Guid.NewGuid(),
                "Alex Morgan",
                $"alex-{Guid.NewGuid():N}@example.com",
                group,
                ProposalFixture.Now);
            var invite = Invite.CreateInitial(
                Guid.NewGuid(),
                attendee.Id,
                DateTimeOffset.UtcNow.AddDays(1),
                [ProposalFixture.LocationId],
                [bookedEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
                attendee.RequiredAppointmentTypeIds,
                0);
            var booking = Booking.Create(
                Guid.NewGuid(), invite, bookedEvent.Id, DateTimeOffset.UtcNow);
            var appointment = BookingAppointment.Create(
                Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp);
            context.AddRange(bookedEvent);
            context.AddRange(spareEvents);
            context.AddRange(attendee, booking, appointment);
            await context.SaveChangesAsync();
            attendeeId = attendee.Id;
            appointmentId = appointment.Id;
        }

        var staffUserId = await factory.GivenStaffAsync([Role.AppointmentStaff], AppointmentTypeIds.MedicalCheckUp);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<UpdateBookingAppointmentStatusHandler>();
            var result = await handler.HandleAsync(
                new UpdateBookingAppointmentStatusCommand
                {
                    StaffUserId = staffUserId,
                    BookingAppointmentId = appointmentId,
                    Status = BookingAppointmentStatus.NoShow,
                    ExpectedVersion = 1,
                },
                CancellationToken.None);
            Assert.True(result.IsSuccess, result.Error.Message);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var bookingId = await context.BookingAppointments
                .Where(a => a.Id == appointmentId)
                .Select(a => a.BookingId)
                .SingleAsync();
            return (attendeeId, bookingId);
        }
    }

    /// <summary>Seeds one active event with a single scoped booking appointment for roster tests.</summary>
    /// <param name="factory">The MCP host providing services and identity helpers.</param>
    /// <param name="appointmentTypeId">The appointment type scoping the seeded workspace.</param>
    /// <returns>The seeded event identifier.</returns>
    public static async Task<Guid> GivenAppointmentWorkspaceAsync(McpFactory factory, Guid appointmentTypeId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;
        var eventItem = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today, new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var groupId = appointmentTypeId == AppointmentTypeIds.MedicalCheckUp
            ? AttendeeGroupIds.GroundOperationsAgent
            : AttendeeGroupIds.Pilots;
        var group = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == groupId);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Alex Morgan",
            $"alex-{Guid.NewGuid():N}@example.com",
            group,
            ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, DateTimeOffset.UtcNow);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);
        context.AddRange(eventItem, attendee, booking, appointment);
        await context.SaveChangesAsync();
        return eventItem.Id;
    }
}
`````

## before — tests/EventBooking.SeedData.Tests/DemoEmailOptionsTests.cs — 1/1

<!-- retirement-file: {"id":77,"file":"tests/EventBooking.SeedData.Tests/DemoEmailOptionsTests.cs","beforeSha":"12b9c9e51d8d2345d14f5085d296d2f2fb70b0b445fbdb19643c89e290d96fc9","afterSha":"6a4a2662b71cf0cb0b28a242458a07ed3c50b82c2bce2c4ae064468a2f43c84a","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Tokens;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>Checks configuration needed for demo links and SMTP delivery.</summary>
public sealed class DemoEmailOptionsTests
{
    /// <summary>Default tokens are readable with the local API's configured key.</summary>
    [Fact]
    public void DefaultsMatchLocalApiAndMailpit()
    {
        var options = DemoEmailOptions.From(_ => null);
        var seed = new HmacTokenService(options.Tokens);
        var api = new HmacTokenService(new TokenOptions(
            "a-local-signing-key-that-is-at-least-32-characters"));
        var id = Guid.NewGuid();
        Assert.True(api.TryRead(seed.Issue(id).Token, out var read));
        Assert.Equal(id, read);
        Assert.Equal("http://localhost:5002", options.Portal.BaseUrl);
        Assert.Equal("localhost", options.Smtp.Host);
        Assert.Equal(1025, options.Smtp.Port);
        Assert.Equal(EmailProvider.Smtp, options.Sender.Provider);
        Assert.Equal("Europe/London", options.Clock.TimeZoneId);
    }

    /// <summary>Explicit deployment settings drive usable links and email transport.</summary>
    [Fact]
    public void OverridesUseConfiguredKeyAndNormalizePortal()
    {
        var values = Complete();
        values["Portal__BaseUrl"] = "https://demo.example.test/portal/";
        values["Email__Smtp__Port"] = "2525";
        values["Email__FromAddress"] = "demo@example.com";
        values["Email__FromName"] = "Demo recruitment";
        values["Portal__CoordinatorContact"] = "help@example.com";
        values["Clock__TimeZoneId"] = "UTC";
        var options = DemoEmailOptions.From(values.GetValueOrDefault);
        var token = new HmacTokenService(options.Tokens).Issue(Guid.NewGuid()).Token;
        var api = new HmacTokenService(new TokenOptions(values["Tokens__SigningKey"]!));
        Assert.True(api.TryRead(token, out _));
        Assert.Equal("https://demo.example.test/portal", options.Portal.BaseUrl);
        Assert.Equal("mailpit", options.Smtp.Host);
        Assert.Equal(2525, options.Smtp.Port);
        Assert.Equal("demo@example.com", options.Sender.FromAddress);
        Assert.Equal("Demo recruitment", options.Sender.FromName);
        Assert.Equal("help@example.com", options.Portal.CoordinatorContact);
        Assert.Equal("UTC", options.Clock.TimeZoneId);
        Assert.DoesNotContain(values["Tokens__SigningKey"]!, options.ToString());
    }

    /// <summary>Non-local links never fall back to the local API's development secret or SMTP host.</summary>
    [Theory]
    [InlineData("Tokens__SigningKey")]
    [InlineData("Email__Smtp__Host")]
    public void NonLocalPortalRequiresExplicitSetting(string missing)
    {
        var values = Complete();
        values.Remove(missing);
        var error = Assert.Throws<SeedException>(() =>
            DemoEmailOptions.From(values.GetValueOrDefault));
        Assert.Contains(missing, error.Message);
    }

    /// <summary>Invalid values fail by setting name without exposing the supplied value.</summary>
    [Theory]
    [InlineData("Portal__BaseUrl", "not-a-url")]
    [InlineData("Portal__BaseUrl", "ftp://demo.example.test")]
    [InlineData("Portal__BaseUrl", "https://user:secret@demo.example.test")]
    [InlineData("Portal__BaseUrl", "https://demo.example.test?secret=x")]
    [InlineData("Portal__BaseUrl", "https://demo.example.test#fragment")]
    [InlineData("Tokens__SigningKey", "short-secret")]
    [InlineData("Email__Smtp__Port", "0")]
    [InlineData("Email__Smtp__Port", "65536")]
    [InlineData("Email__Smtp__Port", "invalid-port")]
    [InlineData("Email__Smtp__Host", " ")]
    [InlineData("Email__FromAddress", "not-an-email")]
    [InlineData("Clock__TimeZoneId", "missing/timezone")]
    public void InvalidConfigurationFailsWithoutValueDisclosure(string key, string value)
    {
        var values = Complete();
        values[key] = value;
        var error = Assert.Throws<SeedException>(() =>
            DemoEmailOptions.From(values.GetValueOrDefault));
        Assert.Contains(key, error.Message);
        if (!string.IsNullOrWhiteSpace(value) && value.Length > 1)
            Assert.DoesNotContain(value, error.Message);
    }

    private static Dictionary<string, string?> Complete() => new()
    {
        ["Portal__BaseUrl"] = "https://demo.example.test",
        ["Tokens__SigningKey"] = "a-test-signing-key-with-at-least-32-characters",
        ["Email__Smtp__Host"] = "mailpit",
    };
}
`````

## after — tests/EventBooking.SeedData.Tests/DemoEmailOptionsTests.cs — 1/1

<!-- retirement-file: {"id":77,"file":"tests/EventBooking.SeedData.Tests/DemoEmailOptionsTests.cs","beforeSha":"12b9c9e51d8d2345d14f5085d296d2f2fb70b0b445fbdb19643c89e290d96fc9","afterSha":"6a4a2662b71cf0cb0b28a242458a07ed3c50b82c2bce2c4ae064468a2f43c84a","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Tokens;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>Checks configuration needed for demo links and SMTP delivery.</summary>
public sealed class DemoEmailOptionsTests
{
    /// <summary>Default tokens are readable with the local API's configured key.</summary>
    [Fact]
    public void DefaultsMatchLocalApiAndMailpit()
    {
        var options = DemoEmailOptions.From(_ => null);
        var seed = new HmacTokenService(options.Tokens);
        var api = new HmacTokenService(new TokenOptions(
            "a-local-signing-key-that-is-at-least-32-characters"));
        var id = Guid.NewGuid();
        Assert.True(api.TryRead(seed.Issue(TokenPurpose.Book, id, 1), out var read));
        Assert.Equal(new TokenReference(TokenPurpose.Book, id, 1), read);
        Assert.Equal("http://localhost:5002", options.Portal.BaseUrl);
        Assert.Equal("localhost", options.Smtp.Host);
        Assert.Equal(1025, options.Smtp.Port);
        Assert.Equal(EmailProvider.Smtp, options.Sender.Provider);
        Assert.Equal("Europe/London", options.Clock.TimeZoneId);
    }

    /// <summary>Explicit deployment settings drive usable links and email transport.</summary>
    [Fact]
    public void OverridesUseConfiguredKeyAndNormalizePortal()
    {
        var values = Complete();
        values["Portal__BaseUrl"] = "https://demo.example.test/portal/";
        values["Email__Smtp__Port"] = "2525";
        values["Email__FromAddress"] = "demo@example.com";
        values["Email__FromName"] = "Demo recruitment";
        values["Portal__CoordinatorContact"] = "help@example.com";
        values["Clock__TimeZoneId"] = "UTC";
        var options = DemoEmailOptions.From(values.GetValueOrDefault);
        var token = new HmacTokenService(options.Tokens)
            .Issue(TokenPurpose.Book, Guid.NewGuid(), 1);
        var api = new HmacTokenService(new TokenOptions(values["Tokens__SigningKey"]!));
        Assert.True(api.TryRead(token, out _));
        Assert.Equal("https://demo.example.test/portal", options.Portal.BaseUrl);
        Assert.Equal("mailpit", options.Smtp.Host);
        Assert.Equal(2525, options.Smtp.Port);
        Assert.Equal("demo@example.com", options.Sender.FromAddress);
        Assert.Equal("Demo recruitment", options.Sender.FromName);
        Assert.Equal("help@example.com", options.Portal.CoordinatorContact);
        Assert.Equal("UTC", options.Clock.TimeZoneId);
        Assert.DoesNotContain(values["Tokens__SigningKey"]!, options.ToString());
    }

    /// <summary>Non-local links never fall back to the local API's development secret or SMTP host.</summary>
    [Theory]
    [InlineData("Tokens__SigningKey")]
    [InlineData("Email__Smtp__Host")]
    public void NonLocalPortalRequiresExplicitSetting(string missing)
    {
        var values = Complete();
        values.Remove(missing);
        var error = Assert.Throws<SeedException>(() =>
            DemoEmailOptions.From(values.GetValueOrDefault));
        Assert.Contains(missing, error.Message);
    }

    /// <summary>Invalid values fail by setting name without exposing the supplied value.</summary>
    [Theory]
    [InlineData("Portal__BaseUrl", "not-a-url")]
    [InlineData("Portal__BaseUrl", "ftp://demo.example.test")]
    [InlineData("Portal__BaseUrl", "https://user:secret@demo.example.test")]
    [InlineData("Portal__BaseUrl", "https://demo.example.test?secret=x")]
    [InlineData("Portal__BaseUrl", "https://demo.example.test#fragment")]
    [InlineData("Tokens__SigningKey", "short-secret")]
    [InlineData("Email__Smtp__Port", "0")]
    [InlineData("Email__Smtp__Port", "65536")]
    [InlineData("Email__Smtp__Port", "invalid-port")]
    [InlineData("Email__Smtp__Host", " ")]
    [InlineData("Email__FromAddress", "not-an-email")]
    [InlineData("Clock__TimeZoneId", "missing/timezone")]
    public void InvalidConfigurationFailsWithoutValueDisclosure(string key, string value)
    {
        var values = Complete();
        values[key] = value;
        var error = Assert.Throws<SeedException>(() =>
            DemoEmailOptions.From(values.GetValueOrDefault));
        Assert.Contains(key, error.Message);
        if (!string.IsNullOrWhiteSpace(value) && value.Length > 1)
            Assert.DoesNotContain(value, error.Message);
    }

    private static Dictionary<string, string?> Complete() => new()
    {
        ["Portal__BaseUrl"] = "https://demo.example.test",
        ["Tokens__SigningKey"] = "a-test-signing-key-with-at-least-32-characters",
        ["Email__Smtp__Host"] = "mailpit",
    };
}
`````
