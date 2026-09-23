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
