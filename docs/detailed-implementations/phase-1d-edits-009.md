# 01d — Capacity generalised to N rows, edits 9 (Task 7)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs — 1/1

<!-- retirement-file: {"id":25,"file":"tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs","beforeSha":"69136955d0a89fe1869c0e88a473056aa9d77ee41ceead6ed5f7ecfdf706238c","afterSha":"11354f6aa0b10daf4b24e8596ef8cb8a37c05bf88c47d6d51b69672e0f27614e","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class RepositoryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ActiveEventsAreFilteredByStatusAndDate()
    {
        await fixture.ResetAsync();

        await using (var write = fixture.NewContext())
        {
            write.EventProposals.Add(ProposalOn(new DateOnly(2026, 9, 1), out var pastEvent));
            write.Events.Add(pastEvent);

            write.EventProposals.Add(ProposalOn(new DateOnly(2026, 9, 20), out var cancelled));
            cancelled.CancelBeforeStart();
            write.Events.Add(cancelled);

            write.EventProposals.Add(ProposalOn(new DateOnly(2026, 9, 21), out var live));
            write.Events.Add(live);

            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var repository = new EventRepository(read);

        var result = await repository.ListActiveAsync(new DateOnly(2026, 9, 3), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 9, 21), result[0].Window.Date);
        Assert.Equal(3, result[0].Capacities.Count);
    }

    [Fact]
    public async Task AAttendeeIsFoundByEmailWithTheirRequirements()
    {
        await fixture.ResetAsync();

        await using (var write = fixture.NewContext())
        {
            var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var found = await new AttendeeRepository(read)
            .GetByEmailAsync("a.novak@mail.com", CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal("Amara Novak", found!.Name);
        Assert.Equal(2, found.Requirements.Count);
    }

    [Fact]
    public async Task TheSettingsSingletonIsAlwaysThere()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var settings = await new SystemSettingsRepository(context).GetAsync(CancellationToken.None);

        Assert.Equal(7, settings.InviteExpiryDays);
        Assert.Equal(3, settings.InviteOptionCount);
    }

    private static EventProposal ProposalOn(DateOnly date, out Event eventItem)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        return proposal;
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs — 1/1

<!-- retirement-file: {"id":26,"file":"tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs","beforeSha":"2ba421efe230d4f65d2d64ed6a231ab7f59ba9b95ee919f96d410bc51aa9ae86","afterSha":"0cfd80fc437de249e153bacbc12c4c8ec621b16bc4822533199f9d07d9780db1","side":"before","part":1,"parts":1} -->

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

        lockedEvent!.Cancel();
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

        lockedEvent!.Cancel();
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
            pilots);
        attendee.MarkInvited();

        const string inviteTokenHash = "invite-token-hash";
        const string manageTokenHash = "manage-token-hash";
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            inviteTokenHash,
            DateTimeOffset.UtcNow.AddDays(4),
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
            attendee.MarkBooked();
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

<!-- retirement-file: {"id":26,"file":"tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs","beforeSha":"2ba421efe230d4f65d2d64ed6a231ab7f59ba9b95ee919f96d410bc51aa9ae86","afterSha":"0cfd80fc437de249e153bacbc12c4c8ec621b16bc4822533199f9d07d9780db1","side":"after","part":1,"parts":1} -->

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
            pilots);
        attendee.MarkInvited();

        const string inviteTokenHash = "invite-token-hash";
        const string manageTokenHash = "manage-token-hash";
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            inviteTokenHash,
            DateTimeOffset.UtcNow.AddDays(4),
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
            attendee.MarkBooked();
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

## before — tests/TestSupport/ProposalFixture.cs — 1/1

<!-- retirement-file: {"id":27,"file":"tests/TestSupport/ProposalFixture.cs","beforeSha":"dfce7611f05e27932227153adde84abe8227c7b8e43789b337c67c6301d194e3","afterSha":"31412adad6dd9e8968226d41efcab0023028829faa2b70b531e4fa6e948c6abb","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;

namespace EventBooking.TestSupport;

/// <summary>
/// Builds proposals in the shape Task 6 introduced while the inherited suites still exercise the
/// rules that did not change. The three predecessor types are listed, with drug and alcohol
/// testing as the proposing type; negotiation itself is covered by the N-type suite.
/// </summary>
public static class ProposalFixture
{
    /// <summary>The proposing type used by every proposal this fixture builds.</summary>
    public static Guid ProposerType => AppointmentTypeIds.DrugAndAlcoholTesting;

    /// <summary>The zone abstraction: every local time names exactly one instant.</summary>
    public static IEventWindowZones Zones { get; } = new UniqueZones();

    /// <summary>The zone every fixture proposal is read in.</summary>
    public const string TimeZoneId = "Europe/London";

    /// <summary>An instant well before any fixture window.</summary>
    public static DateTimeOffset Now => new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Proposes with the inherited three types listed, keeping the old call shape.</summary>
    /// <param name="id">The proposal identifier.</param>
    /// <param name="window">The proposed window.</param>
    /// <param name="createdByManagerUserId">The proposing Manager.</param>
    /// <param name="proposerAppointmentTypeId">The proposing type; drug and alcohol testing by default.</param>
    public static EventProposal Create(
        Guid id,
        EventWindow window,
        Guid createdByManagerUserId,
        Guid? proposerAppointmentTypeId = null) =>
        EventProposal.Propose(
            id,
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            locationIsActive: true,
            TimeZoneId,
            window,
            Zones,
            Now,
            [
                new(AppointmentTypeIds.DrugAndAlcoholTesting, "DAT", true, true),
                new(AppointmentTypeIds.MedicalCheckUp, "MED", true, true),
                new(AppointmentTypeIds.UniformFitting, "UNI", true, true),
            ],
            proposerAppointmentTypeId ?? ProposerType,
            createdByManagerUserId,
            headcount: 1);

    private sealed class UniqueZones : IEventWindowZones
    {
        public bool IsKnownZone(string timeZoneId) => true;

        public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            LocalTimeValidity.Unique;

        public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            new(date.ToDateTime(time), TimeSpan.Zero);

        public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) => "GMT";
    }
}
`````

## after — tests/TestSupport/ProposalFixture.cs — 1/1

<!-- retirement-file: {"id":27,"file":"tests/TestSupport/ProposalFixture.cs","beforeSha":"dfce7611f05e27932227153adde84abe8227c7b8e43789b337c67c6301d194e3","afterSha":"31412adad6dd9e8968226d41efcab0023028829faa2b70b531e4fa6e948c6abb","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;

namespace EventBooking.TestSupport;

/// <summary>
/// Builds proposals in the shape Task 6 introduced while the inherited suites still exercise the
/// rules that did not change. The three predecessor types are listed, with drug and alcohol
/// testing as the proposing type; negotiation itself is covered by the N-type suite.
/// </summary>
public static class ProposalFixture
{
    /// <summary>The proposing type used by every proposal this fixture builds.</summary>
    public static Guid ProposerType => AppointmentTypeIds.DrugAndAlcoholTesting;

    /// <summary>The zone abstraction: every local time names exactly one instant.</summary>
    public static IEventWindowZones Zones { get; } = new UniqueZones();

    /// <summary>The zone every fixture proposal is read in.</summary>
    public const string TimeZoneId = "Europe/London";

    /// <summary>An instant well before any fixture window.</summary>
    public static DateTimeOffset Now => new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Proposes with the inherited three types listed, keeping the old call shape.</summary>
    /// <param name="id">The proposal identifier.</param>
    /// <param name="window">The proposed window.</param>
    /// <param name="createdByManagerUserId">The proposing Manager.</param>
    /// <param name="proposerAppointmentTypeId">The proposing type; drug and alcohol testing by default.</param>
    public static EventProposal Create(
        Guid id,
        EventWindow window,
        Guid createdByManagerUserId,
        Guid? proposerAppointmentTypeId = null) =>
        EventProposal.Propose(
            id,
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            locationIsActive: true,
            TimeZoneId,
            window,
            Zones,
            Now,
            [
                new(AppointmentTypeIds.DrugAndAlcoholTesting, "DAT", true, true),
                new(AppointmentTypeIds.MedicalCheckUp, "MED", true, true),
                new(AppointmentTypeIds.UniformFitting, "UNI", true, true),
            ],
            proposerAppointmentTypeId ?? ProposerType,
            createdByManagerUserId,
            headcount: 1);

    private sealed class UniqueZones : IEventWindowZones
    {
        public bool IsKnownZone(string timeZoneId) => true;

        public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            LocalTimeValidity.Unique;

        public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            new(date.ToDateTime(time), TimeSpan.Zero);

        public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) => "GMT";
    }
}

/// <summary>
/// Cancels an event the way every suite written before Task 7's window rule means to: at an
/// instant before the window starts, which is now the only time cancellation is allowed.
/// </summary>
public static class EventCancellationFixture
{
    /// <summary>Cancels the event one minute before its window starts.</summary>
    /// <param name="eventItem">The event to cancel.</param>
    public static void CancelBeforeStart(this Event eventItem)
    {
        ArgumentNullException.ThrowIfNull(eventItem);

        var start = eventItem.Window.StartInstant(
            ProposalFixture.Zones, ProposalFixture.TimeZoneId);

        eventItem.Cancel(
            ProposalFixture.Zones, ProposalFixture.TimeZoneId, start.AddMinutes(-1));
    }
}
`````
