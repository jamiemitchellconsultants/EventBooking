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

**Architecture:** Read models are parameterised SQL projections, keyset-paginated. The bucket
rule lives in the query, not just the handler: bypassing the capability check still filters
buckets. Personal data never appears in audit rows (names and emails are not stored there by
any Task 12–19 writer — this task's tests assert the absence on the read side too). No domain
entity leaves the handler.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers.

**Spec:** [Master Task 20](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[functional requirements](../design/02-functional-requirements.md),
[solution architecture](../design/04-solution-architecture.md), [ontology](../ontology.md).

## Global constraints

Exactly one StaffCapability per handler (`ViewEventAudit`, `ViewAttendeeAudit`, or both).
Reference-data and settings entries sit in the event bucket. Histories are keyset-paginated
under the same cursor rules as every other list.

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
- Delete: the ported audit-search files they replace
- Test: tests/EventBooking.Application.Tests/Audit/AuditSearchHandlerTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/Queries/AuditSearchQueryTests.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them
after the failing test, not before.

```csharp
namespace EventBooking.Application.Audit;

// FR-12.2 filters with FR-12.3 buckets. Reference data and settings entries
// read as event-bucket rows. The bucket rule is enforced in the query: a
// caller holding only one bucket never receives the other's rows.
public sealed record SearchAuditQuery(
    Guid StaffUserId,
    string? Cursor,
    int Limit,                     // 1-200, default 50
    string? EntityType,            // null means all within the caller's buckets
    string? Action,                // null means all
    DateTimeOffset? From,          // null means unbounded
    DateTimeOffset? To,            // null means unbounded
    Guid? EntityId);               // null means all

public sealed record AuditRow(
    Guid Id,
    string EntityType,
    string Action,
    string ActorType,
    DateTimeOffset OccurredAt,
    string Cursor);

public sealed record AuditSearchView(
    IReadOnlyList<AuditRow> Items,
    string? NextCursor);

// Histories are bucket-filtered the same way: the attendee history needs
// the attendee bucket, the event history the event bucket.
public sealed record GetAttendeeHistoryQuery(
    Guid StaffUserId,
    Guid AttendeeId,
    string? Cursor,
    int Limit);

public sealed record GetEventHistoryQuery(
    Guid StaffUserId,
    Guid EventId,
    string? Cursor,
    int Limit);
```

- [ ] **Step 1: Write the failing tests.** Create the two test files. Required cases, one
  test per rule:

  ```csharp
  // tests/EventBooking.Application.Tests/Audit/AuditSearchHandlerTests.cs
  // (representative file — the Infrastructure suite follows the same shape
  // against real PostgreSQL)
  using EventBooking.Application.Audit;
  using EventBooking.Application.Common;

  namespace EventBooking.Application.Tests.Audit;

  public sealed class AuditSearchHandlerTests
  {
      // A caller with only ViewEventAudit never returns Attendee rows, even
      // when the capability check is bypassed — the query filters buckets.
      [Fact]
      public async Task Event_bucket_caller_never_sees_attendee_rows()
      {
          var fixture = AuditFixture.Create()
              .BypassCapabilityCheck()
              .WithAttendeeRow()
              .WithEventRow();
          var query = new SearchAuditQuery(
              fixture.EventAuditorUserId, Cursor: null, Limit: 50,
              EntityType: null, Action: null,
              From: null, To: null, EntityId: null);

          var result = await fixture.SearchAsync(query, CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.DoesNotContain(result.Value.Items, r => r.EntityType == "Attendee");
      }

      // Reference-data and settings entries read as event-bucket rows.
      [Fact]
      public async Task Reference_data_entries_sit_in_event_bucket()
      {
          var fixture = AuditFixture.Create()
              .WithLocationCreatedRow()
              .WithSettingsChangedRow();
          var query = new SearchAuditQuery(
              fixture.EventAuditorUserId, Cursor: null, Limit: 50,
              EntityType: null, Action: null,
              From: null, To: null, EntityId: null);

          var result = await fixture.SearchAsync(query, CancellationToken.None);

          Assert.Equal(2, result.Value.Items.Count);
      }

      // No returned row carries personal data.
      [Fact]
      public async Task Audit_rows_carry_no_personal_data()
      {
          var fixture = AuditFixture.Create().WithFullHistory();
          var query = new SearchAuditQuery(
              fixture.FullAuditorUserId, Cursor: null, Limit: 200,
              EntityType: null, Action: null,
              From: null, To: null, EntityId: null);

          var result = await fixture.SearchAsync(query, CancellationToken.None);

          Assert.DoesNotContain(result.Value.Items, r => r.ContainsPersonalData());
      }
  }
  ```

  The suites also cover: the attendee-bucket mirror (event rows never returned);
  concurrent inserts yielding non-overlapping pages; the attendee and event histories
  demanding their own buckets.

- [ ] **Step 2: Run.** Expected: FAIL — the audit search handlers do not exist.

  ```bash
  dotnet test tests/EventBooking.Application.Tests --filter "FullyQualifiedName~Audit"
  ```

- [ ] **Step 3: Implement.** Create the handler and query files; delete the ported files.
  Enforce buckets in SQL. Paginate by keyset.

- [ ] **Step 4: Run.** Expected: PASS — the new suites plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  Expect Application and Infrastructure counts to rise. A count that does not match after
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
  git add docs/detailed-implementations/phase-3j-audit-search.md docs/detailed-implementations/phase-3-application.md docs/detailed-implementations/HANDOVER.md
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "docs(plans): Task 20b audit search and histories

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```

## Pull request

Phase 3 is decision-bearing: it puts D13 (the outbox), D14 (the token lifecycle as
implemented) and D15 into code. The pull request therefore carries the `narrative-required`
label and the three narrative headings, spelled exactly as
`.github/pull_request_template.md` spells them:

- `## Narrative Context`
- `## Narrative Decision`
- `## Narrative Consequences`

Supplying a body replaces that template wholesale, so carry those headings and the
`AI-Fingerprint:` footer in the body yourself:

```bash
MERGE_BASE=$(git merge-base origin/main HEAD)
git diff "$MERGE_BASE" HEAD | shasum -a 256 | cut -c1-12
```

Task 20b is the master plan's own gate for this phase, and it is written, so the pull
request is what remains. Recompute the fingerprint and update the body after any further push
to the branch, or the `ai-fingerprint` check fails on the stale value.

Do not merge the pull request yourself: code-owner review and the required checks stand.
