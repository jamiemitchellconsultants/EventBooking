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
using EventBooking.TestSupport;
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
    /// The list tools, each with the field its rows are identified by and the field that
    /// states where the row stands. A tool absent from this table fails the completeness case
    /// below, so adding a list tool without covering it is not something review has to catch.
    ///
    /// The columns are named rather than guessed at: each comes from the view record the tool
    /// returns. An audit row has no name and no status: it is labelled by the action it records
    /// and stated by the actor that caused it, and demanding a name of it would only prove the
    /// table had been written to fit.
    /// </summary>
    public static TheoryData<string, string, string> ListTools
    {
        get
        {
            TheoryData<string, string, string> data = new();
            foreach (var (tool, label, state) in TypedLists)
            {
                data.Add(tool, label, state);
            }

            return data;
        }
    }

    private static readonly (string Tool, string Label, string State)[] TypedLists =
    [
        ("list_locations", "name", "isActive"),
        ("list_appointment_types", "name", "isActive"),
        ("list_attendee_groups", "code", "isActive"),
        ("list_event_proposals", "locationName", "status"),
        ("list_events", "locationName", "status"),
        ("list_cancellable_events", "locationName", "status"),
        ("list_attendees", "name", "status"),
        ("list_workspace_events", "locationName", "status"),
        ("search_audit", "action", "actorType"),
    ];

    /// <summary>
    /// The two list tools held to the weaker rule below rather than to named columns, listed
    /// here so that the completeness case still covers every list tool.
    /// </summary>
    public static TheoryData<string> UntypedListTools
    {
        get
        {
            TheoryData<string> data = new();
            foreach (var tool in UntypedLists)
            {
                data.Add(tool);
            }

            return data;
        }
    }

    private static readonly string[] UntypedLists =
    [
        "list_staff_access",
        "list_attendee_bookings",
    ];

    private static readonly string[] CoveredToolsInSeedOrder =
    [
        "list_locations",
        "list_appointment_types",
        "list_attendee_groups",
        "list_event_proposals",
        "list_events",
        "list_cancellable_events",
        "list_attendees",
        "list_workspace_events",
        "search_audit",
        "list_staff_access",
        "list_attendee_bookings",
    ];

    [Theory]
    [MemberData(nameof(ListTools))]
    public async Task EveryRowCarriesItsIdentifierItsLabelAndItsState(
        string tool, string label, string state)
    {
        var world = await GivenSeededWorldAsync(tool);
        var client = new McpClient(factory.CreateClient());

        var result = await client.CallAsync(tool, ArgumentsFor(tool, world));

        var rows = Rows(result);
        Assert.NotEmpty(rows);
        Assert.All(rows, row =>
        {
            Assert.True(
                HasAny(row, "id", "eventId", "proposalId", "attendeeId", "staffUserId",
                    "bookingId", "entityId"),
                $"{tool} returned a row an agent cannot act on: {row}");
            AssertPresent(row, label, tool);
            AssertPresent(row, state, tool);
        });
    }

    /// <summary>
    /// The weaker rule for the two rows held to no named columns: an identifier, and at
    /// least two further fields with something in them. It still fails a row an agent cannot
    /// tell apart, without asserting column names taken from nowhere.
    /// </summary>
    /// <param name="tool">The tool name.</param>
    /// <returns>A task tracking the case.</returns>
    [Theory]
    [MemberData(nameof(UntypedListTools))]
    public async Task AnUntypedRowIsStillDistinguishable(string tool)
    {
        var world = await GivenSeededWorldAsync(tool);
        var client = new McpClient(factory.CreateClient());

        var result = await client.CallAsync(tool, ArgumentsFor(tool, world));

        var rows = Rows(result);
        Assert.NotEmpty(rows);
        Assert.All(rows, row =>
        {
            Assert.True(
                HasAny(row, "id", "eventId", "proposalId", "attendeeId", "staffUserId",
                    "bookingId", "entityId"),
                $"{tool} returned a row with no identifier: {row}");
            Assert.True(
                row.EnumerateObject().Count(
                    property => property.Value.ValueKind is not JsonValueKind.Null
                        && property.Value.ToString().Length > 0) >= 3,
                $"{tool} returned a row with nothing in it but an identifier: {row}");
        });
    }

    /// <summary>Asserts one named field is present and carries something.</summary>
    /// <param name="row">The row.</param>
    /// <param name="field">The camel-cased field name.</param>
    /// <param name="tool">The tool, for the failure message.</param>
    private static void AssertPresent(JsonElement row, string field, string tool)
    {
        Assert.True(
            row.TryGetProperty(field, out var value),
            $"{tool} rows carry no {field}: {row}");
        Assert.False(
            value.ValueKind is JsonValueKind.Null
                || (value.ValueKind is JsonValueKind.String
                    && string.IsNullOrWhiteSpace(value.GetString())),
            $"{tool} returned an empty {field}: {row}");
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

        var covered = TypedLists.Select(row => row.Tool)
            .Concat(UntypedLists)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(listed.Except(covered));
        Assert.Empty(covered.Except(listed));
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
        // Codes are capped at eight characters by the database columns, so each case
        // takes a short indexed code rather than spelling out its tool name.
        var index = Array.IndexOf(CoveredToolsInSeedOrder, tool);
        var code = $"M{index}";
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

        var type = AppointmentType.Create(Guid.NewGuid(), code + "T", $"MCP {tool} type");
        var location = Location.Create(
            Guid.NewGuid(), code + "L", $"MCP {tool} site", "1 Test Street", "Europe/London",
            ProposalFixture.Zones);
        var group = AttendeeGroup.Create(
            Guid.NewGuid(), code + "G", $"MCP {tool} group", [type.Id], [type.Id]);
        context.AppointmentTypes.Add(type);
        context.Locations.Add(location);
        context.AttendeeGroups.Add(group);
        await context.SaveChangesAsync();

        var attendee = Attendee.Create(
            Guid.NewGuid(), "Test Attendee", $"mcp-list-{index}@example.com", group, Now);
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
            proposal.Accept(type.Id, Guid.NewGuid(), 10);
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
        "list_attendee_bookings" => new { attendeeId = world.AttendeeId },
        "list_attendees" => new { groupId = world.AttendeeGroupId, limit = 50 },
        "list_events" or "list_cancellable_events" =>
            new { locationId = world.LocationId, limit = 50 },
        "list_event_proposals" or "search_audit" => new { limit = 50 },
        "list_workspace_events" => new { locationId = world.LocationId },
        "list_staff_access" => new { },
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

        if (payload.ValueKind == JsonValueKind.Array)
        {
            return [.. payload.EnumerateArray()];
        }

        // The audit search page names its rows rows; every other page names them items.
        var array = payload.TryGetProperty("items", out var items)
            ? items
            : payload.GetProperty("rows");
        return [.. array.EnumerateArray()];
    }
}
