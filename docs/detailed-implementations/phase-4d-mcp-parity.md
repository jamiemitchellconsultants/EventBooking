# 04d — MCP parity and the phase gate (Task 23)

[← Phase overview](phase-4-api-and-mcp.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 22b and closes Phase 4. Every staff operation in the catalogue gains an
MCP tool named from its `x-mcp-tool` extension, calling the same handler under the same
authorization pipeline. A CI test compares the two surfaces and fails on any difference. The
task also carries the phase's pull-request gate.

> Use superpowers:executing-plans. This task is hand-authored: complete code and complete tests
> are written straight into this document, with no prototype. Compile and test-drive them
> yourself. The test counts below are what you should expect to reach, not figures observed by
> the author — nothing here has been run.

**Goal:** Forty-five tools, one per staff operation, split by area to match the endpoint files.
Each is named from the catalogue, described from the catalogue, hinted from the catalogue, and
calls the handler its endpoint calls. `tools/list` equals the OpenAPI operation set minus the
anonymous and token routes (FR-14.1). Every list tool's rows carry an identifier, a name or code,
and a status. A CSV import over a thousand rows is refused identically to REST, and a Manager
with a null scope is refused identically to REST.

**Architecture:** The MCP host already shares the Api project's authentication, its staff policy
and its identity recorder; this task changes neither. What it adds is the tool surface, and the
discipline that the surface is derived rather than invented: a tool's name, description and
hints come from the shared catalogue, so a tool cannot describe itself differently from the
route it mirrors. Tools translate and hold no rule, exactly as endpoints do — the capability
check is the handler's, which is what lets one check serve both surfaces.

**Tech Stack:** .NET 10, xUnit, WebApplicationFactory, ModelContextProtocol, EF Core with
Npgsql, Testcontainers, PostgreSQL 16.

**Spec:** [Master Task 23](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[API design](../design/05-api-design.md),
[security and authentication](../design/06-security-and-authentication.md),
[functional requirements](../design/02-functional-requirements.md), [ontology](../ontology.md).

## Global constraints

A tool calls exactly one handler. No tool exists for an anonymous or attendee-token route: an
agent holding a staff token must not be able to act as an attendee, which is the reason those
four routes were excluded in the catalogue rather than merely omitted here. Every tool's
name matches its catalogue entry's `x-mcp-tool` exactly, and the parity test is what enforces
that rather than review. A tool that fails returns a protocol error carrying the application
error's code, so an agent sees the same slug a REST caller would.

## Review focus

STOP AND CHECK four things. The parity test compares sets both ways and asserts the count, so a
tool added without a route fails it as loudly as a route added without a tool. The list-shape
test drives every list tool rather than a sample, because a sample proves only that the sampled
tools were written carefully. The null-scope test asserts the refusal comes from the handler,
not from the tool — the tool has no scope check to get right, and a tool that grew one would
pass this test while diverging from REST. And the import test uses the same file the REST
import test uses, which is the only way "refused identically" means anything.

### Task 23: MCP parity

**Files:**

- Create: src/EventBooking.Mcp/Tools/ToolDescriptions.cs (names, descriptions and hints from the catalogue)
- Modify: src/EventBooking.Mcp/Tools/McpErrors.cs (preserve the application error code in protocol failures)
- Create: src/EventBooking.Mcp/Tools/IdentityTools.cs
- Create: src/EventBooking.Mcp/Tools/ReferenceDataTools.cs (nine: locations, types, groups)
- Create: src/EventBooking.Mcp/Tools/AdministrationTools.cs (four: settings and staff access)
- Create: src/EventBooking.Mcp/Tools/NegotiationTools.cs
- Modify: src/EventBooking.Mcp/Tools/EventTools.cs (design 05's event tools)
- Modify: src/EventBooking.Mcp/Tools/AttendeeTools.cs (design 05's attendee tools)
- Create: src/EventBooking.Mcp/Tools/DashboardTools.cs
- Create: src/EventBooking.Mcp/Tools/AuditTools.cs
- Create: src/EventBooking.Mcp/Tools/WorkspaceTools.cs
- Delete: src/EventBooking.Mcp/Tools/AdminTools.cs (split into reference data, settings and staff access)
- Delete: src/EventBooking.Mcp/Tools/OperationsTools.cs (its board is gone with Task 20a's handler)
- Modify: src/EventBooking.Mcp/Program.cs (the new tool types)
- Test: tests/EventBooking.Mcp.Tests/ParityTests.cs
- Test: tests/EventBooking.Mcp.Tests/ListShapeTests.cs
- Test: tests/EventBooking.Mcp.Tests/RefusalParityTests.cs
- Test: tests/EventBooking.Mcp.Tests/McpClient.cs (the JSON-RPC helper the three suites share)

The ported suites that drive deleted tools go with them: the vocabulary tool test, the agent
surface parity test and the two per-area tool tests are replaced by the three suites above.

**Interfaces:**

```csharp
namespace EventBooking.Mcp.Tools;

// A tool's name, description and hints come from the shared catalogue rather than from its own
// attribute arguments, so a tool cannot describe itself differently from the route it mirrors.
// The attribute still carries the literal name, because the MCP SDK reads it at registration
// time; ToolDescriptions is what the parity test compares that literal against.
public static class ToolDescriptions
{
    // Throws when the tool name is not in the catalogue, which turns a typo in an attribute
    // into a startup failure rather than a tool nobody can find.
    public static string For(string toolName);

    public static IReadOnlySet<string> All { get; }
}
```

```csharp
// tests/EventBooking.Mcp.Tests/McpClient.cs — the JSON-RPC shape the three suites share.
// The MCP HTTP transport answers either as JSON or as an event stream depending on the
// negotiated accept headers, so a helper that reads only one of them passes or fails for
// reasons that have nothing to do with the tool under test.
public sealed class McpClient(HttpClient client)
{
    public Task<JsonElement> ListToolsAsync();

    public Task<JsonElement> CallAsync(string tool, object arguments);
}
```

- [ ] **Step 1: Write the failing tests.** Four files. The client helper first, because all
  three suites read responses through it.

  ```csharp
  // tests/EventBooking.Mcp.Tests/McpClient.cs (complete)
  using System.Net.Http.Headers;
  using System.Text;
  using System.Text.Json;

  namespace EventBooking.Mcp.Tests;

  /// <summary>
  /// The JSON-RPC calls these suites make. The HTTP transport answers as JSON or as an event
  /// stream depending on what the request accepts, so both are read here — a helper that
  /// understood only one would pass or fail for reasons unrelated to the tool under test.
  /// </summary>
  /// <param name="client">The client to send through.</param>
  public sealed class McpClient(HttpClient client)
  {
      private int _id;

      /// <summary>Calls <c>tools/list</c>.</summary>
      /// <returns>The result element.</returns>
      public async Task<JsonElement> ListToolsAsync() => await SendAsync("tools/list", null);

      /// <summary>Calls one tool.</summary>
      /// <param name="tool">The tool name.</param>
      /// <param name="arguments">The tool arguments.</param>
      /// <returns>The result element.</returns>
      public async Task<JsonElement> CallAsync(string tool, object arguments) =>
          await SendAsync("tools/call", new { name = tool, arguments });

      /// <summary>Calls one tool and returns the raw envelope, errors included.</summary>
      /// <param name="tool">The tool name.</param>
      /// <param name="arguments">The tool arguments.</param>
      /// <returns>The whole response element.</returns>
      public async Task<JsonElement> CallRawAsync(string tool, object arguments) =>
          await SendRawAsync("tools/call", new { name = tool, arguments });

      private async Task<JsonElement> SendAsync(string method, object? parameters)
      {
          var envelope = await SendRawAsync(method, parameters);
          return envelope.GetProperty("result");
      }

      private async Task<JsonElement> SendRawAsync(string method, object? parameters)
      {
          var payload = parameters is null
              ? JsonSerializer.Serialize(new
              {
                  jsonrpc = "2.0", id = Interlocked.Increment(ref _id).ToString(), method,
              })
              : JsonSerializer.Serialize(new
              {
                  jsonrpc = "2.0", id = Interlocked.Increment(ref _id).ToString(), method,
                  @params = parameters,
              });

          using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
          {
              Content = new StringContent(payload, Encoding.UTF8, "application/json"),
          };
          request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
          request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

          using var response = await client.SendAsync(request);
          response.EnsureSuccessStatusCode();
          var body = await response.Content.ReadAsStringAsync();
          var json = body.TrimStart().StartsWith('{')
              ? body
              : body.Split('\n').Select(line => line.Trim())
                  .Last(line => line.StartsWith("data: ", StringComparison.Ordinal))["data: ".Length..];

          using var parsed = JsonDocument.Parse(json);
          return parsed.RootElement.Clone();
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Mcp.Tests/ParityTests.cs (complete)
  using System.Text.Json;
  using EventBooking.Api.OpenApi;
  using EventBooking.Domain.Access;

  namespace EventBooking.Mcp.Tests;

  /// <summary>
  /// FR-14.1: the tool set is the OpenAPI operation set minus the anonymous and token routes.
  /// Compared both ways, so a tool without a route fails as loudly as a route without a tool.
  /// </summary>
  [Collection("mcp")]
  public sealed class ParityTests(McpFactory factory)
  {
      [Fact]
      public async Task TheToolSetEqualsTheStaffOperationSet()
      {
          factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
          var tools = await ToolsAsync();
          var expected = AgentOperationCatalog.All.Values
              .Where(x => x.McpTool is not null)
              .Select(x => x.McpTool!)
              .ToHashSet(StringComparer.Ordinal);

          Assert.Equal(45, expected.Count);
          Assert.Equal(expected.Count, tools.Count);
          Assert.Empty(expected.Except(tools.Keys));
          Assert.Empty(tools.Keys.Except(expected));
      }

      /// <summary>
      /// The four attendee-token routes and the six discovery and health routes have no tool.
      /// Naming them here rather than deriving them means a later decision to expose one has to
      /// change this test on purpose.
      /// </summary>
      [Theory]
      [InlineData("view_invite")]
      [InlineData("confirm_booking")]
      [InlineData("view_managed_booking")]
      [InlineData("cancel_managed_booking")]
      [InlineData("get_api_index")]
      [InlineData("get_metrics")]
      public async Task NoToolExistsForAnAnonymousRoute(string absent)
      {
          factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);

          var tools = await ToolsAsync();

          Assert.DoesNotContain(absent, tools.Keys);
      }

      /// <summary>Each tool's description and hints are the catalogue's, not its own.</summary>
      [Fact]
      public async Task EveryToolCarriesTheCataloguesDescriptionAndHints()
      {
          factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
          var tools = await ToolsAsync();

          foreach (var operation in AgentOperationCatalog.All.Values.Where(x => x.McpTool is not null))
          {
              var tool = tools[operation.McpTool!];
              Assert.Equal(operation.Description, tool.GetProperty("description").GetString());
              var annotations = tool.GetProperty("annotations");
              Assert.Equal(
                  operation.Hints.ReadOnly, annotations.GetProperty("readOnlyHint").GetBoolean());
              Assert.Equal(
                  operation.Hints.Destructive, annotations.GetProperty("destructiveHint").GetBoolean());
              Assert.Equal(
                  operation.Hints.Idempotent, annotations.GetProperty("idempotentHint").GetBoolean());
              Assert.False(annotations.GetProperty("openWorldHint").GetBoolean());
          }
      }

      [Fact]
      public async Task EveryToolNameIsSnakeCase()
      {
          factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);

          var tools = await ToolsAsync();

          Assert.All(tools.Keys, name => Assert.Matches("^[a-z][a-z0-9_]*$", name));
      }

      private async Task<IReadOnlyDictionary<string, JsonElement>> ToolsAsync()
      {
          var client = new McpClient(factory.CreateClient());
          var result = await client.ListToolsAsync();
          return result.GetProperty("tools").EnumerateArray()
              .ToDictionary(x => x.GetProperty("name").GetString()!, x => x);
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Mcp.Tests/ListShapeTests.cs (complete)
  using System.Text.Json;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.AppointmentTypes;
  using EventBooking.Domain.Attendees;
  using EventBooking.Domain.AttendeeGroups;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Bookings;
  using EventBooking.Domain.Events;
  using EventBooking.Domain.Invites;
  using EventBooking.Domain.Locations;
  using EventBooking.Infrastructure.Persistence;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;

  namespace EventBooking.Mcp.Tests;

  /// <summary>
  /// Master Task 23: every list tool's rows carry an identifier, a name or code, and a status.
  /// An agent that cannot tell one row from another has to guess, and every list tool is driven
  /// here rather than a sample — a sample proves only that the sampled tools were careful.
  /// </summary>
  [Collection("mcp")]
  public sealed class ListShapeTests(McpFactory factory)
  {
      /// <summary>
      /// The list tools and the arguments each needs to answer. A tool absent from this table
      /// fails the completeness case below, so adding a list tool without covering it is not
      /// something review has to catch.
      /// </summary>
      public static TheoryData<string> ListTools =>
      [
          "list_locations",
          "list_appointment_types",
          "list_attendee_groups",
          "list_staff_access",
          "list_event_proposals",
          "list_events",
          "list_cancellable_events",
          "list_attendees",
          "list_attendee_bookings",
          "list_workspace_events",
          "search_audit",
      ];

      [Theory]
      [MemberData(nameof(ListTools))]
      public async Task EveryRowCarriesAnIdentifierANameOrCodeAndAStatus(string tool)
      {
          var world = await GivenSeededWorldAsync(tool);
          var client = new McpClient(factory.CreateClient());

          var result = await client.CallAsync(tool, ArgumentsFor(tool, world));

          var rows = Rows(result);
          Assert.NotEmpty(rows);
          Assert.All(rows, row =>
          {
              Assert.True(HasAny(row, "id", "eventId", "proposalId", "attendeeId", "staffUserId"));
              Assert.True(HasAny(row, "name", "code", "locationName"));
              Assert.True(HasAny(row, "status", "isActive", "readiness", "appointmentStatus"));
          });
      }

      /// <summary>Every tool whose name begins with list, plus the audit search, is covered above.</summary>
      [Fact]
      public async Task TheTableCoversEveryListTool()
      {
          factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
          var client = new McpClient(factory.CreateClient());
          var result = await client.ListToolsAsync();

          var listed = result.GetProperty("tools").EnumerateArray()
              .Select(x => x.GetProperty("name").GetString()!)
              .Where(name => name.StartsWith("list_", StringComparison.Ordinal) || name == "search_audit")
              .ToHashSet(StringComparer.Ordinal);

          Assert.Empty(listed.Except(ListTools.Select(row => (string)row[0]!)));
      }

      /// <summary>Everything the eleven lists need at least one row of.</summary>
      /// <param name="AppointmentTypeId">The type the Manager and the workspace are scoped to.</param>
      /// <param name="LocationId">The site every seeded window belongs to.</param>
      /// <param name="AttendeeGroupId">The group the seeded attendee belongs to.</param>
      /// <param name="AttendeeId">The seeded attendee.</param>
      /// <param name="EventId">The confirmed event, ending inside the workspace window.</param>
      private sealed record SeededWorld(
          Guid AppointmentTypeId, Guid LocationId, Guid AttendeeGroupId, Guid AttendeeId,
          Guid EventId);

      /// <summary>
      /// Seeds one row for every list and signs in as the role that list needs. Admin is
      /// exclusive and holds no attendee data, so one identity cannot drive all eleven — the
      /// role is chosen per tool rather than once for the suite.
      /// </summary>
      /// <param name="tool">The tool about to be called.</param>
      /// <returns>The seeded identifiers.</returns>
      private async Task<SeededWorld> GivenSeededWorldAsync(string tool)
      {
          var world = await SeedAsync(tool);
          var roles = tool switch
          {
              "list_staff_access" => (IReadOnlyCollection<Role>)[Role.Admin],
              "list_event_proposals" or "list_workspace_events" => [Role.Manager],
              _ => [Role.Coordinator],
          };
          var scope = roles.Contains(Role.Manager) ? world.AppointmentTypeId : (Guid?)null;
          factory.SignedInAs = await factory.GivenStaffAsync(roles, scope);
          return world;
      }

      /// <summary>
      /// Writes the rows directly. Driving eight handlers to arrange eleven lists would make
      /// this suite a test of those handlers; what it is for is the shape of what comes back.
      /// </summary>
      /// <param name="tool">The tool, used only to keep each case's codes distinct.</param>
      /// <returns>The seeded identifiers.</returns>
      private async Task<SeededWorld> SeedAsync(string tool)
      {
          var prefix = "MCP_" + tool.ToUpperInvariant();
          using var scope = factory.Services.CreateScope();
          var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

          var type = AppointmentType.Create(Guid.NewGuid(), prefix + "_T", prefix + " type");
          var location = Location.Create(
              Guid.NewGuid(), prefix + "_L", prefix + " site", "1 Test Street", "Europe/London",
              ProposalFixture.Zones);
          var group = AttendeeGroup.Create(
              Guid.NewGuid(), prefix + "_G", prefix + " group", [type.Id], [type.Id]);
          context.AppointmentTypes.Add(type);
          context.Locations.Add(location);
          context.AttendeeGroups.Add(group);
          await context.SaveChangesAsync();

          var attendee = Attendee.Create(
              Guid.NewGuid(), "Test Attendee", $"{prefix.ToLowerInvariant()}@example.com",
              group, Now);
          context.Attendees.Add(attendee);

          // The workspace window is the end instant between seven days ago and fourteen ahead,
          // so a date three days out satisfies both it and the cancellable list's future bound.
          // The shared proposal fixture cannot be used here: it lists the three predecessor
          // types, and this world's proposing type is one it has never heard of.
          var window = new EventWindow(
              DateOnly.FromDateTime(Now.AddDays(3).UtcDateTime), new TimeOnly(9, 30), 240);
          // The SystemSettings default offers three options. Seed three distinct confirmed
          // events so Invite.CreateInitial exercises the same invariant as production.
          var eventItems = Enumerable.Range(0, 3).Select(offset =>
          {
              var proposal = EventProposal.Propose(
                  Guid.NewGuid(), location.Id, locationIsActive: true, location.TimeZoneId,
                  new EventWindow(window.Date.AddDays(offset), window.StartTime, window.DurationMinutes),
                  ProposalFixture.Zones, Now,
                  [new ProposableAppointmentType(type.Id, type.Code, true, true)],
                  type.Id, Guid.NewGuid(), headcount: 10);
              var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
              context.EventProposals.Add(proposal);
              context.Events.Add(eventItem);
              return eventItem;
          }).ToArray();

          var invite = Invite.CreateInitial(
              Guid.NewGuid(), attendee.Id, Now.AddDays(7), [location.Id], eventItems.Select(x => x.Id),
              [type.Id], retryCount: 0);
          context.Invites.Add(invite);
          context.Bookings.Add(Booking.Create(Guid.NewGuid(), invite, eventItems[0].Id, Now));

          context.AuditLogs.Add(AuditLog.Record(
              Guid.NewGuid(), AuditEntityTypes.Event, eventItems[0].Id, AuditAction.EventConfirmed,
              ActorType.System, null, Now, "{}"));
          await context.SaveChangesAsync();

          return new SeededWorld(
              type.Id, location.Id, group.Id, attendee.Id, eventItems[0].Id);
      }

      /// <summary>The arguments each list needs to answer with its seeded row.</summary>
      /// <param name="tool">The tool.</param>
      /// <param name="world">The seeded identifiers.</param>
      /// <returns>The tool arguments.</returns>
      private static object ArgumentsFor(string tool, SeededWorld world) => tool switch
      {
          "list_attendee_bookings" => new { id = world.AttendeeId },
          "list_attendees" => new { groupId = world.AttendeeGroupId, limit = 50 },
          "list_events" or "list_cancellable_events" =>
              new { locationId = world.LocationId, limit = 50 },
          "list_event_proposals" or "search_audit" => new { limit = 50 },
          "list_workspace_events" => new { locationId = world.LocationId },
          _ => new { includeInactive = true },
      };

      private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

      private static bool HasAny(JsonElement row, params string[] names) =>
          names.Any(name => row.TryGetProperty(name, out var value) &&
              value.ValueKind is not JsonValueKind.Null);

      /// <summary>Reads the row array out of a tool result, whichever content shape it used.</summary>
      private static IReadOnlyList<JsonElement> Rows(JsonElement result)
      {
          var payload = result.TryGetProperty("structuredContent", out var structured)
              ? structured
              : JsonDocument.Parse(
                  result.GetProperty("content")[0].GetProperty("text").GetString()!)
                  .RootElement.Clone();

          var array = payload.ValueKind == JsonValueKind.Array
              ? payload
              : payload.GetProperty("items");
          return [.. array.EnumerateArray()];
      }
  }
  ```

  The row-shape assertions name alternatives rather than one field because the eleven lists are
  genuinely different resources: an event row is identified by `eventId` and named by its
  location, an attendee row by `id` and their name, a workspace row by its appointment status.
  What the requirement asks is that an agent can always tell rows apart, name one to a human and
  see its state — not that every list uses one spelling.

  ```csharp
  // tests/EventBooking.Mcp.Tests/RefusalParityTests.cs (complete)
  using System.Text.Json;
  using EventBooking.Domain.Access;

  namespace EventBooking.Mcp.Tests;

  /// <summary>
  /// The two refusals master Task 23 names, proved to be the same refusal REST gives. Both come
  /// from the handler, which is the point: a tool with its own check would pass these and still
  /// diverge the moment the rule changed.
  /// </summary>
  [Collection("mcp")]
  public sealed class RefusalParityTests(McpFactory factory)
  {
      /// <summary>
      /// FR-10.7. A Manager profile with no appointment-type scope grants no capability at all,
      /// so every scoped tool refuses — and the tool itself has no scope check to have written.
      /// </summary>
      [Fact]
      public async Task AManagerWithANullScopeIsRefused()
      {
          factory.SignedInAs = await factory.GivenStaffAsync([Role.Manager], null);
          var client = new McpClient(factory.CreateClient());

          var envelope = await client.CallRawAsync("list_event_proposals", new { limit = 50 });

          Assert.Contains("forbidden", Failure(envelope), StringComparison.Ordinal);
      }

      /// <summary>
      /// The same file the REST import suite uses, refused the same way. A different file would
      /// make "identically" a claim about two unrelated inputs.
      /// </summary>
      [Fact]
      public async Task AnImportOverTheRowLimitIsRefusedAsItIsOverRest()
      {
          factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
          var client = new McpClient(factory.CreateClient());
          var csv = OversizedCsv();

          var envelope = await client.CallRawAsync("import_attendees", new { csv });

          Assert.Contains("validation", Failure(envelope), StringComparison.Ordinal);
      }

      /// <summary>An Admin gets no attendee data through MCP either (design 06's data gate).</summary>
      [Fact]
      public async Task AnAdminIsRefusedAttendeeData()
      {
          factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
          var client = new McpClient(factory.CreateClient());

          var envelope = await client.CallRawAsync("list_attendees", new { limit = 50 });

          Assert.Contains("forbidden", Failure(envelope), StringComparison.Ordinal);
      }

      /// <summary>A token with no staff number is refused here exactly as it is over REST.</summary>
      [Fact]
      public async Task AMissingStaffNumberIsRefused()
      {
          factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
          factory.StaffIdClaim = null;
          var client = new McpClient(factory.CreateClient());

          var envelope = await client.CallRawAsync("list_attendees", new { limit = 50 });

          Assert.False(string.IsNullOrWhiteSpace(Failure(envelope)));
      }

      /// <summary>
      /// A refusal reaches the agent either as a JSON-RPC error or as a tool result marked as
      /// an error; both carry the application's own message, and this reads whichever arrived.
      /// </summary>
      private static string Failure(JsonElement envelope)
      {
          if (envelope.TryGetProperty("error", out var error))
          {
              return error.GetProperty("message").GetString() ?? string.Empty;
          }

          var result = envelope.GetProperty("result");
          Assert.True(result.GetProperty("isError").GetBoolean());
          return result.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
      }

      /// <summary>One row more than design 08's bound of a thousand.</summary>
      private static string OversizedCsv()
      {
          var rows = new System.Text.StringBuilder("name,email,attendeeGroupCode\n");
          for (var index = 0; index < 1001; index++)
          {
              rows.Append($"Row {index},row{index}@example.com,MCP_BIG\n");
          }

          return rows.ToString();
      }
  }
  ```

  The import tool takes the CSV as text rather than as an upload, because MCP has no multipart
  transport. That is a difference in how the bytes arrive, not in what is accepted: the same
  handler, the same bound and the same refusal. The REST endpoint reads the file and hands the
  handler its text, and the tool hands the handler the text it was given.

- [ ] **Step 2: Run.** Expected: FAIL.

  The parity suite fails because the ported tool set is the predecessor's; the list-shape suite
  fails on tools that do not exist; and the refusal suite fails to compile against tool names
  nothing registers.

  ```bash
  dotnet test tests/EventBooking.Mcp.Tests
  ```

- [ ] **Step 3: Implement.** The description source first, then the eight tool classes.

  ```csharp
  // src/EventBooking.Mcp/Tools/McpErrors.cs (complete, replacing the ported file)
  using EventBooking.Application.Common;
  using ModelContextProtocol;

  namespace EventBooking.Mcp.Tools;

  /// <summary>Turns application failures into code-bearing MCP errors.</summary>
  internal static class McpErrors
  {
      internal static T ValueOrThrow<T>(this Result<T> result) =>
          result.IsSuccess ? result.Value : throw ToMcpException(result.Error);

      internal static async Task<T> ValueOrThrowAsync<T>(this Task<Result<T>> pending) =>
          (await pending).ValueOrThrow();

      internal static void ThrowIfFailure(this Result result)
      {
          if (result.IsFailure) throw ToMcpException(result.Error);
      }

      private static McpException ToMcpException(Error error) =>
          new($"{error.Code}: {error.Message}");
  }
  ```

  ```csharp
  // src/EventBooking.Mcp/Tools/ToolDescriptions.cs (complete)
  using EventBooking.Api.OpenApi;

  namespace EventBooking.Mcp.Tools;

  /// <summary>
  /// A tool's description, read from the shared catalogue by its tool name. Writing it into the
  /// attribute instead would let a tool describe itself differently from the route it mirrors,
  /// and nothing would notice until an agent acted on the difference.
  /// </summary>
  public static class ToolDescriptions
  {
      private static readonly IReadOnlyDictionary<string, AgentOperation> ByTool =
          AgentOperationCatalog.All.Values
              .Where(operation => operation.McpTool is not null)
              .ToDictionary(operation => operation.McpTool!, StringComparer.Ordinal);

      /// <summary>Gets every tool name the catalogue declares.</summary>
      public static IReadOnlySet<string> All { get; } =
          ByTool.Keys.ToHashSet(StringComparer.Ordinal);

      /// <summary>Returns the catalogued description for one tool.</summary>
      /// <param name="toolName">The snake-case tool name.</param>
      /// <returns>The description.</returns>
      /// <exception cref="InvalidOperationException">Thrown when the tool is not catalogued.</exception>
      public static string For(string toolName) =>
          ByTool.TryGetValue(toolName, out var operation)
              ? operation.Description
              : throw new InvalidOperationException(
                  $"Tool '{toolName}' is not in the operation catalogue. Add the route first: " +
                  "a tool with no route is a surface the OpenAPI document does not describe.");

      /// <summary>Returns the catalogued behaviour hints for one tool.</summary>
      /// <param name="toolName">The snake-case tool name.</param>
      /// <returns>The hints.</returns>
      public static AgentHints HintsFor(string toolName) =>
          ByTool.TryGetValue(toolName, out var operation)
              ? operation.Hints
              : throw new InvalidOperationException($"Tool '{toolName}' is not in the operation catalogue.");
  }
  ```

  The MCP SDK reads the tool's name and its four hint flags from the attribute at registration
  time, so those stay literal; the description is supplied at run time from this class. The
  parity test then compares every literal against the catalogue, which is what keeps the two in
  step without the attribute being able to read a constant.

  ```csharp
  // src/EventBooking.Mcp/Tools/IdentityTools.cs (complete)
  using System.ComponentModel;
  using EventBooking.Api.Auth;
  using EventBooking.Application.Access;
  using Microsoft.Extensions.Options;
  using ModelContextProtocol.Server;

  namespace EventBooking.Mcp.Tools;

  /// <summary>The signed-in staff member's own access.</summary>
  [McpServerToolType]
  public sealed class IdentityTools
  {
      /// <summary>Reads the caller's identity, roles, scope and capabilities.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The identity handler.</param>
      /// <param name="claims">The configured claim names and staff-number pattern.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The caller's access.</returns>
      [McpServerTool(
          Name = "get_my_access", Title = "Get my access",
          ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
      [Description("Read the signed-in staff identity, roles, appointment-type scope and granted capabilities.")]
      public async Task<StaffMeView> GetMyAccessAsync(
          ICallerAccessor caller,
          MeHandler handler,
          IOptions<AuthClaimOptions> claims,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              caller.RequireStaffUserId(), caller.StaffId?.Value, caller.DisplayName,
              caller.Roles, claims.Value.StaffIdPattern, cancellationToken);
          return result.ValueOrThrow();
      }
  }
  ```

  ```csharp
  // src/EventBooking.Mcp/Tools/ReferenceDataTools.cs (complete)
  using System.ComponentModel;
  using EventBooking.Api.Auth;
  using EventBooking.Application.ReferenceData;
  using ModelContextProtocol.Server;

  namespace EventBooking.Mcp.Tools;

  /// <summary>The nine Admin-managed reference-data tools.</summary>
  [McpServerToolType]
  public sealed class ReferenceDataTools
  {
      /// <summary>Lists locations.</summary>
      /// <param name="handler">The list handler.</param>
      /// <param name="includeInactive">Whether retired sites are included.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The locations.</returns>
      [McpServerTool(
          Name = "list_locations", Title = "List locations",
          ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
      [Description("List every location in code order, with its name and whether it is still in use.")]
      public async Task<IReadOnlyList<LocationListItem>> ListLocationsAsync(
          ListLocationsHandler handler,
          [Description("Include retired locations.")] bool includeInactive,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new ListLocationsQuery(includeInactive), cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Creates a location.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The create handler.</param>
      /// <param name="code">The canonical code.</param>
      /// <param name="name">The display name.</param>
      /// <param name="address">The postal address.</param>
      /// <param name="timeZoneId">The IANA zone.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The created location.</returns>
      [McpServerTool(
          Name = "create_location", Title = "Create location",
          ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
      [Description("Create a location. Caller must hold ManageReferenceData.")]
      public async Task<LocationResult> CreateLocationAsync(
          ICallerAccessor caller,
          CreateLocationHandler handler,
          [Description("Canonical uppercase snake-case code.")] string code,
          [Description("Display name.")] string name,
          [Description("Postal address.")] string address,
          [Description("IANA time-zone identifier, for example Europe/London.")] string timeZoneId,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new CreateLocationCommand(
                  caller.RequireStaffUserId(), code, name, address, timeZoneId),
              cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Updates a location.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The update handler.</param>
      /// <param name="locationId">The location to update.</param>
      /// <param name="name">The display name.</param>
      /// <param name="address">The postal address.</param>
      /// <param name="timeZoneId">The IANA zone.</param>
      /// <param name="isActive">Whether the location stays in use.</param>
      /// <param name="expectedVersion">The version the caller read.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The updated location.</returns>
      [McpServerTool(
          Name = "update_location", Title = "Update location",
          ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
      [Description("Update a location's name, address, zone and active flag in one call. Caller must hold ManageReferenceData.")]
      public async Task<LocationResult> UpdateLocationAsync(
          ICallerAccessor caller,
          UpdateLocationHandler handler,
          [Description("The location identifier.")] Guid locationId,
          [Description("Display name.")] string name,
          [Description("Postal address.")] string address,
          [Description("IANA time-zone identifier.")] string timeZoneId,
          [Description("Whether the location stays in use.")] bool isActive,
          [Description("The version you read.")] long expectedVersion,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new UpdateLocationCommand(
                  caller.RequireStaffUserId(), locationId, name, address, timeZoneId, isActive,
                  expectedVersion),
              cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Lists appointment types.</summary>
      /// <param name="handler">The list handler.</param>
      /// <param name="includeInactive">Whether retired types are included.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The appointment types.</returns>
      [McpServerTool(
          Name = "list_appointment_types", Title = "List appointment types",
          ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
      [Description("List every appointment type with its current Manager's display name.")]
      public async Task<IReadOnlyList<AppointmentTypeListItem>> ListAppointmentTypesAsync(
          ListAppointmentTypesHandler handler,
          [Description("Include retired types.")] bool includeInactive,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new ListAppointmentTypesQuery(includeInactive), cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Creates an appointment type.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The create handler.</param>
      /// <param name="code">The canonical code.</param>
      /// <param name="name">The display name.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The created type.</returns>
      [McpServerTool(
          Name = "create_appointment_type", Title = "Create appointment type",
          ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
      [Description("Create an appointment type. Caller must hold ManageReferenceData.")]
      public async Task<AppointmentTypeResult> CreateAppointmentTypeAsync(
          ICallerAccessor caller,
          CreateAppointmentTypeHandler handler,
          [Description("Canonical uppercase snake-case code.")] string code,
          [Description("Display name.")] string name,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new CreateAppointmentTypeCommand(caller.RequireStaffUserId(), code, name),
              cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Updates an appointment type.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The update handler.</param>
      /// <param name="appointmentTypeId">The type to update.</param>
      /// <param name="name">The display name.</param>
      /// <param name="isActive">Whether the type stays in use.</param>
      /// <param name="expectedVersion">The version the caller read.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The updated type.</returns>
      [McpServerTool(
          Name = "update_appointment_type", Title = "Update appointment type",
          ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
      [Description("Update an appointment type's name and active flag. Caller must hold ManageReferenceData.")]
      public async Task<AppointmentTypeResult> UpdateAppointmentTypeAsync(
          ICallerAccessor caller,
          UpdateAppointmentTypeHandler handler,
          [Description("The appointment type identifier.")] Guid appointmentTypeId,
          [Description("Display name.")] string name,
          [Description("Whether the type stays in use.")] bool isActive,
          [Description("The version you read.")] long expectedVersion,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new UpdateAppointmentTypeCommand(
                  caller.RequireStaffUserId(), appointmentTypeId, name, isActive, expectedVersion),
              cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Lists attendee groups.</summary>
      /// <param name="handler">The list handler.</param>
      /// <param name="includeInactive">Whether retired groups are included.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The attendee groups.</returns>
      [McpServerTool(
          Name = "list_attendee_groups", Title = "List attendee groups",
          ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
      [Description("List every attendee group with its requirement type ids and member count.")]
      public async Task<IReadOnlyList<AttendeeGroupListItem>> ListAttendeeGroupsAsync(
          ListAttendeeGroupsHandler handler,
          [Description("Include retired groups.")] bool includeInactive,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new ListAttendeeGroupsQuery(includeInactive), cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Creates an attendee group.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The create handler.</param>
      /// <param name="code">The canonical code.</param>
      /// <param name="name">The display name.</param>
      /// <param name="appointmentTypeIds">The types its members require.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The created group.</returns>
      [McpServerTool(
          Name = "create_attendee_group", Title = "Create attendee group",
          ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
      [Description("Create an attendee group mapping at least one active appointment type. Caller must hold ManageReferenceData.")]
      public async Task<AttendeeGroupResult> CreateAttendeeGroupAsync(
          ICallerAccessor caller,
          CreateAttendeeGroupHandler handler,
          [Description("Canonical uppercase snake-case code.")] string code,
          [Description("Display name.")] string name,
          [Description("Appointment types every member requires.")] IReadOnlyList<Guid> appointmentTypeIds,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new CreateAttendeeGroupCommand(
                  caller.RequireStaffUserId(), code, name, appointmentTypeIds),
              cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Updates an attendee group.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The update handler.</param>
      /// <param name="attendeeGroupId">The group to update.</param>
      /// <param name="name">The display name.</param>
      /// <param name="appointmentTypeIds">The replacement mapping, or null to leave it alone.</param>
      /// <param name="isActive">Whether the group stays in use.</param>
      /// <param name="expectedVersion">The version the caller read.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The updated group.</returns>
      [McpServerTool(
          Name = "update_attendee_group", Title = "Update attendee group",
          ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
      [Description("Update a group's name, requirement mapping and active flag. A mapping change re-derives every member's requirements and is refused while any member holds an active original booking.")]
      public async Task<AttendeeGroupResult> UpdateAttendeeGroupAsync(
          ICallerAccessor caller,
          UpdateAttendeeGroupHandler handler,
          [Description("The attendee group identifier.")] Guid attendeeGroupId,
          [Description("Display name.")] string name,
          [Description("Replacement requirement mapping, or omit to leave it unchanged.")]
          IReadOnlyList<Guid>? appointmentTypeIds,
          [Description("Whether the group stays in use.")] bool isActive,
          [Description("The version you read.")] long expectedVersion,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new UpdateAttendeeGroupCommand(
                  caller.RequireStaffUserId(), attendeeGroupId, name, appointmentTypeIds,
                  isActive, expectedVersion),
              cancellationToken);
          return result.ValueOrThrow();
      }
  }
  ```

  Every tool in this file is the same four lines: bind, call, throw-or-unwrap, return. The handler
  makes the capability decision, so the tool has nothing to get wrong — which is the property
  the refusal suite is checking, and the reason the remaining seven classes below are written to
  exactly this shape.

  ```csharp
  // src/EventBooking.Mcp/Tools/AdministrationTools.cs (complete)
  using System.ComponentModel;
  using EventBooking.Api.Auth;
  using EventBooking.Application.Access;
  using EventBooking.Application.Settings;
  using ModelContextProtocol.Server;

  namespace EventBooking.Mcp.Tools;

  /// <summary>Settings and staff-access scope. There is deliberately no role edit (FR-10.5).</summary>
  [McpServerToolType]
  public sealed class AdministrationTools
  {
      /// <summary>Reads the settings row.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The settings handler.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The settings.</returns>
      [McpServerTool(
          Name = "get_settings", Title = "Get settings",
          ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
      [Description("Read the invite expiry, automatic retry count and option count. Caller must hold ManageSettings.")]
      public async Task<SettingsView> GetSettingsAsync(
          ICallerAccessor caller,
          AdminSettingsHandler handler,
          CancellationToken cancellationToken)
      {
          var result = await handler.GetAsync(caller.RequireStaffUserId(), cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Updates the settings row.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The save handler.</param>
      /// <param name="inviteExpiryDays">Days an invitation stays usable.</param>
      /// <param name="maxAutoRetryCount">Automatic re-issues before giving up.</param>
      /// <param name="inviteOptionCount">Options offered per invitation.</param>
      /// <param name="expectedVersion">The version the caller read.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The updated settings.</returns>
      [McpServerTool(
          Name = "update_settings", Title = "Update settings",
          ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
      [Description("Update the settings. Existing invitations keep the values they were issued under. Caller must hold ManageSettings.")]
      public async Task<SystemSettingsResult> UpdateSettingsAsync(
          ICallerAccessor caller,
          SaveSystemSettingsHandler handler,
          [Description("Days an invitation stays usable, 1 to 60.")] int inviteExpiryDays,
          [Description("Automatic re-issues before giving up, 0 to 10.")] int maxAutoRetryCount,
          [Description("Options offered per invitation, 1 to 5.")] int inviteOptionCount,
          [Description("The version you read.")] long expectedVersion,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new SaveSystemSettingsCommand(
                  caller.RequireStaffUserId(), inviteExpiryDays, maxAutoRetryCount,
                  inviteOptionCount, expectedVersion),
              cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Lists staff access profiles.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The staff access handler.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The profiles.</returns>
      [McpServerTool(
          Name = "list_staff_access", Title = "List staff access",
          ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
      [Description("List every staff access profile with its display name, staff number, read-only roles and scope. Caller must hold ManageStaffAccess.")]
      public async Task<IReadOnlyList<StaffAccessProfileView>> ListStaffAccessAsync(
          ICallerAccessor caller,
          StaffAccessHandler handler,
          CancellationToken cancellationToken)
      {
          var result = await handler.ListAsync(caller.RequireStaffUserId(), cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Sets or clears one profile's appointment-type scope.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The scope handler.</param>
      /// <param name="targetStaffUserId">The profile to change.</param>
      /// <param name="appointmentTypeId">The type to scope to, or null to clear.</param>
      /// <param name="expectedVersion">The version the caller read.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The outcome, naming any displaced Manager.</returns>
      [McpServerTool(
          Name = "set_staff_access_scope", Title = "Set staff access scope",
          ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
      [Description("Set or clear a staff profile's appointment-type scope. Assigning a type another Manager holds displaces them, and the result names who. Caller must hold ManageStaffAccess.")]
      public async Task<SetStaffScopeOutcome> SetStaffAccessScopeAsync(
          ICallerAccessor caller,
          StaffScopeHandler handler,
          [Description("The staff profile to change.")] Guid targetStaffUserId,
          [Description("The appointment type to scope to, or omit to clear the scope.")]
          Guid? appointmentTypeId,
          [Description("The version you read.")] long expectedVersion,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new SetStaffScopeCommand(
                  caller.RequireStaffUserId(), targetStaffUserId, appointmentTypeId,
                  expectedVersion),
              cancellationToken);
          return result.ValueOrThrow();
      }
  }
  ```

  ```csharp
  // src/EventBooking.Mcp/Tools/NegotiationTools.cs (complete)
  using System.ComponentModel;
  using EventBooking.Api.Auth;
  using EventBooking.Application.Negotiation;
  using ModelContextProtocol.Server;

  namespace EventBooking.Mcp.Tools;

  /// <summary>The five proposal tools. Every one is scoped to the caller's own type.</summary>
  [McpServerToolType]
  public sealed class NegotiationTools
  {
      /// <summary>Lists the caller's type's proposals.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The list handler.</param>
      /// <param name="status">The status filter, or null for every status.</param>
      /// <param name="locationId">The location filter, or null for every site.</param>
      /// <param name="cursor">The page cursor, or null for the first page.</param>
      /// <param name="limit">The page size.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>One page of proposals.</returns>
      [McpServerTool(
          Name = "list_event_proposals", Title = "List event proposals",
          ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
      [Description("List the proposals listing your own appointment type, filtered by status and location. Caller must hold ManageEventNegotiation.")]
      public async Task<EventProposalListView> ListEventProposalsAsync(
          ICallerAccessor caller,
          ListEventProposalsHandler handler,
          [Description("Open, Confirmed or Withdrawn; omit for every status.")] string? status,
          [Description("Narrow to one location.")] Guid? locationId,
          [Description("The nextCursor from the previous page.")] string? cursor,
          [Description("Page size, 1 to 200.")] int limit,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new ListEventProposalsQuery(
                  caller.RequireStaffUserId(), status, locationId, cursor, limit),
              cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Proposes an event.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The propose handler.</param>
      /// <param name="locationId">Where the event would be held.</param>
      /// <param name="date">The local calendar date, yyyy-MM-dd.</param>
      /// <param name="startTime">The local start time, HH:mm.</param>
      /// <param name="durationMinutes">The window length.</param>
      /// <param name="appointmentTypeIds">Every type the event will offer.</param>
      /// <param name="headcount">The proposer's own headcount.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The proposal, confirmed already when it lists only your type.</returns>
      [McpServerTool(
          Name = "propose_event", Title = "Propose event",
          ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
      [Description("Propose an event at one location for a set of appointment types. A proposal listing only your own type is confirmed immediately. Caller must hold ManageEventNegotiation.")]
      public async Task<ProposeEventOutcome> ProposeEventAsync(
          ICallerAccessor caller,
          ProposeEventHandler handler,
          [Description("The location identifier.")] Guid locationId,
          [Description("Local calendar date, yyyy-MM-dd.")] string date,
          [Description("Local start time, HH:mm.")] string startTime,
          [Description("Window length in minutes; a multiple of 15, at most 720.")] int durationMinutes,
          [Description("Every appointment type the event will offer, including your own.")]
          IReadOnlyList<Guid> appointmentTypeIds,
          [Description("Your own type's headcount, 1 to 1000.")] int headcount,
          CancellationToken cancellationToken)
      {
          if (!DateOnly.TryParse(date, out var parsedDate) ||
              !TimeOnly.TryParse(startTime, out var parsedStart))
          {
              throw new ModelContextProtocol.McpException(
                  "A yyyy-MM-dd date and an HH:mm startTime are required.");
          }

          var result = await handler.HandleAsync(
              new ProposeEventCommand(
                  caller.RequireStaffUserId(), locationId, parsedDate, parsedStart,
                  durationMinutes, appointmentTypeIds, headcount),
              cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Records or revises the caller's type's acceptance.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The acceptance handler.</param>
      /// <param name="proposalId">The proposal.</param>
      /// <param name="headcount">The accepting type's headcount.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The outcome, carrying the event once the last acceptance lands.</returns>
      [McpServerTool(
          Name = "record_acceptance", Title = "Record acceptance",
          ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
      [Description("Accept a proposal with your type's headcount, or revise it while the proposal is open. The last missing acceptance confirms the event. Caller must hold ManageEventNegotiation.")]
      public async Task<RecordAcceptanceOutcome> RecordAcceptanceAsync(
          ICallerAccessor caller,
          RecordAcceptanceHandler handler,
          [Description("The proposal identifier.")] Guid proposalId,
          [Description("Your type's headcount, 1 to 1000.")] int headcount,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new RecordAcceptanceCommand(caller.RequireStaffUserId(), proposalId, headcount),
              cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Withdraws the caller's type's acceptance.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The withdrawal handler.</param>
      /// <param name="proposalId">The proposal.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>A confirmation message.</returns>
      [McpServerTool(
          Name = "withdraw_acceptance", Title = "Withdraw acceptance",
          ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
      [Description("Withdraw your own type's acceptance while the proposal is open. Caller must hold ManageEventNegotiation.")]
      public async Task<string> WithdrawAcceptanceAsync(
          ICallerAccessor caller,
          WithdrawAcceptanceHandler handler,
          [Description("The proposal identifier.")] Guid proposalId,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new WithdrawAcceptanceCommand(caller.RequireStaffUserId(), proposalId),
              cancellationToken);
          result.ThrowIfFailure();
          return "Acceptance withdrawn.";
      }

      /// <summary>Withdraws the whole proposal.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The withdrawal handler.</param>
      /// <param name="proposalId">The proposal.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>A confirmation message.</returns>
      [McpServerTool(
          Name = "withdraw_proposal", Title = "Withdraw proposal",
          ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
      [Description("Withdraw a proposal your own appointment type raised. Judged against the type, so you inherit it from a predecessor. Caller must hold ManageEventNegotiation.")]
      public async Task<string> WithdrawProposalAsync(
          ICallerAccessor caller,
          WithdrawProposalHandler handler,
          [Description("The proposal identifier.")] Guid proposalId,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new WithdrawProposalCommand(caller.RequireStaffUserId(), proposalId),
              cancellationToken);
          result.ThrowIfFailure();
          return "Proposal withdrawn.";
      }
  }
  ```

  ```csharp
  // src/EventBooking.Mcp/Tools/EventTools.cs (complete, replacing the ported file)
  using System.ComponentModel;
  using EventBooking.Api.Auth;
  using EventBooking.Application.Events;
  using EventBooking.Application.Negotiation;
  using ModelContextProtocol.Server;

  namespace EventBooking.Mcp.Tools;

  /// <summary>The five event tools.</summary>
  [McpServerToolType]
  public sealed class EventTools
  {
      /// <summary>Lists events.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The list handler.</param>
      /// <param name="locationId">The location filter.</param>
      /// <param name="from">The earliest local date.</param>
      /// <param name="to">The latest local date.</param>
      /// <param name="appointmentTypeId">The listed-type filter.</param>
      /// <param name="cursor">The page cursor.</param>
      /// <param name="limit">The page size.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>One page of events.</returns>
      [McpServerTool(
          Name = "list_events", Title = "List events",
          ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
      [Description("List events by location, date range and appointment type. A Manager sees their own type's capacity; an Admin or Coordinator sees every type.")]
      public async Task<EventListView> ListEventsAsync(
          ICallerAccessor caller,
          ListEventsHandler handler,
          [Description("Narrow to one location.")] Guid? locationId,
          [Description("Earliest local date, yyyy-MM-dd.")] string? from,
          [Description("Latest local date, yyyy-MM-dd.")] string? to,
          [Description("Keep only events listing this appointment type.")] Guid? appointmentTypeId,
          [Description("The nextCursor from the previous page.")] string? cursor,
          [Description("Page size, 1 to 200.")] int limit,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new ListEventsQuery(
                  caller.RequireStaffUserId(), locationId, ParseDate(from, nameof(from)),
                  ParseDate(to, nameof(to)), appointmentTypeId, cursor, limit),
              cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Reads one event.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The read handler.</param>
      /// <param name="eventId">The event.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The event.</returns>
      [McpServerTool(
          Name = "get_event", Title = "Get event",
          ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
      [Description("Read one event with its capacities, filtered the same way the list is. An event outside your scope reads as not found.")]
      public async Task<EventView> GetEventAsync(
          ICallerAccessor caller,
          GetEventHandler handler,
          [Description("The event identifier.")] Guid eventId,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new GetEventQuery(caller.RequireStaffUserId(), eventId), cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Lists the events a cancellation can still reach.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The list handler.</param>
      /// <param name="locationId">The location filter.</param>
      /// <param name="from">The earliest local date.</param>
      /// <param name="to">The latest local date.</param>
      /// <param name="cursor">The page cursor.</param>
      /// <param name="limit">The page size.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>One page of cancellable events.</returns>
      [McpServerTool(
          Name = "list_cancellable_events", Title = "List cancellable events",
          ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
      [Description("List the active events whose window has not started, which are the ones a cancellation can still reach. Caller must hold ViewEventOperations.")]
      public async Task<EventListView> ListCancellableEventsAsync(
          ICallerAccessor caller,
          ListCancellableEventsHandler handler,
          [Description("Narrow to one location.")] Guid? locationId,
          [Description("Earliest local date, yyyy-MM-dd.")] string? from,
          [Description("Latest local date, yyyy-MM-dd.")] string? to,
          [Description("The nextCursor from the previous page.")] string? cursor,
          [Description("Page size, 1 to 200.")] int limit,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new ListCancellableEventsQuery(
                  caller.RequireStaffUserId(), locationId, ParseDate(from, nameof(from)),
                  ParseDate(to, nameof(to)), cursor, limit),
              cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Adjusts one appointment type's capacity on one event.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The capacity handler.</param>
      /// <param name="eventId">The event.</param>
      /// <param name="appointmentTypeId">The type, which must be the caller's own.</param>
      /// <param name="totalHeadcount">The new total.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The adjusted capacity.</returns>
      [McpServerTool(
          Name = "adjust_event_capacity", Title = "Adjust event capacity",
          ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
      [Description("Replace the total headcount for one appointment type on an active event. The type must be your own, and the total must still cover every active booking. Caller must hold ManageEventNegotiation.")]
      public async Task<AdjustEventCapacityOutcome> AdjustEventCapacityAsync(
          ICallerAccessor caller,
          AdjustEventCapacityHandler handler,
          [Description("The event identifier.")] Guid eventId,
          [Description("Your own appointment type's identifier.")] Guid appointmentTypeId,
          [Description("The new total headcount, 1 to 1000.")] int totalHeadcount,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new AdjustEventCapacityCommand(
                  caller.RequireStaffUserId(), eventId, appointmentTypeId, totalHeadcount),
              cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Cancels an event, two-step.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The cancellation handler.</param>
      /// <param name="eventId">The event.</param>
      /// <param name="confirm">Whether the bookings may be voided.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The cancellation outcome.</returns>
      [McpServerTool(
          Name = "cancel_event", Title = "Cancel event",
          ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
      [Description("Cancel an event, voiding its bookings and re-inviting the affected attendees. Call once with confirm false to see the consequence and change nothing, then again with confirm true. Caller must hold CancelEvent.")]
      public async Task<CancelEventOutcome> CancelEventAsync(
          ICallerAccessor caller,
          CancelEventHandler handler,
          [Description("The event identifier.")] Guid eventId,
          [Description("Set true to carry the cancellation out.")] bool confirm,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new CancelEventCommand(caller.RequireStaffUserId(), eventId, confirm),
              cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Parses an optional local date, refusing a malformed one.</summary>
      /// <param name="value">The supplied text.</param>
      /// <param name="parameter">The parameter name, for the message.</param>
      /// <returns>The parsed date, or null.</returns>
      private static DateOnly? ParseDate(string? value, string parameter)
      {
          if (string.IsNullOrWhiteSpace(value))
          {
              return null;
          }

          return DateOnly.TryParse(value, out var parsed)
              ? parsed
              : throw new ModelContextProtocol.McpException($"{parameter} must be yyyy-MM-dd.");
      }
  }
  ```

  Dates arrive as text because an agent composing a call has a string, and a malformed one is
  refused here rather than silently ignored. That is the one piece of parsing a tool does that
  its endpoint does not — the endpoint gets the same work from model binding, which is a
  difference in transport, not in behaviour.

  ```csharp
  // src/EventBooking.Mcp/Tools/AttendeeTools.cs — the thirteen tools, replacing the ported
  // file's body. Every one is the same shape as the reference-data tools; the four with
  // something to say are written out, and the nine plain pass-throughs bind their command and
  // return ValueOrThrow exactly as ListLocationsAsync does above.
  [McpServerTool(
      Name = "list_attendees", Title = "List attendees",
      ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
  [Description("List attendees by status, group, readiness and a name or email prefix, one page at a time, with each row's latest delivery status. Caller must hold ManageAttendees; an Admin is refused.")]
  public async Task<AttendeeListView> ListAttendeesAsync(
      ICallerAccessor caller,
      ListAttendeesHandler handler,
      [Description("Attendee status to keep.")] string? status,
      [Description("Attendee group to keep.")] Guid? groupId,
      [Description("Readiness code to keep.")] string? readiness,
      [Description("Name or email prefix.")] string? search,
      [Description("The nextCursor from the previous page.")] string? cursor,
      [Description("Page size, 1 to 200.")] int limit,
      CancellationToken cancellationToken)
  {
      var result = await handler.HandleAsync(
          new ListAttendeesQuery(
              caller.RequireStaffUserId(), cursor, limit, status, groupId, readiness, search),
          cancellationToken);
      return result.ValueOrThrow();
  }

  // The CSV arrives as text: MCP has no multipart transport. The bound, the parsing and the
  // all-or-nothing refusal are the handler's, so an oversized file is refused here exactly as
  // it is over REST — which is what the refusal suite asserts.
  [McpServerTool(
      Name = "import_attendees", Title = "Import attendees",
      ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
  [Description("Import attendees from CSV text with a header row, all or nothing. At most 1000 rows. Caller must hold ManageAttendees.")]
  public async Task<AttendeeImportOutcome> ImportAttendeesAsync(
      ICallerAccessor caller,
      ImportAttendeesHandler handler,
      [Description("The CSV text, including its header row.")] string csv,
      CancellationToken cancellationToken)
  {
      var result = await handler.HandleAsync(
          new ImportAttendeesCommand(caller.RequireStaffUserId(), csv), cancellationToken);
      return result.ValueOrThrow();
  }

  [McpServerTool(
      Name = "delete_attendee", Title = "Delete attendee",
      ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
  [Description("Delete an attendee and their bookings. Call once with confirm false to see the consequence and change nothing, then again with confirm true. Caller must hold ManageAttendees.")]
  public async Task<string> DeleteAttendeeAsync(
      ICallerAccessor caller,
      DeleteAttendeeHandler handler,
      [Description("The attendee identifier.")] Guid attendeeId,
      [Description("Set true to carry the deletion out.")] bool confirm,
      CancellationToken cancellationToken)
  {
      var result = await handler.HandleAsync(
          new DeleteAttendeeCommand(caller.RequireStaffUserId(), attendeeId, confirm),
          cancellationToken);
      result.ThrowIfFailure();
      return "Attendee deleted.";
  }

  // The coordinator's cancellation reports its consequence as a successful outcome rather than
  // as a refusal, so this tool returns it as data. An agent reads ConfirmationRequired and
  // calls again; the REST endpoint renders the same value as a 409 instead. Same handler, same
  // decision, two renderings.
  [McpServerTool(
      Name = "cancel_attendee_booking", Title = "Cancel attendee booking",
      ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
  [Description("Cancel one booking on an attendee's behalf. Call once with confirm false to read the consequence, then again with confirm true. Caller must hold ManageAttendees.")]
  public async Task<CoordinatorCancelOutcome> CancelAttendeeBookingAsync(
      ICallerAccessor caller,
      CancelBookingByCoordinatorHandler handler,
      [Description("The attendee identifier.")] Guid attendeeId,
      [Description("The booking identifier.")] Guid bookingId,
      [Description("Set true to carry the cancellation out.")] bool confirm,
      CancellationToken cancellationToken)
  {
      var result = await handler.HandleAsync(
          new CancelBookingByCoordinatorCommand(
              caller.RequireStaffUserId(), attendeeId, bookingId, confirm),
          cancellationToken);
      return result.ValueOrThrow();
  }
  ```

  ```csharp
  // The remaining attendee tools are explicit so the registered 45-tool surface can be replayed
  // without deriving calls from prose. Attribute descriptions are resolved against ToolDescriptions
  // during startup in the same way as the methods above.
  [McpServerTool(Name = "create_attendee", Title = "Create attendee", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
  public async Task<Guid> CreateAttendeeAsync(ICallerAccessor caller, SaveAttendeeHandler handler,
      string name, string email, Guid attendeeGroupId, CancellationToken cancellationToken) =>
      await handler.CreateAsync(new CreateAttendeeCommand(caller.RequireStaffUserId(), name, email, attendeeGroupId), cancellationToken).ValueOrThrowAsync();

  [McpServerTool(Name = "update_attendee", Title = "Update attendee", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
  public async Task<object> UpdateAttendeeAsync(ICallerAccessor caller, SaveAttendeeHandler handler,
      Guid attendeeId, string name, string email, Guid attendeeGroupId, CancellationToken cancellationToken) =>
      await handler.UpdateAsync(new UpdateAttendeeCommand(caller.RequireStaffUserId(), attendeeId, name, email, attendeeGroupId), cancellationToken).ValueOrThrowAsync();

  [McpServerTool(Name = "count_eligible_events", Title = "Count eligible events", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
  public async Task<object> CountEligibleEventsAsync(ICallerAccessor caller, CountEligibleEventsHandler handler,
      Guid attendeeId, IReadOnlyList<Guid> locationIds, CancellationToken cancellationToken) =>
      await handler.HandleAsync(new CountEligibleEventsQuery(caller.RequireStaffUserId(), attendeeId, locationIds), cancellationToken).ValueOrThrowAsync();

  [McpServerTool(Name = "invite_attendee", Title = "Invite attendee", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
  public async Task<object> InviteAttendeeAsync(ICallerAccessor caller, InviteAttendeeHandler handler,
      Guid attendeeId, IReadOnlyList<Guid> locationIds, CancellationToken cancellationToken) =>
      await handler.HandleAsync(new InviteAttendeeCommand(caller.RequireStaffUserId(), attendeeId, locationIds), cancellationToken).ValueOrThrowAsync();

  [McpServerTool(Name = "start_recovery_invite", Title = "Start recovery invite", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
  public async Task<object> StartRecoveryInviteAsync(ICallerAccessor caller, StartRecoveryHandler handler,
      Guid attendeeId, IReadOnlyList<Guid> additionalLocationIds, CancellationToken cancellationToken) =>
      await handler.HandleAsync(new StartRecoveryCommand(caller.RequireStaffUserId(), attendeeId, additionalLocationIds), cancellationToken).ValueOrThrowAsync();

  [McpServerTool(Name = "cancel_recovery_invite", Title = "Cancel recovery invite", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
  public async Task CancelRecoveryInviteAsync(ICallerAccessor caller, CancelRecoveryInviteHandler handler,
      Guid inviteId, CancellationToken cancellationToken)
  {
      (await handler.HandleAsync(new CancelRecoveryInviteCommand(caller.RequireStaffUserId(), inviteId), cancellationToken)).ThrowIfFailure();
  }

  [McpServerTool(Name = "list_attendee_bookings", Title = "List attendee bookings", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
  public async Task<object> ListAttendeeBookingsAsync(ICallerAccessor caller, GetAttendeeBookingsHandler handler,
      Guid attendeeId, CancellationToken cancellationToken) =>
      await handler.HandleAsync(new GetAttendeeBookingsQuery(caller.RequireStaffUserId(), attendeeId), cancellationToken).ValueOrThrowAsync();

  [McpServerTool(Name = "retry_attendee_email", Title = "Retry attendee email", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
  public async Task<object> RetryAttendeeEmailAsync(ICallerAccessor caller, RetryEmailHandler handler,
      Guid attendeeId, CancellationToken cancellationToken) =>
      await handler.HandleAsync(new RetryNewestEmailCommand(caller.RequireStaffUserId(), attendeeId), cancellationToken).ValueOrThrowAsync();

  [McpServerTool(Name = "get_attendee_readiness", Title = "Get attendee readiness", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
  public async Task<object> GetAttendeeReadinessAsync(ICallerAccessor caller, GetAttendeeReadinessHandler handler,
      Guid attendeeId, CancellationToken cancellationToken) =>
      await handler.HandleAsync(new GetAttendeeReadinessQuery(caller.RequireStaffUserId(), attendeeId), cancellationToken).ValueOrThrowAsync();
  ```


  ```csharp
  // src/EventBooking.Mcp/Tools/DashboardTools.cs (complete)
  using System.ComponentModel;
  using EventBooking.Api.Auth;
  using EventBooking.Application.Dashboards;
  using ModelContextProtocol.Server;

  namespace EventBooking.Mcp.Tools;

  /// <summary>The dashboards tool.</summary>
  [McpServerToolType]
  public sealed class DashboardTools
  {
      /// <summary>Reads the three dashboard tabs and their counts.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The dashboards handler.</param>
      /// <param name="locationId">The location filter, which narrows the events tab only.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The dashboards.</returns>
      [McpServerTool(
          Name = "get_dashboards", Title = "Get dashboards",
          ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
      [Description("Read the awaiting-availability, no-response and events tabs with their counts. The location filter narrows the events tab only. Caller must hold ViewAttendeeDashboards.")]
      public async Task<DashboardsView> GetDashboardsAsync(
          ICallerAccessor caller,
          GetDashboardsHandler handler,
          [Description("Narrow the events tab to one location.")] Guid? locationId,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new GetDashboardsQuery(caller.RequireStaffUserId(), locationId), cancellationToken);
          return result.ValueOrThrow();
      }
  }
  ```

  ```csharp
  // src/EventBooking.Mcp/Tools/AuditTools.cs (complete)
  using System.ComponentModel;
  using EventBooking.Api.Auth;
  using EventBooking.Application.Audit;
  using ModelContextProtocol.Server;

  namespace EventBooking.Mcp.Tools;

  /// <summary>The three audit tools. All three are bucket-scoped by the handler.</summary>
  [McpServerToolType]
  public sealed class AuditTools
  {
      /// <summary>Searches the audit log.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The search handler.</param>
      /// <param name="entityType">The entity type filter.</param>
      /// <param name="action">The action filter.</param>
      /// <param name="entityId">The entity filter.</param>
      /// <param name="from">The earliest instant.</param>
      /// <param name="to">The latest instant.</param>
      /// <param name="cursor">The page cursor.</param>
      /// <param name="limit">The page size.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>One page of entries.</returns>
      [McpServerTool(
          Name = "search_audit", Title = "Search audit",
          ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
      [Description("Search audit entries, scoped to the buckets your capabilities reach: a caller holding only ViewEventAudit never sees an attendee row. Entries carry identifiers and codes, never names or email addresses.")]
      public async Task<AuditSearchView> SearchAuditAsync(
          ICallerAccessor caller,
          SearchAuditHandler handler,
          [Description("Entity type to keep.")] string? entityType,
          [Description("Audit action to keep.")] string? action,
          [Description("Entity identifier to keep.")] Guid? entityId,
          [Description("Earliest instant, ISO 8601.")] DateTimeOffset? from,
          [Description("Latest instant, ISO 8601.")] DateTimeOffset? to,
          [Description("The nextCursor from the previous page.")] string? cursor,
          [Description("Page size, 1 to 200.")] int limit,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new SearchAuditQuery(
                  caller.RequireStaffUserId(), cursor, limit, entityType, action, from, to,
                  entityId),
              cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Reads one attendee's history.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The history handler.</param>
      /// <param name="attendeeId">The attendee.</param>
      /// <param name="cursor">The page cursor.</param>
      /// <param name="limit">The page size.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>One page of entries.</returns>
      [McpServerTool(
          Name = "get_attendee_audit_history", Title = "Get attendee audit history",
          ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
      [Description("Read one attendee's audit history. Caller must hold ViewAttendeeAudit.")]
      public async Task<AuditSearchView> GetAttendeeAuditHistoryAsync(
          ICallerAccessor caller,
          AttendeeHistoryHandler handler,
          [Description("The attendee identifier.")] Guid attendeeId,
          [Description("The nextCursor from the previous page.")] string? cursor,
          [Description("Page size, 1 to 200.")] int limit,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new GetAttendeeHistoryQuery(
                  caller.RequireStaffUserId(), attendeeId, cursor, limit),
              cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Reads one event's history.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The history handler.</param>
      /// <param name="eventId">The event.</param>
      /// <param name="cursor">The page cursor.</param>
      /// <param name="limit">The page size.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>One page of entries.</returns>
      [McpServerTool(
          Name = "get_event_audit_history", Title = "Get event audit history",
          ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
      [Description("Read one event's audit history, including its proposal. Caller must hold ViewEventAudit.")]
      public async Task<AuditSearchView> GetEventAuditHistoryAsync(
          ICallerAccessor caller,
          EventHistoryHandler handler,
          [Description("The event identifier.")] Guid eventId,
          [Description("The nextCursor from the previous page.")] string? cursor,
          [Description("Page size, 1 to 200.")] int limit,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new GetEventHistoryQuery(caller.RequireStaffUserId(), eventId, cursor, limit),
              cancellationToken);
          return result.ValueOrThrow();
      }
  }
  ```

  ```csharp
  // src/EventBooking.Mcp/Tools/WorkspaceTools.cs (complete)
  using System.ComponentModel;
  using EventBooking.Api.Auth;
  using EventBooking.Application.Appointments;
  using ModelContextProtocol.Server;

  namespace EventBooking.Mcp.Tools;

  /// <summary>The four appointment-workspace tools.</summary>
  [McpServerToolType]
  public sealed class WorkspaceTools
  {
      /// <summary>Lists the events the caller's type is conducting.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The list handler.</param>
      /// <param name="locationId">The location filter.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The events.</returns>
      [McpServerTool(
          Name = "list_workspace_events", Title = "List workspace events",
          ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
      [Description("List the events listing your appointment type and ending between seven days ago and fourteen days ahead. Caller must hold ConductAppointments.")]
      public async Task<IReadOnlyList<WorkspaceEventView>> ListWorkspaceEventsAsync(
          ICallerAccessor caller,
          ListWorkspaceEventsHandler handler,
          [Description("Narrow to one location.")] Guid? locationId,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new ListWorkspaceEventsQuery(caller.RequireStaffUserId(), locationId),
              cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Reads one event's roster.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The roster handler.</param>
      /// <param name="eventId">The event.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The roster rows.</returns>
      [McpServerTool(
          Name = "get_workspace_roster", Title = "Get workspace roster",
          ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
      [Description("Read the minimum-data roster for one event: name, email, your own type's appointment status, and nothing else. Caller must hold ConductAppointments.")]
      public async Task<IReadOnlyList<WorkspaceRosterRow>> GetWorkspaceRosterAsync(
          ICallerAccessor caller,
          GetWorkspaceRosterHandler handler,
          [Description("The event identifier.")] Guid eventId,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new GetWorkspaceRosterQuery(caller.RequireStaffUserId(), eventId),
              cancellationToken);
          return result.ValueOrThrow();
      }

      /// <summary>Moves one booking appointment to a target status.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The status handler.</param>
      /// <param name="appointmentId">The booking appointment.</param>
      /// <param name="targetStatus">The status to move to.</param>
      /// <param name="expectedVersion">The version the caller read.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>A confirmation message.</returns>
      [McpServerTool(
          Name = "set_appointment_status", Title = "Set appointment status",
          ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
      [Description("Move one booking appointment to Expected, CheckedIn, Completed or NoShow, or apply a bounded correction. A same-status submission changes nothing. Caller must hold ConductAppointments.")]
      public async Task<string> SetAppointmentStatusAsync(
          ICallerAccessor caller,
          SetAppointmentStatusHandler handler,
          [Description("The booking appointment identifier.")] Guid appointmentId,
          [Description("Expected, CheckedIn, Completed or NoShow.")] string targetStatus,
          [Description("The version you read.")] long expectedVersion,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new SetAppointmentStatusCommand(
                  caller.RequireStaffUserId(), appointmentId, targetStatus, expectedVersion),
              cancellationToken);
          result.ThrowIfFailure();
          return $"Appointment set to {targetStatus}.";
      }

      /// <summary>Exports one event's roster as CSV.</summary>
      /// <param name="caller">The signed-in staff identity.</param>
      /// <param name="handler">The download handler.</param>
      /// <param name="eventId">The event.</param>
      /// <param name="cancellationToken">The cancellation token.</param>
      /// <returns>The CSV text.</returns>
      [McpServerTool(
          Name = "export_workspace_roster", Title = "Export workspace roster",
          ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
      [Description("Export the roster for one event as CSV text, with formula characters neutralised. Caller must hold ConductAppointments.")]
      public async Task<string> ExportWorkspaceRosterAsync(
          ICallerAccessor caller,
          DownloadRosterHandler handler,
          [Description("The event identifier.")] Guid eventId,
          CancellationToken cancellationToken)
      {
          var result = await handler.HandleAsync(
              new GetWorkspaceRosterQuery(caller.RequireStaffUserId(), eventId),
              cancellationToken);
          return result.ValueOrThrow();
      }
  }
  ```

  ```csharp
  // src/EventBooking.Mcp/Program.cs — the registration block, replacing the ported one. The
  // settings reader and the middleware are Task 21's, unchanged.
  builder.Services
      .AddMcpServer()
      .WithHttpTransport(options => options.Stateless = true)
      .WithTools<IdentityTools>()
      .WithTools<ReferenceDataTools>()
      .WithTools<AdministrationTools>()
      .WithTools<NegotiationTools>()
      .WithTools<EventTools>()
      .WithTools<AttendeeTools>()
      .WithTools<DashboardTools>()
      .WithTools<AuditTools>()
      .WithTools<WorkspaceTools>();
  ```

- [ ] **Step 4: Run.** Expected: PASS — the three MCP suites plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  The parity test is the one to read first: it names any tool whose attribute spells its name
  differently from the catalogue, which is the failure this whole task is arranged to make
  impossible to miss.

  No migration is needed: this task adds no column, no index and no table.

  **No figure here is observed.** This task is hand-authored and nothing in it has been built or
  run. Expect the MCP count to rise by roughly twenty-five cases and the ported MCP suites to be
  replaced rather than extended. The last measured checkpoint remains Task 11 at 1570, and the
  first real Phase 4 figure is whatever the executor reaches here.

- [ ] **Step 5: Commit and push** the executor's code — not the plan documents — under the
  master plan's message:

  ```bash
  test -z "$(git status --porcelain --ignored=no | grep -v '^??')"
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  git add src/EventBooking.Mcp/ tests/EventBooking.Mcp.Tests/
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "feat(mcp): tool parity with the REST catalogue

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```

- [ ] **Step 6: Open the Phase 4 pull request.**

  Phase 4 is decision-bearing: the endpoint contract, the MCP parity rule, and the eight
  settlements Tasks 21 and 22a made. The pull request therefore carries the `narrative-required`
  label and the three narrative headings, spelled exactly as `.github/pull_request_template.md`
  spells them. Supplying a body replaces the repository template wholesale, so the body has to
  carry those three headings and the fingerprint footer itself.

  ```bash
  MERGE_BASE=$(git merge-base origin/main HEAD)
  git diff "$MERGE_BASE" HEAD | shasum -a 256 | cut -c1-12
  ```

  Put that value in the body's `AI-Fingerprint: sha256:<value>` footer, then open the pull
  request with the `narrative-required` label.

  Two things about the checks, both of which have cost a cycle before:

  - **The fingerprint check goes red once on every push, and that red is not yours to fix.** It
    runs on the push, before any body edit can land, and compares against the previous hash.
    Wait until the pull request reports the head you just pushed, then update the body; the
    re-run passes and the earlier failure is superseded. Do not chase the first red.
  - **A missing narrative label or a missing heading is not repairable after merge.** The
    maintenance workflow fires on the merge event only: without the label it exits silently, and
    with the label but without the headings it fails visibly. Labelling a merged pull request
    does nothing.

  Do not merge the pull request yourself: code-owner review and the required checks stand.
