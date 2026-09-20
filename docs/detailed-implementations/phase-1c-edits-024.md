# 01c — Negotiation across any number of types, edits 24 (Task 6)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs — 1/1

<!-- retirement-file: {"id":74,"file":"tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs","beforeSha":"70be5aeda75afbf2e137014f7260beefaff1ff3269be4e7dfeb20ea73f81257a","afterSha":"2ba421efe230d4f65d2d64ed6a231ab7f59ba9b95ee919f96d410bc51aa9ae86","side":"after","part":1,"parts":1} -->

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

## before — tests/EventBooking.Mcp.Tests/EventBooking.Mcp.Tests.csproj — 1/1

<!-- retirement-file: {"id":75,"file":"tests/EventBooking.Mcp.Tests/EventBooking.Mcp.Tests.csproj","beforeSha":"bcb17386832a1efbab3c4843d2b42ae9b697dcc0d0c55f8aa09afc3a86236862","afterSha":"1a4e3b80ccd23d0659b7809438d95dab94df9b129291b2e4557289208e6c6985","side":"before","part":1,"parts":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="Microsoft.Extensions.Configuration" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Testcontainers.PostgreSql" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\EventBooking.Mcp\EventBooking.Mcp.csproj" />
    <ProjectReference Include="..\..\src\EventBooking.Domain\EventBooking.Domain.csproj" />
    <ProjectReference Include="..\..\src\EventBooking.Application\EventBooking.Application.csproj" />
    <ProjectReference Include="..\..\src\EventBooking.Infrastructure\EventBooking.Infrastructure.csproj" />
  </ItemGroup>

</Project>
`````

## after — tests/EventBooking.Mcp.Tests/EventBooking.Mcp.Tests.csproj — 1/1

<!-- retirement-file: {"id":75,"file":"tests/EventBooking.Mcp.Tests/EventBooking.Mcp.Tests.csproj","beforeSha":"bcb17386832a1efbab3c4843d2b42ae9b697dcc0d0c55f8aa09afc3a86236862","afterSha":"1a4e3b80ccd23d0659b7809438d95dab94df9b129291b2e4557289208e6c6985","side":"after","part":1,"parts":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="Microsoft.Extensions.Configuration" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Testcontainers.PostgreSql" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\EventBooking.Mcp\EventBooking.Mcp.csproj" />
    <ProjectReference Include="..\..\src\EventBooking.Domain\EventBooking.Domain.csproj" />
    <ProjectReference Include="..\..\src\EventBooking.Application\EventBooking.Application.csproj" />
    <ProjectReference Include="..\..\src\EventBooking.Infrastructure\EventBooking.Infrastructure.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- One proposal builder shared by every suite: proposals now need a location, a listed
         type set and a proposing type, and no suite should reinvent that shape. -->
    <Compile Include="..\TestSupport\ProposalFixture.cs" Link="TestSupport\ProposalFixture.cs" />
    <Using Include="EventBooking.TestSupport" />
  </ItemGroup>

</Project>
`````

## before — tests/EventBooking.Mcp.Tests/Fixtures/EventFixture.cs — 1/1

<!-- retirement-file: {"id":76,"file":"tests/EventBooking.Mcp.Tests/Fixtures/EventFixture.cs","beforeSha":"0c13090f3c6ad44a0345bbc0bdf398e7e8c71acbc46ef53a8d49dba1a3cfcb05","afterSha":"634f2ccbe40f83af3e57516aaa4ed776ca00e78b7bad7d56c2d9e9b169071d39","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

internal static class EventFixture
{
    public static Event Create(Guid id, EventWindow window, IReadOnlyDictionary<Guid, int> headcounts)
    {
        var manager = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var proposal = EventProposal.Create(Guid.NewGuid(), window, manager);
        foreach (var type in AppointmentTypeIds.All)
            proposal.Accept(type, manager, headcounts[type]);
        return Event.CreateFrom(id, proposal);
    }
}
`````

## after — tests/EventBooking.Mcp.Tests/Fixtures/EventFixture.cs — 1/1

<!-- retirement-file: {"id":76,"file":"tests/EventBooking.Mcp.Tests/Fixtures/EventFixture.cs","beforeSha":"0c13090f3c6ad44a0345bbc0bdf398e7e8c71acbc46ef53a8d49dba1a3cfcb05","afterSha":"634f2ccbe40f83af3e57516aaa4ed776ca00e78b7bad7d56c2d9e9b169071d39","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

internal static class EventFixture
{
    public static Event Create(Guid id, EventWindow window, IReadOnlyDictionary<Guid, int> headcounts)
    {
        var manager = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var proposal = ProposalFixture.Create(Guid.NewGuid(), window, manager);
        foreach (var type in AppointmentTypeIds.All)
            proposal.Accept(type, manager, headcounts[type]);
        return Event.CreateFrom(id, proposal);
    }
}
`````

## before — tests/EventBooking.SeedData.Tests/DemoInvitationHostTests.cs — 1/1

<!-- retirement-file: {"id":77,"file":"tests/EventBooking.SeedData.Tests/DemoInvitationHostTests.cs","beforeSha":"838e782b8bd191f8cf27228045ce252797e3a576c32a7aa8c56b3f849d707848","afterSha":"2ed2756c65324b6c26faa5ead89a85b80b7036224fabe0fa73fea0793ddda941","side":"before","part":1,"parts":1} -->

`````csharp
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using EventBooking.Application;
using EventBooking.Application.Bookings;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EventBooking.SeedData.Tests;

/// <summary>Tests real seed-process configuration, exit codes and MIME delivery.</summary>
public sealed class DemoInvitationHostTests : IAsyncLifetime
{
    private const string SigningKey = "host-test-signing-key-at-least-32-characters";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly LoopbackSmtpReceiver _smtp = new();

    /// <inheritdoc/>
    public Task InitializeAsync() => _postgres.StartAsync();
    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await _smtp.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>The console sends clickable HTML/text emails and a second invocation sends none.</summary>
    [Fact]
    public async Task ConsoleSeedSendsUsableMailAndRerunDoesNotDuplicate()
    {
        var first = await RunAsync(false);
        Assert.True(first.ExitCode == 0, first.Output);
        Assert.Equal(5, _smtp.Messages.Count);
        using var services = VerificationServices();
        using var scope = services.CreateScope();
        var view = scope.ServiceProvider.GetRequiredService<ViewInviteHandler>();
        foreach (var message in _smtp.Messages)
        {
            Assert.NotNull(message.TextBody);
            Assert.NotNull(message.HtmlBody);
            var token = Regex.Match(message.TextBody,
                @"https://host-demo\.example\.test/book/([^\s]+)").Groups[1].Value;
            Assert.NotEmpty(token);
            Assert.Contains($"href=\"https://host-demo.example.test/book/{token}\"", message.HtmlBody);
            var result = await view.HandleAsync(new ViewInviteQuery(token), default);
            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value.Options.Count);
            Assert.DoesNotContain(token, first.Output);
        }
        Assert.DoesNotContain(SigningKey, first.Output);
        var second = await RunAsync(false);
        Assert.True(second.ExitCode == 0, second.Output);
        Assert.Equal(5, _smtp.Messages.Count);
    }

    /// <summary>Migration-only bypasses even invalid email settings and creates no demo Attendees.</summary>
    [Fact]
    public async Task MigrationOnlyDoesNotReadEmailConfiguration()
    {
        var result = await RunAsync(true, new Dictionary<string, string?>
        {
            ["Tokens__SigningKey"] = "invalid",
            ["Portal__BaseUrl"] = "invalid",
            ["Email__Smtp__Port"] = "invalid",
        });
        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Empty(_smtp.Messages);
        using var services = VerificationServices();
        using var scope = services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.Equal(0, await database.Attendees.CountAsync());
        Assert.NotEmpty(await database.Database.GetAppliedMigrationsAsync());
    }

    /// <summary>SMTP rejection yields a failed durable delivery and a nonzero process exit.</summary>
    [Fact]
    public async Task ConsoleFailureCanResumeAfterSmtpRecovery()
    {
        _smtp.RejectMessages = true;
        var failed = await RunAsync(false);
        Assert.Equal(2, failed.ExitCode);
        Assert.Contains("delivery failed", failed.Output);
        Assert.Empty(_smtp.Messages);
        using (var services = VerificationServices())
        using (var scope = services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            Assert.Equal(1, await database.EmailLogs.CountAsync(e => e.Status == EmailStatus.Failed));
        }
        _smtp.RejectMessages = false;
        var resumed = await RunAsync(false);
        Assert.True(resumed.ExitCode == 0, resumed.Output);
        Assert.Equal(5, _smtp.Messages.Count);
    }

    private ServiceProvider VerificationServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBookingInfrastructure(_postgres.GetConnectionString(),
            new ClockOptions("Europe/London"), new TokenOptions(SigningKey));
        services.AddEventBookingApplication(new AttendeePortalOptions(
            "https://host-demo.example.test", "help@example.com"));
        return services.BuildServiceProvider();
    }

    private async Task<(int ExitCode, string Output)> RunAsync(
        bool skipSeed, Dictionary<string, string?>? overrides = null)
    {
        var root = RepoRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "run", "--no-build", "--configuration", configuration,
            "--project", "src/EventBooking.SeedData", "--", _postgres.GetConnectionString() })
            start.ArgumentList.Add(argument);
        // Never inherit developer Keycloak credentials or an external SMTP destination.
        foreach (var key in start.Environment.Keys.Where(key =>
            key.StartsWith("Keycloak__", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("Email__", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("Tokens__", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("Portal__", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("Clock__", StringComparison.OrdinalIgnoreCase)).ToArray())
            start.Environment.Remove(key);
        start.Environment["Tokens__SigningKey"] = SigningKey;
        start.Environment["Portal__BaseUrl"] = "https://host-demo.example.test/";
        start.Environment["Email__Smtp__Host"] = "127.0.0.1";
        start.Environment["Email__Smtp__Port"] = _smtp.Port.ToString(CultureInfo.InvariantCulture);
        if (skipSeed) start.ArgumentList.Add("--skip-seed");
        else
        {
            start.ArgumentList.Add("--reanchor");
            start.ArgumentList.Add(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")).DateTime)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        if (overrides is not null)
            foreach (var (key, value) in overrides) start.Environment[key] = value;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Seed did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException("Seed process exceeded 90 seconds.");
        }
        return (process.ExitCode, await stdout + await stderr);
    }

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EventBooking.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Cannot locate repository root.");
    }
}
`````

## after — tests/EventBooking.SeedData.Tests/DemoInvitationHostTests.cs — 1/1

<!-- retirement-file: {"id":77,"file":"tests/EventBooking.SeedData.Tests/DemoInvitationHostTests.cs","beforeSha":"838e782b8bd191f8cf27228045ce252797e3a576c32a7aa8c56b3f849d707848","afterSha":"2ed2756c65324b6c26faa5ead89a85b80b7036224fabe0fa73fea0793ddda941","side":"after","part":1,"parts":1} -->

`````csharp
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using EventBooking.Application;
using EventBooking.Application.Bookings;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EventBooking.SeedData.Tests;

/// <summary>Tests real seed-process configuration, exit codes and MIME delivery.</summary>
public sealed class DemoInvitationHostTests : IAsyncLifetime
{
    private const string SigningKey = "host-test-signing-key-at-least-32-characters";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly LoopbackSmtpReceiver _smtp = new();

    /// <inheritdoc/>
    public Task InitializeAsync() => _postgres.StartAsync();
    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await _smtp.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>The console sends clickable HTML/text emails and a second invocation sends none.</summary>
    [Fact]
    public async Task ConsoleSeedSendsUsableMailAndRerunDoesNotDuplicate()
    {
        var first = await RunAsync(false);
        Assert.True(first.ExitCode == 0, first.Output);
        Assert.Equal(5, _smtp.Messages.Count);
        using var services = VerificationServices();
        using var scope = services.CreateScope();
        var view = scope.ServiceProvider.GetRequiredService<ViewInviteHandler>();
        foreach (var message in _smtp.Messages)
        {
            Assert.NotNull(message.TextBody);
            Assert.NotNull(message.HtmlBody);
            var token = Regex.Match(message.TextBody,
                @"https://host-demo\.example\.test/book/([^\s]+)").Groups[1].Value;
            Assert.NotEmpty(token);
            Assert.Contains($"href=\"https://host-demo.example.test/book/{token}\"", message.HtmlBody);
            var result = await view.HandleAsync(new ViewInviteQuery(token), default);
            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value.Options.Count);
            Assert.DoesNotContain(token, first.Output);
        }
        Assert.DoesNotContain(SigningKey, first.Output);
        var second = await RunAsync(false);
        Assert.True(second.ExitCode == 0, second.Output);
        Assert.Equal(5, _smtp.Messages.Count);
    }

    /// <summary>Migration-only bypasses even invalid email settings and creates no demo Attendees.</summary>
    [Fact]
    public async Task MigrationOnlyDoesNotReadEmailConfiguration()
    {
        var result = await RunAsync(true, new Dictionary<string, string?>
        {
            ["Tokens__SigningKey"] = "invalid",
            ["Portal__BaseUrl"] = "invalid",
            ["Email__Smtp__Port"] = "invalid",
        });
        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Empty(_smtp.Messages);
        using var services = VerificationServices();
        using var scope = services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.Equal(0, await database.Attendees.CountAsync());
        Assert.NotEmpty(await database.Database.GetAppliedMigrationsAsync());
    }

    /// <summary>SMTP rejection yields a failed durable delivery and a nonzero process exit.</summary>
    [Fact]
    public async Task ConsoleFailureCanResumeAfterSmtpRecovery()
    {
        _smtp.RejectMessages = true;
        var failed = await RunAsync(false);
        Assert.Equal(2, failed.ExitCode);
        Assert.Contains("delivery failed", failed.Output);
        Assert.Empty(_smtp.Messages);
        using (var services = VerificationServices())
        using (var scope = services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            Assert.Equal(1, await database.EmailLogs.CountAsync(e => e.Status == EmailStatus.Failed));
        }
        _smtp.RejectMessages = false;
        var resumed = await RunAsync(false);
        Assert.True(resumed.ExitCode == 0, resumed.Output);
        Assert.Equal(5, _smtp.Messages.Count);
    }

    private ServiceProvider VerificationServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBookingInfrastructure(_postgres.GetConnectionString(),
            new ClockOptions("Europe/London"), new TokenOptions(SigningKey));
        services.AddSingleton<EventBooking.Domain.Time.IEventWindowZones>(
            new EventBooking.Infrastructure.Time.NodaTimeEventWindowZones());
        services.AddEventBookingApplication(new AttendeePortalOptions(
            "https://host-demo.example.test", "help@example.com"));
        return services.BuildServiceProvider();
    }

    private async Task<(int ExitCode, string Output)> RunAsync(
        bool skipSeed, Dictionary<string, string?>? overrides = null)
    {
        var root = RepoRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "run", "--no-build", "--configuration", configuration,
            "--project", "src/EventBooking.SeedData", "--", _postgres.GetConnectionString() })
            start.ArgumentList.Add(argument);
        // Never inherit developer Keycloak credentials or an external SMTP destination.
        foreach (var key in start.Environment.Keys.Where(key =>
            key.StartsWith("Keycloak__", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("Email__", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("Tokens__", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("Portal__", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("Clock__", StringComparison.OrdinalIgnoreCase)).ToArray())
            start.Environment.Remove(key);
        start.Environment["Tokens__SigningKey"] = SigningKey;
        start.Environment["Portal__BaseUrl"] = "https://host-demo.example.test/";
        start.Environment["Email__Smtp__Host"] = "127.0.0.1";
        start.Environment["Email__Smtp__Port"] = _smtp.Port.ToString(CultureInfo.InvariantCulture);
        if (skipSeed) start.ArgumentList.Add("--skip-seed");
        else
        {
            start.ArgumentList.Add("--reanchor");
            start.ArgumentList.Add(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")).DateTime)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        if (overrides is not null)
            foreach (var (key, value) in overrides) start.Environment[key] = value;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Seed did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException("Seed process exceeded 90 seconds.");
        }
        return (process.ExitCode, await stdout + await stderr);
    }

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EventBooking.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Cannot locate repository root.");
    }
}
`````

## before — tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs — 1/1

<!-- retirement-file: {"id":78,"file":"tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs","beforeSha":"26caf751bb59f2f8c7b4c39ef845f9d0e849e39780fb48604d09c8e321d38fa4","afterSha":"f011af6dea3891ea2543466b7686a2a0fb7bf9a2ef838987d7c9bd5566989df6","side":"before","part":1,"parts":1} -->

`````csharp
using System.Text.RegularExpressions;
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Events;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;
using EventBooking.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EventBooking.SeedData.Tests;

/// <summary>Exercises real seeded links, delivery recovery and attendee lifecycle preservation.</summary>
[Collection("seed-anchor")]
public sealed class DemoInvitationSeederTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly CapturingTransport _mail = new();
    private readonly DemoClock _clock = new();
    private ServiceProvider _services = null!;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        DemoSeedSpec.OverrideAnchor(_clock.TodayAtTransitionalLocation);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBookingInfrastructure(_postgres.GetConnectionString(),
            new ClockOptions("Europe/London"),
            new TokenOptions("test-seed-and-api-share-this-signing-key"));
        services.AddEventBookingApplication(new AttendeePortalOptions(
            "https://demo.example.test", "help@example.com"));
        services.AddSingleton<IClock>(_clock);
        services.AddSingleton<IEmailTransport>(_mail);
        services.AddScoped<DemoSeeder>();
        services.AddScoped<DemoInvitationSeeder>();
        _services = services.BuildServiceProvider();
        using var scope = _services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<EventBookingDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().RunAsync(default);
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        DemoSeedSpec.OverrideAnchor(null);
        if (_services is not null) await _services.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>All five delivered HTML/text links open future options; one can book and be managed.</summary>
    [Fact]
    public async Task FreshSeedProducesFiveUsableLinksAndARealBooking()
    {
        Assert.Equal(5, await SeedAsync());
        Assert.Equal(5, _mail.Messages.Count);
        Assert.Equal(Enumerable.Range(1, 5).Select(i => $"demo-attendee-{i:000}@example.com"),
            _mail.Messages.Select(m => m.ToAddress));
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.Equal(100, await db.Attendees.CountAsync());
        Assert.Equal(5, await db.Attendees.CountAsync(c => c.Status == AttendeeStatus.Invited));
        // Journey seeds write booking rows directly without transitioning Attendee
        // status, so only the 5 newly invited attendees leave NotYetInvited.
        Assert.Equal(95, await db.Attendees.CountAsync(c => c.Status == AttendeeStatus.NotYetInvited));
        Assert.Equal(5, await db.Invites.CountAsync(i => i.Status == InviteStatus.Pending));
        Assert.Equal(5, await db.EmailLogs.CountAsync(e => e.Status == EmailStatus.Sent));
        Assert.Equal(12, await db.Events.CountAsync());
        // Five negotiation scenarios plus one accepted proposal for each of twelve events.
        Assert.Equal(17, await db.EventProposals.CountAsync());
        var events = await db.Events.AsNoTracking().ToListAsync();
        var proposals = await db.EventProposals.AsNoTracking().ToDictionaryAsync(p => p.Id);
        Assert.Equal(events.Count, events.Select(e => e.ProposalId).Distinct().Count());
        Assert.All(events, e => Assert.Equal(EventProposalStatus.Confirmed, proposals[e.ProposalId].Status));
        Assert.Equal(5, await db.Attendees.Where(c => c.Status == AttendeeStatus.Invited)
            .Select(c => c.AttendeeGroupId).Distinct().CountAsync());
        var view = scope.ServiceProvider.GetRequiredService<ViewInviteHandler>();
        foreach (var message in _mail.Messages.ToArray())
        {
            var token = Token(message);
            Assert.Contains($"href=\"https://demo.example.test/book/{token}\"", message.HtmlBody);
            var result = await view.HandleAsync(new ViewInviteQuery(token), default);
            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value.Options.Count);
            Assert.All(result.Value.Options, option => Assert.True(option.Date > _clock.TodayAtTransitionalLocation));
            var attendee = await db.Attendees.Include(c => c.Requirements)
                .SingleAsync(c => c.Id == message.AttendeeId);
            var invite = await db.Invites.Include(i => i.Requirements)
                .SingleAsync(i => i.Id == result.Value.InviteId);
            Assert.Equal(attendee.RequiredAppointmentTypeIds.Order(), invite.RequiredAppointmentTypeIds.Order());
            Assert.NotEqual(token, invite.TokenHash);
        }
        var first = Token(_mail.Messages[0]);
        var offered = await view.HandleAsync(new ViewInviteQuery(first), default);
        var booking = await scope.ServiceProvider.GetRequiredService<ConfirmBookingHandler>()
            .HandleAsync(new ConfirmBookingCommand(first, offered.Value.Options[0].EventId), default);
        Assert.True(booking.IsSuccess);
        Assert.Equal("Sent", booking.Value.DeliveryStatus);
        Assert.True((await scope.ServiceProvider.GetRequiredService<ViewBookingHandler>()
            .HandleAsync(new ViewBookingQuery(booking.Value.ManageToken), default)).IsSuccess);
        Assert.False((await view.HandleAsync(new ViewInviteQuery(first), default)).IsSuccess);
        Assert.Equal(EmailTemplate.BookingConfirmation, _mail.Messages[^1].Template);
    }

    /// <summary>A rerun preserves successful tokens, capacities and an already-consumed invitation.</summary>
    [Fact]
    public async Task RerunDoesNotResendOrUndoBooking()
    {
        await SeedAsync();
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var first = Token(_mail.Messages[0]);
        var offered = await scope.ServiceProvider.GetRequiredService<ViewInviteHandler>()
            .HandleAsync(new ViewInviteQuery(first), default);
        var booked = await scope.ServiceProvider.GetRequiredService<ConfirmBookingHandler>()
            .HandleAsync(new ConfirmBookingCommand(first, offered.Value.Options[0].EventId), default);
        Assert.True(booked.IsSuccess);
        var hashes = await db.Invites.AsNoTracking().OrderBy(i => i.Id).Select(i => i.TokenHash).ToListAsync();
        var capacities = await db.Events.AsNoTracking().Include(s => s.Capacities)
            .OrderBy(s => s.Id).ToListAsync();
        var before = capacities.SelectMany(s => s.Capacities.OrderBy(c => c.AppointmentTypeId))
            .Select(c => c.RemainingCapacity).ToArray();
        Assert.Equal(0, await SeedAsync());
        Assert.Equal(6, _mail.Messages.Count);
        Assert.Equal(hashes, await db.Invites.AsNoTracking().OrderBy(i => i.Id).Select(i => i.TokenHash).ToListAsync());
        var after = await db.Events.AsNoTracking().Include(s => s.Capacities)
            .OrderBy(s => s.Id).ToListAsync();
        Assert.Equal(before, after.SelectMany(s => s.Capacities.OrderBy(c => c.AppointmentTypeId))
            .Select(c => c.RemainingCapacity).ToArray());
    }

    /// <summary>Provider failure leaves a durable attempt that the next run retries once.</summary>
    [Fact]
    public async Task FailedDeliveryResumesWithoutDuplicatingSuccesses()
    {
        _mail.FailOnAttempt = 3;
        await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var failed = await db.EmailLogs.AsNoTracking().SingleAsync(e => e.Status == EmailStatus.Failed);
        var oldHash = await db.Invites.Where(i => i.Id == failed.InviteId).Select(i => i.TokenHash).SingleAsync();
        Assert.Equal(2, _mail.Messages.Count);
        _mail.FailOnAttempt = null;
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(3, await SeedAsync());
        Assert.Equal(5, _mail.Messages.Count);
        Assert.Equal(5, _mail.Messages.Select(m => m.AttendeeId).Distinct().Count());
        Assert.Equal(5, await db.Invites.CountAsync(i => i.Status == InviteStatus.Pending));
        Assert.Equal(1, await db.EmailLogs.CountAsync(e => e.Status == EmailStatus.Resolved));
        Assert.Equal(5, await db.EmailLogs.CountAsync(e => e.Status == EmailStatus.Sent));
        Assert.NotEqual(oldHash, await db.Invites.Where(i => i.Id == failed.InviteId)
            .Select(i => i.TokenHash).SingleAsync());
        Assert.Equal(0, await SeedAsync());
    }

    /// <summary>Resetting database state creates new invitations and invalidates old raw links.</summary>
    [Fact]
    public async Task ReseedCreatesFreshLinks()
    {
        await SeedAsync();
        var oldToken = Token(_mail.Messages[0]);
        using (var scope = _services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<DemoSeeder>().ReseedAsync(default);
        Assert.Equal(5, await SeedAsync());
        using var verify = _services.CreateScope();
        var view = verify.ServiceProvider.GetRequiredService<ViewInviteHandler>();
        Assert.False((await view.HandleAsync(new ViewInviteQuery(oldToken), default)).IsSuccess);
        Assert.True((await view.HandleAsync(new ViewInviteQuery(Token(_mail.Messages[5])), default)).IsSuccess);
    }

    /// <summary>Expired history is not replaced even when demo dates are moved forward.</summary>
    [Fact]
    public async Task ExpiredInvitationsArePreserved()
    {
        await SeedAsync();
        _clock.Advance(TimeSpan.FromDays(30));
        DemoSeedSpec.OverrideAnchor(_clock.TodayAtTransitionalLocation);
        Assert.Equal(0, await SeedAsync());
        Assert.Equal(5, _mail.Messages.Count);
        using var scope = _services.CreateScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<ViewInviteHandler>()
            .HandleAsync(new ViewInviteQuery(Token(_mail.Messages[0])), default)).IsSuccess);
    }

    /// <summary>Stale windows fail before issuing a misleading invitation.</summary>
    [Fact]
    public async Task StaleAnchorFailsBeforeSending()
    {
        _clock.Advance(TimeSpan.FromDays(4));
        var error = await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        Assert.Contains("--reanchor", error.Message);
        Assert.Empty(_mail.Messages);
    }

    /// <summary>A live claim is not stolen, but an expired claim can be recovered on a later run.</summary>
    [Fact]
    public async Task PendingClaimUsesExistingLease()
    {
        await SeedAsync();
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var prior = await db.EmailLogs.SingleAsync(e => e.AttendeeId == _mail.Messages[0].AttendeeId);
            var pending = EmailLog.RecordPending(Guid.NewGuid(), prior.AttendeeId,
                EmailTemplate.AttendeeInvite, _clock.UtcNow.AddSeconds(1), prior.InviteId);
            Assert.True(pending.TryClaim(_clock.UtcNow, TimeSpan.FromMinutes(5)));
            db.EmailLogs.Add(pending);
            await db.SaveChangesAsync();
        }
        var error = await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        Assert.Contains("already being delivered", error.Message);
        Assert.Equal(5, _mail.Messages.Count);
        _clock.Advance(TimeSpan.FromMinutes(6));
        Assert.Equal(1, await SeedAsync());
        Assert.Equal(6, _mail.Messages.Count);
    }

    /// <summary>A pending invitation lacking its delivery record is not reported as sent.</summary>
    [Fact]
    public async Task MissingDeliveryFailsVisibly()
    {
        await SeedAsync();
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            await db.EmailLogs.Where(e => e.AttendeeId == _mail.Messages[0].AttendeeId).ExecuteDeleteAsync();
        }
        var error = await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        Assert.Contains("no matching delivery", error.Message);
        Assert.Equal(5, _mail.Messages.Count);
    }

    /// <summary>An old failure cannot hide a successful Coordinator replacement for the same Attendee.</summary>
    [Fact]
    public async Task CoordinatorReplacementPreservesItsSuccessfulDelivery()
    {
        _mail.FailOnAttempt = 1;
        await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        _mail.FailOnAttempt = null;
        _clock.Advance(TimeSpan.FromSeconds(1));
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var attendee = await db.Attendees.SingleAsync(c => c.Email == "demo-attendee-001@example.com");
            var issued = await scope.ServiceProvider
                .GetRequiredService<EventBooking.Application.Invites.TriggerInviteHandler>()
                .HandleAsync(new EventBooking.Application.Invites.TriggerInviteCommand(
                    DemoSeedSpec.CoordinatorUserId(), attendee.Id), default);
            Assert.True(issued.IsSuccess);
            Assert.True(issued.Value.EmailSent);
        }
        var replacementToken = Token(Assert.Single(_mail.Messages));
        Assert.Equal(4, await SeedAsync());
        Assert.Equal(5, _mail.Messages.Count);
        using var verify = _services.CreateScope();
        Assert.True((await verify.ServiceProvider.GetRequiredService<ViewInviteHandler>()
            .HandleAsync(new ViewInviteQuery(replacementToken), default)).IsSuccess);
        Assert.Equal(0, await SeedAsync());
    }

    /// <summary>Unrelated outstanding work is not retried or token-rotated by demo seeding.</summary>
    [Fact]
    public async Task AnotherOutstandingTemplateRequiresCoordinatorReview()
    {
        _mail.FailOnAttempt = 1;
        await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        _mail.FailOnAttempt = null;
        string originalHash;
        Guid inviteId;
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var prior = await db.EmailLogs.SingleAsync(e => e.Status == EmailStatus.Failed);
            inviteId = prior.InviteId!.Value;
            originalHash = await db.Invites.Where(i => i.Id == inviteId).Select(i => i.TokenHash).SingleAsync();
            var other = EmailLog.RecordPending(Guid.NewGuid(), prior.AttendeeId,
                EmailTemplate.AttendeeReinvite, _clock.UtcNow.AddSeconds(1), inviteId);
            db.EmailLogs.Add(other);
            await db.SaveChangesAsync();
        }
        var error = await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        Assert.Contains("Another outstanding delivery", error.Message);
        Assert.Empty(_mail.Messages);
        using var verify = _services.CreateScope();
        var database = verify.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.Equal(originalHash, await database.Invites.Where(i => i.Id == inviteId)
            .Select(i => i.TokenHash).SingleAsync());
    }

    private async Task<int> SeedAsync()
    {
        using var scope = _services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DemoInvitationSeeder>().RunAsync(default);
    }

    private static string Token(EmailMessage message) =>
        Regex.Match(message.TextBody, @"https://demo\.example\.test/book/([^\s]+)").Groups[1].Value;

    private sealed class CapturingTransport : IEmailTransport
    {
        /// <summary>Messages accepted by the external provider boundary.</summary>
        public List<EmailMessage> Messages { get; } = [];
        /// <summary>Optional one-based provider attempt to fail.</summary>
        public int? FailOnAttempt { get; set; }
        private int _attempt;
        /// <inheritdoc/>
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (++_attempt == FailOnAttempt) throw new IOException("Test SMTP failure.");
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class DemoClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow { get; private set; } = new(2030, 1, 7, 12, 0, 0, TimeSpan.Zero);
        /// <inheritdoc/>
        public DateTimeOffset NowAtTransitionalLocation => TimeZoneInfo.ConvertTime(UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/London"));
        /// <inheritdoc/>
        public DateOnly TodayAtTransitionalLocation => DateAtTransitionalLocation(UtcNow);
        /// <inheritdoc/>
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")).DateTime);
        /// <inheritdoc/>
        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(
            instant, TimeZoneInfo.FindSystemTimeZoneById("Europe/London"));
        /// <summary>Moves the observation clock without modifying persisted data.</summary>
        public void Advance(TimeSpan elapsed) => UtcNow += elapsed;
    }
}
`````
