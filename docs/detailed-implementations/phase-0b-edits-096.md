# 00b — Vocabulary edits 96 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs — 1/1

<!-- vocabulary-file: {"id":332,"oldPath":"tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs","newPath":"tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs","beforeSha":"113d8ce953d39304aa1d62a154da47962e946fdd2a19d5afa2380d1283efdb05","afterSha":"3c9bd740071a16b6737c13720746957725aa9fa7562a2cecdfa10bcee323bfd3","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Repositories;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;

namespace EventBooking.Infrastructure.Tests;

/// <summary>
/// A real service provider over a real database, with the mail transport stubbed out. Every
/// confirmation runs in its own scope so that concurrent attempts use separate connections.
/// </summary>
public sealed class ConcurrencyHarness : IAsyncDisposable
{
    private static readonly TimeSpan ReadinessTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(30);

    private readonly PostgresFixture _fixture;
    private readonly ServiceProvider _services;
    private IReadOnlyList<Guid> _fallbackEventIds = [];
    private int _nextEventOffset;

    private ConcurrencyHarness(PostgresFixture fixture, ServiceProvider services)
    {
        _fixture = fixture;
        _services = services;
    }

    /// <summary>Sends nothing. Email delivery is not what this task is testing.</summary>
    private sealed class SilentTransport : IEmailTransport
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    /// <summary>
    /// The two genuine high-capacity options offered beside every event under contention. They are
    /// deliberately kept alive so a losing invite remains valid while the proof runs.
    /// </summary>
    public IReadOnlyList<Guid> FallbackEventIds => _fallbackEventIds;

    public static async Task<ConcurrencyHarness> CreateAsync(PostgresFixture fixture)
    {
        await fixture.ResetAsync();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddEventBookingInfrastructure(
            fixture.ConnectionString,
            new TransitionalLocationOptions("Europe/London"),
            new TokenOptions("a-concurrency-test-signing-key-long-enough"));

        services.AddEventBookingApplication(
            new AttendeePortalOptions("https://booking.example.com", "HQ", "recruitment@example.com"));

        // Nothing registers IEmailTransport above any more — AddEventBookingInfrastructure no
        // longer does that itself, and this harness never calls AddAwsEmailTransport or
        // AddLocalEmailTransport, since email delivery is not what this proof is testing.
        services.AddScoped<IEmailTransport, SilentTransport>();

        var harness = new ConcurrencyHarness(fixture, services.BuildServiceProvider());
        harness._fallbackEventIds =
        [
            await harness.GivenEventAsync(drugAndAlcohol: 100, medical: 100, uniform: 100),
            await harness.GivenEventAsync(drugAndAlcohol: 100, medical: 100, uniform: 100),
        ];

        return harness;
    }

    public async Task<Guid> GivenEventAsync(int drugAndAlcohol, int medical, int uniform)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(
                DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30 + Interlocked.Increment(ref _nextEventOffset)),
                new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), drugAndAlcohol);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), medical);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), uniform);

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        await using var context = _fixture.NewContext();
        context.EventProposals.Add(proposal);
        context.Events.Add(eventItem);
        await context.SaveChangesAsync();

        return eventItem.Id;
    }

    /// <summary>Opens an independent scope with its own connection for one racer.</summary>
    public IServiceScope CreateScope() => _services.CreateScope();

    public async Task<string> GivenInvitedAttendeeAsync(Guid eventId, params Guid[] requiredTypeIds)
    {
        var tokens = _services.GetRequiredService<ITokenService>();

        var group = AttendeeGroup.Define(
            Guid.NewGuid(), $"HARNESS_{Guid.NewGuid():N}".ToUpperInvariant(), "Harness", true,
            requiredTypeIds);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Concurrent Attendee", $"{Guid.NewGuid():N}@mail.com", group);
        attendee.MarkInvited();

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);

        // Every invite is real: the fallback events remain valid options if the contested one fills.
        var invite = Invite.CreateInitial(
            inviteId,
            attendee.Id,
            issued.TokenHash,
            DateTimeOffset.UtcNow.AddDays(4),
            [eventId, .. _fallbackEventIds],
            requiredTypeIds,
            0);

        await using var context = _fixture.NewContext();
        context.AttendeeGroups.Add(group);
        context.Attendees.Add(attendee);
        context.Invites.Add(invite);
        await context.SaveChangesAsync();

        return issued.Token;
    }

    public async Task<Result<ConfirmBookingOutcome>> ConfirmAsync(string token, Guid eventId)
    {
        // A scope per attempt: separate context, separate connection, separate transaction.
        await using var scope = _services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ConfirmBookingHandler>();

        return await handler.HandleAsync(
            new ConfirmBookingCommand(token, eventId), CancellationToken.None);
    }

    /// <summary>
    /// Starts every confirmation only after a real external transaction has acquired the target
    /// event's row lock. Every production handler consequently waits on PostgreSQL before the
    /// guard commits, proving that the work overlaps rather than being merely scheduled together.
    /// </summary>
    public async Task<ConfirmationBatch> ConfirmBatchAsync(
        IReadOnlyCollection<string> tokens,
        Guid eventId)
    {
        var tokenList = tokens.ToArray();
        if (tokenList.Length == 0)
        {
            throw new ArgumentException("At least one confirmation is required.", nameof(tokens));
        }

        await using var guardContext = _fixture.NewContext();
        await using var guardTransaction = await guardContext.Database.BeginTransactionAsync();
        var lockedEvent = await new EventRepository(guardContext)
            .LockForUpdateAsync(eventId, CancellationToken.None);
        if (lockedEvent is null)
        {
            throw new InvalidOperationException("The batch target event does not exist.");
        }

        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readiness = tokenList
            .Select(_ => new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();
        var attempts = tokenList
            .Select((token, index) => ConfirmAfterGateAsync(token, eventId, startGate.Task, readiness[index]))
            .ToArray();

        var guardCommitted = false;
        try
        {
            var backendPids = await Task.WhenAll(readiness.Select(ready => ready.Task))
                .WaitAsync(ReadinessTimeout);
            if (backendPids.Distinct().Count() != tokenList.Length)
            {
                throw new InvalidOperationException("Each confirmation must hold its own PostgreSQL connection.");
            }

            startGate.SetResult();
            var blockedAttemptCount = await WaitUntilAllBlockedOnDatabaseLockAsync(
                guardContext,
                backendPids);

            await guardTransaction.CommitAsync();
            guardCommitted = true;
            var results = await Task.WhenAll(attempts).WaitAsync(CompletionTimeout);

            return new ConfirmationBatch(results, blockedAttemptCount);
        }
        catch
        {
            startGate.TrySetResult();
            if (!guardCommitted)
            {
                await guardTransaction.RollbackAsync();
            }

            await Task.WhenAll(attempts).WaitAsync(CompletionTimeout);
            throw;
        }
    }

    /// <summary>
    /// Reloads a pending invite by its raw token and verifies that all three option IDs identify
    /// active events with spare capacity for the attendee's required appointment types.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> LiveOptionEventIdsAsync(string token)
    {
        var tokenHash = _services.GetRequiredService<ITokenService>().Hash(token);

        await using var context = _fixture.NewContext();
        var invite = await context.Invites
            .Include(i => i.Options)
            .SingleAsync(i => i.TokenHash == tokenHash);
        if (invite.Status != InviteStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending invite can retain live options.");
        }

        var optionIds = invite.OfferedEventIds;
        if (optionIds.Count != Invite.RequiredOptionCount || optionIds.Distinct().Count() != optionIds.Count)
        {
            throw new InvalidOperationException("A live invite must retain three distinct options.");
        }

        var attendee = await context.Attendees
            .Include(c => c.Requirements)
            .SingleAsync(c => c.Id == invite.AttendeeId);
        var events = await context.Events
            .Include(s => s.Capacities)
            .Where(s => optionIds.Contains(s.Id))
            .ToListAsync();

        if (events.Count != optionIds.Count
            || events.Any(eventItem => eventItem.Status != EventStatus.Active)
            || events.Any(eventItem => !eventItem.HasSpareCapacityForAll(attendee.RequiredAppointmentTypeIds)))
        {
            throw new InvalidOperationException("Every invite option must be a live eligible eventItem.");
        }

        return optionIds;
    }

    public async Task<int> RemainingCapacityAsync(Guid eventId, Guid appointmentTypeId)
    {
        await using var context = _fixture.NewContext();
        var eventItem = await context.Events
            .Include(s => s.Capacities)
            .SingleAsync(s => s.Id == eventId);

        return eventItem.CapacityFor(appointmentTypeId).RemainingCapacity;
    }

    public async Task<int> ActiveBookingCountAsync(Guid eventId)
    {
        await using var context = _fixture.NewContext();
        return await context.Bookings.CountAsync(
            b => b.EventId == eventId && b.Status == BookingStatus.Active);
    }

    private async Task<Result<ConfirmBookingOutcome>> ConfirmAfterGateAsync(
        string token,
        Guid eventId,
        Task startGate,
        TaskCompletionSource<int> ready)
    {
        await using var scope = _services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        await context.Database.OpenConnectionAsync();

        try
        {
            ready.SetResult(await GetBackendPidAsync(context));
            await startGate;

            var handler = scope.ServiceProvider.GetRequiredService<ConfirmBookingHandler>();
            return await handler.HandleAsync(
                new ConfirmBookingCommand(token, eventId), CancellationToken.None);
        }
        catch (Exception exception)
        {
            ready.TrySetException(exception);
            throw;
        }
    }

    private static async Task<int> GetBackendPidAsync(EventBookingDbContext context)
    {
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_backend_pid();";
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<int> WaitUntilAllBlockedOnDatabaseLockAsync(
        EventBookingDbContext lockOwner,
        IReadOnlyCollection<int> backendPids)
    {
        var connection = (NpgsqlConnection)lockOwner.Database.GetDbConnection();

        for (var attempt = 0; attempt < 200; attempt++)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (NpgsqlTransaction)lockOwner.Database.CurrentTransaction!
                .GetDbTransaction();
            command.CommandText =
                "SELECT COUNT(*) FROM pg_stat_activity WHERE pid = ANY(@backend_pids) AND wait_event_type = 'Lock';";
            command.Parameters.AddWithValue(
                "backend_pids",
                NpgsqlDbType.Array | NpgsqlDbType.Integer,
                backendPids.ToArray());

            var blockedCount = Convert.ToInt32(await command.ExecuteScalarAsync());
            if (blockedCount == backendPids.Count)
            {
                return blockedCount;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException(
            $"Only a subset of the {backendPids.Count} confirmation connections waited on PostgreSQL's row lock.");
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();
}

public sealed record ConfirmationBatch(
    IReadOnlyList<Result<ConfirmBookingOutcome>> Results,
    int BlockedAttemptCount);
`````

## before — tests/EventBooking.Infrastructure.Tests/ConfirmedSlotPersistenceTests.cs — 1/1

<!-- vocabulary-file: {"id":333,"oldPath":"tests/EventBooking.Infrastructure.Tests/ConfirmedSlotPersistenceTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/EventPersistenceTests.cs","beforeSha":"c359a08ddb48a90f859c43f191ad515450e37ee37f154c04743d6555e657a115","afterSha":"0d22aab55787dca35e132a550e4eb7e17158cd1072c82374b8e830c70c6159e2","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Slots;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class ConfirmedSlotPersistenceTests(PostgresFixture fixture)
{
    private static Dictionary<Guid, int> FullHeadcounts() => new()
    {
        [AppointmentTypeIds.DrugAndAlcoholTesting] = 10,
        [AppointmentTypeIds.MedicalCheckUp] = 6,
        [AppointmentTypeIds.UniformFitting] = 8,
    };

    [Fact]
    public async Task AnImportedSlotPersistsWithANullProposalId()
    {
        await using var context = fixture.NewContext();
        var window = new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));
        var slot = ConfirmedSlot.CreateImported(Guid.NewGuid(), window, FullHeadcounts());

        context.ConfirmedSlots.Add(slot);
        await context.SaveChangesAsync();

        await using var reload = fixture.NewContext();
        var reloaded = await reload.ConfirmedSlots
            .Include(s => s.Capacities)
            .SingleAsync(s => s.Id == slot.Id);

        Assert.Null(reloaded.ProposalId);
        Assert.Equal(3, reloaded.Capacities.Count);

        await fixture.ResetAsync();
    }

    [Fact]
    public async Task TwoImportedSlotsCanBothHaveANullProposalId()
    {
        await using var context = fixture.NewContext();
        var first = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, 12), new TimeOnly(9, 0)), FullHeadcounts());
        var second = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, 13), new TimeOnly(9, 0)), FullHeadcounts());

        context.ConfirmedSlots.AddRange(first, second);

        // Proves the existing unique index on proposal_id treats a missing value as distinct
        // (standard SQL and Postgres semantics) rather than colliding two imported slots together.
        await context.SaveChangesAsync();

        await fixture.ResetAsync();
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/EventPersistenceTests.cs — 1/1

<!-- vocabulary-file: {"id":333,"oldPath":"tests/EventBooking.Infrastructure.Tests/ConfirmedSlotPersistenceTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/EventPersistenceTests.cs","beforeSha":"c359a08ddb48a90f859c43f191ad515450e37ee37f154c04743d6555e657a115","afterSha":"0d22aab55787dca35e132a550e4eb7e17158cd1072c82374b8e830c70c6159e2","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class EventPersistenceTests(PostgresFixture fixture)
{
    private static Dictionary<Guid, int> FullHeadcounts() => new()
    {
        [AppointmentTypeIds.DrugAndAlcoholTesting] = 10,
        [AppointmentTypeIds.MedicalCheckUp] = 6,
        [AppointmentTypeIds.UniformFitting] = 8,
    };

    [Fact]
    public async Task AnImportedEventPersistsWithANullProposalId()
    {
        await using var context = fixture.NewContext();
        var window = new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));
        var eventItem = Event.CreateImported(Guid.NewGuid(), window, FullHeadcounts());

        context.Events.Add(eventItem);
        await context.SaveChangesAsync();

        await using var reload = fixture.NewContext();
        var reloaded = await reload.Events
            .Include(s => s.Capacities)
            .SingleAsync(s => s.Id == eventItem.Id);

        Assert.Null(reloaded.ProposalId);
        Assert.Equal(3, reloaded.Capacities.Count);

        await fixture.ResetAsync();
    }

    [Fact]
    public async Task TwoImportedEventsCanBothHaveANullProposalId()
    {
        await using var context = fixture.NewContext();
        var first = Event.CreateImported(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 12), new TimeOnly(9, 0)), FullHeadcounts());
        var second = Event.CreateImported(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 13), new TimeOnly(9, 0)), FullHeadcounts());

        context.Events.AddRange(first, second);

        // Proves the existing unique index on proposal_id treats a missing value as distinct
        // (standard SQL and Postgres semantics) rather than colliding two imported events together.
        await context.SaveChangesAsync();

        await fixture.ResetAsync();
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs — 1/1

<!-- vocabulary-file: {"id":334,"oldPath":"tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs","beforeSha":"d0940eb028c2a2d7e1c383a2959153f38adbea6816b282813163aaac6ed0f8cc","afterSha":"6ccf62b67c557f0f1d28f4c4e763a7cd062582642dae08c6971b60607bd92f15","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class DashboardQueryTests(PostgresFixture fixture)
{
    private sealed class MovableLondonClock(DateTimeOffset now) : IClock
    {
        private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

        public DateTimeOffset UtcNow { get; set; } = now;

        /// <summary>Gets the current instant converted to the London head-office time zone.</summary>
        public DateTimeOffset NowAtHeadOffice => TimeZoneInfo.ConvertTime(UtcNow, London);

        public DateOnly TodayAtHeadOffice => DateAtHeadOffice(UtcNow);

        public DateOnly DateAtHeadOffice(DateTimeOffset instant) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, London).DateTime);

        public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) =>
            TimeZoneInfo.ConvertTime(instant, London);
    }

    [Fact]
    public async Task AwaitingCandidatesUseHeadOfficeDatesAcrossTheUtcMidnightBoundary()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 2, 23, 30, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var awaitingGroup = EmployeeGroup.Define(
                Guid.NewGuid(), "DASH_DAT_MED", "Dashboard DAT MED", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp]);
            write.EmployeeGroups.Add(awaitingGroup);
            var candidate = Candidate.Create(
                Guid.NewGuid(), "C. Diallo", "c.diallo@mail.com", awaitingGroup);
            candidate.MarkAwaitingAvailability();
            write.Candidates.Add(candidate);
            await write.SaveChangesAsync();
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 3, 23, 30, 0, TimeSpan.Zero);

        await using var read = NewContext(clock);
        var row = Assert.Single(await new DashboardQueries(read, clock)
            .AwaitingAvailabilityAsync(CancellationToken.None));

        Assert.Equal("C. Diallo", row.Name);
        Assert.Equal(new[] { "DAT", "MED" }, row.RequiredCodes);
        Assert.Equal(new DateOnly(2026, 9, 3), row.WaitingSince);
        Assert.Equal(1, row.DaysWaiting);
    }

    [Fact]
    public async Task TheStatusStampIsWrittenOnAddAndMovesOnlyWhenStatusMoves()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero));

        Guid candidateId;
        await using (var write = NewContext(clock))
        {
            var uniformOnly = EmployeeGroup.Define(
                Guid.NewGuid(), "DASH_UNI", "Dashboard UNI", true,
                [AppointmentTypeIds.UniformFitting]);
            write.EmployeeGroups.Add(uniformOnly);
            var candidate = Candidate.Create(
                Guid.NewGuid(), "B. Chen", "b.chen@mail.com", uniformOnly);
            write.Candidates.Add(candidate);
            await write.SaveChangesAsync();
            candidateId = candidate.Id;
        }

        await using (var addedRead = NewContext(clock))
        {
            var added = await addedRead.Candidates.SingleAsync(c => c.Id == candidateId);
            Assert.Equal(clock.UtcNow, addedRead.Entry(added)
                .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
        await using (var rename = NewContext(clock))
        {
            var candidate = await rename.Candidates.SingleAsync(c => c.Id == candidateId);
            candidate.UpdateDetails("B. Chen-Smith", "b.chen@mail.com");
            await rename.SaveChangesAsync();
        }

        await using (var renamedRead = NewContext(clock))
        {
            var renamed = await renamedRead.Candidates.SingleAsync(c => c.Id == candidateId);
            Assert.Equal(
                new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero),
                renamedRead.Entry(renamed)
                    .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
        await using (var statusChange = NewContext(clock))
        {
            var candidate = await statusChange.Candidates.SingleAsync(c => c.Id == candidateId);
            candidate.MarkAwaitingAvailability();
            await statusChange.SaveChangesAsync();
        }

        await using var changedRead = NewContext(clock);
        var changed = await changedRead.Candidates.SingleAsync(c => c.Id == candidateId);
        Assert.Equal(clock.UtcNow, changedRead.Entry(changed)
            .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
    }

    [Fact]
    public async Task TheSynchronousSavePathStampsAddsAndStatusChangesButNotUnrelatedEdits()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero));

        Guid candidateId;
        await using (var write = NewContext(clock))
        {
            var uniformOnly = EmployeeGroup.Define(
                Guid.NewGuid(), "DASH_UNI_TWO", "Dashboard UNI two", true,
                [AppointmentTypeIds.UniformFitting]);
            write.EmployeeGroups.Add(uniformOnly);
            var candidate = Candidate.Create(
                Guid.NewGuid(), "S. Patel", "s.patel@mail.com", uniformOnly);
            write.Candidates.Add(candidate);
            write.SaveChanges();
            candidateId = candidate.Id;
        }

        await using (var addedRead = NewContext(clock))
        {
            var candidate = await addedRead.Candidates.SingleAsync(c => c.Id == candidateId);
            Assert.Equal(clock.UtcNow, addedRead.Entry(candidate)
                .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
        await using (var rename = NewContext(clock))
        {
            var candidate = await rename.Candidates.SingleAsync(c => c.Id == candidateId);
            candidate.UpdateDetails("S. Patel-Jones", "s.patel@mail.com");
            rename.SaveChanges();
        }

        await using (var renamedRead = NewContext(clock))
        {
            var candidate = await renamedRead.Candidates.SingleAsync(c => c.Id == candidateId);
            Assert.Equal(
                new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero),
                renamedRead.Entry(candidate)
                    .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
        await using (var statusChange = NewContext(clock))
        {
            var candidate = await statusChange.Candidates.SingleAsync(c => c.Id == candidateId);
            candidate.MarkAwaitingAvailability();
            statusChange.SaveChanges();
        }

        await using var changedRead = NewContext(clock);
        var changed = await changedRead.Candidates.SingleAsync(c => c.Id == candidateId);
        Assert.Equal(clock.UtcNow, changedRead.Entry(changed)
            .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
    }

    [Fact]
    public async Task TheFollowUpListUsesTheHeadOfficeDayTheAutoRetryGaveUp()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 2, 23, 30, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var groundOps = write.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.GroundOperationsAgent);
            var candidate = Candidate.Create(
                Guid.NewGuid(), "D. Reyes", "d.reyes@mail.com", groundOps);
            candidate.MarkInvited();
            candidate.MarkNoResponse();
            write.Candidates.Add(candidate);
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var row = Assert.Single(await new DashboardQueries(read, clock)
            .NoResponseAsync(CancellationToken.None));

        Assert.Equal("D. Reyes", row.Name);
        Assert.Equal(new DateOnly(2026, 9, 3), row.GaveUpOn);
    }

    [Fact]
    public async Task TheSlotsOverviewShowsCapacityAndActiveBookingCount()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var slots = new[]
            {
                SlotFor(new DateOnly(2026, 9, 21), new TimeOnly(9, 0)),
                SlotFor(new DateOnly(2026, 9, 22), new TimeOnly(9, 0)),
                SlotFor(new DateOnly(2026, 9, 23), new TimeOnly(9, 0)),
            };
            slots[0].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();

            var pilots = write.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
            var candidate = Candidate.Create(
                Guid.NewGuid(), "E. Martin", "e.martin@mail.com", pilots);
            candidate.MarkInvited();
            var invite = Invite.CreateInitial(
                Guid.NewGuid(), candidate.Id, "invite-hash", clock.UtcNow.AddDays(4),
                slots.Select(s => s.Id), candidate.RequiredAppointmentTypeIds, 0);
            var booking = Booking.Create(
                Guid.NewGuid(), invite, slots[0].Id, "booking-hash", clock.UtcNow);

            write.ConfirmedSlots.AddRange(slots);
            write.Candidates.Add(candidate);
            write.Invites.Add(invite);
            write.Bookings.Add(booking);
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var rows = await new DashboardQueries(read, clock).SlotsOverviewAsync(CancellationToken.None);
        var row = rows.Single(s => s.Date == new DateOnly(2026, 9, 21));

        Assert.Equal(new TimeOnly(13, 0), row.EndTime);
        Assert.Equal(1, row.ActiveBookings);
        Assert.Equal(new[] { "DAT", "MED", "UNI" }, row.Capacities.Select(c => c.Code));
        var drugAndAlcohol = row.Capacities.Single(c => c.Code == "DAT");
        Assert.Equal(10, drugAndAlcohol.TotalHeadcount);
        Assert.Equal(9, drugAndAlcohol.RemainingCapacity);
    }

    [Fact]
    public async Task ACancelledSlotIsNotOnTheOverview()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var slot = SlotFor(new DateOnly(2026, 9, 21), new TimeOnly(9, 0));
            slot.Cancel();
            write.ConfirmedSlots.Add(slot);
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        Assert.Empty(await new DashboardQueries(read, clock).SlotsOverviewAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OnlyTheLatestEmailLogRowPerCandidateIsReturned()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
        var candidateId = Guid.NewGuid();

        await using (var write = NewContext(clock))
        {
            var pilots = write.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
            var candidate = Candidate.Create(
                candidateId, "A. Novak", "a.novak@mail.com", pilots);
            write.Candidates.Add(candidate);
            write.EmailLogs.Add(EmailLog.Record(
                Guid.NewGuid(), candidateId, EmailTemplate.CandidateInvite,
                new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero), EmailStatus.Resolved));
            write.EmailLogs.Add(EmailLog.Record(
                Guid.NewGuid(), candidateId, EmailTemplate.CandidateReinvite,
                new DateTimeOffset(2026, 9, 2, 9, 0, 0, TimeSpan.Zero), EmailStatus.Sent));
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var row = Assert.Single(
            await new DashboardQueries(read, clock).LatestEmailStatusAsync(CancellationToken.None));

        Assert.Equal(candidateId, row.CandidateId);
        Assert.Equal(EmailTemplate.CandidateReinvite, row.TemplateName);
        Assert.Equal(EmailStatus.Sent, row.Status);
    }

    [Fact]
    public async Task ACandidateWithNoEmailLogRowsIsAbsent()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var uniformOnly = EmployeeGroup.Define(
                Guid.NewGuid(), "DASH_UNI_THREE", "Dashboard UNI three", true,
                [AppointmentTypeIds.UniformFitting]);
            write.EmployeeGroups.Add(uniformOnly);
            write.Candidates.Add(Candidate.Create(
                Guid.NewGuid(), "B. Chen", "b.chen@mail.com", uniformOnly));
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        Assert.Empty(
            await new DashboardQueries(read, clock).LatestEmailStatusAsync(CancellationToken.None));
    }

    /// <summary>Retry visibility follows the latest delivery's current candidate and slot context.</summary>
    [Fact]
    public async Task LatestEmailStatusMarksOnlyActionableDeliveryContextAsRetryable()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
        var actionableId = Guid.NewGuid();
        var staleId = Guid.NewGuid();
        var cancelledSlot = SlotFor(new DateOnly(2026, 9, 21), new TimeOnly(9, 0));
        cancelledSlot.Cancel();
        // Production stages the cancellation notice with its booking identifier, so the
        // retryable delivery carries one; the stale delivery below omits it on purpose.
        var cancelledBookingId = Guid.NewGuid();
        var outstanding = EmailLog.RecordPending(
            Guid.NewGuid(),
            actionableId,
            EmailTemplate.SlotCancelledRebookingNeeded,
            clock.UtcNow,
            bookingId: cancelledBookingId,
            confirmedSlotId: cancelledSlot.Id);
        var laterSent = EmailLog.Record(
            Guid.NewGuid(),
            actionableId,
            EmailTemplate.CandidateInvite,
            clock.UtcNow.AddMinutes(3),
            EmailStatus.Sent);

        await using (var write = NewContext(clock))
        {
            var pilots = write.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
            var actionableCandidate = Candidate.Create(
                actionableId, "Actionable", "actionable@mail.com", pilots);
            actionableCandidate.MarkAwaitingAvailability();
            var staleCandidate = Candidate.Create(
                staleId, "Stale", "stale@mail.com", pilots);
            var cancelledInvite = Invite.CreateInitial(
                Guid.NewGuid(), actionableId, "cancelled-invite-hash", clock.UtcNow.AddDays(4),
                [cancelledSlot.Id, Guid.NewGuid(), Guid.NewGuid()],
                actionableCandidate.RequiredAppointmentTypeIds, 0);
            var cancelledBooking = Booking.Create(
                cancelledBookingId, cancelledInvite, cancelledSlot.Id, "cancelled-booking-hash", clock.UtcNow);
            cancelledBooking.Cancel();
            write.Candidates.AddRange(actionableCandidate, staleCandidate);
            write.ConfirmedSlots.Add(cancelledSlot);
            write.Invites.Add(cancelledInvite);
            write.Bookings.Add(cancelledBooking);
            write.EmailLogs.AddRange(
                outstanding,
                laterSent,
                EmailLog.RecordPending(
                    Guid.NewGuid(),
                    staleCandidate.Id,
                    EmailTemplate.SlotCancelledRebookingNeeded,
                    clock.UtcNow,
                    confirmedSlotId: cancelledSlot.Id));
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var rows = await new DashboardQueries(read, clock)
            .LatestEmailStatusAsync(CancellationToken.None);

        var actionable = rows.Single(row => row.CandidateId == actionableId);
        Assert.True(actionable.CanRetry);
        Assert.Equal(EmailTemplate.SlotCancelledRebookingNeeded, actionable.TemplateName);
        Assert.Equal(EmailStatus.Pending, actionable.Status);
        Assert.False(rows.Single(row => row.CandidateId == staleId).CanRetry);

        await using (var resolve = NewContext(clock))
        {
            var persisted = await resolve.EmailLogs.SingleAsync(delivery => delivery.Id == outstanding.Id);
            persisted.MarkResolved(clock.UtcNow.AddMinutes(2));
            await resolve.SaveChangesAsync();
        }

        await using var reread = NewContext(clock);
        var terminal = (await new DashboardQueries(reread, clock)
            .LatestEmailStatusAsync(CancellationToken.None))
            .Single(row => row.CandidateId == actionableId);
        Assert.Equal(EmailTemplate.CandidateInvite, terminal.TemplateName);
        Assert.Equal(EmailStatus.Sent, terminal.Status);
        Assert.False(terminal.CanRetry);
    }

    private EventBookingDbContext NewContext(IClock clock)
    {
        var options = new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .AddInterceptors(new StatusStampingInterceptor(clock))
            .Options;

        return new EventBookingDbContext(options);
    }

    private static ConfirmedSlot SlotFor(DateOnly date, TimeOnly startTime)
    {
        var proposal = SlotProposal.Create(Guid.NewGuid(), new SlotWindow(date, startTime), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        return ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs — 1/1

<!-- vocabulary-file: {"id":334,"oldPath":"tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs","beforeSha":"d0940eb028c2a2d7e1c383a2959153f38adbea6816b282813163aaac6ed0f8cc","afterSha":"6ccf62b67c557f0f1d28f4c4e763a7cd062582642dae08c6971b60607bd92f15","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class DashboardQueryTests(PostgresFixture fixture)
{
    private sealed class MovableLondonClock(DateTimeOffset now) : IClock
    {
        private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

        public DateTimeOffset UtcNow { get; set; } = now;

        /// <summary>Gets the current instant converted to the London transitional-location time zone.</summary>
        public DateTimeOffset NowAtTransitionalLocation => TimeZoneInfo.ConvertTime(UtcNow, London);

        public DateOnly TodayAtTransitionalLocation => DateAtTransitionalLocation(UtcNow);

        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, London).DateTime);

        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) =>
            TimeZoneInfo.ConvertTime(instant, London);
    }

    [Fact]
    public async Task AwaitingAttendeesUseTransitionalLocationDatesAcrossTheUtcMidnightBoundary()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 2, 23, 30, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var awaitingGroup = AttendeeGroup.Define(
                Guid.NewGuid(), "DASH_DAT_MED", "Dashboard DAT MED", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp]);
            write.AttendeeGroups.Add(awaitingGroup);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "C. Diallo", "c.diallo@mail.com", awaitingGroup);
            attendee.MarkAwaitingAvailability();
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 3, 23, 30, 0, TimeSpan.Zero);

        await using var read = NewContext(clock);
        var row = Assert.Single(await new DashboardQueries(read, clock)
            .AwaitingAvailabilityAsync(CancellationToken.None));

        Assert.Equal("C. Diallo", row.Name);
        Assert.Equal(new[] { "DAT", "MED" }, row.RequiredCodes);
        Assert.Equal(new DateOnly(2026, 9, 3), row.WaitingSince);
        Assert.Equal(1, row.DaysWaiting);
    }

    [Fact]
    public async Task TheStatusStampIsWrittenOnAddAndMovesOnlyWhenStatusMoves()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero));

        Guid attendeeId;
        await using (var write = NewContext(clock))
        {
            var uniformOnly = AttendeeGroup.Define(
                Guid.NewGuid(), "DASH_UNI", "Dashboard UNI", true,
                [AppointmentTypeIds.UniformFitting]);
            write.AttendeeGroups.Add(uniformOnly);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "B. Chen", "b.chen@mail.com", uniformOnly);
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
            attendeeId = attendee.Id;
        }

        await using (var addedRead = NewContext(clock))
        {
            var added = await addedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
            Assert.Equal(clock.UtcNow, addedRead.Entry(added)
                .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
        await using (var rename = NewContext(clock))
        {
            var attendee = await rename.Attendees.SingleAsync(c => c.Id == attendeeId);
            attendee.UpdateDetails("B. Chen-Smith", "b.chen@mail.com");
            await rename.SaveChangesAsync();
        }

        await using (var renamedRead = NewContext(clock))
        {
            var renamed = await renamedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
            Assert.Equal(
                new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero),
                renamedRead.Entry(renamed)
                    .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
        await using (var statusChange = NewContext(clock))
        {
            var attendee = await statusChange.Attendees.SingleAsync(c => c.Id == attendeeId);
            attendee.MarkAwaitingAvailability();
            await statusChange.SaveChangesAsync();
        }

        await using var changedRead = NewContext(clock);
        var changed = await changedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
        Assert.Equal(clock.UtcNow, changedRead.Entry(changed)
            .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
    }

    [Fact]
    public async Task TheSynchronousSavePathStampsAddsAndStatusChangesButNotUnrelatedEdits()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero));

        Guid attendeeId;
        await using (var write = NewContext(clock))
        {
            var uniformOnly = AttendeeGroup.Define(
                Guid.NewGuid(), "DASH_UNI_TWO", "Dashboard UNI two", true,
                [AppointmentTypeIds.UniformFitting]);
            write.AttendeeGroups.Add(uniformOnly);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "S. Patel", "s.patel@mail.com", uniformOnly);
            write.Attendees.Add(attendee);
            write.SaveChanges();
            attendeeId = attendee.Id;
        }

        await using (var addedRead = NewContext(clock))
        {
            var attendee = await addedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
            Assert.Equal(clock.UtcNow, addedRead.Entry(attendee)
                .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
        await using (var rename = NewContext(clock))
        {
            var attendee = await rename.Attendees.SingleAsync(c => c.Id == attendeeId);
            attendee.UpdateDetails("S. Patel-Jones", "s.patel@mail.com");
            rename.SaveChanges();
        }

        await using (var renamedRead = NewContext(clock))
        {
            var attendee = await renamedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
            Assert.Equal(
                new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero),
                renamedRead.Entry(attendee)
                    .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
        await using (var statusChange = NewContext(clock))
        {
            var attendee = await statusChange.Attendees.SingleAsync(c => c.Id == attendeeId);
            attendee.MarkAwaitingAvailability();
            statusChange.SaveChanges();
        }

        await using var changedRead = NewContext(clock);
        var changed = await changedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
        Assert.Equal(clock.UtcNow, changedRead.Entry(changed)
            .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
    }

    [Fact]
    public async Task TheFollowUpListUsesTheTransitionalLocationDayTheAutoRetryGaveUp()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 2, 23, 30, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var groundOps = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.GroundOperationsAgent);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "D. Reyes", "d.reyes@mail.com", groundOps);
            attendee.MarkInvited();
            attendee.MarkNoResponse();
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var row = Assert.Single(await new DashboardQueries(read, clock)
            .NoResponseAsync(CancellationToken.None));

        Assert.Equal("D. Reyes", row.Name);
        Assert.Equal(new DateOnly(2026, 9, 3), row.GaveUpOn);
    }

    [Fact]
    public async Task TheEventsOverviewShowsCapacityAndActiveBookingCount()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var events = new[]
            {
                EventFor(new DateOnly(2026, 9, 21), new TimeOnly(9, 0)),
                EventFor(new DateOnly(2026, 9, 22), new TimeOnly(9, 0)),
                EventFor(new DateOnly(2026, 9, 23), new TimeOnly(9, 0)),
            };
            events[0].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();

            var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "E. Martin", "e.martin@mail.com", pilots);
            attendee.MarkInvited();
            var invite = Invite.CreateInitial(
                Guid.NewGuid(), attendee.Id, "invite-hash", clock.UtcNow.AddDays(4),
                events.Select(s => s.Id), attendee.RequiredAppointmentTypeIds, 0);
            var booking = Booking.Create(
                Guid.NewGuid(), invite, events[0].Id, "booking-hash", clock.UtcNow);

            write.Events.AddRange(events);
            write.Attendees.Add(attendee);
            write.Invites.Add(invite);
            write.Bookings.Add(booking);
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var rows = await new DashboardQueries(read, clock).EventsOverviewAsync(CancellationToken.None);
        var row = rows.Single(s => s.Date == new DateOnly(2026, 9, 21));

        Assert.Equal(new TimeOnly(13, 0), row.EndTime);
        Assert.Equal(1, row.ActiveBookings);
        Assert.Equal(new[] { "DAT", "MED", "UNI" }, row.Capacities.Select(c => c.Code));
        var drugAndAlcohol = row.Capacities.Single(c => c.Code == "DAT");
        Assert.Equal(10, drugAndAlcohol.TotalHeadcount);
        Assert.Equal(9, drugAndAlcohol.RemainingCapacity);
    }

    [Fact]
    public async Task ACancelledEventIsNotOnTheOverview()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var eventItem = EventFor(new DateOnly(2026, 9, 21), new TimeOnly(9, 0));
            eventItem.Cancel();
            write.Events.Add(eventItem);
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        Assert.Empty(await new DashboardQueries(read, clock).EventsOverviewAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OnlyTheLatestEmailLogRowPerAttendeeIsReturned()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
        var attendeeId = Guid.NewGuid();

        await using (var write = NewContext(clock))
        {
            var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var attendee = Attendee.Create(
                attendeeId, "A. Novak", "a.novak@mail.com", pilots);
            write.Attendees.Add(attendee);
            write.EmailLogs.Add(EmailLog.Record(
                Guid.NewGuid(), attendeeId, EmailTemplate.AttendeeInvite,
                new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero), EmailStatus.Resolved));
            write.EmailLogs.Add(EmailLog.Record(
                Guid.NewGuid(), attendeeId, EmailTemplate.AttendeeReinvite,
                new DateTimeOffset(2026, 9, 2, 9, 0, 0, TimeSpan.Zero), EmailStatus.Sent));
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var row = Assert.Single(
            await new DashboardQueries(read, clock).LatestEmailStatusAsync(CancellationToken.None));

        Assert.Equal(attendeeId, row.AttendeeId);
        Assert.Equal(EmailTemplate.AttendeeReinvite, row.TemplateName);
        Assert.Equal(EmailStatus.Sent, row.Status);
    }

    [Fact]
    public async Task AAttendeeWithNoEmailLogRowsIsAbsent()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var uniformOnly = AttendeeGroup.Define(
                Guid.NewGuid(), "DASH_UNI_THREE", "Dashboard UNI three", true,
                [AppointmentTypeIds.UniformFitting]);
            write.AttendeeGroups.Add(uniformOnly);
            write.Attendees.Add(Attendee.Create(
                Guid.NewGuid(), "B. Chen", "b.chen@mail.com", uniformOnly));
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        Assert.Empty(
            await new DashboardQueries(read, clock).LatestEmailStatusAsync(CancellationToken.None));
    }

    /// <summary>Retry visibility follows the latest delivery's current attendee and event context.</summary>
    [Fact]
    public async Task LatestEmailStatusMarksOnlyActionableDeliveryContextAsRetryable()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
        var actionableId = Guid.NewGuid();
        var staleId = Guid.NewGuid();
        var cancelledEvent = EventFor(new DateOnly(2026, 9, 21), new TimeOnly(9, 0));
        cancelledEvent.Cancel();
        // Production stages the cancellation notice with its booking identifier, so the
        // retryable delivery carries one; the stale delivery below omits it on purpose.
        var cancelledBookingId = Guid.NewGuid();
        var outstanding = EmailLog.RecordPending(
            Guid.NewGuid(),
            actionableId,
            EmailTemplate.EventCancelledRebookingNeeded,
            clock.UtcNow,
            bookingId: cancelledBookingId,
            eventId: cancelledEvent.Id);
        var laterSent = EmailLog.Record(
            Guid.NewGuid(),
            actionableId,
            EmailTemplate.AttendeeInvite,
            clock.UtcNow.AddMinutes(3),
            EmailStatus.Sent);

        await using (var write = NewContext(clock))
        {
            var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var actionableAttendee = Attendee.Create(
                actionableId, "Actionable", "actionable@mail.com", pilots);
            actionableAttendee.MarkAwaitingAvailability();
            var staleAttendee = Attendee.Create(
                staleId, "Stale", "stale@mail.com", pilots);
            var cancelledInvite = Invite.CreateInitial(
                Guid.NewGuid(), actionableId, "cancelled-invite-hash", clock.UtcNow.AddDays(4),
                [cancelledEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
                actionableAttendee.RequiredAppointmentTypeIds, 0);
            var cancelledBooking = Booking.Create(
                cancelledBookingId, cancelledInvite, cancelledEvent.Id, "cancelled-booking-hash", clock.UtcNow);
            cancelledBooking.Cancel();
            write.Attendees.AddRange(actionableAttendee, staleAttendee);
            write.Events.Add(cancelledEvent);
            write.Invites.Add(cancelledInvite);
            write.Bookings.Add(cancelledBooking);
            write.EmailLogs.AddRange(
                outstanding,
                laterSent,
                EmailLog.RecordPending(
                    Guid.NewGuid(),
                    staleAttendee.Id,
                    EmailTemplate.EventCancelledRebookingNeeded,
                    clock.UtcNow,
                    eventId: cancelledEvent.Id));
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var rows = await new DashboardQueries(read, clock)
            .LatestEmailStatusAsync(CancellationToken.None);

        var actionable = rows.Single(row => row.AttendeeId == actionableId);
        Assert.True(actionable.CanRetry);
        Assert.Equal(EmailTemplate.EventCancelledRebookingNeeded, actionable.TemplateName);
        Assert.Equal(EmailStatus.Pending, actionable.Status);
        Assert.False(rows.Single(row => row.AttendeeId == staleId).CanRetry);

        await using (var resolve = NewContext(clock))
        {
            var persisted = await resolve.EmailLogs.SingleAsync(delivery => delivery.Id == outstanding.Id);
            persisted.MarkResolved(clock.UtcNow.AddMinutes(2));
            await resolve.SaveChangesAsync();
        }

        await using var reread = NewContext(clock);
        var terminal = (await new DashboardQueries(reread, clock)
            .LatestEmailStatusAsync(CancellationToken.None))
            .Single(row => row.AttendeeId == actionableId);
        Assert.Equal(EmailTemplate.AttendeeInvite, terminal.TemplateName);
        Assert.Equal(EmailStatus.Sent, terminal.Status);
        Assert.False(terminal.CanRetry);
    }

    private EventBookingDbContext NewContext(IClock clock)
    {
        var options = new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .AddInterceptors(new StatusStampingInterceptor(clock))
            .Options;

        return new EventBookingDbContext(options);
    }

    private static Event EventFor(DateOnly date, TimeOnly startTime)
    {
        var proposal = EventProposal.Create(Guid.NewGuid(), new EventWindow(date, startTime), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        return Event.CreateFrom(Guid.NewGuid(), proposal);
    }
}
`````
