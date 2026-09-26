using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        Assert.Null(me.ScopeAppointmentTypeId);
        Assert.Null(me.ScopeAppointmentTypeName);
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
        Assert.Null(body.ScopeAppointmentTypeId);
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
        Assert.Equal(AppointmentTypeIds.UniformFitting, me.ScopeAppointmentTypeId);
        Assert.Equal("UNI", me.ScopeAppointmentTypeCode);
        Assert.Equal("Uniform Fitting", me.ScopeAppointmentTypeName);
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
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, me.ScopeAppointmentTypeId);
        Assert.Equal("DAT", me.ScopeAppointmentTypeCode);
        Assert.Equal("Drug & Alcohol Testing", me.ScopeAppointmentTypeName);
    }

    [Fact]
    public async Task AnAnonymousCallerGetsTheNoRoleViewInsteadOfA401()
    {
        var signedInAs = factory.SignedInAs;
        var rolesClaim = factory.RolesClaim;
        try
        {
            factory.SignedInAs = null;
            factory.RolesClaim = [];
            var client = factory.CreateClient();

            var response = await client.GetAsync("/api/me");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("staff_id", await response.Content.ReadAsStringAsync());
        }
        finally
        {
            factory.SignedInAs = signedInAs;
            factory.RolesClaim = rolesClaim;
        }
    }

    [Fact]
    public async Task TheNameClaimIsSurfacedAsDisplayName()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        factory.RolesClaim = ["Coordinator"];
        factory.NameClaim = "Alex Coordinator";
        try
        {
            var client = factory.CreateClient();

            var me = await client.GetFromJsonAsync<MeResponse>("/api/me");

            Assert.Equal("Alex Coordinator", me!.DisplayName);
        }
        finally
        {
            factory.NameClaim = null;
        }
    }

    public static TheoryData<string, string[]> MutationAffordancesByRole => new()
    {
        { "Admin", ["createLocation", "createAppointmentType", "createAttendeeGroup", "createEventGroup"] },
        { "Coordinator", ["createAttendee", "importAttendees", "createEventGroup"] },
        { "Manager", ["proposeEvent"] },
    };

    [Theory]
    [MemberData(nameof(MutationAffordancesByRole))]
    public async Task EachRoleSeesOnlyItsOwnCollectionMutationAffordances(
        string role, string[] expectedRelations)
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Enum.Parse<Role>(role)],
            role == "Manager" ? AppointmentTypeIds.MedicalCheckUp : null);
        factory.RolesClaim = [role];
        var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<JsonElement>("/api/me");

        var links = body.GetProperty("_links");
        var all = new[]
        {
            "createLocation", "createAppointmentType", "createAttendeeGroup",
            "createAttendee", "importAttendees", "proposeEvent", "createEventGroup",
        };
        foreach (var relation in expectedRelations)
        {
            Assert.True(links.TryGetProperty(relation, out _), relation);
        }

        foreach (var relation in all.Except(expectedRelations))
        {
            Assert.False(links.TryGetProperty(relation, out _), relation);
        }
    }

    [Fact]
    public async Task AnAnonymousCallerSeesNoMutationAffordance()
    {
        var signedInAs = factory.SignedInAs;
        var rolesClaim = factory.RolesClaim;
        try
        {
            factory.SignedInAs = null;
            factory.RolesClaim = [];
            var body = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/me");

            var links = body.GetProperty("_links");
            foreach (var relation in new[]
            {
                "createLocation", "createAppointmentType", "createAttendeeGroup",
                "createAttendee", "importAttendees", "proposeEvent", "createEventGroup",
            })
            {
                Assert.False(links.TryGetProperty(relation, out _), relation);
            }
        }
        finally
        {
            factory.SignedInAs = signedInAs;
            factory.RolesClaim = rolesClaim;
        }
    }

    private sealed record MeResponse(
        string? DisplayName,
        string? StaffId,
        IReadOnlyList<string> Roles,
        Guid? ScopeAppointmentTypeId,
        string? ScopeAppointmentTypeCode,
        string? ScopeAppointmentTypeName);
}
