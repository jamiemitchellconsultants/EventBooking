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
/// Walks the attendee list. Step 3 repairs that route onto Phase 3's paged query and this
/// task's page contract, which is what makes it the one cursor-paged route before Task 22b.
/// Three walks: a single page, three pages, and an empty result.
/// </summary>
[Collection("api")]
public sealed class PaginationWalkTests(ApiFactory factory)
{
    [Fact]
    public async Task OnePageReturnsEveryRowAndNoNextCursor()
    {
        var search = await GivenWalkAttendeesAsync("PAGE_ONE", 3);
        var client = await SignedInCoordinatorAsync();

        var page = await ReadPageAsync(client, $"/api/attendees?search={search}&limit=50");

        Assert.Equal(3, page.Items.Length);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task ThreePagesWalkWithoutRepeatingOrLosingARow()
    {
        var search = await GivenWalkAttendeesAsync("PAGE_THREE", 7);
        var client = await SignedInCoordinatorAsync();

        var seen = new List<string>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var url = $"/api/attendees?search={search}&limit=3"
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
        var search = await GivenWalkAttendeesAsync("PAGE_EMPTY", 0);
        var client = await SignedInCoordinatorAsync();

        var page = await ReadPageAsync(client, $"/api/attendees?search={search}&limit=50");

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task ATamperedCursorIsRefusedAsValidationFailed()
    {
        var search = await GivenWalkAttendeesAsync("PAGE_TAMPER", 4);
        var client = await SignedInCoordinatorAsync();
        var first = await ReadPageAsync(client, $"/api/attendees?search={search}&limit=2");
        var cursor = first.NextCursor!;
        var tampered = cursor[..^2] + (cursor[^2] == 'A' ? "BB" : "AA");

        var response = await client.GetAsync(
            $"/api/attendees?search={search}&limit=2&cursor={Uri.EscapeDataString(tampered)}");

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

    /// <summary>
    /// Seeds members with a unique email prefix into the seeded Pilots group and returns the
    /// search prefix isolating them. The collection shares one database, so the test creates
    /// no group of its own: group-count assertions elsewhere would see it.
    /// </summary>
    private async Task<string> GivenWalkAttendeesAsync(string code, int members)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var group = await context.AttendeeGroups
            .Include(g => g.Requirements)
            .SingleAsync(g => g.Id == AttendeeGroupIds.Pilots);
        var stamped = DateTimeOffset.Parse("2026-09-21T09:00:00Z");
        for (var index = 0; index < members; index++)
        {
            context.Attendees.Add(Attendee.Create(
                Guid.NewGuid(),
                $"{code} Member {index:D2}",
                $"{code.ToLowerInvariant()}-{index:D2}@example.com",
                group,
                stamped));
        }

        await context.SaveChangesAsync();
        return code.ToLowerInvariant() + "-";
    }
}
