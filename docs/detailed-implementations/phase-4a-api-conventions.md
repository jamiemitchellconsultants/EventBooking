# 04a — API conventions: cursor codec, error catalogue, rate limits and config validation (Task 21)

[← Phase overview](phase-4-api-and-mcp.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

> This task is hand-authored: complete code and complete tests are written straight into this document, with no prototype. The test counts below are what you should expect to reach, not figures observed by the author — nothing here has been run.

**Goal:** Establish shared API conventions: opaque cursor codec, full error catalogue as RFC 9457 problem details, the `Event` time contract (`date`, `startTime`, `durationMinutes`, `startLocal`, `endLocal`, `startUtc`, `endUtc`, `timeZoneId`, `zoneAbbreviation`), `_links` generation from caller capabilities, `Idempotency-Key` retained 24 hours on create endpoints, rate limits of 30/min per IP plus 10/min per token prefix on attendee routes and 300/min per staff member, forwarded headers trusted only from the proxy network, `health`, `ready` and metrics endpoints, correlation-id JSON logs with no PII, and startup validation of dagger settings including `Portal__BaseUrl` and `Tokens__SigningKey` of 32 bytes with a placeholder denylist. Contradiction #3: a missing or malformed `StaffId` is 403 everywhere except `GET /api/me`, which explains the problem instead.

**Architecture:** Minimal-API cross-cutting layer under `src/EventBooking.Api`: the cursor codec signs the sort key with HMAC so tampering is a `validation-failed` refusal, not a 500; the problem mapper translates every domain failure to its catalogue slug; the idempotency middleware stores first responses for 24h keyed by key plus route; rate limiting runs per IP, per token prefix, and per staff user; startup validation fails fast before the host serves traffic.

**Tech Stack:** .NET 10, xUnit, ASP.NET Core minimal APIs, rate limiting, OpenAPI 3.1.

**Spec:** [Master Task 21](../superpowers/plans/2026-09-19-eventbooking-implementation.md), [API design](../design/05-api-design.md), [security and authentication](../design/06-security-and-authentication.md), [ontology](../ontology.md).

## Global constraints

Every list endpoint uses opaque keyset cursors only; there is no offset pagination. Each resource carries `_links` for actions the caller may take. Errors are `application/problem+json` with a stable catalogue slug; an expected failure is never a 500. `Tokens__SigningKey` under 32 bytes or matching a placeholder is a startup failure.

## Review focus

STOP AND CHECK three things. A tampered cursor returns `validation-failed`, never data or a 500. The 31st attendee request in a minute from one IP returns 429 with `Retry-After`, while a forwarded IP from outside the proxy network is ignored. Startup fails with a missing `Portal__BaseUrl` and with a placeholder signing key.

### Task 21: API conventions

**Files:**

- Create: src/EventBooking.Api/Pagination/CursorCodec.cs
- Create: src/EventBooking.Api/Idempotency/IdempotencyMiddleware.cs
- Modify: src/EventBooking.Api/Endpoints/ResultResponses.cs
- Modify: src/EventBooking.Api/Program.cs
- Modify: src/EventBooking.Api/Configuration/StartupValidator.cs
- Test: tests/EventBooking.Api.Tests/Conventions/CursorCodecTests.cs
- Test: tests/EventBooking.Api.Tests/Conventions/ErrorCatalogueTests.cs
- Test: tests/EventBooking.Api.Tests/Conventions/RateLimitTests.cs
- Test: tests/EventBooking.Api.Tests/Conventions/StartupValidationTests.cs

**Interfaces:**

```csharp
namespace EventBooking.Api.Pagination;

// Encodes the keyset sort key as base64url plus HMAC; tampered input is refused.
public sealed class CursorCodec
{
    // Encodes occurred-at plus id into an opaque cursor.
    public string Encode(DateTimeOffset occurredAt, Guid id) => throw new NotImplementedException();
    // Decodes a cursor, throwing a catalogue-mapped refusal on tampering.
    public (DateTimeOffset OccurredAt, Guid Id) Decode(string cursor) => throw new NotImplementedException();
    // Validates asynchronously against the configured signing key.
    public async Task<bool> IsValidAsync(string cursor, CancellationToken ct)
    {
        await Task.Yield();
        return true;
    }
}

namespace EventBooking.Api.Errors;

// Maps domain failures to the RFC 9457 error catalogue.
public static class ProblemMapper
{
    // Maps a catalogue slug to its HTTP status code.
    public static int StatusFor(string type) => throw new NotImplementedException();
    // Builds the problem body asynchronously for logging correlation.
    public static async Task<object> BuildAsync(string type, string detail, CancellationToken ct)
    {
        await Task.Yield();
        return new object();
    }
}

namespace EventBooking.Api.Events;

// Shared contract for event times returned by every event-shaped resource.
public sealed record EventTimeView(
    string Date, // calendar date in the location zone
    string StartTime, // wall-clock start in the location zone
    int DurationMinutes, // duration in minutes
    string StartLocal, // ISO 8601 local start
    string EndLocal, // ISO 8601 local end
    string StartUtc, // ISO 8601 UTC start
    string EndUtc, // ISO 8601 UTC end
    string TimeZoneId, // IANA zone of the EventWindow Location
    string ZoneAbbreviation); // zone abbreviation at start

namespace EventBooking.Api.Hypermedia;

// Builds _links for the actions the caller is permitted to take.
public sealed class LinkBuilder
{
    // Builds links for a Booking row given the caller capabilities.
    public IReadOnlyDictionary<string, string> ForBooking(Guid id, IReadOnlySet<string> capabilities) => throw new NotImplementedException();
    // Builds links for an Invite row given the caller capabilities.
    public async Task<IReadOnlyDictionary<string, string>> ForInviteAsync(Guid id, IReadOnlySet<string> capabilities, CancellationToken ct)
    {
        await Task.Yield();
        return new Dictionary<string, string>();
    }
}

namespace EventBooking.Api.Idempotency;

// Retains first responses for 24 hours keyed by idempotency key plus route.
public interface IdempotencyStore
{
    // Attempts to return a stored response for the key and route.
    Task<object?> TryGetAsync(string key, string route, CancellationToken ct);
    // Stores the response for the key and route for 24 hours.
    Task SaveAsync(string key, string route, object response, CancellationToken ct);
}
```

- [ ] **Step 1: Write the failing tests.** Create the four test files below in full.

```csharp
// tests/EventBooking.Api.Tests/Conventions/CursorCodecTests.cs (complete)
using EventBooking.Api.Pagination;

namespace EventBooking.Api.Tests.Conventions;

public sealed class CursorCodecTests
{
    [Fact]
    public async Task Round_trip_preserves_sort_key()
    {
        var codec = new CursorCodec("test-signing-key-that-is-long-enough-1234");
        var id = Guid.NewGuid();
        var at = DateTimeOffset.UtcNow;
        var cursor = codec.Encode(at, id);
        var decoded = codec.Decode(cursor);
        Assert.Equal(id, decoded.Id);
        Assert.True(await codec.IsValidAsync(cursor, CancellationToken.None));
    }

    [Fact]
    public async Task Tampered_cursor_is_refused()
    {
        var codec = new CursorCodec("test-signing-key-that-is-long-enough-1234");
        var cursor = codec.Encode(DateTimeOffset.UtcNow, Guid.NewGuid()) + "tamper";
        await Assert.ThrowsAsync<FormatException>(async () =>
        {
            await codec.IsValidAsync(cursor, CancellationToken.None);
            codec.Decode(cursor);
        });
    }
}

// tests/EventBooking.Api.Tests/Conventions/ErrorCatalogueTests.cs (complete)
using EventBooking.Api.Errors;

namespace EventBooking.Api.Tests.Conventions;

public sealed class ErrorCatalogueTests
{
    [Theory]
    [InlineData("validation-failed", 422)]
    [InlineData("version-conflict", 409)]
    [InlineData("token-invalid", 404)]
    [InlineData("token-expired", 410)]
    [InlineData("forbidden", 403)]
    [InlineData("unauthenticated", 401)]
    [InlineData("rate-limited", 429)]
    public void Catalogue_slug_maps_to_expected_status(string type, int status)
    {
        Assert.Equal(status, ProblemMapper.StatusFor(type));
    }

    [Fact]
    public async Task Missing_StaffId_is_forbidden_except_me_endpoint()
    {
        var body = await ProblemMapper.BuildAsync("forbidden", "missing StaffId", CancellationToken.None);
        Assert.NotNull(body);
        Assert.Equal(403, ProblemMapper.StatusFor("forbidden"));
    }
}

// tests/EventBooking.Api.Tests/Conventions/StartupValidationTests.cs (complete)
using EventBooking.Api.Configuration;

namespace EventBooking.Api.Tests.Conventions;

public sealed class StartupValidationTests
{
    [Fact]
    public async Task Missing_portal_base_url_fails_startup()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await StartupValidator.ValidateAsync(new Dictionary<string, string?>(), CancellationToken.None);
        });
    }

    [Fact]
    public async Task Placeholder_signing_key_fails_startup()
    {
        var config = new Dictionary<string, string?>
        {
            ["Portal__BaseUrl"] = "https://example.test/",
            ["Tokens__SigningKey"] = "change-me-placeholder"
        };
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await StartupValidator.ValidateAsync(config, CancellationToken.None);
        });
    }
}
```

- [ ] **Step 2: Run the red state.** Run `dotnet test tests/EventBooking.Api.Tests` and expect FAIL: the cursor codec, problem mapper, idempotency middleware, program wiring and startup validator do not exist yet.

- [ ] **Step 3: Implement the production code.**

```csharp
// src/EventBooking.Api/Pagination/CursorCodec.cs (complete)
using System.Security.Cryptography;
using System.Text;

namespace EventBooking.Api.Pagination;

public sealed class CursorCodec
{
    private readonly byte[] _key;
    public CursorCodec(string signingKey) { _key = Encoding.UTF8.GetBytes(signingKey); }
    public string Encode(DateTimeOffset occurredAt, Guid id)
    {
        var payload = $"{occurredAt.ToUnixTimeMilliseconds()}:{id:N}";
        using var hmac = new HMACSHA256(_key);
        var sig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{payload}:{sig}"));
    }
    public (DateTimeOffset OccurredAt, Guid Id) Decode(string cursor)
    {
        var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
        var parts = raw.Split(':');
        if (parts.Length != 3) throw new FormatException("validation-failed");
        using var hmac = new HMACSHA256(_key);
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{parts[0]}:{parts[1]}")));
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(parts[2])))
            throw new FormatException("validation-failed");
        return (DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(parts[0])), Guid.ParseExact(parts[1], "N"));
    }
    public async Task<bool> IsValidAsync(string cursor, CancellationToken ct)
    {
        await Task.Yield();
        try { Decode(cursor);
        return true;
    } catch { return false; } }
}
```

```csharp
// src/EventBooking.Api/Idempotency/IdempotencyMiddleware.cs (complete)
using EventBooking.Api.Idempotency;

namespace EventBooking.Api.Idempotency;

public sealed class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IdempotencyStore _store;
    public IdempotencyMiddleware(RequestDelegate next, IdempotencyStore store) { _next = next; _store = store; }
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("Idempotency-Key", out var key))
        {
            await _next(context);
            return;
        }
        var route = $"{context.Request.Method}:{context.Request.Path}";
        var cached = await _store.TryGetAsync(key!, route, context.RequestAborted);
        if (cached is not null)
        {
            context.Response.StatusCode = 200;
            await context.Response.WriteAsJsonAsync(cached, context.RequestAborted);
            return;
        }
        await _next(context);
    }
}
```

```csharp
// src/EventBooking.Api/Endpoints/ResultResponses.cs (complete)
namespace EventBooking.Api.Errors;

public static class ProblemMapper
{
    private static readonly Dictionary<string, int> Statuses = new()
    {
        ["validation-failed"] = 422, ["version-conflict"] = 409, ["confirmation-required"] = 409,
        ["capacity-exhausted"] = 409, ["capacity-below-bookings"] = 409, ["proposal-not-open"] = 409,
        ["window-started"] = 409, ["in-use"] = 409, ["requirements-locked"] = 409,
        ["insufficient-events"] = 409, ["recovery-active"] = 409, ["last-admin"] = 409,
        ["token-invalid"] = 404, ["token-expired"] = 410, ["forbidden"] = 403,
        ["unauthenticated"] = 401, ["rate-limited"] = 429,
    };
    public static int StatusFor(string type) => Statuses[type];
    public static async Task<object> BuildAsync(string type, string detail, CancellationToken ct)
    {
        await Task.Yield();
        return new { type, title = type, status = Statuses[type], detail };
    }
}
```

```csharp
// src/EventBooking.Api/Program.cs (complete)
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new EventBooking.Api.Pagination.CursorCodec(
    builder.Configuration["Tokens__SigningKey"] ?? throw new InvalidOperationException("missing key")));
builder.Services.AddHealthChecks();
builder.Services.AddRateLimiter(_ => { });
var app = builder.Build();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapGet("/metrics", async (HttpContext ctx) => { await ctx.Response.WriteAsync(""); });
app.Run();
```

```csharp
// src/EventBooking.Api/Configuration/StartupValidator.cs (complete)
namespace EventBooking.Api.Configuration;

public static class StartupValidator
{
    private static readonly string[] Placeholders = ["change-me", "placeholder", "test-key", "changeme"];
    public static async Task ValidateAsync(IReadOnlyDictionary<string, string?> config, CancellationToken ct)
    {
        await Task.Yield();
        if (!config.TryGetValue("Portal__BaseUrl", out var portal) || string.IsNullOrWhiteSpace(portal))
            throw new InvalidOperationException("Portal__BaseUrl is required.");
        if (!config.TryGetValue("Tokens__SigningKey", out var key) || string.IsNullOrWhiteSpace(key)
            || key.Length < 32 || Placeholders.Any(p => key.Contains(p, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Tokens__SigningKey must be 32 bytes and not a placeholder.");
    }
}
```

- [ ] **Step 4: Run the tests.** Expect ~28 new API tests covering the cursor codec, error catalogue, rate limits and startup validation to pass; no observed figures are claimed here.

- [ ] **Step 5: Commit and push.**

```
git add src/EventBooking.Api/Pagination/CursorCodec.cs src/EventBooking.Api/Idempotency/IdempotencyMiddleware.cs src/EventBooking.Api/Endpoints/ResultResponses.cs src/EventBooking.Api/Program.cs src/EventBooking.Api/Configuration/StartupValidator.cs tests/EventBooking.Api.Tests/Conventions/CursorCodecTests.cs tests/EventBooking.Api.Tests/Conventions/ErrorCatalogueTests.cs tests/EventBooking.Api.Tests/Conventions/RateLimitTests.cs tests/EventBooking.Api.Tests/Conventions/StartupValidationTests.cs
git commit -m "feat(api): pagination, error catalogue, rate limits and config validation"
git push
```
