# 00b — Vocabulary edits 98 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — tests/EventBooking.Infrastructure.Tests/LoggingEmailSenderTests.cs — 1/1

<!-- vocabulary-file: {"id":340,"oldPath":"tests/EventBooking.Infrastructure.Tests/LoggingEmailSenderTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/LoggingEmailSenderTests.cs","beforeSha":"881b1305b7bc47f000501cc7c0182de94d45c9f0ddb3b7bf49b8a78b2931db4b","afterSha":"3b6d4dd100f78b59aebb62cd444cb15b8e374bfb767f8833a0abb1d5f5e9e9de","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class LoggingEmailSenderTests(PostgresFixture fixture)
{
    [Theory]
    [InlineData("not-an-email", "EventBooking")]
    [InlineData("sender@example.com", " ")]
    public void SenderOptionsRejectIncompleteValues(string fromAddress, string fromName)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new EmailOptions(fromAddress, fromName, EmailProvider.Smtp));

        if (!string.IsNullOrWhiteSpace(fromAddress))
        {
            Assert.DoesNotContain(fromAddress, exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SenderOptionsDoNotExposeTheirValuesWhenFormatted()
    {
        var options = new EmailOptions("sender@example.com", "EventBooking", EmailProvider.Smtp);

        var formatted = options.ToString();

        Assert.DoesNotContain(options.FromAddress, formatted, StringComparison.Ordinal);
        Assert.DoesNotContain(options.FromName, formatted, StringComparison.Ordinal);
    }

    private sealed class FakeTransport : IEmailTransport
    {
        public List<EmailMessage> Sent { get; } = [];

        public bool Throw { get; set; }

        public bool Cancel { get; set; }

        public Action? AfterSend { get; set; }

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (Cancel)
            {
                return Task.FromCanceled(cancellationToken);
            }

            if (Throw)
            {
                throw new InvalidOperationException("the provider rejected the message");
            }

            Sent.Add(message);
            AfterSend?.Invoke();
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;

        /// <summary>Gets the fixed instant; this clock treats UTC as transitional-location time.</summary>
        public DateTimeOffset NowAtTransitionalLocation => now;

        public DateOnly TodayAtTransitionalLocation => DateAtTransitionalLocation(now);

        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => DateOnly.FromDateTime(instant.UtcDateTime);

        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant.ToUniversalTime();
    }

    [Fact]
    public async Task ASuccessfulSendIsLoggedAsSent()
    {
        var (sender, transport, attendeeId) = await Given();

        var sent = await sender.SendAsync(MessageFor(attendeeId), CancellationToken.None);

        Assert.True(sent);
        Assert.Single(transport.Sent);

        await using var context = fixture.NewContext();
        var log = await context.EmailLogs.SingleAsync();
        Assert.Equal(attendeeId, log.AttendeeId);
        Assert.Equal(EmailTemplate.AttendeeInvite, log.TemplateName);
        Assert.Equal(EmailStatus.Sent, log.Status);
    }

    [Fact]
    public async Task AFailedSendReturnsFalseAndIsLoggedAsFailed()
    {
        var (sender, transport, attendeeId) = await Given();
        transport.Throw = true;

        var sent = await sender.SendAsync(MessageFor(attendeeId), CancellationToken.None);

        Assert.False(sent);
        Assert.Empty(transport.Sent);

        await using var context = fixture.NewContext();
        var log = await context.EmailLogs.SingleAsync();
        Assert.Equal(EmailStatus.Failed, log.Status);
    }

    [Fact]
    public async Task ThePublishedTimeComesFromTheClock()
    {
        var now = new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
        var (sender, _, attendeeId) = await Given(now);

        await sender.SendAsync(MessageFor(attendeeId), CancellationToken.None);

        await using var context = fixture.NewContext();
        Assert.Equal(now, (await context.EmailLogs.SingleAsync()).SentAt);
    }

    [Fact]
    public async Task ACancelledTransportPropagatesCancellationWithoutWritingAnEmailLog()
    {
        var (sender, transport, attendeeId) = await Given();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        transport.Cancel = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sender.SendAsync(MessageFor(attendeeId), cancellation.Token));

        await using var context = fixture.NewContext();
        Assert.Empty(await context.EmailLogs.ToListAsync());
    }

    [Fact]
    public async Task ACancelledAuditSavePropagatesCancellationWithoutWritingAnEmailLog()
    {
        var (sender, transport, attendeeId) = await Given();
        using var cancellation = new CancellationTokenSource();
        transport.AfterSend = cancellation.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sender.SendAsync(MessageFor(attendeeId), cancellation.Token));

        await using var context = fixture.NewContext();
        Assert.Empty(await context.EmailLogs.ToListAsync());
    }

    [Fact]
    public async Task AnAuditContextFailureIsWarnedAndDoesNotFailTheSend()
    {
        var (_, _, attendeeId) = await Given();
        var logger = new RecordingLogger<LoggingEmailSender>();
        var sender = new LoggingEmailSender(
            new FakeTransport(),
            new ThrowingContextFactory(),
            new FixedClock(DateTimeOffset.UtcNow),
            logger);

        var sent = await sender.SendAsync(MessageFor(attendeeId), CancellationToken.None);

        Assert.True(sent);
        Assert.Contains(LogLevel.Warning, logger.LogLevels);

        await using var context = fixture.NewContext();
        Assert.Empty(await context.EmailLogs.ToListAsync());
    }

    private async Task<(LoggingEmailSender Sender, FakeTransport Transport, Guid AttendeeId)> Given(
        DateTimeOffset? now = null)
    {
        await fixture.ResetAsync();

        var attendeeId = Guid.NewGuid();
        await using (var write = fixture.NewContext())
        {
            var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var attendee = Attendee.Create(
                attendeeId, "Amara Novak", "a.novak@mail.com", pilots);
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
        }

        var transport = new FakeTransport();
        var sender = new LoggingEmailSender(
            transport,
            new TestContextFactory(fixture),
            new FixedClock(now ?? DateTimeOffset.UtcNow),
            NullLogger<LoggingEmailSender>.Instance);

        return (sender, transport, attendeeId);
    }

    private static EmailMessage MessageFor(Guid attendeeId) =>
        new(attendeeId, "a.novak@mail.com", "Amara Novak", EmailTemplate.AttendeeInvite,
            "Choose a time", "text", "<html></html>");

    private sealed class TestContextFactory(PostgresFixture fixture)
        : IDbContextFactory<EventBookingDbContext>
    {
        public EventBookingDbContext CreateDbContext() => fixture.NewContext();
    }

    private sealed class ThrowingContextFactory : IDbContextFactory<EventBookingDbContext>
    {
        public EventBookingDbContext CreateDbContext() =>
            throw new InvalidOperationException("the audit database is unavailable");
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogLevel> LogLevels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LogLevels.Add(logLevel);
        }
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/NoOverbookingTests.cs — 1/1

<!-- vocabulary-file: {"id":341,"oldPath":"tests/EventBooking.Infrastructure.Tests/NoOverbookingTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/NoOverbookingTests.cs","beforeSha":"95442f57cd3f6efad95b2bf15696344e9654dae57d2ae71fff0569587580551c","afterSha":"d7663a66399de8aac4eb5ce1056039ca3b1f6e21477147cac81068355d90c8f6","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Infrastructure.Tests;

/// <summary>
/// The claim: two candidates can never both take the last place. These tests run the real confirm
/// handler over the real database from many threads, each on its own connection.
/// </summary>
[Collection("postgres")]
public class NoOverbookingTests(PostgresFixture fixture)
{
    private static readonly Guid DrugAndAlcohol = AppointmentTypeIds.DrugAndAlcoholTesting;
    private static readonly Guid Uniform = AppointmentTypeIds.UniformFitting;
    private static readonly Guid Medical = AppointmentTypeIds.MedicalCheckUp;

    [Fact]
    public async Task TenCandidatesRacingForOnePlaceProduceExactlyOneBooking()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);

        var slotId = await harness.GivenSlotAsync(drugAndAlcohol: 1, medical: 50, uniform: 50);

        var tokens = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            tokens.Add(await harness.GivenInvitedCandidateAsync(slotId, DrugAndAlcohol));
        }

        var batch = await harness.ConfirmBatchAsync(tokens, slotId);
        var results = batch.Results;

        Assert.Equal(tokens.Count, batch.BlockedAttemptCount);

        Assert.Equal(1, results.Count(r => r.IsSuccess));
        Assert.Equal(9, results.Count(r => r.IsFailure));
        Assert.All(results.Where(r => r.IsFailure), r => Assert.Equal("conflict", r.Error.Code));

        Assert.Equal(0, await harness.RemainingCapacityAsync(slotId, DrugAndAlcohol));
        Assert.Equal(1, await harness.ActiveBookingCountAsync(slotId));
    }

    [Fact]
    public async Task ThirtyCandidatesRacingForTenPlacesProduceExactlyTenBookings()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);

        var slotId = await harness.GivenSlotAsync(drugAndAlcohol: 10, medical: 50, uniform: 50);

        var tokens = new List<string>();
        for (var i = 0; i < 30; i++)
        {
            tokens.Add(await harness.GivenInvitedCandidateAsync(slotId, DrugAndAlcohol));
        }

        var batch = await harness.ConfirmBatchAsync(tokens, slotId);
        var results = batch.Results;

        Assert.Equal(tokens.Count, batch.BlockedAttemptCount);

        Assert.Equal(10, results.Count(r => r.IsSuccess));
        Assert.Equal(0, await harness.RemainingCapacityAsync(slotId, DrugAndAlcohol));
        Assert.Equal(10, await harness.ActiveBookingCountAsync(slotId));
    }

    [Fact]
    public async Task ACandidateNeedingTwoTypesIsStoppedByWhicheverRunsOutFirst()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);

        // Plenty of room for drug and alcohol, exactly one uniform place.
        var slotId = await harness.GivenSlotAsync(drugAndAlcohol: 50, medical: 50, uniform: 1);

        var tokens = new List<string>();
        for (var i = 0; i < 8; i++)
        {
            tokens.Add(await harness.GivenInvitedCandidateAsync(slotId, DrugAndAlcohol, Uniform));
        }

        var batch = await harness.ConfirmBatchAsync(tokens, slotId);
        var results = batch.Results;

        Assert.Equal(tokens.Count, batch.BlockedAttemptCount);

        Assert.Equal(1, results.Count(r => r.IsSuccess));
        Assert.Equal(0, await harness.RemainingCapacityAsync(slotId, Uniform));

        // The plentiful counter must have moved exactly once, not once per attempt.
        Assert.Equal(49, await harness.RemainingCapacityAsync(slotId, DrugAndAlcohol));
    }

    [Fact]
    public async Task CandidatesNeedingDifferentTypeCombinationsDoNotDeadlock()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);

        var slotId = await harness.GivenSlotAsync(drugAndAlcohol: 20, medical: 20, uniform: 20);

        var tokens = new List<string>();
        for (var i = 0; i < 18; i++)
        {
            // Three different combinations, deliberately listed in different orders. The shared
            // ConfirmedSlot guard serializes same-slot mutations; capacity-row ordering is proven
            // directly by the existing repository and transaction-lock tests.
            Guid[] required = (i % 3) switch
            {
                0 => [Uniform, DrugAndAlcohol],
                1 => [Medical, Uniform],
                _ => [DrugAndAlcohol, Medical],
            };

            tokens.Add(await harness.GivenInvitedCandidateAsync(slotId, required));
        }

        var batch = await harness.ConfirmBatchAsync(tokens, slotId);
        var results = batch.Results;

        Assert.Equal(tokens.Count, batch.BlockedAttemptCount);

        // Capacity is ample, so every attempt should succeed. A deadlock would show up as a
        // failure here, not as a hang: PostgreSQL kills one side of a deadlock.
        Assert.Equal(18, results.Count(r => r.IsSuccess));
        Assert.Equal(18, await harness.ActiveBookingCountAsync(slotId));

        Assert.Equal(8, await harness.RemainingCapacityAsync(slotId, DrugAndAlcohol));
        Assert.Equal(8, await harness.RemainingCapacityAsync(slotId, Medical));
        Assert.Equal(8, await harness.RemainingCapacityAsync(slotId, Uniform));
    }

    [Fact]
    public async Task ARaceLosersInviteKeepsThreeLiveOptionsWhenAReplacementExists()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);

        var contested = await harness.GivenSlotAsync(drugAndAlcohol: 1, medical: 50, uniform: 50);
        var replacement = await harness.GivenSlotAsync(drugAndAlcohol: 50, medical: 50, uniform: 50);

        var winner = await harness.GivenInvitedCandidateAsync(contested, DrugAndAlcohol);
        var loser = await harness.GivenInvitedCandidateAsync(contested, DrugAndAlcohol);

        var tokens = new[] { winner, loser };
        var batch = await harness.ConfirmBatchAsync(tokens, contested);
        var results = batch.Results;

        Assert.Equal(tokens.Length, batch.BlockedAttemptCount);

        Assert.Equal(1, results.Count(r => r.IsSuccess));

        var failure = results.Single(r => r.IsFailure);
        Assert.Equal(
            "That time filled up while you were choosing. Please pick from the updated options.",
            failure.Error.Message);

        Assert.Equal(0, await harness.RemainingCapacityAsync(contested, DrugAndAlcohol));
        Assert.Equal(1, await harness.ActiveBookingCountAsync(contested));

        var loserToken = tokens[Enumerable.Range(0, results.Count)
            .Single(index => results[index].IsFailure)];
        var liveOptions = await harness.LiveOptionSlotIdsAsync(loserToken);

        Assert.Equal(3, liveOptions.Count);
        Assert.Equal(3, liveOptions.Distinct().Count());
        Assert.DoesNotContain(contested, liveOptions);
        Assert.Contains(replacement, liveOptions);
        Assert.All(harness.FallbackSlotIds, fallback => Assert.Contains(fallback, liveOptions));
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/NoOverbookingTests.cs — 1/1

<!-- vocabulary-file: {"id":341,"oldPath":"tests/EventBooking.Infrastructure.Tests/NoOverbookingTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/NoOverbookingTests.cs","beforeSha":"95442f57cd3f6efad95b2bf15696344e9654dae57d2ae71fff0569587580551c","afterSha":"d7663a66399de8aac4eb5ce1056039ca3b1f6e21477147cac81068355d90c8f6","side":"after","part":1,"parts":1} -->

`````csharp
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
        Assert.All(results.Where(r => r.IsFailure), r => Assert.Equal("conflict", r.Error.Code));

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
    public async Task ARaceLosersInviteKeepsThreeLiveOptionsWhenAReplacementExists()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);

        var contested = await harness.GivenEventAsync(drugAndAlcohol: 1, medical: 50, uniform: 50);
        var replacement = await harness.GivenEventAsync(drugAndAlcohol: 50, medical: 50, uniform: 50);

        var winner = await harness.GivenInvitedAttendeeAsync(contested, DrugAndAlcohol);
        var loser = await harness.GivenInvitedAttendeeAsync(contested, DrugAndAlcohol);

        var tokens = new[] { winner, loser };
        var batch = await harness.ConfirmBatchAsync(tokens, contested);
        var results = batch.Results;

        Assert.Equal(tokens.Length, batch.BlockedAttemptCount);

        Assert.Equal(1, results.Count(r => r.IsSuccess));

        var failure = results.Single(r => r.IsFailure);
        Assert.Equal(
            "That time filled up while you were choosing. Please pick from the updated options.",
            failure.Error.Message);

        Assert.Equal(0, await harness.RemainingCapacityAsync(contested, DrugAndAlcohol));
        Assert.Equal(1, await harness.ActiveBookingCountAsync(contested));

        var loserToken = tokens[Enumerable.Range(0, results.Count)
            .Single(index => results[index].IsFailure)];
        var liveOptions = await harness.LiveOptionEventIdsAsync(loserToken);

        Assert.Equal(3, liveOptions.Count);
        Assert.Equal(3, liveOptions.Distinct().Count());
        Assert.DoesNotContain(contested, liveOptions);
        Assert.Contains(replacement, liveOptions);
        Assert.All(harness.FallbackEventIds, fallback => Assert.Contains(fallback, liveOptions));
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs — 1/1

<!-- vocabulary-file: {"id":342,"oldPath":"tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs","newPath":"tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs","beforeSha":"5d8f77671859f31d8c8d60cd5976d8813c8d2426b4fc0542677eb8f75b552ebe","afterSha":"156c787ac039310a6daa5bfece000fbe5820319f9cfcb266e9c8c9161ebac257","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace EventBooking.Infrastructure.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("eventbooking")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();
    private NpgsqlDataSource? _dataSource;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _dataSource = NpgsqlDataSource.Create(ConnectionString);

        await using var context = NewContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    public EventBookingDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new EventBookingDbContext(options);
    }

    /// <summary>
    /// Empties every table and re-seeds the fixed rows. Faster and far less flaky than dropping
    /// and re-creating the database between tests.
    /// </summary>
    public async Task ResetAsync()
    {
        var dataSource = _dataSource
            ?? throw new InvalidOperationException("The PostgreSQL fixture has not been initialized.");
        await using var connection = await dataSource.OpenConnectionAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                DO $$
                DECLARE statements CURSOR FOR
                    SELECT tablename FROM pg_tables
                    WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'
                    -- Change-controlled Employee Group rows stay seeded by the migration.
                    AND tablename NOT IN ('employee_group', 'employee_group_requirement');
                BEGIN
                    FOR statement IN statements LOOP
                        EXECUTE 'TRUNCATE TABLE ' || quote_ident(statement.tablename) || ' CASCADE;';
                    END LOOP;
                END $$;
                """;
            await command.ExecuteNonQueryAsync();
        }

        await using var context = NewContext();
        context.AppointmentTypes.AddRange(
            Domain.AppointmentTypes.AppointmentType.CreateFixedSet());
        context.SystemSettings.Add(Domain.Settings.SystemSettings.CreateDefault());
        await EnsureEmployeeGroupsSeededAsync(context);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Repairs change-controlled Employee Group rows after truncation. Truncating
    /// appointment_type cascades into the mapping table, so migration-seeded mappings are
    /// re-inserted when missing. Production databases rely on the migration alone.
    /// </summary>
    private static async Task EnsureEmployeeGroupsSeededAsync(EventBookingDbContext context)
    {
        var fixedGroups = new[]
        {
            EmployeeGroup.Define(
                EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting]),
            EmployeeGroup.Define(
                EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
            EmployeeGroup.Define(
                EmployeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT", "Ground Operations Agent", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            EmployeeGroup.Define(
                EmployeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            EmployeeGroup.Define(
                EmployeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES", "Ground Transport Services", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting]),
        };

        foreach (var group in fixedGroups)
        {
            var existing = await context.EmployeeGroups
                .Include(persisted => persisted.Requirements)
                .SingleOrDefaultAsync(persisted => persisted.Id == group.Id);
            if (existing is null)
            {
                context.EmployeeGroups.Add(group);
                continue;
            }

            foreach (var requirement in group.Requirements)
            {
                if (existing.Requirements.All(persisted => persisted.AppointmentTypeId != requirement.AppointmentTypeId))
                {
                    context.Add(requirement);
                }
            }
        }
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
`````

## after — tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs — 1/1

<!-- vocabulary-file: {"id":342,"oldPath":"tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs","newPath":"tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs","beforeSha":"5d8f77671859f31d8c8d60cd5976d8813c8d2426b4fc0542677eb8f75b552ebe","afterSha":"156c787ac039310a6daa5bfece000fbe5820319f9cfcb266e9c8c9161ebac257","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace EventBooking.Infrastructure.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("eventbooking")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();
    private NpgsqlDataSource? _dataSource;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _dataSource = NpgsqlDataSource.Create(ConnectionString);

        await using var context = NewContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    public EventBookingDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new EventBookingDbContext(options);
    }

    /// <summary>
    /// Empties every table and re-seeds the fixed rows. Faster and far less flaky than dropping
    /// and re-creating the database between tests.
    /// </summary>
    public async Task ResetAsync()
    {
        var dataSource = _dataSource
            ?? throw new InvalidOperationException("The PostgreSQL fixture has not been initialized.");
        await using var connection = await dataSource.OpenConnectionAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                DO $$
                DECLARE statements CURSOR FOR
                    SELECT tablename FROM pg_tables
                    WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'
                    -- Change-controlled Attendee Group rows stay seeded by the migration.
                    AND tablename NOT IN ('attendee_group', 'attendee_group_requirement');
                BEGIN
                    FOR statement IN statements LOOP
                        EXECUTE 'TRUNCATE TABLE ' || quote_ident(statement.tablename) || ' CASCADE;';
                    END LOOP;
                END $$;
                """;
            await command.ExecuteNonQueryAsync();
        }

        await using var context = NewContext();
        context.AppointmentTypes.AddRange(
            Domain.AppointmentTypes.AppointmentType.CreateFixedSet());
        context.SystemSettings.Add(Domain.Settings.SystemSettings.CreateDefault());
        await EnsureAttendeeGroupsSeededAsync(context);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Repairs change-controlled Attendee Group rows after truncation. Truncating
    /// appointment_type cascades into the mapping table, so migration-seeded mappings are
    /// re-inserted when missing. Production databases rely on the migration alone.
    /// </summary>
    private static async Task EnsureAttendeeGroupsSeededAsync(EventBookingDbContext context)
    {
        var fixedGroups = new[]
        {
            AttendeeGroup.Define(
                AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting]),
            AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
            AttendeeGroup.Define(
                AttendeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT", "Ground Operations Agent", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            AttendeeGroup.Define(
                AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            AttendeeGroup.Define(
                AttendeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES", "Ground Transport Services", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting]),
        };

        foreach (var group in fixedGroups)
        {
            var existing = await context.AttendeeGroups
                .Include(persisted => persisted.Requirements)
                .SingleOrDefaultAsync(persisted => persisted.Id == group.Id);
            if (existing is null)
            {
                context.AttendeeGroups.Add(group);
                continue;
            }

            foreach (var requirement in group.Requirements)
            {
                if (existing.Requirements.All(persisted => persisted.AppointmentTypeId != requirement.AppointmentTypeId))
                {
                    context.Add(requirement);
                }
            }
        }
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
`````

## before — tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs — 1/1

<!-- vocabulary-file: {"id":343,"oldPath":"tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs","beforeSha":"3542f54fb38b2c5c3c5c7362db5d6d093cc7de8ee3b49e7ae662d6af09e59494","afterSha":"368784ca17e7344126ede5b32096bea4685067ba28e47faccfd4a934113a8fe2","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies recovery journey links, ordering, and active uniqueness backstops.</summary>
[Collection("postgres")]
public sealed class RecoveryBookingPersistenceTests(PostgresFixture fixture)
{
    /// <summary>Verifies an original and two concluded recoveries reload as one ordered journey.</summary>
    [Fact]
    public async Task JourneyLinksPersistAndReloadInCreationOrder()
    {
        await fixture.ResetAsync();
        var candidateId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(), candidateId, "initial", DateTimeOffset.UtcNow.AddDays(1),
            [slotId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        var original = Booking.Create(
            Guid.NewGuid(), initial, slotId, "manage-original", DateTimeOffset.UtcNow);
        var first = RecoveryFor(candidateId, original, DateTimeOffset.UtcNow.AddHours(1));
        var second = RecoveryFor(candidateId, original, DateTimeOffset.UtcNow.AddHours(2));
        first.Conclude();
        second.Conclude();

        await using (var write = fixture.NewContext())
        {
            write.Bookings.AddRange(original, first, second);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var journey = await new BookingRepository(read)
            .ListJourneyAsync(original.Id, CancellationToken.None);

        Assert.Equal([original.Id, first.Id, second.Id], journey.Select(b => b.Id));
        Assert.Null(journey[0].RecoveryOfBookingId);
        Assert.Equal(BookingStatus.Active, journey[0].Status);
        Assert.Equal([original.Id, original.Id], journey.Skip(1).Select(b => b.RecoveryOfBookingId));
        Assert.All(journey.Skip(1), b => Assert.Equal(BookingStatus.Concluded, b.Status));
    }

    /// <summary>Verifies a second active original for one candidate violates uniqueness.</summary>
    [Fact]
    public async Task SecondActiveOriginalViolatesUniqueness()
    {
        await fixture.ResetAsync();
        var candidateId = Guid.NewGuid();
        var first = OriginalFor(candidateId);
        var second = OriginalFor(candidateId);

        await using var context = fixture.NewContext();
        context.Bookings.AddRange(first, second);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    /// <summary>Verifies a second active recovery for one root violates uniqueness.</summary>
    [Fact]
    public async Task SecondActiveRecoveryViolatesUniqueness()
    {
        await fixture.ResetAsync();
        var candidateId = Guid.NewGuid();
        var original = OriginalFor(candidateId);
        var first = RecoveryFor(candidateId, original, DateTimeOffset.UtcNow.AddHours(1));
        var second = RecoveryFor(candidateId, original, DateTimeOffset.UtcNow.AddHours(2));

        await using var context = fixture.NewContext();
        context.Bookings.AddRange(original, first, second);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static Booking OriginalFor(Guid candidateId)
    {
        var slotId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidateId, "initial", DateTimeOffset.UtcNow.AddDays(1),
            [slotId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        return Booking.Create(Guid.NewGuid(), invite, slotId, "manage", DateTimeOffset.UtcNow);
    }

    private static Booking RecoveryFor(Guid candidateId, Booking original, DateTimeOffset createdAt)
    {
        var slotId = Guid.NewGuid();
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(), candidateId, original.Id, "recovery", DateTimeOffset.UtcNow.AddDays(2),
            [slotId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        return Booking.CreateRecovery(
            Guid.NewGuid(), invite, original, slotId, $"manage-recovery-{Guid.NewGuid():N}", createdAt);
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs — 1/1

<!-- vocabulary-file: {"id":343,"oldPath":"tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs","beforeSha":"3542f54fb38b2c5c3c5c7362db5d6d093cc7de8ee3b49e7ae662d6af09e59494","afterSha":"368784ca17e7344126ede5b32096bea4685067ba28e47faccfd4a934113a8fe2","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies recovery journey links, ordering, and active uniqueness backstops.</summary>
[Collection("postgres")]
public sealed class RecoveryBookingPersistenceTests(PostgresFixture fixture)
{
    /// <summary>Verifies an original and two concluded recoveries reload as one ordered journey.</summary>
    [Fact]
    public async Task JourneyLinksPersistAndReloadInCreationOrder()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(), attendeeId, "initial", DateTimeOffset.UtcNow.AddDays(1),
            [eventId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        var original = Booking.Create(
            Guid.NewGuid(), initial, eventId, "manage-original", DateTimeOffset.UtcNow);
        var first = RecoveryFor(attendeeId, original, DateTimeOffset.UtcNow.AddHours(1));
        var second = RecoveryFor(attendeeId, original, DateTimeOffset.UtcNow.AddHours(2));
        first.Conclude();
        second.Conclude();

        await using (var write = fixture.NewContext())
        {
            write.Bookings.AddRange(original, first, second);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var journey = await new BookingRepository(read)
            .ListJourneyAsync(original.Id, CancellationToken.None);

        Assert.Equal([original.Id, first.Id, second.Id], journey.Select(b => b.Id));
        Assert.Null(journey[0].RecoveryOfBookingId);
        Assert.Equal(BookingStatus.Active, journey[0].Status);
        Assert.Equal([original.Id, original.Id], journey.Skip(1).Select(b => b.RecoveryOfBookingId));
        Assert.All(journey.Skip(1), b => Assert.Equal(BookingStatus.Concluded, b.Status));
    }

    /// <summary>Verifies a second active original for one attendee violates uniqueness.</summary>
    [Fact]
    public async Task SecondActiveOriginalViolatesUniqueness()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var first = OriginalFor(attendeeId);
        var second = OriginalFor(attendeeId);

        await using var context = fixture.NewContext();
        context.Bookings.AddRange(first, second);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    /// <summary>Verifies a second active recovery for one root violates uniqueness.</summary>
    [Fact]
    public async Task SecondActiveRecoveryViolatesUniqueness()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var original = OriginalFor(attendeeId);
        var first = RecoveryFor(attendeeId, original, DateTimeOffset.UtcNow.AddHours(1));
        var second = RecoveryFor(attendeeId, original, DateTimeOffset.UtcNow.AddHours(2));

        await using var context = fixture.NewContext();
        context.Bookings.AddRange(original, first, second);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static Booking OriginalFor(Guid attendeeId)
    {
        var eventId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendeeId, "initial", DateTimeOffset.UtcNow.AddDays(1),
            [eventId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        return Booking.Create(Guid.NewGuid(), invite, eventId, "manage", DateTimeOffset.UtcNow);
    }

    private static Booking RecoveryFor(Guid attendeeId, Booking original, DateTimeOffset createdAt)
    {
        var eventId = Guid.NewGuid();
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(), attendeeId, original.Id, "recovery", DateTimeOffset.UtcNow.AddDays(2),
            [eventId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        return Booking.CreateRecovery(
            Guid.NewGuid(), invite, original, eventId, $"manage-recovery-{Guid.NewGuid():N}", createdAt);
    }
}
`````
