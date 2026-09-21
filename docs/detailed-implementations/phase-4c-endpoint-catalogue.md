# 04c — The endpoint catalogue (Task 22b)

[← Phase overview](phase-4-api-and-mcp.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 22a and is the second half of master Task 22. It puts design 05's routes
on the wire: fifty-five operations across fifteen endpoint files, each one translating a request
into exactly one handler call and rendering what comes back through Task 21's conventions. The
catalogue test parses design 05's own tables and fails if the OpenAPI document and the design
disagree about a single route.

> Use superpowers:executing-plans. This task is hand-authored: complete code and complete tests
> are written straight into this document, with no prototype. Compile and test-drive them
> yourself. The test counts below are what you should expect to reach, not figures observed by
> the author — nothing here has been run.

**Goal:** Exactly the routes, bodies and capabilities design 05 names, each with an
`operationId` and — for a staff operation — an `x-mcp-tool` extension naming its snake-case tool.
The `/api` link index lists every top-level resource. Every list response is Task 21's
`{ items, nextCursor }`; every failure is Task 21's problem body; every window is Task 21's one
event-time representation; every representation carries `_links` for the actions its caller may
take. No endpoint holds a business rule.

**Architecture:** One shared operation catalogue is the spine. It already exists as the ported
agent catalogue, and this task replaces its contents with design 05's operations: the OpenAPI
document, the `/api` index, `_links` and Task 23's tool list all read from it, so a route can
never appear in one and be missing from another. Endpoints take the handler and the caller
accessor by injection, bind the request, call the handler once, and return. The capability check
stays in the handler; the endpoint declares the capability only as OpenAPI metadata, which the
catalogue test compares against the design.

**Tech Stack:** .NET 10, xUnit, WebApplicationFactory, EF Core with Npgsql, Testcontainers,
PostgreSQL 16.

**Spec:** [Master Task 22](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[API design](../design/05-api-design.md),
[security and authentication](../design/06-security-and-authentication.md),
[functional requirements](../design/02-functional-requirements.md), [ontology](../ontology.md).

## What changes from the ported surface

The ported catalogue is the predecessor's, and almost none of its routes survive design 05. The
board and operations routes are gone with the handlers Task 20a deleted; `/api/admin/settings`
becomes `/api/settings`; `/api/admin/staff-access/{id}` becomes
`/api/staff-access/{staffUserId}/scope` with no delete; `/api/attendees/{id}/invite` becomes
`/api/attendees/{id}/invites`; `/api/booking/manage/{token}` becomes `/api/manage/{token}`;
cancellation moves from `DELETE /api/events/{id}` to `POST /api/events/{id}/cancel?confirm=`;
and eleven routes design 05 names have no ported equivalent at all. The catalogue test is what
makes that comprehensive rather than approximate.

## Global constraints

An endpoint calls exactly one handler and holds no rule. Two-step destructive actions read
`?confirm=` and render Task 21's `confirmation-required` body from the outcome the handler
returned — a translation, not a decision. Every cursor crossing the boundary is wrapped and
unwrapped by Task 21's signed codec, so an Application-level cursor never reaches a caller
unsigned. A missing or malformed `staff_id` is 403 on every route but `GET /api/me`
(contradiction #3). Attendee token routes are anonymous, carry both attendee rate-limit policies
and appear in no tool list.

## Review focus

STOP AND CHECK four things. The catalogue test reads design 05 and must fail when a route is
added to one side only — check it by deleting a row from the parsed table in a scratch copy and
watching it go red, because a parser that silently matches nothing passes everything. The
two-step tests assert the first call changed no state, not merely that it returned 409. The
capacity route's test asserts a Manager naming another type's identifier is refused, which is
Task 22a's rule seen through the endpoint. And the `_links` tests assert a Coordinator and an
Admin get different link sets from the same representation.

### Task 22b: The endpoint catalogue

**Files:**

- Modify: src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs (design 05's operations)
- Modify: src/EventBooking.Api/OpenApi/OpenApiConfiguration.cs (capability extension, tags)
- Modify: src/EventBooking.Api/Endpoints/ApiDiscoveryEndpoints.cs (every top-level resource)
- Create: src/EventBooking.Api/Endpoints/LocationEndpoints.cs
- Create: src/EventBooking.Api/Endpoints/AppointmentTypeEndpoints.cs
- Create: src/EventBooking.Api/Endpoints/AttendeeGroupEndpoints.cs
- Create: src/EventBooking.Api/Endpoints/SettingsEndpoints.cs
- Create: src/EventBooking.Api/Endpoints/StaffAccessEndpoints.cs
- Create: src/EventBooking.Api/Endpoints/EventProposalEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/EventEndpoints.cs (design 05's event routes)
- Modify: src/EventBooking.Api/Endpoints/AttendeeEndpoints.cs (design 05's attendee routes)
- Create: src/EventBooking.Api/Endpoints/DashboardEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/AuditEndpoints.cs (design 05's audit routes)
- Modify: src/EventBooking.Api/Endpoints/AppointmentWorkspaceEndpoints.cs (design 05's routes)
- Modify: src/EventBooking.Api/Endpoints/BookingEndpoints.cs (the book token only)
- Create: src/EventBooking.Api/Endpoints/ManageEndpoints.cs
- Create: src/EventBooking.Api/Endpoints/MeEndpoints.cs
- Create: src/EventBooking.Api/Contracts/ApiResponses.cs (the shared response records)
- Create: src/EventBooking.Api/Contracts/CallerCapabilities.cs (the capability set behind `_links`)
- Modify: src/EventBooking.Application/Access/StaffAccessAuthorizer.cs (the matrix predicate made internal)
- Delete: src/EventBooking.Api/Endpoints/AdminEndpoints.cs (split into settings and staff access)
- Delete: src/EventBooking.Api/Contracts/AdministrationHypermediaResponses.cs
- Delete: src/EventBooking.Api/Contracts/AttendeeHypermediaResponses.cs
- Delete: src/EventBooking.Api/Contracts/EventHypermediaResponses.cs
- Delete: src/EventBooking.Api/Contracts/OperationsHypermediaResponses.cs
- Modify: src/EventBooking.Api/Program.cs (the new mapping calls)
- Test: tests/EventBooking.Api.Tests/Catalogue/CatalogueFixture.cs (the shared seeding base)
- Test: tests/EventBooking.Api.Tests/Catalogue/DesignCatalogueTests.cs
- Test: tests/EventBooking.Api.Tests/Catalogue/ReferenceDataEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/Catalogue/NegotiationEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/Catalogue/EventEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/Catalogue/AttendeeEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/Catalogue/TokenEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/Catalogue/LinkTests.cs

The four ported hypermedia response files are deleted, not edited: they project the predecessor's
view shapes, every one of which has been replaced by a Phase 3 or Task 22a result. One new file
carries the response records design 05 names.

**Interfaces:**

```csharp
namespace EventBooking.Api.OpenApi;

// The ported record gains the capability the design names, so the catalogue test can compare
// design 05's third column against the code rather than against a reviewer's memory. Null means
// the design's "Any staff" or "Anonymous" — the two are told apart by RequiresBearer.
public sealed record AgentOperation(
    string OperationId, string Method, string Route, string Tag, string Summary,
    string Description, bool RequiresBearer, string? Capability, string? McpTool,
    string? ExclusionReason, AgentHints Hints);
```

```csharp
namespace EventBooking.Api.Contracts;

// The response records design 05 names, in one file. Each carries _links, and each window
// carries Task 21's one event-time representation rather than loose date and time fields.
public sealed record LocationResponse(
    Guid Id, string Code, string Name, string Address, string TimeZoneId, bool IsActive,
    long Version, IReadOnlyDictionary<string, ApiLink> Links);

public sealed record AppointmentTypeResponse(
    Guid Id, string Code, string Name, bool IsActive, long Version, string? ManagerDisplayName,
    IReadOnlyDictionary<string, ApiLink> Links);

public sealed record AttendeeGroupResponse(
    Guid Id, string Code, string Name, bool IsActive, long Version,
    IReadOnlyList<Guid> RequirementTypeIds, int MemberCount,
    IReadOnlyDictionary<string, ApiLink> Links);

public sealed record SettingsResponse(
    int InviteExpiryDays, int MaxAutoRetryCount, int InviteOptionCount, long Version,
    IReadOnlyDictionary<string, ApiLink> Links);

public sealed record StaffAccessResponse(
    Guid StaffUserId, string? StaffId, string? DisplayName, IReadOnlyList<string> Roles,
    Guid? AppointmentTypeId, long Version, IReadOnlyDictionary<string, ApiLink> Links);

public sealed record EventCapacityResponse(
    Guid AppointmentTypeId, string Code, string Name, int TotalHeadcount, int RemainingCapacity);

public sealed record EventResponse(
    Guid Id, Guid ProposalId, Guid LocationId, string LocationCode, string LocationName,
    EventTimeResponse Time, string Status, IReadOnlyList<EventCapacityResponse> Capacities,
    int ActiveBookings, IReadOnlyDictionary<string, ApiLink> Links);

public sealed record EventProposalResponse(
    Guid Id, Guid LocationId, string LocationCode, string LocationName, EventTimeResponse Time,
    string Status, int ListedTypeCount, int AcceptedTypeCount, int? MyAcceptedHeadcount,
    bool AcceptedByMe, bool CreatedByMe, IReadOnlyDictionary<string, ApiLink> Links);
```

- [ ] **Step 1: Write the failing catalogue test.** This is the one test that makes the rest
  comprehensive rather than approximate: it reads design 05 itself and compares the routes and
  capabilities it finds against the shipped OpenAPI document.

  ```csharp
  // tests/EventBooking.Api.Tests/Catalogue/DesignCatalogueTests.cs (complete)
  using System.Text.Json;
  using System.Text.RegularExpressions;
  using EventBooking.Api.OpenApi;

  namespace EventBooking.Api.Tests.Catalogue;

  /// <summary>
  /// Design 05's endpoint tables, parsed, against the OpenAPI document the host serves. A route
  /// on one side only fails this, which is what stops the contract and the code drifting.
  /// </summary>
  [Collection("api")]
  public sealed partial class DesignCatalogueTests(ApiFactory factory)
  {
      /// <summary>
      /// The one route the catalogue carries that design 05's tables do not name. Design 08
      /// requires it ("OpenTelemetry metrics are exposed at /metrics"); design 05 is the
      /// endpoint contract for callers, and a scrape endpoint has none.
      /// </summary>
      private static readonly HashSet<string> NotInDesign05 = new(StringComparer.Ordinal)
      {
          "GET /metrics",
      };

      /// <summary>
      /// The two operations that exist but are not OpenAPI operations: the document itself and
      /// the UI that renders it.
      /// </summary>
      private static readonly HashSet<string> NotOpenApiOperations = new(StringComparer.Ordinal)
      {
          "GET /openapi/v1.json",
          "GET /swagger",
      };

      /// <summary>
      /// A parser that matches nothing passes every other case in this file, so the row count is
      /// asserted first and on its own. Fifty-four is what this parser returns against design 05
      /// as it stands; if a table gains a row, this number changes with it, deliberately.
      /// </summary>
      [Fact]
      public void TheParserFindsEveryRowInDesignFive()
      {
          var design = ParseDesign();

          Assert.Equal(54, design.Count);
          Assert.Contains("GET /api/me", design.Keys);
          Assert.Contains("POST /api/events/{id}/cancel", design.Keys);
          Assert.Contains("POST /api/manage/{token}/cancel", design.Keys);
      }

      [Fact]
      public void TheCatalogueCarriesExactlyDesignFivesRoutesPlusTheScrapeEndpoint()
      {
          var design = ParseDesign().Keys.ToHashSet(StringComparer.Ordinal);
          var catalogue = AgentOperationCatalog.All.Values
              .Select(Key)
              .ToHashSet(StringComparer.Ordinal);

          Assert.Empty(design.Except(catalogue));
          Assert.Equal(NotInDesign05, catalogue.Except(design).ToHashSet(StringComparer.Ordinal));
      }

      [Fact]
      public async Task TheOpenApiDocumentCarriesExactlyTheCataloguedRoutes()
      {
          factory.SignedInAs = null;
          using var document = JsonDocument.Parse(
              await factory.CreateClient().GetStringAsync("/openapi/v1.json"));

          var served = new HashSet<string>(StringComparer.Ordinal);
          foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
          {
              foreach (var operation in path.Value.EnumerateObject())
              {
                  served.Add($"{operation.Name.ToUpperInvariant()} {path.Name}");
              }
          }

          var expected = AgentOperationCatalog.All.Values
              .Select(Key)
              .Where(key => !NotOpenApiOperations.Contains(key))
              .ToHashSet(StringComparer.Ordinal);

          Assert.Equal(expected.Order(StringComparer.Ordinal), served.Order(StringComparer.Ordinal));
      }

      /// <summary>
      /// Design 05's third column against the catalogue's. "Any staff" and "Staff; no capability
      /// needed" both mean a bearer token and no capability, which is not the same as anonymous
      /// — the catalogue distinguishes them, and so does this.
      /// </summary>
      [Fact]
      public void EveryRouteDemandsTheCapabilityDesignFiveNames()
      {
          var design = ParseDesign();
          var catalogue = AgentOperationCatalog.All.Values.ToDictionary(Key, x => x, StringComparer.Ordinal);

          foreach (var (key, expected) in design)
          {
              var actual = catalogue[key];
              Assert.Equal(expected.RequiresBearer, actual.RequiresBearer);

              // A row naming two capabilities is one the handler filters on instead of
              // demanding — settlement #12 for the Event reads, and Task 20b's own settlement
              // for the audit search. The catalogue declares no single capability for those,
              // and declaring one would be a lie the OpenAPI document then published.
              if (expected.Capabilities.Count == 1)
              {
                  Assert.Equal(expected.Capabilities[0], actual.Capability);
              }
              else
              {
                  Assert.Null(actual.Capability);
              }
          }
      }

      /// <summary>
      /// Every staff operation has a tool and every anonymous or token route has none (FR-14.1).
      /// Task 23 asserts the tool list equals this set; here the catalogue's own side is pinned.
      /// </summary>
      [Fact]
      public void EveryStaffOperationHasAToolAndNoTokenRouteDoes()
      {
          foreach (var operation in AgentOperationCatalog.All.Values)
          {
              if (operation.RequiresBearer)
              {
                  Assert.NotNull(operation.McpTool);
                  Assert.Matches("^[a-z][a-z0-9_]*$", operation.McpTool);
              }
              else
              {
                  Assert.Null(operation.McpTool);
                  Assert.False(string.IsNullOrWhiteSpace(operation.ExclusionReason));
              }
          }

          Assert.Equal(45, AgentOperationCatalog.All.Values.Count(x => x.McpTool is not null));
      }

      [Fact]
      public async Task ProtectedOperationsCarryBearerAndTheToolExtension()
      {
          factory.SignedInAs = null;
          using var document = JsonDocument.Parse(
              await factory.CreateClient().GetStringAsync("/openapi/v1.json"));
          var paths = document.RootElement.GetProperty("paths");

          foreach (var operation in AgentOperationCatalog.All.Values.Where(x => x.McpTool is not null))
          {
              var served = paths.GetProperty(operation.Route)
                  .GetProperty(operation.Method.ToLowerInvariant());
              Assert.Equal(operation.OperationId, served.GetProperty("operationId").GetString());
              Assert.Equal(operation.McpTool, served.GetProperty("x-mcp-tool").GetString());
              Assert.True(served.TryGetProperty("security", out _));
              if (operation.Capability is not null)
              {
                  Assert.Equal(
                      operation.Capability, served.GetProperty("x-capability").GetString());
              }
          }
      }

      private static string Key(AgentOperation operation) =>
          $"{operation.Method.ToUpperInvariant()} {operation.Route}";

      private sealed record DesignRoute(bool RequiresBearer, IReadOnlyList<string> Capabilities);

      /// <summary>
      /// Parses every "Method and path" table in design 05. Query strings are dropped — they are
      /// documentation of the filters, not part of the route — and a row naming two paths yields
      /// two entries. A row whose capability column reads "as above" inherits the row before it,
      /// which is how the design writes the single-event read.
      /// </summary>
      private static IReadOnlyDictionary<string, DesignRoute> ParseDesign()
      {
          var path = Path.Combine(RepositoryRoot(), "docs", "design", "05-api-design.md");
          var routes = new Dictionary<string, DesignRoute>(StringComparer.Ordinal);
          var inTable = false;
          DesignRoute? previous = null;

          foreach (var raw in File.ReadLines(path))
          {
              var line = raw.Trim();
              if (line.StartsWith("| Method and path", StringComparison.Ordinal))
              {
                  inTable = true;
                  previous = null;
                  continue;
              }

              if (!line.StartsWith('|'))
              {
                  inTable = false;
                  continue;
              }

              if (!inTable || line.StartsWith("|---", StringComparison.Ordinal) ||
                  line.StartsWith("|--", StringComparison.Ordinal))
              {
                  continue;
              }

              // Split on unescaped pipes only: one use-case cell contains "{appointmentTypeId \| null}".
              var cells = PipeSplitter().Split(line.Trim('|'))
                  .Select(cell => cell.Replace("\\|", "|", StringComparison.Ordinal).Trim())
                  .ToList();
              if (cells.Count < 2)
              {
                  continue;
              }

              var route = ParseCapability(cells.Count >= 3 ? cells[^1] : "Anonymous", previous);
              previous = route;

              var lastMethod = "GET";
              foreach (Match match in BacktickedPath().Matches(cells[0]))
              {
                  var text = match.Groups[1].Value;
                  var space = text.IndexOf(' ', StringComparison.Ordinal);
                  var method = space > 0 ? text[..space] : lastMethod;
                  var target = (space > 0 ? text[(space + 1)..] : text).Split('?')[0].Trim();
                  lastMethod = method;
                  routes[$"{method} {target}"] = route;
              }
          }

          return routes;
      }

      private static DesignRoute ParseCapability(string cell, DesignRoute? previous)
      {
          if (cell.Equals("as above", StringComparison.OrdinalIgnoreCase) && previous is not null)
          {
              return previous;
          }

          if (cell.Contains("Anonymous", StringComparison.OrdinalIgnoreCase))
          {
              return new DesignRoute(RequiresBearer: false, []);
          }

          var capabilities = BacktickedPath().Matches(cell)
              .Select(match => match.Groups[1].Value)
              .ToList();
          return new DesignRoute(RequiresBearer: true, capabilities);
      }

      /// <summary>Walks up from the test assembly to the directory holding the design package.</summary>
      private static string RepositoryRoot()
      {
          var directory = new DirectoryInfo(AppContext.BaseDirectory);
          while (directory is not null &&
              !Directory.Exists(Path.Combine(directory.FullName, "docs", "design")))
          {
              directory = directory.Parent;
          }

          return directory?.FullName
              ?? throw new InvalidOperationException("The design package was not found above the test assembly.");
      }

      [GeneratedRegex(@"(?<!\\)\|")]
      private static partial Regex PipeSplitter();

      [GeneratedRegex(@"`([^`]+)`")]
      private static partial Regex BacktickedPath();
  }
  ```

  The parser drops query strings on purpose. Design 05 writes `GET /api/locations?includeInactive=`
  because that is how a reader understands the filter; the route is `/api/locations`, and an
  OpenAPI path that carried the query string would not match any request.

  Two of design 05's rows name two paths each, and one of those — `GET /openapi/v1.json`,
  `/swagger` — omits the method on the second. The parser carries the last method forward for
  exactly that row rather than guessing per path, which is why the row-count case asserts a
  number rather than trusting the parse.

  **Write the failing endpoint tests.** One shared fixture seeds the reference data every area
  needs, so the six suites below are about routes and bodies rather than about arranging a
  database. It is an extension of the Task 1 factory, not a replacement for it.

  ```csharp
  // tests/EventBooking.Api.Tests/Catalogue/CatalogueFixture.cs (complete)
  using System.Net.Http.Headers;
  using System.Net.Http.Json;
  using System.Text.Json;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.AppointmentTypes;
  using EventBooking.Domain.AttendeeGroups;
  using EventBooking.Domain.Attendees;
  using EventBooking.Domain.Locations;
  using EventBooking.Infrastructure.Persistence;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;

  namespace EventBooking.Api.Tests.Catalogue;

  /// <summary>
  /// Seeding every catalogue suite shares. Each helper returns identifiers rather than entities:
  /// a test that holds an aggregate is a test that can assert against the write model instead of
  /// the wire, which is the one thing these suites must not do.
  /// </summary>
  public abstract class CatalogueSuite(ApiFactory factory)
  {
      /// <summary>The instant every seeded row is stamped with.</summary>
      protected static readonly DateTimeOffset Seeded = new(2026, 9, 21, 9, 0, 0, TimeSpan.Zero);

      /// <summary>Gives the tests a signed-in Admin and returns a client.</summary>
      /// <param name="staffId">The staff number claim.</param>
      /// <returns>The client.</returns>
      protected async Task<HttpClient> AdminAsync(string staffId = "U700001")
      {
          factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
          factory.StaffIdClaim = staffId;
          return factory.CreateClient();
      }

      /// <summary>Gives the tests a signed-in Coordinator and returns a client.</summary>
      /// <param name="staffId">The staff number claim.</param>
      /// <returns>The client.</returns>
      protected async Task<HttpClient> CoordinatorAsync(string staffId = "U700002")
      {
          factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
          factory.StaffIdClaim = staffId;
          return factory.CreateClient();
      }

      /// <summary>Gives the tests a Manager scoped to one type and returns a client.</summary>
      /// <param name="appointmentTypeId">The scope.</param>
      /// <param name="staffId">The staff number claim.</param>
      /// <returns>The client.</returns>
      protected async Task<HttpClient> ManagerAsync(Guid appointmentTypeId, string staffId = "U700003")
      {
          factory.SignedInAs = await factory.GivenStaffAsync([Role.Manager], appointmentTypeId);
          factory.StaffIdClaim = staffId;
          return factory.CreateClient();
      }

      /// <summary>Seeds an active appointment type and returns its identifier.</summary>
      /// <param name="code">The canonical code.</param>
      /// <returns>The identifier.</returns>
      protected async Task<Guid> GivenAppointmentTypeAsync(string code)
      {
          await using var scoped = NewScope();
          var type = AppointmentType.Create(Guid.NewGuid(), code, code);
          scoped.Context.AppointmentTypes.Add(type);
          await scoped.Context.SaveChangesAsync();
          return type.Id;
      }

      /// <summary>Seeds an active location and returns its identifier.</summary>
      /// <param name="code">The canonical code.</param>
      /// <param name="timeZoneId">The IANA zone.</param>
      /// <returns>The identifier.</returns>
      protected async Task<Guid> GivenLocationAsync(string code, string timeZoneId = "Europe/London")
      {
          await using var scoped = NewScope();
          var location = Location.Create(
              Guid.NewGuid(), code, code, "1 Test Street", timeZoneId, ProposalFixture.Zones);
          scoped.Context.Locations.Add(location);
          await scoped.Context.SaveChangesAsync();
          return location.Id;
      }

      /// <summary>Seeds an active group mapping one type, and returns its identifier.</summary>
      /// <param name="code">The canonical code.</param>
      /// <param name="appointmentTypeId">The type its members require.</param>
      /// <returns>The identifier.</returns>
      protected async Task<Guid> GivenAttendeeGroupAsync(string code, Guid appointmentTypeId)
      {
          await using var scoped = NewScope();
          var group = AttendeeGroup.Create(
              Guid.NewGuid(), code, code, [appointmentTypeId], [appointmentTypeId]);
          scoped.Context.AttendeeGroups.Add(group);
          await scoped.Context.SaveChangesAsync();
          return group.Id;
      }

      /// <summary>Seeds one attendee in a group and returns its identifier.</summary>
      /// <param name="groupId">The group.</param>
      /// <param name="email">The unique email.</param>
      /// <returns>The identifier.</returns>
      protected async Task<Guid> GivenAttendeeAsync(Guid groupId, string email)
      {
          await using var scoped = NewScope();
          var group = await scoped.Context.AttendeeGroups
              .Include(g => g.Requirements)
              .SingleAsync(g => g.Id == groupId);
          var attendee = Attendee.Create(Guid.NewGuid(), "Test Attendee", email, group, Seeded);
          scoped.Context.Attendees.Add(attendee);
          await scoped.Context.SaveChangesAsync();
          return attendee.Id;
      }

      /// <summary>Reads a JSON response body as an element, whatever its status.</summary>
      /// <param name="response">The response.</param>
      /// <returns>The body.</returns>
      protected static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
          await response.Content.ReadFromJsonAsync<JsonElement>();

      /// <summary>Sends a request carrying an idempotency key, for the create endpoints.</summary>
      /// <param name="client">The client.</param>
      /// <param name="url">The route.</param>
      /// <param name="body">The request body.</param>
      /// <returns>The response.</returns>
      protected static async Task<HttpResponseMessage> PostAsync(
          HttpClient client, string url, object body)
      {
          using var request = new HttpRequestMessage(HttpMethod.Post, url)
          {
              Content = JsonContent.Create(body),
          };
          request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
          return await client.SendAsync(request);
      }

      /// <summary>
      /// A context on the factory's own database, owning the scope it came from. Resolving a
      /// scoped context and letting the scope fall out of use disposes the context underneath
      /// the caller, so the two are disposed together here.
      /// </summary>
      /// <returns>The scoped context.</returns>
      protected ScopedContext NewScope() => new(factory.Services.CreateScope());

      /// <summary>A scoped database context that disposes its scope with it.</summary>
      /// <param name="scope">The scope to own.</param>
      protected sealed class ScopedContext(IServiceScope scope) : IAsyncDisposable
      {
          /// <summary>Gets the context.</summary>
          public EventBookingDbContext Context { get; } =
              scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

          /// <inheritdoc />
          public async ValueTask DisposeAsync()
          {
              await Context.DisposeAsync();
              scope.Dispose();
          }
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Api.Tests/Catalogue/ReferenceDataEndpointTests.cs (complete)
  using System.Net;
  using System.Net.Http.Json;

  namespace EventBooking.Api.Tests.Catalogue;

  /// <summary>The eleven reference-data and settings routes.</summary>
  [Collection("api")]
  public sealed class ReferenceDataEndpointTests(ApiFactory factory)
      : CatalogueSuite(factory)
  {
      [Fact]
      public async Task AnAdminCreatesAndUpdatesALocation()
      {
          var client = await AdminAsync();

          var created = await PostAsync(client, "/api/locations", new
          {
              code = "REF_LON",
              name = "London",
              address = "1 Test Street",
              timeZoneId = "Europe/London",
          });
          var body = await BodyAsync(created);
          var id = body.GetProperty("id").GetGuid();

          var updated = await client.PutAsJsonAsync($"/api/locations/{id}", new
          {
              name = "London Bridge",
              address = "2 Test Street",
              timeZoneId = "Europe/London",
              isActive = true,
              expectedVersion = body.GetProperty("version").GetInt64(),
          });

          Assert.Equal(HttpStatusCode.Created, created.StatusCode);
          Assert.Equal($"/api/locations/{id}", created.Headers.Location?.ToString());
          Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
          Assert.Equal("London Bridge", (await BodyAsync(updated)).GetProperty("name").GetString());
      }

      [Fact]
      public async Task AStaleVersionIsAVersionConflictCarryingTheCurrentOne()
      {
          var client = await AdminAsync();
          var created = await BodyAsync(await PostAsync(client, "/api/locations", new
          {
              code = "REF_STALE", name = "Stale", address = "1 Test Street",
              timeZoneId = "Europe/London",
          }));
          var id = created.GetProperty("id").GetGuid();

          var response = await client.PutAsJsonAsync($"/api/locations/{id}", new
          {
              name = "Other", address = "1 Test Street", timeZoneId = "Europe/London",
              isActive = true, expectedVersion = 999L,
          });

          Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
          var problem = await BodyAsync(response);
          Assert.Equal("version-conflict", problem.GetProperty("type").GetString());
          Assert.True(problem.GetProperty("current").GetProperty("currentVersion").GetInt64() > 0);
      }

      /// <summary>The forbidden case from design 06's matrix: reference data is Admin-only.</summary>
      [Fact]
      public async Task ACoordinatorCannotWriteReferenceData()
      {
          var client = await CoordinatorAsync();

          var response = await PostAsync(client, "/api/appointment-types", new
          {
              code = "REF_DENIED", name = "Denied",
          });

          Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
          Assert.Equal("forbidden", (await BodyAsync(response)).GetProperty("type").GetString());
      }

      /// <summary>Reads are open to any staff member; only writes need the capability.</summary>
      [Fact]
      public async Task ACoordinatorMayReadReferenceData()
      {
          await GivenAppointmentTypeAsync("REF_READ");
          var client = await CoordinatorAsync();

          var response = await client.GetAsync("/api/appointment-types");

          Assert.Equal(HttpStatusCode.OK, response.StatusCode);
          Assert.NotEmpty((await BodyAsync(response)).GetProperty("items").EnumerateArray());
      }

      [Fact]
      public async Task InactiveRowsAppearOnlyWhenAsked()
      {
          var client = await AdminAsync();
          var created = await BodyAsync(await PostAsync(client, "/api/locations", new
          {
              code = "REF_HIDDEN", name = "Hidden", address = "1 Test Street",
              timeZoneId = "Europe/London",
          }));
          var id = created.GetProperty("id").GetGuid();
          await client.PutAsJsonAsync($"/api/locations/{id}", new
          {
              name = "Hidden", address = "1 Test Street", timeZoneId = "Europe/London",
              isActive = false, expectedVersion = created.GetProperty("version").GetInt64(),
          });

          var hidden = await BodyAsync(await client.GetAsync("/api/locations"));
          var shown = await BodyAsync(await client.GetAsync("/api/locations?includeInactive=true"));

          Assert.DoesNotContain(
              hidden.GetProperty("items").EnumerateArray(),
              x => x.GetProperty("id").GetGuid() == id);
          Assert.Contains(
              shown.GetProperty("items").EnumerateArray(),
              x => x.GetProperty("id").GetGuid() == id);
      }

      [Fact]
      public async Task SettingsRoundTripThroughTheirOwnRoute()
      {
          var client = await AdminAsync();

          var read = await BodyAsync(await client.GetAsync("/api/settings"));
          var written = await client.PutAsJsonAsync("/api/settings", new
          {
              inviteExpiryDays = 10,
              maxAutoRetryCount = 1,
              inviteOptionCount = 4,
              expectedVersion = read.GetProperty("version").GetInt64(),
          });

          Assert.Equal(HttpStatusCode.OK, written.StatusCode);
          var body = await BodyAsync(written);
          Assert.Equal(10, body.GetProperty("inviteExpiryDays").GetInt32());
          Assert.Equal(4, body.GetProperty("inviteOptionCount").GetInt32());
      }

      [Fact]
      public async Task ASettingsValueOutsideItsRangeIsAValidationFailure()
      {
          var client = await AdminAsync();
          var read = await BodyAsync(await client.GetAsync("/api/settings"));

          var response = await client.PutAsJsonAsync("/api/settings", new
          {
              inviteExpiryDays = 7, maxAutoRetryCount = 2, inviteOptionCount = 9,
              expectedVersion = read.GetProperty("version").GetInt64(),
          });

          Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
          Assert.Equal(
              "validation-failed", (await BodyAsync(response)).GetProperty("type").GetString());
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Api.Tests/Catalogue/NegotiationEndpointTests.cs (complete)
  using System.Net;
  using System.Net.Http.Json;

  namespace EventBooking.Api.Tests.Catalogue;

  /// <summary>
  /// The five proposal routes. The two cases the master plan names by hand are the shape of the
  /// creation response: three listed types leaves it Open, one confirms it on the spot.
  /// </summary>
  [Collection("api")]
  public sealed class NegotiationEndpointTests(ApiFactory factory)
      : CatalogueSuite(factory)
  {
      [Fact]
      public async Task ProposingForThreeTypesReturnsCreatedAndOpen()
      {
          var medical = await GivenAppointmentTypeAsync("NEG3_MED");
          var fitting = await GivenAppointmentTypeAsync("NEG3_FIT");
          var induction = await GivenAppointmentTypeAsync("NEG3_IND");
          await GivenManagersForAsync(fitting, induction);
          var location = await GivenLocationAsync("NEG3_LON");
          var client = await ManagerAsync(medical, "U700110");

          var response = await PostAsync(client, "/api/event-proposals", new
          {
              locationId = location,
              date = "2026-11-10",
              startTime = "09:30",
              durationMinutes = 240,
              appointmentTypeIds = new[] { medical, fitting, induction },
              headcount = 8,
          });

          Assert.Equal(HttpStatusCode.Created, response.StatusCode);
          var body = await BodyAsync(response);
          Assert.Equal("Open", body.GetProperty("status").GetString());
          Assert.Equal(JsonValueKind.Null, body.GetProperty("eventId").ValueKind);
      }

      [Fact]
      public async Task ProposingForOneTypeReturnsCreatedConfirmedAndAnEventId()
      {
          var medical = await GivenAppointmentTypeAsync("NEG1_MED");
          var location = await GivenLocationAsync("NEG1_LON");
          var client = await ManagerAsync(medical, "U700111");

          var response = await PostAsync(client, "/api/event-proposals", new
          {
              locationId = location,
              date = "2026-11-11",
              startTime = "09:30",
              durationMinutes = 240,
              appointmentTypeIds = new[] { medical },
              headcount = 8,
          });

          Assert.Equal(HttpStatusCode.Created, response.StatusCode);
          var body = await BodyAsync(response);
          Assert.Equal("Confirmed", body.GetProperty("status").GetString());
          Assert.NotEqual(Guid.Empty, body.GetProperty("eventId").GetGuid());
      }

      [Fact]
      public async Task AcceptanceIsRecordedRevisedAndWithdrawn()
      {
          var (proposalId, otherType) = await GivenOpenProposalAsync("NEGA");
          var client = await ManagerAsync(otherType, "U700112");

          var recorded = await client.PutAsJsonAsync(
              $"/api/event-proposals/{proposalId}/acceptance", new { headcount = 5 });
          var revised = await client.PutAsJsonAsync(
              $"/api/event-proposals/{proposalId}/acceptance", new { headcount = 6 });
          var withdrawn = await client.DeleteAsync(
              $"/api/event-proposals/{proposalId}/acceptance");

          Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
          Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
          Assert.Equal(HttpStatusCode.NoContent, withdrawn.StatusCode);
      }

      /// <summary>
      /// The typed refusal Task 21 added. A withdrawn proposal is not open, and the body says so
      /// with a slug rather than only in its prose.
      /// </summary>
      [Fact]
      public async Task AcceptingAWithdrawnProposalIsProposalNotOpen()
      {
          var (proposalId, otherType) = await GivenOpenProposalAsync("NEGW");
          var proposer = await ManagerAsync(await ProposerTypeOfAsync(proposalId), "U700113");
          await PostAsync(proposer, $"/api/event-proposals/{proposalId}/withdraw", new { });
          var client = await ManagerAsync(otherType, "U700114");

          var response = await client.PutAsJsonAsync(
              $"/api/event-proposals/{proposalId}/acceptance", new { headcount = 5 });

          Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
          Assert.Equal(
              "proposal-not-open", (await BodyAsync(response)).GetProperty("type").GetString());
      }

      /// <summary>The forbidden case: negotiation is a Manager capability.</summary>
      [Fact]
      public async Task ACoordinatorCannotPropose()
      {
          var location = await GivenLocationAsync("NEGF_LON");
          var type = await GivenAppointmentTypeAsync("NEGF_MED");
          var client = await CoordinatorAsync("U700115");

          var response = await PostAsync(client, "/api/event-proposals", new
          {
              locationId = location, date = "2026-11-12", startTime = "09:30",
              durationMinutes = 240, appointmentTypeIds = new[] { type }, headcount = 8,
          });

          Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
      }

      [Fact]
      public async Task TheProposalListIsTheCallersOwnTypesAndFiltersByStatus()
      {
          var (proposalId, otherType) = await GivenOpenProposalAsync("NEGL");
          var client = await ManagerAsync(otherType, "U700116");

          var open = await BodyAsync(
              await client.GetAsync("/api/event-proposals?status=Open&limit=50"));
          var confirmed = await BodyAsync(
              await client.GetAsync("/api/event-proposals?status=Confirmed&limit=50"));

          Assert.Contains(
              open.GetProperty("items").EnumerateArray(),
              x => x.GetProperty("id").GetGuid() == proposalId);
          Assert.DoesNotContain(
              confirmed.GetProperty("items").EnumerateArray(),
              x => x.GetProperty("id").GetGuid() == proposalId);
      }

      [Fact]
      public async Task AnUnknownStatusIsAValidationFailureRatherThanAnEmptyPage()
      {
          var (_, otherType) = await GivenOpenProposalAsync("NEGS");
          var client = await ManagerAsync(otherType, "U700117");

          var response = await client.GetAsync("/api/event-proposals?status=open");

          Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
      }

      /// <summary>
      /// Every listed type must have a current Manager before a proposal can be created, so a
      /// case listing three types has to seed two more profiles than it signs in as.
      /// </summary>
      /// <param name="appointmentTypeIds">The types needing a Manager.</param>
      /// <returns>A task tracking the seeding.</returns>
      private async Task GivenManagersForAsync(params Guid[] appointmentTypeIds)
      {
          foreach (var appointmentTypeId in appointmentTypeIds)
          {
              await factory.GivenStaffAsync([Role.Manager], appointmentTypeId);
          }
      }

      /// <summary>
      /// Seeds two types with a Manager each and proposes through the route as the first,
      /// leaving the proposal Open because the second type has not accepted. Returns the
      /// proposal and the second type, which is the one the calling case signs in as.
      /// </summary>
      /// <param name="prefix">A per-case code prefix, so the suite's rows never collide.</param>
      /// <returns>The proposal identifier and the non-proposing type.</returns>
      private async Task<(Guid ProposalId, Guid OtherType)> GivenOpenProposalAsync(string prefix)
      {
          var proposerType = await GivenAppointmentTypeAsync($"{prefix}_ONE");
          var otherType = await GivenAppointmentTypeAsync($"{prefix}_TWO");
          await GivenManagersForAsync(otherType);
          var location = await GivenLocationAsync($"{prefix}_LOC");
          var proposer = await ManagerAsync(proposerType, $"U7002{prefix.Length:D2}");

          var created = await BodyAsync(await PostAsync(proposer, "/api/event-proposals", new
          {
              locationId = location,
              date = "2026-11-20",
              startTime = "09:30",
              durationMinutes = 240,
              appointmentTypeIds = new[] { proposerType, otherType },
              headcount = 8,
          }));

          return (created.GetProperty("id").GetGuid(), otherType);
      }

      /// <summary>
      /// Reads the proposing type back. Settlement #1 judges withdrawal against the type, not
      /// the identity, so a case that withdraws has to sign in as that type's Manager.
      /// </summary>
      /// <param name="proposalId">The proposal.</param>
      /// <returns>The proposing appointment type.</returns>
      private async Task<Guid> ProposerTypeOfAsync(Guid proposalId)
      {
          await using var scoped = NewScope();
          return await scoped.Context.EventProposals
              .Where(p => p.Id == proposalId)
              .Select(p => p.ProposerAppointmentTypeId)
              .SingleAsync();
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Api.Tests/Catalogue/EventEndpointTests.cs (complete)
  using System.Net;
  using System.Net.Http.Json;
  using EventBooking.Domain.Access;
  using Microsoft.EntityFrameworkCore;

  namespace EventBooking.Api.Tests.Catalogue;

  /// <summary>
  /// The five event routes. The two-step cancellation is the case the master plan names: the
  /// first call must change nothing, which is asserted against the stored status rather than
  /// against the second response.
  /// </summary>
  [Collection("api")]
  public sealed class EventEndpointTests(ApiFactory factory)
      : CatalogueSuite(factory)
  {
      [Fact]
      public async Task ACoordinatorSeesEveryCapacityAndAManagerSeesOne()
      {
          var seeded = await GivenConfirmedEventAsync("EVT_SCOPE");
          var coordinator = await CoordinatorAsync("U700201");
          var manager = await ManagerAsync(seeded.SecondType, "U700202");

          var everyType = await BodyAsync(
              await coordinator.GetAsync($"/api/events/{seeded.EventId}"));
          var ownType = await BodyAsync(
              await manager.GetAsync($"/api/events/{seeded.EventId}"));

          Assert.Equal(2, everyType.GetProperty("capacities").GetArrayLength());
          var only = Assert.Single(ownType.GetProperty("capacities").EnumerateArray());
          Assert.Equal(seeded.SecondType, only.GetProperty("appointmentTypeId").GetGuid());
      }

      /// <summary>
      /// Task 21's one event-time representation, seen through a route. Every member design 05
      /// names has to be present, because the Web work in Phase 5 renders from these and nothing
      /// else.
      /// </summary>
      [Fact]
      public async Task AnEventCarriesEveryMemberOfTheTimeContract()
      {
          var seeded = await GivenConfirmedEventAsync("EVT_TIME");
          var client = await CoordinatorAsync("U700203");

          var time = (await BodyAsync(await client.GetAsync($"/api/events/{seeded.EventId}")))
              .GetProperty("time");

          foreach (var member in new[]
          {
              "date", "startTime", "durationMinutes", "startLocal", "endLocal",
              "startUtc", "endUtc", "timeZoneId", "zoneAbbreviation",
          })
          {
              Assert.True(time.TryGetProperty(member, out _), member);
          }
      }

      [Fact]
      public async Task AnEventOutsideTheCallersScopeIsNotFound()
      {
          var seeded = await GivenConfirmedEventAsync("EVT_HIDDEN");
          var stranger = await GivenAppointmentTypeAsync("EVT_HIDDEN_OTHER");
          var client = await ManagerAsync(stranger, "U700204");

          var response = await client.GetAsync($"/api/events/{seeded.EventId}");

          Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
          Assert.Equal("not-found", (await BodyAsync(response)).GetProperty("type").GetString());
      }

      [Fact]
      public async Task TheListFiltersByLocationAndDate()
      {
          var seeded = await GivenConfirmedEventAsync("EVT_FILTER");
          var client = await CoordinatorAsync("U700205");

          var matching = await BodyAsync(await client.GetAsync(
              $"/api/events?locationId={seeded.LocationId}&from=2026-11-01&to=2026-11-30&limit=50"));
          var missing = await BodyAsync(await client.GetAsync(
              "/api/events?from=2027-01-01&to=2027-01-31&limit=50"));

          Assert.Contains(
              matching.GetProperty("items").EnumerateArray(),
              x => x.GetProperty("id").GetGuid() == seeded.EventId);
          Assert.Empty(missing.GetProperty("items").EnumerateArray());
      }

      /// <summary>
      /// The route names the type. A Manager naming somebody else's is refused — Task 22a's rule,
      /// reached through the endpoint, which is the only place a caller can supply the wrong one.
      /// </summary>
      [Fact]
      public async Task AManagerCannotAdjustAnotherTypesCapacity()
      {
          var seeded = await GivenConfirmedEventAsync("EVT_CAP");
          var client = await ManagerAsync(seeded.SecondType, "U700206");

          var own = await client.PutAsJsonAsync(
              $"/api/events/{seeded.EventId}/capacities/{seeded.SecondType}",
              new { totalHeadcount = 12 });
          var other = await client.PutAsJsonAsync(
              $"/api/events/{seeded.EventId}/capacities/{seeded.ProposerType}",
              new { totalHeadcount = 12 });

          Assert.Equal(HttpStatusCode.OK, own.StatusCode);
          Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
      }

      /// <summary>
      /// Two-step cancellation. The first call must leave the event Active — asserting only the
      /// 409 would pass even if the endpoint had cancelled it and then reported a consequence.
      /// </summary>
      [Fact]
      public async Task CancellationIsTwoStepAndTheFirstCallChangesNothing()
      {
          var seeded = await GivenConfirmedEventAsync("EVT_CANCEL");
          var client = await CoordinatorAsync("U700207");

          var first = await PostAsync(client, $"/api/events/{seeded.EventId}/cancel", new { });
          var afterFirst = await StatusOfAsync(seeded.EventId);
          var second = await PostAsync(
              client, $"/api/events/{seeded.EventId}/cancel?confirm=true", new { });
          var afterSecond = await StatusOfAsync(seeded.EventId);

          Assert.Equal(HttpStatusCode.Conflict, first.StatusCode);
          var problem = await BodyAsync(first);
          Assert.Equal("confirmation-required", problem.GetProperty("type").GetString());
          Assert.True(problem.TryGetProperty("consequence", out _));
          Assert.Equal("Active", afterFirst);

          Assert.Equal(HttpStatusCode.OK, second.StatusCode);
          Assert.Equal("Cancelled", afterSecond);
      }

      [Fact]
      public async Task TheCancellableListExcludesACancelledEvent()
      {
          var seeded = await GivenConfirmedEventAsync("EVT_LIST");
          var client = await CoordinatorAsync("U700208");
          await PostAsync(client, $"/api/events/{seeded.EventId}/cancel?confirm=true", new { });

          var body = await BodyAsync(await client.GetAsync("/api/events/cancellable?limit=50"));

          Assert.DoesNotContain(
              body.GetProperty("items").EnumerateArray(),
              x => x.GetProperty("id").GetGuid() == seeded.EventId);
      }

      private sealed record SeededEvent(
          Guid EventId, Guid LocationId, Guid ProposerType, Guid SecondType);

      /// <summary>
      /// Proposes for two types and accepts as both, which confirms the proposal and creates the
      /// event with two capacity rows — the shape every case here needs.
      /// </summary>
      /// <param name="prefix">A per-case code prefix, so the suite's rows never collide.</param>
      /// <returns>The seeded event.</returns>
      private async Task<SeededEvent> GivenConfirmedEventAsync(string prefix)
      {
          var proposerType = await GivenAppointmentTypeAsync($"{prefix}_ONE");
          var secondType = await GivenAppointmentTypeAsync($"{prefix}_TWO");
          var locationId = await GivenLocationAsync($"{prefix}_LOC");

          var proposer = await ManagerAsync(proposerType, $"U7003{prefix.Length:D2}");
          await factory.GivenStaffAsync([Role.Manager], secondType);
          var created = await BodyAsync(await PostAsync(proposer, "/api/event-proposals", new
          {
              locationId,
              date = "2026-11-25",
              startTime = "09:30",
              durationMinutes = 240,
              appointmentTypeIds = new[] { proposerType, secondType },
              headcount = 8,
          }));
          var proposalId = created.GetProperty("id").GetGuid();

          var second = await ManagerAsync(secondType, $"U7004{prefix.Length:D2}");
          var confirmed = await BodyAsync(await second.PutAsJsonAsync(
              $"/api/event-proposals/{proposalId}/acceptance", new { headcount = 6 }));

          return new SeededEvent(
              confirmed.GetProperty("eventId").GetGuid(), locationId, proposerType, secondType);
      }

      /// <summary>Reads the stored status, so a two-step case can assert state not responses.</summary>
      /// <param name="eventId">The event.</param>
      /// <returns>The status name.</returns>
      private async Task<string> StatusOfAsync(Guid eventId)
      {
          await using var scoped = NewScope();
          var status = await scoped.Context.Events
              .Where(e => e.Id == eventId)
              .Select(e => e.Status)
              .SingleAsync();
          return status.ToString();
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Api.Tests/Catalogue/AttendeeEndpointTests.cs (complete)
  using System.Net;
  using System.Net.Http.Json;
  using System.Text;
  using Microsoft.EntityFrameworkCore;

  namespace EventBooking.Api.Tests.Catalogue;

  /// <summary>The thirteen attendee routes, including the two-step delete and the CSV import.</summary>
  [Collection("api")]
  public sealed class AttendeeEndpointTests(ApiFactory factory)
      : CatalogueSuite(factory)
  {
      [Fact]
      public async Task AttendeesAreCreatedEditedAndListed()
      {
          var type = await GivenAppointmentTypeAsync("ATT_CRUD_TYPE");
          var group = await GivenAttendeeGroupAsync("ATT_CRUD", type);
          var client = await CoordinatorAsync("U700301");

          var created = await PostAsync(client, "/api/attendees", new
          {
              name = "Ada Lovelace", email = "ada-crud@example.com", attendeeGroupId = group,
          });
          var id = (await BodyAsync(created)).GetProperty("id").GetGuid();
          var edited = await client.PutAsJsonAsync($"/api/attendees/{id}", new
          {
              name = "Ada King", email = "ada-crud@example.com", attendeeGroupId = group,
          });
          var listed = await BodyAsync(
              await client.GetAsync($"/api/attendees?groupId={group}&limit=50"));

          Assert.Equal(HttpStatusCode.Created, created.StatusCode);
          Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
          Assert.Single(listed.GetProperty("items").EnumerateArray());
      }

      /// <summary>The forbidden case: attendee data is a Coordinator capability, never Admin's.</summary>
      [Fact]
      public async Task AnAdminCannotReadAttendees()
      {
          var client = await AdminAsync("U700302");

          var response = await client.GetAsync("/api/attendees?limit=50");

          Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
      }

      [Fact]
      public async Task DeletionIsTwoStepAndTheFirstCallChangesNothing()
      {
          var type = await GivenAppointmentTypeAsync("ATT_DEL_TYPE");
          var group = await GivenAttendeeGroupAsync("ATT_DEL", type);
          var attendee = await GivenAttendeeAsync(group, "ada-del@example.com");
          var client = await CoordinatorAsync("U700303");

          var first = await client.DeleteAsync($"/api/attendees/{attendee}");
          var stillThere = await ExistsAsync(attendee);
          var second = await client.DeleteAsync($"/api/attendees/{attendee}?confirm=true");
          var gone = !await ExistsAsync(attendee);

          Assert.Equal(HttpStatusCode.Conflict, first.StatusCode);
          Assert.Equal(
              "confirmation-required", (await BodyAsync(first)).GetProperty("type").GetString());
          Assert.True(stillThere);
          Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
          Assert.True(gone);
      }

      /// <summary>
      /// Design 05 says multipart, and the whole file is one transaction: a bad row anywhere
      /// means nothing is written, with the line number in the body (FR-4.3).
      /// </summary>
      [Fact]
      public async Task AnImportIsAllOrNothingAndNamesTheFailingLine()
      {
          var type = await GivenAppointmentTypeAsync("ATT_CSV_TYPE");
          var group = await GivenAttendeeGroupAsync("ATT_CSV", type);
          var client = await CoordinatorAsync("U700304");
          var csv = string.Join('\n',
              "name,email,attendeeGroupCode",
              "Good Row,good@example.com,ATT_CSV",
              "Bad Row,not-an-email,ATT_CSV");

          var response = await ImportAsync(client, csv);

          Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
          var problem = await BodyAsync(response);
          Assert.Equal("validation-failed", problem.GetProperty("type").GetString());
          Assert.Equal(3, problem.GetProperty("errors")[0].GetProperty("line").GetInt32());
          Assert.False(await AnyInGroupAsync(group));
      }

      [Fact]
      public async Task AnImportOverTheRowLimitIsRefused()
      {
          var type = await GivenAppointmentTypeAsync("ATT_BIG_TYPE");
          await GivenAttendeeGroupAsync("ATT_BIG", type);
          var client = await CoordinatorAsync("U700305");
          var rows = new StringBuilder("name,email,attendeeGroupCode\n");
          for (var index = 0; index < 1001; index++)
          {
              rows.Append($"Row {index},row{index}@example.com,ATT_BIG\n");
          }

          var response = await ImportAsync(client, rows.ToString());

          Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
      }

      [Fact]
      public async Task TheEligibleCountAndTheInviteShareTheirLocationFilter()
      {
          var type = await GivenAppointmentTypeAsync("ATT_INV_TYPE");
          var group = await GivenAttendeeGroupAsync("ATT_INV", type);
          var attendee = await GivenAttendeeAsync(group, "ada-inv@example.com");
          var location = await GivenLocationAsync("ATT_INV_LOC");
          var client = await CoordinatorAsync("U700306");

          var counted = await BodyAsync(await client.GetAsync(
              $"/api/attendees/{attendee}/eligible-event-count?locationIds={location}"));
          var invited = await PostAsync(
              client, $"/api/attendees/{attendee}/invites", new { locationIds = new[] { location } });

          Assert.Equal(0, counted.GetProperty("count").GetInt32());
          Assert.Equal(HttpStatusCode.OK, invited.StatusCode);
          Assert.Equal(
              "AwaitingAvailability", (await BodyAsync(invited)).GetProperty("status").GetString());
      }

      /// <summary>Settlement #13: readiness is a dashboard read, not an attendee-management one.</summary>
      [Fact]
      public async Task ReadinessAnswersUnderTheDashboardCapability()
      {
          var type = await GivenAppointmentTypeAsync("ATT_RDY_TYPE");
          var group = await GivenAttendeeGroupAsync("ATT_RDY", type);
          var attendee = await GivenAttendeeAsync(group, "ada-rdy@example.com");
          var coordinator = await CoordinatorAsync("U700307");
          var admin = await AdminAsync("U700308");

          var allowed = await coordinator.GetAsync($"/api/attendees/{attendee}/readiness");
          var refused = await admin.GetAsync($"/api/attendees/{attendee}/readiness");

          Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
          Assert.Equal(
              "NoActiveBooking", (await BodyAsync(allowed)).GetProperty("code").GetString());
          Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
      }

      private static async Task<HttpResponseMessage> ImportAsync(HttpClient client, string csv)
      {
          using var content = new MultipartFormDataContent();
          using var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
          file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
          content.Add(file, "file", "attendees.csv");
          return await client.PostAsync("/api/attendees/import", content);
      }

      private async Task<bool> ExistsAsync(Guid attendeeId)
      {
          await using var scoped = NewScope();
          return await scoped.Context.Attendees.AnyAsync(a => a.Id == attendeeId);
      }

      private async Task<bool> AnyInGroupAsync(Guid groupId)
      {
          await using var scoped = NewScope();
          return await scoped.Context.Attendees.AnyAsync(a => a.AttendeeGroupId == groupId);
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Api.Tests/Catalogue/TokenEndpointTests.cs (complete)
  using System.Net;
  using System.Net.Http.Json;
  using EventBooking.Application.Abstractions;
  using EventBooking.Domain.Events;
  using EventBooking.Domain.Invites;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;

  namespace EventBooking.Api.Tests.Catalogue;

  /// <summary>
  /// The four anonymous token routes. Two of them carry the distinction Task 21 settled: a
  /// forged token and an expired one are different answers, and everything else is one.
  /// </summary>
  [Collection("api")]
  public sealed class TokenEndpointTests(ApiFactory factory)
      : CatalogueSuite(factory)
  {
      [Fact]
      public async Task AnUnknownBookTokenIsFourOhFourTokenInvalid()
      {
          factory.SignedInAs = null;
          var client = factory.CreateClient();

          var response = await client.GetAsync($"/api/booking/{UnknownToken()}");

          Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
          Assert.Equal("token-invalid", (await BodyAsync(response)).GetProperty("type").GetString());
      }

      [Fact]
      public async Task AnUnknownManageTokenIsFourOhFourTokenInvalid()
      {
          factory.SignedInAs = null;
          var client = factory.CreateClient();

          var response = await client.GetAsync($"/api/manage/{UnknownToken()}");

          Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
          Assert.Equal("token-invalid", (await BodyAsync(response)).GetProperty("type").GetString());
      }

      /// <summary>
      /// A real link whose invite has lapsed. The token verifies, the row resolves and the
      /// version matches, so this is the one token failure the holder is told about.
      /// </summary>
      [Fact]
      public async Task AnExpiredBookTokenIsFourTenTokenExpired()
      {
          var token = await GivenExpiredInviteTokenAsync();
          factory.SignedInAs = null;
          var client = factory.CreateClient();

          var response = await client.GetAsync($"/api/booking/{token}");

          Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
          Assert.Equal("token-expired", (await BodyAsync(response)).GetProperty("type").GetString());
      }

      /// <summary>
      /// A superseded invite stays indistinguishable from a forgery: its state is not the
      /// holder's doing, and disclosing it would say something about another link.
      /// </summary>
      [Fact]
      public async Task ASupersededBookTokenStaysIndistinguishableFromAForgery()
      {
          var token = await GivenSupersededInviteTokenAsync();
          factory.SignedInAs = null;
          var client = factory.CreateClient();

          var response = await client.GetAsync($"/api/booking/{token}");

          Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
          Assert.Equal("token-invalid", (await BodyAsync(response)).GetProperty("type").GetString());
      }

      /// <summary>
      /// Design 05's worked example, asserted member by member. A confirmation against an event
      /// whose required type has no place left is the one refusal the design writes out in full.
      /// </summary>
      [Fact]
      public async Task ConfirmingAgainstAFullEventMatchesTheDesignsExampleBody()
      {
          var (token, eventId) = await GivenInviteOnAFullEventAsync();
          factory.SignedInAs = null;
          var client = factory.CreateClient();

          var response = await client.PostAsJsonAsync(
              $"/api/booking/{token}/confirm", new { eventId });

          Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
          Assert.Equal(
              "application/problem+json", response.Content.Headers.ContentType?.MediaType);
          var problem = await BodyAsync(response);
          Assert.Equal("capacity-exhausted", problem.GetProperty("type").GetString());
          Assert.Equal(409, problem.GetProperty("status").GetInt32());
          Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
          Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
          Assert.Equal(
              "capacity-exhausted",
              problem.GetProperty("errors")[0].GetProperty("code").GetString());
      }

      /// <summary>Both attendee limits apply here and nowhere else.</summary>
      [Fact]
      public async Task TheTokenRoutesCarryTheAttendeeRateLimitPolicies()
      {
          factory.SignedInAs = null;
          var client = factory.CreateClient();
          var token = UnknownToken();

          HttpResponseMessage? last = null;
          for (var attempt = 0; attempt < 31; attempt++)
          {
              last = await client.GetAsync($"/api/booking/{token}");
          }

          Assert.NotNull(last);
          Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
      }

      private static string UnknownToken() =>
          "b" + Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();

      /// <summary>
      /// An invite whose expiry has passed. The token is issued by the host's own token
      /// service, never assembled here: a token a test builds itself proves only that the test
      /// and the service agree with each other.
      /// </summary>
      /// <returns>The book token.</returns>
      private async Task<string> GivenExpiredInviteTokenAsync()
      {
          var invite = await GivenPendingInviteAsync(
              "TOK_EXP", expiresAt: Seeded.AddDays(-1), options: []);
          return Issue(invite);
      }

      /// <summary>An invite that has been replaced, which is not the holder's doing.</summary>
      /// <returns>The book token.</returns>
      private async Task<string> GivenSupersededInviteTokenAsync()
      {
          var inviteId = await GivenPendingInviteAsync(
              "TOK_SUP", expiresAt: Seeded.AddDays(7), options: []);
          await using var scoped = NewScope();
          var invite = await scoped.Context.Invites.SingleAsync(i => i.Id == inviteId);
          invite.MarkSuperseded();
          await scoped.Context.SaveChangesAsync();
          return Issue(inviteId);
      }

      /// <summary>
      /// An invite offering one event whose required type has no place left, which is the state
      /// design 05's worked example describes.
      /// </summary>
      /// <returns>The book token and the offered event.</returns>
      private async Task<(string Token, Guid EventId)> GivenInviteOnAFullEventAsync()
      {
          var type = await GivenAppointmentTypeAsync("TOK_FULL_TYPE");
          var location = await GivenLocationAsync("TOK_FULL_LOC");
          var proposal = ProposalFixture.Create(
              Guid.NewGuid(), new EventWindow(new DateOnly(2026, 11, 26), new TimeOnly(9, 30), 240),
              Guid.NewGuid(), type);
          proposal.Accept(type, Guid.NewGuid(), 1);
          var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

          await using (var seed = NewScope())
          {
              seed.Context.EventProposals.Add(proposal);
              seed.Context.Events.Add(eventItem);
              await seed.Context.SaveChangesAsync();

              // The single place is taken, so the only option this invite offers is full. The
              // capacity row is written directly because taking the place through a booking
              // would need a second attendee and a second invite to no purpose.
              await seed.Context.Database.ExecuteSqlRawAsync(
                  "UPDATE event_capacity SET remaining_capacity = 0 WHERE event_id = {0}",
                  eventItem.Id);
              await seed.Context.Database.ExecuteSqlRawAsync(
                  "UPDATE event SET location_id = {0} WHERE id = {1}", location, eventItem.Id);
          }

          var inviteId = await GivenPendingInviteAsync(
              "TOK_FULL", Seeded.AddDays(7), [eventItem.Id], type, location);
          return (Issue(inviteId), eventItem.Id);
      }

      /// <summary>Seeds one pending invite for a fresh attendee and returns its identifier.</summary>
      /// <param name="prefix">A per-case code prefix, so the suite's rows never collide.</param>
      /// <param name="expiresAt">When the invite stops being usable.</param>
      /// <param name="options">The events offered.</param>
      /// <param name="appointmentTypeId">The snapshotted requirement, or null to seed one.</param>
      /// <param name="locationId">The invite's location, or null to seed one.</param>
      /// <returns>The invite identifier.</returns>
      private async Task<Guid> GivenPendingInviteAsync(
          string prefix, DateTimeOffset expiresAt, IReadOnlyList<Guid> options,
          Guid? appointmentTypeId = null, Guid? locationId = null)
      {
          var type = appointmentTypeId ?? await GivenAppointmentTypeAsync($"{prefix}_TYPE");
          var location = locationId ?? await GivenLocationAsync($"{prefix}_LOC");
          var group = await GivenAttendeeGroupAsync($"{prefix}_GRP", type);
          var attendee = await GivenAttendeeAsync(group, $"{prefix.ToLowerInvariant()}@example.com");

          await using var scoped = NewScope();
          var invite = Invite.CreateInitial(
              Guid.NewGuid(), attendee, expiresAt, [location], options, [type], retryCount: 0);
          scoped.Context.Invites.Add(invite);
          await scoped.Context.SaveChangesAsync();
          return invite.Id;
      }

      /// <summary>Issues the link the attendee would have been emailed.</summary>
      /// <param name="inviteId">The invite.</param>
      /// <returns>The book token.</returns>
      private string Issue(Guid inviteId)
      {
          using var scope = factory.Services.CreateScope();
          return scope.ServiceProvider.GetRequiredService<ITokenService>()
              .Issue(TokenPurpose.Book, inviteId, version: 1);
      }
  }
  ```

  The expired and superseded cases both seed an invite with no options, because neither reaches
  the point of reading them: the expiry and status checks come before the option projection in
  Task 21's rewritten token branch, and an invite with options would only make the seeding
  longer without making the case stronger.

  ```csharp
  // tests/EventBooking.Api.Tests/Catalogue/LinkTests.cs (complete)
  using EventBooking.Domain.Access;

  namespace EventBooking.Api.Tests.Catalogue;

  /// <summary>
  /// Design 05: a representation carries links to the actions the caller may currently take.
  /// Two callers of different capability must therefore get different link sets from one row,
  /// which is the assertion that makes _links load-bearing rather than decorative.
  /// </summary>
  [Collection("api")]
  public sealed class LinkTests(ApiFactory factory) : CatalogueSuite(factory)
  {
      [Fact]
      public async Task AnAdminSeesTheUpdateLinkOnALocationAndACoordinatorDoesNot()
      {
          var admin = await AdminAsync("U700401");
          var created = await BodyAsync(await PostAsync(admin, "/api/locations", new
          {
              code = "LNK_LON", name = "London", address = "1 Test Street",
              timeZoneId = "Europe/London",
          }));
          var id = created.GetProperty("id").GetGuid();
          var coordinator = await CoordinatorAsync("U700402");

          var asAdmin = await LinksOfAsync(admin, id);
          var asCoordinator = await LinksOfAsync(coordinator, id);

          Assert.Contains("update", asAdmin);
          Assert.Contains("self", asAdmin);
          Assert.DoesNotContain("update", asCoordinator);
          Assert.Contains("self", asCoordinator);
      }

      [Fact]
      public async Task TheApiIndexListsEveryTopLevelResource()
      {
          factory.SignedInAs = null;

          var body = await BodyAsync(await factory.CreateClient().GetAsync("/api"));
          var links = body.GetProperty("_links").EnumerateObject().Select(x => x.Name).ToList();

          foreach (var relation in new[]
          {
              "self", "openapi", "swagger", "health", "me", "locations", "appointmentTypes",
              "attendeeGroups", "settings", "staffAccess", "eventProposals", "events",
              "attendees", "dashboards", "audit", "appointmentWorkspace",
          })
          {
              Assert.Contains(relation, links);
          }
      }

      /// <summary>Every link's href must be a route the catalogue knows.</summary>
      [Fact]
      public async Task EveryIndexLinkNamesACataloguedOperation()
      {
          factory.SignedInAs = null;

          var body = await BodyAsync(await factory.CreateClient().GetAsync("/api"));

          foreach (var link in body.GetProperty("_links").EnumerateObject())
          {
              var operationId = link.Value.GetProperty("operationId").GetString()!;
              Assert.True(
                  OpenApi.AgentOperationCatalog.All.ContainsKey(operationId), operationId);
          }
      }

      private static async Task<IReadOnlyList<string>> LinksOfAsync(HttpClient client, Guid id)
      {
          var body = await BodyAsync(await client.GetAsync($"/api/locations?includeInactive=true"));
          var row = body.GetProperty("items").EnumerateArray()
              .Single(x => x.GetProperty("id").GetGuid() == id);
          return [.. row.GetProperty("_links").EnumerateObject().Select(x => x.Name)];
      }
  }
  ```

- [ ] **Step 2: Run.** Expected: FAIL.

  Every suite fails: the catalogue test because the ported catalogue carries the predecessor's
  routes, the endpoint suites because most of their routes return 404, and the link suite
  because the ported index lists the predecessor's relations.

  ```bash
  dotnet test tests/EventBooking.Api.Tests --filter "FullyQualifiedName~Catalogue"
  ```

- [ ] **Step 3: Implement.** The catalogue first — every other file reads from it.

  ```csharp
  // src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs — the operations list, replacing the
  // ported one. AgentHints, AgentOperation and the constructor's duplicate checks are unchanged
  // except for the Capability member; the four hint factories keep their names.
  static AgentOperationCatalog()
  {
      var operations = new List<AgentOperation>
      {
          // Discovery, health and the document itself. None is a business capability, so none
          // has a tool: an agent discovers this surface through tools/list, not through /api.
          Excluded("getApiIndex", HttpMethods.Get, "/api", "Discovery",
              "Read the API entry document.",
              "Anonymous entry document listing every top-level resource.",
              "Transport discovery, not a business capability. MCP has tools/list."),
          Excluded("getOpenApiDocument", HttpMethods.Get, "/openapi/v1.json", "Discovery",
              "Read the OpenAPI document.", "Anonymous machine-readable API contract.",
              "Transport discovery, not a business capability."),
          Excluded("getSwaggerUi", HttpMethods.Get, "/swagger", "Discovery",
              "Open the Swagger UI.", "Anonymous human documentation UI.",
              "Human documentation UI, not a business capability."),
          Excluded("getLiveness", HttpMethods.Get, "/health/live", "Health",
              "Read the liveness probe.", "Anonymous deployment liveness probe.",
              "Deployment probe, not a staff workflow."),
          Excluded("getReadiness", HttpMethods.Get, "/health/ready", "Health",
              "Read the readiness probe.",
              "Anonymous readiness probe; reports whether the database is reachable.",
              "Deployment probe, not a staff workflow."),
          Excluded("getMetrics", HttpMethods.Get, "/metrics", "Health",
              "Scrape the metrics endpoint.",
              "Anonymous Prometheus exposition of the API's meters (design 08).",
              "Operational scrape endpoint, not a staff workflow."),

          // Identity. A staff token with no valid staff number still reaches this one, which is
          // the whole of contradiction #3's exception.
          Staff("getMyAccess", HttpMethods.Get, "/api/me", "Identity",
              "Read the signed-in staff access.",
              "Reads display name, staff number, roles, scope and granted capabilities (FR-10.8). " +
              "Requires a staff bearer token; no capability, and answers even when the staff " +
              "number claim is missing or malformed.",
              null, "get_my_access", Read()),

          // Reference data. Reads are open to any staff member; writes are Admin-only.
          Staff("listLocations", HttpMethods.Get, "/api/locations", "Reference Data",
              "List locations.",
              "Reads every location in code order. Requires a staff bearer token.",
              null, "list_locations", Read()),
          Staff("createLocation", HttpMethods.Post, "/api/locations", "Reference Data",
              "Create a location.",
              "Creates a location with its code, name, address and IANA zone.",
              nameof(StaffCapability.ManageReferenceData), "create_location", Create()),
          Staff("updateLocation", HttpMethods.Put, "/api/locations/{id}", "Reference Data",
              "Update a location.",
              "Updates a location's name, address, zone and active flag. Refuses a zone change " +
              "or a deactivation while the location has open proposals or future events.",
              nameof(StaffCapability.ManageReferenceData), "update_location", Transition()),
          Staff("listAppointmentTypes", HttpMethods.Get, "/api/appointment-types", "Reference Data",
              "List appointment types.",
              "Reads every appointment type with its current Manager's display name.",
              null, "list_appointment_types", Read()),
          Staff("createAppointmentType", HttpMethods.Post, "/api/appointment-types", "Reference Data",
              "Create an appointment type.", "Creates an appointment type with its code and name.",
              nameof(StaffCapability.ManageReferenceData), "create_appointment_type", Create()),
          Staff("updateAppointmentType", HttpMethods.Put, "/api/appointment-types/{id}",
              "Reference Data", "Update an appointment type.",
              "Updates an appointment type's name and active flag. Refuses a deactivation while " +
              "it is listed on an open proposal or a future event, or mapped by an active group.",
              nameof(StaffCapability.ManageReferenceData), "update_appointment_type", Transition()),
          Staff("listAttendeeGroups", HttpMethods.Get, "/api/attendee-groups", "Reference Data",
              "List attendee groups.",
              "Reads every attendee group with its requirement type ids and member count.",
              null, "list_attendee_groups", Read()),
          Staff("createAttendeeGroup", HttpMethods.Post, "/api/attendee-groups", "Reference Data",
              "Create an attendee group.",
              "Creates an attendee group mapping at least one active appointment type.",
              nameof(StaffCapability.ManageReferenceData), "create_attendee_group", Create()),
          Staff("updateAttendeeGroup", HttpMethods.Put, "/api/attendee-groups/{id}",
              "Reference Data", "Update an attendee group.",
              "Updates a group's name, requirement mapping and active flag. A mapping change " +
              "re-derives every member's requirements and is refused while any member holds an " +
              "active original booking (FR-1.5).",
              nameof(StaffCapability.ManageReferenceData), "update_attendee_group", Transition()),
          Staff("getSettings", HttpMethods.Get, "/api/settings", "Settings",
              "Read the system settings.", "Reads the single settings row and its version.",
              nameof(StaffCapability.ManageSettings), "get_settings", Read()),
          Staff("updateSettings", HttpMethods.Put, "/api/settings", "Settings",
              "Update the system settings.",
              "Updates invite expiry, automatic retry count and option count. Existing invites " +
              "keep the values they were issued under.",
              nameof(StaffCapability.ManageSettings), "update_settings", Transition()),

          // Staff access. There is deliberately no create, delete or role edit (FR-10.5).
          Staff("listStaffAccess", HttpMethods.Get, "/api/staff-access", "Staff Access",
              "List staff access profiles.",
              "Reads every profile with its display name, staff number, read-only roles and scope.",
              nameof(StaffCapability.ManageStaffAccess), "list_staff_access", Read()),
          Staff("setStaffAccessScope", HttpMethods.Put, "/api/staff-access/{staffUserId}/scope",
              "Staff Access", "Set a staff access scope.",
              "Sets or clears one profile's appointment-type scope. Assigning a type that " +
              "another Manager holds displaces them, and the response names who.",
              nameof(StaffCapability.ManageStaffAccess), "set_staff_access_scope", Transition()),

          // Negotiation.
          Staff("listEventProposals", HttpMethods.Get, "/api/event-proposals", "Negotiation",
              "List the caller's type's proposals.",
              "Reads the proposals listing the caller's own appointment type (FR-2.13), " +
              "filtered by status and location, one keyset page at a time.",
              nameof(StaffCapability.ManageEventNegotiation), "list_event_proposals", Read()),
          Staff("proposeEvent", HttpMethods.Post, "/api/event-proposals", "Negotiation",
              "Propose an event.",
              "Creates a proposal for one location, window and listed appointment-type set. " +
              "A proposal listing only the proposer's type is confirmed on creation.",
              nameof(StaffCapability.ManageEventNegotiation), "propose_event", Create()),
          Staff("recordAcceptance", HttpMethods.Put, "/api/event-proposals/{id}/acceptance",
              "Negotiation", "Record or revise an acceptance.",
              "Records the caller's type's acceptance with its headcount, or revises it while " +
              "the proposal is open. The last missing acceptance confirms the event.",
              nameof(StaffCapability.ManageEventNegotiation), "record_acceptance", Transition()),
          Staff("withdrawAcceptance", HttpMethods.Delete, "/api/event-proposals/{id}/acceptance",
              "Negotiation", "Withdraw an acceptance.",
              "Withdraws the caller's type's acceptance while the proposal is open.",
              nameof(StaffCapability.ManageEventNegotiation), "withdraw_acceptance", Delete()),
          Staff("withdrawProposal", HttpMethods.Post, "/api/event-proposals/{id}/withdraw",
              "Negotiation", "Withdraw a proposal.",
              "Withdraws the whole proposal. Judged against the proposing appointment type, so " +
              "a Manager inherits it from a predecessor.",
              nameof(StaffCapability.ManageEventNegotiation), "withdraw_proposal", Delete()),

          // Events. The two reads carry no capability: design 05 gives them two, and the
          // handler filters by whichever the caller holds (settlement #12).
          Staff("listEvents", HttpMethods.Get, "/api/events", "Events", "List events.",
              "Reads events filtered by location, date range and appointment type. A Manager " +
              "sees their own type's capacity; an Admin or Coordinator sees every type.",
              null, "list_events", Read()),
          Staff("getEvent", HttpMethods.Get, "/api/events/{id}", "Events", "Read one event.",
              "Reads one event with its capacities, filtered the same way the list is. An event " +
              "outside the caller's scope reads as not found.",
              null, "get_event", Read()),
          Staff("adjustEventCapacity", HttpMethods.Put,
              "/api/events/{id}/capacities/{appointmentTypeId}", "Events",
              "Adjust an event capacity.",
              "Replaces the total headcount for one appointment type on an active event. The " +
              "type must be the caller's own, and the total must still cover every active booking.",
              nameof(StaffCapability.ManageEventNegotiation), "adjust_event_capacity", Transition()),
          Staff("cancelEvent", HttpMethods.Post, "/api/events/{id}/cancel", "Events",
              "Cancel an event.",
              "Cancels an event, voiding its bookings and re-inviting the affected attendees. " +
              "Two-step: without confirm=true the call reports its consequence and changes nothing.",
              nameof(StaffCapability.CancelEvent), "cancel_event", Delete()),
          Staff("listCancellableEvents", HttpMethods.Get, "/api/events/cancellable", "Events",
              "List cancellable events.",
              "Reads the active events whose window has not started, which are the ones a " +
              "cancellation can still reach (FR-7.2).",
              nameof(StaffCapability.ViewEventOperations), "list_cancellable_events", Read()),

          // Attendees.
          Staff("listAttendees", HttpMethods.Get, "/api/attendees", "Attendees", "List attendees.",
              "Reads attendees filtered by status, group, readiness and a name or email prefix, " +
              "one keyset page at a time, with each row's latest delivery status.",
              nameof(StaffCapability.ManageAttendees), "list_attendees", Read()),
          Staff("createAttendee", HttpMethods.Post, "/api/attendees", "Attendees",
              "Create an attendee.",
              "Creates an attendee in a group, deriving their requirements from it.",
              nameof(StaffCapability.ManageAttendees), "create_attendee", Create()),
          Staff("updateAttendee", HttpMethods.Put, "/api/attendees/{id}", "Attendees",
              "Update an attendee.",
              "Updates an attendee's name, email and group (FR-4.2). A group change that would " +
              "alter an active booking's requirements is refused.",
              nameof(StaffCapability.ManageAttendees), "update_attendee", Transition()),
          Staff("deleteAttendee", HttpMethods.Delete, "/api/attendees/{id}", "Attendees",
              "Delete an attendee.",
              "Deletes an attendee and their bookings (FR-4.5). Two-step: without confirm=true " +
              "the call reports its consequence and changes nothing.",
              nameof(StaffCapability.ManageAttendees), "delete_attendee", Delete()),
          Staff("importAttendees", HttpMethods.Post, "/api/attendees/import", "Attendees",
              "Import attendees from CSV.",
              "Imports a multipart CSV file, all or nothing (FR-4.3). At most 1000 rows and 1 MB.",
              nameof(StaffCapability.ManageAttendees), "import_attendees", Create()),
          Staff("countEligibleEvents", HttpMethods.Get,
              "/api/attendees/{id}/eligible-event-count", "Attendees",
              "Count an attendee's eligible events.",
              "Counts the events this attendee could currently be offered at the given locations.",
              nameof(StaffCapability.ManageAttendees), "count_eligible_events", Read()),
          Staff("inviteAttendee", HttpMethods.Post, "/api/attendees/{id}/invites", "Attendees",
              "Invite an attendee.",
              "Issues an invitation restricted to the chosen locations, returning Invited or " +
              "AwaitingAvailability when too few events are eligible.",
              nameof(StaffCapability.ManageAttendees), "invite_attendee", Create()),
          Staff("startRecoveryInvite", HttpMethods.Post,
              "/api/attendees/{id}/recovery-invites", "Attendees", "Start a recovery invite.",
              "Starts missed-appointment recovery for the attendee's outstanding no-show types " +
              "(FR-9.1), optionally widening the locations.",
              nameof(StaffCapability.ManageAttendees), "start_recovery_invite", Create()),
          Staff("cancelRecoveryInvite", HttpMethods.Delete,
              "/api/attendees/{id}/recovery-invites/{inviteId}", "Attendees",
              "Cancel a recovery invite.", "Cancels one pending recovery invitation.",
              nameof(StaffCapability.ManageAttendees), "cancel_recovery_invite", Delete()),
          Staff("listAttendeeBookings", HttpMethods.Get, "/api/attendees/{id}/bookings",
              "Attendees", "List an attendee's bookings.",
              "Reads the attendee's bookings with their event and location.",
              nameof(StaffCapability.ManageAttendees), "list_attendee_bookings", Read()),
          Staff("cancelAttendeeBooking", HttpMethods.Post,
              "/api/attendees/{id}/bookings/{bookingId}/cancel", "Attendees",
              "Cancel an attendee's booking.",
              "Cancels one booking on the attendee's behalf (FR-7.1). Two-step: without " +
              "confirm=true the call reports its consequence and changes nothing.",
              nameof(StaffCapability.ManageAttendees), "cancel_attendee_booking", Delete()),
          Staff("retryAttendeeEmail", HttpMethods.Post, "/api/attendees/{id}/email-retry",
              "Attendees", "Retry an attendee email.",
              "Regenerates and re-queues the newest failed or stale delivery (FR-11.3).",
              nameof(StaffCapability.ManageAttendees), "retry_attendee_email", Create()),
          Staff("getAttendeeReadiness", HttpMethods.Get, "/api/attendees/{id}/readiness",
              "Attendees", "Read an attendee's readiness.",
              "Reads the calculated readiness and any outstanding appointment types.",
              nameof(StaffCapability.ViewAttendeeDashboards), "get_attendee_readiness", Read()),

          // Dashboards and audit. The search carries no capability for the same reason the
          // event reads do not: Task 20b's handler filters by the buckets the caller holds.
          Staff("getDashboards", HttpMethods.Get, "/api/dashboards", "Dashboards",
              "Read the dashboards.",
              "Reads the three tabs and their counts (FR-13), optionally filtered by location.",
              nameof(StaffCapability.ViewAttendeeDashboards), "get_dashboards", Read()),
          Staff("searchAudit", HttpMethods.Get, "/api/audit", "Audit", "Search the audit log.",
              "Searches audit entries, scoped to the buckets the caller's capabilities reach " +
              "(FR-12.3), one keyset page at a time.",
              null, "search_audit", Read()),
          Staff("getAttendeeAuditHistory", HttpMethods.Get, "/api/audit/attendees/{id}", "Audit",
              "Read an attendee's history.", "Reads the audit history of one attendee.",
              nameof(StaffCapability.ViewAttendeeAudit), "get_attendee_audit_history", Read()),
          Staff("getEventAuditHistory", HttpMethods.Get, "/api/audit/events/{id}", "Audit",
              "Read an event's history.",
              "Reads the audit history of one event, including its proposal.",
              nameof(StaffCapability.ViewEventAudit), "get_event_audit_history", Read()),

          // Appointment workspace.
          Staff("listWorkspaceEvents", HttpMethods.Get, "/api/appointment-workspace/events",
              "Appointment Workspace", "List workspace events.",
              "Reads the events listing the caller's type and ending between seven days ago and " +
              "fourteen days ahead.",
              nameof(StaffCapability.ConductAppointments), "list_workspace_events", Read()),
          Staff("getWorkspaceRoster", HttpMethods.Get,
              "/api/appointment-workspace/events/{eventId}", "Appointment Workspace",
              "Read a workspace roster.",
              "Reads the minimum-data roster for one event (FR-8.1): name, email, the caller's " +
              "own type's appointment status, and nothing else.",
              nameof(StaffCapability.ConductAppointments), "get_workspace_roster", Read()),
          Staff("setAppointmentStatus", HttpMethods.Put,
              "/api/appointment-workspace/appointments/{id}/status", "Appointment Workspace",
              "Set an appointment status.",
              "Moves one booking appointment to a target status, or applies a bounded correction. " +
              "A same-status submission is idempotent.",
              nameof(StaffCapability.ConductAppointments), "set_appointment_status", Transition()),
          Staff("exportWorkspaceRoster", HttpMethods.Get,
              "/api/appointment-workspace/events/{eventId}/roster.csv", "Appointment Workspace",
              "Export a workspace roster.",
              "Downloads the roster as CSV (FR-8.7, FR-8.8), with formula characters neutralised.",
              nameof(StaffCapability.ConductAppointments), "export_workspace_roster", Read()),

          // Attendee token routes. Anonymous, rate-limited and REST-only by the approved remote
          // MCP design: an agent holding a staff token must not be able to act as an attendee.
          Excluded("viewInvite", HttpMethods.Get, "/api/booking/{token}", "Attendee Booking",
              "View an invitation.",
              "Anonymous. Shows the live options, topped up (FR-5.8), or the expired state.",
              "Anonymous attendee token flow; excluded by the approved remote MCP design."),
          Excluded("confirmBooking", HttpMethods.Post, "/api/booking/{token}/confirm",
              "Attendee Booking", "Confirm a booking.",
              "Anonymous. Confirms one offered event and returns the booking and its manage link.",
              "Anonymous attendee token flow; excluded by the approved remote MCP design."),
          Excluded("viewManagedBooking", HttpMethods.Get, "/api/manage/{token}",
              "Attendee Booking", "View a booking.",
              "Anonymous. Shows the booking and whether it can still be cancelled.",
              "Anonymous attendee token flow; excluded by the approved remote MCP design."),
          Excluded("cancelManagedBooking", HttpMethods.Post, "/api/manage/{token}/cancel",
              "Attendee Booking", "Cancel a booking.",
              "Anonymous. Cancels the booking and optionally asks for a new time.",
              "Anonymous attendee token flow; excluded by the approved remote MCP design."),
      };

      // The ported validation is unchanged: exactly one of McpTool or ExclusionReason, no
      // duplicate id, no duplicate method-and-route, no duplicate tool.
      var byId = new Dictionary<string, AgentOperation>(StringComparer.Ordinal);
      var byRoute = new HashSet<string>(StringComparer.Ordinal);
      var byTool = new HashSet<string>(StringComparer.Ordinal);
      foreach (var operation in operations)
      {
          if ((operation.McpTool is null) == (operation.ExclusionReason is null))
          {
              throw new InvalidOperationException(
                  $"Operation {operation.OperationId} must set exactly one of McpTool or ExclusionReason.");
          }

          if (!byId.TryAdd(operation.OperationId, operation))
          {
              throw new InvalidOperationException($"Duplicate operation id {operation.OperationId}.");
          }

          if (!byRoute.Add($"{operation.Method} {operation.Route}"))
          {
              throw new InvalidOperationException(
                  $"Duplicate method and route {operation.Method} {operation.Route}.");
          }

          if (operation.McpTool is not null && !byTool.Add(operation.McpTool))
          {
              throw new InvalidOperationException($"Duplicate MCP tool {operation.McpTool}.");
          }
      }

      All = byId;
  }

  private static AgentOperation Staff(
      string operationId, string method, string route, string tag, string summary,
      string description, string? capability, string mcpTool, AgentHints hints) =>
      new(operationId, method, route, tag, summary, description, true, capability, mcpTool, null, hints);

  private static AgentOperation Excluded(
      string operationId, string method, string route, string tag, string summary,
      string description, string reason) =>
      new(operationId, method, route, tag, summary, description, false, null, null, reason,
          new AgentHints(true, false, true, false));
  ```

  The capability is written as `nameof(StaffCapability.…)` rather than a literal, so a capability
  renamed in the enum fails the build here instead of quietly publishing a name no authorizer
  answers to. The two reads that carry `null` are the ones settlement #12 and Task 20b govern;
  `getMyAccess` and the three reference-data lists carry `null` because design 05 asks for a
  bearer token and no capability, which the bearer flag already records.

  ```csharp
  // src/EventBooking.Api/OpenApi/OpenApiConfiguration.cs — inside the operation transformer,
  // beside the existing x-mcp-tool and x-agent-hints extensions. Everything else is unchanged.
  operation.Summary = entry.Summary;
  operation.Description = entry.Description;
  operation.Tags = [new OpenApiTagReference(entry.Tag)];

  if (entry.Capability is not null)
  {
      operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
      operation.Extensions["x-capability"] =
          new JsonNodeExtension(JsonValue.Create(entry.Capability)!);
  }
  ```

  Publishing the capability in the document is what lets the catalogue test compare design 05's
  third column against what a caller actually receives, rather than against a constant only the
  server can see.

  **The response contracts and the capability set behind `_links`.**

  ```csharp
  // src/EventBooking.Api/Contracts/CallerCapabilities.cs (complete)
  using EventBooking.Api.Auth;
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;

  namespace EventBooking.Api.Contracts;

  /// <summary>
  /// The capability names the caller currently holds, resolved once per request. This is
  /// presentation, not authorization: it decides which affordances a representation advertises,
  /// and every handler still makes its own decision. A caller who ignores `_links` and posts
  /// anyway is refused by the handler exactly as before.
  /// </summary>
  /// <param name="caller">The signed-in staff identity.</param>
  /// <param name="profiles">The access profiles.</param>
  public sealed class CallerCapabilities(
      ICallerAccessor caller, IStaffAccessProfileRepository profiles)
  {
      private IReadOnlySet<string>? _resolved;

      /// <summary>Returns the capability names the caller holds, or an empty set.</summary>
      /// <param name="ct">The cancellation token.</param>
      /// <returns>The capability names.</returns>
      public async Task<IReadOnlySet<string>> GetAsync(CancellationToken ct)
      {
          if (_resolved is not null)
          {
              return _resolved;
          }

          if (caller.StaffUserId is not { } staffUserId)
          {
              return _resolved = new HashSet<string>(StringComparer.Ordinal);
          }

          var profile = await profiles.GetAsync(staffUserId, ct);
          if (profile is null || !profile.IsValid())
          {
              return _resolved = new HashSet<string>(StringComparer.Ordinal);
          }

          var held = new HashSet<string>(StringComparer.Ordinal);
          foreach (var capability in Enum.GetValues<StaffCapability>())
          {
              if (StaffAccessAuthorizer.IsAllowed(profile, capability))
              {
                  held.Add(capability.ToString());
              }
          }

          return _resolved = held;
      }
  }
  ```

  `StaffAccessAuthorizer.IsAllowed` is the ported private predicate, made internal and exposed
  through an internal-visible-to shim for the API project — the same table the authorizer itself
  answers from, so a link can never advertise a capability the handler would refuse. Duplicating
  the matrix here instead is how the two would drift.

  ```csharp
  // src/EventBooking.Api/Contracts/ApiResponses.cs (complete — the projections; the records
  // themselves are in the Interfaces section above)
  using EventBooking.Api.OpenApi;
  using EventBooking.Application.Events;
  using EventBooking.Application.ReferenceData;
  using EventBooking.Application.Settings;
  using EventBooking.Domain.Time;

  namespace EventBooking.Api.Contracts;

  /// <summary>Projects application results onto design 05's response bodies.</summary>
  public static class ApiResponses
  {
      /// <summary>Projects one location, with the links its caller may follow.</summary>
      /// <param name="result">The application result.</param>
      /// <param name="capabilities">The caller's capabilities.</param>
      /// <returns>The response body.</returns>
      public static LocationResponse Location(
          LocationResult result, IReadOnlySet<string> capabilities)
      {
          ArgumentNullException.ThrowIfNull(result);
          return new LocationResponse(
              result.Id, result.Code, result.Name, result.Address, result.TimeZoneId,
              result.IsActive, result.Version,
              CallerLinks.For(
                  capabilities,
                  new LinkCandidate("self", "listLocations", "/api/locations", null),
                  new LinkCandidate(
                      "update", "updateLocation", $"/api/locations/{result.Id}",
                      nameof(StaffCapability.ManageReferenceData))));
      }

      /// <summary>Projects one appointment type.</summary>
      /// <param name="result">The application result.</param>
      /// <param name="capabilities">The caller's capabilities.</param>
      /// <returns>The response body.</returns>
      public static AppointmentTypeResponse AppointmentType(
          AppointmentTypeResult result, IReadOnlySet<string> capabilities)
      {
          ArgumentNullException.ThrowIfNull(result);
          return new AppointmentTypeResponse(
              result.Id, result.Code, result.Name, result.IsActive, result.Version,
              result.ManagerDisplayName,
              CallerLinks.For(
                  capabilities,
                  new LinkCandidate("self", "listAppointmentTypes", "/api/appointment-types", null),
                  new LinkCandidate(
                      "update", "updateAppointmentType", $"/api/appointment-types/{result.Id}",
                      nameof(StaffCapability.ManageReferenceData))));
      }

      /// <summary>Projects one attendee group.</summary>
      /// <param name="result">The application result.</param>
      /// <param name="capabilities">The caller's capabilities.</param>
      /// <returns>The response body.</returns>
      public static AttendeeGroupResponse AttendeeGroup(
          AttendeeGroupResult result, IReadOnlySet<string> capabilities)
      {
          ArgumentNullException.ThrowIfNull(result);
          return new AttendeeGroupResponse(
              result.Id, result.Code, result.Name, result.IsActive, result.Version,
              result.RequirementTypeIds, result.MemberCount,
              CallerLinks.For(
                  capabilities,
                  new LinkCandidate("self", "listAttendeeGroups", "/api/attendee-groups", null),
                  new LinkCandidate(
                      "update", "updateAttendeeGroup", $"/api/attendee-groups/{result.Id}",
                      nameof(StaffCapability.ManageReferenceData))));
      }

      /// <summary>Projects the settings row.</summary>
      /// <param name="result">The application result.</param>
      /// <param name="capabilities">The caller's capabilities.</param>
      /// <returns>The response body.</returns>
      public static SettingsResponse Settings(
          SystemSettingsResult result, IReadOnlySet<string> capabilities)
      {
          ArgumentNullException.ThrowIfNull(result);
          return new SettingsResponse(
              result.InviteExpiryDays, result.MaxAutoRetryCount, result.InviteOptionCount,
              result.Version,
              CallerLinks.For(
                  capabilities,
                  new LinkCandidate("self", "getSettings", "/api/settings", null),
                  new LinkCandidate(
                      "update", "updateSettings", "/api/settings",
                      nameof(StaffCapability.ManageSettings))));
      }

      /// <summary>Projects one event, deriving its time in the location's own zone.</summary>
      /// <param name="view">The read model.</param>
      /// <param name="zones">The zone resolver.</param>
      /// <param name="capabilities">The caller's capabilities.</param>
      /// <returns>The response body.</returns>
      public static EventResponse Event(
          EventView view, IEventWindowZones zones, IReadOnlySet<string> capabilities)
      {
          ArgumentNullException.ThrowIfNull(view);
          return new EventResponse(
              view.EventId, view.ProposalId, view.LocationId, view.LocationCode, view.LocationName,
              EventTimeResponse.From(
                  view.Date, view.StartTime, view.DurationMinutes, view.TimeZoneId, zones),
              view.Status,
              [.. view.Capacities.Select(c => new EventCapacityResponse(
                  c.AppointmentTypeId, c.Code, c.Name, c.TotalHeadcount, c.RemainingCapacity))],
              view.ActiveBookings,
              CallerLinks.For(
                  capabilities,
                  new LinkCandidate("self", "getEvent", $"/api/events/{view.EventId}", null),
                  new LinkCandidate(
                      "cancel", "cancelEvent", $"/api/events/{view.EventId}/cancel",
                      nameof(StaffCapability.CancelEvent)),
                  new LinkCandidate(
                      "roster", "getWorkspaceRoster",
                      $"/api/appointment-workspace/events/{view.EventId}",
                      nameof(StaffCapability.ConductAppointments))));
      }

      /// <summary>Projects one proposal.</summary>
      /// <param name="item">The read model.</param>
      /// <param name="zones">The zone resolver.</param>
      /// <param name="capabilities">The caller's capabilities.</param>
      /// <returns>The response body.</returns>
      public static EventProposalResponse EventProposal(
          EventProposalListItem item, IEventWindowZones zones, IReadOnlySet<string> capabilities)
      {
          ArgumentNullException.ThrowIfNull(item);
          return new EventProposalResponse(
              item.ProposalId, item.LocationId, item.LocationCode, item.LocationName,
              EventTimeResponse.From(
                  item.Date, item.StartTime, item.DurationMinutes, item.TimeZoneId, zones),
              item.Status, item.ListedTypeCount, item.AcceptedTypeCount, item.MyAcceptedHeadcount,
              item.AcceptedByMe, item.CreatedByMe,
              CallerLinks.For(
                  capabilities,
                  new LinkCandidate("self", "listEventProposals", "/api/event-proposals", null),
                  new LinkCandidate(
                      "accept", "recordAcceptance",
                      $"/api/event-proposals/{item.ProposalId}/acceptance",
                      nameof(StaffCapability.ManageEventNegotiation)),
                  new LinkCandidate(
                      "withdraw", "withdrawProposal",
                      $"/api/event-proposals/{item.ProposalId}/withdraw",
                      nameof(StaffCapability.ManageEventNegotiation))));
      }
  }
  ```

  **Discovery and identity.**

  ```csharp
  // src/EventBooking.Api/Endpoints/ApiDiscoveryEndpoints.cs (complete, replacing the ported file)
  using EventBooking.Api.Contracts;
  using EventBooking.Api.OpenApi;

  namespace EventBooking.Api.Endpoints;

  /// <summary>Maps the anonymous API discovery root.</summary>
  public static class ApiDiscoveryEndpoints
  {
      /// <summary>Maps the anonymous entry document listing every top-level resource.</summary>
      /// <param name="app">The endpoint route builder.</param>
      /// <returns>The endpoint route builder.</returns>
      public static IEndpointRouteBuilder MapApiDiscoveryEndpoints(this IEndpointRouteBuilder app)
      {
          ArgumentNullException.ThrowIfNull(app);

          app.MapGet("/api", () =>
          {
              // Unconditional: the index is anonymous, so it advertises the resources rather
              // than the caller's permissions. Each resource then carries its own _links, which
              // is where capability actually shows.
              var links = new Dictionary<string, ApiLink>(StringComparer.Ordinal)
              {
                  ["self"] = Link("getApiIndex"),
                  ["openapi"] = Link("getOpenApiDocument"),
                  ["swagger"] = Link("getSwaggerUi"),
                  ["health"] = Link("getLiveness"),
                  ["me"] = Link("getMyAccess"),
                  ["locations"] = Link("listLocations"),
                  ["appointmentTypes"] = Link("listAppointmentTypes"),
                  ["attendeeGroups"] = Link("listAttendeeGroups"),
                  ["settings"] = Link("getSettings"),
                  ["staffAccess"] = Link("listStaffAccess"),
                  ["eventProposals"] = Link("listEventProposals"),
                  ["events"] = Link("listEvents"),
                  ["attendees"] = Link("listAttendees"),
                  ["dashboards"] = Link("getDashboards"),
                  ["audit"] = Link("searchAudit"),
                  ["appointmentWorkspace"] = Link("listWorkspaceEvents"),
              };
              return Results.Ok(new ApiDiscoveryResponse("EventBooking API", "v1", links));
          })
              .AllowAnonymous()
              .WithAgentMetadata("getApiIndex")
              .Produces<ApiDiscoveryResponse>(200);

          return app;
      }

      private static ApiLink Link(string operationId)
      {
          var operation = AgentOperationCatalog.Get(operationId);
          return new ApiLink(operation.Route, operation.Method, operation.OperationId);
      }
  }
  ```

  ```csharp
  // src/EventBooking.Api/Endpoints/MeEndpoints.cs (complete)
  using EventBooking.Api.Auth;
  using EventBooking.Api.OpenApi;
  using EventBooking.Application.Access;
  using Microsoft.Extensions.Options;

  namespace EventBooking.Api.Endpoints;

  /// <summary>Maps the signed-in staff member's own view of their access.</summary>
  public static class MeEndpoints
  {
      /// <summary>Maps <c>GET /api/me</c>.</summary>
      /// <param name="app">The endpoint route builder.</param>
      /// <returns>The endpoint route builder.</returns>
      public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
      {
          ArgumentNullException.ThrowIfNull(app);

          app.MapGet("/api/me", async (
              ICallerAccessor caller,
              MeHandler handler,
              IOptions<AuthClaimOptions> claims,
              CancellationToken cancellationToken) =>
          {
              // The one route a token with no usable staff number still reaches. It is answered
              // from the claims rather than refused, so the caller is told what is wrong with
              // their token instead of being shown a bare 403 everywhere (contradiction #3).
              var result = await handler.HandleAsync(
                  caller.RequireStaffUserId(),
                  caller.StaffId?.Value,
                  caller.DisplayName,
                  caller.Roles,
                  claims.Value.StaffIdPattern,
                  cancellationToken);
              return result.ToResponse();
          })
              .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
              .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName)
              .WithAgentMetadata("getMyAccess")
              .Produces<StaffMeView>(200)
              .ProducesProblem(401);

          return app;
      }
  }
  ```

  **Reference data, settings and staff access.**

  ```csharp
  // src/EventBooking.Api/Endpoints/LocationEndpoints.cs (complete)
  using EventBooking.Api.Auth;
  using EventBooking.Api.Contracts;
  using EventBooking.Api.OpenApi;
  using EventBooking.Application.ReferenceData;

  namespace EventBooking.Api.Endpoints;

  /// <summary>Maps the three location routes.</summary>
  public static class LocationEndpoints
  {
      /// <summary>The creation body design 05 names.</summary>
      /// <param name="Code">The canonical code.</param>
      /// <param name="Name">The display name.</param>
      /// <param name="Address">The postal address.</param>
      /// <param name="TimeZoneId">The IANA zone.</param>
      public sealed record CreateLocationRequest(
          string? Code, string? Name, string? Address, string? TimeZoneId);

      /// <summary>The update body design 05 names.</summary>
      /// <param name="Name">The display name.</param>
      /// <param name="Address">The postal address.</param>
      /// <param name="TimeZoneId">The IANA zone.</param>
      /// <param name="IsActive">Whether the location stays in use.</param>
      /// <param name="ExpectedVersion">The version the caller read.</param>
      public sealed record UpdateLocationRequest(
          string? Name, string? Address, string? TimeZoneId, bool IsActive, long ExpectedVersion);

      /// <summary>Maps the location routes.</summary>
      /// <param name="app">The endpoint route builder.</param>
      /// <returns>The endpoint route builder.</returns>
      public static IEndpointRouteBuilder MapLocationEndpoints(this IEndpointRouteBuilder app)
      {
          ArgumentNullException.ThrowIfNull(app);
          var group = app.MapGroup("/api/locations")
              .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
              .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName);

          group.MapGet("/", async (
              bool? includeInactive,
              ListLocationsHandler handler,
              CallerCapabilities capabilities,
              CancellationToken cancellationToken) =>
          {
              var result = await handler.HandleAsync(
                  new ListLocationsQuery(includeInactive ?? false), cancellationToken);
              if (result.IsFailure)
              {
                  return result.ToResponse();
              }

              var held = await capabilities.GetAsync(cancellationToken);
              return Results.Ok(new Page<LocationListResponse>(
                  [.. result.Value.Select(x => LocationListResponse.From(x, held))], null));
          })
              .WithAgentMetadata("listLocations")
              .Produces<Page<LocationListResponse>>(200)
              .ProducesProblem(401)
              .ProducesProblem(403);

          group.MapPost("/", async (
              CreateLocationRequest request,
              ICallerAccessor caller,
              CreateLocationHandler handler,
              CallerCapabilities capabilities,
              CancellationToken cancellationToken) =>
          {
              var result = await handler.HandleAsync(
                  new CreateLocationCommand(
                      caller.RequireStaffUserId(), request.Code, request.Name, request.Address,
                      request.TimeZoneId),
                  cancellationToken);
              if (result.IsFailure)
              {
                  return result.ToResponse();
              }

              var held = await capabilities.GetAsync(cancellationToken);
              return Results.Created(
                  $"/api/locations/{result.Value.Id}",
                  ApiResponses.Location(result.Value, held));
          })
              .WithAgentMetadata("createLocation")
              .Produces<LocationResponse>(201)
              .ProducesProblem(403)
              .ProducesProblem(409)
              .ProducesProblem(422);

          group.MapPut("/{id:guid}", async (
              Guid id,
              UpdateLocationRequest request,
              ICallerAccessor caller,
              UpdateLocationHandler handler,
              CallerCapabilities capabilities,
              CancellationToken cancellationToken) =>
          {
              var result = await handler.HandleAsync(
                  new UpdateLocationCommand(
                      caller.RequireStaffUserId(), id, request.Name, request.Address,
                      request.TimeZoneId, request.IsActive, request.ExpectedVersion),
                  cancellationToken);
              if (result.IsFailure)
              {
                  return result.ToResponse();
              }

              var held = await capabilities.GetAsync(cancellationToken);
              return Results.Ok(ApiResponses.Location(result.Value, held));
          })
              .WithAgentMetadata("updateLocation")
              .Produces<LocationResponse>(200)
              .ProducesProblem(403)
              .ProducesProblem(404)
              .ProducesProblem(409)
              .ProducesProblem(422);

          return app;
      }
  }

  /// <summary>One row of the location list, with the links its caller may follow.</summary>
  /// <param name="Id">The identifier.</param>
  /// <param name="Code">The canonical code.</param>
  /// <param name="Name">The display name.</param>
  /// <param name="IsActive">Whether the location is in use.</param>
  /// <param name="Links">The affordances the caller holds.</param>
  public sealed record LocationListResponse(
      Guid Id, string Code, string Name, bool IsActive,
      [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
      IReadOnlyDictionary<string, ApiLink> Links)
  {
      /// <summary>Projects one list row.</summary>
      /// <param name="item">The application row.</param>
      /// <param name="capabilities">The caller's capabilities.</param>
      /// <returns>The response row.</returns>
      public static LocationListResponse From(
          LocationListItem item, IReadOnlySet<string> capabilities)
      {
          ArgumentNullException.ThrowIfNull(item);
          return new LocationListResponse(
              item.Id, item.Code, item.Name, item.IsActive,
              CallerLinks.For(
                  capabilities,
                  new LinkCandidate("self", "listLocations", "/api/locations", null),
                  new LinkCandidate(
                      "update", "updateLocation", $"/api/locations/{item.Id}",
                      nameof(StaffCapability.ManageReferenceData))));
      }
  }
  ```

  The appointment-type and attendee-group files are this file with three substitutions each: the
  route prefix, the handler trio, and the request bodies design 05 gives them — `{code, name}`
  and `{name, isActive, expectedVersion}` for types, `{code, name, appointmentTypeIds[]}` and
  `{name, isActive, appointmentTypeIds[], expectedVersion}` for groups. Their list rows project
  through `ApiResponses.AppointmentType` and `ApiResponses.AttendeeGroup` above.

  ```csharp
  // src/EventBooking.Api/Endpoints/AppointmentTypeEndpoints.cs — the two bodies and the three
  // route registrations that differ. The group, the capability metadata and the projection
  // pattern are the location file's, unchanged.
  public sealed record CreateAppointmentTypeRequest(string? Code, string? Name);

  public sealed record UpdateAppointmentTypeRequest(string? Name, bool IsActive, long ExpectedVersion);

  var group = app.MapGroup("/api/appointment-types")
      .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
      .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName);

  group.MapGet("/", async (
      bool? includeInactive,
      ListAppointmentTypesHandler handler,
      CallerCapabilities capabilities,
      CancellationToken cancellationToken) =>
  {
      var result = await handler.HandleAsync(
          new ListAppointmentTypesQuery(includeInactive ?? false), cancellationToken);
      if (result.IsFailure)
      {
          return result.ToResponse();
      }

      var held = await capabilities.GetAsync(cancellationToken);
      return Results.Ok(new Page<AppointmentTypeListResponse>(
          [.. result.Value.Select(x => AppointmentTypeListResponse.From(x, held))], null));
  })
      .WithAgentMetadata("listAppointmentTypes")
      .Produces<Page<AppointmentTypeListResponse>>(200)
      .ProducesProblem(401)
      .ProducesProblem(403);
  ```

  ```csharp
  // src/EventBooking.Api/Endpoints/AttendeeGroupEndpoints.cs — the same two differences. The
  // mapping array is the part design 05 adds over the other two reference-data resources.
  public sealed record CreateAttendeeGroupRequest(
      string? Code, string? Name, IReadOnlyList<Guid>? AppointmentTypeIds);

  public sealed record UpdateAttendeeGroupRequest(
      string? Name, IReadOnlyList<Guid>? AppointmentTypeIds, bool IsActive, long ExpectedVersion);

  group.MapPut("/{id:guid}", async (
      Guid id,
      UpdateAttendeeGroupRequest request,
      ICallerAccessor caller,
      UpdateAttendeeGroupHandler handler,
      CallerCapabilities capabilities,
      CancellationToken cancellationToken) =>
  {
      var result = await handler.HandleAsync(
          new UpdateAttendeeGroupCommand(
              caller.RequireStaffUserId(), id, request.Name, request.AppointmentTypeIds,
              request.IsActive, request.ExpectedVersion),
          cancellationToken);
      if (result.IsFailure)
      {
          return result.ToResponse();
      }

      var held = await capabilities.GetAsync(cancellationToken);
      return Results.Ok(ApiResponses.AttendeeGroup(result.Value, held));
  })
      .WithAgentMetadata("updateAttendeeGroup")
      .Produces<AttendeeGroupResponse>(200)
      .ProducesProblem(403)
      .ProducesProblem(404)
      .ProducesProblem(409)
      .ProducesProblem(422);
  ```

  ```csharp
  // src/EventBooking.Api/Endpoints/SettingsEndpoints.cs (complete)
  using EventBooking.Api.Auth;
  using EventBooking.Api.Contracts;
  using EventBooking.Api.OpenApi;
  using EventBooking.Application.Settings;

  namespace EventBooking.Api.Endpoints;

  /// <summary>Maps the two settings routes.</summary>
  public static class SettingsEndpoints
  {
      /// <summary>The settings body design 05 names.</summary>
      /// <param name="InviteExpiryDays">Days an invitation stays usable.</param>
      /// <param name="MaxAutoRetryCount">Automatic re-issues before giving up.</param>
      /// <param name="InviteOptionCount">Options offered per invitation.</param>
      /// <param name="ExpectedVersion">The version the caller read.</param>
      public sealed record UpdateSettingsRequest(
          int InviteExpiryDays, int MaxAutoRetryCount, int InviteOptionCount, long ExpectedVersion);

      /// <summary>Maps the settings routes.</summary>
      /// <param name="app">The endpoint route builder.</param>
      /// <returns>The endpoint route builder.</returns>
      public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
      {
          ArgumentNullException.ThrowIfNull(app);
          var group = app.MapGroup("/api/settings")
              .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
              .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName);

          group.MapGet("/", async (
              ICallerAccessor caller,
              AdminSettingsHandler handler,
              CallerCapabilities capabilities,
              CancellationToken cancellationToken) =>
          {
              var result = await handler.GetAsync(caller.RequireStaffUserId(), cancellationToken);
              if (result.IsFailure)
              {
                  return result.ToResponse();
              }

              var held = await capabilities.GetAsync(cancellationToken);
              return Results.Ok(ApiResponses.Settings(result.Value, held));
          })
              .WithAgentMetadata("getSettings")
              .Produces<SettingsResponse>(200)
              .ProducesProblem(403);

          group.MapPut("/", async (
              UpdateSettingsRequest request,
              ICallerAccessor caller,
              SaveSystemSettingsHandler handler,
              CallerCapabilities capabilities,
              CancellationToken cancellationToken) =>
          {
              var result = await handler.HandleAsync(
                  new SaveSystemSettingsCommand(
                      caller.RequireStaffUserId(), request.InviteExpiryDays,
                      request.MaxAutoRetryCount, request.InviteOptionCount,
                      request.ExpectedVersion),
                  cancellationToken);
              if (result.IsFailure)
              {
                  return result.ToResponse();
              }

              var held = await capabilities.GetAsync(cancellationToken);
              return Results.Ok(ApiResponses.Settings(result.Value, held));
          })
              .WithAgentMetadata("updateSettings")
              .Produces<SettingsResponse>(200)
              .ProducesProblem(403)
              .ProducesProblem(409)
              .ProducesProblem(422);

          return app;
      }
  }
  ```

  ```csharp
  // src/EventBooking.Api/Endpoints/StaffAccessEndpoints.cs (complete)
  using EventBooking.Api.Auth;
  using EventBooking.Api.Contracts;
  using EventBooking.Api.OpenApi;
  using EventBooking.Application.Access;

  namespace EventBooking.Api.Endpoints;

  /// <summary>
  /// Maps the two staff-access routes. There is deliberately no create, no delete and no role
  /// edit (FR-10.5): roles live in the identity provider, and EventBooking owns only the scope.
  /// </summary>
  public static class StaffAccessEndpoints
  {
      /// <summary>The scope body design 05 names.</summary>
      /// <param name="AppointmentTypeId">The type to scope to, or null to clear.</param>
      /// <param name="ExpectedVersion">The version the caller read.</param>
      public sealed record SetScopeRequest(Guid? AppointmentTypeId, long ExpectedVersion);

      /// <summary>Maps the staff-access routes.</summary>
      /// <param name="app">The endpoint route builder.</param>
      /// <returns>The endpoint route builder.</returns>
      public static IEndpointRouteBuilder MapStaffAccessEndpoints(this IEndpointRouteBuilder app)
      {
          ArgumentNullException.ThrowIfNull(app);
          var group = app.MapGroup("/api/staff-access")
              .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
              .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName);

          group.MapGet("/", async (
              ICallerAccessor caller,
              StaffAccessHandler handler,
              CancellationToken cancellationToken) =>
          {
              var result = await handler.ListAsync(
                  caller.RequireStaffUserId(), cancellationToken);
              return result.IsFailure
                  ? result.ToResponse()
                  : Results.Ok(new Page<StaffAccessProfileView>(result.Value, null));
          })
              .WithAgentMetadata("listStaffAccess")
              .Produces<Page<StaffAccessProfileView>>(200)
              .ProducesProblem(403);

          group.MapPut("/{staffUserId:guid}/scope", async (
              Guid staffUserId,
              SetScopeRequest request,
              ICallerAccessor caller,
              StaffScopeHandler handler,
              CancellationToken cancellationToken) =>
              (await handler.HandleAsync(
                  new SetStaffScopeCommand(
                      caller.RequireStaffUserId(), staffUserId, request.AppointmentTypeId,
                      request.ExpectedVersion),
                  cancellationToken))
                  .ToResponse())
              .WithAgentMetadata("setStaffAccessScope")
              .Produces<SetStaffScopeOutcome>(200)
              .ProducesProblem(403)
              .ProducesProblem(404)
              .ProducesProblem(409);

          return app;
      }
  }
  ```

  The staff-access list is returned in the page envelope with a null cursor rather than as a bare
  array, because design 05 says every list endpoint returns `{ items, nextCursor }` and a client
  that special-cases one list is a client that breaks when the list grows a cursor. The same
  holds for the three reference-data lists.

  **Negotiation and events.**

  ```csharp
  // src/EventBooking.Api/Endpoints/EventProposalEndpoints.cs (complete)
  using EventBooking.Api.Auth;
  using EventBooking.Api.Contracts;
  using EventBooking.Api.OpenApi;
  using EventBooking.Api.Pagination;
  using EventBooking.Application.Negotiation;
  using EventBooking.Domain.Time;

  namespace EventBooking.Api.Endpoints;

  /// <summary>Maps the five proposal routes.</summary>
  public static class EventProposalEndpoints
  {
      /// <summary>The proposal body design 05 names.</summary>
      /// <param name="LocationId">Where the event would be held.</param>
      /// <param name="Date">The local calendar date.</param>
      /// <param name="StartTime">The local start time.</param>
      /// <param name="DurationMinutes">The window length.</param>
      /// <param name="AppointmentTypeIds">Every type the event will offer.</param>
      /// <param name="Headcount">The proposer's own headcount.</param>
      public sealed record ProposeEventRequest(
          Guid LocationId, DateOnly Date, TimeOnly StartTime, int DurationMinutes,
          IReadOnlyList<Guid>? AppointmentTypeIds, int Headcount);

      /// <summary>The acceptance body design 05 names.</summary>
      /// <param name="Headcount">The accepting type's headcount.</param>
      public sealed record RecordAcceptanceRequest(int Headcount);

      /// <summary>Maps the proposal routes.</summary>
      /// <param name="app">The endpoint route builder.</param>
      /// <returns>The endpoint route builder.</returns>
      public static IEndpointRouteBuilder MapEventProposalEndpoints(this IEndpointRouteBuilder app)
      {
          ArgumentNullException.ThrowIfNull(app);
          var group = app.MapGroup("/api/event-proposals")
              .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
              .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName);

          group.MapGet("/", async (
              string? status,
              Guid? locationId,
              string? cursor,
              int? limit,
              ICallerAccessor caller,
              ListEventProposalsHandler handler,
              PageCursor cursors,
              IEventWindowZones zones,
              CallerCapabilities capabilities,
              CancellationToken cancellationToken) =>
          {
              if (!PageRequest.TryBind(cursor, limit, out var page, out var field))
              {
                  return ResultResponses.ValidationFailed(
                      field!, "out-of-range", "Limit must be between 1 and 200.");
              }

              // The signed cursor is unwrapped here and nowhere else: the handler's cursor is a
              // sort key, and a caller must never be handed one that is not signed.
              string? inner = null;
              if (page.Cursor is not null && !cursors.TryUnprotect(page.Cursor, out inner!))
              {
                  return ResultResponses.ValidationFailed(
                      "cursor", "cursor-invalid", "That cursor is not valid.");
              }

              var result = await handler.HandleAsync(
                  new ListEventProposalsQuery(
                      caller.RequireStaffUserId(), status, locationId, inner, page.Limit),
                  cancellationToken);
              if (result.IsFailure)
              {
                  return result.ToResponse();
              }

              var held = await capabilities.GetAsync(cancellationToken);
              return Results.Ok(new Page<EventProposalResponse>(
                  [.. result.Value.Items.Select(x => ApiResponses.EventProposal(x, zones, held))],
                  result.Value.NextCursor is null ? null : cursors.Protect(result.Value.NextCursor)));
          })
              .WithAgentMetadata("listEventProposals")
              .Produces<Page<EventProposalResponse>>(200)
              .ProducesProblem(403)
              .ProducesProblem(422);

          group.MapPost("/", async (
              ProposeEventRequest request,
              ICallerAccessor caller,
              ProposeEventHandler handler,
              CancellationToken cancellationToken) =>
          {
              var result = await handler.HandleAsync(
                  new ProposeEventCommand(
                      caller.RequireStaffUserId(), request.LocationId, request.Date,
                      request.StartTime, request.DurationMinutes,
                      request.AppointmentTypeIds ?? [], request.Headcount),
                  cancellationToken);
              return result.IsFailure
                  ? result.ToResponse()
                  : Results.Created($"/api/event-proposals/{result.Value.ProposalId}", new
                  {
                      id = result.Value.ProposalId,
                      status = result.Value.Status,
                      eventId = result.Value.EventId,
                  });
          })
              .WithAgentMetadata("proposeEvent")
              .Produces(201)
              .ProducesProblem(403)
              .ProducesProblem(409)
              .ProducesProblem(422);

          group.MapPut("/{id:guid}/acceptance", async (
              Guid id,
              RecordAcceptanceRequest request,
              ICallerAccessor caller,
              RecordAcceptanceHandler handler,
              CancellationToken cancellationToken) =>
              (await handler.HandleAsync(
                  new RecordAcceptanceCommand(caller.RequireStaffUserId(), id, request.Headcount),
                  cancellationToken))
                  .ToResponse())
              .WithAgentMetadata("recordAcceptance")
              .Produces<RecordAcceptanceOutcome>(200)
              .ProducesProblem(403)
              .ProducesProblem(404)
              .ProducesProblem(409)
              .ProducesProblem(422);

          group.MapDelete("/{id:guid}/acceptance", async (
              Guid id,
              ICallerAccessor caller,
              WithdrawAcceptanceHandler handler,
              CancellationToken cancellationToken) =>
              (await handler.HandleAsync(
                  new WithdrawAcceptanceCommand(caller.RequireStaffUserId(), id),
                  cancellationToken))
                  .ToResponse())
              .WithAgentMetadata("withdrawAcceptance")
              .Produces(204)
              .ProducesProblem(403)
              .ProducesProblem(404)
              .ProducesProblem(409);

          group.MapPost("/{id:guid}/withdraw", async (
              Guid id,
              ICallerAccessor caller,
              WithdrawProposalHandler handler,
              CancellationToken cancellationToken) =>
              (await handler.HandleAsync(
                  new WithdrawProposalCommand(caller.RequireStaffUserId(), id),
                  cancellationToken))
                  .ToResponse())
              .WithAgentMetadata("withdrawProposal")
              .Produces(204)
              .ProducesProblem(403)
              .ProducesProblem(404)
              .ProducesProblem(409);

          return app;
      }
  }
  ```

  The creation response is an anonymous shape rather than the handler's outcome record because
  design 05 names its members: `status` and, when the single-type case confirms on creation,
  `eventId`. Returning the outcome record directly would publish its .NET member names, which is
  the one place the wire contract and the application contract must not be the same thing.

  ```csharp
  // src/EventBooking.Api/Endpoints/EventEndpoints.cs (complete, replacing the ported file)
  using EventBooking.Api.Auth;
  using EventBooking.Api.Contracts;
  using EventBooking.Api.OpenApi;
  using EventBooking.Api.Pagination;
  using EventBooking.Application.Events;
  using EventBooking.Application.Negotiation;
  using EventBooking.Domain.Time;

  namespace EventBooking.Api.Endpoints;

  /// <summary>Maps the five event routes.</summary>
  public static class EventEndpoints
  {
      /// <summary>The capacity body design 05 names.</summary>
      /// <param name="TotalHeadcount">The new total for the named type.</param>
      public sealed record AdjustCapacityRequest(int TotalHeadcount);

      /// <summary>Maps the event routes.</summary>
      /// <param name="app">The endpoint route builder.</param>
      /// <returns>The endpoint route builder.</returns>
      public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
      {
          ArgumentNullException.ThrowIfNull(app);
          var group = app.MapGroup("/api/events")
              .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
              .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName);

          group.MapGet("/", async (
              Guid? locationId,
              DateOnly? from,
              DateOnly? to,
              Guid? appointmentTypeId,
              string? cursor,
              int? limit,
              ICallerAccessor caller,
              ListEventsHandler handler,
              PageCursor cursors,
              IEventWindowZones zones,
              CallerCapabilities capabilities,
              CancellationToken cancellationToken) =>
          {
              if (!PageRequest.TryBind(cursor, limit, out var page, out var field))
              {
                  return ResultResponses.ValidationFailed(
                      field!, "out-of-range", "Limit must be between 1 and 200.");
              }

              string? inner = null;
              if (page.Cursor is not null && !cursors.TryUnprotect(page.Cursor, out inner!))
              {
                  return ResultResponses.ValidationFailed(
                      "cursor", "cursor-invalid", "That cursor is not valid.");
              }

              var result = await handler.HandleAsync(
                  new ListEventsQuery(
                      caller.RequireStaffUserId(), locationId, from, to, appointmentTypeId,
                      inner, page.Limit),
                  cancellationToken);
              if (result.IsFailure)
              {
                  return result.ToResponse();
              }

              var held = await capabilities.GetAsync(cancellationToken);
              return Results.Ok(new Page<EventResponse>(
                  [.. result.Value.Items.Select(x => ApiResponses.Event(x, zones, held))],
                  result.Value.NextCursor is null ? null : cursors.Protect(result.Value.NextCursor)));
          })
              .WithAgentMetadata("listEvents")
              .Produces<Page<EventResponse>>(200)
              .ProducesProblem(403)
              .ProducesProblem(422);

          // Registered before the parameter route for readability only: ASP.NET already prefers
          // a literal segment over a parameter, so "cancellable" can never be read as an id.
          group.MapGet("/cancellable", async (
              Guid? locationId,
              DateOnly? from,
              DateOnly? to,
              string? cursor,
              int? limit,
              ICallerAccessor caller,
              ListCancellableEventsHandler handler,
              PageCursor cursors,
              IEventWindowZones zones,
              CallerCapabilities capabilities,
              CancellationToken cancellationToken) =>
          {
              if (!PageRequest.TryBind(cursor, limit, out var page, out var field))
              {
                  return ResultResponses.ValidationFailed(
                      field!, "out-of-range", "Limit must be between 1 and 200.");
              }

              string? inner = null;
              if (page.Cursor is not null && !cursors.TryUnprotect(page.Cursor, out inner!))
              {
                  return ResultResponses.ValidationFailed(
                      "cursor", "cursor-invalid", "That cursor is not valid.");
              }

              var result = await handler.HandleAsync(
                  new ListCancellableEventsQuery(
                      caller.RequireStaffUserId(), locationId, from, to, inner, page.Limit),
                  cancellationToken);
              if (result.IsFailure)
              {
                  return result.ToResponse();
              }

              var held = await capabilities.GetAsync(cancellationToken);
              return Results.Ok(new Page<EventResponse>(
                  [.. result.Value.Items.Select(x => ApiResponses.Event(x, zones, held))],
                  result.Value.NextCursor is null ? null : cursors.Protect(result.Value.NextCursor)));
          })
              .WithAgentMetadata("listCancellableEvents")
              .Produces<Page<EventResponse>>(200)
              .ProducesProblem(403)
              .ProducesProblem(422);

          group.MapGet("/{id:guid}", async (
              Guid id,
              ICallerAccessor caller,
              GetEventHandler handler,
              IEventWindowZones zones,
              CallerCapabilities capabilities,
              CancellationToken cancellationToken) =>
          {
              var result = await handler.HandleAsync(
                  new GetEventQuery(caller.RequireStaffUserId(), id), cancellationToken);
              if (result.IsFailure)
              {
                  return result.ToResponse();
              }

              var held = await capabilities.GetAsync(cancellationToken);
              return Results.Ok(ApiResponses.Event(result.Value, zones, held));
          })
              .WithAgentMetadata("getEvent")
              .Produces<EventResponse>(200)
              .ProducesProblem(403)
              .ProducesProblem(404);

          group.MapPut("/{id:guid}/capacities/{appointmentTypeId:guid}", async (
              Guid id,
              Guid appointmentTypeId,
              AdjustCapacityRequest request,
              ICallerAccessor caller,
              AdjustEventCapacityHandler handler,
              CancellationToken cancellationToken) =>
              (await handler.HandleAsync(
                  new AdjustEventCapacityCommand(
                      caller.RequireStaffUserId(), id, appointmentTypeId, request.TotalHeadcount),
                  cancellationToken))
                  .ToResponse())
              .WithAgentMetadata("adjustEventCapacity")
              .Produces<AdjustEventCapacityOutcome>(200)
              .ProducesProblem(403)
              .ProducesProblem(404)
              .ProducesProblem(409)
              .ProducesProblem(422);

          group.MapPost("/{id:guid}/cancel", async (
              Guid id,
              bool? confirm,
              ICallerAccessor caller,
              CancelEventHandler handler,
              CancellationToken cancellationToken) =>
          {
              var result = await handler.HandleAsync(
                  new CancelEventCommand(caller.RequireStaffUserId(), id, confirm ?? false),
                  cancellationToken);
              return result.IsFailure ? result.ToResponse() : Results.Ok(result.Value);
          })
              .WithAgentMetadata("cancelEvent")
              .Produces<CancelEventOutcome>(200)
              .ProducesProblem(403)
              .ProducesProblem(404)
              .ProducesProblem(409);

          return app;
      }
  }
  ```

  Event cancellation reports its two-step refusal as a failure from the handler, not as a
  success the endpoint reinterprets: Task 15's CancelEvent returns `confirmation-required` when
  `confirm` is false, so the endpoint's job is only to pass the flag through. The coordinator's
  booking cancellation is the other way round — its outcome carries the confirmation flag as
  data — and is translated below.

  **Attendees, dashboards, audit and the workspace.**

  ```csharp
  // src/EventBooking.Api/Endpoints/AttendeeEndpoints.cs — the thirteen registrations, replacing
  // the ported file's body. The group, the request records and the projections follow the
  // location file's shape; the four that are not a plain pass-through are written out in full.
  var group = app.MapGroup("/api/attendees")
      .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
      .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName);

  group.MapGet("/", async (
      string? status,
      Guid? groupId,
      string? readiness,
      string? search,
      string? cursor,
      int? limit,
      ICallerAccessor caller,
      ListAttendeesHandler handler,
      PageCursor cursors,
      CancellationToken cancellationToken) =>
  {
      if (!PageRequest.TryBind(cursor, limit, out var page, out var field))
      {
          return ResultResponses.ValidationFailed(
              field!, "out-of-range", "Limit must be between 1 and 200.");
      }

      string? inner = null;
      if (page.Cursor is not null && !cursors.TryUnprotect(page.Cursor, out inner!))
      {
          return ResultResponses.ValidationFailed(
              "cursor", "cursor-invalid", "That cursor is not valid.");
      }

      var result = await handler.HandleAsync(
          new ListAttendeesQuery(
              caller.RequireStaffUserId(), inner, page.Limit, status, groupId, readiness, search),
          cancellationToken);
      if (result.IsFailure)
      {
          return result.ToResponse();
      }

      // Each row's own cursor is signed too: a client that pages from a row it is looking at
      // must not be handed an unsigned key it could edit.
      return Results.Ok(new Page<object>(
          [.. result.Value.Items.Select(x => (object)new
          {
              x.Name, x.Email, x.Status, x.GroupCode, x.Readiness, x.RequiredTypeCodes,
              x.LatestDeliveryStatus,
              cursor = cursors.Protect(x.Cursor),
          })],
          result.Value.NextCursor is null ? null : cursors.Protect(result.Value.NextCursor)));
  })
      .WithAgentMetadata("listAttendees")
      .Produces(200)
      .ProducesProblem(403)
      .ProducesProblem(422);

  // Two-step delete. The handler reports the consequence as a failure carrying its count, so
  // the endpoint renders Task 21's confirmation body from it and decides nothing.
  group.MapDelete("/{id:guid}", async (
      Guid id,
      bool? confirm,
      ICallerAccessor caller,
      DeleteAttendeeHandler handler,
      CancellationToken cancellationToken) =>
      (await handler.HandleAsync(
          new DeleteAttendeeCommand(caller.RequireStaffUserId(), id, confirm ?? false),
          cancellationToken))
          .ToResponse())
      .WithAgentMetadata("deleteAttendee")
      .Produces(204)
      .ProducesProblem(403)
      .ProducesProblem(404)
      .ProducesProblem(409);

  // Multipart, per design 05. The handler takes the file's text, so the endpoint's whole job is
  // to find the part, hold it to the 1 MB bound before reading it, and decode it as UTF-8.
  group.MapPost("/import", async (
      HttpRequest request,
      ICallerAccessor caller,
      ImportAttendeesHandler handler,
      CancellationToken cancellationToken) =>
  {
      if (!request.HasFormContentType)
      {
          return ResultResponses.ValidationFailed(
              "file", "multipart-required", "Upload the CSV as a multipart form file.");
      }

      var form = await request.ReadFormAsync(cancellationToken);
      var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
      if (file is null)
      {
          return ResultResponses.ValidationFailed(
              "file", "file-required", "A CSV file is required.");
      }

      if (file.Length > MaxImportBytes)
      {
          return ResultResponses.ValidationFailed(
              "file", "file-too-large", "The file must be 1 MB or smaller.");
      }

      await using var stream = file.OpenReadStream();
      using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
      var csv = await reader.ReadToEndAsync(cancellationToken);

      return (await handler.HandleAsync(
          new ImportAttendeesCommand(caller.RequireStaffUserId(), csv), cancellationToken))
          .ToResponse();
  })
      .WithAgentMetadata("importAttendees")
      .DisableAntiforgery()
      .Produces<AttendeeImportOutcome>(200)
      .ProducesProblem(403)
      .ProducesProblem(422);

  // Two-step booking cancellation. Here the handler reports the consequence as a SUCCESS
  // carrying ConfirmationRequired, so the translation is the endpoint's — a rendering decision,
  // not a business one, and the only place in this file where a success becomes a 409.
  group.MapPost("/{id:guid}/bookings/{bookingId:guid}/cancel", async (
      Guid id,
      Guid bookingId,
      bool? confirm,
      ICallerAccessor caller,
      CancelBookingByCoordinatorHandler handler,
      CancellationToken cancellationToken) =>
  {
      var result = await handler.HandleAsync(
          new CancelBookingByCoordinatorCommand(
              caller.RequireStaffUserId(), id, bookingId, confirm ?? false),
          cancellationToken);
      if (result.IsFailure)
      {
          return result.ToResponse();
      }

      return result.Value.ConfirmationRequired
          ? ResultResponses.ConfirmationRequired(
              "Cancelling this booking will release its place.",
              new Dictionary<string, long> { ["activeBookings"] = result.Value.ActiveBookingCount })
          : Results.Ok(result.Value);
  })
      .WithAgentMetadata("cancelAttendeeBooking")
      .Produces<CoordinatorCancelOutcome>(200)
      .ProducesProblem(403)
      .ProducesProblem(404)
      .ProducesProblem(409);
  ```

  ```csharp
  // The remaining attendee registrations are explicit rather than prose. Each only binds its
  // transport shape and calls one handler; all authorization and state rules remain in Application.
  group.MapPost("/", async (SaveAttendeeRequest request, ICallerAccessor caller,
      SaveAttendeeHandler handler, CancellationToken ct) =>
      (await handler.CreateAsync(new CreateAttendeeCommand(caller.RequireStaffUserId(),
          request.Name, request.Email, request.AttendeeGroupId), ct)).ToCreated(id => $"/api/attendees/{id}"))
      .WithAgentMetadata("createAttendee");

  group.MapPut("/{id:guid}", async (Guid id, SaveAttendeeRequest request, ICallerAccessor caller,
      SaveAttendeeHandler handler, CancellationToken ct) =>
      (await handler.UpdateAsync(new UpdateAttendeeCommand(caller.RequireStaffUserId(), id,
          request.Name, request.Email, request.AttendeeGroupId), ct)).ToResponse())
      .WithAgentMetadata("updateAttendee");

  group.MapGet("/{id:guid}/eligible-event-count", async (Guid id, Guid[] locationIds,
      ICallerAccessor caller, CountEligibleEventsHandler handler, CancellationToken ct) =>
      (await handler.HandleAsync(new CountEligibleEventsQuery(caller.RequireStaffUserId(), id,
          locationIds), ct)).ToResponse())
      .WithAgentMetadata("countEligibleEvents");

  group.MapPost("/{id:guid}/invites", async (Guid id, LocationIdsRequest request,
      ICallerAccessor caller, InviteAttendeeHandler handler, CancellationToken ct) =>
      (await handler.HandleAsync(new InviteAttendeeCommand(caller.RequireStaffUserId(), id,
          request.LocationIds), ct)).ToResponse())
      .WithAgentMetadata("inviteAttendee");

  group.MapPost("/{id:guid}/recovery-invites", async (Guid id, AdditionalLocationIdsRequest request,
      ICallerAccessor caller, StartRecoveryHandler handler, CancellationToken ct) =>
      (await handler.HandleAsync(new StartRecoveryCommand(caller.RequireStaffUserId(), id,
          request.AdditionalLocationIds), ct)).ToResponse())
      .WithAgentMetadata("startRecoveryInvite");

  group.MapDelete("/{id:guid}/recovery-invites/{inviteId:guid}", async (Guid inviteId,
      ICallerAccessor caller, CancelRecoveryInviteHandler handler, CancellationToken ct) =>
      (await handler.HandleAsync(new CancelRecoveryInviteCommand(caller.RequireStaffUserId(), inviteId), ct)).ToResponse())
      .WithAgentMetadata("cancelRecoveryInvite");

  group.MapGet("/{id:guid}/bookings", async (Guid id, ICallerAccessor caller,
      GetAttendeeBookingsHandler handler, CancellationToken ct) =>
      (await handler.HandleAsync(new GetAttendeeBookingsQuery(caller.RequireStaffUserId(), id), ct)).ToResponse())
      .WithAgentMetadata("listAttendeeBookings");

  group.MapPost("/{id:guid}/email-retry", async (Guid id, ICallerAccessor caller,
      RetryNewestEmailHandler handler, CancellationToken ct) =>
      (await handler.HandleAsync(new RetryNewestEmailCommand(caller.RequireStaffUserId(), id), ct)).ToResponse())
      .WithAgentMetadata("retryAttendeeEmail");

  group.MapGet("/{id:guid}/readiness", async (Guid id, ICallerAccessor caller,
      GetAttendeeReadinessHandler handler, CancellationToken ct) =>
      (await handler.HandleAsync(new GetAttendeeReadinessQuery(caller.RequireStaffUserId(), id), ct)).ToResponse())
      .WithAgentMetadata("getAttendeeReadiness");

  public sealed record SaveAttendeeRequest(string Name, string Email, Guid AttendeeGroupId);
  public sealed record LocationIdsRequest(IReadOnlyList<Guid> LocationIds);
  public sealed record AdditionalLocationIdsRequest(IReadOnlyList<Guid> AdditionalLocationIds);
  ```

  ```csharp
  // src/EventBooking.Api/Endpoints/DashboardEndpoints.cs (complete)
  using EventBooking.Api.Auth;
  using EventBooking.Api.OpenApi;
  using EventBooking.Application.Dashboards;

  namespace EventBooking.Api.Endpoints;

  /// <summary>Maps the dashboards route.</summary>
  public static class DashboardEndpoints
  {
      /// <summary>Maps <c>GET /api/dashboards</c>.</summary>
      /// <param name="app">The endpoint route builder.</param>
      /// <returns>The endpoint route builder.</returns>
      public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
      {
          ArgumentNullException.ThrowIfNull(app);

          app.MapGet("/api/dashboards", async (
              Guid? locationId,
              ICallerAccessor caller,
              GetDashboardsHandler handler,
              CancellationToken cancellationToken) =>
              (await handler.HandleAsync(
                  new GetDashboardsQuery(caller.RequireStaffUserId(), locationId),
                  cancellationToken))
                  .ToResponse())
              .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
              .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName)
              .WithAgentMetadata("getDashboards")
              .Produces<DashboardsView>(200)
              .ProducesProblem(403);

          return app;
      }
  }
  ```

  ```csharp
  // src/EventBooking.Api/Endpoints/AuditEndpoints.cs — the three registrations, replacing the
  // ported file's body. All three page the same way, so the cursor handling is factored once.
  var group = app.MapGroup("/api/audit")
      .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
      .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName);

  group.MapGet("/", async (
      DateTimeOffset? from,
      DateTimeOffset? to,
      string? action,
      string? entityType,
      Guid? entityId,
      string? cursor,
      int? limit,
      ICallerAccessor caller,
      SearchAuditHandler handler,
      PageCursor cursors,
      CancellationToken cancellationToken) =>
      await PagedAsync(cursors, cursor, limit, async (inner, size) =>
          await handler.HandleAsync(
              new SearchAuditQuery(
                  caller.RequireStaffUserId(), inner, size, entityType, action, from, to, entityId),
              cancellationToken)))
      .WithAgentMetadata("searchAudit")
      .Produces<Page<object>>(200)
      .ProducesProblem(403)
      .ProducesProblem(422);

  group.MapGet("/attendees/{id:guid}", async (
      Guid id,
      string? cursor,
      int? limit,
      ICallerAccessor caller,
      AttendeeHistoryHandler handler,
      PageCursor cursors,
      CancellationToken cancellationToken) =>
      await PagedAsync(cursors, cursor, limit, async (inner, size) =>
          await handler.HandleAsync(
              new GetAttendeeHistoryQuery(caller.RequireStaffUserId(), id, inner, size),
              cancellationToken)))
      .WithAgentMetadata("getAttendeeAuditHistory")
      .Produces<Page<object>>(200)
      .ProducesProblem(403)
      .ProducesProblem(422);

  group.MapGet("/events/{id:guid}", async (
      Guid id,
      string? cursor,
      int? limit,
      ICallerAccessor caller,
      EventHistoryHandler handler,
      PageCursor cursors,
      CancellationToken cancellationToken) =>
      await PagedAsync(cursors, cursor, limit, async (inner, size) =>
          await handler.HandleAsync(
              new GetEventHistoryQuery(caller.RequireStaffUserId(), id, inner, size),
              cancellationToken)))
      .WithAgentMetadata("getEventAuditHistory")
      .Produces<Page<object>>(200)
      .ProducesProblem(403)
      .ProducesProblem(422);

  /// <summary>
  /// Binds the page, unwraps the signed cursor, runs the query and signs every cursor on the
  /// way out. The three audit reads differ only in which query they run, so the wrapping lives
  /// here rather than three times.
  /// </summary>
  private static async Task<IResult> PagedAsync(
      PageCursor cursors, string? cursor, int? limit,
      Func<string?, int, Task<Result<AuditSearchView>>> query)
  {
      if (!PageRequest.TryBind(cursor, limit, out var page, out var field))
      {
          return ResultResponses.ValidationFailed(
              field!, "out-of-range", "Limit must be between 1 and 200.");
      }

      string? inner = null;
      if (page.Cursor is not null && !cursors.TryUnprotect(page.Cursor, out inner!))
      {
          return ResultResponses.ValidationFailed(
              "cursor", "cursor-invalid", "That cursor is not valid.");
      }

      var result = await query(inner, page.Limit);
      if (result.IsFailure)
      {
          return result.ToResponse();
      }

      return Results.Ok(new Page<object>(
          [.. result.Value.Items.Select(row => (object)new
          {
              row.Id, row.EntityType, row.Action, row.ActorType, row.OccurredAt,
              cursor = cursors.Protect(row.Cursor),
          })],
          result.Value.NextCursor is null ? null : cursors.Protect(result.Value.NextCursor)));
  }
  ```

  ```csharp
  // src/EventBooking.Api/Endpoints/AppointmentWorkspaceEndpoints.cs — the four registrations,
  // replacing the ported file's body. The roster download is the only one that is not JSON.
  var group = app.MapGroup("/api/appointment-workspace")
      .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
      .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName);

  group.MapGet("/events", async (
      Guid? locationId,
      ICallerAccessor caller,
      ListWorkspaceEventsHandler handler,
      CancellationToken cancellationToken) =>
  {
      var result = await handler.HandleAsync(
          new ListWorkspaceEventsQuery(caller.RequireStaffUserId(), locationId),
          cancellationToken);
      return result.IsFailure
          ? result.ToResponse()
          : Results.Ok(new Page<WorkspaceEventView>(result.Value, null));
  })
      .WithAgentMetadata("listWorkspaceEvents")
      .Produces<Page<WorkspaceEventView>>(200)
      .ProducesProblem(403);

  group.MapGet("/events/{eventId:guid}", async (
      Guid eventId,
      ICallerAccessor caller,
      GetWorkspaceRosterHandler handler,
      CancellationToken cancellationToken) =>
  {
      var result = await handler.HandleAsync(
          new GetWorkspaceRosterQuery(caller.RequireStaffUserId(), eventId), cancellationToken);
      return result.IsFailure
          ? result.ToResponse()
          : Results.Ok(new Page<WorkspaceRosterRow>(result.Value, null));
  })
      .WithAgentMetadata("getWorkspaceRoster")
      .Produces<Page<WorkspaceRosterRow>>(200)
      .ProducesProblem(403)
      .ProducesProblem(404);

  group.MapPut("/appointments/{id:guid}/status", async (
      Guid id,
      SetAppointmentStatusRequest request,
      ICallerAccessor caller,
      SetAppointmentStatusHandler handler,
      CancellationToken cancellationToken) =>
      (await handler.HandleAsync(
          new SetAppointmentStatusCommand(
              caller.RequireStaffUserId(), id, request.TargetStatus, request.ExpectedVersion),
          cancellationToken))
          .ToResponse())
      .WithAgentMetadata("setAppointmentStatus")
      .Produces(204)
      .ProducesProblem(403)
      .ProducesProblem(404)
      .ProducesProblem(409)
      .ProducesProblem(422);

  // The route ends in a literal ".csv", which is how design 05 writes it. The colon-less
  // segment keeps the eventId constraint on the part before it.
  group.MapGet("/events/{eventId:guid}/roster.csv", async (
      Guid eventId,
      ICallerAccessor caller,
      DownloadRosterHandler handler,
      CancellationToken cancellationToken) =>
  {
      var result = await handler.HandleAsync(
          new GetWorkspaceRosterQuery(caller.RequireStaffUserId(), eventId), cancellationToken);
      return result.IsFailure
          ? result.ToResponse()
          : Results.Text(
              result.Value, "text/csv", System.Text.Encoding.UTF8);
  })
      .WithAgentMetadata("exportWorkspaceRoster")
      .Produces<string>(200, "text/csv")
      .ProducesProblem(403)
      .ProducesProblem(404);
  ```

  ```csharp
  // src/EventBooking.Api/Endpoints/BookingEndpoints.cs — the two book-token routes, replacing
  // the ported file's body. The global attendee-address limiter and this endpoint token-prefix
  // limiter are composed by the rate-limiting middleware; duplicate endpoint metadata would
  // select only the final policy.
  var group = app.MapGroup("/api/booking")
      .AllowAnonymous()
      .RequireRateLimiting(TokenPrefixRateLimiterPolicy.PolicyName);

  group.MapGet("/{token}", async (
      string token,
      ViewInviteHandler handler,
      CancellationToken cancellationToken) =>
      (await handler.HandleAsync(new ViewInviteQuery(token), cancellationToken))
          .ToResponse())
      .WithAgentMetadata("viewInvite")
      .Produces<InviteView>(200)
      .ProducesProblem(404)
      .ProducesProblem(410)
      .ProducesProblem(429);

  group.MapPost("/{token}/confirm", async (
      string token,
      ConfirmBookingRequest request,
      ConfirmBookingHandler handler,
      CancellationToken cancellationToken) =>
  {
      var result = await handler.HandleAsync(
          new ConfirmBookingCommand(token, request.EventId), cancellationToken);
      return result.IsFailure
          ? result.ToResponse()
          : Results.Created(
              $"/api/manage/{result.Value.ManageToken}",
              new { bookingId = result.Value.BookingId, manageToken = result.Value.ManageToken });
  })
      .WithAgentMetadata("confirmBooking")
      .Produces(201)
      .ProducesProblem(404)
      .ProducesProblem(409)
      .ProducesProblem(410)
      .ProducesProblem(429);
  ```

  ```csharp
  // src/EventBooking.Api/Endpoints/ManageEndpoints.cs (complete)
  using EventBooking.Api.Auth;
  using EventBooking.Api.OpenApi;
  using EventBooking.Application.Bookings;

  namespace EventBooking.Api.Endpoints;

  /// <summary>Maps the two manage-token routes. Anonymous and rate-limited, like the book pair.</summary>
  public static class ManageEndpoints
  {
      /// <summary>The cancellation body design 05 names.</summary>
      /// <param name="RequestNewTime">Whether the attendee wants a replacement invitation.</param>
      public sealed record CancelBookingRequest(bool RequestNewTime);

      /// <summary>Maps the manage-token routes.</summary>
      /// <param name="app">The endpoint route builder.</param>
      /// <returns>The endpoint route builder.</returns>
      public static IEndpointRouteBuilder MapManageEndpoints(this IEndpointRouteBuilder app)
      {
          ArgumentNullException.ThrowIfNull(app);
          var group = app.MapGroup("/api/manage")
              .AllowAnonymous()
              .RequireRateLimiting(TokenPrefixRateLimiterPolicy.PolicyName);

          group.MapGet("/{token}", async (
              string token,
              ViewBookingHandler handler,
              CancellationToken cancellationToken) =>
              (await handler.HandleAsync(new ViewBookingQuery(token), cancellationToken))
                  .ToResponse())
              .WithAgentMetadata("viewManagedBooking")
              .Produces<BookingView>(200)
              .ProducesProblem(404)
              .ProducesProblem(429);

          group.MapPost("/{token}/cancel", async (
              string token,
              CancelBookingRequest request,
              CancelBookingByAttendeeHandler handler,
              CancellationToken cancellationToken) =>
              (await handler.HandleAsync(
                  new CancelBookingByAttendeeCommand(token, request.RequestNewTime),
                  cancellationToken))
                  .ToResponse())
              .WithAgentMetadata("cancelManagedBooking")
              .Produces<AttendeeCancelOutcome>(200)
              .ProducesProblem(404)
              .ProducesProblem(409)
              .ProducesProblem(429);

          return app;
      }
  }
  ```

  ```csharp
  // src/EventBooking.Api/Program.cs — the mapping block, replacing Task 21's. The capability
  // resolver is scoped, so one request resolves the profile once however many representations
  // it renders.
  builder.Services.AddScoped<EventBooking.Api.Contracts.CallerCapabilities>();

  app.MapApiDiscoveryEndpoints();
  app.MapMeEndpoints();

  app.MapLocationEndpoints();
  app.MapAppointmentTypeEndpoints();
  app.MapAttendeeGroupEndpoints();
  app.MapSettingsEndpoints();
  app.MapStaffAccessEndpoints();

  app.MapEventProposalEndpoints();
  app.MapEventEndpoints();

  app.MapAttendeeEndpoints();
  app.MapDashboardEndpoints();
  app.MapAuditEndpoints();
  app.MapAppointmentWorkspaceEndpoints();

  app.MapBookingEndpoints();
  app.MapManageEndpoints();
  ```

- [ ] **Step 4: Run.** Expected: PASS — the catalogue suite plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  Then refresh the OpenAPI snapshot and read its diff before accepting it. Every route in the
  diff should be one design 05 names; a route that appears and is not in the design is a
  catalogue mistake, and the catalogue test will already have said so.

  No migration is needed: this task adds no column, no index and no table.

  **No figure here is observed.** This task is hand-authored and nothing in it has been built or
  run. Expect the API count to rise by roughly forty-five cases and the ported endpoint suites to
  shrink as their routes are replaced — several of them test routes that no longer exist and are
  deleted with the ported hypermedia files. The last measured checkpoint remains Task 11 at 1570.

- [ ] **Step 5: Commit and push** the executor's code — not the plan documents — under the
  master plan's message:

  ```bash
  test -z "$(git status --porcelain --ignored=no | grep -v '^??')"
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  git add src/EventBooking.Api/ tests/EventBooking.Api.Tests/
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "feat(api): full EventBooking endpoint catalogue

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```
