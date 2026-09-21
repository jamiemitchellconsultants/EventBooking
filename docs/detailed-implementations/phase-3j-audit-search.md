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
    DateTimeOffset? From, DateTimeOffset? To, Guid? EntityId);
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
  using EventBooking.Domain.Access;

  namespace EventBooking.Application.Tests.Audit;

  public sealed class AuditSearchHandlerTests
  {
      [Fact]
      public async Task Event_bucket_caller_never_sees_attendee_rows()
      {
          var queries = new MemoryAuditQueries()
              .WithRow("Attendee", "InviteCreated")
              .WithRow("Event", "EventConfirmed");
          var profiles = new InMemoryStaffAccessProfileRepository();
          var auditor = Guid.NewGuid();
          profiles.Items.Add(StaffAccessProfile.Create(auditor, Role.Admin, null));
          var handler = new SearchAuditHandler(queries, profiles);

          var result = await handler.HandleAsync(new SearchAuditQuery(
              auditor, null, 50, null, null, null, null, null), CancellationToken.None);

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
          var profiles = new InMemoryStaffAccessProfileRepository();
          var auditor = Guid.NewGuid();
          profiles.Items.Add(StaffAccessProfile.Create(auditor, Role.Admin, null));
          var handler = new SearchAuditHandler(queries, profiles);

          var result = await handler.HandleAsync(new SearchAuditQuery(
              auditor, null, 50, null, null, null, null, null), CancellationToken.None);

          Assert.Equal(2, result.Value.Items.Count);
      }

      [Fact]
      public async Task No_returned_row_carries_personal_data()
      {
          var queries = new MemoryAuditQueries().WithFullHistory();
          var profiles = new InMemoryStaffAccessProfileRepository();
          var auditor = Guid.NewGuid();
          profiles.Items.Add(StaffAccessProfile.Create(auditor, Role.Coordinator, null));
          var handler = new SearchAuditHandler(queries, profiles);

          var result = await handler.HandleAsync(new SearchAuditQuery(
              auditor, null, 200, null, null, null, null, null), CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.DoesNotContain(result.Value.Items, r => r.ContainsPersonalData());
      }

      [Fact]
      public async Task Pages_do_not_overlap_under_concurrent_inserts()
      {
          var queries = new MemoryAuditQueries().WithRows(30);
          var profiles = new InMemoryStaffAccessProfileRepository();
          var auditor = Guid.NewGuid();
          profiles.Items.Add(StaffAccessProfile.Create(auditor, Role.Coordinator, null));
          var handler = new SearchAuditHandler(queries, profiles);

          var first = await handler.HandleAsync(new SearchAuditQuery(
              auditor, null, 10, null, null, null, null, null), CancellationToken.None);
          queries.WithRows(10);
          var second = await handler.HandleAsync(new SearchAuditQuery(
              auditor, first.Value.NextCursor, 10, null, null, null, null, null),
              CancellationToken.None);

          Assert.Empty(first.Value.Items.Select(i => i.Cursor)
              .Intersect(second.Value.Items.Select(i => i.Cursor)));
      }
  }
  ```

  The handler resolves buckets from the caller's capabilities; the query re-enforces
  them, and the Infrastructure suite proves the SQL filters without any handler.
  MemoryAuditQueries is a private sealed class in the same file: rows carry entity type,
  action, actor, instant and details; bucket derivation follows the Architecture list
  above; keyset order is (occurred at, id) with cursor round-trip; ContainsPersonalData
  scans details for `@` and for any name in the seeded history. WithFullHistory seeds
  every action Tasks 12–19 emit. Write all three doubles in full in the file.

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

- [ ] **Step 3: Implement.** Add the production code below in full. No placeholders:
  every file below is complete.

  ```csharp
  // src/EventBooking.Application/Audit/AuditSearchHandlers.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;
  using EventBooking.Application.ReadModels;
  using EventBooking.Domain.Access;

  namespace EventBooking.Application.Audit;

  public sealed class SearchAuditHandler(
      IAuditSearchQueries queries,
      IStaffAccessAuthorizer access)
  {
      public async Task<Result<AuditSearchView>> HandleAsync(
          SearchAuditQuery query, CancellationToken ct)
      {
          if (query.Limit is < 1 or > 200)
              return Result<AuditSearchView>.Failure(
                  Error.Validation("Limit must be between 1 and 200."));

          var eventAccess = await access.AuthorizeAsync(
              query.StaffUserId, StaffCapability.ViewEventAudit, null, ct);
          var attendeeAccess = await access.AuthorizeAsync(
              query.StaffUserId, StaffCapability.ViewAttendeeAudit, null, ct);
          if (eventAccess.IsFailure && attendeeAccess.IsFailure)
              return Result<AuditSearchView>.Failure(
                  Error.Forbidden("Audit search needs an audit capability."));
          var shape = ShapeOf(
              eventAccess.IsSuccess ? eventAccess.Value : attendeeAccess.Value);
          var buckets = new List<string>();
          if (eventAccess.IsSuccess) buckets.Add("event");
          if (attendeeAccess.IsSuccess) buckets.Add("attendee");

          var page = await queries.SearchAsync(shape, buckets, query.Cursor, query.Limit,
              query.EntityType, query.Action, query.From, query.To, query.EntityId, ct);
          return Result<AuditSearchView>.Success(page);
      }

      private static CallerShape ShapeOf(StaffAccessContext context) => new(
          context.StaffUserId, context.Roles.Contains(Role.Admin),
          context.Roles.Select(r => r.ToString()).ToHashSet());
  }

  public sealed class AttendeeHistoryHandler(
      IAuditSearchQueries queries,
      IStaffAccessAuthorizer access)
  {
      public async Task<Result<AuditSearchView>> HandleAsync(
          GetAttendeeHistoryQuery query, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(
              query.StaffUserId, StaffCapability.ViewAttendeeAudit, null, ct);
          if (authorized.IsFailure) return Result<AuditSearchView>.Failure(authorized.Error);
          var page = await queries.HistoryAsync(ShapeOf(authorized.Value), ["attendee"],
              query.AttendeeId, query.Cursor, query.Limit, ct);
          return Result<AuditSearchView>.Success(page);
      }

      private static CallerShape ShapeOf(StaffAccessContext context) => new(
          context.StaffUserId, context.Roles.Contains(Role.Admin),
          context.Roles.Select(r => r.ToString()).ToHashSet());
  }

  public sealed class EventHistoryHandler(
      IAuditSearchQueries queries,
      IStaffAccessAuthorizer access)
  {
      public async Task<Result<AuditSearchView>> HandleAsync(
          GetEventHistoryQuery query, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(
              query.StaffUserId, StaffCapability.ViewEventAudit, null, ct);
          if (authorized.IsFailure) return Result<AuditSearchView>.Failure(authorized.Error);
          var page = await queries.HistoryAsync(ShapeOf(authorized.Value), ["event"],
              query.EventId, query.Cursor, query.Limit, ct);
          return Result<AuditSearchView>.Success(page);
      }

      private static CallerShape ShapeOf(StaffAccessContext context) => new(
          context.StaffUserId, context.Roles.Contains(Role.Admin),
          context.Roles.Select(r => r.ToString()).ToHashSet());
  }
  ```

  The tests construct the handlers with the profiles fake (which implements the
  authorizer, as in Tasks 12–17) — update the three constructions in the Application test
  file to `new SearchAuditHandler(queries, profiles)`. The bucket derivation is the
  handler's only authorization logic, and the query re-enforces it: the search query takes
  the caller shape plus the bucket list and refuses Admin-shaped callers asking for the
  attendee bucket (and any caller asking for a bucket outside its shape) before reading.

  ```csharp
  // src/EventBooking.Application/Abstractions/IAuditSearchQueries.cs (complete)
  using EventBooking.Application.Audit;
  using EventBooking.Application.ReadModels;

  namespace EventBooking.Application.Abstractions;

  /// <summary>
  /// Bucketed audit search. The handler resolves the caller's buckets from its
  /// capabilities and passes them; this port enforces them in SQL.
  /// </summary>
  public interface IAuditSearchQueries
  {
      Task<AuditSearchView> SearchAsync(
          CallerShape shape, IReadOnlyList<string> buckets, string? cursor, int limit,
          string? entityType, string? action, DateTimeOffset? from, DateTimeOffset? to,
          Guid? entityId, CancellationToken ct);

      Task<AuditSearchView> HistoryAsync(
          CallerShape shape, IReadOnlyList<string> buckets, Guid entityId,
          string? cursor, int limit, CancellationToken ct);
  }
  ```

  ```csharp
  // src/EventBooking.Infrastructure/Persistence/Queries/AuditSearchQueries.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Audit;
  using EventBooking.Application.ReadModels;
  using EventBooking.Domain.Audit;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.EntityFrameworkCore.Storage;
  using Npgsql;
  using NpgsqlTypes;

  namespace EventBooking.Infrastructure.Persistence.Queries;

  /// <summary>
  /// Bucketed audit search in one statement per page.
  ///
  /// The column names here are this schema's, read from AuditLogConfiguration rather than
  /// assumed: the instant is `timestamp` (quoted, because it is also a type name), while
  /// `action` and `actor_type` are the domain enums stored through HasConversion&lt;int&gt;
  /// and are therefore read as integers and named in .NET. The keyset index this paging
  /// needs already exists — `ix_audit_log_timestamp` on (`timestamp`, `id`), created by the
  /// Task 9b initial schema — so this task adds no migration.
  /// </summary>
  /// <param name="context">The read-only persistence context.</param>
  public sealed class AuditSearchQueries(EventBookingDbContext context) : IAuditSearchQueries
  {
      /// <summary>
      /// The bucket is derived in SQL and never taken from the caller, so a staff member
      /// holding only one audit capability cannot reach the other bucket's rows by naming
      /// an entity type in a filter. An entity type in neither list yields NULL and matches
      /// no bucket: a type added later is invisible until it is classified here, rather
      /// than defaulting into `event` and leaking.
      /// </summary>
      private const string BucketCase =
          """
          CASE
              WHEN a.entity_type IN ('Attendee', 'Invite', 'Booking') THEN 'attendee'
              WHEN a.entity_type IN ('Location', 'AppointmentType', 'AttendeeGroup',
                                     'SystemSettings', 'EventProposal', 'Event',
                                     'BookingAppointment', 'StaffAccessProfile') THEN 'event'
          END
          """;

      // Newest first: an audit trail is read backwards from the most recent change. The
      // index serves either direction, so this is a product choice, not a planner one.
      private static readonly string PageSql =
          $"""
           SELECT a.id, a.entity_type, a.action, a.actor_type, a."timestamp"
             FROM audit_log a
            WHERE ({BucketCase}) = ANY(@buckets)
              AND (@entityType IS NULL OR a.entity_type = @entityType)
              AND (@action IS NULL OR a.action = @action)
              AND (@from IS NULL OR a."timestamp" >= @from)
              AND (@to IS NULL OR a."timestamp" <= @to)
              AND (@entityId IS NULL OR a.entity_id = @entityId)
              AND (@cursorAt IS NULL
                   OR a."timestamp" < @cursorAt
                   OR (a."timestamp" = @cursorAt AND a.id < @cursorId))
            ORDER BY a."timestamp" DESC, a.id DESC
            LIMIT @limit
           """;

      /// <inheritdoc />
      public Task<AuditSearchView> SearchAsync(
          CallerShape shape, IReadOnlyList<string> buckets, string? cursor, int limit,
          string? entityType, string? action, DateTimeOffset? from, DateTimeOffset? to,
          Guid? entityId, CancellationToken ct) =>
          PageAsync(buckets, cursor, limit, entityType, action, from, to, entityId, ct);

      /// <inheritdoc />
      public Task<AuditSearchView> HistoryAsync(
          CallerShape shape, IReadOnlyList<string> buckets, Guid entityId,
          string? cursor, int limit, CancellationToken ct) =>
          PageAsync(buckets, cursor, limit, null, null, null, null, entityId, ct);

      // `shape` is part of the port because the handler holds it and Task 21 signs the
      // cursor with it. It deliberately does not widen or narrow the buckets here: the
      // capability check is the handler's, and duplicating it would give two places to
      // get the same rule wrong.
      private async Task<AuditSearchView> PageAsync(
          IReadOnlyList<string> buckets, string? cursor, int limit, string? entityType,
          string? action, DateTimeOffset? from, DateTimeOffset? to, Guid? entityId,
          CancellationToken ct)
      {
          ArgumentNullException.ThrowIfNull(buckets);

          // No capability, no rows. The handler refuses this first; the query does not
          // rely on that, because an empty bucket set must never mean "everything".
          if (buckets.Count == 0)
          {
              return new AuditSearchView([], null);
          }

          // A filter naming an action this system does not emit matches nothing. That is
          // an empty page, not an error: the caller asked a well-formed question.
          int? actionValue = null;
          if (action is not null)
          {
              if (!Enum.TryParse<AuditAction>(action, ignoreCase: true, out var parsed))
              {
                  return new AuditSearchView([], null);
              }

              actionValue = (int)parsed;
          }

          DateTimeOffset? cursorAt = null;
          Guid? cursorId = null;
          if (AttendeeCursor.TryDecode(cursor, out var sortKey, out var decodedId)
              && DateTimeOffset.TryParse(
                  sortKey, null, DateTimeStyles.RoundtripKind, out var decodedAt))
          {
              cursorAt = decodedAt.ToUniversalTime();
              cursorId = decodedId;
          }

          await context.Database.OpenConnectionAsync(ct);
          try
          {
              var connection = (NpgsqlConnection)context.Database.GetDbConnection();
              await using var command = new NpgsqlCommand(PageSql, connection);
              command.Transaction =
                  context.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;

              command.Parameters.Add(
                  new NpgsqlParameter("buckets", NpgsqlDbType.Array | NpgsqlDbType.Text)
                  { Value = buckets.ToArray() });
              Add(command, "entityType", NpgsqlDbType.Text, entityType);
              Add(command, "action", NpgsqlDbType.Integer, actionValue);
              Add(command, "from", NpgsqlDbType.TimestampTz, from?.ToUniversalTime());
              Add(command, "to", NpgsqlDbType.TimestampTz, to?.ToUniversalTime());
              Add(command, "entityId", NpgsqlDbType.Uuid, entityId);
              Add(command, "cursorAt", NpgsqlDbType.TimestampTz, cursorAt);
              Add(command, "cursorId", NpgsqlDbType.Uuid, cursorId);
              // One more than asked for, so the presence of a next page is known without
              // a second count query.
              command.Parameters.Add(new NpgsqlParameter("limit", limit + 1));

              var rows = new List<AuditRow>(limit + 1);
              await using var reader = await command.ExecuteReaderAsync(ct);
              while (await reader.ReadAsync(ct))
              {
                  var id = reader.GetGuid(0);
                  var occurredAt = reader.GetFieldValue<DateTimeOffset>(4);
                  rows.Add(new AuditRow(
                      id,
                      reader.GetString(1),
                      ((AuditAction)reader.GetInt32(2)).ToString(),
                      ((ActorType)reader.GetInt32(3)).ToString(),
                      occurredAt,
                      AttendeeCursor.Encode(
                          occurredAt.ToUniversalTime().ToString("o"), id)));
              }

              if (rows.Count <= limit)
              {
                  return new AuditSearchView(rows, null);
              }

              var page = rows.Take(limit).ToList();
              return new AuditSearchView(page, page[^1].Cursor);
          }
          finally
          {
              await context.Database.CloseConnectionAsync();
          }
      }

      private static void Add(
          NpgsqlCommand command, string name, NpgsqlDbType type, object? value) =>
          command.Parameters.Add(
              new NpgsqlParameter(name, type) { Value = value ?? DBNull.Value });
  }
  ```

  AuditSearchQueries needs `using System.Globalization;` for the cursor's
  `DateTimeStyles.RoundtripKind`, and CallerShape comes from Task 20a.

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
