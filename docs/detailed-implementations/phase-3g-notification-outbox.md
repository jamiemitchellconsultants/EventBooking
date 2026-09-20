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
  from the new composer)
- Modify: src/EventBooking.Application/Bookings/ViewInviteHandler.cs (same)
- Delete: src/EventBooking.Application/Notifications/AttendeeEmailComposer.cs
- Delete: src/EventBooking.Application/Notifications/EmailDeliveryService.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/<generated-timestamp>_EmailOutboxColumns.cs
  (plus its Designer; the timestamp prefix comes from generation)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs
- Modify: tests/EventBooking.Application.Tests/Notifications/AttendeeEmailComposerTests.cs
  (deleted; replaced by the composer golden suite below — delete the file, do not edit it)
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
              profiles, deliveries, unitOfWork, audit, new FakeClock());

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
              profiles, deliveries, new FakeUnitOfWork(), new RecordingAuditLogger(), new FakeClock());

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

- [ ] **Step 3: Implement.** Create the composer (render exactly the golden lines; types
  sorted in code order; windows via the location zone with abbreviation; HTML mirroring
  text), the claim query (up to 20 rows, `FOR UPDATE SKIP LOCKED`, reclaiming claims older
  than 5 minutes, backoff through the new not-before column, correlation id written at
  claim time), the dispatcher (one DispatchOnceAsync pass plus the 5-second hosted loop
  with the post-commit signal; `InviteSent` audit for invite templates; transient stays
  pending to 3 claims then `Failed`; permanent fails at once; links regenerated from
  entity id and token version), and the retry handler (new pending row, old row
  `Resolved`). Rework the SMTP transport to outcomes with a 30-second timeout. Retire the
  ported delivery service and composer; move the view handlers to the new window
  formatter. Read the generated migration before accepting it: backfill existing rows as
  never-claimed, then drop the new column defaults.

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