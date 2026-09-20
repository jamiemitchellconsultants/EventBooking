# 03g — Durable outbox dispatcher and location-aware templates (Task 18)

[← Phase overview](phase-3-application.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 17. Email becomes a durable outbox: commands stage pending rows with
the business change, a hosted dispatcher claims and sends them, and the composer renders all
four templates with location-aware windows. This is decision D13 in code.

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
time. The head-office wording retires, replaced with location details.

**Architecture:** No email is sent inside a transaction: commands insert a pending row and
commit it with the business change, and the dispatcher wakes every 5 seconds or on an
in-process signal after a commit. Two dispatcher instances against one database never send the
same row twice (skip-locked claims). A resent email carries the same link as the original
(deterministic regeneration). `EmailLog` rows never contain an address, name, token or URL —
assert over all columns. No domain entity leaves the handler.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers.

**Spec:** [Master Task 18](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[functional requirements](../design/02-functional-requirements.md),
[solution architecture](../design/04-solution-architecture.md), [ontology](../ontology.md).

## Outbox schema (contradiction #6, settled with the user)

The dispatcher needs attempt, backoff and correlation state the current `EmailLog` does not
carry, so Task 18 extends the schema: a claim-count column, a next-attempt-not-before column
for backoff, and a correlation id propagated from the request's structured log. The migration
backfills existing rows as never-claimed and drops no defaults new rows must state. Remove any
in-transaction send left from the port.

## Delivery promise (contradiction #7, settled with the user)

The design promises at-least-once delivery, not exactly-once: an SMTP crash after send but
before marking sent means the reclaimed row sends again. The document states the crash window
explicitly, and the idempotency that makes a duplicate harmless is the deterministic link
(same entity id and version regenerates the same URL) plus the recipient-visible duplicate
being a re-send of identical content.

## Global constraints

Composer golden tests cover all four templates, including the recovery singular and plural
wording and both `EventCancelledRebookingNeeded` endings; a London window in July renders
"BST"; types render in code order regardless of input order; the HTML part carries the same
lines. A row claimed 6 minutes ago by a crashed dispatcher is reclaimed. Transient failures
three times end `Failed` with no error text stored.

## Review focus

STOP AND CHECK four things. The two-dispatcher test proves no double-send against one
database. The reclaim test uses a 6-minute-old claim (past the 5-minute line). The no-personal-data
test scans every column of every `EmailLog` row. And the golden files carry the location name,
address, local time and zone — never the retired head-office wording.

### Task 18: Notification outbox and templates

**Files:**

- Create: src/EventBooking.Infrastructure/Email/OutboxDispatcher.cs
- Create: src/EventBooking.Infrastructure/Email/EmailComposer.cs
- Modify: src/EventBooking.Infrastructure/Email/SmtpEmailSender.cs (outcomes, timeout)
- Modify: src/EventBooking.Application/Notifications/RetryEmailHandler.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/ (outbox columns)
- Delete: any in-transaction send left from the port
- Test: tests/EventBooking.Infrastructure.Tests/Email/OutboxDispatcherTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/Email/EmailComposerTests.cs
- Test: tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them
after the failing test, not before.

```csharp
namespace EventBooking.Infrastructure.Email;

// The sender never throws for a provider error: every outcome is a value.
// A 30-second timeout counts as a transient failure.
public enum EmailSendOutcome { Sent, TransientFailure, PermanentFailure }

public interface IEmailSenderPort
{
    Task<EmailSendOutcome> SendAsync(
        string recipient,      // never stored; passed through only
        string subject,
        string textBody,
        string htmlBody,
        CancellationToken ct);
}

// Pure rendering: (template, context) to (subject, text, HTML). Windows
// render in the location's zone with its abbreviation; types sort in code
// order; links are not rendered here — the dispatcher regenerates them
// from the row's entity id and token version at send time.
public static class EmailComposer
{
    public static EmailMessage Compose(
        string template,       // AttendeeInvite | AttendeeReinvite |
                               // EventCancelledRebookingNeeded | BookingConfirmation
        EmailContext context);
}

public sealed record EmailContext(
    IReadOnlyList<string> TypeCodes,
    string LocationName,
    string LocationAddress,
    string WindowText,         // e.g. "Tue 14 Oct 2026, 09:30-11:00 BST"
    string BookUrl,
    string CoordinatorContact,
    bool ReplacementCreated,   // EventCancelledRebookingNeeded ending
    bool IsRecovery,
    int OutstandingCount);     // recovery singular/plural wording
```

```csharp
namespace EventBooking.Application.Notifications;

// A staff retry creates a new pending row and marks the old one Resolved;
// the resent email carries the same link as the original.
public sealed record RetryEmailCommand(
    Guid StaffUserId,
    Guid AttendeeId,
    Guid EmailLogId);
```

- [ ] **Step 1: Write the failing tests.** Create the three test files. Required cases, one
  test per rule:

  ```csharp
  // tests/EventBooking.Infrastructure.Tests/Email/EmailComposerTests.cs
  // (representative file — the dispatcher and retry suites follow the
  // same shape against real PostgreSQL and fake ports respectively)
  using EventBooking.Infrastructure.Email;

  namespace EventBooking.Infrastructure.Tests.Email;

  public sealed class EmailComposerTests
  {
      // Golden test: a London window in July renders BST, types sort in
      // code order regardless of input order, and the HTML part carries
      // the same lines as the text part.
      [Fact]
      public void Invite_renders_zone_abbreviation_and_sorted_types()
      {
          var context = ComposerFixture.LondonJulyContext(
              typeCodes: ["IND", "FIT", "MED"]);

          var message = EmailComposer.Compose("AttendeeInvite", context);

          Assert.Contains("BST", message.TextBody);
          Assert.True(message.TypeLineIs("FIT, IND, MED"));
          Assert.Equal(message.TextLines, message.HtmlLines);
      }

      // Recovery singular vs plural wording.
      [Theory]
      [InlineData(1, "appointment remains")]
      [InlineData(2, "appointments remain")]
      public void Recovery_wording_follows_count(int count, string expected)
      {
          var context = ComposerFixture.RecoveryContext(count);

          var message = EmailComposer.Compose("AttendeeInvite", context);

          Assert.Contains(expected, message.TextBody);
      }
  }
  ```

  The remaining suites cover: both `EventCancelledRebookingNeeded` endings (replacement
  created or not); two dispatcher instances against one database never sending the same row
  twice; reclaim of a 6-minute-old claim; transient-times-three ending `Failed` with no
  error text stored; a resent email carrying the same link as the original; `EmailLog`
  rows never containing an address, name, token or URL across all columns.

- [ ] **Step 2: Run.** Expected: FAIL — the dispatcher and composer do not exist.

  ```bash
  dotnet test tests/EventBooking.Infrastructure.Tests --filter "FullyQualifiedName~Email"
  ```

- [ ] **Step 3: Implement.** Create the dispatcher, composer and migration; rework the SMTP
  sender to outcomes with a 30-second timeout; implement the retry handler. Remove any
  in-transaction send left from the port. Read the generated migration before accepting it;
  backfill existing rows as never-claimed.

- [ ] **Step 4: Run.** Expected: PASS — the new suites plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  Expect Infrastructure and Application counts to rise. A count that does not match after
  the change is a signal to read the diff, not to adjust the number.

- [ ] **Step 5: Commit and push.**

  ```bash
  test -z "$(git status --porcelain --ignored=no | grep -v '^??')"
  node --input-type=module <<'LINT_PLANS'
  import fs from 'node:fs';
  import {execFileSync} from 'node:child_process';
  const directory = 'docs/detailed-implementations';
  const files = fs.readdirSync(directory).filter(name => name.endsWith('.md')).map(name => directory + '/' + name);
  process.stdout.write(execFileSync('node', ['scripts/check-ontology-terms.mjs', '--also', ...files], {encoding:'utf8', maxBuffer:1e7}));
  LINT_PLANS
  git add docs/detailed-implementations/phase-3g-notification-outbox.md docs/detailed-implementations/phase-3-application.md docs/detailed-implementations/HANDOVER.md
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "docs(plans): Task 18 notification outbox and templates

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```
