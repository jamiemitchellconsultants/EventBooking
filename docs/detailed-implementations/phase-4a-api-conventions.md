# 04a — API conventions (Task 21)

[← Phase overview](phase-4-api-and-mcp.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task is the first of Phase 4. It builds the conventions every endpoint in Tasks 22a, 22b
and 23 then obeys: the RFC 9457 problem body and its error catalogue, the opaque signed cursor,
the one shared event-time representation, `_links` generated from the caller's capabilities,
the Idempotency-Key retention, the three rate limits, the liveness and readiness probes, the
metrics endpoint, structured logging with a correlation identifier, and startup validation of
every required setting.

> Use superpowers:executing-plans. This task is hand-authored: complete code and complete tests
> are written straight into this document, with no prototype. Compile and test-drive them
> yourself. The test counts below are what you should expect to reach, not figures observed by
> the author — nothing here has been run.

**Goal:** One problem-details writer mapping every application error code to the design's `type`
slug and status, carrying `errors[]`, `current`, `consequence`, `minimum` and `blocking` where
the error supplies them. One cursor codec: base64url of the sort key plus an HMAC over it, with a
tampered cursor refused as `validation-failed`. One page contract: `?cursor=&limit=` bounded to
1–200, default 50, returning `{ items, nextCursor }`. One event-time contract carrying `date`,
`startTime`, `durationMinutes`, `startLocal`, `endLocal`, `startUtc`, `endUtc`, `timeZoneId` and
`zoneAbbreviation`. `_links` built from the capabilities the caller holds. An Idempotency-Key
retained 24 hours on create endpoints. Rate limits of 30 per minute per client address and 10 per
minute per token prefix on the attendee routes, and 300 per minute per staff identity elsewhere,
with forwarded headers trusted only from the configured proxy network. `/health/live`,
`/health/ready` and `/metrics`. Structured JSON logs carrying a correlation identifier that
reaches the outbox row. Startup validation of every required setting from design 04, rejecting a
signing key shorter than 32 bytes or matching the placeholder denylist.

**Architecture:** Everything here is API-layer, with three exceptions that cannot be: the
idempotency store is a port in the Application project with an EF adapter in Infrastructure
(the middleware lives in the API project and reads the port); the correlation identifier is an
Application-project ambient accessor so the outbox writer can read it without the API project
being referenced downward; and five error factories plus the token handlers' expiry distinction
are Application changes the catalogue needs in order to produce the design's slugs at all. No
endpoint gains a business rule: the problem writer, the cursor codec and the link builder are
pure functions over what a handler already returned.

**Tech Stack:** .NET 10, xUnit, WebApplicationFactory, EF Core with Npgsql, Testcontainers,
PostgreSQL 16.

**Spec:** [Master Task 21](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[API design](../design/05-api-design.md),
[security and authentication](../design/06-security-and-authentication.md),
[solution architecture](../design/04-solution-architecture.md),
[non-functional requirements](../design/08-nonfunctional-requirements.md),
[ontology](../ontology.md).

## Settlements this task inherits and makes

Contradiction #3, settled in Phase 3 and recorded in the handover's section 8, becomes real here:
**a missing or malformed `staff_id` is 403 everywhere except `GET /api/me`**, against the API
catalogue's 401. The catalogue keeps `unauthenticated` at 401 for a missing or invalid bearer
token — that is a different failure, and the two must not be merged.

Five further contradictions were settled with the user while this task was authored. Each is
recorded in the handover's section 8 under Task 21.

- **An expired attendee token returns 410 `token-expired`.** Design 05 and design 06 both
  distinguish it from 404 `token-invalid`; the ported handlers collapsed every token failure into
  one not-found so that a forged token could not be told from an expired one. The design wins.
  A token whose signature verifies, whose row resolves and whose stored version matches, but whose
  `Invite` has expired, is a link the holder already knows they were sent — telling them it has
  lapsed is what lets the page name who to contact. Everything else stays indistinguishable.
- **`proposal-not-open` becomes a typed error.** Task 13 returns a generic conflict carrying the
  status in its message. This task adds the factory and repoints Task 13's three catch blocks at
  it, so the status travels in the problem body the way Task 12's other 409 bodies carry theirs.
- **`last-admin` is reachable from the staff-access endpoint**, not from role synchronisation.
  Task 17's per-request sync keeps its silent refusal and its alert: a background reconciliation
  must not fail an unrelated request because of someone else's identity-provider change.
- **The catalogue gains four slugs the design's table omits.** Design 05's table is a list of the
  stable slugs, not a closed enumeration of every failure the application can already produce, and
  four of them have no row: `not-found` (404) for an unknown resource; `requirement-mismatch`
  (409) for the requirement-snapshot refusal Tasks 15 and 16 return, which folded into
  `requirements-locked` would tell a Coordinator their group change was blocked when it was not;
  `already-confirmed` (409), which is Phase 3 settlement #2 and postdates the design; and
  `conflict` (409) as the residual generic for the state refusals Phase 3 raises without
  structured data. Every one of design 05's own slugs still has a producing error code, which is
  what the catalogue test asserts.
- **The outbox row's correlation identifier is the request's where one exists.** Settlement #6
  records the column as the dispatcher's; master Task 21 asks for the request's identifier to be
  propagated into outbox rows. Both hold once the dispatcher's claim writes
  `COALESCE(correlation_id, @correlationId)`: a row staged by a request keeps that request's
  identifier, and a row staged by a background job takes the dispatcher run's.

## Global constraints

The problem body is `application/problem+json` for every failure, and an expected failure is
never a 500. `type` is one of the catalogue's slugs and nothing else — an unmapped application
error code fails the catalogue test rather than degrading to 500 in production. Every list
response is `{ items, nextCursor }` with no offset parameter anywhere. Every cursor is signed
with the same key the attendee tokens use and verified in constant time before it is decoded.
Logs and metric labels carry no name, email address, token or cursor. Startup fails, naming every
missing key at once, before the host binds a port.

## Review focus

STOP AND CHECK five things. The catalogue test walks every slug in design 05's table and asserts
the status — a slug with no producing error code fails it, which is what caught `proposal-not-open`
and `last-admin` while this was authored. The tampered-cursor test flips one byte of the signature
rather than of the payload, because a payload edit would also fail decoding and would pass for the
wrong reason. The forwarded-header test asserts the limiter partitioned on the socket address, not
merely that the request was allowed. The redaction test asserts against the captured log output of
a booking confirm, whose request body carries a token and whose attendee carries an email address.
And the startup test asserts the message names every missing key, not just the first.

### Task 21: API conventions

**Files:**

- Create: src/EventBooking.Api/Endpoints/ProblemCatalogue.cs
- Modify: src/EventBooking.Api/Endpoints/ResultResponses.cs (RFC 9457 bodies from the catalogue)
- Create: src/EventBooking.Api/Pagination/PageCursor.cs
- Create: src/EventBooking.Api/Pagination/PageRequest.cs
- Create: src/EventBooking.Api/Contracts/EventTimeResponse.cs
- Create: src/EventBooking.Api/Contracts/CallerLinks.cs
- Create: src/EventBooking.Api/Idempotency/IdempotencyMiddleware.cs
- Create: src/EventBooking.Api/Auth/StaffRateLimiterPolicy.cs
- Create: src/EventBooking.Api/Auth/TokenPrefixRateLimiterPolicy.cs
- Modify: src/EventBooking.Api/Auth/RemoteIpRateLimiterPolicy.cs (sliding window, limit from configuration)
- Create: src/EventBooking.Api/Observability/CorrelationMiddleware.cs
- Create: src/EventBooking.Api/Observability/EventBookingMetrics.cs
- Create: src/EventBooking.Api/Observability/PrometheusText.cs
- Modify: src/EventBooking.Api/EventBookingConfiguration.cs (every required setting, key denylist)
- Modify: src/EventBooking.Api/Program.cs (probes, metrics, middleware order, limiter policies)
- Modify: src/EventBooking.Mcp/Program.cs (the settings record; Task 23 rewrites the rest)
- Create: src/EventBooking.Application/Abstractions/ICorrelationContext.cs
- Create: src/EventBooking.Application/Abstractions/IIdempotencyStore.cs
- Modify: src/EventBooking.Application/Common/Error.cs (five factories the catalogue needs)
- Modify: src/EventBooking.Application/Bookings/ViewInviteHandler.cs (expired is not invalid)
- Modify: src/EventBooking.Application/Bookings/ViewBookingHandler.cs (same distinction)
- Modify: src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs (same distinction)
- Modify: src/EventBooking.Application/Negotiation/RecordAcceptanceHandler.cs (typed refusal)
- Modify: src/EventBooking.Application/Negotiation/WithdrawAcceptanceHandler.cs (typed refusal)
- Modify: src/EventBooking.Application/Negotiation/WithdrawProposalHandler.cs (typed refusal)
- Modify: src/EventBooking.Application/Access/StaffScopeHandler.cs (last-admin refusal)
- Create: src/EventBooking.Infrastructure/Persistence/Idempotency/IdempotencyRecord.cs
- Create: src/EventBooking.Infrastructure/Persistence/Idempotency/IdempotencyStore.cs
- Modify: src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs (the new set; the stage-time correlation backstop)
- Modify: src/EventBooking.Infrastructure/Email/OutboxDispatcher.cs (claim keeps a staged identifier)
- Modify: src/EventBooking.Domain/Notifications/EmailLog.cs (StampCorrelation)
- Test: tests/EventBooking.Api.Tests/Conventions/ProblemCatalogueTests.cs
- Test: tests/EventBooking.Api.Tests/Conventions/PageCursorTests.cs
- Test: tests/EventBooking.Api.Tests/Conventions/PaginationWalkTests.cs
- Test: tests/EventBooking.Api.Tests/Conventions/IdempotencyTests.cs
- Test: tests/EventBooking.Api.Tests/Conventions/RateLimitTests.cs
- Test: tests/EventBooking.Api.Tests/Conventions/StartupValidationTests.cs
- Test: tests/EventBooking.Api.Tests/Conventions/LogRedactionTests.cs
- Test: tests/EventBooking.Api.Tests/Conventions/EventTimeContractTests.cs
- Test: tests/EventBooking.Api.Tests/Conventions/HealthAndMetricsTests.cs

**Interfaces:**

```csharp
namespace EventBooking.Api.Endpoints;

// The whole wire contract for a failure, in one place. Slug and status come from design 05's
// error catalogue; DataMember names the extension the design gives that slug for the numeric
// map the application error carries (current, consequence, blocking), and ScalarKey names the
// one key to lift out where the design gives a bare number instead (minimum). An application
// code with no row here is a programming error, not a 500 in production: the catalogue test
// enumerates the rows, and ResultResponses throws on a miss.
public sealed record ProblemShape(
    string Type, int Status, string Title, string? DataMember = null, string? ScalarKey = null);

public static class ProblemCatalogue
{
    public static IReadOnlyDictionary<string, ProblemShape> ByErrorCode { get; }
    public static ProblemShape For(string errorCode);
}
```

```csharp
namespace EventBooking.Api.Pagination;

// An opaque keyset token: base64url(payload) + "." + base64url(HMAC-SHA256(key, payload)).
// The signature is compared in constant time before the payload is read, so a tampered cursor
// costs one hash and never reaches a query. There is no offset pagination anywhere.
public sealed class PageCursor(byte[] signingKey)
{
    public string Protect(string payload);
    public bool TryUnprotect(string? cursor, out string payload);
}

// The bound query string every list endpoint takes. Limit is clamped to the design's 1-200
// with a default of 50; a limit outside the range is a field error, not a silent clamp.
public sealed record PageRequest(string? Cursor, int Limit)
{
    public const int DefaultLimit = 50;
    public const int MinLimit = 1;
    public const int MaxLimit = 200;
    public static bool TryBind(string? cursor, int? limit, out PageRequest request, out string? field);
}

// The one list envelope. Items are already projected by the caller; NextCursor is null on the
// last page.
public sealed record Page<T>(IReadOnlyList<T> Items, string? NextCursor);
```

```csharp
namespace EventBooking.Api.Contracts;

// The single event-time representation design 05 names, shared by every response that carries
// an EventWindow. Derived members are computed once, here, from the Location's zone — no
// endpoint recomputes them and no two endpoints can disagree.
public sealed record EventTimeResponse(
    DateOnly Date, TimeOnly StartTime, int DurationMinutes,
    DateTimeOffset StartLocal, DateTimeOffset EndLocal,
    DateTimeOffset StartUtc, DateTimeOffset EndUtc,
    string TimeZoneId, string ZoneAbbreviation)
{
    public static EventTimeResponse From(
        DateOnly date, TimeOnly startTime, int durationMinutes,
        string timeZoneId, IEventWindowZones zones);
}

// _links generation over the ported ApiLink record in Contracts/ApiLink.cs, which is unchanged.
// The caller's granted capabilities decide which affordances appear, so the Web front end can
// enable or disable controls without restating the authorization matrix. A candidate whose
// RequiredCapability is null is unconditional — "self" and the like.
public sealed record LinkCandidate(
    string Rel, string OperationId, string Href, string? RequiredCapability);

public static class CallerLinks
{
    public static IReadOnlyDictionary<string, ApiLink> For(
        IReadOnlySet<string> capabilities, params LinkCandidate[] candidates);
}
```

```csharp
namespace EventBooking.Application.Abstractions;

// The request's correlation identifier, readable by anything that stages an outbox row. Set
// once per request by the API middleware and once per run by the sweep and the dispatcher.
public interface ICorrelationContext
{
    string CorrelationId { get; }
    IDisposable Begin(string correlationId);
}

// Idempotency-Key retention. Keys are scoped to the staff identity and the route, so one
// caller's key can never replay another's response. Retention is 24 hours; a row older than
// that reads as absent and is deleted opportunistically on the next write.
public interface IIdempotencyStore
{
    Task<IdempotentResponse?> TryGetAsync(
        Guid staffUserId, string route, string key, DateTimeOffset now, CancellationToken ct);

    Task SaveAsync(
        Guid staffUserId, string route, string key, string requestHash,
        int statusCode, string body, DateTimeOffset now, CancellationToken ct);
}

public sealed record IdempotentResponse(string RequestHash, int StatusCode, string Body);
```

```csharp
// Error.cs: five factories the catalogue needs, beside the Task 12-16 ones. Each carries its
// data numerically, matching the Task 12 shape; the proposal's status is a string and so
// travels in the message, with the outcome record carrying it typed where a caller needs it.
public const string ProposalNotOpenCode = "proposal-not-open";
public static Error ProposalNotOpen(string message) => new(ProposalNotOpenCode, message);

public const string LastAdminCode = "last-admin";
public static Error LastAdmin(string message) => new(LastAdminCode, message);

public const string TokenInvalidCode = "token-invalid";
public static Error TokenInvalid(string message) => new(TokenInvalidCode, message);

public const string TokenExpiredCode = "token-expired";
public static Error TokenExpired(string message) => new(TokenExpiredCode, message);

public const string RequirementMismatchCode = "requirement-mismatch";
public static Error RequirementMismatch(string message) => new(RequirementMismatchCode, message);
```

- [ ] **Step 1: Write the failing tests.** Create the conventions suite. Nine files, each
  complete. The suite runs against the real host through the Task 1 factory, with one exception:
  the startup suite builds configuration directly, because a host that refuses to start cannot be
  reached through a client.

  ```csharp
  // tests/EventBooking.Api.Tests/Conventions/ProblemCatalogueTests.cs (complete)
  using System.Net;
  using System.Net.Http.Json;
  using System.Text.Json;
  using EventBooking.Api.Endpoints;
  using EventBooking.Application.Common;

  namespace EventBooking.Api.Tests.Conventions;

  /// <summary>Pins the wire contract of every failure the API can return.</summary>
  [Collection("api")]
  public sealed class ProblemCatalogueTests(ApiFactory factory)
  {
      /// <summary>Every slug design 05 names, with the status it names.</summary>
      public static TheoryData<string, int> DesignSlugs => new()
      {
          { "validation-failed", 422 },
          { "version-conflict", 409 },
          { "confirmation-required", 409 },
          { "capacity-exhausted", 409 },
          { "capacity-below-bookings", 409 },
          { "proposal-not-open", 409 },
          { "window-started", 409 },
          { "in-use", 409 },
          { "requirements-locked", 409 },
          { "insufficient-events", 409 },
          { "recovery-active", 409 },
          { "last-admin", 409 },
          { "token-invalid", 404 },
          { "token-expired", 410 },
          { "forbidden", 403 },
          { "unauthenticated", 401 },
          { "rate-limited", 429 },
      };

      [Theory]
      [MemberData(nameof(DesignSlugs))]
      public void EveryDesignSlugHasAProducingErrorCodeAndTheDesignStatus(string slug, int status)
      {
          var rows = ProblemCatalogue.ByErrorCode.Values.Where(x => x.Type == slug).ToList();
          Assert.NotEmpty(rows);
          Assert.All(rows, row => Assert.Equal(status, row.Status));
      }

      /// <summary>
      /// The four slugs the design's table omits. They are listed here rather than folded into
      /// the theory above so that a reader can tell the design's contract from this task's
      /// additions at a glance.
      /// </summary>
      [Theory]
      [InlineData("not-found", 404)]
      [InlineData("requirement-mismatch", 409)]
      [InlineData("already-confirmed", 409)]
      [InlineData("conflict", 409)]
      public void TheAddedSlugsCarryTheirAgreedStatus(string slug, int status)
      {
          var rows = ProblemCatalogue.ByErrorCode.Values.Where(x => x.Type == slug).ToList();
          Assert.NotEmpty(rows);
          Assert.All(rows, row => Assert.Equal(status, row.Status));
      }

      /// <summary>
      /// Every error code the application can emit is mapped. An unmapped code would otherwise
      /// reach a caller as a 500, which design 05 forbids for an expected failure.
      /// </summary>
      [Fact]
      public void EveryApplicationErrorCodeIsMapped()
      {
          foreach (var code in ApplicationErrorCodes())
          {
              Assert.True(
                  ProblemCatalogue.ByErrorCode.ContainsKey(code),
                  $"Application error code '{code}' has no catalogue row.");
          }
      }

      /// <summary>A mapped error renders every member design 05 names for its slug.</summary>
      [Fact]
      public void AVersionConflictCarriesCurrentAndAnInUseCarriesBlocking()
      {
          var conflict = ResultResponses.ProblemBodyFor(Error.VersionConflict("Stale.", 7));
          Assert.Equal("version-conflict", conflict.Type);
          Assert.Equal(409, conflict.Status);
          var current = Assert.IsAssignableFrom<IReadOnlyDictionary<string, long>>(
              conflict.Extensions["current"]);
          Assert.Equal(7L, current["currentVersion"]);

          var inUse = ResultResponses.ProblemBodyFor(Error.ReferenceDataInUse(
              "In use.", new Dictionary<string, int> { ["openProposals"] = 1, ["futureEvents"] = 2 }));
          Assert.Equal("in-use", inUse.Type);
          var blocking = Assert.IsAssignableFrom<IReadOnlyDictionary<string, long>>(
              inUse.Extensions["blocking"]);
          Assert.Equal(1L, blocking["openProposals"]);
          Assert.Equal(2L, blocking["futureEvents"]);
      }

      /// <summary>A field error travels in errors[], per the design's example body.</summary>
      [Fact]
      public void ACapacityRefusalRendersTheDesignsExampleShape()
      {
          var body = ResultResponses.ProblemBodyFor(
              Error.CapacityExhausted("The last Induction place at this event was just taken."));

          Assert.Equal("capacity-exhausted", body.Type);
          Assert.Equal(409, body.Status);
          Assert.False(string.IsNullOrWhiteSpace(body.Title));
          Assert.Equal(
              "The last Induction place at this event was just taken.", body.Detail);
      }

      /// <summary>A staff route with no staff_id is 403, not 401 (contradiction #3).</summary>
      [Fact]
      public async Task AMissingStaffNumberIsForbiddenEverywhereButMe()
      {
          factory.SignedInAs = await factory.GivenStaffAsync(
              [EventBooking.Domain.Access.Role.Coordinator], null);
          factory.StaffIdClaim = null;
          var client = factory.CreateClient();

          var guarded = await client.GetAsync("/api/attendees?limit=1");
          var me = await client.GetAsync("/api/me");

          Assert.Equal(HttpStatusCode.Forbidden, guarded.StatusCode);
          Assert.Equal("application/problem+json", guarded.Content.Headers.ContentType?.MediaType);
          var problem = await guarded.Content.ReadFromJsonAsync<JsonElement>();
          Assert.Equal("forbidden", problem.GetProperty("type").GetString());
          Assert.Equal(HttpStatusCode.OK, me.StatusCode);
      }

      /// <summary>An anonymous staff route is 401 unauthenticated, which is a different failure.</summary>
      [Fact]
      public async Task AnAnonymousStaffRouteIsUnauthenticated()
      {
          factory.SignedInAs = null;
          var client = factory.CreateClient();

          var response = await client.GetAsync("/api/attendees?limit=1");

          Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
      }

      /// <summary>Every public const ending in Code on the application's error type.</summary>
      private static IEnumerable<string> ApplicationErrorCodes()
      {
          foreach (var field in typeof(Error).GetFields(
              System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
          {
              if (field.IsLiteral && field.FieldType == typeof(string) &&
                  field.Name.EndsWith("Code", StringComparison.Ordinal))
              {
                  yield return (string)field.GetRawConstantValue()!;
              }
          }

          // The four factory codes that predate the constants convention.
          yield return "validation";
          yield return "not_found";
          yield return "conflict";
          yield return "forbidden";
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Api.Tests/Conventions/PageCursorTests.cs (complete)
  using System.Security.Cryptography;
  using System.Text;
  using EventBooking.Api.Pagination;

  namespace EventBooking.Api.Tests.Conventions;

  /// <summary>Pins the opaque cursor: it round-trips, and a tampered one is refused.</summary>
  public sealed class PageCursorTests
  {
      private static readonly byte[] Key = Encoding.UTF8.GetBytes(
          "a-test-signing-key-that-is-long-enough-here");

      [Fact]
      public void ACursorRoundTripsItsPayload()
      {
          var cursor = new PageCursor(Key);

          var protectedValue = cursor.Protect("2026-10-14T09:30:00Z|1f0c…");

          Assert.True(cursor.TryUnprotect(protectedValue, out var payload));
          Assert.Equal("2026-10-14T09:30:00Z|1f0c…", payload);
      }

      /// <summary>
      /// The flipped byte is in the signature, not the payload. Editing the payload would also
      /// break decoding, so that version of this test would pass for the wrong reason.
      /// </summary>
      [Fact]
      public void ATamperedSignatureIsRefused()
      {
          var cursor = new PageCursor(Key);
          var issued = cursor.Protect("page-2");
          var parts = issued.Split('.');
          var signature = Base64Url.DecodeFromChars(parts[1]);
          signature[0] ^= 0xFF;
          var tampered = parts[0] + "." + Base64Url.EncodeToString(signature);

          Assert.False(cursor.TryUnprotect(tampered, out _));
      }

      [Fact]
      public void ACursorFromAnotherKeyIsRefused()
      {
          var issued = new PageCursor(Key).Protect("page-2");
          var other = new PageCursor(RandomNumberGenerator.GetBytes(32));

          Assert.False(other.TryUnprotect(issued, out _));
      }

      [Theory]
      [InlineData(null)]
      [InlineData("")]
      [InlineData("not-a-cursor")]
      [InlineData("only-one-part.")]
      public void AMalformedCursorIsRefusedWithoutThrowing(string? value)
      {
          Assert.False(new PageCursor(Key).TryUnprotect(value, out _));
      }

      [Theory]
      [InlineData(null, PageRequest.DefaultLimit)]
      [InlineData(1, 1)]
      [InlineData(200, 200)]
      public void ALimitInRangeBinds(int? limit, int expected)
      {
          Assert.True(PageRequest.TryBind(null, limit, out var request, out var field));
          Assert.Equal(expected, request.Limit);
          Assert.Null(field);
      }

      [Theory]
      [InlineData(0)]
      [InlineData(201)]
      [InlineData(-5)]
      public void ALimitOutsideTheRangeIsAFieldErrorRatherThanASilentClamp(int limit)
      {
          Assert.False(PageRequest.TryBind(null, limit, out _, out var field));
          Assert.Equal("limit", field);
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Api.Tests/Conventions/PaginationWalkTests.cs (complete)
  using System.Net;
  using System.Net.Http.Json;
  using System.Text.Json;
  using EventBooking.Domain.AppointmentTypes;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.AttendeeGroups;
  using EventBooking.Domain.Attendees;
  using EventBooking.Infrastructure.Persistence;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;

  namespace EventBooking.Api.Tests.Conventions;

  /// <summary>
  /// Walks the attendee list, which is the one cursor-paged route that exists before Task 22b.
  /// Three walks: a single page, three pages, and an empty result.
  /// </summary>
  [Collection("api")]
  public sealed class PaginationWalkTests(ApiFactory factory)
  {
      [Fact]
      public async Task OnePageReturnsEveryRowAndNoNextCursor()
      {
          var group = await GivenGroupWithAttendeesAsync("PAGE_ONE", 3);
          var client = await SignedInCoordinatorAsync();

          var page = await ReadPageAsync(client, $"/api/attendees?groupId={group}&limit=50");

          Assert.Equal(3, page.Items.Length);
          Assert.Null(page.NextCursor);
      }

      [Fact]
      public async Task ThreePagesWalkWithoutRepeatingOrLosingARow()
      {
          var group = await GivenGroupWithAttendeesAsync("PAGE_THREE", 7);
          var client = await SignedInCoordinatorAsync();

          var seen = new List<string>();
          string? cursor = null;
          var pages = 0;
          do
          {
              var url = $"/api/attendees?groupId={group}&limit=3"
                  + (cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}");
              var page = await ReadPageAsync(client, url);
              seen.AddRange(page.Items);
              cursor = page.NextCursor;
              pages++;
          }
          while (cursor is not null && pages < 10);

          Assert.Equal(3, pages);
          Assert.Equal(7, seen.Count);
          Assert.Equal(7, seen.Distinct().Count());
      }

      [Fact]
      public async Task AnEmptyResultReturnsAnEmptyListAndNoNextCursor()
      {
          var group = await GivenGroupWithAttendeesAsync("PAGE_EMPTY", 0);
          var client = await SignedInCoordinatorAsync();

          var page = await ReadPageAsync(client, $"/api/attendees?groupId={group}&limit=50");

          Assert.Empty(page.Items);
          Assert.Null(page.NextCursor);
      }

      [Fact]
      public async Task ATamperedCursorIsRefusedAsValidationFailed()
      {
          var group = await GivenGroupWithAttendeesAsync("PAGE_TAMPER", 4);
          var client = await SignedInCoordinatorAsync();
          var first = await ReadPageAsync(client, $"/api/attendees?groupId={group}&limit=2");
          var cursor = first.NextCursor!;
          var tampered = cursor[..^2] + (cursor[^2] == 'A' ? "BB" : "AA");

          var response = await client.GetAsync(
              $"/api/attendees?groupId={group}&limit=2&cursor={Uri.EscapeDataString(tampered)}");

          Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
          var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
          Assert.Equal("validation-failed", problem.GetProperty("type").GetString());
          Assert.Equal("cursor", problem.GetProperty("errors")[0].GetProperty("field").GetString());
      }

      [Fact]
      public async Task ALimitOutsideTheRangeIsRefusedRatherThanClamped()
      {
          var client = await SignedInCoordinatorAsync();

          var response = await client.GetAsync("/api/attendees?limit=500");

          Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
          var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
          Assert.Equal("limit", problem.GetProperty("errors")[0].GetProperty("field").GetString());
      }

      private async Task<HttpClient> SignedInCoordinatorAsync()
      {
          factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
          factory.StaffIdClaim = "U500001";
          return factory.CreateClient();
      }

      private sealed record Walked(string[] Items, string? NextCursor);

      private static async Task<Walked> ReadPageAsync(HttpClient client, string url)
      {
          var response = await client.GetAsync(url);
          response.EnsureSuccessStatusCode();
          var body = await response.Content.ReadFromJsonAsync<JsonElement>();
          var items = body.GetProperty("items").EnumerateArray()
              .Select(x => x.GetProperty("email").GetString()!)
              .ToArray();
          var next = body.GetProperty("nextCursor");
          return new Walked(
              items, next.ValueKind == JsonValueKind.Null ? null : next.GetString());
      }

      /// <summary>Seeds one group with a known number of members and returns its identifier.</summary>
      private async Task<Guid> GivenGroupWithAttendeesAsync(string code, int members)
      {
          using var scope = factory.Services.CreateScope();
          var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
          var type = AppointmentType.Create(Guid.NewGuid(), code + "_TYPE", "Type " + code);
          context.AppointmentTypes.Add(type);
          var group = AttendeeGroup.Create(
              Guid.NewGuid(), code, "Group " + code, [type.Id], [type.Id]);
          context.AttendeeGroups.Add(group);
          var stamped = DateTimeOffset.Parse("2026-09-21T09:00:00Z");
          for (var index = 0; index < members; index++)
          {
              context.Attendees.Add(Attendee.Create(
                  Guid.NewGuid(),
                  $"Member {index:D2}",
                  $"{code.ToLowerInvariant()}-{index:D2}@example.com",
                  group,
                  stamped));
          }

          await context.SaveChangesAsync();
          return group.Id;
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Api.Tests/Conventions/IdempotencyTests.cs (complete)
  using System.Net;
  using System.Net.Http.Json;
  using System.Text.Json;
  using EventBooking.Domain.AppointmentTypes;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.AttendeeGroups;
  using EventBooking.Infrastructure.Persistence;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;

  namespace EventBooking.Api.Tests.Conventions;

  /// <summary>Pins the Idempotency-Key contract on a create endpoint.</summary>
  [Collection("api")]
  public sealed class IdempotencyTests(ApiFactory factory)
  {
      [Fact]
      public async Task TheSameKeyTwiceReturnsTheFirstResultAndCreatesOneRow()
      {
          var (client, groupId) = await GivenCoordinatorAndGroupAsync("IDEM_ONE");
          var key = Guid.NewGuid().ToString();
          var body = new { name = "Ada Lovelace", email = "ada@example.com", attendeeGroupId = groupId };

          var first = await PostAsync(client, body, key);
          var second = await PostAsync(client, body, key);

          Assert.Equal(HttpStatusCode.Created, first.StatusCode);
          Assert.Equal(HttpStatusCode.Created, second.StatusCode);
          Assert.Equal(
              await first.Content.ReadAsStringAsync(),
              await second.Content.ReadAsStringAsync());
          Assert.Equal(1, await CountAttendeesAsync(groupId));
      }

      [Fact]
      public async Task ADifferentBodyUnderTheSameKeyIsRefused()
      {
          var (client, groupId) = await GivenCoordinatorAndGroupAsync("IDEM_TWO");
          var key = Guid.NewGuid().ToString();

          await PostAsync(
              client, new { name = "Ada", email = "ada2@example.com", attendeeGroupId = groupId }, key);
          var second = await PostAsync(
              client, new { name = "Grace", email = "grace@example.com", attendeeGroupId = groupId }, key);

          Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
          var problem = await second.Content.ReadFromJsonAsync<JsonElement>();
          Assert.Equal("validation-failed", problem.GetProperty("type").GetString());
          Assert.Equal(1, await CountAttendeesAsync(groupId));
      }

      [Fact]
      public async Task NoKeyMeansNoReplayProtectionAndTwoRows()
      {
          var (client, groupId) = await GivenCoordinatorAndGroupAsync("IDEM_NONE");

          await PostAsync(
              client, new { name = "A", email = "a-none@example.com", attendeeGroupId = groupId }, null);
          await PostAsync(
              client, new { name = "B", email = "b-none@example.com", attendeeGroupId = groupId }, null);

          Assert.Equal(2, await CountAttendeesAsync(groupId));
      }

      /// <summary>
      /// A key older than the 24-hour retention reads as absent, so the second call runs again.
      /// The stored instant is moved back rather than the clock moved forward: the middleware
      /// reads the wall clock, and only the row is under the test's control.
      /// </summary>
      [Fact]
      public async Task AKeyOlderThanTheRetentionIsNoLongerReplayed()
      {
          var (client, groupId) = await GivenCoordinatorAndGroupAsync("IDEM_OLD");
          var key = Guid.NewGuid().ToString();
          await PostAsync(
              client, new { name = "A", email = "a-old@example.com", attendeeGroupId = groupId }, key);
          await AgeEveryKeyAsync(TimeSpan.FromHours(25));

          var second = await PostAsync(
              client, new { name = "A", email = "a-old-2@example.com", attendeeGroupId = groupId }, key);

          Assert.Equal(HttpStatusCode.Created, second.StatusCode);
          Assert.Equal(2, await CountAttendeesAsync(groupId));
      }

      private static async Task<HttpResponseMessage> PostAsync(
          HttpClient client, object body, string? key)
      {
          using var request = new HttpRequestMessage(HttpMethod.Post, "/api/attendees")
          {
              Content = JsonContent.Create(body),
          };
          if (key is not null)
          {
              request.Headers.Add("Idempotency-Key", key);
          }

          return await client.SendAsync(request);
      }

      private async Task<(HttpClient Client, Guid GroupId)> GivenCoordinatorAndGroupAsync(string code)
      {
          factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
          factory.StaffIdClaim = "U500002";
          using var scope = factory.Services.CreateScope();
          var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
          var type = AppointmentType.Create(Guid.NewGuid(), code + "_TYPE", "Type " + code);
          context.AppointmentTypes.Add(type);
          var group = AttendeeGroup.Create(Guid.NewGuid(), code, "Group " + code, [type.Id], [type.Id]);
          context.AttendeeGroups.Add(group);
          await context.SaveChangesAsync();
          return (factory.CreateClient(), group.Id);
      }

      private async Task<int> CountAttendeesAsync(Guid groupId)
      {
          using var scope = factory.Services.CreateScope();
          var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
          return await context.Attendees.CountAsync(a => a.AttendeeGroupId == groupId);
      }

      private async Task AgeEveryKeyAsync(TimeSpan by)
      {
          using var scope = factory.Services.CreateScope();
          var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
          await context.Database.ExecuteSqlRawAsync(
              "UPDATE idempotency_record SET created_at = created_at - @p0", by);
      }
  }
  ```

  The rate-limit suite needs a client address, and the test server leaves one unset. A start-up
  filter inserted ahead of the pipeline copies a test-only header onto the connection, which is
  what lets the partitioning assertions be about the socket address rather than about a shared
  fallback bucket.

  ```csharp
  // tests/EventBooking.Api.Tests/Conventions/RateLimitTests.cs (complete)
  using System.Net;
  using System.Net.Http.Headers;
  using Microsoft.AspNetCore.Hosting;
  using Microsoft.AspNetCore.Http;
  using Microsoft.Extensions.DependencyInjection;

  namespace EventBooking.Api.Tests.Conventions;

  /// <summary>
  /// Pins the three limits and the forwarded-header trust boundary. Each case uses its own
  /// client address so one case cannot exhaust another's window.
  /// </summary>
  [Collection("api")]
  public sealed class RateLimitTests(ApiFactory factory)
  {
      [Fact]
      public async Task TheThirtyFirstAttendeeRequestInAMinuteFromOneAddressIs429WithRetryAfter()
      {
          using var host = WithRemoteAddress();
          factory.SignedInAs = null;
          var client = host.CreateClient();
          var token = UnknownToken();

          HttpResponseMessage? last = null;
          for (var attempt = 0; attempt < 31; attempt++)
          {
              last = await SendAsync(client, $"/api/booking/{token}", "203.0.113.10");
          }

          Assert.NotNull(last);
          Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
          Assert.NotNull(last.Headers.RetryAfter);
          Assert.True(last.Headers.RetryAfter!.Delta > TimeSpan.Zero);
      }

      /// <summary>
      /// Thirty-one requests from one socket address, each claiming a different forwarded
      /// address. The proxy network is not configured, so the forwarded header is ignored and
      /// all thirty-one share one partition — which is what the 429 proves. Asserting only
      /// that a request succeeded would pass whether or not the header was trusted.
      /// </summary>
      [Fact]
      public async Task AForwardedAddressFromOutsideTheProxyNetworkIsIgnored()
      {
          using var host = WithRemoteAddress();
          factory.SignedInAs = null;
          var client = host.CreateClient();
          var token = UnknownToken();

          HttpResponseMessage? last = null;
          for (var attempt = 0; attempt < 31; attempt++)
          {
              last = await SendAsync(
                  client, $"/api/booking/{token}", "203.0.113.20", $"198.51.100.{attempt}");
          }

          Assert.NotNull(last);
          Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
      }

      /// <summary>
      /// The same thirty-one requests with the socket address inside the configured proxy
      /// network: now each forwarded address gets its own partition and none is rejected. This
      /// is the case that proves the setting is live rather than the limiter being generous.
      /// </summary>
      [Fact]
      public async Task AForwardedAddressFromInsideTheProxyNetworkPartitionsIndependently()
      {
          using var host = WithRemoteAddress(proxyNetwork: "203.0.113.0/24");
          factory.SignedInAs = null;
          var client = host.CreateClient();
          var token = UnknownToken();

          HttpResponseMessage? last = null;
          for (var attempt = 0; attempt < 31; attempt++)
          {
              last = await SendAsync(
                  client, $"/api/booking/{token}", "203.0.113.30", $"198.51.100.{attempt}");
          }

          Assert.NotNull(last);
          Assert.NotEqual(HttpStatusCode.TooManyRequests, last.StatusCode);
      }

      /// <summary>
      /// Eleven requests carrying one token prefix from eleven different addresses. The
      /// per-address allowance is nowhere near exhausted, so only the per-token limit can
      /// reject the eleventh.
      /// </summary>
      [Fact]
      public async Task TheEleventhRequestForOneTokenInAMinuteIs429()
      {
          using var host = WithRemoteAddress();
          factory.SignedInAs = null;
          var client = host.CreateClient();
          var token = UnknownToken();

          HttpResponseMessage? last = null;
          for (var attempt = 0; attempt < 11; attempt++)
          {
              last = await SendAsync(client, $"/api/booking/{token}", $"198.51.100.{100 + attempt}");
          }

          Assert.NotNull(last);
          Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
      }

      [Fact]
      public async Task ARejectedRequestCarriesTheProblemBody()
      {
          using var host = WithRemoteAddress();
          factory.SignedInAs = null;
          var client = host.CreateClient();
          var token = UnknownToken();

          HttpResponseMessage? last = null;
          for (var attempt = 0; attempt < 31; attempt++)
          {
              last = await SendAsync(client, $"/api/booking/{token}", "203.0.113.40");
          }

          Assert.NotNull(last);
          Assert.Equal(
              "application/problem+json", last.Content.Headers.ContentType?.MediaType);
          Assert.Contains("rate-limited", await last.Content.ReadAsStringAsync());
      }

      private static string UnknownToken() =>
          "b" + Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();

      private static async Task<HttpResponseMessage> SendAsync(
          HttpClient client, string url, string remote, string? forwarded = null)
      {
          using var request = new HttpRequestMessage(HttpMethod.Get, url);
          request.Headers.Add(RemoteAddressStartupFilter.Header, remote);
          if (forwarded is not null)
          {
              request.Headers.Add("X-Forwarded-For", forwarded);
          }

          request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
          return await client.SendAsync(request);
      }

      private WebApplicationFactoryHost WithRemoteAddress(string? proxyNetwork = null) =>
          new(factory.WithWebHostBuilder(builder =>
          {
              if (proxyNetwork is not null)
              {
                  builder.UseSetting("Proxy:Networks:0", proxyNetwork);
              }

              builder.ConfigureTestServices(services =>
                  services.AddSingleton<
                      Microsoft.AspNetCore.Hosting.IStartupFilter, RemoteAddressStartupFilter>());
          }));

      /// <summary>Owns the derived host so each case disposes its own pipeline.</summary>
      private sealed class WebApplicationFactoryHost(
          Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> inner) : IDisposable
      {
          public HttpClient CreateClient() => inner.CreateClient();

          public void Dispose() => inner.Dispose();
      }

      /// <summary>
      /// Test-only: the test server sets no client address, so the limiter would see every
      /// request as the one unknown partition. This copies the header onto the connection
      /// before anything else in the pipeline runs.
      /// </summary>
      private sealed class RemoteAddressStartupFilter : IStartupFilter
      {
          public const string Header = "X-Test-Remote-Ip";

          public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
              app =>
              {
                  app.Use(async (context, following) =>
                  {
                      if (context.Request.Headers.TryGetValue(Header, out var value) &&
                          System.Net.IPAddress.TryParse(value.ToString(), out var address))
                      {
                          context.Connection.RemoteIpAddress = address;
                      }

                      await following(context);
                  });
                  next(app);
              };
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Api.Tests/Conventions/StartupValidationTests.cs (complete)
  using EventBooking.Api;
  using Microsoft.Extensions.Configuration;

  namespace EventBooking.Api.Tests.Conventions;

  /// <summary>
  /// Startup validation runs before the host binds a port, so these cases build configuration
  /// directly rather than through the factory: a host that refuses to start has no client.
  /// </summary>
  public sealed class StartupValidationTests
  {
      [Fact]
      public void EveryMissingKeyIsNamedAtOnce()
      {
          var empty = new ConfigurationBuilder().Build();

          var ex = Assert.Throws<InvalidOperationException>(() => EventBookingConfiguration.Read(empty));

          foreach (var key in new[]
          {
              "ConnectionStrings:EventBooking",
              "Auth:Authority",
              "Auth:Audience",
              "Tokens:SigningKey",
              "Email:Smtp:Host",
              "Email:FromAddress",
              "Portal:BaseUrl",
              "Portal:CoordinatorContact",
              "Cors:AllowedOrigins",
          })
          {
              Assert.Contains(key, ex.Message);
          }
      }

      [Fact]
      public void AMissingPortalBaseUrlIsNamedOnItsOwn()
      {
          var configuration = Complete(remove: "Portal:BaseUrl");

          var ex = Assert.Throws<InvalidOperationException>(
              () => EventBookingConfiguration.Read(configuration));

          Assert.Contains("Portal:BaseUrl", ex.Message);
          Assert.DoesNotContain("Tokens:SigningKey", ex.Message);
      }

      [Theory]
      [InlineData("change-me")]
      [InlineData("CHANGE_ME")]
      [InlineData("development-signing-key-development-signing-key")]
      [InlineData("insecure-development-key-insecure-development-key")]
      public void APlaceholderSigningKeyIsRejected(string key)
      {
          var configuration = Complete(signingKey: key);

          var ex = Assert.Throws<InvalidOperationException>(
              () => EventBookingConfiguration.Read(configuration));

          Assert.Contains("Tokens:SigningKey", ex.Message);
          Assert.Contains("placeholder", ex.Message, StringComparison.OrdinalIgnoreCase);
      }

      [Fact]
      public void ASigningKeyShorterThanThirtyTwoBytesIsRejected()
      {
          var configuration = Complete(signingKey: new string('k', 31));

          var ex = Assert.Throws<InvalidOperationException>(
              () => EventBookingConfiguration.Read(configuration));

          Assert.Contains("32", ex.Message);
      }

      [Fact]
      public void ACompleteConfigurationIsAccepted()
      {
          var settings = EventBookingConfiguration.Read(Complete());

          Assert.Equal("https://portal.example.com", settings.Portal.BaseUrl);
          Assert.Equal(30, settings.RateLimits.AttendeePerMinute);
          Assert.Equal(10, settings.RateLimits.TokenPerMinute);
          Assert.Equal(300, settings.RateLimits.StaffPerMinute);
      }

      [Fact]
      public void TheAttendeeLimitIsConfigurable()
      {
          var configuration = Complete(extra: new() { ["RateLimiting:AttendeePerMinute"] = "5" });

          var settings = EventBookingConfiguration.Read(configuration);

          Assert.Equal(5, settings.RateLimits.AttendeePerMinute);
      }

      /// <summary>Every required key present and valid, with one override or removal applied.</summary>
      private static IConfiguration Complete(
          string? remove = null,
          string signingKey = "a-signing-key-that-is-at-least-32-bytes-long",
          Dictionary<string, string?>? extra = null)
      {
          var values = new Dictionary<string, string?>
          {
              ["ConnectionStrings:EventBooking"] = "Host=localhost;Database=x;Username=x;Password=x",
              ["Auth:Authority"] = "https://id.example.com/realms/eventbooking",
              ["Auth:Audience"] = "eventbooking-api",
              ["Tokens:SigningKey"] = signingKey,
              ["Email:Smtp:Host"] = "mail.example.com",
              ["Email:Smtp:Port"] = "1025",
              ["Email:FromAddress"] = "events@example.com",
              ["Email:FromName"] = "Events Team",
              ["Portal:BaseUrl"] = "https://portal.example.com",
              ["Portal:CoordinatorContact"] = "events@example.com",
              ["Cors:AllowedOrigins:0"] = "https://portal.example.com",
          };

          if (remove is not null)
          {
              values.Remove(remove);
          }

          foreach (var pair in extra ?? [])
          {
              values[pair.Key] = pair.Value;
          }

          return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
      }
  }
  ```

  The redaction suite drives the two request shapes that carry the data design 08 forbids in
  logs: an attendee token in the path, and an email address in a request body. Neither case
  needs a seeded `Invite`, because what is under test is the logging pipeline, not the handler —
  a request that fails still logs, and a request body that is rejected has still been read.

  ```csharp
  // tests/EventBooking.Api.Tests/Conventions/LogRedactionTests.cs (complete)
  using System.Net.Http.Json;
  using System.Text.Json;
  using EventBooking.Domain.Access;
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.Extensions.Logging;

  namespace EventBooking.Api.Tests.Conventions;

  /// <summary>Design 08: operational logs never carry a name, email address or token.</summary>
  [Collection("api")]
  public sealed class LogRedactionTests(ApiFactory factory)
  {
      [Fact]
      public async Task ABookingRequestLogsNeitherItsTokenNorAnyEmailAddress()
      {
          var sink = new CapturingLoggerProvider();
          using var host = factory.WithWebHostBuilder(builder =>
              builder.ConfigureTestServices(services =>
                  services.AddSingleton<ILoggerProvider>(sink)));
          var client = host.CreateClient();
          var token = "b" + Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();

          await client.GetAsync($"/api/booking/{token}");

          var logged = sink.Text();
          Assert.DoesNotContain(token, logged, StringComparison.OrdinalIgnoreCase);
          Assert.DoesNotContain("@", logged, StringComparison.Ordinal);
      }

      [Fact]
      public async Task ACreateAttendeeRequestLogsNeitherTheNameNorTheEmail()
      {
          var sink = new CapturingLoggerProvider();
          using var host = factory.WithWebHostBuilder(builder =>
              builder.ConfigureTestServices(services =>
                  services.AddSingleton<ILoggerProvider>(sink)));
          factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
          factory.StaffIdClaim = "U500003";
          var client = host.CreateClient();

          await client.PostAsJsonAsync("/api/attendees", new
          {
              name = "Marguerite Hathaway",
              email = "marguerite.hathaway@example.com",
              attendeeGroupId = Guid.NewGuid(),
          });

          var logged = sink.Text();
          Assert.DoesNotContain("Marguerite", logged, StringComparison.OrdinalIgnoreCase);
          Assert.DoesNotContain("marguerite.hathaway@example.com", logged, StringComparison.OrdinalIgnoreCase);
      }

      /// <summary>Every request log line carries the correlation identifier it was served under.</summary>
      [Fact]
      public async Task EveryRequestCarriesACorrelationIdentifierInItsResponseAndItsLogs()
      {
          var sink = new CapturingLoggerProvider();
          using var host = factory.WithWebHostBuilder(builder =>
              builder.ConfigureTestServices(services =>
                  services.AddSingleton<ILoggerProvider>(sink)));
          factory.SignedInAs = null;
          var client = host.CreateClient();

          var response = await client.GetAsync("/health/live");

          var correlation = Assert.Single(response.Headers.GetValues("X-Correlation-Id"));
          Assert.False(string.IsNullOrWhiteSpace(correlation));
          Assert.Contains(correlation, sink.Text(), StringComparison.Ordinal);
      }

      /// <summary>A supplied traceparent is adopted rather than replaced.</summary>
      [Fact]
      public async Task ASuppliedTraceparentBecomesTheCorrelationIdentifier()
      {
          factory.SignedInAs = null;
          var client = factory.CreateClient();
          const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
          using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
          request.Headers.Add("traceparent", $"00-{TraceId}-00f067aa0ba902b7-01");

          var response = await client.SendAsync(request);

          Assert.Equal(TraceId, Assert.Single(response.Headers.GetValues("X-Correlation-Id")));
      }

      /// <summary>Captures every log line the host writes while one case runs.</summary>
      private sealed class CapturingLoggerProvider : ILoggerProvider
      {
          private readonly List<string> _lines = [];

          public string Text()
          {
              lock (_lines)
              {
                  return string.Join('\n', _lines);
              }
          }

          public ILogger CreateLogger(string categoryName) => new Sink(_lines);

          public void Dispose()
          {
          }

          private sealed class Sink(List<string> lines) : ILogger
          {
              public IDisposable? BeginScope<TState>(TState state)
                  where TState : notnull
              {
                  lock (lines)
                  {
                      lines.Add(state.ToString() ?? string.Empty);
                  }

                  return null;
              }

              public bool IsEnabled(LogLevel logLevel) => true;

              public void Log<TState>(
                  LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                  Func<TState, Exception?, string> formatter)
              {
                  lock (lines)
                  {
                      lines.Add(formatter(state, exception));
                      if (exception is not null)
                      {
                          lines.Add(exception.ToString());
                      }
                  }
              }
          }
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Api.Tests/Conventions/EventTimeContractTests.cs (complete)
  using EventBooking.Api.Contracts;
  using EventBooking.Infrastructure.Time;

  namespace EventBooking.Api.Tests.Conventions;

  /// <summary>
  /// The one event-time representation, proved in two zones. London and Tokyo, not London and
  /// Dublin: contradiction #10 — two zones sharing an offset cannot demonstrate that the
  /// conversion is zone-dependent at all.
  /// </summary>
  public sealed class EventTimeContractTests
  {
      private static readonly NodaTimeEventWindowZones Zones = new();

      [Fact]
      public void ALondonSummerWindowCarriesItsOffsetAndAbbreviation()
      {
          var time = EventTimeResponse.From(
              new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 90, "Europe/London", Zones);

          Assert.Equal(new DateOnly(2026, 10, 14), time.Date);
          Assert.Equal(new TimeOnly(9, 30), time.StartTime);
          Assert.Equal(90, time.DurationMinutes);
          Assert.Equal(TimeSpan.FromHours(1), time.StartLocal.Offset);
          Assert.Equal(new TimeOnly(11, 0), TimeOnly.FromTimeSpan(time.EndLocal.TimeOfDay));
          Assert.Equal(TimeSpan.Zero, time.StartUtc.Offset);
          Assert.Equal(new DateTimeOffset(2026, 10, 14, 8, 30, 0, TimeSpan.Zero), time.StartUtc);
          Assert.Equal(new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero), time.EndUtc);
          Assert.Equal("Europe/London", time.TimeZoneId);
          Assert.Equal("BST", time.ZoneAbbreviation);
      }

      [Fact]
      public void TheSameWallClockInTokyoIsADifferentInstant()
      {
          var london = EventTimeResponse.From(
              new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 90, "Europe/London", Zones);
          var tokyo = EventTimeResponse.From(
              new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 90, "Asia/Tokyo", Zones);

          Assert.Equal(london.Date, tokyo.Date);
          Assert.Equal(london.StartTime, tokyo.StartTime);
          Assert.NotEqual(london.StartUtc, tokyo.StartUtc);
          Assert.Equal(TimeSpan.FromHours(9), tokyo.StartLocal.Offset);
      }

      [Fact]
      public void AWinterLondonWindowCarriesTheWinterAbbreviation()
      {
          var time = EventTimeResponse.From(
              new DateOnly(2026, 12, 2), new TimeOnly(9, 30), 60, "Europe/London", Zones);

          Assert.Equal("GMT", time.ZoneAbbreviation);
          Assert.Equal(TimeSpan.Zero, time.StartLocal.Offset);
      }

      /// <summary>The end is derived, never stored, so it moves with the duration alone.</summary>
      [Fact]
      public void TheEndIsDerivedFromTheDuration()
      {
          var shorter = EventTimeResponse.From(
              new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 15, "Europe/London", Zones);
          var longer = EventTimeResponse.From(
              new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 720, "Europe/London", Zones);

          Assert.Equal(shorter.StartUtc, longer.StartUtc);
          Assert.Equal(TimeSpan.FromMinutes(15), shorter.EndUtc - shorter.StartUtc);
          Assert.Equal(TimeSpan.FromMinutes(720), longer.EndUtc - longer.StartUtc);
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Api.Tests/Conventions/HealthAndMetricsTests.cs (complete)
  using System.Net;

  namespace EventBooking.Api.Tests.Conventions;

  /// <summary>The two probes and the metrics endpoint design 08 names.</summary>
  [Collection("api")]
  public sealed class HealthAndMetricsTests(ApiFactory factory)
  {
      [Fact]
      public async Task LivenessAnswersAnonymously()
      {
          factory.SignedInAs = null;

          var response = await factory.CreateClient().GetAsync("/health/live");

          Assert.Equal(HttpStatusCode.OK, response.StatusCode);
      }

      /// <summary>Readiness reaches the database, so it can only pass with the container up.</summary>
      [Fact]
      public async Task ReadinessReportsTheDatabase()
      {
          factory.SignedInAs = null;

          var response = await factory.CreateClient().GetAsync("/health/ready");

          Assert.Equal(HttpStatusCode.OK, response.StatusCode);
          Assert.Contains("database", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
      }

      [Fact]
      public async Task MetricsAreExposedInPrometheusTextFormat()
      {
          factory.SignedInAs = null;
          var client = factory.CreateClient();
          await client.GetAsync("/health/live");

          var response = await client.GetAsync("/metrics");

          Assert.Equal(HttpStatusCode.OK, response.StatusCode);
          Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
          var body = await response.Content.ReadAsStringAsync();
          Assert.Contains("# TYPE eventbooking_http_requests_total counter", body, StringComparison.Ordinal);
          Assert.Contains("eventbooking_http_request_duration_seconds", body, StringComparison.Ordinal);
          Assert.Contains("eventbooking_capacity_exhausted_total", body, StringComparison.Ordinal);
      }

      /// <summary>A metric label never carries a route parameter value.</summary>
      [Fact]
      public async Task MetricLabelsCarryTheRoutePatternRatherThanItsValues()
      {
          factory.SignedInAs = null;
          var client = factory.CreateClient();
          var token = "b" + Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
          await client.GetAsync($"/api/booking/{token}");

          var body = await client.GetStringAsync("/metrics");

          Assert.DoesNotContain(token, body, StringComparison.OrdinalIgnoreCase);
          Assert.Contains("/api/booking/{token}", body, StringComparison.Ordinal);
      }
  }
  ```

- [ ] **Step 2: Run.** Expected: FAIL.

  The catalogue suite fails on the missing type; the cursor and page suites fail to compile
  against types that do not exist yet; the pagination walk fails because the ported attendee
  list returns a bare array with no cursor; the idempotency suite fails because no middleware
  and no table exist; the rate-limit suite fails on the missing token-prefix and proxy-network
  behaviour; the startup suite fails because the current reader validates a different set of
  keys; the redaction suite fails because no correlation middleware writes the header; and the
  health suite fails because `/health/live`, `/health/ready` and `/metrics` are not mapped.

  ```bash
  dotnet test tests/EventBooking.Api.Tests --filter "FullyQualifiedName~Conventions"
  ```

- [ ] **Step 3: Implement.** Every file below is complete. Take them in this order:
  the catalogue and the writer first, because the rest of the task returns its bodies.

  **The problem catalogue and the result writer.**

  ```csharp
  // src/EventBooking.Api/Endpoints/ProblemCatalogue.cs (complete)
  using EventBooking.Application.Common;

  namespace EventBooking.Api.Endpoints;

  /// <summary>One wire contract for one failure: its slug, status, title and data member.</summary>
  /// <param name="Type">The stable slug from design 05's error catalogue.</param>
  /// <param name="Status">The HTTP status design 05 gives the slug.</param>
  /// <param name="Title">The short, caller-safe title.</param>
  /// <param name="DataMember">The extension carrying the error's numeric map, if any.</param>
  /// <param name="ScalarKey">The single key to lift out where the design gives a bare number.</param>
  public sealed record ProblemShape(
      string Type, int Status, string Title, string? DataMember = null, string? ScalarKey = null);

  /// <summary>
  /// Every failure the API can return, keyed by the application error code that produces it.
  /// Design 05's table supplies seventeen slugs; four more close failures the table does not
  /// name and the application already produces. A code with no row here is a programming
  /// error — <see cref="For"/> throws rather than letting an expected failure become a 500.
  /// </summary>
  public static class ProblemCatalogue
  {
      /// <summary>The code the rate limiter reports under; no application error produces it.</summary>
      public const string RateLimitedCode = "rate-limited";

      /// <summary>The code the authentication challenge reports under.</summary>
      public const string UnauthenticatedCode = "unauthenticated";

      /// <summary>The code the cursor and limit binders report under.</summary>
      public const string ValidationCode = "validation";

      /// <summary>Gets every catalogued failure, keyed by application error code.</summary>
      public static IReadOnlyDictionary<string, ProblemShape> ByErrorCode { get; } =
          new Dictionary<string, ProblemShape>(StringComparer.Ordinal)
          {
              // 422 — a field or row the caller can correct.
              [ValidationCode] = Validation,
              ["attendee_group_required"] = Validation,
              ["attendee_group_unknown"] = Validation,
              ["attendee_group_inactive"] = Validation,
              ["attendee_group_unmapped"] = Validation,

              // 409 — the request was well formed and the state refused it.
              [Error.VersionConflictCode] = new(
                  "version-conflict", StatusCodes.Status409Conflict,
                  "Someone else changed this first.", "current"),
              [Error.AppointmentVersionConflictCode] = new(
                  "version-conflict", StatusCodes.Status409Conflict,
                  "Someone else changed this first.", "current"),
              [Error.ConfirmationRequiredCode] = new(
                  "confirmation-required", StatusCodes.Status409Conflict,
                  "This action needs confirming.", "consequence"),
              [Error.CapacityExhaustedCode] = new(
                  "capacity-exhausted", StatusCodes.Status409Conflict,
                  "This time is no longer available."),
              [Error.CapacityBelowBookingsCode] = new(
                  "capacity-below-bookings", StatusCodes.Status409Conflict,
                  "That total is below the places already booked.", "minimum", "minimum"),
              [Error.ProposalNotOpenCode] = new(
                  "proposal-not-open", StatusCodes.Status409Conflict,
                  "This proposal is no longer open."),
              [Error.WindowStartedCode] = new(
                  "window-started", StatusCodes.Status409Conflict,
                  "This event has already started."),
              [Error.ReferenceDataInUseCode] = new(
                  "in-use", StatusCodes.Status409Conflict,
                  "This is still in use.", "blocking"),
              [Error.RequirementsLockedCode] = new(
                  "requirements-locked", StatusCodes.Status409Conflict,
                  "Requirements cannot change while bookings are active.", "blocking"),
              [Error.AttendeeGroupActiveBookingConflictCode] = new(
                  "requirements-locked", StatusCodes.Status409Conflict,
                  "Requirements cannot change while bookings are active.", "blocking"),
              [Error.InsufficientEventsCode] = new(
                  "insufficient-events", StatusCodes.Status409Conflict,
                  "There are not enough events to offer."),
              [Error.RecoveryActiveCode] = new(
                  "recovery-active", StatusCodes.Status409Conflict,
                  "A recovery is already under way."),
              [Error.RecoveryAlreadyPendingCode] = new(
                  "recovery-active", StatusCodes.Status409Conflict,
                  "A recovery is already under way."),
              [Error.RecoveryStateChangedCode] = new(
                  "recovery-active", StatusCodes.Status409Conflict,
                  "A recovery is already under way."),
              [Error.LastAdminCode] = new(
                  "last-admin", StatusCodes.Status409Conflict,
                  "That change would leave no administrator."),

              // 409 — the four slugs design 05's table does not name (see the settlements above).
              [Error.RequirementMismatchCode] = new(
                  "requirement-mismatch", StatusCodes.Status409Conflict,
                  "This attendee's requirements have changed."),
              [Error.RecoveryNotAvailableCode] = new(
                  "requirement-mismatch", StatusCodes.Status409Conflict,
                  "This attendee's requirements have changed."),
              [Error.AttendeeRequirementSnapshotMismatchCode] = new(
                  "requirement-mismatch", StatusCodes.Status409Conflict,
                  "This attendee's requirements have changed."),
              [Error.AlreadyConfirmedCode] = new(
                  "already-confirmed", StatusCodes.Status409Conflict,
                  "This invitation has already been used."),
              ["conflict"] = new(
                  "conflict", StatusCodes.Status409Conflict, "That is not possible right now."),

              // 404, 410 — the attendee token pair. Unknown, forged, superseded and cancelled
              // are one answer; only a lapsed expiry is distinguishable (settled with the user).
              [Error.TokenInvalidCode] = new(
                  "token-invalid", StatusCodes.Status404NotFound, "This link is not valid."),
              ["not_found"] = new(
                  "not-found", StatusCodes.Status404NotFound, "That was not found."),
              [Error.TokenExpiredCode] = new(
                  "token-expired", StatusCodes.Status410Gone, "This link has expired."),

              // 403, 401, 429 — the boundary. A missing staff number is forbidden, not
              // unauthenticated: contradiction #3, settled in Phase 3.
              ["forbidden"] = new(
                  "forbidden", StatusCodes.Status403Forbidden, "You cannot do that."),
              [UnauthenticatedCode] = new(
                  "unauthenticated", StatusCodes.Status401Unauthorized, "Please sign in."),
              [RateLimitedCode] = new(
                  "rate-limited", StatusCodes.Status429TooManyRequests, "Too many requests."),
          };

      /// <summary>Returns the catalogued shape for an application error code.</summary>
      /// <param name="errorCode">The application error code.</param>
      /// <returns>The shape to render.</returns>
      /// <exception cref="InvalidOperationException">Thrown when the code has no row.</exception>
      public static ProblemShape For(string errorCode) =>
          ByErrorCode.TryGetValue(errorCode, out var shape)
              ? shape
              : throw new InvalidOperationException(
                  $"Application error code '{errorCode}' has no problem-catalogue row. " +
                  "Add one rather than letting an expected failure reach a caller as a 500.");

      private static ProblemShape Validation => new(
          "validation-failed", StatusCodes.Status422UnprocessableEntity,
          "The request could not be accepted.");
  }
  ```

  ```csharp
  // src/EventBooking.Api/Endpoints/ResultResponses.cs (complete, replacing the ported file)
  using EventBooking.Application.Common;
  using Microsoft.AspNetCore.Mvc;

  namespace EventBooking.Api.Endpoints;

  /// <summary>One field or row error inside an RFC 9457 body.</summary>
  /// <param name="Field">The request field at fault, when one can be named.</param>
  /// <param name="Line">The CSV line at fault, when one can be named.</param>
  /// <param name="Code">The machine-readable reason.</param>
  /// <param name="Message">The caller-safe explanation.</param>
  public sealed record ProblemError(string? Field, int? Line, string Code, string Message);

  /// <summary>Maps application results to the API's RFC 9457 response contract.</summary>
  public static class ResultResponses
  {
      /// <summary>Returns no content for success or problem details for failure.</summary>
      /// <param name="result">The application result.</param>
      /// <returns>The HTTP result.</returns>
      public static IResult ToResponse(this Result result) =>
          result.IsSuccess ? Results.NoContent() : Problem(result.Error);

      /// <summary>Returns the result value for success or problem details for failure.</summary>
      /// <param name="result">The application result.</param>
      /// <returns>The HTTP result.</returns>
      public static IResult ToResponse<T>(this Result<T> result) =>
          result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);

      /// <summary>Projects a successful value before returning it, or renders the failure.</summary>
      /// <param name="result">The application result.</param>
      /// <param name="projection">The response projection.</param>
      /// <returns>The HTTP result.</returns>
      public static IResult ToResponse<T, TResponse>(
          this Result<T> result, Func<T, TResponse> projection) =>
          result.IsSuccess ? Results.Ok(projection(result.Value)) : Problem(result.Error);

      /// <summary>Returns a created result for success or problem details for failure.</summary>
      /// <param name="result">The application result.</param>
      /// <param name="location">The location of the created resource.</param>
      /// <returns>The HTTP result.</returns>
      public static IResult ToCreated<T>(this Result<T> result, Func<T, string> location) =>
          result.IsSuccess
              ? Results.Created(location(result.Value), result.Value)
              : Problem(result.Error);

      /// <summary>Renders one field error as the catalogue's validation-failed body.</summary>
      /// <param name="field">The field at fault.</param>
      /// <param name="code">The machine-readable reason.</param>
      /// <param name="message">The caller-safe explanation.</param>
      /// <returns>The HTTP result.</returns>
      public static IResult ValidationFailed(string field, string code, string message)
      {
          var shape = ProblemCatalogue.For(ProblemCatalogue.ValidationCode);
          var body = Body(shape, message, [new ProblemError(field, null, code, message)]);
          return Results.Problem(body);
      }

      /// <summary>Renders the first call of a two-step action, carrying its consequence.</summary>
      /// <param name="detail">The caller-safe explanation.</param>
      /// <param name="consequence">The effects confirming would have.</param>
      /// <returns>The HTTP result.</returns>
      public static IResult ConfirmationRequired(
          string detail, IReadOnlyDictionary<string, long> consequence)
      {
          var shape = ProblemCatalogue.For(Error.ConfirmationRequiredCode);
          var body = Body(shape, detail, [new ProblemError(null, null, shape.Type, detail)]);
          body.Extensions["consequence"] = consequence;
          return Results.Problem(body);
      }

      /// <summary>Renders one application error as its catalogued problem body.</summary>
      /// <param name="error">The application error.</param>
      /// <returns>The problem body, for a test or a caller that needs it before writing.</returns>
      public static ProblemDetails ProblemBodyFor(Error error)
      {
          var shape = ProblemCatalogue.For(error.Code);
          var body = Body(
              shape, error.Message, [new ProblemError(null, null, shape.Type, error.Message)]);

          if (shape.DataMember is not null && error.Data is { Count: > 0 } data)
          {
              body.Extensions[shape.DataMember] = shape.ScalarKey is { } key
                  ? data.TryGetValue(key, out var scalar) ? scalar : null
                  : data;
          }

          return body;
      }

      private static IResult Problem(Error error) => Results.Problem(ProblemBodyFor(error));

      private static ProblemDetails Body(
          ProblemShape shape, string detail, IReadOnlyList<ProblemError> errors)
      {
          var body = new ProblemDetails
          {
              Type = shape.Type,
              Title = shape.Title,
              Status = shape.Status,
              Detail = detail,
          };
          body.Extensions["errors"] = errors;
          return body;
      }
  }
  ```

  The slug goes in `type` as the design writes it — a bare slug, not a resolvable URI. RFC 9457
  permits a relative reference, and a caller matching on the string is what design 05 asks for;
  minting `https://` URIs nobody serves would be worse.

  ```csharp
  // src/EventBooking.Api/Pagination/PageCursor.cs (complete)
  using System.Buffers.Text;
  using System.Security.Cryptography;
  using System.Text;

  namespace EventBooking.Api.Pagination;

  /// <summary>
  /// The opaque keyset cursor: base64url(payload) + "." + base64url(HMAC-SHA256(key, payload)).
  /// The signature is checked in constant time before the payload is read, so a tampered or
  /// forged cursor costs one hash and never reaches a query. There is no offset pagination.
  /// </summary>
  /// <param name="signingKey">The signing key, shared with the attendee token service.</param>
  public sealed class PageCursor(byte[] signingKey)
  {
      /// <summary>Signs and encodes one sort-key payload.</summary>
      /// <param name="payload">The keyset payload the query produced.</param>
      /// <returns>The opaque cursor.</returns>
      public string Protect(string payload)
      {
          ArgumentNullException.ThrowIfNull(payload);
          var bytes = Encoding.UTF8.GetBytes(payload);
          var signature = HMACSHA256.HashData(signingKey, bytes);
          return Base64Url.EncodeToString(bytes) + "." + Base64Url.EncodeToString(signature);
      }

      /// <summary>Verifies and decodes one cursor.</summary>
      /// <param name="cursor">The cursor supplied by the caller.</param>
      /// <param name="payload">Receives the payload when the cursor verifies.</param>
      /// <returns>Whether the cursor verified.</returns>
      public bool TryUnprotect(string? cursor, out string payload)
      {
          payload = string.Empty;
          if (string.IsNullOrEmpty(cursor))
          {
              return false;
          }

          var separator = cursor.IndexOf('.', StringComparison.Ordinal);
          if (separator <= 0 || separator == cursor.Length - 1)
          {
              return false;
          }

          byte[] bytes;
          byte[] supplied;
          try
          {
              bytes = Base64Url.DecodeFromChars(cursor.AsSpan(0, separator));
              supplied = Base64Url.DecodeFromChars(cursor.AsSpan(separator + 1));
          }
          catch (FormatException)
          {
              return false;
          }

          var expected = HMACSHA256.HashData(signingKey, bytes);
          if (!CryptographicOperations.FixedTimeEquals(expected, supplied))
          {
              return false;
          }

          payload = Encoding.UTF8.GetString(bytes);
          return true;
      }
  }
  ```

  ```csharp
  // src/EventBooking.Api/Pagination/PageRequest.cs (complete)
  namespace EventBooking.Api.Pagination;

  /// <summary>The bound query string every list endpoint takes.</summary>
  /// <param name="Cursor">The opaque cursor, or null for the first page.</param>
  /// <param name="Limit">The page size, within the design's bounds.</param>
  public sealed record PageRequest(string? Cursor, int Limit)
  {
      /// <summary>The page size used when the caller supplies none (design 08).</summary>
      public const int DefaultLimit = 50;

      /// <summary>The smallest page size the API accepts.</summary>
      public const int MinLimit = 1;

      /// <summary>The largest page size the API accepts.</summary>
      public const int MaxLimit = 200;

      /// <summary>
      /// Binds the two query-string values. A limit outside the range is a field error rather
      /// than a silent clamp: a caller asking for 500 rows and quietly getting 200 cannot tell
      /// a short page from the end of the collection.
      /// </summary>
      /// <param name="cursor">The raw cursor value.</param>
      /// <param name="limit">The raw limit value.</param>
      /// <param name="request">Receives the bound request.</param>
      /// <param name="field">Receives the offending field name when binding fails.</param>
      /// <returns>Whether the values bound.</returns>
      public static bool TryBind(string? cursor, int? limit, out PageRequest request, out string? field)
      {
          field = null;
          request = new PageRequest(cursor, DefaultLimit);
          if (limit is null)
          {
              return true;
          }

          if (limit < MinLimit || limit > MaxLimit)
          {
              field = "limit";
              return false;
          }

          request = new PageRequest(cursor, limit.Value);
          return true;
      }
  }

  /// <summary>The one list envelope: items already projected, and the next cursor or null.</summary>
  /// <param name="Items">The page's rows.</param>
  /// <param name="NextCursor">The cursor for the following page, or null on the last.</param>
  public sealed record Page<T>(IReadOnlyList<T> Items, string? NextCursor);
  ```

  ```csharp
  // src/EventBooking.Api/Contracts/EventTimeResponse.cs (complete)
  using EventBooking.Domain.Time;

  namespace EventBooking.Api.Contracts;

  /// <summary>
  /// The one representation of an EventWindow on the wire (design 05, "Times"). Every response
  /// carrying a window uses this record, so no two endpoints can disagree about what 09:30
  /// means. The end is derived from the duration in the location's own wall clock, never stored.
  /// </summary>
  /// <param name="Date">The local calendar date at the location.</param>
  /// <param name="StartTime">The local start time of day.</param>
  /// <param name="DurationMinutes">The window length in minutes.</param>
  /// <param name="StartLocal">The start instant carrying the location's offset.</param>
  /// <param name="EndLocal">The end instant carrying the location's offset.</param>
  /// <param name="StartUtc">The start instant in UTC.</param>
  /// <param name="EndUtc">The end instant in UTC.</param>
  /// <param name="TimeZoneId">The location's IANA zone identifier.</param>
  /// <param name="ZoneAbbreviation">The abbreviation in force at the start, for display.</param>
  public sealed record EventTimeResponse(
      DateOnly Date,
      TimeOnly StartTime,
      int DurationMinutes,
      DateTimeOffset StartLocal,
      DateTimeOffset EndLocal,
      DateTimeOffset StartUtc,
      DateTimeOffset EndUtc,
      string TimeZoneId,
      string ZoneAbbreviation)
  {
      /// <summary>Derives every member from a stored window and its location's zone.</summary>
      /// <param name="date">The local calendar date.</param>
      /// <param name="startTime">The local start time of day.</param>
      /// <param name="durationMinutes">The window length in minutes.</param>
      /// <param name="timeZoneId">The location's IANA zone identifier.</param>
      /// <param name="zones">The zone resolver.</param>
      /// <returns>The wire representation.</returns>
      public static EventTimeResponse From(
          DateOnly date, TimeOnly startTime, int durationMinutes, string timeZoneId,
          IEventWindowZones zones)
      {
          ArgumentNullException.ThrowIfNull(zones);

          // The attendee-facing window is wall clock, so the end is the local end time rather
          // than the start instant plus the duration. The two differ only across a daylight
          // saving change, and the EventWindow invariant already refuses a window whose end has
          // no unique instant, so this cannot silently produce a wrong answer.
          var endTime = startTime.Add(TimeSpan.FromMinutes(durationMinutes));
          var startLocal = zones.InstantOf(date, startTime, timeZoneId);
          var endLocal = zones.InstantOf(date, endTime, timeZoneId);

          return new EventTimeResponse(
              date,
              startTime,
              durationMinutes,
              startLocal,
              endLocal,
              startLocal.ToUniversalTime(),
              endLocal.ToUniversalTime(),
              timeZoneId,
              zones.AbbreviationOf(startLocal, timeZoneId));
      }
  }
  ```

  ```csharp
  // src/EventBooking.Api/Contracts/CallerLinks.cs (complete)
  namespace EventBooking.Api.Contracts;

  /// <summary>One candidate affordance and the capability that unlocks it.</summary>
  /// <param name="Rel">The stable relation name.</param>
  /// <param name="OperationId">The OpenAPI operation id.</param>
  /// <param name="Href">The root-relative URI.</param>
  /// <param name="RequiredCapability">The capability needed, or null when unconditional.</param>
  public sealed record LinkCandidate(
      string Rel, string OperationId, string Href, string? RequiredCapability);

  /// <summary>
  /// Builds the `_links` map a representation carries. Design 05: each representation carries
  /// links to the actions the caller is currently permitted to take, which is what lets the Web
  /// front end enable or disable a control without restating the authorization matrix. The
  /// capabilities come from the caller's access profile, which the request has already
  /// resolved — this function decides nothing, it only filters.
  /// </summary>
  public static class CallerLinks
  {
      /// <summary>Returns the candidates the caller's capabilities permit.</summary>
      /// <param name="capabilities">The capability names the caller currently holds.</param>
      /// <param name="candidates">Every affordance the representation could carry.</param>
      /// <returns>The permitted affordances, keyed by relation name.</returns>
      public static IReadOnlyDictionary<string, ApiLink> For(
          IReadOnlySet<string> capabilities, params LinkCandidate[] candidates)
      {
          ArgumentNullException.ThrowIfNull(capabilities);
          ArgumentNullException.ThrowIfNull(candidates);

          var links = new Dictionary<string, ApiLink>(StringComparer.Ordinal);
          foreach (var candidate in candidates)
          {
              if (candidate.RequiredCapability is null ||
                  capabilities.Contains(candidate.RequiredCapability))
              {
                  var operation = OpenApi.AgentOperationCatalog.Get(candidate.OperationId);
                  links[candidate.Rel] = new ApiLink(
                      candidate.Href, operation.Method, candidate.OperationId);
              }
          }

          return links;
      }
  }
  ```

  Looking the method up in the operation catalogue rather than passing it means a link can never
  name a method the OpenAPI document does not declare for that operation, and an unknown
  operation id throws at the first request rather than shipping a dead relation.

  **The correlation identifier, the metrics and the probes.**

  ```csharp
  // src/EventBooking.Application/Abstractions/ICorrelationContext.cs (complete)
  namespace EventBooking.Application.Abstractions;

  /// <summary>
  /// The correlation identifier the current work is running under. Set once per request by the
  /// API middleware, and once per run by the sweep and the outbox dispatcher. It lives in the
  /// Application project so that anything staging an outbox row can read it without the
  /// Infrastructure or Application projects referencing the API project.
  /// </summary>
  public interface ICorrelationContext
  {
      /// <summary>Gets the identifier the current work is running under.</summary>
      string CorrelationId { get; }

      /// <summary>Runs the enclosing scope under an identifier.</summary>
      /// <param name="correlationId">The identifier to adopt.</param>
      /// <returns>A handle that restores the previous identifier when disposed.</returns>
      IDisposable Begin(string correlationId);
  }

  /// <summary>
  /// The ambient implementation. Registered as a singleton: the value is carried by the
  /// execution context, not by the instance, so a scoped registration would buy nothing and a
  /// background run would see an empty value.
  /// </summary>
  public sealed class AsyncLocalCorrelationContext : ICorrelationContext
  {
      private static readonly AsyncLocal<string?> Current = new();

      /// <summary>The identifier used when nothing has begun a scope.</summary>
      public const string None = "none";

      /// <inheritdoc />
      public string CorrelationId => Current.Value ?? None;

      /// <inheritdoc />
      public IDisposable Begin(string correlationId)
      {
          var previous = Current.Value;
          Current.Value = correlationId;
          return new Scope(previous);
      }

      private sealed class Scope(string? previous) : IDisposable
      {
          public void Dispose() => Current.Value = previous;
      }
  }
  ```

  ```csharp
  // src/EventBooking.Api/Observability/CorrelationMiddleware.cs (complete)
  using System.Diagnostics;
  using EventBooking.Application.Abstractions;

  namespace EventBooking.Api.Observability;

  /// <summary>
  /// Adopts the caller's trace identifier, or mints one, and puts it on the logging scope, the
  /// response and the ambient correlation context. Design 08: a correlation id taken from
  /// traceparent or generated, propagated to background work and written into outbox rows.
  /// </summary>
  /// <param name="next">The following middleware.</param>
  /// <param name="correlation">The ambient correlation context.</param>
  /// <param name="logger">The logger the scope is opened on.</param>
  public sealed class CorrelationMiddleware(
      RequestDelegate next,
      ICorrelationContext correlation,
      ILogger<CorrelationMiddleware> logger)
  {
      /// <summary>The response header carrying the identifier back to the caller.</summary>
      public const string HeaderName = "X-Correlation-Id";

      /// <summary>Runs the request under a correlation identifier.</summary>
      /// <param name="context">The request context.</param>
      /// <returns>A task tracking the request.</returns>
      public async Task InvokeAsync(HttpContext context)
      {
          var correlationId = FromTraceparent(context) ?? Activity.Current?.TraceId.ToString()
              ?? Guid.NewGuid().ToString("n");

          context.Response.Headers[HeaderName] = correlationId;
          using var ambient = correlation.Begin(correlationId);
          using var scope = logger.BeginScope(new Dictionary<string, object>
          {
              ["correlationId"] = correlationId,
              ["route"] = context.Request.Path.Value ?? string.Empty,
          });

          await next(context);
      }

      /// <summary>
      /// The W3C form is version-traceid-spanid-flags. Only the trace id is adopted: a span id
      /// identifies the caller's own operation, and reusing it would make two requests from one
      /// trace indistinguishable in this service's logs.
      /// </summary>
      private static string? FromTraceparent(HttpContext context)
      {
          if (!context.Request.Headers.TryGetValue("traceparent", out var header))
          {
              return null;
          }

          var parts = header.ToString().Split('-');
          return parts.Length >= 3 && parts[1].Length == 32 ? parts[1] : null;
      }
  }
  ```

  The route goes on the logging scope as the raw path, which is a value the caller controls. It
  is bounded by the server's URL limit and it is the one piece of request data an operator needs
  to find a failing call. Nothing else from the request reaches a log line: the redaction suite
  asserts that no token and no email address does.

  ```csharp
  // src/EventBooking.Api/Observability/EventBookingMetrics.cs (complete)
  using System.Diagnostics.Metrics;

  namespace EventBooking.Api.Observability;

  /// <summary>
  /// The instruments design 08 names, on one meter. System.Diagnostics.Metrics is the
  /// OpenTelemetry metrics API in .NET, so this is an OpenTelemetry meter without taking a
  /// dependency on a pre-release exporter package; PrometheusText renders it.
  /// </summary>
  public sealed class EventBookingMetrics : IDisposable
  {
      /// <summary>The meter name every instrument here is published under.</summary>
      public const string MeterName = "EventBooking.Api";

      private readonly Meter _meter;

      /// <summary>Creates the meter and its instruments.</summary>
      /// <param name="factory">The meter factory.</param>
      public EventBookingMetrics(IMeterFactory factory)
      {
          ArgumentNullException.ThrowIfNull(factory);
          _meter = factory.Create(MeterName);
          Requests = _meter.CreateCounter<long>(
              "eventbooking_http_requests_total", "requests",
              "Requests served, by route pattern, method and status.");
          Duration = _meter.CreateHistogram<double>(
              "eventbooking_http_request_duration_seconds", "s",
              "Request duration, by route pattern.");
          CapacityExhausted = _meter.CreateCounter<long>(
              "eventbooking_capacity_exhausted_total", "refusals",
              "Bookings refused for want of capacity — a business signal of undersupply.");
          RateLimited = _meter.CreateCounter<long>(
              "eventbooking_rate_limited_total", "rejections",
              "Requests rejected by a rate limiter, by policy.");
      }

      /// <summary>Gets the request counter.</summary>
      public Counter<long> Requests { get; }

      /// <summary>Gets the request-duration histogram.</summary>
      public Histogram<double> Duration { get; }

      /// <summary>Gets the capacity-refusal counter.</summary>
      public Counter<long> CapacityExhausted { get; }

      /// <summary>Gets the rate-limiter rejection counter.</summary>
      public Counter<long> RateLimited { get; }

      /// <inheritdoc />
      public void Dispose() => _meter.Dispose();
  }
  ```

  ```csharp
  // src/EventBooking.Api/Observability/PrometheusText.cs (complete)
  using System.Diagnostics.Metrics;
  using System.Globalization;
  using System.Text;

  namespace EventBooking.Api.Observability;

  /// <summary>
  /// Renders the meter's instruments in Prometheus text format. A listener collects every
  /// measurement as it is taken and keeps the running totals; rendering is then a read of that
  /// state, so a scrape costs nothing on the request path.
  /// </summary>
  public sealed class PrometheusText : IDisposable
  {
      private readonly MeterListener _listener = new();
      private readonly Dictionary<string, Series> _series = new(StringComparer.Ordinal);
      private readonly Lock _gate = new();

      /// <summary>Starts listening to the API's meter.</summary>
      public PrometheusText()
      {
          _listener.InstrumentPublished = (instrument, listener) =>
          {
              if (instrument.Meter.Name == EventBookingMetrics.MeterName)
              {
                  listener.EnableMeasurementEvents(instrument);
              }
          };
          _listener.SetMeasurementEventCallback<long>(
              (instrument, measurement, tags, _) => Record(instrument, measurement, tags));
          _listener.SetMeasurementEventCallback<double>(
              (instrument, measurement, tags, _) => Record(instrument, measurement, tags));
          _listener.Start();
      }

      /// <summary>Renders every collected series.</summary>
      /// <returns>The Prometheus exposition text.</returns>
      public string Render()
      {
          var builder = new StringBuilder();
          lock (_gate)
          {
              foreach (var group in _series.Values.GroupBy(x => x.Name, StringComparer.Ordinal)
                  .OrderBy(x => x.Key, StringComparer.Ordinal))
              {
                  var first = group.First();
                  builder.Append("# HELP ").Append(group.Key).Append(' ')
                      .Append(first.Description).Append('\n');
                  builder.Append("# TYPE ").Append(group.Key).Append(' ')
                      .Append(first.Kind).Append('\n');
                  foreach (var series in group.OrderBy(x => x.Labels, StringComparer.Ordinal))
                  {
                      builder.Append(group.Key).Append(series.Labels).Append(' ')
                          .Append(series.Value.ToString("G17", CultureInfo.InvariantCulture))
                          .Append('\n');
                  }
              }
          }

          return builder.ToString();
      }

      /// <inheritdoc />
      public void Dispose() => _listener.Dispose();

      private void Record(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags)
      {
          var labels = Format(tags);
          var key = instrument.Name + labels;
          lock (_gate)
          {
              if (!_series.TryGetValue(key, out var series))
              {
                  series = new Series(
                      instrument.Name,
                      labels,
                      instrument is Histogram<double> ? "histogram" : "counter",
                      instrument.Description ?? instrument.Name);
                  _series[key] = series;
              }

              series.Value += measurement;
          }
      }

      /// <summary>
      /// Label values are escaped, not trusted. Every tag this API emits is a route pattern, a
      /// method, a status or a policy name — never a route parameter value — and the metrics
      /// suite asserts that an attendee token never reaches the output.
      /// </summary>
      private static string Format(ReadOnlySpan<KeyValuePair<string, object?>> tags)
      {
          if (tags.Length == 0)
          {
              return string.Empty;
          }

          var builder = new StringBuilder("{");
          for (var index = 0; index < tags.Length; index++)
          {
              if (index > 0)
              {
                  builder.Append(',');
              }

              builder.Append(tags[index].Key).Append("=\"")
                  .Append((tags[index].Value?.ToString() ?? string.Empty)
                      .Replace("\\", "\\\\", StringComparison.Ordinal)
                      .Replace("\"", "\\\"", StringComparison.Ordinal)
                      .Replace("\n", "\\n", StringComparison.Ordinal))
                  .Append('"');
          }

          return builder.Append('}').ToString();
      }

      private sealed class Series(string name, string labels, string kind, string description)
      {
          public string Name { get; } = name;

          public string Labels { get; } = labels;

          public string Kind { get; } = kind;

          public string Description { get; } = description;

          public double Value { get; set; }
      }
  }
  ```

  A histogram rendered as a running sum is a deliberate simplification: design 08 asks for
  latency per endpoint group, and a sum plus the request counter gives a mean without shipping
  bucket state this service has no consumer for. If an operator later wants quantiles, the
  exporter package replaces this file and nothing else changes, because every instrument is
  already a standard meter instrument.

  **The three rate limits.** Design 06 gives the attendee routes a per-address and a per-token
  allowance, and every staff route a per-identity one. The two attendee limiters chain: a
  request must pass both, so one busy network cannot starve other attendees and one leaked link
  cannot be hammered from many addresses.

  ```csharp
  // src/EventBooking.Api/Auth/RemoteIpRateLimiterPolicy.cs (complete, replacing the ported file)
  using System.Threading.RateLimiting;
  using EventBooking.Api.Endpoints;
  using Microsoft.AspNetCore.RateLimiting;
  using Microsoft.Extensions.Options;

  namespace EventBooking.Api.Auth;

  /// <summary>
  /// The configured per-minute allowances (design 06, design 08). A settable class rather than
  /// a record because the limiter policies read it through the options pattern, which binds by
  /// property.
  /// </summary>
  public sealed class RateLimitSettings
  {
      /// <summary>Gets or sets the attendee requests allowed per client address.</summary>
      public int AttendeePerMinute { get; set; } = 30;

      /// <summary>Gets or sets the attendee requests allowed per token prefix.</summary>
      public int TokenPerMinute { get; set; } = 10;

      /// <summary>Gets or sets the staff requests allowed per staff identity.</summary>
      public int StaffPerMinute { get; set; } = 300;
  }

  /// <summary>Applies the anonymous attendee-link allowance independently per client address.</summary>
  /// <param name="settings">The configured allowances.</param>
  public sealed class RemoteIpRateLimiterPolicy(IOptions<RateLimitSettings> settings)
      : IRateLimiterPolicy<string>
  {
      /// <summary>The policy name the attendee routes are decorated with.</summary>
      public const string PolicyName = "attendee-address";

      /// <inheritdoc/>
      public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected =>
          (context, ct) => RateLimitRejection.WriteAsync(context, PolicyName, ct);

      /// <inheritdoc/>
      public RateLimitPartition<string> GetPartition(HttpContext httpContext)
      {
          ArgumentNullException.ThrowIfNull(httpContext);

          // ForwardedHeadersMiddleware runs before the limiter and rewrites RemoteIpAddress only
          // for a request arriving from a known proxy network, so this is the real client
          // address in production and the socket address otherwise. Unknown addresses share one
          // fallback bucket rather than bypassing the limit.
          var client = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
          return RateLimitPartition.GetSlidingWindowLimiter(
              client,
              _ => new SlidingWindowRateLimiterOptions
              {
                  PermitLimit = settings.Value.AttendeePerMinute,
                  Window = TimeSpan.FromMinutes(1),
                  SegmentsPerWindow = 6,
                  QueueLimit = 0,
              });
      }
  }

  /// <summary>
  /// The one rejection writer. A 429 is a catalogued failure like any other, so it carries the
  /// same problem body, and Retry-After comes from the lease when the limiter supplies it.
  /// </summary>
  internal static class RateLimitRejection
  {
      public static async ValueTask WriteAsync(
          OnRejectedContext context, string policy, CancellationToken ct)
      {
          var shape = ProblemCatalogue.For(ProblemCatalogue.RateLimitedCode);
          var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var value)
              ? value
              : TimeSpan.FromMinutes(1);

          context.HttpContext.Response.StatusCode = shape.Status;
          context.HttpContext.Response.Headers.RetryAfter =
              ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(
                  System.Globalization.CultureInfo.InvariantCulture);

          context.HttpContext.RequestServices
              .GetRequiredService<Observability.EventBookingMetrics>()
              .RateLimited.Add(1, new KeyValuePair<string, object?>("policy", policy));

          await context.HttpContext.Response.WriteAsJsonAsync(
              new
              {
                  type = shape.Type,
                  title = shape.Title,
                  status = shape.Status,
                  detail = "Too many requests. Please wait and try again.",
              },
              contentType: "application/problem+json",
              cancellationToken: ct);
      }
  }
  ```

  ```csharp
  // src/EventBooking.Api/Auth/TokenPrefixRateLimiterPolicy.cs (complete)
  using System.Threading.RateLimiting;
  using Microsoft.AspNetCore.RateLimiting;
  using Microsoft.Extensions.Options;

  namespace EventBooking.Api.Auth;

  /// <summary>
  /// Applies the per-link allowance, partitioned on a prefix of the attendee token rather than
  /// the whole token: a prefix is enough to separate one link from another, and it keeps the
  /// full token out of the limiter's key space, which is memory that outlives the request.
  /// </summary>
  /// <param name="settings">The configured allowances.</param>
  public sealed class TokenPrefixRateLimiterPolicy(IOptions<RateLimitSettings> settings)
      : IRateLimiterPolicy<string>
  {
      /// <summary>The policy name the attendee routes are decorated with.</summary>
      public const string PolicyName = "attendee-token";

      /// <summary>The number of leading token characters the partition key uses.</summary>
      public const int PrefixLength = 12;

      /// <inheritdoc/>
      public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected =>
          (context, ct) => RateLimitRejection.WriteAsync(context, PolicyName, ct);

      /// <inheritdoc/>
      public RateLimitPartition<string> GetPartition(HttpContext httpContext)
      {
          ArgumentNullException.ThrowIfNull(httpContext);

          var token = httpContext.Request.RouteValues.TryGetValue("token", out var value)
              ? value?.ToString()
              : null;
          var prefix = string.IsNullOrEmpty(token)
              ? "none"
              : token[..Math.Min(PrefixLength, token.Length)];

          return RateLimitPartition.GetSlidingWindowLimiter(
              prefix,
              _ => new SlidingWindowRateLimiterOptions
              {
                  PermitLimit = settings.Value.TokenPerMinute,
                  Window = TimeSpan.FromMinutes(1),
                  SegmentsPerWindow = 6,
                  QueueLimit = 0,
              });
      }
  }
  ```

  ```csharp
  // src/EventBooking.Api/Auth/StaffRateLimiterPolicy.cs (complete)
  using System.Threading.RateLimiting;
  using Microsoft.AspNetCore.RateLimiting;
  using Microsoft.Extensions.Options;

  namespace EventBooking.Api.Auth;

  /// <summary>
  /// Defence in depth on the staff surface: 300 requests a minute per identity. Partitioned on
  /// the provider key rather than the address, because staff share office addresses and an
  /// address-shaped limit would punish a whole floor for one script.
  /// </summary>
  /// <param name="settings">The configured allowances.</param>
  public sealed class StaffRateLimiterPolicy(IOptions<RateLimitSettings> settings)
      : IRateLimiterPolicy<string>
  {
      /// <summary>The policy name the staff route groups are decorated with.</summary>
      public const string PolicyName = "staff";

      /// <inheritdoc/>
      public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected =>
          (context, ct) => RateLimitRejection.WriteAsync(context, PolicyName, ct);

      /// <inheritdoc/>
      public RateLimitPartition<string> GetPartition(HttpContext httpContext)
      {
          ArgumentNullException.ThrowIfNull(httpContext);

          var staffUserId = HttpContextCallerAccessor.StaffUserIdOf(httpContext.User);
          return staffUserId is null
              ? RateLimitPartition.GetNoLimiter("anonymous")
              : RateLimitPartition.GetSlidingWindowLimiter(
                  staffUserId.Value.ToString(),
                  _ => new SlidingWindowRateLimiterOptions
                  {
                      PermitLimit = settings.Value.StaffPerMinute,
                      Window = TimeSpan.FromMinutes(1),
                      SegmentsPerWindow = 6,
                      QueueLimit = 0,
                  });
      }
  }
  ```

  An anonymous request on a staff route gets no limiter partition because it has no identity to
  partition on; the authentication middleware has already refused it with a 401 by the time the
  handler would run, and inventing a shared bucket for every unauthenticated caller would be a
  denial-of-service lever rather than a defence.

  **The Idempotency-Key retention.** Keys are scoped to the staff identity and the route: one
  caller's key can never replay another's response, and the same key on two endpoints is two
  keys. Retention is 24 hours, enforced on read rather than by a job — a row older than that
  reads as absent, and writes delete the expired rows opportunistically, so the Task 19 sweep
  needs no fourth step.

  ```csharp
  // src/EventBooking.Application/Abstractions/IIdempotencyStore.cs (complete)
  namespace EventBooking.Application.Abstractions;

  /// <summary>One retained response, replayed for a repeated key.</summary>
  /// <param name="RequestHash">The hash of the body the key was first used with.</param>
  /// <param name="StatusCode">The status the first call returned.</param>
  /// <param name="Body">The body the first call returned.</param>
  public sealed record IdempotentResponse(string RequestHash, int StatusCode, string Body);

  /// <summary>Retains create-endpoint responses against their Idempotency-Key for 24 hours.</summary>
  public interface IIdempotencyStore
  {
      /// <summary>The retention window design 05 gives the header.</summary>
      public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

      /// <summary>Reads a retained response, ignoring rows past the retention window.</summary>
      /// <param name="staffUserId">The calling staff identity.</param>
      /// <param name="route">The route pattern the key was used on.</param>
      /// <param name="key">The caller's key.</param>
      /// <param name="now">The current instant.</param>
      /// <param name="ct">The cancellation token.</param>
      /// <returns>The retained response, or null.</returns>
      Task<IdempotentResponse?> TryGetAsync(
          Guid staffUserId, string route, string key, DateTimeOffset now, CancellationToken ct);

      /// <summary>Retains one response and prunes anything past the retention window.</summary>
      /// <param name="staffUserId">The calling staff identity.</param>
      /// <param name="route">The route pattern the key was used on.</param>
      /// <param name="key">The caller's key.</param>
      /// <param name="requestHash">The hash of the request body.</param>
      /// <param name="statusCode">The status returned.</param>
      /// <param name="body">The body returned.</param>
      /// <param name="now">The current instant.</param>
      /// <param name="ct">The cancellation token.</param>
      /// <returns>A task tracking the write.</returns>
      Task SaveAsync(
          Guid staffUserId, string route, string key, string requestHash,
          int statusCode, string body, DateTimeOffset now, CancellationToken ct);
  }
  ```

  ```csharp
  // src/EventBooking.Infrastructure/Persistence/Idempotency/IdempotencyRecord.cs (complete)
  using Microsoft.EntityFrameworkCore;
  using Microsoft.EntityFrameworkCore.Metadata.Builders;

  namespace EventBooking.Infrastructure.Persistence.Idempotency;

  /// <summary>
  /// One retained create-endpoint response. This is infrastructure, not a domain concept: it
  /// carries no business meaning, is keyed by a header value, and expires. It is deliberately
  /// absent from the ontology for that reason.
  /// </summary>
  public sealed class IdempotencyRecord
  {
      /// <summary>Gets the calling staff identity.</summary>
      public Guid StaffUserId { get; init; }

      /// <summary>Gets the route pattern the key was used on.</summary>
      public string Route { get; init; } = string.Empty;

      /// <summary>Gets the caller's key.</summary>
      public string Key { get; init; } = string.Empty;

      /// <summary>Gets the hash of the request body the key was first used with.</summary>
      public string RequestHash { get; init; } = string.Empty;

      /// <summary>Gets the status the first call returned.</summary>
      public int StatusCode { get; init; }

      /// <summary>Gets the body the first call returned.</summary>
      public string Body { get; init; } = string.Empty;

      /// <summary>Gets the instant the row was written.</summary>
      public DateTimeOffset CreatedAt { get; init; }
  }

  /// <summary>Maps the retention table.</summary>
  public sealed class IdempotencyRecordConfiguration
      : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<IdempotencyRecord>
  {
      /// <inheritdoc />
      public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
      {
          ArgumentNullException.ThrowIfNull(builder);

          builder.ToTable("idempotency_record");
          builder.HasKey(x => new { x.StaffUserId, x.Route, x.Key });
          builder.Property(x => x.StaffUserId).HasColumnName("staff_user_id");
          builder.Property(x => x.Route).HasColumnName("route").HasMaxLength(200);
          builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(200);
          builder.Property(x => x.RequestHash).HasColumnName("request_hash").HasMaxLength(64);
          builder.Property(x => x.StatusCode).HasColumnName("status_code");
          builder.Property(x => x.Body).HasColumnName("body");
          builder.Property(x => x.CreatedAt).HasColumnName("created_at");
          builder.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_idempotency_record_created_at");
      }
  }
  ```

  ```csharp
  // src/EventBooking.Infrastructure/Persistence/Idempotency/IdempotencyStore.cs (complete)
  using EventBooking.Application.Abstractions;
  using Microsoft.EntityFrameworkCore;

  namespace EventBooking.Infrastructure.Persistence.Idempotency;

  /// <summary>The PostgreSQL retention store.</summary>
  /// <param name="context">The database context.</param>
  public sealed class IdempotencyStore(EventBookingDbContext context) : IIdempotencyStore
  {
      /// <inheritdoc />
      public async Task<IdempotentResponse?> TryGetAsync(
          Guid staffUserId, string route, string key, DateTimeOffset now, CancellationToken ct)
      {
          var cutoff = now - IIdempotencyStore.Retention;
          var row = await context.IdempotencyRecords
              .AsNoTracking()
              .SingleOrDefaultAsync(
                  x => x.StaffUserId == staffUserId && x.Route == route && x.Key == key &&
                       x.CreatedAt > cutoff,
                  ct);

          return row is null
              ? null
              : new IdempotentResponse(row.RequestHash, row.StatusCode, row.Body);
      }

      /// <inheritdoc />
      public async Task SaveAsync(
          Guid staffUserId, string route, string key, string requestHash,
          int statusCode, string body, DateTimeOffset now, CancellationToken ct)
      {
          context.IdempotencyRecords.Add(new IdempotencyRecord
          {
              StaffUserId = staffUserId,
              Route = route,
              Key = key,
              RequestHash = requestHash,
              StatusCode = statusCode,
              Body = body,
              CreatedAt = now,
          });
          await context.SaveChangesAsync(ct);

          // Opportunistic pruning on the write path, keyed by the created-at index. A retention
          // table that only ever grows is the failure mode this avoids, and a sweep step would
          // put an unrelated concern into the Task 19 run.
          var cutoff = now - IIdempotencyStore.Retention;
          await context.IdempotencyRecords
              .Where(x => x.CreatedAt <= cutoff)
              .ExecuteDeleteAsync(ct);
      }
  }
  ```

  ```csharp
  // src/EventBooking.Api/Idempotency/IdempotencyMiddleware.cs (complete)
  using System.Security.Cryptography;
  using System.Text;
  using EventBooking.Api.Auth;
  using EventBooking.Api.Endpoints;
  using EventBooking.Application.Abstractions;

  namespace EventBooking.Api.Idempotency;

  /// <summary>
  /// Replays the first response for a repeated Idempotency-Key on a create endpoint. A key
  /// reused with a different body is a caller mistake, not a replay, and is refused rather than
  /// answered with somebody else's result.
  /// </summary>
  /// <param name="next">The following middleware.</param>
  public sealed class IdempotencyMiddleware(RequestDelegate next)
  {
      /// <summary>The header design 05 names.</summary>
      public const string HeaderName = "Idempotency-Key";

      /// <summary>Applies the retention to one request.</summary>
      /// <param name="context">The request context.</param>
      /// <param name="store">The retention store.</param>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="clock">The clock.</param>
      /// <returns>A task tracking the request.</returns>
      public async Task InvokeAsync(
          HttpContext context, IIdempotencyStore store, ICallerAccessor caller, IClock clock)
      {
          if (!HttpMethods.IsPost(context.Request.Method) ||
              !context.Request.Headers.TryGetValue(HeaderName, out var header) ||
              string.IsNullOrWhiteSpace(header) ||
              caller.StaffUserId is not { } staffUserId)
          {
              await next(context);
              return;
          }

          var key = header.ToString();
          var route = context.GetEndpoint()?.DisplayName ?? context.Request.Path.Value ?? "/";
          context.Request.EnableBuffering();
          var requestHash = await HashBodyAsync(context.Request);
          var now = clock.UtcNow;

          var retained = await store.TryGetAsync(staffUserId, route, key, now, context.RequestAborted);
          if (retained is not null)
          {
              if (!CryptographicOperations.FixedTimeEquals(
                  Encoding.UTF8.GetBytes(retained.RequestHash), Encoding.UTF8.GetBytes(requestHash)))
              {
                  await ResultResponses
                      .ValidationFailed(
                          HeaderName,
                          "idempotency-key-reused",
                          "This Idempotency-Key was already used with a different request body.")
                      .ExecuteAsync(context);
                  return;
              }

              context.Response.StatusCode = retained.StatusCode;
              context.Response.ContentType = "application/json";
              await context.Response.WriteAsync(retained.Body, context.RequestAborted);
              return;
          }

          // Buffer the response so a retained body is exactly what the caller received. Only a
          // success is retained: a failure is a state the caller can legitimately retry out of.
          var original = context.Response.Body;
          using var buffer = new MemoryStream();
          context.Response.Body = buffer;
          try
          {
              await next(context);
          }
          finally
          {
              context.Response.Body = original;
          }

          buffer.Position = 0;
          var body = await new StreamReader(buffer).ReadToEndAsync(context.RequestAborted);
          if (context.Response.StatusCode is >= 200 and < 300)
          {
              await store.SaveAsync(
                  staffUserId, route, key, requestHash, context.Response.StatusCode, body, now,
                  context.RequestAborted);
          }

          await context.Response.WriteAsync(body, context.RequestAborted);
      }

      private static async Task<string> HashBodyAsync(HttpRequest request)
      {
          request.Body.Position = 0;
          using var sha = SHA256.Create();
          var hash = await sha.ComputeHashAsync(request.Body, request.HttpContext.RequestAborted);
          request.Body.Position = 0;
          return Convert.ToHexString(hash);
      }
  }
  ```

  **Startup validation.** Design 04 marks nine settings as having no default. The reader names
  every missing one at once rather than failing on the first, because an operator bringing a new
  deployment up should get one list rather than nine restarts. Two additions: `Proxy__Networks`,
  which design 06's "forwarded headers are trusted only from the configured reverse-proxy
  network" needs and design 04's table does not name, and `RateLimiting__TokenPerMinute` and
  `__StaffPerMinute` beside the attendee limit the table does name.

  ```csharp
  // src/EventBooking.Api/EventBookingConfiguration.cs (complete, replacing the ported file)
  using System.Text;
  using EventBooking.Api.Auth;
  using EventBooking.Application.Notifications;
  using EventBooking.Infrastructure.Email;
  using EventBooking.Infrastructure.Time;
  using EventBooking.Infrastructure.Tokens;

  namespace EventBooking.Api;

  /// <summary>Everything the host needs, validated before it binds a port.</summary>
  /// <param name="ConnectionString">The application-user PostgreSQL connection string.</param>
  /// <param name="Clock">The transitional single-zone clock options.</param>
  /// <param name="Tokens">The attendee-token signing options.</param>
  /// <param name="Email">The sender identity and provider.</param>
  /// <param name="Smtp">The SMTP endpoint.</param>
  /// <param name="Portal">The public portal origin and contact.</param>
  /// <param name="AllowedOrigins">The web origins CORS permits; never a wildcard.</param>
  /// <param name="ProxyNetworks">The CIDR networks forwarded headers are trusted from.</param>
  /// <param name="RateLimits">The three per-minute allowances.</param>
  /// <param name="SweepInterval">The invite-sweep schedule.</param>
  public sealed record EventBookingSettings(
      string ConnectionString,
      ClockOptions Clock,
      TokenOptions Tokens,
      EmailOptions Email,
      SmtpOptions Smtp,
      AttendeePortalOptions Portal,
      IReadOnlyList<string> AllowedOrigins,
      IReadOnlyList<string> ProxyNetworks,
      RateLimitSettings RateLimits,
      TimeSpan SweepInterval);

  /// <summary>Reads and validates the configuration required to start the EventBooking API.</summary>
  public static class EventBookingConfiguration
  {
      /// <summary>
      /// Signing keys that have appeared in a sample, a README or a container default. A key on
      /// this list is worse than a missing one: it starts, and every attendee link is forgeable.
      /// Matched case-insensitively after trimming.
      /// </summary>
      public static readonly IReadOnlySet<string> PlaceholderSigningKeys =
          new HashSet<string>(StringComparer.OrdinalIgnoreCase)
          {
              "change-me",
              "changeme",
              "change_me",
              "secret",
              "development-signing-key-development-signing-key",
              "insecure-development-key-insecure-development-key",
              "a-test-signing-key-that-is-long-enough-here",
          };

      /// <summary>The smallest signing key design 06 accepts.</summary>
      public const int MinimumSigningKeyBytes = 32;

      /// <summary>Reads every setting, or throws naming all that are missing or invalid.</summary>
      /// <param name="configuration">The bound configuration.</param>
      /// <returns>The validated settings.</returns>
      /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
      public static EventBookingSettings Read(IConfiguration configuration)
      {
          ArgumentNullException.ThrowIfNull(configuration);

          var missing = new List<string>();
          var invalid = new List<string>();

          string Required(string key)
          {
              var value = configuration[key];
              if (string.IsNullOrWhiteSpace(value))
              {
                  missing.Add(key);
                  return string.Empty;
              }

              return value;
          }

          var connectionString = Required("ConnectionStrings:EventBooking");
          Required("Auth:Authority");
          Required("Auth:Audience");
          var signingKey = Required("Tokens:SigningKey");
          var smtpHost = Required("Email:Smtp:Host");
          var fromAddress = Required("Email:FromAddress");
          var baseUrl = Required("Portal:BaseUrl");
          var coordinatorContact = Required("Portal:CoordinatorContact");

          var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
          if (allowedOrigins.Length == 0)
          {
              missing.Add("Cors:AllowedOrigins");
          }

          if (missing.Count > 0)
          {
              throw new InvalidOperationException(
                  "The following configuration values are missing: " + string.Join(", ", missing));
          }

          // Checked after the missing-key check, not folded into it: a key that is present but
          // holds an unusable value is a different failure from one that was never set.
          if (PlaceholderSigningKeys.Contains(signingKey.Trim()))
          {
              invalid.Add(
                  "Tokens:SigningKey is a known placeholder value. Generate a real key: " +
                  "openssl rand -base64 48");
          }
          else if (SigningKeyBytes(signingKey) < MinimumSigningKeyBytes)
          {
              invalid.Add(
                  $"Tokens:SigningKey must be at least {MinimumSigningKeyBytes} bytes.");
          }

          if (allowedOrigins.Contains("*"))
          {
              invalid.Add("Cors:AllowedOrigins must name origins, never a wildcard.");
          }

          if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
          {
              invalid.Add("Portal:BaseUrl must be an absolute URI.");
          }

          if (invalid.Count > 0)
          {
              throw new InvalidOperationException(string.Join(" ", invalid));
          }

          return new EventBookingSettings(
              connectionString,
              new ClockOptions(configuration["Clock:TimeZoneId"] ?? "Etc/UTC"),
              new TokenOptions(signingKey),
              new EmailOptions(
                  fromAddress, configuration["Email:FromName"] ?? "EventBooking", EmailProvider.Smtp),
              new SmtpOptions(
                  smtpHost,
                  int.TryParse(configuration["Email:Smtp:Port"], out var port) ? port : 25),
              new AttendeePortalOptions(baseUrl, coordinatorContact),
              allowedOrigins,
              configuration.GetSection("Proxy:Networks").Get<string[]>() ?? [],
              new RateLimitSettings
              {
                  AttendeePerMinute = Positive(configuration, "RateLimiting:AttendeePerMinute", 30),
                  TokenPerMinute = Positive(configuration, "RateLimiting:TokenPerMinute", 10),
                  StaffPerMinute = Positive(configuration, "RateLimiting:StaffPerMinute", 300),
              },
              TimeSpan.TryParse(configuration["Jobs:SweepInterval"], out var interval)
                  ? interval
                  : TimeSpan.FromMinutes(15));
      }

      /// <summary>
      /// The documented form is base64, and that is what an operator running the openssl command
      /// above will paste. A value that is not base64 is taken as UTF-8 rather than rejected, so
      /// a pasted passphrase becomes a working key of its own length — it is still held to the
      /// same 32-byte floor, so this cannot admit a weak key by accident.
      /// </summary>
      private static int SigningKeyBytes(string value)
      {
          Span<byte> decoded = stackalloc byte[value.Length];
          return Convert.TryFromBase64String(value, decoded, out var written)
              ? written
              : Encoding.UTF8.GetByteCount(value);
      }

      private static int Positive(IConfiguration configuration, string key, int fallback) =>
          int.TryParse(configuration[key], out var value) && value > 0 ? value : fallback;
  }
  ```

  The transitional single-zone clock survives this task. Phase 3's transitional-construct table
  retires it "when handlers carry a `Location`", but no Phase 3 document removes the clock
  options type or the `Clock:TimeZoneId` key, so the reader keeps it — optional now, defaulting to `Etc/UTC`,
  because design 04's table does not list it. Removing it is Phase 6's seed-rework work and is
  recorded in the handover's section 8 table; inventing its removal here would leave the
  infrastructure registration calling for options nothing supplies.

  ```csharp
  // src/EventBooking.Api/Program.cs (complete, replacing the ported file)
  using System.Diagnostics;
  using System.Net;
  using EventBooking.Api;
  using EventBooking.Api.Auth;
  using EventBooking.Api.Endpoints;
  using EventBooking.Api.Idempotency;
  using EventBooking.Api.Observability;
  using EventBooking.Api.OpenApi;
  using EventBooking.Api.Pagination;
  using EventBooking.Application;
  using EventBooking.Application.Abstractions;
  using EventBooking.Infrastructure;
  using EventBooking.Infrastructure.Email;
  using EventBooking.Infrastructure.Persistence;
  using Microsoft.AspNetCore.HttpOverrides;
  using Microsoft.AspNetCore.RateLimiting;
  using Microsoft.EntityFrameworkCore;
  using System.Text;

  var builder = WebApplication.CreateBuilder(args);

  const string WebClientCorsPolicy = "web-client";
  var settings = EventBookingConfiguration.Read(builder.Configuration);

  builder.Logging.ClearProviders();
  builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

  builder.Services.AddEventBookingInfrastructure(
      settings.ConnectionString, settings.Clock, settings.Tokens);
  builder.Services.AddLocalEmailTransport(settings.Email, settings.Smtp);
  builder.Services.AddEventBookingApplication(
      settings.Portal,
      new EventBooking.Application.Access.StaffIdPolicy(builder.Configuration["Identity:StaffIdPattern"]));
  builder.Services.AddEventBookingAuthentication(builder.Configuration);
  builder.Services.AddProblemDetails();
  builder.Services.AddEventBookingOpenApi();

  builder.Services.AddSingleton(settings);
  builder.Services.Configure<RateLimitSettings>(options =>
  {
      options.AttendeePerMinute = settings.RateLimits.AttendeePerMinute;
      options.TokenPerMinute = settings.RateLimits.TokenPerMinute;
      options.StaffPerMinute = settings.RateLimits.StaffPerMinute;
  });
  builder.Services.AddSingleton<ICorrelationContext, AsyncLocalCorrelationContext>();
  builder.Services.AddSingleton<EventBookingMetrics>();
  builder.Services.AddSingleton<PrometheusText>();
  builder.Services.AddSingleton(new PageCursor(Encoding.UTF8.GetBytes(settings.Tokens.SigningKey)));
  builder.Services.AddScoped<IIdempotencyStore,
      EventBooking.Infrastructure.Persistence.Idempotency.IdempotencyStore>();

  builder.Services.AddCors(options => options.AddPolicy(WebClientCorsPolicy, policy => policy
      .WithOrigins([.. settings.AllowedOrigins])
      .AllowAnyHeader()
      .AllowAnyMethod()
      .WithExposedHeaders("Content-Disposition", CorrelationMiddleware.HeaderName)));

  builder.Services.AddRateLimiter(options =>
  {
      options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
      options.AddPolicy<string, RemoteIpRateLimiterPolicy>(RemoteIpRateLimiterPolicy.PolicyName);
      options.AddPolicy<string, TokenPrefixRateLimiterPolicy>(TokenPrefixRateLimiterPolicy.PolicyName);
      options.AddPolicy<string, StaffRateLimiterPolicy>(StaffRateLimiterPolicy.PolicyName);
  });

  builder.Services.AddHostedService<InviteSweepService>();

  var app = builder.Build();

  // Forwarded headers first, and only from the configured networks: every later decision that
  // reads the client address — the limiter partition above all — must see the real one or the
  // socket one, never an attacker-supplied one.
  var forwarded = new ForwardedHeadersOptions
  {
      ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
      ForwardLimit = 1,
  };
  forwarded.KnownNetworks.Clear();
  forwarded.KnownProxies.Clear();
  foreach (var network in settings.ProxyNetworks)
  {
      var parts = network.Split('/');
      if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var prefix) &&
          int.TryParse(parts[1], out var length))
      {
          forwarded.KnownNetworks.Add(new System.Net.IPNetwork(prefix, length));
      }
      else if (IPAddress.TryParse(network, out var proxy))
      {
          forwarded.KnownProxies.Add(proxy);
      }
  }

  app.UseForwardedHeaders(forwarded);
  app.UseMiddleware<CorrelationMiddleware>();

  // Request metrics, inline because the instruments live on one meter and the middleware is
  // three statements. The route pattern is the label, never the request path: a path carries
  // attendee tokens and identifiers, and a metric label outlives the request.
  app.Use(async (context, next) =>
  {
      var started = Stamp.GetTimestamp();
      await next(context);
      var metrics = context.RequestServices.GetRequiredService<EventBookingMetrics>();
      var route = context.GetEndpoint() is Microsoft.AspNetCore.Routing.RouteEndpoint endpoint
          ? "/" + endpoint.RoutePattern.RawText?.TrimStart('/')
          : "unmatched";
      var tags = new TagList
      {
          { "route", route },
          { "method", context.Request.Method },
          { "status", context.Response.StatusCode },
      };
      metrics.Requests.Add(1, tags);
      metrics.Duration.Record(Stamp.GetElapsedTime(started).TotalSeconds, tags);
  });

  app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous().DisableRateLimiting();
  app.UseSwaggerUI(options =>
  {
      options.RoutePrefix = "swagger";
      options.DocumentTitle = "EventBooking API v1";
      options.SwaggerEndpoint("/openapi/v1.json", "EventBooking API v1");
      options.DisplayOperationId();
  });

  app.UseCors(WebClientCorsPolicy);
  app.UseAuthentication();
  app.UseMiddleware<StaffIdentityRecorder>();
  app.UseAuthorization();
  app.UseRateLimiter();

  // After authentication, because a key is scoped to the caller it was issued under.
  app.UseMiddleware<IdempotencyMiddleware>();

  app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }))
      .AllowAnonymous().DisableRateLimiting().WithAgentMetadata("getLiveness");

  app.MapGet("/health/ready", async (EventBookingDbContext context, CancellationToken ct) =>
      await context.Database.CanConnectAsync(ct)
          ? Results.Ok(new { status = "ok", checks = new[] { new { name = "database", status = "ok" } } })
          : Results.Json(
              new { status = "unavailable", checks = new[] { new { name = "database", status = "failed" } } },
              statusCode: StatusCodes.Status503ServiceUnavailable))
      .AllowAnonymous().DisableRateLimiting().WithAgentMetadata("getReadiness");

  app.MapGet("/metrics", (PrometheusText metrics) =>
      Results.Text(metrics.Render(), "text/plain"))
      .AllowAnonymous().DisableRateLimiting().WithAgentMetadata("getMetrics");

  app.MapApiDiscoveryEndpoints();

  app.MapEventEndpoints();
  app.MapAttendeeEndpoints();
  app.MapAttendeeGroupEndpoints();
  app.MapAdminEndpoints();
  app.MapStaffAccessEndpoints();
  app.MapMeEndpoints();
  app.MapBookingEndpoints();
  app.MapDashboardEndpoints();
  app.MapAuditEndpoints();
  app.MapAppointmentWorkspaceEndpoints();

  app.Run();

  /// <summary>Named so the integration test factory can start this host.</summary>
  public partial class Program;

  /// <summary>The monotonic clock the request-duration histogram reads.</summary>
  internal static class Stamp
  {
      public static long GetTimestamp() => Stopwatch.GetTimestamp();

      public static TimeSpan GetElapsedTime(long from) => Stopwatch.GetElapsedTime(from);
  }
  ```

  `/metrics` is anonymous and rate-limit-exempt, like the probes. Design 07 keeps the containers
  on an internal network with nothing but the web and MCP hostnames routed at the ingress, so the
  endpoint is not publicly reachable; putting an auth scheme in front of it would only stop the
  scrape. The four route entries are decorated with agent metadata so Task 22b's catalogue test
  sees them as declared operations rather than as strays.

  **The Application changes the catalogue needs.** Five factories, the token-expiry
  distinction, the typed proposal refusal and the last-Admin refusal. None of these is new
  behaviour except the expiry disclosure: the rest is the same refusal wearing a code the
  catalogue can map.

  ```csharp
  // src/EventBooking.Application/Bookings/ViewInviteHandler.cs — the token branch, replacing
  // the three not-found returns at the head of HandleAsync. The rest of the method is unchanged.
  public const string InvalidLinkMessage = "This booking link is no longer valid.";

  /// <summary>The one disclosure: a real link that has lapsed, so the page can say who to ask.</summary>
  public const string ExpiredLinkMessage = "This booking link has expired.";

  if (!tokens.TryRead(query.Token, out var link) || link.Purpose != TokenPurpose.Book)
  {
      return Result<InviteView>.Failure(Error.TokenInvalid(InvalidLinkMessage));
  }

  var invite = await invites.GetAsync(link.EntityId, cancellationToken);
  if (invite is null || invite.TokenVersion != link.Version)
  {
      return Result<InviteView>.Failure(Error.TokenInvalid(InvalidLinkMessage));
  }

  // The signature verified, the row resolved and the stored version matched, so the holder
  // demonstrably received this link. Telling them it has lapsed discloses nothing they did not
  // already know and is what design 06's expired page needs. A Used, Superseded or Cancelled
  // invite stays indistinguishable from a forgery, because those states are not the holder's.
  if (!invite.IsUsableAt(clock.UtcNow))
  {
      return Result<InviteView>.Failure(
          invite.Status == InviteStatus.Pending
              ? Error.TokenExpired(ExpiredLinkMessage)
              : Error.TokenInvalid(InvalidLinkMessage));
  }

  var attendee = await attendees.GetAsync(invite.AttendeeId, cancellationToken);
  if (attendee is null)
  {
      return Result<InviteView>.Failure(Error.TokenInvalid(InvalidLinkMessage));
  }
  ```

  ```csharp
  // src/EventBooking.Application/Bookings/ViewBookingHandler.cs — the same repointing, without
  // the expiry branch. Design 06: nothing invalidates a manage token in the first release, so
  // there is no lapsed state to disclose and every failure is one answer.
  if (!tokens.TryRead(query.ManageToken, out var link) || link.Purpose != TokenPurpose.Manage)
  {
      return Result<BookingView>.Failure(Error.TokenInvalid(InvalidLinkMessage));
  }

  var booking = await bookings.GetAsync(link.EntityId, cancellationToken);
  if (booking is null || booking.ManageTokenVersion != link.Version)
  {
      return Result<BookingView>.Failure(Error.TokenInvalid(InvalidLinkMessage));
  }
  ```

  ```csharp
  // src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs — the four token-shaped
  // refusals inside the locked section become token errors, so the endpoint renders 404 or 410
  // rather than a 409 the design's catalogue has no slug for. The already-confirmed branch and
  // everything below it are unchanged.
  if (invite.AttendeeId != attendee.Id)
  {
      return Result<ConfirmBookingOutcome>.Failure(
          Error.TokenInvalid(ViewInviteHandler.InvalidLinkMessage));
  }

  if (invite.TokenVersion != reference.Version)
  {
      return Result<ConfirmBookingOutcome>.Failure(
          Error.TokenInvalid(ViewInviteHandler.InvalidLinkMessage));
  }

  if (invite.Status == InviteStatus.Used)
  {
      var existing = await bookings.GetByInviteIdAsync(invite.Id, ct);
      await transaction.RollbackAsync(ct);
      return Result<ConfirmBookingOutcome>.Failure(Error.AlreadyConfirmed(
          $"This invite already confirmed booking {existing?.Id}.", existing?.Id ?? Guid.Empty));
  }

  if (invite.Status != InviteStatus.Pending)
  {
      return Result<ConfirmBookingOutcome>.Failure(
          Error.TokenInvalid(ViewInviteHandler.InvalidLinkMessage));
  }

  if (!invite.IsUsableAt(clock.UtcNow))
  {
      return Result<ConfirmBookingOutcome>.Failure(
          Error.TokenExpired(ViewInviteHandler.ExpiredLinkMessage));
  }
  ```

  The order matters and is not interchangeable. A `Used` invite is answered with
  `already-confirmed` and its booking identifier before the expiry check runs, because settlement
  #2 makes a replayed confirmation a refusal naming the existing booking — checking expiry first
  would turn a replay of an old confirmation into a bare 410 and lose the booking the attendee is
  asking about. The same expiry branch goes into the attendee cancel path in the same file's
  sibling handler, against `booking.ManageTokenVersion` and with no expiry case, as above.

  ```csharp
  // src/EventBooking.Application/Negotiation/RecordAcceptanceHandler.cs — and identically in
  // WithdrawAcceptanceHandler.cs and WithdrawProposalHandler.cs. Ordered, not interchangeable:
  // ProposalNotOpenException derives from DomainException, so the general catch first would
  // swallow it and turn a conflict into a validation error.
  catch (ProposalNotOpenException ex)
  {
      return Result<RecordAcceptanceOutcome>.Failure(Error.ProposalNotOpen(
          $"The proposal is {ex.CurrentStatus} and can no longer be changed."));
  }
  catch (DomainException ex)
  {
      return Result<RecordAcceptanceOutcome>.Failure(Error.Validation(ex.Message));
  }
  ```

  ```csharp
  // src/EventBooking.Application/Access/StaffScopeHandler.cs — the last-Admin refusal, added
  // beside the existing version check. Role synchronisation keeps its own silent refusal and
  // alert; this is the interactive path, where the Admin making the change is present to be told.
  var remainingAdmins = await profiles.CountAdminsExcludingAsync(command.TargetStaffUserId, ct);
  if (target.IsAdmin && remainingAdmins == 0)
  {
      return Result<SetStaffScopeOutcome>.Failure(Error.LastAdmin(
          "That change would leave no administrator."));
  }
  ```

  CountAdminsExcludingAsync is a new unlocked count on the staff-access repository port and its
  in-memory fake, in the shape of Task 15's CountActiveForAttendeeAsync: a consequence reported
  back to the caller, not a gate, so it takes no lock. Add it to the port, the PostgreSQL
  repository and the Application fake together, or the fake and the adapter will disagree.

  **The correlation identifier on the outbox row.**

  ```csharp
  // src/EventBooking.Domain/Notifications/EmailLog.cs — one setter beside Task 18's
  // SetNotBefore. The row's correlation is write-once: whatever staged it owns it, and a later
  // dispatcher claim must not relabel a row it did not cause.
  /// <summary>Gets the correlation identifier of the work that staged or claimed the row.</summary>
  public string? CorrelationId { get; private set; }

  /// <summary>Stamps the correlation identifier if the row does not already carry one.</summary>
  /// <param name="correlationId">The identifier of the work staging the row.</param>
  public void StampCorrelation(string? correlationId)
  {
      if (string.IsNullOrWhiteSpace(CorrelationId) && !string.IsNullOrWhiteSpace(correlationId))
      {
          CorrelationId = correlationId;
      }
  }
  ```

  ```csharp
  // src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs — the retention set,
  // and the save-time correlation backstop.
  /// <summary>Gets the Idempotency-Key retention rows.</summary>
  public DbSet<Idempotency.IdempotencyRecord> IdempotencyRecords => Set<Idempotency.IdempotencyRecord>();

  /// <inheritdoc />
  public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
  {
      // The same shape as Task 11's start-instant backstop, and for the same reason: an outbox
      // row is staged from a handful of handlers plus the seeder, and a derived column cannot
      // depend on which path inserted the row. The stamp is write-once, so a row that already
      // names its correlation keeps it.
      foreach (var entry in ChangeTracker.Entries<Domain.Notifications.EmailLog>())
      {
          if (entry.State == EntityState.Added)
          {
              entry.Entity.StampCorrelation(correlation.CorrelationId);
          }
      }

      return base.SaveChangesAsync(cancellationToken);
  }
  ```

  The context takes `ICorrelationContext correlation` as a constructor parameter beside its
  existing options. The registration is a singleton whose value rides the execution context, so
  a scoped context resolving it costs nothing.

  ```csharp
  // src/EventBooking.Infrastructure/Email/OutboxDispatcher.cs — one clause in Task 18's claim
  // statement. A row staged by a request keeps that request's identifier; a row staged by a
  // background job, which has none, takes the dispatcher run's. Settlement #6 and master Task 21
  // both hold.
  correlation_id = COALESCE(correlation_id, @correlationId)
  ```

  ```csharp
  // src/EventBooking.Mcp/Program.cs — the destructuring, now that the reader returns a record.
  // Task 23 rewrites the rest of this file; this keeps it building in the meantime.
  var settings = EventBookingConfiguration.Read(builder.Configuration);

  builder.Services.AddEventBookingInfrastructure(
      settings.ConnectionString, settings.Clock, settings.Tokens);
  builder.Services.AddLocalEmailTransport(settings.Email, settings.Smtp);
  builder.Services.AddEventBookingApplication(
      settings.Portal,
      new EventBooking.Application.Access.StaffIdPolicy(builder.Configuration["Identity:StaffIdPattern"]));
  builder.Services.AddSingleton<ICorrelationContext, AsyncLocalCorrelationContext>();
  ```

- [ ] **Step 4: Run.** Expected: PASS — the new suite plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  The schema gains one table, so generate and read the migration before accepting it:

  ```bash
  dotnet ef migrations add IdempotencyRetention --project src/EventBooking.Infrastructure \
    --startup-project src/EventBooking.Api
  ```

  It should create `idempotency_record` with the composite key and the created-at index, and
  nothing else. A migration that also touches `email_log` means the correlation column from
  Task 18 was never applied; a migration that touches anything else means the model snapshot is
  dirty — restore it before regenerating rather than accepting the diff.

  **No figure here is observed.** This task is hand-authored and nothing in it has been built or
  run. Expect the API count to rise by roughly forty cases and every other project to be
  unchanged except the Application suite, which gains the token-expiry and last-Admin cases. The
  last measured checkpoint remains Task 11 at 1570; a count that does not match the executor's
  own before-and-after diff is a signal to read the diff, not to adjust the number.

- [ ] **Step 5: Commit and push** the executor's code — not the plan documents — under the
  master plan's message:

  ```bash
  test -z "$(git status --porcelain --ignored=no | grep -v '^??')"
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  git add src/EventBooking.Api/ src/EventBooking.Mcp/Program.cs src/EventBooking.Application/ src/EventBooking.Domain/Notifications/EmailLog.cs src/EventBooking.Infrastructure/ tests/EventBooking.Api.Tests/
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "feat(api): pagination, error catalogue, rate limits and config validation

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```
