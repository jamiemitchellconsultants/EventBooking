using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Infrastructure.Tests;

/// <summary>
/// The claim: two attendees can never both take the last place. These tests run the real confirm
/// handler over the real database from many threads, each on its own connection.
/// </summary>
[Collection("postgres")]
public class NoOverbookingTests(PostgresFixture fixture)
{
    private static readonly Guid DrugAndAlcohol = AppointmentTypeIds.DrugAndAlcoholTesting;
    private static readonly Guid Uniform = AppointmentTypeIds.UniformFitting;
    private static readonly Guid Medical = AppointmentTypeIds.MedicalCheckUp;

    [Fact]
    public async Task TenAttendeesRacingForOnePlaceProduceExactlyOneBooking()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);

        var eventId = await harness.GivenEventAsync(drugAndAlcohol: 1, medical: 50, uniform: 50);

        var tokens = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            tokens.Add(await harness.GivenInvitedAttendeeAsync(eventId, DrugAndAlcohol));
        }

        var batch = await harness.ConfirmBatchAsync(tokens, eventId);
        var results = batch.Results;

        Assert.Equal(tokens.Count, batch.BlockedAttemptCount);

        Assert.Equal(1, results.Count(r => r.IsSuccess));
        Assert.Equal(9, results.Count(r => r.IsFailure));
        Assert.All(results.Where(r => r.IsFailure), r => Assert.Equal("capacity-exhausted", r.Error.Code));

        Assert.Equal(0, await harness.RemainingCapacityAsync(eventId, DrugAndAlcohol));
        Assert.Equal(1, await harness.ActiveBookingCountAsync(eventId));
    }

    [Fact]
    public async Task ThirtyAttendeesRacingForTenPlacesProduceExactlyTenBookings()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);

        var eventId = await harness.GivenEventAsync(drugAndAlcohol: 10, medical: 50, uniform: 50);

        var tokens = new List<string>();
        for (var i = 0; i < 30; i++)
        {
            tokens.Add(await harness.GivenInvitedAttendeeAsync(eventId, DrugAndAlcohol));
        }

        var batch = await harness.ConfirmBatchAsync(tokens, eventId);
        var results = batch.Results;

        Assert.Equal(tokens.Count, batch.BlockedAttemptCount);

        Assert.Equal(10, results.Count(r => r.IsSuccess));
        Assert.Equal(0, await harness.RemainingCapacityAsync(eventId, DrugAndAlcohol));
        Assert.Equal(10, await harness.ActiveBookingCountAsync(eventId));
    }

    [Fact]
    public async Task AAttendeeNeedingTwoTypesIsStoppedByWhicheverRunsOutFirst()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);

        // Plenty of room for drug and alcohol, exactly one uniform place.
        var eventId = await harness.GivenEventAsync(drugAndAlcohol: 50, medical: 50, uniform: 1);

        var tokens = new List<string>();
        for (var i = 0; i < 8; i++)
        {
            tokens.Add(await harness.GivenInvitedAttendeeAsync(eventId, DrugAndAlcohol, Uniform));
        }

        var batch = await harness.ConfirmBatchAsync(tokens, eventId);
        var results = batch.Results;

        Assert.Equal(tokens.Count, batch.BlockedAttemptCount);

        Assert.Equal(1, results.Count(r => r.IsSuccess));
        Assert.Equal(0, await harness.RemainingCapacityAsync(eventId, Uniform));

        // The plentiful counter must have moved exactly once, not once per attempt.
        Assert.Equal(49, await harness.RemainingCapacityAsync(eventId, DrugAndAlcohol));
    }

    [Fact]
    public async Task AttendeesNeedingDifferentTypeCombinationsDoNotDeadlock()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);

        var eventId = await harness.GivenEventAsync(drugAndAlcohol: 20, medical: 20, uniform: 20);

        var tokens = new List<string>();
        for (var i = 0; i < 18; i++)
        {
            // Three different combinations, deliberately listed in different orders. The shared
            // Event guard serializes same-event mutations; capacity-row ordering is proven
            // directly by the existing repository and transaction-lock tests.
            Guid[] required = (i % 3) switch
            {
                0 => [Uniform, DrugAndAlcohol],
                1 => [Medical, Uniform],
                _ => [DrugAndAlcohol, Medical],
            };

            tokens.Add(await harness.GivenInvitedAttendeeAsync(eventId, required));
        }

        var batch = await harness.ConfirmBatchAsync(tokens, eventId);
        var results = batch.Results;

        Assert.Equal(tokens.Count, batch.BlockedAttemptCount);

        // Capacity is ample, so every attempt should succeed. A deadlock would show up as a
        // failure here, not as a hang: PostgreSQL kills one side of a deadlock.
        Assert.Equal(18, results.Count(r => r.IsSuccess));
        Assert.Equal(18, await harness.ActiveBookingCountAsync(eventId));

        Assert.Equal(8, await harness.RemainingCapacityAsync(eventId, DrugAndAlcohol));
        Assert.Equal(8, await harness.RemainingCapacityAsync(eventId, Medical));
        Assert.Equal(8, await harness.RemainingCapacityAsync(eventId, Uniform));
    }

    [Fact]
    public async Task ARaceLoserIsRefusedAndChangesNothing()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);

        var contested = await harness.GivenEventAsync(drugAndAlcohol: 1, medical: 50, uniform: 50);

        var winner = await harness.GivenInvitedAttendeeAsync(contested, DrugAndAlcohol);
        var loser = await harness.GivenInvitedAttendeeAsync(contested, DrugAndAlcohol);

        var tokens = new[] { winner, loser };
        var batch = await harness.ConfirmBatchAsync(tokens, contested);
        var results = batch.Results;

        Assert.Equal(tokens.Length, batch.BlockedAttemptCount);

        Assert.Equal(1, results.Count(r => r.IsSuccess));

        var failure = results.Single(r => r.IsFailure);
        Assert.Equal("capacity-exhausted", failure.Error.Code);

        Assert.Equal(0, await harness.RemainingCapacityAsync(contested, DrugAndAlcohol));
        Assert.Equal(1, await harness.ActiveBookingCountAsync(contested));
    }
}
