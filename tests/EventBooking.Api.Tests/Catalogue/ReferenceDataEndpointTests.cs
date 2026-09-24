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
            code = "REF_DENY", name = "Denied",
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
