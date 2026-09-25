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
        public Task<EmailSendOutcome> SendAsync(
            string recipient, string subject, string textBody, string htmlBody,
            CancellationToken cancellationToken) =>
            Task.FromResult(EmailSendOutcome.Sent);
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

        // Every attempt in a batch holds its own open connection at once, so the pool must admit
        // the largest batch rather than the 20-per-replica production default.
        services.AddEventBookingInfrastructure(
            new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { MaxPoolSize = 100 }.ConnectionString,
            new TokenOptions("a-concurrency-test-signing-key-long-enough"));

        services.AddSingleton<EventBooking.Domain.Time.IEventWindowZones>(

            new EventBooking.Infrastructure.Time.NodaTimeEventWindowZones());

        services.AddEventBookingApplication(
            new AttendeePortalOptions("https://booking.example.com", "recruitment@example.com"));

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
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(
                DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30 + Interlocked.Increment(ref _nextEventOffset)),
                new TimeOnly(9, 0), 240),
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
            Guid.NewGuid(),
            "Concurrent Attendee",
            $"{Guid.NewGuid():N}@mail.com",
            group,
            ProposalFixture.Now);
        attendee.MarkInvited(ProposalFixture.Now);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(TokenPurpose.Book, inviteId, Invite.InitialTokenVersion);

        // Every invite is real: the fallback events remain valid options if the contested one fills.
        var invite = Invite.CreateInitial(
            inviteId,
            attendee.Id,
            DateTimeOffset.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            [eventId, .. _fallbackEventIds],
            requiredTypeIds,
            0);

        await using var context = _fixture.NewContext();
        context.AttendeeGroups.Add(group);
        context.Attendees.Add(attendee);
        context.Invites.Add(invite);
        await context.SaveChangesAsync();

        return issued;
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
