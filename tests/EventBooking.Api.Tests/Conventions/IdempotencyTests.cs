using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventBooking.Api.Idempotency;
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
        var (client, groupId) = await GivenCoordinatorAndGroupAsync();
        var key = Guid.NewGuid().ToString();
        var body = new { name = "Ada Lovelace", email = "ada@example.com", attendeeGroupId = groupId };

        var first = await PostAsync(client, body, key);
        var second = await PostAsync(client, body, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(
            await first.Content.ReadAsStringAsync(),
            await second.Content.ReadAsStringAsync());
        Assert.Equal(1, await CountByEmailAsync(body.email));
    }

    [Fact]
    public async Task ADifferentBodyUnderTheSameKeyIsRefused()
    {
        var (client, groupId) = await GivenCoordinatorAndGroupAsync();
        var key = Guid.NewGuid().ToString();

        await PostAsync(
            client, new { name = "Ada", email = "ada2@example.com", attendeeGroupId = groupId }, key);
        var second = await PostAsync(
            client, new { name = "Grace", email = "grace@example.com", attendeeGroupId = groupId }, key);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation-failed", problem.GetProperty("type").GetString());
        Assert.Equal(1, await CountByEmailAsync("ada2@example.com"));
        Assert.Equal(0, await CountByEmailAsync("grace@example.com"));
    }

    [Fact]
    public async Task NoKeyMeansNoReplayProtectionAndTwoRows()
    {
        var (client, groupId) = await GivenCoordinatorAndGroupAsync();

        await PostAsync(
            client, new { name = "A", email = "a-none@example.com", attendeeGroupId = groupId }, null);
        await PostAsync(
            client, new { name = "B", email = "b-none@example.com", attendeeGroupId = groupId }, null);

        Assert.Equal(1, await CountByEmailAsync("a-none@example.com"));
        Assert.Equal(1, await CountByEmailAsync("b-none@example.com"));
    }

    /// <summary>
    /// A key older than the 24-hour retention reads as absent, so the second call runs again.
    /// The stored instant is moved back rather than the clock moved forward: the middleware
    /// reads the wall clock, and only the row is under the test's control.
    /// </summary>
    [Fact]
    public async Task AKeyOlderThanTheRetentionIsNoLongerReplayed()
    {
        var (client, groupId) = await GivenCoordinatorAndGroupAsync();
        var key = Guid.NewGuid().ToString();
        await PostAsync(
            client, new { name = "A", email = "a-old@example.com", attendeeGroupId = groupId }, key);
        await AgeEveryKeyAsync(TimeSpan.FromHours(25));

        var second = await PostAsync(
            client, new { name = "A", email = "a-old-2@example.com", attendeeGroupId = groupId }, key);

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(1, await CountByEmailAsync("a-old@example.com"));
        Assert.Equal(1, await CountByEmailAsync("a-old-2@example.com"));
    }

    [Fact]
    public async Task ConcurrentRequestsWithOneKeyCreateOneAttendeeAndReplayOneResponse()
    {
        var (client, groupId) = await GivenCoordinatorAndGroupAsync();
        var key = Guid.NewGuid().ToString();
        var body = new { name = "Ada", email = "ada-concurrent@example.com", attendeeGroupId = groupId };

        var responses = await Task.WhenAll(PostAsync(client, body, key), PostAsync(client, body, key));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        Assert.Equal(1, await CountByEmailAsync(body.email));
        Assert.Equal(await responses[0].Content.ReadAsStringAsync(),
            await responses[1].Content.ReadAsStringAsync());
    }

    /// <summary>
    /// The replay is the response the caller missed, headers included: a 201 whose Location
    /// vanished on retry would leave the caller unable to find what it created.
    /// </summary>
    [Fact]
    public async Task AReplayCarriesTheFirstResponsesLocationAndContentType()
    {
        var (client, groupId) = await GivenCoordinatorAndGroupAsync();
        var key = Guid.NewGuid().ToString();
        var body = new { name = "Ada", email = "ada-location@example.com", attendeeGroupId = groupId };

        var first = await PostAsync(client, body, key);
        var second = await PostAsync(client, body, key);

        Assert.NotNull(first.Headers.Location);
        Assert.Equal(first.Headers.Location, second.Headers.Location);
        Assert.Equal(
            first.Content.Headers.ContentType?.MediaType,
            second.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// The key is refused before the handler runs. Checked after, the create would already be
    /// committed when the retention write failed, and the caller's retry would create twice.
    /// </summary>
    [Fact]
    public async Task AKeyLongerThanTheRetainedColumnIsRefusedBeforeAnythingIsCreated()
    {
        var (client, groupId) = await GivenCoordinatorAndGroupAsync();
        var key = new string('k', IdempotencyMiddleware.MaxKeyLength + 1);
        var body = new { name = "Ada", email = "ada-long-key@example.com", attendeeGroupId = groupId };

        var response = await PostAsync(client, body, key);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation-failed", problem.GetProperty("type").GetString());
        Assert.Equal(0, await CountByEmailAsync(body.email));
    }

    [Fact]
    public async Task AKeyAtTheLimitIsRetainedAndReplayed()
    {
        var (client, groupId) = await GivenCoordinatorAndGroupAsync();
        var key = new string('m', IdempotencyMiddleware.MaxKeyLength - 36) + Guid.NewGuid();
        var body = new { name = "Ada", email = "ada-max-key@example.com", attendeeGroupId = groupId };

        var first = await PostAsync(client, body, key);
        var second = await PostAsync(client, body, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(1, await CountByEmailAsync(body.email));
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

    /// <summary>
    /// Signs in a Coordinator against the seeded Pilots group. The collection shares one
    /// database, so the test creates no group of its own and counts by unique email.
    /// </summary>
    private async Task<(HttpClient Client, Guid GroupId)> GivenCoordinatorAndGroupAsync()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        factory.StaffIdClaim = "U500002";
        return (factory.CreateClient(), AttendeeGroupIds.Pilots);
    }

    private async Task<int> CountByEmailAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        return await context.Attendees.CountAsync(a => a.Email == email);
    }

    private async Task AgeEveryKeyAsync(TimeSpan by)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE idempotency_record SET created_at = created_at - @p0", by);
    }
}
