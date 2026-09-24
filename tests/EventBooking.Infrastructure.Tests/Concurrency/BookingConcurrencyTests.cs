using EventBooking.Application.Events;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EventBooking.Infrastructure.Tests.Concurrency;

/// <summary>
/// The scenarios the master plan names for Task 10, driven through the real ConfirmBookingHandler
/// from many connections at once. Two things are being proved: no capacity row can go below zero,
/// and no combination of type sets or event orders can deadlock.
/// </summary>
[Collection("postgres")]
public class BookingConcurrencyTests(PostgresFixture fixture)
{
    private static readonly Guid Med = AppointmentTypeIds.MedicalCheckUp;
    private static readonly Guid Fit = AppointmentTypeIds.UniformFitting;
    private static readonly Guid Ind = AppointmentTypeIds.DrugAndAlcoholTesting;

    /// <summary>The rotation from the master plan. Every subset needs IND, so IND is the ceiling.</summary>
    private static readonly IReadOnlyList<Guid>[] Rotation =
    [
        [Med, Ind],
        [Ind],
        [Fit, Ind],
        [Med, Fit, Ind],
    ];

    private readonly BookingConcurrencyHarness _raw = new(fixture);

    [Fact]
    public async Task FortyParallelBookingsFillTheBindingTypeExactlyOnce()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);
        var eventId = await harness.GivenEventAsync(drugAndAlcohol: 10, medical: 10, uniform: 10);

        var tokens = new List<string>();
        for (var i = 0; i < 40; i++)
            tokens.Add(await harness.GivenInvitedAttendeeAsync(eventId, Rotation[i % Rotation.Length].ToArray()));

        var attempts = await Task.WhenAll(tokens.Select(token =>
            Task.Run(() => TryConfirmAsync(harness, token, eventId))));

        Assert.DoesNotContain(attempts, attempt => attempt.Outcome == ConcurrentOutcome.Deadlocked);
        Assert.Equal(10, attempts.Count(attempt => attempt.Outcome == ConcurrentOutcome.Booked));
        Assert.Equal(30, attempts.Count(attempt => attempt.Outcome == ConcurrentOutcome.CapacityExhausted));

        var rows = await _raw.CapacitiesAsync(eventId);
        Assert.All(rows, row => Assert.InRange(row.RemainingCapacity, 0, row.TotalHeadcount));
        Assert.Equal(0, Remaining(rows, Ind));
        Assert.Equal(10, await harness.ActiveBookingCountAsync(eventId));
    }

    /// <summary>
    /// The ordering test through the real handler. Each attempt confirms one of two events,
    /// alternating down the list, and every invite offers the event it confirms: without
    /// ordered locks the two event rows would be taken in opposite orders, which is the
    /// textbook deadlock.
    /// </summary>
    [Fact]
    public async Task BookingsAcrossTwoEventsInShuffledOrderNeverDeadlock()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);
        var first = await harness.GivenEventAsync(drugAndAlcohol: 10, medical: 10, uniform: 10);
        var second = await harness.GivenEventAsync(drugAndAlcohol: 10, medical: 10, uniform: 10);

        var work = new List<(string Token, Guid Target)>();
        for (var i = 0; i < 40; i++)
        {
            var target = i % 2 == 0 ? first : second;
            work.Add((await harness.GivenInvitedAttendeeAsync(
                target, Rotation[i % Rotation.Length].ToArray()), target));
        }

        var attempts = await Task.WhenAll(work.Select(item =>
            Task.Run(() => TryConfirmAsync(harness, item.Token, item.Target))));

        Assert.DoesNotContain(attempts, attempt => attempt.Outcome == ConcurrentOutcome.Deadlocked);
        Assert.Equal(20, attempts.Count(attempt => attempt.Outcome == ConcurrentOutcome.Booked));

        foreach (var eventId in new[] { first, second })
        {
            var rows = await _raw.CapacitiesAsync(eventId);
            Assert.All(rows, row => Assert.InRange(row.RemainingCapacity, 0, row.TotalHeadcount));
            Assert.Equal(0, Remaining(rows, Ind));
            Assert.Equal(10, await harness.ActiveBookingCountAsync(eventId));
        }
    }

    /// <summary>
    /// The same shuffle with the event locks removed, so the capacity row order is the only thing
    /// preventing a deadlock. This is the test that fails if <c>CapacityLockOrder</c> stops being
    /// applied: the one above passes either way, because the event lock serialises first.
    /// </summary>
    [Fact]
    public async Task CapacityLocksAcrossTwoEventsInShuffledOrderNeverDeadlock()
    {
        await fixture.ResetAsync();
        var first = await GivenRawEventAsync(headcount: 30);
        var second = await GivenRawEventAsync(headcount: 30);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 40).Select(index =>
            Task.Run(() => _raw.TryChargeCapacitiesOnlyAsync(
                index % 2 == 0 ? [first, second] : [second, first],
                Rotation[index % Rotation.Length],
                CancellationToken.None))));

        Assert.DoesNotContain(attempts, attempt => attempt.Outcome == ConcurrentOutcome.Deadlocked);
        Assert.Equal(30, attempts.Count(attempt => attempt.Outcome == ConcurrentOutcome.Booked));

        foreach (var eventId in new[] { first, second })
        {
            var rows = await _raw.CapacitiesAsync(eventId);
            Assert.All(rows, row => Assert.InRange(row.RemainingCapacity, 0, row.TotalHeadcount));
            Assert.Equal(0, Remaining(rows, Ind));
        }
    }

    /// <summary>
    /// A headcount cut racing the bookings it would invalidate. Whichever order the rows grant the
    /// lock in, the arithmetic has to close: either the cut lands and the bookings that follow see
    /// the smaller total, or it is refused because the bookings got there first.
    /// </summary>
    [Fact]
    public async Task ACapacityCutRacingEightBookingsLeavesTheArithmeticIntact()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);
        var eventId = await harness.GivenEventAsync(drugAndAlcohol: 10, medical: 10, uniform: 10);

        var tokens = new List<string>();
        for (var i = 0; i < 8; i++)
            tokens.Add(await harness.GivenInvitedAttendeeAsync(eventId, Ind));

        var work = new List<Task<ConcurrentAttempt>>
        {
            Task.Run(() => _raw.TryAdjustAsync(eventId, Ind, 5, CancellationToken.None)),
        };
        work.AddRange(tokens.Select(token =>
            Task.Run(() => TryConfirmAsync(harness, token, eventId))));

        var results = await Task.WhenAll(work);
        var adjustment = results[0];
        var bookings = results[1..];

        Assert.DoesNotContain(results, attempt => attempt.Outcome == ConcurrentOutcome.Deadlocked);

        var booked = bookings.Count(attempt => attempt.Outcome == ConcurrentOutcome.Booked);
        var rows = await _raw.CapacitiesAsync(eventId);
        var row = rows.Single(item => item.AppointmentTypeId == Ind);

        Assert.Equal(row.TotalHeadcount - booked, row.RemainingCapacity);
        Assert.InRange(row.RemainingCapacity, 0, row.TotalHeadcount);

        if (adjustment.Outcome == ConcurrentOutcome.AdjustmentRefused)
        {
            // Refused only because more bookings than the new total were already in. The minimum
            // it reported is what was booked at that instant, so it is above the requested five
            // and no higher than the number that eventually got in.
            Assert.Equal(10, row.TotalHeadcount);
            Assert.InRange(adjustment.MinimumAccepted!.Value, 6, booked);
        }
        else
        {
            Assert.Equal(5, row.TotalHeadcount);
            Assert.True(booked <= 5);
        }
    }

    /// <summary>
    /// An event cancellation racing bookings on the same event. Whatever order the rows grant,
    /// there is no deadlock and the arithmetic closes: every surviving active booking still
    /// holds its place, and every cancelled booking gave its place back.
    /// </summary>
    [Fact]
    public async Task CancelEventRacingBookingsLeavesCapacityConsistent()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);
        var coordinator = await GivenCoordinatorAsync();
        var eventId = await harness.GivenEventAsync(drugAndAlcohol: 10, medical: 10, uniform: 10);

        var tokens = new List<string>();
        for (var i = 0; i < 8; i++)
            tokens.Add(await harness.GivenInvitedAttendeeAsync(eventId, Ind));

        var bookingTasks = tokens.Select(token =>
            Task.Run(() => TryConfirmAsync(harness, token, eventId))).ToArray();
        var cancelTask = Task.Run(() => TryCancelEventAsync(harness, coordinator, eventId));
        var attempts = await Task.WhenAll(bookingTasks);
        var cancel = await cancelTask;

        Assert.DoesNotContain(attempts, attempt => attempt.Outcome == ConcurrentOutcome.Deadlocked);
        Assert.False(cancel.Deadlocked);
        Assert.True(cancel.IsSuccess);

        await using var context = fixture.NewContext();
        var status = await context.Events.Where(e => e.Id == eventId)
            .Select(e => e.Status).SingleAsync();
        Assert.Equal(EventStatus.Cancelled, status);

        var rows = await _raw.CapacitiesAsync(eventId);
        var row = rows.Single(item => item.AppointmentTypeId == Ind);
        var surviving = await harness.ActiveBookingCountAsync(eventId);
        Assert.Equal(row.TotalHeadcount - surviving, row.RemainingCapacity);
        Assert.InRange(row.RemainingCapacity, 0, row.TotalHeadcount);
        var booked = attempts.Count(attempt => attempt.Outcome == ConcurrentOutcome.Booked);
        Assert.Equal(booked - surviving, cancel.Cancelled);
    }

    private static int Remaining(IEnumerable<EventCapacity> rows, Guid appointmentTypeId) =>
        rows.Single(row => row.AppointmentTypeId == appointmentTypeId).RemainingCapacity;

    private async Task<Guid> GivenRawEventAsync(int headcount)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 10, 12), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(Med, Guid.NewGuid(), headcount);
        proposal.Accept(Fit, Guid.NewGuid(), headcount);
        proposal.Accept(Ind, Guid.NewGuid(), headcount);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        await using var context = fixture.NewContext();
        context.EventProposals.Add(proposal);
        context.Events.Add(eventItem);
        await context.SaveChangesAsync();

        return eventItem.Id;
    }

    private async Task<Guid> GivenCoordinatorAsync()
    {
        var coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
        await using var context = fixture.NewContext();
        context.StaffAccessProfiles.Add(
            Domain.Access.StaffAccessProfile.Create(coordinator, Role.Coordinator, null));
        await context.SaveChangesAsync();
        return coordinator;
    }

    private static async Task<ConcurrentAttempt> TryConfirmAsync(
        ConcurrencyHarness harness, string token, Guid eventId)
    {
        try
        {
            var result = await harness.ConfirmAsync(token, eventId);
            if (result.IsSuccess)
                return new ConcurrentAttempt(ConcurrentOutcome.Booked);
            if (result.Error.Code == "capacity-exhausted")
                return new ConcurrentAttempt(ConcurrentOutcome.CapacityExhausted);
            throw new InvalidOperationException(
                $"Unexpected refusal: {result.Error.Code} {result.Error.Message}");
        }
        catch (Exception exception) when (IsDeadlock(exception))
        {
            return new ConcurrentAttempt(ConcurrentOutcome.Deadlocked);
        }
    }

    private sealed record CancelAttempt(bool Deadlocked, bool IsSuccess, int Cancelled);

    private static async Task<CancelAttempt> TryCancelEventAsync(
        ConcurrencyHarness harness, Guid coordinator, Guid eventId)
    {
        // The handler re-validates its booking snapshot under the event lock and asks the
        // caller to retry when a racing confirmation changed it, so the harness retries
        // that conflict instead of treating it as a failure.
        for (var attempt = 0; attempt < 25; attempt++)
        {
            try
            {
                using var scope = harness.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<CancelEventHandler>()
                    .HandleAsync(new CancelEventCommand(coordinator, eventId, true), CancellationToken.None);
                if (result.IsSuccess)
                    return new CancelAttempt(false, true, result.Value.CancelledCount);
                if (result.Error is not { Code: "conflict" } error
                    || !error.Message.Contains("Please retry", StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Unexpected cancel refusal: {result.Error.Code} {result.Error.Message}");
                await Task.Delay(50);
            }
            catch (Exception exception) when (IsDeadlock(exception))
            {
                return new CancelAttempt(true, false, 0);
            }
        }

        throw new InvalidOperationException(
            "Cancel never stopped conflicting; expected a retry to succeed.");
    }

    /// <summary>SQLSTATE 40P01. The one outcome none of these scenarios may produce.</summary>
    private static bool IsDeadlock(Exception exception) =>
        exception is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected }
            || exception.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.DeadlockDetected };
}
