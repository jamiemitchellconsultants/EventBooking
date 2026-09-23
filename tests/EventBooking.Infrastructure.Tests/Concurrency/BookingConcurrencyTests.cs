using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

namespace EventBooking.Infrastructure.Tests.Concurrency;

/// <summary>
/// The scenarios the master plan names for Task 10, driven straight against the lock helpers from
/// many connections at once. Two things are being proved: no capacity row can go below zero, and
/// no combination of type sets or event orders can deadlock.
///
/// The master plan writes the three types as MED, FIT and IND. The prototype carries the
/// predecessor's three seeded types, so MED is the medical check-up, FIT the uniform fitting, and
/// IND the drug and alcohol test — the type every requirement subset includes, and therefore the
/// one that binds.
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

    private readonly BookingConcurrencyHarness _harness = new(fixture);

    [Fact]
    public async Task FortyParallelBookingsFillTheBindingTypeExactlyOnce()
    {
        await fixture.ResetAsync();
        var eventId = await GivenEventAsync(headcount: 10);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 40).Select(index =>
            Task.Run(() => _harness.TryChargeAsync(
                [eventId], Rotation[index % Rotation.Length], CancellationToken.None))));

        Assert.DoesNotContain(attempts, attempt => attempt.Outcome == ConcurrentOutcome.Deadlocked);
        Assert.Equal(10, attempts.Count(attempt => attempt.Outcome == ConcurrentOutcome.Booked));
        Assert.Equal(30, attempts.Count(attempt => attempt.Outcome == ConcurrentOutcome.CapacityExhausted));
        Assert.All(
            attempts.Where(attempt => attempt.Outcome == ConcurrentOutcome.CapacityExhausted),
            attempt => Assert.Equal(Ind, attempt.ExhaustedTypeId));

        var rows = await _harness.CapacitiesAsync(eventId);
        Assert.All(rows, row => Assert.InRange(row.RemainingCapacity, 0, row.TotalHeadcount));
        Assert.Equal(0, Remaining(rows, Ind));
    }

    /// <summary>
    /// The ordering test. Each attempt names its two events in a different order, and the helpers
    /// have to sort them: without that, half the attempts take A then B and half take B then A,
    /// which is the textbook deadlock.
    /// </summary>
    [Fact]
    public async Task BookingsAcrossTwoEventsInShuffledOrderNeverDeadlock()
    {
        await fixture.ResetAsync();
        var first = await GivenEventAsync(headcount: 30);
        var second = await GivenEventAsync(headcount: 30);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 40).Select(index =>
            Task.Run(() => _harness.TryChargeAsync(
                index % 2 == 0 ? [first, second] : [second, first],
                Rotation[index % Rotation.Length],
                CancellationToken.None))));

        Assert.DoesNotContain(attempts, attempt => attempt.Outcome == ConcurrentOutcome.Deadlocked);
        Assert.Equal(30, attempts.Count(attempt => attempt.Outcome == ConcurrentOutcome.Booked));

        foreach (var eventId in new[] { first, second })
        {
            var rows = await _harness.CapacitiesAsync(eventId);
            Assert.All(rows, row => Assert.InRange(row.RemainingCapacity, 0, row.TotalHeadcount));
            Assert.Equal(0, Remaining(rows, Ind));
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
        var first = await GivenEventAsync(headcount: 30);
        var second = await GivenEventAsync(headcount: 30);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 40).Select(index =>
            Task.Run(() => _harness.TryChargeCapacitiesOnlyAsync(
                index % 2 == 0 ? [first, second] : [second, first],
                Rotation[index % Rotation.Length],
                CancellationToken.None))));

        Assert.DoesNotContain(attempts, attempt => attempt.Outcome == ConcurrentOutcome.Deadlocked);
        Assert.Equal(30, attempts.Count(attempt => attempt.Outcome == ConcurrentOutcome.Booked));

        foreach (var eventId in new[] { first, second })
        {
            var rows = await _harness.CapacitiesAsync(eventId);
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
        await fixture.ResetAsync();
        var eventId = await GivenEventAsync(headcount: 10);

        var work = new List<Task<ConcurrentAttempt>>
        {
            Task.Run(() => _harness.TryAdjustAsync(eventId, Ind, 5, CancellationToken.None)),
        };
        work.AddRange(Enumerable.Range(0, 8).Select(_ =>
            Task.Run(() => _harness.TryChargeAsync([eventId], [Ind], CancellationToken.None))));

        var results = await Task.WhenAll(work);
        var adjustment = results[0];
        var bookings = results[1..];

        Assert.DoesNotContain(results, attempt => attempt.Outcome == ConcurrentOutcome.Deadlocked);

        var booked = bookings.Count(attempt => attempt.Outcome == ConcurrentOutcome.Booked);
        var rows = await _harness.CapacitiesAsync(eventId);
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

    private static int Remaining(IEnumerable<EventCapacity> rows, Guid appointmentTypeId) =>
        rows.Single(row => row.AppointmentTypeId == appointmentTypeId).RemainingCapacity;

    private async Task<Guid> GivenEventAsync(int headcount)
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
}
