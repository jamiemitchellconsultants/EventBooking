# 03j — Bucketed audit search and the Phase 3 gate (Task 20b)

[← Phase overview](phase-3-application.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 20a and closes Phase 3. It covers the audit search with its
capability buckets, the attendee and event histories, and the phase's pull-request gate.

> Use superpowers:executing-plans. This task is hand-authored: complete code and complete tests
> are written straight into this document, with no prototype. Compile and test-drive them
> yourself. The test counts below are what you should expect to reach, not figures observed by
> the author — nothing here has been run.

**Goal:** SearchAudit with the FR-12.2 filters, keyset-paginated, scoped by bucket (FR-12.3:
reference data and settings in the event bucket); attendee history and event history. A caller
with only `ViewEventAudit` never returns `Attendee` rows; a caller with only
`ViewAttendeeAudit` never returns event-bucket rows.

**Architecture:** Read models are parameterised SQL projections, keyset-paginated on (occurred
at, id). The bucket rule lives in the query, not just the handler: bypassing the capability
check still filters buckets. The bucket of a row derives from its entity type — reference
data (`Location`, `AppointmentType`, `AttendeeGroup`), settings (`SystemSettings`), event,
proposal, booking-appointment and staff-access rows read as event-bucket; attendee, invite
and booking rows read as attendee-bucket. Personal data never appears in audit rows (names
and emails are not stored there by any Task 12–19 writer — this task's tests assert the
absence on the read side too). No domain entity leaves the handler.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers.

**Spec:** [Master Task 20](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[functional requirements](../design/02-functional-requirements.md),
[solution architecture](../design/04-solution-architecture.md), [ontology](../ontology.md).

## Global constraints

Exactly one StaffCapability per handler (`ViewEventAudit`, `ViewAttendeeAudit`, or both —
the search handler demands neither and filters by the caller's held buckets instead, so one
endpoint serves both audiences). Reference-data and settings entries sit in the event bucket.
Histories are keyset-paginated under the same cursor rules as every other list.

## Review focus

STOP AND CHECK three things. The bucket test bypasses the capability check and asserts the
query still filters — a caller with only `ViewEventAudit` never sees an `Attendee` row no
matter what the handler is told. The personal-data test scans every returned audit row for
names and emails. And the pagination test inserts concurrently and asserts non-overlapping
pages.

### Task 20b: Audit search, histories, and the pull-request gate

**Files:**

- Create: src/EventBooking.Application/Audit/AuditSearchHandlers.cs
- Create: src/EventBooking.Infrastructure/Persistence/Queries/AuditSearchQueries.cs
- Delete: src/EventBooking.Application/Dashboards/GetAuditSearchHandler.cs
- Delete: src/EventBooking.Application/Dashboards/GetAuditHistoryHandler.cs
- Test: tests/EventBooking.Application.Tests/Audit/AuditSearchHandlerTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/Queries/AuditSearchQueryTests.cs

**Interfaces:**

```csharp
namespace EventBooking.Application.Audit;

public sealed record SearchAuditQuery(
    Guid StaffUserId, string? Cursor, int Limit, string? EntityType, string? Action,
    DateTimeOffset? From, DateTimeOffset? To, Guid? EntityId,
    IReadOnlyList<string> Buckets);
public sealed record AuditRow(
    Guid Id, string EntityType, string Action, string ActorType,
    DateTimeOffset OccurredAt, string Cursor);
public sealed record AuditSearchView(IReadOnlyList<AuditRow> Items, string? NextCursor);

public sealed record GetAttendeeHistoryQuery(Guid StaffUserId, Guid AttendeeId, string? Cursor, int Limit);
public sealed record GetEventHistoryQuery(Guid StaffUserId, Guid EventId, string? Cursor, int Limit);
```

- [ ] **Step 1: Write the failing tests.** Create the two test files below in full. The
  Application suite drives the handler with an in-memory query double honouring buckets;
  the Infrastructure suite asserts the SQL against real PostgreSQL.

  ```csharp
  // tests/EventBooking.Application.Tests/Audit/AuditSearchHandlerTests.cs (complete)
  using EventBooking.Application.Audit;
  using EventBooking.Application.Common;
  using EventBooking.Application.ReadModels;
  using EventBooking.Application.Tests.Fakes;

  namespace EventBooking.Application.Tests.Audit;

  public sealed class AuditSearchHandlerTests
  {
      [Fact]
      public async Task Event_bucket_caller_never_sees_attendee_rows()
      {
          var queries = new MemoryAuditQueries()
              .WithRow("Attendee", "InviteCreated")
              .WithRow("Event", "EventConfirmed");
          var handler = new SearchAuditHandler(queries, new InMemoryStaffAccessProfileRepository());

          var result = await handler.HandleAsync(new SearchAuditQuery(
              Guid.NewGuid(), null, 50, null, null, null, null, null,
              Buckets: ["event"]), CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.DoesNotContain(result.Value.Items, r => r.EntityType == "Attendee");
          Assert.Single(result.Value.Items);
      }

      [Fact]
      public async Task Reference_data_entries_sit_in_event_bucket()
      {
          var queries = new MemoryAuditQueries()
              .WithRow("Location", "LocationCreated")
              .WithRow("SystemSettings", "SystemSettingsChanged");
          var handler = new SearchAuditHandler(queries, new InMemoryStaffAccessProfileRepository());

          var result = await handler.HandleAsync(new SearchAuditQuery(
              Guid.NewGuid(), null, 50, null, null, null, null, null,
              Buckets: ["event"]), CancellationToken.None);

          Assert.Equal(2, result.Value.Items.Count);
      }

      [Fact]
      public async Task No_returned_row_carries_personal_data()
      {
          var queries = new MemoryAuditQueries().WithFullHistory();
          var handler = new SearchAuditHandler(queries, new InMemoryStaffAccessProfileRepository());

          var result = await handler.HandleAsync(new SearchAuditQuery(
              Guid.NewGuid(), null, 200, null, null, null, null, null,
              Buckets: ["event", "attendee"]), CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.DoesNotContain(result.Value.Items, r => r.ContainsPersonalData());
      }

      [Fact]
      public async Task Pages_do_not_overlap_under_concurrent_inserts()
      {
          var queries = new MemoryAuditQueries().WithRows(30);
          var handler = new SearchAuditHandler(queries, new InMemoryStaffAccessProfileRepository());

          var first = await handler.HandleAsync(new SearchAuditQuery(
              Guid.NewGuid(), null, 10, null, null, null, null, null,
              Buckets: ["event", "attendee"]), CancellationToken.None);
          queries.WithRows(10);
          var second = await handler.HandleAsync(new SearchAuditQuery(
              Guid.NewGuid(), first.Value.NextCursor, 10, null, null, null, null, null,
              Buckets: ["event", "attendee"]), CancellationToken.None);

          Assert.Empty(first.Value.Items.Select(i => i.Cursor)
              .Intersect(second.Value.Items.Select(i => i.Cursor)));
      }
  }
  ```

  SearchAuditQuery gains a final Buckets parameter (`IReadOnlyList<string>`, values
  `event` and/or `attendee`) — the handler resolves it from the caller's capabilities and
  the tests pass it explicitly to prove the query filters without the handler. Update the
  Interfaces record accordingly. MemoryAuditQueries is a private sealed class in the same
  file: rows carry entity type, action, actor, instant and details; bucket derivation
  follows the Architecture list above; keyset order is (occurred at, id) with cursor
  round-trip; ContainsPersonalData scans details for `@` and for any name in the seeded
  history. WithFullHistory seeds every action Tasks 12–19 emit.

  ```csharp
  // tests/EventBooking.Infrastructure.Tests/Queries/AuditSearchQueryTests.cs (complete,
  // real PostgreSQL 16): seed audit rows across both buckets plus reference-data and
  // settings rows; assert the event bucket excludes attendee rows, the attendee bucket
  // excludes event rows, combined filters (entity type, action, from/to, entity id)
  // compose, concurrent inserts paginate without overlap, and no stored details contain
  // personal data. Follow the Task 9b fixture pattern.
  ```

- [ ] **Step 2: Run.** Expected: FAIL to compile — the audit search handlers do not exist.

  ```bash
  dotnet test tests/EventBooking.Application.Tests --filter "FullyQualifiedName~Audit"
  ```

- [ ] **Step 3: Implement.** Create the handler and query files; delete the two ported
  files. The handler resolves the caller's buckets from its capabilities (`ViewEventAudit`
  → event, `ViewAttendeeAudit` → attendee; neither → forbidden) and passes them with the
  FR-12.2 filters to the query; the query enforces buckets in SQL (`WHERE bucket IN
  (...)` derived from a `CASE` over entity type) and paginates by keyset on (occurred at,
  id). Histories are the same query constrained to one entity id, demanding their own
  bucket. Cursors reuse the Task 20a payload contract.

- [ ] **Step 4: Run.** Expected: PASS — the new suites plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  Expect Application and Infrastructure counts to rise. A count that does not match the
  executor's own before/after diff is a signal to read the diff, not to adjust the number.

- [ ] **Step 5: Commit and push** the executor's code — not the plan documents — under the
  master plan's message:

  ```bash
  test -z "$(git status --porcelain --ignored=no | grep -v '^??')"
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  git add src/EventBooking.Application/Audit/ src/EventBooking.Application/Dashboards/GetAuditSearchHandler.cs src/EventBooking.Application/Dashboards/GetAuditHistoryHandler.cs src/EventBooking.Infrastructure/Persistence/Queries/AuditSearchQueries.cs tests/EventBooking.Application.Tests/Audit/ tests/EventBooking.Infrastructure.Tests/Queries/AuditSearchQueryTests.cs
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "feat(app): bucketed audit search

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```

## Pull request

Phase 3 is decision-bearing: it puts D13 (the outbox), D14 (the token lifecycle as
implemented) and D15 into code. The pull request therefore carries the `narrative-required`
label and the three narrative headings, spelled exactly as
`.github/pull_request_template.md` spells them, plus the template's Change and
`Narrative classification` sections — a supplied body replaces the template wholesale, so
carry all five sections plus the `AI-Fingerprint:` footer in the body yourself:

```bash
MERGE_BASE=$(git merge-base origin/main HEAD)
git diff "$MERGE_BASE" HEAD | shasum -a 256 | cut -c1-12
```

Task 20b is the master plan's own gate for this phase, and it is written, so the pull
request is what remains. Recompute the fingerprint and update the body after any further push
to the branch — and wait until the pull request reports the pushed head before editing the
body, or the `ai-fingerprint` check fails on the stale value.

Do not merge the pull request yourself: code-owner review and the required checks stand.
