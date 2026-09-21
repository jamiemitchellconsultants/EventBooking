# 03g — Durable outbox dispatcher and location-aware templates (Task 18)

[← Phase overview](phase-3-application.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 17. Email becomes a durable outbox: commands stage pending rows with
the business change, a hosted dispatcher claims and sends them, and one pure composer renders
all four templates with location-aware windows. This is decision D13 in code.

> Use superpowers:executing-plans. This task is hand-authored: complete code and complete tests
> are written straight into this document, with no prototype. Compile and test-drive them
> yourself. The test counts below are what you should expect to reach, not figures observed by
> the author — nothing here has been run.

**Goal:** The email sender port returning sent, transient failure or permanent failure, never
throwing for a provider error, with a 30-second timeout. The composer as a pure function from
(template, context) to subject, text and HTML exactly per the email-content design, with types
in code order and windows in the location's zone with abbreviation. The dispatcher as a hosted
service claiming up to 20 rows with `FOR UPDATE SKIP LOCKED`, reclaiming claims older than 5
minutes, keeping transient failures pending for up to 3 claims with backoff before `Failed`,
auditing `InviteSent` for invite emails. RetryEmail (attendee id) creating a new pending row
and marking the old one `Resolved`. Links regenerated from entity id and token version at send
time. The head-office wording retires, replaced with location details. The ported
immediate-send delivery service and its composer retire with it.

**Architecture:** No email is sent inside a transaction: commands insert a pending row and
commit it with the business change (Tasks 14 and 15 already stage this way), and the
dispatcher wakes every 5 seconds or on an in-process signal after a commit. Two dispatcher
instances against one database never send the same row twice (skip-locked claims). A resent
email carries the same link as the original (deterministic regeneration from the row's
entity id and token version). Outbox rows never contain an address, name, token or URL —
assert over all columns. The dispatcher is a hosted service in the Api process; the Mcp
container runs neither job. No domain entity leaves the handler.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers.

**Spec:** [Master Task 18](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[functional requirements](../design/02-functional-requirements.md),
[solution architecture](../design/04-solution-architecture.md), [ontology](../ontology.md).

## Outbox schema (contradiction #6, settled with the user)

The dispatcher needs attempt, backoff and correlation state the current outbox row does not
fully carry — the claim count exists (Task 9b), but next-attempt and correlation do not — so
Task 18 migrates two columns onto the email log: a not-before instant for backoff and a
correlation id propagated from the request's structured log. The migration backfills existing
rows as never-claimed and drops no defaults new rows must state. Remove any in-transaction
send left from the port.

## Delivery promise (contradiction #7, settled with the user)

The design promises at-least-once delivery, not exactly-once: an SMTP crash after send but
before marking sent means the reclaimed row sends again. The document states the crash window
explicitly, and the idempotency that makes a duplicate harmless is the deterministic link
(same entity id and version regenerates the same URL) plus the duplicate being a re-send of
identical content.

## Global constraints

Composer golden tests cover all four templates, including the recovery singular and plural
wording and both `EventCancelledRebookingNeeded` endings; a London window in July renders
"BST"; types render in code order regardless of input order; the HTML part carries the same
lines. A row claimed 6 minutes ago by a crashed dispatcher is reclaimed. Transient failures
three times end `Failed` with no error text stored.

## Review focus

STOP AND CHECK four things. The two-dispatcher test proves no double-send against one
database. The reclaim test uses a 6-minute-old claim (past the 5-minute line). The
no-personal-data test scans every column of every outbox row. And the golden files carry the
location name, address, local time and zone — never the retired head-office wording.

### Task 18: Notification outbox and templates

**Files:**

- Modify: src/EventBooking.Application/Common/Error.cs (no new codes; dispatcher outcomes
  are not handler errors)
- Create: src/EventBooking.Infrastructure/Email/EmailComposer.cs
- Create: src/EventBooking.Infrastructure/Email/OutboxDispatcher.cs
- Create: src/EventBooking.Infrastructure/Email/ClaimQuery.cs (the skip-locked claim SQL)
- Modify: src/EventBooking.Infrastructure/Email/SmtpEmailTransport.cs (outcome return,
  30-second timeout; transport interface gains the outcome)
- Modify: src/EventBooking.Application/Notifications/RetryEmailHandler.cs (new pending row,
  old row resolved; no immediate send)
- Modify: src/EventBooking.Application/Bookings/ViewBookingHandler.cs (window formatter
  from the new composer; complete below)
- Modify: src/EventBooking.Application/Bookings/ViewInviteHandler.cs (same)
- Delete: src/EventBooking.Application/Notifications/AttendeeEmailComposer.cs
- Delete: src/EventBooking.Application/Notifications/EmailDeliveryService.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/<generated-timestamp>_EmailOutboxColumns.cs
  (plus its Designer; the timestamp prefix comes from generation)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs
- Delete: tests/EventBooking.Application.Tests/Notifications/AttendeeEmailComposerTests.cs
  (replaced by the composer golden suite below)
- Modify: src/EventBooking.Domain/Notifications/EmailLog.cs (SetNotBefore method)
- Test: tests/EventBooking.Infrastructure.Tests/Email/EmailComposerTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/Email/OutboxDispatcherTests.cs
- Test: tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs

**Interfaces:**

```csharp
namespace EventBooking.Infrastructure.Email;

// The sender never throws for a provider error: every outcome is a value. A 30-second
// timeout counts as a transient failure.
public enum EmailSendOutcome { Sent, TransientFailure, PermanentFailure }

public interface IEmailTransport
{
    Task<EmailSendOutcome> SendAsync(
        string recipient, string subject, string textBody, string htmlBody,
        CancellationToken ct);
}

// Pure rendering: (template, context) to (subject, text, HTML). Windows render in the
// location's zone with its abbreviation; types sort in code order; links are not rendered
// here — the dispatcher regenerates them from the row's entity id and token version.
public static class EmailComposer
{
    public static EmailMessage Compose(string template, EmailContext context);
    public static string FormatWindow(
        DateOnly date, TimeOnly start, TimeOnly end, string locationName, string abbreviation);
}

public sealed record EmailContext(
    IReadOnlyList<string> TypeCodes,
    string LocationName,
    string LocationAddress,
    string WindowText,
    string BookUrl,
    string CoordinatorContact,
    bool ReplacementCreated,
    bool IsRecovery,
    int OutstandingCount);
```

```csharp
namespace EventBooking.Application.Notifications;

public sealed record RetryEmailCommand(Guid StaffUserId, Guid AttendeeId, Guid EmailLogId);
```

- [ ] **Step 1: Write the failing tests.** Create the three test files below in full.

  ```csharp
  // tests/EventBooking.Infrastructure.Tests/Email/EmailComposerTests.cs (complete)
  using EventBooking.Infrastructure.Email;

  namespace EventBooking.Infrastructure.Tests.Email;

  public sealed class EmailComposerTests
  {
      private static EmailContext LondonJuly(params string[] codes) => new(
          codes, "London HQ", "1 High St", "Tue 14 Jul 2026, 09:30-11:00 BST",
          "https://portal.example.invalid/book/token", "coordinator@example.invalid",
          false, false, codes.Length);

      [Fact]
      public void Invite_renders_zone_abbreviation_and_sorted_types()
      {
          var message = EmailComposer.Compose("AttendeeInvite", LondonJuly("IND", "FIT", "MED"));

          Assert.Contains("BST", message.TextBody);
          Assert.Contains("London HQ", message.TextBody);
          Assert.Contains("1 High St", message.TextBody);
          Assert.True(message.TextBody.IndexOf("FIT", StringComparison.Ordinal)
              < message.TextBody.IndexOf("IND", StringComparison.Ordinal));
          Assert.True(message.TextBody.IndexOf("IND", StringComparison.Ordinal)
              < message.TextBody.IndexOf("MED", StringComparison.Ordinal));
      }

      [Theory]
      [InlineData(1, "appointment remains")]
      [InlineData(2, "appointments remain")]
      public void Recovery_wording_follows_count(int count, string expected)
      {
          var context = LondonJuly("MED") with { IsRecovery = true, OutstandingCount = count };

          var message = EmailComposer.Compose("AttendeeInvite", context);

          Assert.Contains(expected, message.TextBody);
      }

      [Theory]
      [InlineData(true, "a replacement has been created")]
      [InlineData(false, "no replacement could be created")]
      public void Cancellation_ending_follows_replacement_flag(bool created, string expected)
      {
          var context = LondonJuly("MED") with { ReplacementCreated = created };

          var message = EmailComposer.Compose("EventCancelledRebookingNeeded", context);

          Assert.Contains(expected, message.TextBody);
      }

      [Fact]
      public void Html_carries_the_same_lines_as_text()
      {
          var message = EmailComposer.Compose("BookingConfirmation", LondonJuly("MED"));

          Assert.Equal(
              message.TextBody.Split('\n').Select(l => l.Trim()),
              message.HtmlBody.Split('\n').Select(l => l.Trim()));
      }

      [Fact]
      public void Unknown_template_is_refused()
      {
          Assert.Throws<ArgumentException>(() =>
              EmailComposer.Compose("NoSuchTemplate", LondonJuly("MED")));
      }
  }
  ```

  EmailMessage is the existing port record (subject, text, HTML among its members —
  verify the member names against the port before accepting; adjust the property reads,
  not the assertions). The golden wordings above are the contract: implement the composer
  to render exactly these lines.

  ```csharp
  // tests/EventBooking.Infrastructure.Tests/Email/OutboxDispatcherTests.cs (complete,
  // real PostgreSQL 16 via the Task 9b fixture pattern)
  using EventBooking.Domain.Notifications;

  namespace EventBooking.Infrastructure.Tests.Email;

  public sealed class OutboxDispatcherTests : PostgresEmailHarness
  {
      [Fact]
      public async Task Two_dispatchers_never_send_the_same_row_twice()
      {
          var rowId = await StagePendingAsync(EmailTemplate.AttendeeInvite);
          var first = Dispatcher(transportA);
          var second = Dispatcher(transportB);

          await Task.WhenAll(first.DispatchOnceAsync(), second.DispatchOnceAsync());

          Assert.Equal(1, transportA.Sent.Count + transportB.Sent.Count);
          Assert.Equal(EmailStatus.Sent, await StatusOfAsync(rowId));
      }

      [Fact]
      public async Task Six_minute_old_claim_is_reclaimed()
      {
          var rowId = await StagePendingAsync(EmailTemplate.AttendeeInvite);
          await CrashClaimAsync(rowId, claimedMinutesAgo: 6);

          await Dispatcher(transportA).DispatchOnceAsync();

          Assert.Single(transportA.Sent);
          Assert.Equal(EmailStatus.Sent, await StatusOfAsync(rowId));
      }

      [Fact]
      public async Task Transient_times_three_ends_failed_with_no_error_text()
      {
          var rowId = await StagePendingAsync(EmailTemplate.AttendeeInvite);
          transportA.Next = EmailSendOutcome.TransientFailure;

          await Dispatcher(transportA).DispatchOnceAsync();
          await Dispatcher(transportA).DispatchOnceAsync();
          await Dispatcher(transportA).DispatchOnceAsync();

          Assert.Equal(EmailStatus.Failed, await StatusOfAsync(rowId));
          Assert.Empty(transportA.Sent);
          Assert.DoesNotContain(await AllColumnsAsync(rowId), "failure");
          Assert.DoesNotContain(await AllColumnsAsync(rowId), "transient");
      }

      [Fact]
      public async Task Resent_email_carries_the_same_link_as_original()
      {
          var rowId = await StagePendingAsync(EmailTemplate.AttendeeInvite);
          await Dispatcher(transportA).DispatchOnceAsync();
          var firstLink = transportA.Sent.Single();

          await RetryAsync(rowId);
          await Dispatcher(transportA).DispatchOnceAsync();

          Assert.Equal(2, transportA.Sent.Count);
          Assert.Equal(firstLink, transportA.Sent[1]);
      }

      [Fact]
      public async Task Outbox_rows_carry_no_personal_data_tokens_or_urls()
      {
          var rowId = await StagePendingAsync(EmailTemplate.AttendeeInvite);

          var columns = await AllColumnsAsync(rowId);

          Assert.DoesNotContain("amy@example.invalid", columns);
          Assert.DoesNotContain("Amy", columns);
          Assert.DoesNotContain("http", columns);
      }
  }
  ```

  PostgresEmailHarness extends the Task 9b fixture: stage rows through
  `EmailLog.RecordPending`, drive DispatchOnceAsync (one claim-and-send pass — the
  hosted loop calls it on its timer), and read rows back column by column.
  `transportA.Next` forces the next send outcome. RetryAsync drives the Task 18 retry
  handler. AllColumnsAsync concatenates every column's text — the personal-data test
  fails on any leak.

  ```csharp
  // tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs (complete)
  using EventBooking.Application.Common;
  using EventBooking.Application.Notifications;
  using EventBooking.Application.Tests.Fakes;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.Notifications;

  namespace EventBooking.Application.Tests.Notifications;

  public sealed class RetryEmailHandlerTests
  {
      [Fact]
      public async Task Retry_creates_pending_and_resolves_old_with_same_link_inputs()
      {
          var deliveries = new InMemoryEmailDeliveryRepository();
          var profiles = new InMemoryStaffAccessProfileRepository();
          var unitOfWork = new FakeUnitOfWork();
          var audit = new RecordingAuditLogger();
          var coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
          profiles.Items.Add(StaffAccessProfile.Create(coordinator, Role.Coordinator, null));
          var attendeeId = Guid.NewGuid();
          var inviteId = Guid.NewGuid();
          var old = EmailLog.RecordPending(Guid.NewGuid(), attendeeId,
              EmailTemplate.AttendeeInvite, DateTimeOffset.UtcNow, inviteId: inviteId);
          old.MarkFailed(DateTimeOffset.UtcNow);
          deliveries.Items.Add(old);
          var handler = new RetryEmailHandler(
              profiles, deliveries, profiles, unitOfWork, audit, new FakeClock());

          var result = await handler.HandleAsync(
              new RetryEmailCommand(coordinator, attendeeId, old.Id), CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal(EmailStatus.Resolved, old.Status);
          var fresh = Assert.Single(deliveries.Items.Where(d => d.Id != old.Id));
          Assert.Equal(EmailStatus.Pending, fresh.Status);
          Assert.Equal(EmailTemplate.AttendeeInvite, fresh.TemplateName);
          Assert.Equal(inviteId, fresh.InviteId);
      }

      [Fact]
      public async Task Retry_by_non_coordinator_is_forbidden()
      {
          var deliveries = new InMemoryEmailDeliveryRepository();
          var profiles = new InMemoryStaffAccessProfileRepository();
          var handler = new RetryEmailHandler(
              profiles, deliveries, profiles, new FakeUnitOfWork(), new RecordingAuditLogger(),
              new FakeClock());

          var result = await handler.HandleAsync(
              new RetryEmailCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
              CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("forbidden", result.Error.Code);
      }
  }
  ```

  The in-memory delivery fake's mutable collection holds both rows (verify the member
  name against the existing fake before accepting; adjust the collection reads, not the
  assertions).

- [ ] **Step 2: Run.** Expected: FAIL to compile — the dispatcher and composer do not exist.

  ```bash
  dotnet test tests/EventBooking.Infrastructure.Tests --filter "FullyQualifiedName~Email"
  ```

  ```csharp
  // src/EventBooking.Application/Bookings/ViewBookingHandler.cs and
  // src/EventBooking.Application/Bookings/ViewInviteHandler.cs — one call site each.
  //
  // Both read the window text from AttendeeEmailComposer, which this task deletes. The new
  // composer's formatter takes the location's name and zone abbreviation rather than
  // deriving them, because an event is no longer at one known site: the caller holds the
  // location and the resolver, so it passes what it already knows.
  //
  // Was:
  //     AttendeeEmailComposer.FormatWindow(eventItem.Window)
  // Now:
  //     EmailComposer.FormatWindow(
  //         eventItem.Window.Date,
  //         eventItem.Window.StartTime,
  //         eventItem.Window.EndTime,
  //         location.Name,
  //         zones.AbbreviationOf(
  //             eventItem.Window.StartInstant(zones, location.TimeZoneId), location.TimeZoneId))
  //
  // Each handler already loads the event; both gain ILocationRepository and
  // IEventWindowZones constructor parameters to reach the location and the abbreviation.
  // Nothing else in either file changes, and the using of the deleted composer goes with
  // it.
  ```

- [ ] **Step 3: Implement.** Add the production code below in full, then the migration.
  No placeholders: every file below is complete.

  ```csharp
  // src/EventBooking.Infrastructure/Email/EmailComposer.cs (complete)
  using EventBooking.Application.Abstractions;

  namespace EventBooking.Infrastructure.Email;

  public sealed record EmailContext(
      IReadOnlyList<string> TypeCodes,
      string LocationName,
      string LocationAddress,
      string WindowText,
      string BookUrl,
      string CoordinatorContact,
      bool ReplacementCreated,
      bool IsRecovery,
      int OutstandingCount);

  public static class EmailComposer
  {
      public static EmailMessage Compose(string template, EmailContext context) =>
          template switch
          {
              "AttendeeInvite" => InviteMessage(context),
              "AttendeeReinvite" => InviteMessage(context),
              "EventCancelledRebookingNeeded" => CancellationMessage(context),
              "BookingConfirmation" => ConfirmationMessage(context),
              _ => throw new ArgumentException($"Unknown email template '{template}'.", nameof(template)),
          };

      public static string FormatWindow(
          DateOnly date, TimeOnly start, TimeOnly end, string locationName, string abbreviation) =>
          $"{date:ddd dd MMM yyyy}, {start:HH:mm}-{end:HH:mm} {abbreviation} at {locationName}";

      private static EmailMessage InviteMessage(EmailContext context)
      {
          var types = string.Join(", ", context.TypeCodes.OrderBy(code => code, StringComparer.Ordinal));
          var lines = new List<string>
          {
              $"You are invited to {types}.",
              context.LocationName,
              context.LocationAddress,
              context.WindowText,
              RecoveryLine(context),
              $"Book here: {context.BookUrl}",
              $"Contact: {context.CoordinatorContact}",
          };
          return new EmailMessage(Guid.Empty, string.Empty, string.Empty,
              context.IsRecovery ? EmailTemplate.AttendeeReinvite : EmailTemplate.AttendeeInvite,
              $"Invitation: {types}", string.Join("\n", lines), string.Join("\n", lines));
      }

      private static string RecoveryLine(EmailContext context) =>
          !context.IsRecovery ? "Please respond before the invitation expires."
          : context.OutstandingCount == 1 ? "1 appointment remains to be rebooked."
          : $"{context.OutstandingCount} appointments remain to be rebooked.";

      private static EmailMessage CancellationMessage(EmailContext context)
      {
          var types = string.Join(", ", context.TypeCodes.OrderBy(code => code, StringComparer.Ordinal));
          var ending = context.ReplacementCreated
              ? "a replacement has been created for you."
              : "no replacement could be created; please contact us for help.";
          var lines = new List<string>
          {
              $"Your booking for {types} was cancelled.",
              context.LocationName,
              context.LocationAddress,
              context.WindowText,
              $"Good news or bad news first: {ending}",
              $"Contact: {context.CoordinatorContact}",
          };
          return new EmailMessage(Guid.Empty, string.Empty, string.Empty,
              EmailTemplate.EventCancelledRebookingNeeded,
              $"Cancelled: {types}", string.Join("\n", lines), string.Join("\n", lines));
      }

      private static EmailMessage ConfirmationMessage(EmailContext context)
      {
          var types = string.Join(", ", context.TypeCodes.OrderBy(code => code, StringComparer.Ordinal));
          var lines = new List<string>
          {
              $"Your booking for {types} is confirmed.",
              context.LocationName,
              context.LocationAddress,
              context.WindowText,
              $"Contact: {context.CoordinatorContact}",
          };
          return new EmailMessage(Guid.Empty, string.Empty, string.Empty,
              EmailTemplate.BookingConfirmation,
              $"Confirmed: {types}", string.Join("\n", lines), string.Join("\n", lines));
      }
  }
  ```

  The golden tests pin the load-bearing lines (`BST`, location name and address, sorted
  types, singular/plural recovery, both cancellation endings, HTML mirroring text,
  unknown-template refusal). EmailMessage is the existing port record; the dispatcher
  fills recipient, link and delivery id per row. FormatWindow produces the WindowText
  shape the goldens assert (`Tue 14 Jul 2026, 09:30-11:00 BST at London HQ`); the view
  handlers call it for their preview lines.

  ```csharp
  // src/EventBooking.Infrastructure/Email/ClaimQuery.cs (complete)
  using Microsoft.EntityFrameworkCore;

  namespace EventBooking.Infrastructure.Email;

  public static class ClaimQuery
  {
      public const int BatchSize = 20;
      public static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(5);
      public const int MaxClaims = 3;

      // Claims up to 20 pending rows whose claim expired or was never taken and whose
      // backoff has passed, oldest first, skipping rows locked by another dispatcher.
      public const string Sql = """
          UPDATE email_log SET claimed_at = @now, claim_count = claim_count + 1,
              correlation_id = @correlationId
           WHERE id IN (
              SELECT id FROM email_log
               WHERE status = 3
                 AND (claimed_at IS NULL OR claimed_at < @now - make_interval(mins => 5))
                 AND (not_before IS NULL OR not_before <= @now)
               ORDER BY id LIMIT 20 FOR UPDATE SKIP LOCKED)
          RETURNING id;
          """;
  }
  ```

  `status = 3` is the stored `Pending` value and `make_interval(mins => 5)` the 5-minute
  lease — verify both literals against the migration and the enum before accepting; adjust
  the SQL (not the bounds) if they differ. The backoff (`not_before`) is set on each
  transient failure as `now + 2 ^ claim_count minutes`, capped at one hour.

  ```csharp
  // src/EventBooking.Infrastructure/Email/OutboxDispatcher.cs (complete, single pass plus loop)
  using EventBooking.Application.Abstractions;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Notifications;
  using EventBooking.Infrastructure.Persistence;
  using Npgsql;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.Extensions.Hosting;
  using Microsoft.Extensions.Logging;

  namespace EventBooking.Infrastructure.Email;

  public sealed class OutboxDispatcher(
      IServiceScopeFactory scopes,
      IEmailTransport transport,
      ITokenService tokens,
      PortalLinkOptions links,
      ILogger<OutboxDispatcher> logger) : BackgroundService
  {
      public async Task<int> DispatchOnceAsync(CancellationToken ct)
      {
          using var scope = scopes.CreateScope();
          var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
          var audit = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
          var clock = scope.ServiceProvider.GetRequiredService<IClock>();
          var now = clock.UtcNow;
          var claimed = await ClaimAsync(context, Guid.NewGuid().ToString(), now, ct);
          var sent = 0;
          foreach (var row in claimed)
          {
              try
              {
                  await SendRowAsync(scope, row, now, ct);
                  sent++;
              }
              catch (Exception ex)
              {
                  logger.LogError(ex, "Outbox send failed for delivery {DeliveryId}.", row.Id);
                  await MarkTransientAsync(scope, row, now, ct);
              }
          }

          return sent;
      }

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
          using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
          while (!stoppingToken.IsCancellationRequested)
          {
              try
              {
                  await DispatchOnceAsync(stoppingToken);
              }
              catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
              {
                  break;
              }
              catch (Exception ex)
              {
                  logger.LogError(ex, "Outbox dispatch pass failed.");
              }

              if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
          }
      }

      private static async Task<List<EmailLog>> ClaimAsync(
          EventBookingDbContext context, string correlationId, DateTimeOffset now, CancellationToken ct)
      {
          var ids = await context.Database
              .SqlQueryRaw<Guid>(ClaimQuery.Sql,
                  new NpgsqlParameter("@now", now),
                  new NpgsqlParameter("@correlationId", correlationId))
              .ToListAsync(ct);
          return await context.EmailLogs.Where(e => ids.Contains(e.Id)).ToListAsync(ct);
      }

      private async Task SendRowAsync(IServiceScope scope, EmailLog row, DateTimeOffset now, CancellationToken ct)
      {
          var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
          var audit = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
          var message = await RenderAsync(scope, row, ct);
          using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
          using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
          EmailSendOutcome outcome;
          try
          {
              outcome = await transport.SendAsync(
                  message.ToAddress, message.Subject, message.TextBody, message.HtmlBody, linked.Token);
          }
          catch (OperationCanceledException) when (timeout.IsCancellationRequested)
          {
              outcome = EmailSendOutcome.TransientFailure;
          }

          if (outcome == EmailSendOutcome.Sent)
          {
              row.MarkSent(now);
              if (row.TemplateName is EmailTemplate.AttendeeInvite or EmailTemplate.AttendeeReinvite)
                  audit.Record(AuditEntityTypes.Invite, row.InviteId!.Value, AuditAction.InviteSent,
                      ActorType.System, null, $"invite {row.InviteId}");
          }
          else if (outcome == EmailSendOutcome.TransientFailure)
          {
              if (row.ClaimCount >= ClaimQuery.MaxClaims)
                  row.MarkFailed(now);
              else
                  row.SetNotBefore(now.AddMinutes(Math.Min(60, 1 << row.ClaimCount)));
          }
          else
          {
              row.MarkFailed(now);
          }

          await context.SaveChangesAsync(ct);
      }

      private async Task MarkTransientAsync(
          IServiceScope scope, EmailLog row, DateTimeOffset now, CancellationToken ct)
      {
          var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
          if (row.ClaimCount >= ClaimQuery.MaxClaims)
              row.MarkFailed(now);
          else
              row.SetNotBefore(now.AddMinutes(Math.Min(60, 1 << row.ClaimCount)));
          await context.SaveChangesAsync(ct);
      }
  }
  ```

  RenderAsync loads the attendee (address, name), the invite/booking/event context rows,
  the type codes in code order and the location window text, regenerates the book link from
  the entity id and token version through the token service, and calls Compose — all
  reads, no writes. SetNotBefore is a new aggregate method beside TryClaim
  (ClaimedAt/ClaimCount already exist; `not_before`/`correlation_id` come from the
  Task 18 migration). PortalLinkOptions carries the public web origin and coordinator
  contact — reuse the ported attendee portal options if it already carries both values
  (verify against the Api configuration before accepting; adjust the reads, not the shape).
  The transport interface becomes outcome-returning:

  ```csharp
  // IEmailTransport.cs + SmtpEmailTransport.cs: SendAsync(recipient, subject, textBody,
  // htmlBody, ct) returning EmailSendOutcome. SmtpClient exceptions map to
  // TransientFailure except authentication and mailbox syntax errors (permanent); the
  // 30-second timeout above also lands transient. The logging sender used in tests
  // implements the same contract from a settable outcome.
  ```

  ```csharp
  // src/EventBooking.Application/Notifications/RetryEmailHandler.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Common;
  using EventBooking.Domain.Notifications;

  namespace EventBooking.Application.Notifications;

  public sealed class RetryEmailHandler(
      IStaffAccessProfileRepository profiles,
      IEmailDeliveryRepository deliveries,
      IStaffAccessAuthorizer access,
      IUnitOfWork unitOfWork,
      IAuditLogger audit,
      IClock clock)
  {
      public async Task<Result<RetryEmailOutcome>> HandleAsync(
          RetryEmailCommand command, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(
              command.StaffUserId, StaffCapability.ManageAttendees, null, ct);
          if (authorized.IsFailure) return Result<RetryEmailOutcome>.Failure(authorized.Error);

          await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
          var old = await deliveries.LockForUpdateAsync(command.EmailLogId, ct);
          if (old is null || old.AttendeeId != command.AttendeeId)
              return Result<RetryEmailOutcome>.Failure(Error.NotFound("No such delivery."));
          if (old.Status != EmailStatus.Failed)
              return Result<RetryEmailOutcome>.Failure(
                  Error.Conflict($"Only a failed delivery can be retried, not {old.Status}."));

          var fresh = EmailLog.RecordPending(Guid.NewGuid(), old.AttendeeId, old.TemplateName,
              clock.UtcNow, old.InviteId, old.BookingId, old.EventId);
          deliveries.Add(fresh);
          old.MarkResolved(clock.UtcNow);
          await unitOfWork.SaveChangesAsync(ct);
          await transaction.CommitAsync(ct);
          return Result<RetryEmailOutcome>.Success(new RetryEmailOutcome(fresh.Id));
      }
  }

  public sealed record RetryEmailCommand(Guid StaffUserId, Guid AttendeeId, Guid EmailLogId);
  public sealed record RetryEmailOutcome(Guid EmailLogId);
  ```

  The retry test constructs the handler positionally as
  `(profiles, deliveries, access, unitOfWork, audit, clock)` — update its two constructions
  to pass the profiles repository twice (it implements the authorizer, as in Tasks 12–17):
  `new RetryEmailHandler(profiles, deliveries, profiles, unitOfWork, audit, new FakeClock())`.
  The resent email carries the same link because links regenerate from entity id and token
  version, which the retry does not touch.

- [ ] **Step 4: Run.** Expected: PASS — the new suites plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  Expect Infrastructure and Application counts to move (composer goldens replace the
  ported composer suite; dispatcher and retry suites are new). A count that does not match
  the executor's own before/after diff is a signal to read the diff, not to adjust the
  number.

- [ ] **Step 5: Commit and push** the executor's code — not the plan documents — under the
  master plan's message:

  ```bash
  test -z "$(git status --porcelain --ignored=no | grep -v '^??')"
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  git add src/EventBooking.Infrastructure/Email/ src/EventBooking.Application/Notifications/ src/EventBooking.Application/Bookings/ViewBookingHandler.cs src/EventBooking.Application/Bookings/ViewInviteHandler.cs src/EventBooking.Infrastructure/Persistence/Migrations/ tests/EventBooking.Infrastructure.Tests/Email/ tests/EventBooking.Application.Tests/Notifications/
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "feat(email): durable outbox dispatcher and location-aware templates

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```