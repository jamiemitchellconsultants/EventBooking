# 00a — Port source 72 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs","encoding":"utf8","sha256":"113d8ce953d39304aa1d62a154da47962e946fdd2a19d5afa2380d1283efdb05","parts":1,"part":1} -->

`````csharp
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;
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
    private IReadOnlyList<Guid> _fallbackSlotIds = [];
    private int _nextSlotOffset;

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
    /// The two genuine high-capacity options offered beside every slot under contention. They are
    /// deliberately kept alive so a losing invite remains valid while the proof runs.
    /// </summary>
    public IReadOnlyList<Guid> FallbackSlotIds => _fallbackSlotIds;

    public static async Task<ConcurrencyHarness> CreateAsync(PostgresFixture fixture)
    {
        await fixture.ResetAsync();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddEventBookingInfrastructure(
            fixture.ConnectionString,
            new HeadOfficeOptions("Europe/London"),
            new TokenOptions("a-concurrency-test-signing-key-long-enough"));

        services.AddEventBookingApplication(
            new CandidatePortalOptions("https://booking.example.com", "HQ", "recruitment@example.com"));

        // Nothing registers IEmailTransport above any more — AddEventBookingInfrastructure no
        // longer does that itself, and this harness never calls AddAwsEmailTransport or
        // AddLocalEmailTransport, since email delivery is not what this proof is testing.
        services.AddScoped<IEmailTransport, SilentTransport>();

        var harness = new ConcurrencyHarness(fixture, services.BuildServiceProvider());
        harness._fallbackSlotIds =
        [
            await harness.GivenSlotAsync(drugAndAlcohol: 100, medical: 100, uniform: 100),
            await harness.GivenSlotAsync(drugAndAlcohol: 100, medical: 100, uniform: 100),
        ];

        return harness;
    }

    public async Task<Guid> GivenSlotAsync(int drugAndAlcohol, int medical, int uniform)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(
                DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30 + Interlocked.Increment(ref _nextSlotOffset)),
                new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), drugAndAlcohol);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), medical);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), uniform);

        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);

        await using var context = _fixture.NewContext();
        context.SlotProposals.Add(proposal);
        context.ConfirmedSlots.Add(slot);
        await context.SaveChangesAsync();

        return slot.Id;
    }

    /// <summary>Opens an independent scope with its own connection for one racer.</summary>
    public IServiceScope CreateScope() => _services.CreateScope();

    public async Task<string> GivenInvitedCandidateAsync(Guid slotId, params Guid[] requiredTypeIds)
    {
        var tokens = _services.GetRequiredService<ITokenService>();

        var group = EmployeeGroup.Define(
            Guid.NewGuid(), $"HARNESS_{Guid.NewGuid():N}".ToUpperInvariant(), "Harness", true,
            requiredTypeIds);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Concurrent Candidate", $"{Guid.NewGuid():N}@mail.com", group);
        candidate.MarkInvited();

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);

        // Every invite is real: the fallback slots remain valid options if the contested one fills.
        var invite = Invite.CreateInitial(
            inviteId,
            candidate.Id,
            issued.TokenHash,
            DateTimeOffset.UtcNow.AddDays(4),
            [slotId, .. _fallbackSlotIds],
            requiredTypeIds,
            0);

        await using var context = _fixture.NewContext();
        context.EmployeeGroups.Add(group);
        context.Candidates.Add(candidate);
        context.Invites.Add(invite);
        await context.SaveChangesAsync();

        return issued.Token;
    }

    public async Task<Result<ConfirmBookingOutcome>> ConfirmAsync(string token, Guid slotId)
    {
        // A scope per attempt: separate context, separate connection, separate transaction.
        await using var scope = _services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ConfirmBookingHandler>();

        return await handler.HandleAsync(
            new ConfirmBookingCommand(token, slotId), CancellationToken.None);
    }

    /// <summary>
    /// Starts every confirmation only after a real external transaction has acquired the target
    /// slot's row lock. Every production handler consequently waits on PostgreSQL before the
    /// guard commits, proving that the work overlaps rather than being merely scheduled together.
    /// </summary>
    public async Task<ConfirmationBatch> ConfirmBatchAsync(
        IReadOnlyCollection<string> tokens,
        Guid slotId)
    {
        var tokenList = tokens.ToArray();
        if (tokenList.Length == 0)
        {
            throw new ArgumentException("At least one confirmation is required.", nameof(tokens));
        }

        await using var guardContext = _fixture.NewContext();
        await using var guardTransaction = await guardContext.Database.BeginTransactionAsync();
        var lockedSlot = await new ConfirmedSlotRepository(guardContext)
            .LockForUpdateAsync(slotId, CancellationToken.None);
        if (lockedSlot is null)
        {
            throw new InvalidOperationException("The batch target slot does not exist.");
        }

        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readiness = tokenList
            .Select(_ => new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();
        var attempts = tokenList
            .Select((token, index) => ConfirmAfterGateAsync(token, slotId, startGate.Task, readiness[index]))
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
    /// active slots with spare capacity for the candidate's required appointment types.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> LiveOptionSlotIdsAsync(string token)
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

        var optionIds = invite.OfferedSlotIds;
        if (optionIds.Count != Invite.RequiredOptionCount || optionIds.Distinct().Count() != optionIds.Count)
        {
            throw new InvalidOperationException("A live invite must retain three distinct options.");
        }

        var candidate = await context.Candidates
            .Include(c => c.Requirements)
            .SingleAsync(c => c.Id == invite.CandidateId);
        var slots = await context.ConfirmedSlots
            .Include(s => s.Capacities)
            .Where(s => optionIds.Contains(s.Id))
            .ToListAsync();

        if (slots.Count != optionIds.Count
            || slots.Any(slot => slot.Status != ConfirmedSlotStatus.Active)
            || slots.Any(slot => !slot.HasSpareCapacityForAll(candidate.RequiredAppointmentTypeIds)))
        {
            throw new InvalidOperationException("Every invite option must be a live eligible slot.");
        }

        return optionIds;
    }

    public async Task<int> RemainingCapacityAsync(Guid slotId, Guid appointmentTypeId)
    {
        await using var context = _fixture.NewContext();
        var slot = await context.ConfirmedSlots
            .Include(s => s.Capacities)
            .SingleAsync(s => s.Id == slotId);

        return slot.CapacityFor(appointmentTypeId).RemainingCapacity;
    }

    public async Task<int> ActiveBookingCountAsync(Guid slotId)
    {
        await using var context = _fixture.NewContext();
        return await context.Bookings.CountAsync(
            b => b.ConfirmedSlotId == slotId && b.Status == BookingStatus.Active);
    }

    private async Task<Result<ConfirmBookingOutcome>> ConfirmAfterGateAsync(
        string token,
        Guid slotId,
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
                new ConfirmBookingCommand(token, slotId), CancellationToken.None);
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

## tests/EventBooking.Infrastructure.Tests/ConfirmedSlotPersistenceTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/ConfirmedSlotPersistenceTests.cs","encoding":"utf8","sha256":"c359a08ddb48a90f859c43f191ad515450e37ee37f154c04743d6555e657a115","parts":1,"part":1} -->

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

## tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs","encoding":"utf8","sha256":"d0940eb028c2a2d7e1c383a2959153f38adbea6816b282813163aaac6ed0f8cc","parts":1,"part":1} -->

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

## tests/EventBooking.Infrastructure.Tests/DurableEmailDeliveryTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/DurableEmailDeliveryTests.cs","encoding":"utf8","sha256":"57fea72360599cfcfb502afbf885a5fa15b2fa6dcf69adc5d140d2e6011409b5","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies durable delivery rollback and PostgreSQL row-claim serialization.</summary>
[Collection("postgres")]
public class DurableEmailDeliveryTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

    /// <summary>A business commit failure rolls back the staged delivery and never calls transport.</summary>
    [Fact]
    public async Task ACommitFailureAfterStagingLeavesNoDurableDeliveryAndCallsNoTransport()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        var transport = new RecordingSender();
        var unitOfWork = new FailingCommitUnitOfWork(context);
        var service = new EmailDeliveryService(
            new EmailDeliveryRepository(context), transport, unitOfWork, new FixedClock(Now),
            NullLogger<EmailDeliveryService>.Instance);

        await using var transaction = await unitOfWork.BeginTransactionAsync(CancellationToken.None);
        var delivery = service.StagePending(Guid.NewGuid(), EmailTemplate.CandidateInvite);
        service.ClaimForDispatch(delivery);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transaction.CommitAsync(CancellationToken.None));
        await transaction.RollbackAsync(CancellationToken.None);

        await using var read = fixture.NewContext();
        Assert.Empty(await read.EmailLogs.ToListAsync());
        Assert.Empty(transport.Messages);
    }

    /// <summary>Only the worker holding the committed claim is allowed to call transport.</summary>
    [Fact]
    public async Task ConcurrentDispatchesSendOneMessageAndPersistOneSentOutcome()
    {
        await fixture.ResetAsync();
        var candidateId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();

        await using (var seed = fixture.NewContext())
        {
            seed.EmailLogs.Add(EmailLog.RecordPending(
                deliveryId,
                candidateId,
                EmailTemplate.CandidateInvite,
                Now));
            await seed.SaveChangesAsync();
        }

        await using var firstContext = fixture.NewContext();
        await using var secondContext = fixture.NewContext();
        var transport = new BlockingSender();
        var first = new EmailDeliveryService(
            new EmailDeliveryRepository(firstContext),
            transport,
            new UnitOfWork(firstContext),
            new FixedClock(Now),
            NullLogger<EmailDeliveryService>.Instance);
        var second = new EmailDeliveryService(
            new EmailDeliveryRepository(secondContext),
            transport,
            new UnitOfWork(secondContext),
            new FixedClock(Now),
            NullLogger<EmailDeliveryService>.Instance);

        var firstTask = first.DispatchAsync(
            deliveryId,
            Message(candidateId),
            CancellationToken.None);
        await transport.SendStarted;

        var secondTask = second.DispatchAsync(
            deliveryId,
            Message(candidateId),
            CancellationToken.None);

        Assert.Equal(EmailStatus.Pending, await secondTask);
        transport.Release();
        Assert.Equal(EmailStatus.Sent, await firstTask);

        await using var read = fixture.NewContext();
        var persisted = await read.EmailLogs.SingleAsync();
        Assert.Equal(EmailStatus.Sent, persisted.Status);
        Assert.Equal(1, transport.SendCount);
    }

    /// <summary>An unresolved attempt remains lockable until a retry resolves it.</summary>
    [Fact]
    public async Task RetryLockPrioritizesOutstandingDeliveryOverLaterTerminalHistory()
    {
        await fixture.ResetAsync();
        var candidateId = Guid.NewGuid();
        var outstanding = EmailLog.RecordPending(
            Guid.NewGuid(), candidateId, EmailTemplate.CandidateInvite, Now);
        outstanding.MarkFailed(Now);
        var laterSent = EmailLog.Record(
            Guid.NewGuid(),
            candidateId,
            EmailTemplate.SlotCancelledRebookingNeeded,
            Now.AddMinutes(3),
            EmailStatus.Sent);

        await using (var seed = fixture.NewContext())
        {
            seed.EmailLogs.AddRange(outstanding, laterSent);
            await seed.SaveChangesAsync();
        }

        await using (var retry = fixture.NewContext())
        await using (var transaction = await retry.Database.BeginTransactionAsync())
        {
            var selected = await new EmailDeliveryRepository(retry)
                .LockLatestForCandidateAsync(candidateId, CancellationToken.None);
            Assert.Equal(outstanding.Id, selected?.Id);
            selected!.MarkResolved(Now.AddMinutes(2));
            await retry.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await using var terminalRead = fixture.NewContext();
        await using var terminalTransaction = await terminalRead.Database.BeginTransactionAsync();
        var terminal = await new EmailDeliveryRepository(terminalRead)
            .LockLatestForCandidateAsync(candidateId, CancellationToken.None);
        Assert.Equal(laterSent.Id, terminal?.Id);
        await terminalTransaction.CommitAsync();
    }

    private static EmailMessage Message(Guid candidateId) =>
        new(
            candidateId,
            "candidate@example.com",
            "Candidate",
            EmailTemplate.CandidateInvite,
            "Choose a time",
            "body",
            "<p>body</p>");

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;

        /// <summary>Gets the fixed instant; this clock treats UTC as head-office time.</summary>
        public DateTimeOffset NowAtHeadOffice => now;

        public DateOnly TodayAtHeadOffice => DateOnly.FromDateTime(now.UtcDateTime);

        public DateOnly DateAtHeadOffice(DateTimeOffset instant) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) => instant.ToUniversalTime();
    }

    private sealed class RecordingSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];

        public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.FromResult(true);
        }
    }

    private sealed class BlockingSender : IEmailSender
    {
        private readonly TaskCompletionSource<bool> _sendStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _sendCount;

        public Task SendStarted => _sendStarted.Task;

        public int SendCount => Volatile.Read(ref _sendCount);

        public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _sendCount);
            _sendStarted.TrySetResult(true);
            return WaitForReleaseAsync(cancellationToken);
        }

        public void Release() => _release.TrySetResult(true);

        private async Task<bool> WaitForReleaseAsync(CancellationToken cancellationToken)
        {
            await _release.Task.WaitAsync(cancellationToken);
            return true;
        }
    }

    private sealed class FailingCommitUnitOfWork(EventBookingDbContext context) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            context.SaveChangesAsync(cancellationToken);

        public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken)
        {
            var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            return new Scope(transaction);
        }

        private sealed class Scope(IDbContextTransaction transaction) : ITransactionScope
        {
            public Task CommitAsync(CancellationToken cancellationToken) =>
                Task.FromException(new InvalidOperationException("simulated commit failure"));

            public Task RollbackAsync(CancellationToken cancellationToken) =>
                transaction.RollbackAsync(cancellationToken);

            public ValueTask DisposeAsync() => transaction.DisposeAsync();
        }
    }
}
`````

## tests/EventBooking.Infrastructure.Tests/EfAuditLoggerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/EfAuditLoggerTests.cs","encoding":"utf8","sha256":"a2a208b4f93d162bb240b866dfc02414108d3d9cc5ce71165c5b07eb9ae5c22a","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Audit;
using EventBooking.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class EfAuditLoggerTests(PostgresFixture fixture)
{
    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;

        /// <summary>Gets the fixed instant; this clock treats UTC as head-office time.</summary>
        public DateTimeOffset NowAtHeadOffice => now;

        public DateOnly TodayAtHeadOffice => DateAtHeadOffice(now);

        public DateOnly DateAtHeadOffice(DateTimeOffset instant) => DateOnly.FromDateTime(instant.UtcDateTime);

        public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) => instant.ToUniversalTime();
    }

    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AStagedEntryIsWrittenWhenTheUnitOfWorkSaves()
    {
        await fixture.ResetAsync();
        var entityId = Guid.NewGuid();

        await using (var context = fixture.NewContext())
        {
            new EfAuditLogger(context, new FixedClock(Now)).Record(
                AuditEntityTypes.Booking, entityId, AuditAction.BookingCreated,
                ActorType.CandidateToken, "invite-1", "chose option 2");

            await context.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var entry = await read.AuditLogs.SingleAsync();
        Assert.Equal(AuditEntityTypes.Booking, entry.EntityType);
        Assert.Equal(entityId, entry.EntityId);
        Assert.Equal(AuditAction.BookingCreated, entry.Action);
        Assert.Equal(ActorType.CandidateToken, entry.ActorType);
        Assert.Equal("invite-1", entry.ActorId);
        Assert.Equal(Now, entry.Timestamp);
        Assert.Equal("chose option 2", entry.Details);
    }

    [Fact]
    public async Task AStagedEntryIsLostWhenTheTransactionRollsBack()
    {
        await fixture.ResetAsync();

        await using (var context = fixture.NewContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            new EfAuditLogger(context, new FixedClock(Now)).Record(
                AuditEntityTypes.ConfirmedSlot, Guid.NewGuid(), AuditAction.SlotCancelled,
                ActorType.Staff, Guid.NewGuid().ToString());

            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using var read = fixture.NewContext();
        Assert.Equal(0, await read.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task ASystemActorNeedsNoIdentifier()
    {
        await fixture.ResetAsync();

        await using (var context = fixture.NewContext())
        {
            new EfAuditLogger(context, new FixedClock(Now)).Record(
                AuditEntityTypes.Invite, Guid.NewGuid(), AuditAction.InviteExpired,
                ActorType.System, null);

            await context.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        Assert.Null((await read.AuditLogs.SingleAsync()).ActorId);
    }
}
`````

## tests/EventBooking.Infrastructure.Tests/EmployeeGroupPersistenceTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/EmployeeGroupPersistenceTests.cs","encoding":"utf8","sha256":"7b2469ea66b29c65e9b9294e6a0342dda710d59c1e73beeebcfd76a4c8b4a47b","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies deterministic Employee Group persistence and nullable Candidate rollout.</summary>
[Collection("postgres")]
public sealed class EmployeeGroupPersistenceTests(PostgresFixture fixture)
{
    /// <summary>The database contains exactly the approved five groups and ten mappings.</summary>
    [Fact]
    public async Task SeededGroupsMatchTheApprovedReferenceData()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        var repository = new EmployeeGroupRepository(context);

        var groups = await repository.ListActiveAsync(CancellationToken.None);

        Assert.Equal(5, groups.Count);
        Assert.Equal(10, groups.Sum(group => group.Requirements.Count));
        var pilots = Assert.Single(groups, group => group.Code == "PILOTS");
        Assert.Equal(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            pilots.RequiredAppointmentTypeIds);
        var lowerCaseLookup = await repository.GetByCodeAsync(" pilots ", CancellationToken.None);
        Assert.Equal(EmployeeGroupIds.Pilots, lowerCaseLookup!.Id);
    }

    /// <summary>Release 2 requires the Employee Group on every Candidate row.</summary>
    [Fact]
    public async Task CandidateAssociationIsRequiredAfterReleaseTwo()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        var nullable = context.Model.FindEntityType("EventBooking.Domain.Candidates.Candidate")!
            .FindProperty("EmployeeGroupId")!.IsNullable;

        Assert.False(nullable);
        var relationship = context.Model.FindEntityType("EventBooking.Domain.Candidates.Candidate")!
            .GetForeignKeys()
            .Single(key => key.Properties.Single().Name == "EmployeeGroupId");
        Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior);
    }
}
`````

## tests/EventBooking.Infrastructure.Tests/EmployeeGroupRequiredMigrationTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/EmployeeGroupRequiredMigrationTests.cs","encoding":"utf8","sha256":"dc7b6d2ea600737da23b2a42b7ef777b245d78a4bdf8520387b2d892c0934db1","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies Release 2 refuses incomplete reconciliation before changing nullability.</summary>
[Collection("postgres")]
public sealed class EmployeeGroupRequiredMigrationTests(PostgresFixture fixture)
{
    private const string ReleaseOneMigration = "20260909120000_AddRecoveryBookings";

    /// <summary>An unassigned row blocks migration; explicit matching assignment then succeeds.</summary>
    [Fact]
    public async Task GuardBlocksUnassignedCandidateBeforeAlterColumn()
    {
        var databaseName = $"eventbooking_group_required_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        }.ConnectionString;
        try
        {
            await ExecuteAdminAsync(fixture.ConnectionString, $"CREATE DATABASE \"{databaseName}\"");
            await using (var releaseOne = NewContext(connectionString))
            {
                await releaseOne.Database.MigrateAsync(ReleaseOneMigration);
            }
            var candidateId = Guid.NewGuid();
            await ExecuteAsync(connectionString,
                """
                INSERT INTO candidate (id, name, email, status, status_changed_at, employee_group_id)
                VALUES (@id, 'Legacy', 'legacy@example.com', @status, now(), NULL);
                """,
                ("id", candidateId), ("status", (int)CandidateStatus.NotYetInvited));

            await using (var blocked = NewContext(connectionString))
            {
                var error = await Assert.ThrowsAnyAsync<Exception>(() => blocked.Database.MigrateAsync());
                Assert.Contains("Employee Group reconciliation", error.ToString());
            }
            Assert.True(await IsNullableAsync(connectionString));

            await ExecuteAsync(connectionString,
                """
                UPDATE candidate SET employee_group_id = @group_id WHERE id = @id;
                INSERT INTO candidate_requirement (candidate_id, appointment_type_id)
                SELECT @id, appointment_type_id
                FROM employee_group_requirement
                WHERE employee_group_id = @group_id;
                """,
                ("id", candidateId), ("group_id", EmployeeGroupIds.Pilots));
            await using (var reconciled = NewContext(connectionString))
            {
                await reconciled.Database.MigrateAsync();
            }

            Assert.False(await IsNullableAsync(connectionString));
        }
        finally
        {
            await ExecuteAdminAsync(
                fixture.ConnectionString,
                $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        }
    }

    private static EventBookingDbContext NewContext(string connectionString) => new(
        new DbContextOptionsBuilder<EventBookingDbContext>().UseNpgsql(connectionString).Options);

    private static async Task ExecuteAdminAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteAsync(
        string connectionString,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> IsNullableAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT is_nullable = 'YES'
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'candidate'
              AND column_name = 'employee_group_id';
            """, connection);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
`````

## tests/EventBooking.Infrastructure.Tests/EventBooking.Infrastructure.Tests.csproj — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/EventBooking.Infrastructure.Tests.csproj","encoding":"utf8","sha256":"874f5e1bc2440ce548604c595b31ef7d35dcae31498448c8f9832a01f256ac60","parts":1,"part":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <PackageReference Include="Testcontainers.PostgreSql" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\EventBooking.Infrastructure\EventBooking.Infrastructure.csproj" />
    <ProjectReference Include="..\..\src\EventBooking.Domain\EventBooking.Domain.csproj" />
    <ProjectReference Include="..\..\src\EventBooking.Application\EventBooking.Application.csproj" />
  </ItemGroup>

</Project>
`````
