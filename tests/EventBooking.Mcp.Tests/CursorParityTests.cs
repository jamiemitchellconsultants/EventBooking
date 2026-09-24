using System.Text;
using System.Text.Json;
using EventBooking.Api.Pagination;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Mcp.Tests;

/// <summary>
/// The paged tools use the REST cursor, not a variant of it: signed on the way out, verified on
/// the way in. A raw keyset payload on one surface and a signed one on the other would make a
/// cursor from either read as page one on the other, and would let an unverified payload reach
/// the query on the surface that skipped the check.
/// </summary>
[Collection("mcp")]
public sealed class CursorParityTests(McpFactory factory)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 9, 0, 0, TimeSpan.Zero);

    public static TheoryData<string> PagedTools => new()
    {
        "list_attendees",
        "list_events",
        "list_cancellable_events",
        "list_event_proposals",
        "search_audit",
    };

    /// <summary>
    /// A cursor the API never signed is refused with the code REST renders as
    /// validation-failed, before any handler runs, rather than read as the first page.
    /// </summary>
    [Theory]
    [MemberData(nameof(PagedTools))]
    public async Task ACursorTheApiNeverSignedIsRefusedAsRestRefusesIt(string tool)
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = new McpClient(factory.CreateClient());

        var envelope = await client.CallRawAsync(tool, new { limit = 1, cursor = "not-a-cursor" });

        var failure = Failure(envelope);
        var separator = failure.IndexOf(": ", StringComparison.Ordinal);
        Assert.True(separator > 0, $"{tool} refused without an application error code: {failure}");
        var shape = EventBooking.Api.Endpoints.ProblemCatalogue.For(failure[..separator]);
        Assert.Equal("validation-failed", shape.Type);
    }

    /// <summary>
    /// The next cursor is the REST one: it verifies under the key the API signs with, and
    /// handing it back reads the following page.
    /// </summary>
    [Fact]
    public async Task TheNextCursorIsSignedAndReadsTheFollowingPage()
    {
        var groupId = await SeedTwoAttendeesAsync();
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        var client = new McpClient(factory.CreateClient());

        var first = Payload(await client.CallAsync(
            "list_attendees", new { groupId, limit = 1 }));
        var next = first.GetProperty("nextCursor").GetString();

        Assert.NotNull(next);
        Assert.True(RestCursor().TryUnprotect(next, out _), $"Unsigned cursor: {next}");
        var second = Payload(await client.CallAsync(
            "list_attendees", new { groupId, limit = 1, cursor = next }));
        var firstId = first.GetProperty("items")[0].GetProperty("attendeeId").GetGuid();
        var secondId = second.GetProperty("items")[0].GetProperty("attendeeId").GetGuid();
        Assert.NotEqual(firstId, secondId);
    }

    /// <summary>The signer is injected, never an argument an agent is asked to supply.</summary>
    [Theory]
    [MemberData(nameof(PagedTools))]
    public async Task TheSignerIsNotPartOfTheToolSchema(string tool)
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
        var client = new McpClient(factory.CreateClient());

        var listed = (await client.ListToolsAsync()).GetProperty("tools").EnumerateArray()
            .Single(x => x.GetProperty("name").GetString() == tool);

        var properties = listed.GetProperty("inputSchema").GetProperty("properties")
            .EnumerateObject().Select(x => x.Name).ToList();
        Assert.Contains("cursor", properties);
        Assert.DoesNotContain("cursors", properties);
    }

    private PageCursor RestCursor() => new(Encoding.UTF8.GetBytes(
        factory.Services.GetRequiredService<IConfiguration>()["Tokens:SigningKey"]!));

    private async Task<Guid> SeedTwoAttendeesAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var type = AppointmentType.Create(Guid.NewGuid(), "CURT", "Cursor type");
        var group = AttendeeGroup.Create(
            Guid.NewGuid(), "CURG", "Cursor group", [type.Id], [type.Id]);
        context.AppointmentTypes.Add(type);
        context.AttendeeGroups.Add(group);
        await context.SaveChangesAsync();

        context.Attendees.Add(Attendee.Create(
            Guid.NewGuid(), "Cursor One", "cursor-one@example.com", group, Now));
        context.Attendees.Add(Attendee.Create(
            Guid.NewGuid(), "Cursor Two", "cursor-two@example.com", group, Now));
        await context.SaveChangesAsync();
        return group.Id;
    }

    private static JsonElement Payload(JsonElement result) =>
        result.TryGetProperty("structuredContent", out var structured)
            ? structured
            : JsonDocument.Parse(result.GetProperty("content")[0].GetProperty("text").GetString()!)
                .RootElement.Clone();

    /// <summary>The same reading RefusalParityTests gives a refusal.</summary>
    private static string Failure(JsonElement envelope)
    {
        string text;
        if (envelope.TryGetProperty("error", out var error))
        {
            text = error.GetProperty("message").GetString() ?? string.Empty;
        }
        else
        {
            var result = envelope.GetProperty("result");
            Assert.True(
                result.TryGetProperty("isError", out var isError) && isError.GetBoolean(),
                $"The call succeeded instead of refusing: {result}");
            text = result.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
        }

        return System.Text.RegularExpressions.Regex.Replace(
            text, "^An error occurred invoking '[^']*': ", string.Empty);
    }
}
