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
    public void AnAlreadyConfirmedBodyCarriesItsBookingId()
    {
        var bookingId = Guid.NewGuid();

        var body = ResultResponses.ProblemBodyFor(
            Error.AlreadyConfirmed($"This invite already confirmed booking {bookingId}.", bookingId));

        Assert.Equal("already-confirmed", body.Type);
        Assert.Equal(bookingId.ToString(), body.Extensions["relatedId"]);
    }

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
