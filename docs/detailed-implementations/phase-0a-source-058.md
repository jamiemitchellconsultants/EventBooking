# 00a — Port source 58 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Api.Tests/ResultResponsesTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/ResultResponsesTests.cs","encoding":"utf8","sha256":"3b7115bbb16f28d77a9cff70db5de491cc3925578ebece1dadab0952ba6d4883","parts":1,"part":1} -->

`````csharp
using EventBooking.Api.Endpoints;
using EventBooking.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace EventBooking.Api.Tests;

public class ResultResponsesTests
{
    [Theory]
    [InlineData("validation", 400)]
    [InlineData("forbidden", 403)]
    [InlineData("not_found", 404)]
    [InlineData("conflict", 409)]
    [InlineData("appointment_version_conflict", 409)]
    public void EachErrorCodeMapsToItsStatus(string code, int expected)
    {
        Assert.Equal(expected, ResultResponses.StatusCodeFor(code));
    }

    [Fact]
    public void AnUnrecognisedCodeIsAServerError()
    {
        Assert.Equal(500, ResultResponses.StatusCodeFor("something-new"));
    }

    [Fact]
    public void ASuccessfulResultWithNoValueIsNoContent()
    {
        var response = Result.Success().ToResponse();

        Assert.Equal("NoContent", response.GetType().Name.Replace("Result", "NoContent"));
    }

    [Fact]
    public void AFailedResultCarriesItsMessage()
    {
        var response = Result.Failure(Error.Conflict("No remaining capacity.")).ToResponse();

        Assert.NotNull(response);
        Assert.Contains("ProblemHttpResult", response.GetType().Name);
    }

    [Fact]
    public async Task AFailedResultWritesProblemDetails()
    {
        var context = await Execute(Result.Failure(Error.Conflict("No remaining capacity.")).ToResponse());

        Assert.Equal(409, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        var problem = await ReadJson(context);
        Assert.Equal("conflict", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal("No remaining capacity.", problem.RootElement.GetProperty("detail").GetString());
        Assert.Equal(409, problem.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task ASuccessfulGenericResultWritesItsValueWithOk()
    {
        var context = await Execute(Result<string>.Success("confirmed").ToResponse());

        Assert.Equal(200, context.Response.StatusCode);
        var body = await ReadBody(context);
        Assert.Equal("\"confirmed\"", body);
    }

    [Fact]
    public async Task ASuccessfulCreatedResultWritesLocationAndValue()
    {
        var context = await Execute(Result<string>.Success("confirmed").ToCreated(value => $"/bookings/{value}"));

        Assert.Equal(201, context.Response.StatusCode);
        Assert.Equal("/bookings/confirmed", context.Response.Headers.Location.ToString());
        var body = await ReadBody(context);
        Assert.Equal("\"confirmed\"", body);
    }

    [Fact]
    public async Task FailedGenericAndCreatedResultsWriteProblemDetailsWithoutReadingValue()
    {
        var failed = Result<string>.Failure(Error.NotFound("Booking was not found."));

        var genericContext = await Execute(failed.ToResponse());
        var createdContext = await Execute(failed.ToCreated(_ => throw new InvalidOperationException("location must not be evaluated")));

        Assert.Equal(404, genericContext.Response.StatusCode);
        Assert.Equal(404, createdContext.Response.StatusCode);
        Assert.Equal("Booking was not found.", (await ReadJson(genericContext)).RootElement.GetProperty("detail").GetString());
        Assert.Equal("Booking was not found.", (await ReadJson(createdContext)).RootElement.GetProperty("detail").GetString());
    }

    private static async Task<DefaultHttpContext> Execute(IResult result)
    {
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection().AddOptions().AddLogging().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        return context;
    }

    private static async Task<string> ReadBody(DefaultHttpContext context)
    {
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static async Task<JsonDocument> ReadJson(DefaultHttpContext context) =>
        await JsonDocument.ParseAsync(context.Response.Body);
}
`````

## tests/EventBooking.Api.Tests/SlotEndpointTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/SlotEndpointTests.cs","encoding":"utf8","sha256":"af886fe1601f9f414d3bd52ec4be8f427d17d74a749f405d0224783ce61feba2","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class SlotEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task AnAnonymousCallerIsChallenged()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/slots/board");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ASignedInUserWithNoRoleAssignmentIsForbidden()
    {
        factory.SignedInAs = Guid.NewGuid();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/slots/board");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AManagerCanProposeASlotAndSeeItOnTheBoard()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        var created = await client.PostAsJsonAsync(
            "/api/slots/proposals",
            new { Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), StartTime = new TimeOnly(9, 0) });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var board = await client.GetFromJsonAsync<BoardResponse>("/api/slots/board");
        Assert.NotNull(board);
        Assert.Contains(board!.OpenProposals, p => p.StartTime == new TimeOnly(9, 0));
    }

    [Fact]
    public async Task AWindowInThePastIsRejectedWithFourHundred()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.MedicalCheckUp);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/slots/proposals",
            new { Date = new DateOnly(2020, 1, 1), StartTime = new TimeOnly(9, 0) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ACoordinatorGetsForbiddenFromAManagerRoute()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/slots/board");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    [Fact]
    public async Task AnAdminGetsSlotRowsAndNoCandidateData()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        await GivenSlotAsync();
        var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/slots/operations");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("confirmedSlotId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("candidateId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ACoordinatorGetsTheSameSlotOperationsView()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var slotId = await GivenSlotAsync();
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<SlotOperationsResponse>("/api/slots/operations");

        Assert.NotNull(view);
        Assert.Contains(view!.Slots, slot => slot.ConfirmedSlotId == slotId);
    }

    [Fact]
    public async Task ASignedInUserWithNoRoleAssignmentIsForbiddenFromSlotOperations()
    {
        factory.SignedInAs = Guid.NewGuid();
        factory.RolesClaim = [];
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/slots/operations");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TheTwoStageCancellationProtocolIsUnchangedForAnAdmin()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var slotId = await GivenSlotWithOneBookingAsync();
        var client = factory.CreateClient();

        using var first = await client.DeleteAsync($"/api/slots/confirmed/{slotId}?confirm=false");
        Assert.Equal(HttpStatusCode.Conflict, first.StatusCode);

        using var second = await client.DeleteAsync($"/api/slots/confirmed/{slotId}?confirm=true");
        Assert.True(second.IsSuccessStatusCode, await second.Content.ReadAsStringAsync());
    }

    /// <summary>Seeds one confirmed slot with capacity for every appointment type.</summary>
    private async Task<Guid> GivenSlotAsync()
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        context.SlotProposals.Add(proposal);
        context.ConfirmedSlots.Add(slot);
        await context.SaveChangesAsync();
        return slot.Id;
    }

    /// <summary>Seeds one confirmed slot holding a single active booking, so the cascade gate trips.</summary>
    private async Task<Guid> GivenSlotWithOneBookingAsync()
    {
        var slotId = await GivenSlotAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var pilots = await context.EmployeeGroups
            .Include(g => g.Requirements)
            .SingleAsync(g => g.Id == EmployeeGroupIds.Pilots);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "S. Booked", $"s.booked.{Guid.NewGuid():N}@mail.com", pilots);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            candidate.Id,
            $"hash-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(4),
            [slotId, Guid.NewGuid(), Guid.NewGuid()],
            candidate.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, slotId, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        candidate.MarkInvited();
        candidate.MarkBooked();

        // Mirrors ConfirmBookingHandler: one appointment per required type, each holding a place.
        var slot = await context.ConfirmedSlots
            .Include(s => s.Capacities)
            .SingleAsync(s => s.Id == slotId);

        context.Candidates.Add(candidate);
        context.Invites.Add(invite);
        context.Bookings.Add(booking);
        foreach (var appointmentTypeId in candidate.RequiredAppointmentTypeIds)
        {
            context.BookingAppointments.Add(
                BookingAppointment.Create(Guid.NewGuid(), booking.Id, appointmentTypeId));
            slot.CapacityFor(appointmentTypeId).Decrement();
        }

        await context.SaveChangesAsync();
        return slotId;
    }

    private sealed record SlotOperationsResponse(IReadOnlyList<SlotOperationsRow> Slots);

    private sealed record SlotOperationsRow(Guid ConfirmedSlotId, DateOnly Date, int ActiveBookings);

    private sealed record BoardResponse(IReadOnlyList<OpenProposalResponse> OpenProposals);

    private sealed record OpenProposalResponse(Guid ProposalId, DateOnly Date, TimeOnly StartTime);
}
`````

## tests/EventBooking.Api.Tests/StaffAccessEndpointTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/StaffAccessEndpointTests.cs","encoding":"utf8","sha256":"29d02621c7b1e1288ceedb065a708f07b567ac69cea666e988cfa66de8e3f662","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class StaffAccessEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task AdminCanListSetScopeAndClearScope()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var target = await factory.GivenStaffAsync(
            [Role.Coordinator, Role.Manager], null);
        factory.RolesClaim = ["Admin"];
        var client = factory.CreateClient();

        var list = await client.GetFromJsonAsync<List<ProfileResponse>>(
            "/api/admin/staff-access");
        Assert.Contains(list!, profile => profile.StaffUserId == target);

        var set = await client.PutAsJsonAsync(
            $"/api/admin/staff-access/{target}",
            new
            {
                AppointmentTypeId = AppointmentTypeIds.MedicalCheckUp,
                ExpectedVersion = 1L,
            });
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        var setBody = await set.Content.ReadFromJsonAsync<MutationResponse>();
        Assert.Equal(AppointmentTypeIds.MedicalCheckUp, setBody!.Profile.AppointmentTypeId);

        var stale = await client.PutAsJsonAsync(
            $"/api/admin/staff-access/{target}",
            new
            {
                AppointmentTypeId = AppointmentTypeIds.UniformFitting,
                ExpectedVersion = 1L,
            });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task PutRejectsABodyContainingARolesField()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var target = await factory.GivenStaffAsync(Role.Manager, AppointmentTypeIds.MedicalCheckUp);
        factory.RolesClaim = ["Admin"];

        var response = await factory.CreateClient().PutAsJsonAsync(
            $"/api/admin/staff-access/{target}",
            new { Roles = new[] { "Manager" }, AppointmentTypeId = AppointmentTypeIds.MedicalCheckUp, ExpectedVersion = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteClearsScopeButKeepsTheProfileAndItsRoles()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var target = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.UniformFitting);
        factory.RolesClaim = ["Admin"];
        var client = factory.CreateClient();

        var response = await client.DeleteAsync(
            $"/api/admin/staff-access/{target}?expectedVersion=1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var list = await client.GetFromJsonAsync<List<ProfileResponse>>(
            "/api/admin/staff-access");
        var profile = list!.Single(item => item.StaffUserId == target);
        Assert.Null(profile.AppointmentTypeId);
        Assert.Equal(["AppointmentStaff"], profile.Roles);
    }

    [Fact]
    public async Task DeleteOnAnAlreadyNullScopeIsRejected()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var target = await factory.GivenStaffAsync([Role.Manager], null);
        factory.RolesClaim = ["Admin"];

        var response = await factory.CreateClient().DeleteAsync(
            $"/api/admin/staff-access/{target}?expectedVersion=1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoordinatorCannotReadStaffAccess()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        factory.RolesClaim = ["Coordinator"];

        var response = await factory.CreateClient().GetAsync("/api/admin/staff-access");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    /// <summary>Verifies an observed name reaches the listing and an unobserved one stays absent.</summary>
    [Fact]
    public async Task StaffAccessListReturnsDisplayNameWhenKnown()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var named = await factory.GivenStaffAsync(Role.Coordinator);
        var unnamed = await factory.GivenStaffAsync(Role.Coordinator);
        factory.RolesClaim = ["Admin"];
        await factory.GivenIdentityAsync(named, "U000021", "Dana Datson");
        await factory.GivenIdentityAsync(unnamed, "U000022");
        var client = factory.CreateClient();

        var list = await client.GetFromJsonAsync<List<ProfileResponse>>("/api/admin/staff-access");

        Assert.NotNull(list);
        var first = list!.Single(profile => profile.StaffUserId == named);
        var second = list.Single(profile => profile.StaffUserId == unnamed);
        Assert.Equal("Dana Datson", first.DisplayName);
        Assert.Equal("U000021", first.StaffId);
        Assert.Null(second.DisplayName);
        Assert.Equal("U000022", second.StaffId);
        factory.RolesClaim = [];
    }

    private sealed record ProfileResponse(
        Guid StaffUserId,
        string? StaffId,
        IReadOnlyList<string> Roles,
        Guid? AppointmentTypeId,
        string? AppointmentTypeName,
        long Version,
        string? DisplayName = null);

    private sealed record MutationResponse(
        ProfileResponse Profile,
        Guid? FormerManagerStaffUserId);
}
`````

## tests/EventBooking.Api.Tests/StaffHypermediaTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/StaffHypermediaTests.cs","encoding":"utf8","sha256":"634b322ec28e58cecd355a1028ca92150f952cf9ab1b43ab9aedbecc20118390","parts":1,"part":1} -->

`````csharp
using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class StaffHypermediaTests(ApiFactory factory)
{
    [Fact]
    public async Task SettingsCarriesSelfAndUpdate()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        using var json = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/api/admin/settings"));
        var links = json.RootElement.GetProperty("_links");
        AssertLink(links, "self", "/api/admin/settings", "GET", "getSettings");
        AssertLink(links, "update", "/api/admin/settings", "PUT", "updateSettings");
    }

    [Fact]
    public async Task SlotCollectionsCarryEntryLinksEvenWhenEmpty()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();
        using var board = JsonDocument.Parse(await client.GetStringAsync("/api/slots/board"));
        AssertLink(board.RootElement.GetProperty("_links"), "self", "/api/slots/board", "GET", "getSlotBoard");
        using var workspace = JsonDocument.Parse(await client.GetStringAsync("/api/appointment-workspace/slots"));
        AssertLink(workspace.RootElement.GetProperty("_links"), "self",
            "/api/appointment-workspace/slots", "GET", "listAppointmentSlots");
    }

    [Fact]
    public async Task AuditSearchCarriesSelfAndOmitsNextWhenExhausted()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        using var json = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/api/audit/search?pageSize=50"));
        var links = json.RootElement.GetProperty("_links");
        AssertLink(links, "self", "/api/audit/search?pageSize=50", "GET", "searchAudit");
        Assert.False(links.TryGetProperty("next", out _));
    }

    [Fact]
    public async Task MePreservesFieldsAndAddsLinks()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        using var json = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/api/me"));
        Assert.True(json.RootElement.TryGetProperty("staffId", out _));
        Assert.True(json.RootElement.TryGetProperty("roles", out _));
        AssertLink(json.RootElement.GetProperty("_links"), "self", "/api/me", "GET", "getMyAccess");
    }

    private static void AssertLink(JsonElement links, string relation, string href, string method, string operationId)
    {
        var link = links.GetProperty(relation);
        Assert.Equal(href, link.GetProperty("href").GetString());
        Assert.Equal(method, link.GetProperty("method").GetString());
        Assert.Equal(operationId, link.GetProperty("operationId").GetString());
    }
}
`````

## tests/EventBooking.Api.Tests/StaffIdentityRecorderTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/StaffIdentityRecorderTests.cs","encoding":"utf8","sha256":"2e671dd157b26c6fbe9aef9a778f6adaa310aa95552496b478cf14541adebde2","parts":1,"part":1} -->

`````csharp
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
    [InlineData("X123456")]
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
`````

## tests/EventBooking.Api.Tests/StaffRequirementHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/StaffRequirementHandlerTests.cs","encoding":"utf8","sha256":"3ece4ca3c35c3bef5bb3c3f25400fbaa745b4656dce11cffb2967f339029915c","parts":1,"part":1} -->

`````csharp
using System.Security.Claims;
using EventBooking.Api.Auth;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventBooking.Api.Tests;

public class StaffRequirementHandlerTests
{
    [Fact]
    public async Task TheRoleLookupUsesTheRequestCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        var httpContext = new DefaultHttpContext
        {
            RequestAborted = cancellation.Token,
        };
        var roles = new CapturingStaffAccessProfileRepository();
        var handler = new StaffRequirementHandler(
            new FixedCallerAccessor(
                Guid.Parse("11111111-2222-3333-4444-555555555555"),
                new StaffId("U999999")),
            roles,
            SyncFor(roles));
        var authorizationContext = new AuthorizationHandlerContext(
            [new StaffRequirement()],
            new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test")),
            httpContext);

        await handler.HandleAsync(authorizationContext);

        Assert.Equal(cancellation.Token, roles.CapturedCancellationToken);
    }

    [Fact]
    public async Task ARevokedRoleIsAppliedWithoutCallingMeFirst()
    {
        var staffUserId = Guid.NewGuid();
        var profiles = new CapturingStaffAccessProfileRepository
        {
            Profile = StaffAccessProfile.Create(staffUserId, Role.Coordinator, null),
        };
        // The token no longer carries any role; the stored profile must not linger.
        var handler = new StaffRequirementHandler(
            new FixedCallerAccessor(staffUserId, new StaffId("U999999")),
            profiles,
            SyncFor(profiles));
        var authorizationContext = new AuthorizationHandlerContext(
            [new StaffRequirement()],
            new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test")),
            new DefaultHttpContext());

        await handler.HandleAsync(authorizationContext);

        Assert.False(authorizationContext.HasSucceeded);
    }

    [Fact]
    public async Task AProfileDoesNotAuthorizeACallerWithoutAStaffNumber()
    {
        var staffUserId = Guid.NewGuid();
        var profiles = new CapturingStaffAccessProfileRepository
        {
            Profile = StaffAccessProfile.Create(staffUserId, Role.Coordinator, null),
        };
        var handler = new StaffRequirementHandler(
            new FixedCallerAccessor(staffUserId, null),
            profiles,
            SyncFor(profiles));
        var authorizationContext = new AuthorizationHandlerContext(
            [new StaffRequirement()],
            new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test")),
            new DefaultHttpContext());

        await handler.HandleAsync(authorizationContext);

        Assert.False(authorizationContext.HasSucceeded);
    }

    private static SyncStaffAccessProfileRolesHandler SyncFor(
        IStaffAccessProfileRepository profiles) =>
        new(
            profiles,
            new NoOpUnitOfWork(),
            new NoOpAuditLogger(),
            NullLogger<SyncStaffAccessProfileRolesHandler>.Instance);

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(0);

        public Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken) =>
            Task.FromResult<ITransactionScope>(new NoOpTransactionScope());

        private sealed class NoOpTransactionScope : ITransactionScope
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;

            public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            public Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }

    private sealed class NoOpAuditLogger : IAuditLogger
    {
        public void Record(
            string entityType,
            Guid entityId,
            AuditAction action,
            ActorType actorType,
            string? actorId,
            string? details = null)
        {
        }
    }

    private sealed class FixedCallerAccessor(Guid staffUserId, StaffId? staffId = null) : ICallerAccessor
    {
        public Guid? StaffUserId => staffUserId;

        public StaffId? StaffId => staffId;

        public string? DisplayName => null;

        public IReadOnlySet<Role> Roles => new HashSet<Role>();

        public Guid RequireStaffUserId() => staffUserId;

        public StaffId RequireStaffId() => staffId
            ?? throw new InvalidOperationException("The request has no staff number.");
    }

    private sealed class CapturingStaffAccessProfileRepository : IStaffAccessProfileRepository
    {
        private readonly List<StaffAccessProfile> _items = [];

        public StaffAccessProfile? Profile
        {
            get => _items.SingleOrDefault();
            init
            {
                if (value is not null)
                {
                    _items.Add(value);
                }
            }
        }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public Task<StaffAccessProfile?> GetAsync(Guid staffUserId, CancellationToken cancellationToken)
        {
            CapturedCancellationToken = cancellationToken;
            return Task.FromResult(_items.SingleOrDefault(profile => profile.StaffUserId == staffUserId));
        }

        public Task<IReadOnlyList<StaffAccessProfile>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StaffAccessProfile>>(_items.ToList());

        public Task<IReadOnlyList<StaffAccessProfile>> LockAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StaffAccessProfile>>(_items.ToList());

        public void Add(StaffAccessProfile profile) => _items.Add(profile);

        public void Remove(StaffAccessProfile profile) => _items.Remove(profile);
    }
}
`````

## tests/EventBooking.Application.Tests/Access/AdminCandidateDataIsolationTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Access/AdminCandidateDataIsolationTests.cs","encoding":"utf8","sha256":"1232897cb477ef4d18c21476854a70623b74811730173b8be2ed8cfd7a91a615","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Candidates;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Tests.Access;

public class AdminCandidateDataIsolationTests
{
    [Fact]
    public async Task AdminCandidateListIsDeniedBeforeRepositoryInvocation()
    {
        var admin = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(admin, [Role.Admin], null));
        var candidates = new CountingCandidateRepository();
        var handler = new ListCandidatesHandler(candidates, new InMemoryEmployeeGroupRepository(), new StaffAccessAuthorizer(profiles));

        var result = await handler.HandleAsync(
            new ListCandidatesQuery(admin, null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, candidates.Calls);
    }

    [Fact]
    public async Task AdminDashboardIsDeniedBeforeAnyQueryInvocation()
    {
        var admin = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(admin, [Role.Admin], null));
        var queries = new CountingDashboardQueries();
        var handler = new GetDashboardsHandler(queries, new StaffAccessAuthorizer(profiles));

        var result = await handler.HandleAsync(
            new GetDashboardsQuery(admin), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, queries.Calls);
    }

    [Fact]
    public async Task CoordinatorCandidateListStillRuns()
    {
        var coordinator = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(coordinator, [Role.Coordinator], null));
        var candidates = new CountingCandidateRepository();
        var handler = new ListCandidatesHandler(candidates, new InMemoryEmployeeGroupRepository(), new StaffAccessAuthorizer(profiles));

        var result = await handler.HandleAsync(
            new ListCandidatesQuery(coordinator, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, candidates.Calls);
    }

    [Fact]
    public async Task AdminCanReadSlotAuditButNotCandidateAudit()
    {
        var admin = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(admin, [Role.Admin], null));
        var queries = new CountingAuditQueries();
        var handler = new GetAuditHistoryHandler(queries, new StaffAccessAuthorizer(profiles));

        var candidate = await handler.HandleAsync(
            new GetAuditHistoryQuery(admin, null, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(candidate.IsFailure);
        Assert.Equal(0, queries.Calls);

        var slot = await handler.HandleAsync(
            new GetAuditHistoryQuery(
                admin, AuditEntityTypes.ConfirmedSlot, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(slot.IsSuccess);
        Assert.Equal(1, queries.Calls);
    }

    private static InMemoryStaffAccessProfileRepository Profiles(StaffAccessProfile profile)
    {
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(profile);
        return profiles;
    }

    private sealed class CountingCandidateRepository : ICandidateRepository
    {
        public int Calls { get; private set; }

        public Task<Candidate?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<Candidate?>(null);
        }

        public Task<Candidate?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<Candidate?>(null);
        }

        public Task<Candidate?> GetByEmailAsync(string email, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<Candidate?>(null);
        }

        public Task<IReadOnlyList<Candidate>> ListAsync(
            CandidateStatus? status,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<Candidate>>([]);
        }

        public void Add(Candidate candidate) => Calls++;
        public void Remove(Candidate candidate) => Calls++;
    }

    private sealed class CountingDashboardQueries : IDashboardQueries
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<AwaitingAvailabilityRow>> AwaitingAvailabilityAsync(
            CancellationToken cancellationToken) => Return<AwaitingAvailabilityRow>();

        public Task<IReadOnlyList<NoResponseRow>> NoResponseAsync(
            CancellationToken cancellationToken) => Return<NoResponseRow>();

        public Task<IReadOnlyList<SlotOverviewRow>> SlotsOverviewAsync(
            CancellationToken cancellationToken) => Return<SlotOverviewRow>();

        public Task<IReadOnlyList<CandidateEmailStatusRow>> LatestEmailStatusAsync(
            CancellationToken cancellationToken) => Return<CandidateEmailStatusRow>();

        private Task<IReadOnlyList<T>> Return<T>()
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<T>>([]);
        }
    }

    private sealed class CountingAuditQueries : IAuditQueries
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<AuditHistoryRow>> ForEntityAsync(
            string entityType,
            Guid entityId,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<AuditHistoryRow>>([]);
        }

        public Task<IReadOnlyList<AuditHistoryRow>> ForCandidateAsync(
            Guid candidateId,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<AuditHistoryRow>>([]);
        }

        public Task<AuditSearchPage> SearchAsync(
            AuditSearchFilter filter,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new AuditSearchPage([], null));
        }
    }
}
`````

## tests/EventBooking.Application.Tests/Access/MeHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Access/MeHandlerTests.cs","encoding":"utf8","sha256":"8238d42f4d22c86f0974380740852c4710ec5c220579dc23a3f36d3bc3561e68","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventBooking.Application.Tests.Access;

public class MeHandlerTests
{
    [Fact]
    public async Task AnUnassignedCallerGetsAnEmptyRoleSet()
    {
        var me = await Handler(new InMemoryStaffAccessProfileRepository())
            .GetAsync(Guid.NewGuid(), new StaffId("U123456"), new HashSet<Role>(), CancellationToken.None);

        Assert.Equal(new StaffId("U123456"), me.StaffId);
        Assert.Empty(me.Roles);
        Assert.Null(me.AppointmentTypeId);
        Assert.Null(me.AppointmentTypeName);
    }

    [Fact]
    public async Task CombinedRolesAreOrderedAndTheSharedScopeIsNamed()
    {
        var staffUserId = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staffUserId,
            [Role.AppointmentStaff, Role.Coordinator, Role.Manager],
            AppointmentTypeIds.DrugAndAlcoholTesting));

        var me = await Handler(profiles).GetAsync(
            staffUserId,
            new StaffId("N654321"),
            new HashSet<Role> { Role.AppointmentStaff, Role.Coordinator, Role.Manager },
            CancellationToken.None);

        Assert.Equal(new StaffId("N654321"), me.StaffId);
        Assert.Equal(
            [Role.Manager, Role.Coordinator, Role.AppointmentStaff],
            me.Roles);
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, me.AppointmentTypeId);
        Assert.Equal("Drug & Alcohol Testing", me.AppointmentTypeName);
    }

    private static MeHandler Handler(InMemoryStaffAccessProfileRepository profiles) =>
        new(
            new InMemoryAppointmentTypeRepository(),
            new SyncStaffAccessProfileRolesHandler(
                profiles,
                new FakeUnitOfWork(),
                new RecordingAuditLogger(),
                NullLogger<SyncStaffAccessProfileRolesHandler>.Instance));
}
`````

## tests/EventBooking.Application.Tests/Access/StaffAccessAuthorizerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Access/StaffAccessAuthorizerTests.cs","encoding":"utf8","sha256":"115ed949e9b2846e87b111a9c38402b171fedf85400f6b8566bccd56b7bdb6fe","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Access;

public class StaffAccessAuthorizerTests
{
    private readonly InMemoryStaffAccessProfileRepository _profiles = new();

    [Theory]
    [InlineData(Role.Admin, StaffCapability.ManageSettings, true)]
    [InlineData(Role.Admin, StaffCapability.ManageCandidates, false)]
    [InlineData(Role.Admin, StaffCapability.ViewCandidateDashboards, false)]
    [InlineData(Role.Admin, StaffCapability.ImportConfirmedSlots, true)]
    [InlineData(Role.Coordinator, StaffCapability.ManageCandidates, true)]
    [InlineData(Role.Coordinator, StaffCapability.ImportConfirmedSlots, true)]
    [InlineData(Role.Coordinator, StaffCapability.ManageSettings, false)]
    [InlineData(Role.Manager, StaffCapability.ManageSlotNegotiation, true)]
    [InlineData(Role.Manager, StaffCapability.ConductAppointments, true)]
    [InlineData(Role.AppointmentStaff, StaffCapability.ConductAppointments, true)]
    [InlineData(Role.AppointmentStaff, StaffCapability.CancelConfirmedSlot, false)]
    [InlineData(Role.Admin, StaffCapability.ViewSlotOperations, true)]
    [InlineData(Role.Coordinator, StaffCapability.ViewSlotOperations, true)]
    public async Task SingleRoleCapabilitiesMatchTheMatrix(
        Role role,
        StaffCapability capability,
        bool expected)
    {
        var staffUserId = Add(role);
        var result = await Authorizer().AuthorizeAsync(
            staffUserId, capability, null, CancellationToken.None);

        Assert.Equal(expected, result.IsSuccess);
    }

    [Fact]
    public async Task CombinedCoordinatorManagerGetsTheUnionAndTrustedScope()
    {
        var staffUserId = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            staffUserId,
            [Role.Coordinator, Role.Manager],
            AppointmentTypeIds.MedicalCheckUp));

        var candidate = await Authorizer().AuthorizeAsync(
            staffUserId, StaffCapability.ManageCandidates, null, CancellationToken.None);
        var manager = await Authorizer().AuthorizeAsync(
            staffUserId,
            StaffCapability.ManageSlotNegotiation,
            AppointmentTypeIds.MedicalCheckUp,
            CancellationToken.None);

        Assert.True(candidate.IsSuccess);
        Assert.True(manager.IsSuccess);
        Assert.Equal(AppointmentTypeIds.MedicalCheckUp, manager.Value.AppointmentTypeId);
    }

    [Fact]
    public async Task AScopedCapabilityRejectsAnotherAppointmentType()
    {
        var manager = Add(Role.Manager);

        var result = await Authorizer().AuthorizeAsync(
            manager,
            StaffCapability.ManageSlotNegotiation,
            AppointmentTypeIds.UniformFitting,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public async Task AnUnassignedIdentityIsDenied()
    {
        var result = await Authorizer().AuthorizeAsync(
            Guid.NewGuid(), StaffCapability.ViewSlotOperations, null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Theory]
    [InlineData(StaffCapability.ManageSettings)]
    [InlineData(StaffCapability.ManageStaffAccess)]
    [InlineData(StaffCapability.ImportConfirmedSlots)]
    [InlineData(StaffCapability.ManageCandidates)]
    [InlineData(StaffCapability.ViewCandidateDashboards)]
    [InlineData(StaffCapability.ViewCandidateAudit)]
    [InlineData(StaffCapability.ViewSlotAudit)]
    [InlineData(StaffCapability.ManageSlotNegotiation)]
    [InlineData(StaffCapability.ViewSlotOperations)]
    [InlineData(StaffCapability.CancelConfirmedSlot)]
    [InlineData(StaffCapability.ConductAppointments)]
    public async Task AScopedCapabilityIsDeniedWhenScopeIsNull(StaffCapability capability)
    {
        var profile = StaffAccessProfile.Create(Guid.NewGuid(), [Role.Manager], null);
        var repository = new InMemoryStaffAccessProfileRepository();
        repository.Items.Add(profile);
        var authorizer = new StaffAccessAuthorizer(repository);

        var result = await authorizer.AuthorizeAsync(
            profile.StaffUserId, capability, requiredAppointmentTypeId: null, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task AnUnscopedCoordinatorCapabilityIsStillGrantedWhenScopeIsNull()
    {
        var profile = StaffAccessProfile.Create(
            Guid.NewGuid(), [Role.Coordinator, Role.Manager], null);
        var repository = new InMemoryStaffAccessProfileRepository();
        repository.Items.Add(profile);
        var authorizer = new StaffAccessAuthorizer(repository);

        var result = await authorizer.AuthorizeAsync(
            profile.StaffUserId,
            StaffCapability.ManageCandidates,
            requiredAppointmentTypeId: null,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    private Guid Add(Role role)
    {
        var id = Guid.NewGuid();
        Guid? scope = role is Role.Manager or Role.AppointmentStaff
            ? AppointmentTypeIds.DrugAndAlcoholTesting
            : null;
        _profiles.Add(StaffAccessProfile.Create(id, role, scope));
        return id;
    }

    private StaffAccessAuthorizer Authorizer() => new(_profiles);
}
`````
