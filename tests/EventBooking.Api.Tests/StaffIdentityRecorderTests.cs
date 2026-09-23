using System.Net;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

/// <summary>Verifies authenticated identity recording occurs before staff authorization.</summary>
[Collection("api")]
public sealed class StaffIdentityRecorderTests(ApiFactory factory)
{
    /// <summary>Verifies first sight is recorded even when no access profile exists.</summary>
    [Fact]
    public async Task FirstValidIdentityIsRecordedBeforeTheRequestIsForbidden()
    {
        var staffUserId = Guid.NewGuid();
        factory.SignedInAs = staffUserId;
        factory.StaffIdClaim = "u123456";

        var response = await factory.CreateClient().GetAsync("/api/admin/staff-access");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var identity = await database.StaffIdentities.SingleAsync(
            value => value.StaffUserId == staffUserId);
        Assert.Equal("U123456", identity.StaffId.Value);
    }

    /// <summary>Verifies invalid identity data neither records a row nor blocks authenticated-only APIs.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("invalid staff id")]
    public async Task MissingOrMalformedClaimIsNotRecordedButMeRemainsReachable(string? claim)
    {
        var staffUserId = Guid.NewGuid();
        factory.SignedInAs = staffUserId;
        factory.StaffIdClaim = claim;

        var me = await factory.CreateClient().GetAsync("/api/me");
        var staff = await factory.CreateClient().GetAsync("/api/admin/staff-access");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, staff.StatusCode);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.False(await database.StaffIdentities.AnyAsync(
            value => value.StaffUserId == staffUserId));
    }

    /// <summary>Verifies the process cache avoids refreshing the row on every request.</summary>
    [Fact]
    public async Task CachedIdentityDoesNotWriteAgain()
    {
        var staffUserId = Guid.NewGuid();
        factory.SignedInAs = staffUserId;
        factory.StaffIdClaim = "N654321";
        var client = factory.CreateClient();

        await client.GetAsync("/api/me");
        DateTimeOffset firstSeen;
        using (var firstScope = factory.Services.CreateScope())
        {
            firstSeen = await firstScope.ServiceProvider
                .GetRequiredService<EventBookingDbContext>()
                .StaffIdentities
                .Where(value => value.StaffUserId == staffUserId)
                .Select(value => value.LastSeenAt)
                .SingleAsync();
        }

        await client.GetAsync("/api/me");

        using var secondScope = factory.Services.CreateScope();
        var secondSeen = await secondScope.ServiceProvider
            .GetRequiredService<EventBookingDbContext>()
            .StaffIdentities
            .Where(value => value.StaffUserId == staffUserId)
            .Select(value => value.LastSeenAt)
            .SingleAsync();
        Assert.Equal(firstSeen, secondSeen);
    }

    /// <summary>Verifies a provider uniqueness conflict is logged without failing the caller's request.</summary>
    [Fact]
    public async Task DuplicateStaffNumberDoesNotFailTheAuthenticatedRequest()
    {
        factory.StaffIdClaim = "U777777";
        factory.SignedInAs = Guid.NewGuid();
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me")).StatusCode);

        var conflictingUserId = Guid.NewGuid();
        factory.SignedInAs = conflictingUserId;

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.False(await database.StaffIdentities.AnyAsync(
            value => value.StaffUserId == conflictingUserId));
    }

    /// <summary>Verifies a present name claim is mirrored onto the identity on a cache miss.</summary>
    [Fact]
    public async Task RecorderPassesDisplayNameThroughOnCacheMiss()
    {
        var staffUserId = Guid.NewGuid();
        factory.SignedInAs = staffUserId;
        factory.StaffIdClaim = "U200001";
        factory.NameClaim = "Dana Datson";

        await factory.CreateClient().GetAsync("/api/me");

        Assert.Equal("Dana Datson", await DisplayNameOfAsync(staffUserId));
        factory.NameClaim = null;
    }

    /// <summary>Verifies an absent name claim mirrors no name and never blocks the request.</summary>
    [Fact]
    public async Task RecorderPassesNullDisplayNameWhenClaimAbsent()
    {
        var staffUserId = Guid.NewGuid();
        factory.SignedInAs = staffUserId;
        factory.StaffIdClaim = "U200002";
        factory.NameClaim = null;

        var response = await factory.CreateClient().GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(await DisplayNameOfAsync(staffUserId));
    }

    /// <summary>Verifies a rename lags until the cache expires, exactly like the observation time.</summary>
    [Fact]
    public async Task RecorderSuppressesTheWriteOnACacheHitSoARenameLags()
    {
        var staffUserId = Guid.NewGuid();
        factory.SignedInAs = staffUserId;
        factory.StaffIdClaim = "U200003";
        factory.NameClaim = "Old Name";
        var client = factory.CreateClient();

        await client.GetAsync("/api/me");
        Assert.Equal("Old Name", await DisplayNameOfAsync(staffUserId));

        factory.NameClaim = "New Name";
        await client.GetAsync("/api/me");

        Assert.Equal("Old Name", await DisplayNameOfAsync(staffUserId));
        factory.NameClaim = null;
    }

    private async Task<string?> DisplayNameOfAsync(Guid staffUserId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<EventBookingDbContext>()
            .StaffIdentities
            .Where(value => value.StaffUserId == staffUserId)
            .Select(value => value.DisplayName)
            .SingleAsync();
    }
}
