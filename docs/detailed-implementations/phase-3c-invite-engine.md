# 03c — Location-restricted invite engine (Task 14)

[← Phase overview](phase-3-application.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 13. The ported invite issuer, trigger-invite, expire-invites and option
top-up logic become the generalised invite engine: every path that creates an invite goes
through one internal issuer, options come from the Task 11 eligibility port in UTC order, and
the invite carries the location set the Coordinator selected.

> Use superpowers:executing-plans. This task is hand-authored: complete code and complete tests
> are written straight into this document, with no prototype. Compile and test-drive them
> yourself. The test counts below are what you should expect to reach, not figures observed by
> the author — nothing here has been run.

**Goal:** InviteAttendee (attendee id, location ids) returning `Invited` or
`AwaitingAvailability` (`insufficient-events`); CountEligibleEventsForAttendee (attendee id,
location ids) for the dialog; one internal InviteIssuer used by every path that creates an
invite (initial, re-invite, reissue after expiry, top-up, replacement after cancellation,
recovery); ExpireInvites as a sweep step run per item; TopUpOptions called on view and on
confirm. The issuer always supersedes the journey's pending invite, snapshots the Task 12
settings values onto the new invite, and stages the right template (`AttendeeInvite` for an
initial invite, `AttendeeReinvite` for a reissue) as one pending outbox row in the transaction.

**Architecture:** One transaction per command; the attendee lock is taken first (ascending id
order where several are touched), then the invite row where one exists. The issuer never saves
and never sends — the caller owns the unit of work and the outbox row is committed with the
business change; the Task 18 dispatcher sends it. Expiry reissues with the same location set
and `retryCount + 1` while retries remain, else moves the attendee to `NoResponseNeedsFollowUp`
with a reason in the audit. The fixed three-option count retires: the option count comes from
the snapshotted `inviteOptionCount`. The transitional-location restriction retires: the
location set comes from the command. No domain entity leaves the handler.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers.

**Spec:** [Master Task 14](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[domain model](../design/01-domain-model.md), [functional requirements](../design/02-functional-requirements.md),
[solution architecture](../design/04-solution-architecture.md), [ontology](../ontology.md).

## Boundary

Task 14 owns the invite engine only. The ported delivery-service path (immediate send inside
the issuing call) retires: issuing stages a pending outbox row and the Task 18 dispatcher
sends it. Recovery paths keep their Task 8 factories; StartRecovery stays with Task 16 — this
task's issuer only exposes the recovery-issue operation it calls. Event-cancellation
replacement (Task 15) calls the same issuer; this task defines the operation it will call but
does not implement cancellation.

## Global constraints

One transaction per command; attendee lock first; audit in the transaction; no domain entity
leaves the handler; exactly one StaffCapability per handler (`ManageAttendees` for the
coordinator trigger, `System` actor for the sweep step). An inactive location in the selection
is refused. Expiry works while the same invite is already tracked in the unit of work (load by
id; do not attach a second copy). Valid settings never alter existing invites — the issuer
reads settings only to snapshot them onto the new row.

## Review focus

STOP AND CHECK four things. The option count is the snapshotted settings value, not a constant
— inviting with 3 eligible events and option count 3 creates 3 options in UTC order with
`expiresAt` equal to now plus `inviteExpiryDays`. With 2 eligible events there is no invite,
the status is `AwaitingAvailability`, and the outcome is `insufficient-events`. Expiry at the
retry limit — and a failed reissue — both yield `NoResponseNeedsFollowUp`, the latter with a
reason in the audit. And top-up excludes events already offered and the just-cancelled
booking's event, audits `InviteOptionReplaced`, and sets `NoResponseNeedsFollowUp` when still
short.

### Task 14: Location-restricted invite engine

**Files:**

- Create: src/EventBooking.Application/Invites/InviteAttendeeHandler.cs
- Create: src/EventBooking.Application/Invites/CountEligibleEventsHandler.cs
- Create: src/EventBooking.Application/Invites/InviteIssuer.cs
- Create: src/EventBooking.Application/Invites/ExpireInvitesHandler.cs
- Create: src/EventBooking.Application/Invites/TopUpInviteOptionsHandler.cs
- Delete: the ported trigger-invite, expire-invites and top-up files they replace
- Test: tests/EventBooking.Application.Tests/Invites/InviteAttendeeHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Invites/TopUpInviteOptionsHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them
after the failing test, not before.

```csharp
namespace EventBooking.Application.Invites;

// Coordinators name the locations; options are drawn only from events at
// those locations. Location ids are de-duplicated; an unknown or inactive
// location is refused and nothing is created.
public sealed record InviteAttendeeCommand(
    Guid StaffUserId,
    Guid AttendeeId,
    IReadOnlyList<Guid> LocationIds); // 1-50, distinct

public sealed record InviteAttendeeOutcome(
    Guid AttendeeId,
    string Status,      // "Invited" or "AwaitingAvailability"
    Guid? InviteId);    // set only when an invite was created

public sealed record CountEligibleEventsQuery(
    Guid StaffUserId,
    Guid AttendeeId,
    IReadOnlyList<Guid> LocationIds);

// The one place invites are created. Every path — initial, re-invite,
// reissue after expiry, top-up replacement, cancellation replacement,
// recovery — enters here. It supersedes the journey's pending invite,
// snapshots the current settings onto the new row, selects options in UTC
// order through the eligibility port, stages the template's pending outbox
// row, and returns the new invite. It never saves and never sends.
public interface IInviteIssuer
{
    // Issues for the attendee's current requirements at the given
    // locations; reissueOf names the expired invite being replaced, if any.
    // Returns insufficient-events instead of an invite when options fall
    // short, parking the attendee per the closed status table.
    Task<Result<InviteIssueOutcome>> IssueAsync(
        AttendeeJourney journey,   // locked attendee + requirement snapshot
        IReadOnlyList<Guid> locationIds,
        Guid? reissueOf,
        string template,           // AttendeeInvite or AttendeeReinvite
        string actorType,          // Staff, System
        string? actorId,
        CancellationToken ct);
}

public sealed record InviteIssueOutcome(
    Guid InviteId,
    int OptionCount,
    DateTimeOffset ExpiresAt,
    int RetryCount);

// The sweep step. Each item runs in its own transaction (the Task 19 sweep
// calls it per item); a failed reissue still moves the attendee on.
public sealed record ExpireInviteCommand(
    Guid InviteId); // System actor; no staff capability demanded

// Replaces a filled option with the next eligible event, excluding events
// already offered and the just-cancelled booking's event.
public sealed record TopUpInviteOptionsCommand(
    Guid StaffUserId,
    Guid InviteId,
    Guid? ExcludeEventId);
```

- [ ] **Step 1: Write the failing tests.** Create the four Application test files. Required
  cases, one test per rule:

  ```csharp
  // tests/EventBooking.Application.Tests/Invites/InviteAttendeeHandlerTests.cs
  // (representative file — the other three follow the same shape)
  using EventBooking.Application.Common;
  using EventBooking.Application.Invites;

  namespace EventBooking.Application.Tests.Invites;

  public sealed class InviteAttendeeHandlerTests
  {
      // 3 eligible events and option count 3: the invite carries 3 options
      // in UTC order, the location set equals the selection, expiresAt is
      // now + inviteExpiryDays, audit InviteCreated, one pending
      // AttendeeInvite outbox row.
      [Fact]
      public async Task Invite_with_full_options_creates_invite_and_stages_email()
      {
          var fixture = InviteFixture.Create(optionCount: 3)
              .WithEligibleEvents(3);
          var command = new InviteAttendeeCommand(
              fixture.CoordinatorUserId, fixture.AttendeeId, fixture.LocationIds);

          var result = await fixture.InviteAsync(command, CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal("Invited", result.Value.Status);
          var invite = fixture.StoredInvite(result.Value.InviteId);
          Assert.Equal(
              fixture.EligibleEventIdsInUtcOrder,
              invite.OptionEventIds);
          Assert.Equal(fixture.LocationIds, invite.LocationIds);
          Assert.Equal(
              fixture.Clock.UtcNow.AddDays(7),
              invite.ExpiresAt);
          Assert.Equal(3, invite.SnapshottedOptionCount);
          Assert.Single(fixture.Outbox.PendingFor(invite.Id, "AttendeeInvite"));
      }

      // 2 eligible events: no invite, AwaitingAvailability,
      // insufficient-events.
      [Fact]
      public async Task Invite_short_of_options_parks_attendee()
      {
          var fixture = InviteFixture.Create(optionCount: 3)
              .WithEligibleEvents(2);
          var command = new InviteAttendeeCommand(
              fixture.CoordinatorUserId, fixture.AttendeeId, fixture.LocationIds);

          var result = await fixture.InviteAsync(command, CancellationToken.None);

          Assert.Equal("insufficient-events", result.Error.Type);
          Assert.Equal("AwaitingAvailability", fixture.StoredAttendee.Status);
          Assert.Empty(fixture.Outbox.Rows);
      }

      // An inactive location in the selection is refused and nothing changes.
      [Fact]
      public async Task Inactive_location_refuses_with_nothing_created()
      {
          var fixture = InviteFixture.Create(optionCount: 3)
              .WithInactiveLocation();
          var command = new InviteAttendeeCommand(
              fixture.CoordinatorUserId, fixture.AttendeeId, fixture.LocationIds);

          var result = await fixture.InviteAsync(command, CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Empty(fixture.Outbox.Rows);
      }
  }
  ```

  The remaining suites cover: re-invite from `NoResponseNeedsFollowUp` with new locations
  works (FR-5.9); expiry below the retry limit reissues with the same location set and
  `retryCount + 1` staging `AttendeeReinvite`; expiry at the limit yields
  `NoResponseNeedsFollowUp`; a failed reissue also yields `NoResponseNeedsFollowUp` with a
  reason in the audit (FR-5.7); the expiry transition works while the same invite is already
  tracked in the unit of work; top-up replaces a filled option with the next eligible event,
  audits `InviteOptionReplaced`, excludes already-offered events and the just-cancelled
  booking's event, and sets `NoResponseNeedsFollowUp` when still short.

- [ ] **Step 2: Run.** Expected: FAIL — the Invites handlers do not exist yet.

  ```bash
  dotnet test tests/EventBooking.Application.Tests --filter "FullyQualifiedName~Invites"
  ```

- [ ] **Step 3: Implement.** Create the five handler files; delete the ported files they
  replace. The issuer takes the attendee lock first where the caller has not already,
  supersedes the pending invite, snapshots settings, selects through the eligibility port in
  UTC order, writes the audit entry, stages the outbox row, and returns the DTO. Retire the
  fixed three-option constant and the transitional-location restriction.

- [ ] **Step 4: Run.** Expected: PASS — the new suites plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  Expect the Application count to rise (new invite suites) and Infrastructure to stay flat.
  A count that does not match after the change is a signal to read the diff, not to adjust
  the number.

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
  git add docs/detailed-implementations/phase-3c-invite-engine.md docs/detailed-implementations/phase-3-application.md docs/detailed-implementations/HANDOVER.md
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "docs(plans): Task 14 location-restricted invite engine

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```
