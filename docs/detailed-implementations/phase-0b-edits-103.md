# 00b — Vocabulary edits 103 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs — 1/1

<!-- vocabulary-file: {"id":352,"oldPath":"tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs","beforeSha":"1c70afbc33c79eb36aee9e9cd482ae576840367989e4995adf78593d2258ec22","afterSha":"90a29068b9b5a4b170b03cab8e0759ec4079aa02869da1c351158a3c371d9c4c","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;
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
    public async Task SlotCancellationCommitsBeforeAWaitingConfirmationCanCreateABooking()
    {
        var scenario = await GivenScenarioAsync(withBooking: false);

        await using var cancellationContext = fixture.NewContext();
        await using var cancellationTransaction =
            await cancellationContext.Database.BeginTransactionAsync();
        var lockedSlot = await new ConfirmedSlotRepository(cancellationContext)
            .LockForUpdateAsync(scenario.SlotId, CancellationToken.None);
        Assert.NotNull(lockedSlot);

        // This is the authoritative booking read for whole-slot cancellation. It deliberately
        // happens after the shared slot guard has been taken.
        var activeBookings = await cancellationContext.Bookings
            .Where(b => b.ConfirmedSlotId == scenario.SlotId && b.Status == BookingStatus.Active)
            .ToListAsync();
        Assert.Empty(activeBookings);

        var waitingBackend = NewBarrier();
        var confirmation = ConfirmAfterSlotGuardAsync(scenario, waitingBackend);
        var confirmationPid = await waitingBackend.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitUntilBlockedOnDatabaseLockAsync(cancellationContext, confirmationPid);

        lockedSlot!.Cancel();
        await cancellationContext.SaveChangesAsync();
        await cancellationTransaction.CommitAsync();

        await confirmation.WaitAsync(TimeSpan.FromSeconds(10));

        await using var read = fixture.NewContext();
        Assert.Equal(
            ConfirmedSlotStatus.Cancelled,
            (await read.ConfirmedSlots.SingleAsync(s => s.Id == scenario.SlotId)).Status);
        Assert.False(await read.Bookings.AnyAsync(
            b => b.ConfirmedSlotId == scenario.SlotId && b.Status == BookingStatus.Active));
    }

    [Fact]
    public async Task SlotCancellationAndAWaitingBookingCancellationReleaseCapacityExactlyOnce()
    {
        var scenario = await GivenScenarioAsync(withBooking: true);

        await using var slotCancellationContext = fixture.NewContext();
        await using var slotCancellationTransaction =
            await slotCancellationContext.Database.BeginTransactionAsync();
        var lockedSlot = await new ConfirmedSlotRepository(slotCancellationContext)
            .LockForUpdateAsync(scenario.SlotId, CancellationToken.None);
        Assert.NotNull(lockedSlot);

        // As in the application handler, this authoritative read occurs only after the slot lock.
        var activeBooking = await slotCancellationContext.Bookings.SingleAsync(
            b => b.ConfirmedSlotId == scenario.SlotId && b.Status == BookingStatus.Active);

        var waitingBackend = NewBarrier();
        var individualCancellation = CancelBookingAfterSlotGuardAsync(scenario, waitingBackend);
        var cancellationPid = await waitingBackend.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitUntilBlockedOnDatabaseLockAsync(slotCancellationContext, cancellationPid);

        lockedSlot!.Cancel();
        activeBooking.Cancel();
        await IncrementCapacityAsync(slotCancellationContext, scenario.SlotId);
        await slotCancellationContext.SaveChangesAsync();
        await slotCancellationTransaction.CommitAsync();

        await individualCancellation.WaitAsync(TimeSpan.FromSeconds(10));

        await using var read = fixture.NewContext();
        var capacity = await read.SlotCapacities.SingleAsync(
            c => c.ConfirmedSlotId == scenario.SlotId
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

    private async Task ConfirmAfterSlotGuardAsync(
        Scenario scenario,
        TaskCompletionSource<int> waitingBackend)
    {
        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        waitingBackend.SetResult(await GetBackendPidAsync(context));

        var slot = await new ConfirmedSlotRepository(context)
            .LockForUpdateAsync(scenario.SlotId, CancellationToken.None);
        Assert.NotNull(slot);

        if (slot!.Status == ConfirmedSlotStatus.Active)
        {
            var invite = await new InviteRepository(context).LockByTokenHashForUpdateAsync(
                scenario.InviteTokenHash, CancellationToken.None);
            Assert.NotNull(invite);

            var booking = Booking.Create(
                Guid.NewGuid(),
                invite!,
                scenario.SlotId,
                "confirmation-manage-hash",
                DateTimeOffset.UtcNow);
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();
        }

        await transaction.CommitAsync();
    }

    private async Task CancelBookingAfterSlotGuardAsync(
        Scenario scenario,
        TaskCompletionSource<int> waitingBackend)
    {
        await using var context = fixture.NewContext();
        var bookings = new BookingRepository(context);

        // This lookup is intentionally preliminary and untracked. The booking is re-read under a
        // row lock only after this transaction acquires the shared slot guard.
        var slotId = await bookings.GetConfirmedSlotIdByManageTokenHashAsync(
            scenario.ManageTokenHash, CancellationToken.None);
        Assert.Equal(scenario.SlotId, slotId);

        await using var transaction = await context.Database.BeginTransactionAsync();
        waitingBackend.SetResult(await GetBackendPidAsync(context));

        Assert.NotNull(await new ConfirmedSlotRepository(context)
            .LockForUpdateAsync(slotId!.Value, CancellationToken.None));
        var booking = await bookings.LockByManageTokenHashForUpdateAsync(
            scenario.ManageTokenHash, CancellationToken.None);
        Assert.NotNull(booking);

        if (booking!.Status == BookingStatus.Active)
        {
            booking.Cancel();
            await IncrementCapacityAsync(context, scenario.SlotId);
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

        var proposals = new List<SlotProposal>();
        var slots = new List<ConfirmedSlot>();
        for (var offset = 0; offset < Invite.RequiredOptionCount; offset++)
        {
            var proposal = SlotProposal.Create(
                Guid.NewGuid(),
                new SlotWindow(new DateOnly(2026, 9, 10 + offset), new TimeOnly(9, 0)),
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
            slots.Add(ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal));
        }

        var pilots = EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var candidate = Candidate.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            pilots);
        candidate.MarkInvited();

        const string inviteTokenHash = "invite-token-hash";
        const string manageTokenHash = "manage-token-hash";
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            candidate.Id,
            inviteTokenHash,
            DateTimeOffset.UtcNow.AddDays(4),
            slots.Select(s => s.Id),
            candidate.RequiredAppointmentTypeIds,
            retryCount: 0);

        Booking? booking = null;
        if (withBooking)
        {
            booking = Booking.Create(
                Guid.NewGuid(), invite, slots[0].Id, manageTokenHash, DateTimeOffset.UtcNow);
            slots[0].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
            invite.MarkUsed();
            candidate.MarkBooked();
        }

        await using var write = fixture.NewContext();
        write.SlotProposals.AddRange(proposals);
        write.ConfirmedSlots.AddRange(slots);
        write.Candidates.Add(candidate);
        write.Invites.Add(invite);
        if (booking is not null)
        {
            write.Bookings.Add(booking);
        }

        await write.SaveChangesAsync();

        return new Scenario(
            slots[0].Id,
            invite.Id,
            inviteTokenHash,
            booking?.Id ?? Guid.Empty,
            manageTokenHash);
    }

    private static async Task IncrementCapacityAsync(
        EventBookingDbContext context,
        Guid slotId)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE slot_capacity
             SET remaining_capacity = remaining_capacity + 1
             WHERE confirmed_slot_id = {slotId}
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
        Guid SlotId,
        Guid InviteId,
        string InviteTokenHash,
        Guid BookingId,
        string ManageTokenHash);
}
`````

## after — tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs — 1/1

<!-- vocabulary-file: {"id":352,"oldPath":"tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs","beforeSha":"1c70afbc33c79eb36aee9e9cd482ae576840367989e4995adf78593d2258ec22","afterSha":"90a29068b9b5a4b170b03cab8e0759ec4079aa02869da1c351158a3c371d9c4c","side":"after","part":1,"parts":1} -->

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
            var proposal = EventProposal.Create(
                Guid.NewGuid(),
                new EventWindow(new DateOnly(2026, 9, 10 + offset), new TimeOnly(9, 0)),
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

## before — tests/EventBooking.Mcp.Tests/CandidateMcpTests.cs — 1/1

<!-- vocabulary-file: {"id":353,"oldPath":"tests/EventBooking.Mcp.Tests/CandidateMcpTests.cs","newPath":"tests/EventBooking.Mcp.Tests/AttendeeMcpTests.cs","beforeSha":"e77ac8010f2133925c74d805b5ecdbc38d25c470903c0467cfd796258444d541","afterSha":"806c53f6a7f1ab8043a43724493c4a70af28318b1ac8abd7ec7f80f9af06dd8f","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Mcp.Tests;

/// <summary>Covers the candidate read-parity contract exposed by MCP.</summary>
[Collection("mcp")]
public sealed class CandidateMcpTests(McpFactory factory)
{
    /// <summary>Listing exposes group identity, derived requirements, and readiness.</summary>
    [Fact]
    public async Task ListCandidates_ReturnsGroupRequirementsAndReadiness()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        var email = $"mcp-{Guid.NewGuid():N}@example.com";
        await CallToolResultAsync(
            "create_candidate",
            new { name = "Mcp Pilot", email, employeeGroupCode = "PILOTS" });

        var items = await CallToolResultAsync(
            "list_candidates", new { search = email });
        var item = items.EnumerateArray().Single();

        Assert.Equal("PILOTS", item.GetProperty("employeeGroupCode").GetString());
        Assert.False(item.GetProperty("requiresEmployeeGroupReconciliation").GetBoolean());
        Assert.Equal(2, item.GetProperty("requiredAppointmentTypes").GetArrayLength());
        Assert.Equal(
            "NoActiveBooking",
            item.GetProperty("readiness").GetProperty("code").GetString());
    }

    /// <summary>Pages slice the list without overlap.</summary>
    [Fact]
    public async Task ListCandidates_PagesWithoutOverlap()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        var prefix = $"paged-{Guid.NewGuid():N}";
        for (var index = 0; index < 3; index++)
        {
            await CallToolResultAsync(
                "create_candidate",
                new
                {
                    name = $"Paged {index}",
                    email = $"{prefix}-{index}@example.com",
                    employeeGroupCode = "PILOTS",
                });
        }

        var first = await CallToolResultAsync(
            "list_candidates", new { search = prefix, page = 1, pageSize = 2 });
        var second = await CallToolResultAsync(
            "list_candidates", new { search = prefix, page = 2, pageSize = 2 });

        Assert.Equal(2, first.GetArrayLength());
        Assert.Single(second.EnumerateArray());
        Assert.Empty(
            first.EnumerateArray().Select(item => item.GetRawText())
                .Intersect(second.EnumerateArray().Select(item => item.GetRawText())));
    }

    /// <summary>An unknown status surfaces as a tool error, not a transport failure.</summary>
    [Fact]
    public async Task ListCandidates_RejectsUnknownStatus()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await CallToolAsync("list_candidates", new { status = "Bogus" });

        Assert.True(IsToolError(payload));
    }

    /// <summary>Create input accepts a group code and no appointment-type codes.</summary>
    [Fact]
    public async Task CreateCandidateSchema_HasGroupCodeWithoutAppointmentTypes()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await PostRpcJsonAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });
        var create = payload
            .GetProperty("result").GetProperty("tools").EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == "create_candidate");
        var properties = create
            .GetProperty("inputSchema").GetProperty("properties").EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("employeeGroupCode", properties);
        Assert.DoesNotContain("appointmentTypeIds", properties);
        Assert.DoesNotContain("appointmentTypes", properties);
        Assert.DoesNotContain("requiredTypes", properties);
    }

    /// <summary>Recovery tool inputs carry candidate and invite identifiers and no token.</summary>
    [Fact]
    public async Task RecoveryToolSchemas_HaveIdentifiersWithoutTokens()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await PostRpcJsonAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });
        var tools = payload
            .GetProperty("result").GetProperty("tools").EnumerateArray()
            .Where(tool => tool.GetProperty("name").GetString() is "start_recovery_invite" or "cancel_recovery_invite")
            .ToList();

        Assert.Equal(2, tools.Count);
        foreach (var tool in tools)
        {
            var properties = tool
                .GetProperty("inputSchema").GetProperty("properties").EnumerateObject()
                .Select(property => property.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains("candidateId", properties);
            Assert.DoesNotContain("token", properties);
        }

        var cancel = tools.Single(tool => tool.GetProperty("name").GetString() == "cancel_recovery_invite");
        var cancelProperties = cancel
            .GetProperty("inputSchema").GetProperty("properties").EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("inviteId", cancelProperties);
    }

    private async Task<JsonElement> CallToolResultAsync(string name, object arguments)
    {
        var payload = await CallToolAsync(name, arguments);
        Assert.False(IsToolError(payload), payload.GetRawText());
        var text = payload.GetProperty("result").GetProperty("content")[0]
            .GetProperty("text").GetString();
        using var document = JsonDocument.Parse(text!);
        return document.RootElement.Clone();
    }

    private Task<JsonElement> CallToolAsync(string name, object arguments) =>
        PostRpcJsonAsync(new
        {
            jsonrpc = "2.0",
            id = "1",
            method = "tools/call",
            @params = new { name, arguments },
        });

    private async Task<JsonElement> PostRpcJsonAsync(object body)
    {
        var response = await PostRpcAsync(body);
        response.EnsureSuccessStatusCode();
        return ParseRpcPayload(await response.Content.ReadAsStringAsync());
    }

    private async Task<HttpResponseMessage> PostRpcAsync(object body)
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return await client.SendAsync(request);
    }

    private static JsonElement ParseRpcPayload(string body)
    {
        if (body.TrimStart().StartsWith('{'))
        {
            return JsonDocument.Parse(body).RootElement.Clone();
        }

        var data = body.Split('\n').Select(line => line.Trim())
            .Last(line => line.StartsWith("data: "))["data: ".Length..];
        return JsonDocument.Parse(data).RootElement.Clone();
    }

    private static bool IsToolError(JsonElement payload) =>
        payload.TryGetProperty("result", out var result) &&
        result.TryGetProperty("isError", out var isError) &&
        isError.ValueKind == JsonValueKind.True;
}
`````

## after — tests/EventBooking.Mcp.Tests/AttendeeMcpTests.cs — 1/1

<!-- vocabulary-file: {"id":353,"oldPath":"tests/EventBooking.Mcp.Tests/CandidateMcpTests.cs","newPath":"tests/EventBooking.Mcp.Tests/AttendeeMcpTests.cs","beforeSha":"e77ac8010f2133925c74d805b5ecdbc38d25c470903c0467cfd796258444d541","afterSha":"806c53f6a7f1ab8043a43724493c4a70af28318b1ac8abd7ec7f80f9af06dd8f","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Mcp.Tests;

/// <summary>Covers the attendee read-parity contract exposed by MCP.</summary>
[Collection("mcp")]
public sealed class AttendeeMcpTests(McpFactory factory)
{
    /// <summary>Listing exposes group identity, derived requirements, and readiness.</summary>
    [Fact]
    public async Task ListAttendees_ReturnsGroupRequirementsAndReadiness()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        var email = $"mcp-{Guid.NewGuid():N}@example.com";
        await CallToolResultAsync(
            "create_attendee",
            new { name = "Mcp Pilot", email, attendeeGroupCode = "PILOTS" });

        var items = await CallToolResultAsync(
            "list_attendees", new { search = email });
        var item = items.EnumerateArray().Single();

        Assert.Equal("PILOTS", item.GetProperty("attendeeGroupCode").GetString());
        Assert.False(item.GetProperty("requiresAttendeeGroupReconciliation").GetBoolean());
        Assert.Equal(2, item.GetProperty("requiredAppointmentTypes").GetArrayLength());
        Assert.Equal(
            "NoActiveBooking",
            item.GetProperty("readiness").GetProperty("code").GetString());
    }

    /// <summary>Pages slice the list without overlap.</summary>
    [Fact]
    public async Task ListAttendees_PagesWithoutOverlap()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        var prefix = $"paged-{Guid.NewGuid():N}";
        for (var index = 0; index < 3; index++)
        {
            await CallToolResultAsync(
                "create_attendee",
                new
                {
                    name = $"Paged {index}",
                    email = $"{prefix}-{index}@example.com",
                    attendeeGroupCode = "PILOTS",
                });
        }

        var first = await CallToolResultAsync(
            "list_attendees", new { search = prefix, page = 1, pageSize = 2 });
        var second = await CallToolResultAsync(
            "list_attendees", new { search = prefix, page = 2, pageSize = 2 });

        Assert.Equal(2, first.GetArrayLength());
        Assert.Single(second.EnumerateArray());
        Assert.Empty(
            first.EnumerateArray().Select(item => item.GetRawText())
                .Intersect(second.EnumerateArray().Select(item => item.GetRawText())));
    }

    /// <summary>An unknown status surfaces as a tool error, not a transport failure.</summary>
    [Fact]
    public async Task ListAttendees_RejectsUnknownStatus()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await CallToolAsync("list_attendees", new { status = "Bogus" });

        Assert.True(IsToolError(payload));
    }

    /// <summary>Create input accepts a group code and no appointment-type codes.</summary>
    [Fact]
    public async Task CreateAttendeeSchema_HasGroupCodeWithoutAppointmentTypes()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await PostRpcJsonAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });
        var create = payload
            .GetProperty("result").GetProperty("tools").EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == "create_attendee");
        var properties = create
            .GetProperty("inputSchema").GetProperty("properties").EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("attendeeGroupCode", properties);
        Assert.DoesNotContain("appointmentTypeIds", properties);
        Assert.DoesNotContain("appointmentTypes", properties);
        Assert.DoesNotContain("requiredTypes", properties);
    }

    /// <summary>Recovery tool inputs carry attendee and invite identifiers and no token.</summary>
    [Fact]
    public async Task RecoveryToolSchemas_HaveIdentifiersWithoutTokens()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await PostRpcJsonAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });
        var tools = payload
            .GetProperty("result").GetProperty("tools").EnumerateArray()
            .Where(tool => tool.GetProperty("name").GetString() is "start_recovery_invite" or "cancel_recovery_invite")
            .ToList();

        Assert.Equal(2, tools.Count);
        foreach (var tool in tools)
        {
            var properties = tool
                .GetProperty("inputSchema").GetProperty("properties").EnumerateObject()
                .Select(property => property.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains("attendeeId", properties);
            Assert.DoesNotContain("token", properties);
        }

        var cancel = tools.Single(tool => tool.GetProperty("name").GetString() == "cancel_recovery_invite");
        var cancelProperties = cancel
            .GetProperty("inputSchema").GetProperty("properties").EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("inviteId", cancelProperties);
    }

    private async Task<JsonElement> CallToolResultAsync(string name, object arguments)
    {
        var payload = await CallToolAsync(name, arguments);
        Assert.False(IsToolError(payload), payload.GetRawText());
        var text = payload.GetProperty("result").GetProperty("content")[0]
            .GetProperty("text").GetString();
        using var document = JsonDocument.Parse(text!);
        return document.RootElement.Clone();
    }

    private Task<JsonElement> CallToolAsync(string name, object arguments) =>
        PostRpcJsonAsync(new
        {
            jsonrpc = "2.0",
            id = "1",
            method = "tools/call",
            @params = new { name, arguments },
        });

    private async Task<JsonElement> PostRpcJsonAsync(object body)
    {
        var response = await PostRpcAsync(body);
        response.EnsureSuccessStatusCode();
        return ParseRpcPayload(await response.Content.ReadAsStringAsync());
    }

    private async Task<HttpResponseMessage> PostRpcAsync(object body)
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return await client.SendAsync(request);
    }

    private static JsonElement ParseRpcPayload(string body)
    {
        if (body.TrimStart().StartsWith('{'))
        {
            return JsonDocument.Parse(body).RootElement.Clone();
        }

        var data = body.Split('\n').Select(line => line.Trim())
            .Last(line => line.StartsWith("data: "))["data: ".Length..];
        return JsonDocument.Parse(data).RootElement.Clone();
    }

    private static bool IsToolError(JsonElement payload) =>
        payload.TryGetProperty("result", out var result) &&
        result.TryGetProperty("isError", out var isError) &&
        isError.ValueKind == JsonValueKind.True;
}
`````

## before — tests/EventBooking.Mcp.Tests/CandidateParityMcpTests.cs — 1/1

<!-- vocabulary-file: {"id":354,"oldPath":"tests/EventBooking.Mcp.Tests/CandidateParityMcpTests.cs","newPath":"tests/EventBooking.Mcp.Tests/AttendeeParityMcpTests.cs","beforeSha":"6a0704135fe6faeb7ae3a1ea14fb7c1ca6e881c1b6d1c308c56fd2886f3017a7","afterSha":"f96185ec61836d225030b344fb27a4832bdbf639870c9a83018011dcc61b8c98","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;

namespace EventBooking.Mcp.Tests;

[Collection("mcp")]
public sealed class CandidateParityMcpTests(McpFactory factory)
{
    [Fact]
    public async Task ReadinessAndBookingListReturnSafeViews()
    {
        var seeded = await McpScenarioSeeder.GivenBookedCandidateAsync(factory);
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        using var readiness = await CallResultAsync("get_candidate_readiness", new { candidateId = seeded.CandidateId });
        Assert.Equal(seeded.CandidateId, readiness.RootElement.GetProperty("candidateId").GetGuid());
        Assert.Equal("AppointmentsOutstanding", readiness.RootElement.GetProperty("code").GetString());
        using var bookings = await CallResultAsync("list_candidate_bookings", new { candidateId = seeded.CandidateId });
        var row = Assert.Single(bookings.RootElement.EnumerateArray());
        Assert.Equal(seeded.BookingId, row.GetProperty("bookingId").GetGuid());
        Assert.DoesNotContain("token", row.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CoordinatorStartsAndCancelsRecovery()
    {
        var seeded = await McpScenarioSeeder.GivenCandidateWithNoShowAsync(factory);
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        using var started = await CallResultAsync("start_recovery_invite", new { candidateId = seeded.CandidateId });
        var inviteId = started.RootElement.GetProperty("inviteId").GetGuid();
        Assert.NotEqual(Guid.Empty, inviteId);
        var cancelled = await CallAsync(
            "cancel_recovery_invite", new { candidateId = seeded.CandidateId, inviteId });
        Assert.False(cancelled.GetProperty("result").TryGetProperty("isError", out var cancelError) && cancelError.GetBoolean(), cancelled.GetRawText());
        Assert.Equal("Recovery invite cancelled.", cancelled.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task CoordinatorCancelsCandidateBooking()
    {
        var seeded = await McpScenarioSeeder.GivenBookedCandidateAsync(factory);
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        using var result = await CallResultAsync("cancel_candidate_booking", new
        {
            candidateId = seeded.CandidateId, bookingId = seeded.BookingId, rebook = false,
        });
        Assert.False(result.RootElement.GetProperty("reinvited").GetBoolean());
    }

    [Fact]
    public async Task AdminCannotReadCandidateReadiness()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
        var payload = await CallAsync("get_candidate_readiness", new { candidateId = Guid.NewGuid() });
        Assert.True(payload.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    private async Task<JsonDocument> CallResultAsync(string name, object arguments)
    {
        var payload = await CallAsync(name, arguments);
        Assert.False(payload.GetProperty("result").TryGetProperty("isError", out var error) && error.GetBoolean(), payload.GetRawText());
        return JsonDocument.Parse(payload.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!);
    }

    private async Task<JsonElement> CallAsync(string name, object arguments)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = "1", method = "tools/call", @params = new { name, arguments },
            }), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await factory.CreateClient().SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var data = body.TrimStart().StartsWith('{') ? body : body.Split('\n').Select(x => x.Trim())
            .Last(x => x.StartsWith("data: "))["data: ".Length..];
        return JsonDocument.Parse(data).RootElement.Clone();
    }
}
`````

## after — tests/EventBooking.Mcp.Tests/AttendeeParityMcpTests.cs — 1/1

<!-- vocabulary-file: {"id":354,"oldPath":"tests/EventBooking.Mcp.Tests/CandidateParityMcpTests.cs","newPath":"tests/EventBooking.Mcp.Tests/AttendeeParityMcpTests.cs","beforeSha":"6a0704135fe6faeb7ae3a1ea14fb7c1ca6e881c1b6d1c308c56fd2886f3017a7","afterSha":"f96185ec61836d225030b344fb27a4832bdbf639870c9a83018011dcc61b8c98","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;

namespace EventBooking.Mcp.Tests;

[Collection("mcp")]
public sealed class AttendeeParityMcpTests(McpFactory factory)
{
    [Fact]
    public async Task ReadinessAndBookingListReturnSafeViews()
    {
        var seeded = await McpScenarioSeeder.GivenBookedAttendeeAsync(factory);
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        using var readiness = await CallResultAsync("get_attendee_readiness", new { attendeeId = seeded.AttendeeId });
        Assert.Equal(seeded.AttendeeId, readiness.RootElement.GetProperty("attendeeId").GetGuid());
        Assert.Equal("AppointmentsOutstanding", readiness.RootElement.GetProperty("code").GetString());
        using var bookings = await CallResultAsync("list_attendee_bookings", new { attendeeId = seeded.AttendeeId });
        var row = Assert.Single(bookings.RootElement.EnumerateArray());
        Assert.Equal(seeded.BookingId, row.GetProperty("bookingId").GetGuid());
        Assert.DoesNotContain("token", row.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CoordinatorStartsAndCancelsRecovery()
    {
        var seeded = await McpScenarioSeeder.GivenAttendeeWithNoShowAsync(factory);
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        using var started = await CallResultAsync("start_recovery_invite", new { attendeeId = seeded.AttendeeId });
        var inviteId = started.RootElement.GetProperty("inviteId").GetGuid();
        Assert.NotEqual(Guid.Empty, inviteId);
        var cancelled = await CallAsync(
            "cancel_recovery_invite", new { attendeeId = seeded.AttendeeId, inviteId });
        Assert.False(cancelled.GetProperty("result").TryGetProperty("isError", out var cancelError) && cancelError.GetBoolean(), cancelled.GetRawText());
        Assert.Equal("Recovery invite cancelled.", cancelled.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task CoordinatorCancelsAttendeeBooking()
    {
        var seeded = await McpScenarioSeeder.GivenBookedAttendeeAsync(factory);
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        using var result = await CallResultAsync("cancel_attendee_booking", new
        {
            attendeeId = seeded.AttendeeId, bookingId = seeded.BookingId, rebook = false,
        });
        Assert.False(result.RootElement.GetProperty("reinvited").GetBoolean());
    }

    [Fact]
    public async Task AdminCannotReadAttendeeReadiness()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
        var payload = await CallAsync("get_attendee_readiness", new { attendeeId = Guid.NewGuid() });
        Assert.True(payload.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    private async Task<JsonDocument> CallResultAsync(string name, object arguments)
    {
        var payload = await CallAsync(name, arguments);
        Assert.False(payload.GetProperty("result").TryGetProperty("isError", out var error) && error.GetBoolean(), payload.GetRawText());
        return JsonDocument.Parse(payload.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!);
    }

    private async Task<JsonElement> CallAsync(string name, object arguments)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = "1", method = "tools/call", @params = new { name, arguments },
            }), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await factory.CreateClient().SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var data = body.TrimStart().StartsWith('{') ? body : body.Split('\n').Select(x => x.Trim())
            .Last(x => x.StartsWith("data: "))["data: ".Length..];
        return JsonDocument.Parse(data).RootElement.Clone();
    }
}
`````
