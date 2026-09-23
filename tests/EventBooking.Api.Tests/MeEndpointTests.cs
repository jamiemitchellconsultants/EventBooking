using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class MeEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task AnUnassignedCallerGetsTwoHundredWithAnEmptyRoleSet()
    {
        factory.SignedInAs = Guid.NewGuid();
        factory.StaffIdClaim = "u123456";
        factory.RolesClaim = [];
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Empty(me!.Roles);
        Assert.Equal("U123456", me.StaffId);
        Assert.Null(me.AppointmentTypeId);
        Assert.Null(me.AppointmentTypeName);
    }

    [Fact]
    public async Task SigningInWithARoleClaimCreatesAProfileWithNullScope()
    {
        var staffUserId = Guid.NewGuid();
        factory.SignedInAs = staffUserId;
        factory.RolesClaim = ["Manager"];
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/me");
        var body = await response.Content.ReadFromJsonAsync<MeResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Manager", body!.Roles);
        Assert.Null(body.AppointmentTypeId);
    }

    [Fact]
    public async Task ASecondCallWithTheSameRoleClaimDoesNotDuplicateTheAuditEntry()
    {
        var staffUserId = Guid.NewGuid();
        factory.SignedInAs = staffUserId;
        factory.RolesClaim = ["Coordinator"];
        var client = factory.CreateClient();

        await client.GetAsync("/api/me");
        await client.GetAsync("/api/me");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var syncedCount = await context.AuditLogs.CountAsync(
            log => log.EntityId == staffUserId && log.Action == AuditAction.StaffRolesSynced);
        Assert.Equal(1, syncedCount);
    }

    [Fact]
    public async Task ConcurrentFirstCallsBothSucceedWithOneProfileAndOneAuditEntry()
    {
        var staffUserId = Guid.NewGuid();
        factory.SignedInAs = staffUserId;
        factory.RolesClaim = ["Manager"];
        var firstClient = factory.CreateClient();
        var secondClient = factory.CreateClient();

        var responses = await Task.WhenAll(
            firstClient.GetAsync("/api/me"),
            secondClient.GetAsync("/api/me"));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.Single(await context.StaffAccessProfiles
            .Where(value => value.StaffUserId == staffUserId)
            .ToListAsync());
        Assert.Single(await context.AuditLogs
            .Where(value => value.EntityId == staffUserId
                && value.Action == AuditAction.StaffRolesSynced)
            .ToListAsync());
    }

    [Fact]
    public async Task AManagerGetsTheirRoleAndAppointmentTypeName()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.UniformFitting);
        factory.RolesClaim = ["Manager"];
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/me");

        var me = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal(["Manager"], me!.Roles);
        Assert.Equal(AppointmentTypeIds.UniformFitting, me.AppointmentTypeId);
        Assert.Equal("Uniform Fitting", me.AppointmentTypeName);
    }

    [Fact]
    public async Task CombinedRolesAreReturnedInEnumOrder()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff, Role.Coordinator, Role.Manager],
            AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.RolesClaim = ["AppointmentStaff", "Coordinator", "Manager"];
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/me");

        var me = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal(["Manager", "Coordinator", "AppointmentStaff"], me!.Roles);
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, me.AppointmentTypeId);
        Assert.Equal("Drug & Alcohol Testing", me.AppointmentTypeName);
    }

    [Fact]
    public async Task AnAnonymousCallerIsRejected()
    {
        factory.SignedInAs = null;
        factory.RolesClaim = [];
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record MeResponse(
        string? StaffId,
        IReadOnlyList<string> Roles,
        Guid? AppointmentTypeId,
        string? AppointmentTypeName);
}
