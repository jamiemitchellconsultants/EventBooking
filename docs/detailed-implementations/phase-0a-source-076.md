# 00a — Port source 76 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Infrastructure.Tests/SystemClockTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/SystemClockTests.cs","encoding":"utf8","sha256":"1605c316ec6443580c2d1f8ffb3a6619dfae56998cb482465d0de58d8e1320d7","parts":1,"part":1} -->

`````csharp
using EventBooking.Infrastructure.Time;

namespace EventBooking.Infrastructure.Tests;

public class SystemClockTests
{
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    [Fact]
    public void TheLocalDateFollowsTheHeadOfficeZoneNotUtc()
    {
        // 23:30 UTC on 9 September is already 00:30 on 10 September in British Summer Time.
        var instant = new DateTimeOffset(2026, 9, 9, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 9, 10), SystemClock.LocalDateOf(instant, London));
    }

    [Fact]
    public void InWinterTheZoneMatchesUtc()
    {
        var instant = new DateTimeOffset(2026, 1, 9, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 1, 9), SystemClock.LocalDateOf(instant, London));
    }

    [Fact]
    public void TheClockReportsAUtcInstant()
    {
        var clock = new SystemClock(new HeadOfficeOptions("Europe/London"));

        Assert.Equal(TimeSpan.Zero, clock.UtcNow.Offset);
        Assert.InRange(
            clock.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public void AnUnknownTimeZoneFailsAtConstructionNotAtUseTime()
    {
        Assert.ThrowsAny<Exception>(() => new SystemClock(new HeadOfficeOptions("Mars/Olympus_Mons")));
    }

    /// <summary>Verifies a UTC instant renders with the head-office offset in British Summer Time.</summary>
    [Fact]
    public void InstantAtHeadOfficeConvertsAUtcInstantToLondonLocalTime()
    {
        var clock = new SystemClock(new HeadOfficeOptions("Europe/London"));
        var instant = new DateTimeOffset(2026, 9, 15, 8, 30, 0, TimeSpan.Zero);

        var local = clock.InstantAtHeadOffice(instant);

        Assert.Equal(TimeSpan.FromHours(1), local.Offset);
        Assert.Equal(new TimeOnly(9, 30), TimeOnly.FromDateTime(local.DateTime));
    }

    /// <summary>Verifies the London local date can run ahead of the UTC date late in the evening.</summary>
    [Fact]
    public void InstantAtHeadOfficeLocalDateCanDifferFromTheUtcDate()
    {
        var clock = new SystemClock(new HeadOfficeOptions("Europe/London"));
        // 23:30 UTC on 15 September is already 00:30 on 16 September in British Summer Time.
        var instant = new DateTimeOffset(2026, 9, 15, 23, 30, 0, TimeSpan.Zero);

        var local = clock.InstantAtHeadOffice(instant);

        Assert.Equal(new DateOnly(2026, 9, 16), DateOnly.FromDateTime(local.DateTime));
    }

    /// <summary>Verifies conversion re-expresses an instant rather than shifting it.</summary>
    [Fact]
    public void InstantAtHeadOfficePreservesTheSameInstant()
    {
        var clock = new SystemClock(new HeadOfficeOptions("Europe/London"));
        var instant = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.FromHours(-4));

        Assert.Equal(instant.UtcDateTime, clock.InstantAtHeadOffice(instant).UtcDateTime);
    }

    /// <summary>Verifies a winter instant carries the zero London offset.</summary>
    [Fact]
    public void InstantAtHeadOfficeUsesTheZeroOffsetInLondonWinter()
    {
        var clock = new SystemClock(new HeadOfficeOptions("Europe/London"));
        var instant = new DateTimeOffset(2026, 1, 15, 8, 30, 0, TimeSpan.Zero);

        var local = clock.InstantAtHeadOffice(instant);

        Assert.Equal(TimeSpan.Zero, local.Offset);
        Assert.Equal(new TimeOnly(8, 30), TimeOnly.FromDateTime(local.DateTime));
    }
}
`````

## tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs","encoding":"utf8","sha256":"1c70afbc33c79eb36aee9e9cd482ae576840367989e4995adf78593d2258ec22","parts":1,"part":1} -->

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

## tests/EventBooking.Mcp.Tests/AgentSurfaceParityTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Mcp.Tests/AgentSurfaceParityTests.cs","encoding":"utf8","sha256":"e814d3a03868d57fb05c2638c76dd92cbfa7e9beb7d7c226063ccd014ee0214c","parts":1,"part":1} -->

`````csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Api.OpenApi;
using EventBooking.Domain.Access;

namespace EventBooking.Mcp.Tests;

[Collection("mcp")]
public sealed class AgentSurfaceParityTests(McpFactory factory)
{
    [Fact]
    public async Task ToolsListExactlyMatchesCatalogAndHints()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { jsonrpc = "2.0", id = "1", method = "tools/list" }),
                Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var data = body.TrimStart().StartsWith('{') ? body : body.Split('\n').Select(x => x.Trim())
            .Last(x => x.StartsWith("data: "))["data: ".Length..];
        using var json = JsonDocument.Parse(data);
        var actual = json.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .ToDictionary(x => x.GetProperty("name").GetString()!);
        var expected = AgentOperationCatalog.All.Values.Where(x => x.McpTool is not null)
            .ToDictionary(x => x.McpTool!);
        Assert.Equal(36, actual.Count);
        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        foreach (var pair in expected)
        {
            var annotations = actual[pair.Key].GetProperty("annotations");
            Assert.Equal(pair.Value.Hints.ReadOnly, annotations.GetProperty("readOnlyHint").GetBoolean());
            Assert.Equal(pair.Value.Hints.Destructive, annotations.GetProperty("destructiveHint").GetBoolean());
            Assert.Equal(pair.Value.Hints.Idempotent, annotations.GetProperty("idempotentHint").GetBoolean());
            Assert.Equal(pair.Value.Hints.OpenWorld, annotations.GetProperty("openWorldHint").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(actual[pair.Key].GetProperty("description").GetString()));
        }
    }
}
`````

## tests/EventBooking.Mcp.Tests/CandidateMcpTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Mcp.Tests/CandidateMcpTests.cs","encoding":"utf8","sha256":"e77ac8010f2133925c74d805b5ecdbc38d25c470903c0467cfd796258444d541","parts":1,"part":1} -->

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

## tests/EventBooking.Mcp.Tests/CandidateParityMcpTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Mcp.Tests/CandidateParityMcpTests.cs","encoding":"utf8","sha256":"6a0704135fe6faeb7ae3a1ea14fb7c1ca6e881c1b6d1c308c56fd2886f3017a7","parts":1,"part":1} -->

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

## tests/EventBooking.Mcp.Tests/EventBooking.Mcp.Tests.csproj — 1/1

<!-- port-file: {"path":"tests/EventBooking.Mcp.Tests/EventBooking.Mcp.Tests.csproj","encoding":"utf8","sha256":"bcb17386832a1efbab3c4843d2b42ae9b697dcc0d0c55f8aa09afc3a86236862","parts":1,"part":1} -->

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

## tests/EventBooking.Mcp.Tests/Fakes/RecordingEmailTransport.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Mcp.Tests/Fakes/RecordingEmailTransport.cs","encoding":"utf8","sha256":"79bf099a3aa7307e5da63ee17aca7441847864f38b6552d05782373cc40ee86a","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure.Email;

namespace EventBooking.Mcp.Tests.Fakes;

/// <summary>
/// Stands in for AWS SES in every MCP test. Constructing the real
/// <c>AmazonSimpleEmailServiceV2Client</c> throws immediately outside an AWS environment (no
/// RegionEndpoint or ServiceURL configured), which is exactly where CI runs — so no test may
/// depend on it, directly or through a tool that happens to send an email.
/// </summary>
public sealed class RecordingEmailTransport : IEmailTransport
{
    /// <summary>Messages accepted by this fake provider.</summary>
    public List<EmailMessage> Sent { get; } = [];

    /// <inheritdoc />
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}
`````

## tests/EventBooking.Mcp.Tests/McpEndpointTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Mcp.Tests/McpEndpointTests.cs","encoding":"utf8","sha256":"8e620d676694f54dccd134a01d445e6cbf84ab996579c7e9e274d46f4c69be5d","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;

namespace EventBooking.Mcp.Tests;

/// <summary>Covers the MCP transport: authorization, tool discovery, and stateless calls.</summary>
[Collection("mcp")]
public sealed class McpEndpointTests(McpFactory factory)
{
    private static readonly string[] ExpectedTools =
    [
        "propose_slot", "accept_proposal", "withdraw_acceptance", "withdraw_proposal",
        "slot_board", "adjust_slot_capacity", "cancel_confirmed_slot", "import_confirmed_slots",
        "list_candidates", "create_candidate", "update_candidate", "delete_candidate",
        "list_employee_groups",
        "import_candidates", "trigger_invite", "retry_candidate_email",
        "start_recovery_invite", "cancel_recovery_invite", "list_candidate_bookings",
        "cancel_candidate_booking", "get_candidate_readiness",
        "get_settings", "update_settings", "list_staff_access", "replace_staff_access_scope",
        "clear_staff_access_scope", "get_my_access",
        "get_dashboards", "slot_audit_history", "candidate_audit_history", "search_audit",
        "appointment_slots", "appointment_slot_detail", "export_appointment_roster", "update_appointment_status",
        "get_slot_operations",
    ];

    /// <summary>Anonymous MCP requests are refused before any tool runs.</summary>
    [Fact]
    public async Task AnonymousMcpRequest_IsUnauthorized()
    {
        factory.SignedInAs = null;

        var response = await PostRpcAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>An authenticated profile without a valid staff claim cannot reach MCP tools.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("not-a-staff-id")]
    public async Task AuthenticatedMcpRequestWithoutValidStaffClaim_IsForbidden(string? staffIdClaim)
    {
        var originalStaffUserId = factory.SignedInAs;
        var originalStaffIdClaim = factory.StaffIdClaim;
        try
        {
            factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
            factory.StaffIdClaim = staffIdClaim;

            var response = await PostRpcAsync(
                new { jsonrpc = "2.0", id = "1", method = "tools/list" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            factory.SignedInAs = originalStaffUserId;
            factory.StaffIdClaim = originalStaffIdClaim;
        }
    }

    /// <summary>An authenticated caller discovers the full staff tool surface.</summary>
    [Fact]
    public async Task ToolsList_ExposesFullStaffSurface()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);

        var payload = await PostRpcJsonAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });
        var names = payload
            .GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString())
            .ToHashSet();

        Assert.Equal(ExpectedTools.Order(), names.Order());
        Assert.Equal(36, names.Count);
    }

    /// <summary>Every tool carries explicit safety hints with a closed world.</summary>
    [Fact]
    public async Task ToolsList_ExposesExplicitSafetyHints()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);

        var payload = await PostRpcJsonAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });
        foreach (var tool in payload.GetProperty("result").GetProperty("tools").EnumerateArray())
        {
            Assert.True(tool.TryGetProperty("annotations", out var annotations), $"Tool {tool.GetProperty("name")} is missing annotations.");
            Assert.True(annotations.TryGetProperty("readOnlyHint", out _), $"Tool {tool.GetProperty("name")} is missing readOnlyHint.");
            Assert.True(annotations.TryGetProperty("destructiveHint", out _), $"Tool {tool.GetProperty("name")} is missing destructiveHint.");
            Assert.True(annotations.TryGetProperty("idempotentHint", out _), $"Tool {tool.GetProperty("name")} is missing idempotentHint.");
            Assert.True(
                annotations.TryGetProperty("openWorldHint", out var openWorld) && openWorld.ValueKind == JsonValueKind.False,
                $"Tool {tool.GetProperty("name")} must have openWorldHint false.");
        }
    }

    /// <summary>A tool call runs as the signed-in identity.</summary>
    [Fact]
    public async Task GetMyAccess_ReturnsCallerRoles()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await CallToolAsync("get_my_access", new { });

        Assert.Contains("Coordinator", payload.GetRawText());
        Assert.False(IsToolError(payload));
    }

    /// <summary>The caller's validated staff claim is returned by the self-description tool.</summary>
    [Fact]
    public async Task GetMyAccess_ReturnsCallerStaffNumber()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Coordinator], null, "U123456");

        var payload = await CallToolAsync("get_my_access", new { });
        var contentText = payload.GetProperty("result").GetProperty("content")[0]
            .GetProperty("text").GetString();
        using var content = JsonDocument.Parse(contentText!);

        Assert.Equal(
            "U123456",
            content.RootElement.GetProperty("staffId").GetString());
        Assert.False(IsToolError(payload));
    }

    /// <summary>Sequential calls without any session identifier each succeed.</summary>
    [Fact]
    public async Task StatelessCalls_NeedNoSession()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var first = await CallToolAsync("get_my_access", new { });
        var second = await CallToolAsync("get_dashboards", new { });

        Assert.True(first.TryGetProperty("result", out _));
        Assert.True(second.TryGetProperty("result", out _));
        Assert.False(IsToolError(first));
        Assert.False(IsToolError(second));
    }

    /// <summary>A capability failure surfaces as a tool error, not a transport failure.</summary>
    [Fact]
    public async Task ForbiddenCapability_SurfacesAsToolError()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await CallToolAsync(
            "propose_slot", new { date = "2026-10-01", startTime = "09:00" });

        Assert.True(IsToolError(payload));
    }

    private async Task<HttpResponseMessage> PostRpcAsync(object body)
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return await client.SendAsync(request);
    }

    private async Task<JsonElement> PostRpcJsonAsync(object body)
    {
        var response = await PostRpcAsync(body);
        response.EnsureSuccessStatusCode();
        return ParseRpcPayload(await response.Content.ReadAsStringAsync());
    }

    private Task<JsonElement> CallToolAsync(string name, object arguments) =>
        PostRpcJsonAsync(new
        {
            jsonrpc = "2.0",
            id = "1",
            method = "tools/call",
            @params = new { name, arguments },
        });

    private static JsonElement ParseRpcPayload(string body)
    {
        if (body.TrimStart().StartsWith('{'))
        {
            return JsonDocument.Parse(body).RootElement;
        }

        var data = body
            .Split('\n')
            .Select(line => line.Trim())
            .LastOrDefault(line => line.StartsWith("data: "))
            ?.Substring("data: ".Length);
        Assert.False(string.IsNullOrWhiteSpace(data), "MCP response carried no data frame.");
        return JsonDocument.Parse(data!).RootElement;
    }

    private static bool IsToolError(JsonElement payload) =>
        payload.TryGetProperty("result", out var result) &&
        result.TryGetProperty("isError", out var isError) &&
        isError.ValueKind == JsonValueKind.True;
}
`````

## tests/EventBooking.Mcp.Tests/McpFactory.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Mcp.Tests/McpFactory.cs","encoding":"utf8","sha256":"98f0b347aa2090c96b15150125b9b425562844247b94e867c39ff5fff60a56ed","parts":1,"part":1} -->

`````csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using EventBooking.Domain.Access;
using EventBooking.Infrastructure.Email;
using EventBooking.Mcp.Tests.Fakes;
using EventBooking.Mcp.Tools;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace EventBooking.Mcp.Tests;

/// <summary>
/// Starts the real MCP host against a throwaway PostgreSQL container, with the bearer
/// token validation replaced by a test scheme so a test can say who is calling by
/// setting <see cref="SignedInAs"/>.
/// </summary>
public sealed class McpFactory : WebApplicationFactory<SlotTools>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();
    private int _staffIdSequence;

    /// <summary>The Entra object identifier every request is made as. Null means anonymous.</summary>
    public Guid? SignedInAs { get; set; }

    /// <summary>The provider-issued staff number claim, or null to omit the claim.</summary>
    public string? StaffIdClaim { get; set; } = "U999999";

    /// <summary>The untrusted <c>roles</c> claim values attached to authenticated test requests.</summary>
    public IReadOnlyCollection<string> RolesClaim { get; set; } = [];

    /// <summary>Stands in for AWS SES so MCP tools that send email don't need an AWS environment.</summary>
    public RecordingEmailTransport EmailTransport { get; } = new();

    /// <summary>Creates a staff access profile for a fresh identity.</summary>
    /// <param name="roles">The roles to grant.</param>
    /// <param name="appointmentTypeId">The shared scope, if any.</param>
    /// <param name="staffIdClaim">The fixed staff number for the request, or null to allocate one.</param>
    /// <returns>The new staff identity.</returns>
    public async Task<Guid> GivenStaffAsync(
        IReadOnlyCollection<Role> roles,
        Guid? appointmentTypeId,
        string? staffIdClaim = null)
    {
        StaffIdClaim = staffIdClaim ?? $"U{Interlocked.Increment(ref _staffIdSequence):D6}";
        RolesClaim = roles.Select(role => role.ToString()).ToList();
        var staffUserId = Guid.NewGuid();
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EventBooking.Infrastructure.Persistence.EventBookingDbContext>();
        if (roles.Contains(Role.Manager) && appointmentTypeId is not null)
        {
            var previous = await context.StaffAccessProfiles.SingleOrDefaultAsync(profile =>
                profile.IsManager && profile.AppointmentTypeId == appointmentTypeId);
            if (previous is not null)
            {
                context.StaffAccessProfiles.Remove(previous);
            }
        }

        context.StaffAccessProfiles.Add(
            StaffAccessProfile.Create(staffUserId, roles, appointmentTypeId));
        await context.SaveChangesAsync();
        return staffUserId;
    }

    /// <summary>Returns the recorded identity for the provider key, when one exists.</summary>
    /// <param name="staffUserId">The provider key to find.</param>
    /// <returns>The recorded identity, or null.</returns>
    public async Task<StaffIdentity?> FindIdentityAsync(Guid staffUserId)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EventBooking.Infrastructure.Persistence.EventBookingDbContext>();
        return await context.StaffIdentities.AsNoTracking().SingleOrDefaultAsync(
            identity => identity.StaffUserId == staffUserId);
    }

    /// <summary>Returns the access profile for the provider key, when one exists.</summary>
    /// <param name="staffUserId">The provider key to find.</param>
    /// <returns>The access profile, or null.</returns>
    public async Task<StaffAccessProfile?> FindProfileAsync(Guid staffUserId)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EventBooking.Infrastructure.Persistence.EventBookingDbContext>();
        return await context.StaffAccessProfiles.AsNoTracking().SingleOrDefaultAsync(
            profile => profile.StaffUserId == staffUserId);
    }

    /// <summary>Starts the container and migrates the throwaway database.</summary>
    /// <returns>A task tracking initialization.</returns>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EventBooking.Infrastructure.Persistence.EventBookingDbContext>();
        await context.Database.MigrateAsync();
    }

    /// <summary>Disposes the container and the host.</summary>
    /// <returns>A task tracking disposal.</returns>
    public new async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:EventBooking", _container.GetConnectionString());
        builder.UseSetting("Tokens:SigningKey", "a-test-signing-key-that-is-long-enough-here");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(this);
            services
                .AddAuthentication(TestAuthenticationHandler.Scheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.Scheme, _ => { });

            // The real transport constructs an AWS SES client that throws immediately outside an
            // AWS environment (no RegionEndpoint or ServiceURL configured) — exactly where CI runs.
            services.AddSingleton<IEmailTransport>(EmailTransport);
        });
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        McpFactory factory) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "Test";

        /// <inheritdoc />
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (factory.SignedInAs is null)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new("oid", factory.SignedInAs.Value.ToString()),
            };
            if (factory.StaffIdClaim is not null)
            {
                claims.Add(new Claim("staff_id", factory.StaffIdClaim));
            }
            foreach (var role in factory.RolesClaim)
            {
                claims.Add(new Claim("roles", role));
            }

            var identity = new ClaimsIdentity(claims, Scheme);

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}

/// <summary>Shares one MCP host across the endpoint tests.</summary>
[CollectionDefinition("mcp")]
public sealed class McpCollection : ICollectionFixture<McpFactory>;
`````
