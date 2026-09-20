# 00a — Port source 55 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Api.Tests/ApiFactory.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/ApiFactory.cs","encoding":"utf8","sha256":"0593bb1d8a1f27f01223624f5d00eb8cf454a9b0207dc9c93717c9fc7d85a751","parts":1,"part":1} -->

`````csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using EventBooking.Api.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace EventBooking.Api.Tests;

/// <summary>
/// Starts the real host against a throwaway PostgreSQL container, with Entra ID replaced by a test
/// authentication scheme so a test can say who is calling by setting <see cref="SignedInAs"/>.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    /// <summary>The Entra object identifier every request is made as. Null means anonymous.</summary>
    public Guid? SignedInAs { get; set; }

    /// <summary>The untrusted <c>staff_id</c> claim attached to authenticated test requests.</summary>
    public string? StaffIdClaim { get; set; } = "U999999";

    /// <summary>The untrusted <c>roles</c> claim values attached to authenticated test requests.</summary>
    public IReadOnlyCollection<string> RolesClaim { get; set; } = [];

    /// <summary>The untrusted <c>name</c> claim attached to authenticated test requests.</summary>
    public string? NameClaim { get; set; }

    /// <summary>Stands in for AWS SES; a test can inspect what would have been sent.</summary>
    public RecordingEmailTransport EmailTransport { get; } = new();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        await context.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    public async Task<Guid> GivenStaffAsync(Role role, Guid? appointmentTypeId = null) =>
        await GivenStaffAsync([role], appointmentTypeId);

    public async Task<Guid> GivenStaffAsync(
        IReadOnlyCollection<Role> roles,
        Guid? appointmentTypeId) =>
        await GivenStaffWithIdAsync(Guid.NewGuid(), roles, appointmentTypeId);

    public async Task<Guid> GivenStaffWithIdAsync(
        Guid staffUserId,
        IReadOnlyCollection<Role> roles,
        Guid? appointmentTypeId)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        if (roles.Contains(Role.Manager) && appointmentTypeId is not null)
        {
            var previous = await context.StaffAccessProfiles.SingleOrDefaultAsync(profile =>
                profile.IsManager && profile.AppointmentTypeId == appointmentTypeId);
            if (previous is not null)
            {
                context.StaffAccessProfiles.Remove(previous);
            }
        }

        context.StaffAccessProfiles.Add(
            StaffAccessProfile.Create(staffUserId, roles, appointmentTypeId));
        await context.SaveChangesAsync();

        // Staff requests reconcile the stored profile with the token's roles before
        // authorizing, so a seeded profile must arrive with matching token roles —
        // exactly as a production token carries the identity-provider roles the
        // profile mirrors. Tests asserting a mismatch assign RolesClaim afterwards.
        RolesClaim = roles.Select(role => role.ToString()).ToList();
        return staffUserId;
    }

    /// <summary>Stores a staff-number/provider-key pair as though the identity had signed in.</summary>
    public async Task GivenIdentityAsync(Guid staffUserId, string staffId, string? displayName = null)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        context.StaffIdentities.Add(StaffIdentity.Create(
            staffUserId,
            new StaffId(staffId),
            displayName,
            DateTimeOffset.Parse("2026-09-08T10:00:00Z")));
        await context.SaveChangesAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:EventBooking", _container.GetConnectionString());
        builder.UseSetting("Tokens:SigningKey", "a-test-signing-key-that-is-long-enough-here");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(this);
            services
                .AddAuthentication(TestAuthenticationHandler.Scheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.Scheme, _ => { });

            // The real transport constructs an AWS SES client that throws immediately outside an
            // AWS environment (no RegionEndpoint or ServiceURL configured) — exactly where CI runs.
            services.AddSingleton<IEmailTransport>(EmailTransport);
        });
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ApiFactory factory) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (factory.SignedInAs is null)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new("oid", factory.SignedInAs.Value.ToString()),
            };
            if (factory.StaffIdClaim is not null)
            {
                claims.Add(new Claim("staff_id", factory.StaffIdClaim));
            }
            if (factory.NameClaim is not null)
            {
                claims.Add(new Claim("name", factory.NameClaim));
            }
            foreach (var role in factory.RolesClaim)
            {
                claims.Add(new Claim("roles", role));
            }

            var identity = new ClaimsIdentity(claims, Scheme);

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
`````

## tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs","encoding":"utf8","sha256":"3d92c46770ca227dc36f8145e31d97d47a38e5998a6df1648b0235343422f4f8","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventBooking.Application.Abstractions;
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

/// <summary>Verifies authorization, shape, validation, and updates at the workspace HTTP boundary.</summary>
[Collection("api")]
public sealed class AppointmentWorkspaceEndpointTests(ApiFactory factory)
{
    /// <summary>Defines the single and combined profiles that may or may not use the workspace.</summary>
    public static TheoryData<Role[], HttpStatusCode> ReadMatrix => new()
    {
        { [Role.Manager], HttpStatusCode.OK },
        { [Role.AppointmentStaff], HttpStatusCode.OK },
        { [Role.Manager, Role.AppointmentStaff], HttpStatusCode.OK },
        { [Role.Coordinator, Role.AppointmentStaff], HttpStatusCode.OK },
        { [Role.Coordinator, Role.Manager, Role.AppointmentStaff], HttpStatusCode.OK },
        { [Role.Coordinator], HttpStatusCode.Forbidden },
        { [Role.Admin], HttpStatusCode.Forbidden },
    };

    /// <summary>Verifies the role union while keeping one trusted appointment-type scope.</summary>
    [Theory]
    [MemberData(nameof(ReadMatrix))]
    public async Task RoleMatrixProtectsTheSlotList(Role[] roles, HttpStatusCode expected)
    {
        Guid? scope = roles.Any(role => role is Role.Manager or Role.AppointmentStaff)
            ? AppointmentTypeIds.DrugAndAlcoholTesting
            : null;
        factory.SignedInAs = await factory.GivenStaffAsync(roles, scope);

        using var response = await factory.CreateClient()
            .GetAsync("/api/appointment-workspace/slots");

        Assert.Equal(expected, response.StatusCode);
    }

    /// <summary>Verifies anonymous and unassigned identities receive no workspace data.</summary>
    [Fact]
    public async Task AnonymousAndUnassignedCallersAreRejected()
    {
        factory.SignedInAs = null;
        using var anonymous = await factory.CreateClient()
            .GetAsync("/api/appointment-workspace/slots");
        factory.SignedInAs = Guid.NewGuid();
        using var unassigned = await factory.CreateClient()
            .GetAsync("/api/appointment-workspace/slots");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unassigned.StatusCode);
    }

    /// <summary>Verifies list/detail JSON contains exactly the approved minimum-data properties.</summary>
    [Fact]
    public async Task ResponsesHaveTheExactApprovedPropertySets()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var list = JsonDocument.Parse(await client.GetStringAsync(
            "/api/appointment-workspace/slots"));
        AssertKeys(list.RootElement, "appointmentTypeName", "slots", "_links");
        var slot = Assert.Single(
            list.RootElement.GetProperty("slots").EnumerateArray(),
            item => item.GetProperty("confirmedSlotId").GetString() == data.SlotId.ToString());
        AssertKeys(slot, "confirmedSlotId", "date", "startTime", "endTime", "counts", "_links");
        AssertKeys(slot.GetProperty("counts"), "expected", "checkedIn", "completed", "noShow");

        using var detail = JsonDocument.Parse(await client.GetStringAsync(
            $"/api/appointment-workspace/slots/{data.SlotId}"));
        AssertKeys(detail.RootElement,
            "appointmentTypeName", "confirmedSlotId", "date", "startTime", "endTime", "appointments", "_links");
        var row = Assert.Single(detail.RootElement.GetProperty("appointments").EnumerateArray());
        AssertKeys(row, "bookingAppointmentId", "candidateName", "candidateEmail",
            "status", "checkedInAt", "outcomeAt", "version", "_links");
        Assert.Equal("Expected", row.GetProperty("status").GetString());
        Assert.DoesNotContain("candidateId", detail.RootElement.GetRawText());
        Assert.DoesNotContain("bookingId", detail.RootElement.GetRawText());
        Assert.DoesNotContain("requirement", detail.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies the update body is parsed, applied, and returned as a named state.</summary>
    [Fact]
    public async Task CheckInReturnsOnlyTheRowLocalState()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient().PutAsJsonAsync(
            $"/api/appointment-workspace/appointments/{data.AppointmentId}/status",
            new { status = "CheckedIn", expectedVersion = 1 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        AssertKeys(json.RootElement,
            "bookingAppointmentId", "status", "checkedInAt", "outcomeAt", "version", "_links");
        Assert.Equal("CheckedIn", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(2, json.RootElement.GetProperty("version").GetInt64());
    }

    /// <summary>Verifies malformed bodies fail with validation and expose no record existence.</summary>
    [Theory]
    [InlineData("Unknown", 1)]
    [InlineData("CheckedIn", 0)]
    public async Task MalformedUpdateReturnsBadRequest(string status, long expectedVersion)
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient().PutAsJsonAsync(
            $"/api/appointment-workspace/appointments/{Guid.NewGuid()}/status",
            new { status, expectedVersion });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Verifies missing and cross-type identifiers have indistinguishable responses.</summary>
    [Fact]
    public async Task CrossTypeAndMissingAppointmentsShareOneNotFoundResponse()
    {
        var other = await GivenWorkspaceAsync(AppointmentTypeIds.MedicalCheckUp);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var crossType = await client.PutAsJsonAsync(
            $"/api/appointment-workspace/appointments/{other.AppointmentId}/status",
            new { status = "CheckedIn", expectedVersion = 1 });
        using var missing = await client.PutAsJsonAsync(
            $"/api/appointment-workspace/appointments/{Guid.NewGuid()}/status",
            new { status = "CheckedIn", expectedVersion = 1 });

        Assert.Equal(HttpStatusCode.NotFound, crossType.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var crossProblem = JsonDocument.Parse(await crossType.Content.ReadAsStringAsync());
        using var missingProblem = JsonDocument.Parse(await missing.Content.ReadAsStringAsync());
        Assert.Equal(
            missingProblem.RootElement.GetProperty("title").GetString(),
            crossProblem.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            missingProblem.RootElement.GetProperty("detail").GetString(),
            crossProblem.RootElement.GetProperty("detail").GetString());
    }

    /// <summary>Seeds one active slot, candidate, booking, and scoped appointment row.</summary>

    /// <summary>Verifies the roster route is protected by the same role matrix as the slot list.</summary>
    [Theory]
    [MemberData(nameof(ReadMatrix))]
    public async Task RoleMatrixProtectsTheRoster(Role[] roles, HttpStatusCode expected)
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        Guid? scope = roles.Any(role => role is Role.Manager or Role.AppointmentStaff)
            ? AppointmentTypeIds.DrugAndAlcoholTesting
            : null;
        factory.SignedInAs = await factory.GivenStaffAsync(roles, scope);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/slots/{data.SlotId}/roster");

        Assert.Equal(expected, response.StatusCode);
    }

    /// <summary>Verifies a scoped caller downloads the roster with CSV headers and a named file.</summary>
    [Fact]
    public async Task RosterReturnsScopedCsvWithHeaders()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/slots/{data.SlotId}/roster");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType!.CharSet);

        var disposition = response.Content.Headers.ContentDisposition!;
        Assert.Equal("attachment", disposition.DispositionType);
        var fileName = (disposition.FileNameStar ?? disposition.FileName)!.Trim('"');
        Assert.StartsWith("roster-drug-&-alcohol-testing-", fileName, StringComparison.Ordinal);
        Assert.EndsWith("-0900.csv", fileName, StringComparison.Ordinal);

        var body = await response.Content.ReadAsStringAsync();
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(
            "Candidate Name,Candidate Email,Appointment Type,Status,Checked In At,Outcome At",
            lines[0]);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Alex Morgan,", lines[1], StringComparison.Ordinal);
        Assert.Contains(",Drug & Alcohol Testing,Expected,", lines[1], StringComparison.Ordinal);
        Assert.EndsWith(",,", lines[1], StringComparison.Ordinal);
    }

    /// <summary>Verifies the roster never carries the write path's command target or version.</summary>
    [Fact]
    public async Task RosterOmitsTheCommandTargetAndConcurrencyToken()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/slots/{data.SlotId}/roster");

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(data.AppointmentId.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Version", body, StringComparison.Ordinal);
    }

    /// <summary>Verifies a caller scoped to another appointment type cannot learn the slot exists.</summary>
    [Fact]
    public async Task RosterCrossTypeRequestReturnsNotFound()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.UniformFitting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/slots/{data.SlotId}/roster");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies an unknown slot is refused the same way the JSON detail route refuses it.</summary>
    [Fact]
    public async Task RosterMissingSlotReturnsNotFound()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/slots/{Guid.NewGuid()}/roster");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies an anonymous caller is challenged before any candidate data is read.</summary>
    [Fact]
    public async Task RosterAnonymousCallerIsUnauthorized()
    {
        factory.SignedInAs = null;

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/slots/{Guid.NewGuid()}/roster");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Verifies a free-text candidate name with a comma and a quote survives the CSV.</summary>
    [Fact]
    public async Task RosterEscapesCommaAndQuoteInCandidateName()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, "Okafor, Ada \"Bisi\"");
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/slots/{data.SlotId}/roster");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"Okafor, Ada \"\"Bisi\"\"\",", body, StringComparison.Ordinal);
    }


    /// <summary>Verifies appointment staff download their own scope and cannot reach another's.</summary>
    [Fact]
    public async Task RosterAppointmentStaffIsScopedToItsOwnAppointmentType()
    {
        var inScope = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        var outOfScope = await GivenWorkspaceAsync(AppointmentTypeIds.MedicalCheckUp);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var allowed = await client
            .GetAsync($"/api/appointment-workspace/slots/{inScope.SlotId}/roster");
        using var refused = await client
            .GetAsync($"/api/appointment-workspace/slots/{outOfScope.SlotId}/roster");

        allowed.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", allowed.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
    }

    /// <summary>Verifies a signed-in identity with no access profile downloads nothing.</summary>
    [Fact]
    public async Task RosterUnassignedProfileIsForbidden()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = Guid.NewGuid();

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/slots/{data.SlotId}/roster");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<(Guid SlotId, Guid AppointmentId)> GivenWorkspaceAsync(
        Guid appointmentTypeId,
        string candidateName = "Alex Morgan")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtHeadOffice;
        var slot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(today, new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var groupId = appointmentTypeId == AppointmentTypeIds.MedicalCheckUp
            ? EmployeeGroupIds.GroundOperationsAgent
            : EmployeeGroupIds.Pilots;
        var group = context.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == groupId);
        var candidate = Candidate.Create(
            Guid.NewGuid(), candidateName, $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [slot.Id, Guid.NewGuid(), Guid.NewGuid()],
            candidate.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, slot.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);
        context.AddRange(slot, candidate, booking, appointment);
        await context.SaveChangesAsync();
        return (slot.Id, appointment.Id);
    }

    /// <summary>Asserts a JSON object carries exactly the approved property names.</summary>
    private static void AssertKeys(JsonElement value, params string[] expected) =>
        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
}
`````

## tests/EventBooking.Api.Tests/AuditEndpointTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/AuditEndpointTests.cs","encoding":"utf8","sha256":"3c505f046c94aaa0a6b9bdac0fdd2378e0bce751bdd6e4b83dc6bb2f24cfc7af","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;
using EventBooking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class AuditEndpointTests(ApiFactory factory)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ACoordinatorGetsASlotsHistoryWithEveryFieldThePanelBindsTo()
    {
        var slotId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            context.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, slotId, AuditAction.SlotConfirmed,
                ActorType.Staff, "staff-1", Now, "6 headcount total"));
            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var rows = await client.GetFromJsonAsync<List<RowResponse>>($"/api/audit/slot/{slotId}");

        var row = Assert.Single(rows!);
        Assert.Equal(Now, row.Timestamp);
        Assert.Equal(AuditEntityTypes.ConfirmedSlot, row.EntityType);
        Assert.Equal(slotId, row.EntityId);
        Assert.Equal("SlotConfirmed", row.Action);
        Assert.Equal("Staff", row.ActorType);
        Assert.Equal("staff-1", row.ActorId);
        Assert.Equal("6 headcount total", row.Details);
    }

    [Fact]
    public async Task ACoordinatorGetsACandidatesHistoryFromItsInvitesAndBookings()
    {
        var candidateId = Guid.NewGuid();
        var inviteId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var pilots = context.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
            var candidate = Candidate.Create(candidateId, "Amara Novak", "a.novak@mail.com", pilots);
            context.Candidates.Add(candidate);
            context.Invites.Add(Invite.CreateInitial(
                inviteId, candidate.Id, "hash", Now.AddDays(4),
                [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
                candidate.RequiredAppointmentTypeIds, 0));
            context.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Invite, inviteId, AuditAction.InviteCreated,
                ActorType.System, null, Now, "retry 0"));
            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var rows = await client.GetFromJsonAsync<List<RowResponse>>($"/api/audit/candidate/{candidateId}");

        var row = Assert.Single(rows!);
        Assert.Equal("InviteCreated", row.Action);
        Assert.Equal("System", row.ActorType);
        Assert.Null(row.ActorId);
        Assert.Equal("retry 0", row.Details);
    }

    [Fact]
    public async Task AManagerIsForbiddenFromEitherAuditRoute()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.MedicalCheckUp);
        var client = factory.CreateClient();

        var slotResponse = await client.GetAsync($"/api/audit/slot/{Guid.NewGuid()}");
        var candidateResponse = await client.GetAsync($"/api/audit/candidate/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, slotResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, candidateResponse.StatusCode);
    }

    [Fact]
    public async Task AnUnauthenticatedCallerIsChallenged()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/audit/slot/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task SearchParsesQueryParametersAndReturnsNewestFirst()
    {
        var actorId = $"search-actor-{Guid.NewGuid():N}";
        var older = Guid.NewGuid();
        var newer = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            context.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, older, AuditAction.SlotConfirmed,
                ActorType.Staff, actorId, Now, null));
            context.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, newer, AuditAction.SlotConfirmed,
                ActorType.Staff, actorId, Now.AddHours(1), null));
            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<SearchPageResponse>(
            $"/api/audit/search?action=SlotConfirmed&actorType=Staff&identifier={actorId}&pageSize=10");

        Assert.NotNull(page);
        Assert.Equal(2, page!.Rows.Count);
        Assert.Equal(newer, page.Rows[0].EntityId);
        Assert.Equal(older, page.Rows[1].EntityId);
    }

    [Fact]
    public async Task SearchHonoursTheFromAndToBounds()
    {
        var actorId = $"search-actor-{Guid.NewGuid():N}";
        var inRange = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            context.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, inRange, AuditAction.SlotConfirmed,
                ActorType.Staff, actorId, Now, null));
            context.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, Guid.NewGuid(), AuditAction.SlotConfirmed,
                ActorType.Staff, actorId, Now.AddDays(-30), null));
            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<SearchPageResponse>(
            $"/api/audit/search?identifier={actorId}"
            + $"&from={Uri.EscapeDataString(Now.AddHours(-1).ToString("O"))}"
            + $"&to={Uri.EscapeDataString(Now.AddHours(1).ToString("O"))}");

        Assert.NotNull(page);
        var row = Assert.Single(page!.Rows);
        Assert.Equal(inRange, row.EntityId);
    }

    [Fact]
    public async Task SearchClampsOversizePageSize()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/audit/search?pageSize=1000");

        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<SearchPageResponse>();
        Assert.NotNull(page);
        Assert.True(page!.Rows.Count <= 200);
    }

    [Fact]
    public async Task SearchWithMalformedCursorRestartsFromNewest()
    {
        var slotId = Guid.NewGuid();
        var actorId = $"search-actor-{Guid.NewGuid():N}";

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            context.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, slotId, AuditAction.SlotConfirmed,
                ActorType.Staff, actorId, Now, null));
            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<SearchPageResponse>(
            $"/api/audit/search?identifier={actorId}&cursor=not-valid-base64!!");

        Assert.NotNull(page);
        Assert.Contains(page!.Rows, r => r.EntityId == slotId);
    }

    [Fact]
    public async Task SearchRejectsAnUnparsableTimestampBound()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/audit/search?from=yesterday");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchNeverReturnsCandidateRowsToAnAdmin()
    {
        var bookingId = Guid.NewGuid();
        var actorId = $"search-actor-{Guid.NewGuid():N}";

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            context.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Booking, bookingId, AuditAction.BookingCreated,
                ActorType.Staff, actorId, Now, null));
            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<SearchPageResponse>(
            $"/api/audit/search?identifier={actorId}");

        Assert.NotNull(page);
        Assert.Empty(page!.Rows);

        var forbidden = await client.GetAsync(
            $"/api/audit/search?entityType={AuditEntityTypes.Booking}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task AManagerIsForbiddenFromSearch()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.UniformFitting);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/audit/search");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed record SearchPageResponse(List<RowResponse> Rows, string? NextCursor);

    private sealed record RowResponse(
        DateTimeOffset Timestamp,
        string EntityType,
        Guid EntityId,
        string Action,
        string ActorType,
        string? ActorId,
        string? Details);
}
`````

## tests/EventBooking.Api.Tests/AuthorizationMatrixTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/AuthorizationMatrixTests.cs","encoding":"utf8","sha256":"1a7458a3254cef1aa9cf1ee16b9b1542c8f0cda6ff87d0ee716ccbeb8b5b248d","parts":1,"part":1} -->

`````csharp
using System.Net;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class AuthorizationMatrixTests(ApiFactory factory)
{
    public static TheoryData<Role, string, HttpStatusCode> Matrix => new()
    {
        { Role.Admin, "/api/candidates", HttpStatusCode.Forbidden },
        { Role.Admin, "/api/dashboards", HttpStatusCode.Forbidden },
        { Role.Admin, $"/api/audit/candidate/{Guid.NewGuid()}", HttpStatusCode.Forbidden },
        { Role.Admin, "/api/admin/settings", HttpStatusCode.OK },
        { Role.Coordinator, "/api/candidates", HttpStatusCode.OK },
        { Role.Coordinator, "/api/dashboards", HttpStatusCode.OK },
        { Role.Coordinator, "/api/admin/settings", HttpStatusCode.Forbidden },
        { Role.Manager, "/api/slots/board", HttpStatusCode.OK },
        { Role.AppointmentStaff, "/api/slots/board", HttpStatusCode.Forbidden },
        { Role.AppointmentStaff, "/api/candidates", HttpStatusCode.Forbidden },
    };

    public static TheoryData<Role[], string, HttpStatusCode> CombinedMatrix => new()
    {
        { [Role.Coordinator, Role.Manager], "/api/candidates", HttpStatusCode.OK },
        { [Role.Coordinator, Role.Manager], "/api/slots/board", HttpStatusCode.OK },
        { [Role.Coordinator, Role.AppointmentStaff], "/api/candidates", HttpStatusCode.OK },
        { [Role.Coordinator, Role.AppointmentStaff], "/api/slots/board", HttpStatusCode.Forbidden },
        { [Role.Manager, Role.AppointmentStaff], "/api/slots/board", HttpStatusCode.OK },
        { [Role.Manager, Role.AppointmentStaff], "/api/candidates", HttpStatusCode.Forbidden },
        { [Role.Coordinator, Role.Manager, Role.AppointmentStaff], "/api/candidates", HttpStatusCode.OK },
        { [Role.Coordinator, Role.Manager, Role.AppointmentStaff], "/api/slots/board", HttpStatusCode.OK },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task SingleRoleEndpointMatrix(
        Role role,
        string route,
        HttpStatusCode expected)
    {
        Guid? scope = role is Role.Manager or Role.AppointmentStaff
            ? AppointmentTypeIds.DrugAndAlcoholTesting
            : null;
        factory.SignedInAs = await factory.GivenStaffAsync(role, scope);

        var response = await factory.CreateClient().GetAsync(route);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(CombinedMatrix))]
    public async Task CombinedProfileEndpointMatrix(
        Role[] roles,
        string route,
        HttpStatusCode expected)
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            roles,
            AppointmentTypeIds.DrugAndAlcoholTesting);

        var response = await factory.CreateClient().GetAsync(route);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task UnassignedAndAnonymousCallersRemainProtected()
    {
        factory.SignedInAs = Guid.NewGuid();
        factory.RolesClaim = [];
        var unassigned = await factory.CreateClient().GetAsync("/api/candidates");

        factory.SignedInAs = null;
        var anonymous = await factory.CreateClient().GetAsync("/api/candidates");

        Assert.Equal(HttpStatusCode.Forbidden, unassigned.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }
}
`````

## tests/EventBooking.Api.Tests/BookingEndpointTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/BookingEndpointTests.cs","encoding":"utf8","sha256":"a5961b374719335ddf48f7562ce3049739c4c1b07145cc167428cf8f6d9ef399","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;
using Microsoft.EntityFrameworkCore;
using EventBooking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class BookingEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task AValidTokenReturnsOnlyTheCandidateFacingOptionsEarliestFirst()
    {
        var invite = await GivenAnInvitedCandidate();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/booking/{invite.Token}");
        var json = await response.Content.ReadAsStringAsync();
        var view = JsonSerializer.Deserialize<InviteResponse>(json, JsonSerializerOptions.Web);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(view);

        // Catches an incomplete root projection that drops the invite or candidate identity.
        Assert.Equal(invite.Id, view!.InviteId);
        Assert.Equal("Amara Novak", view!.CandidateName);

        // Catches projecting no appointment types or types from the wrong source.
        Assert.Equal(["Drug & Alcohol Testing", "Uniform Fitting"], view.AppointmentTypeNames);

        // Catches removing chronological ordering or ordering by the offered list or slot ID.
        Assert.Equal(
            [
                Guid.Parse("00000000-0000-0000-0000-000000000010"),
                Guid.Parse("00000000-0000-0000-0000-000000000090"),
                Guid.Parse("00000000-0000-0000-0000-000000000050"),
            ],
            view.Options.Select(option => option.ConfirmedSlotId));
        Assert.Equal(
            [new DateOnly(2030, 1, 14), new DateOnly(2030, 1, 15), new DateOnly(2030, 1, 16)],
            view.Options.Select(option => option.Date));

        // Catches omitted or incorrectly mapped candidate-facing window fields.
        Assert.Equal(
            [new TimeOnly(9, 0), new TimeOnly(11, 0), new TimeOnly(13, 0)],
            view.Options.Select(option => option.StartTime));
        Assert.Equal(
            [new TimeOnly(13, 0), new TimeOnly(15, 0), new TimeOnly(17, 0)],
            view.Options.Select(option => option.EndTime));
        Assert.Equal(
            [
                "Monday 14 Jan 2030, 09:00-13:00",
                "Tuesday 15 Jan 2030, 11:00-15:00",
                "Wednesday 16 Jan 2030, 13:00-17:00",
            ],
            view.Options.Select(option => option.Display));

        // Catches returning a domain slot/capacity object instead of the candidate-facing projection.
        using var document = JsonDocument.Parse(json);
        var links = document.RootElement.GetProperty("_links");
        var confirm = links.GetProperty("confirm");
        Assert.Equal($"/api/booking/{Uri.EscapeDataString(invite.Token)}/confirm", confirm.GetProperty("href").GetString());
        Assert.Equal("POST", confirm.GetProperty("method").GetString());
        Assert.Equal("confirmBooking", confirm.GetProperty("operationId").GetString());
        Assert.DoesNotContain("tokenHash", json, StringComparison.OrdinalIgnoreCase);
        AssertNoPropertiesNamed(
            document.RootElement,
            "capacities",
            "remainingCapacity",
            "totalHeadcount",
            "headcount",
            "status",
            "id",
            "proposalId",
            "window",
            "acceptances",
            "createdByManagerUserId",
            "candidateId",
            "tokenHash",
            "offeredSlotIds",
            "expiresAt",
            "usedAt");
    }

    [Fact]
    public async Task AnUnknownTokenIsNotFoundWithTheGenericMessage()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/booking/nonsense");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("no longer valid", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheRouteNeedsNoAuthentication()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/booking/nonsense");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Seeds three deliberately non-ID-ordered slots, a candidate and a pending invite.</summary>
    internal async Task<InviteFixture> GivenAnInvitedCandidate()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var offeredSlots = new[]
        {
            new InviteOptionFixture(
                Guid.Parse("00000000-0000-0000-0000-000000000050"), new DateOnly(2030, 1, 16), new TimeOnly(13, 0)),
            new InviteOptionFixture(
                Guid.Parse("00000000-0000-0000-0000-000000000010"), new DateOnly(2030, 1, 14), new TimeOnly(9, 0)),
            new InviteOptionFixture(
                Guid.Parse("00000000-0000-0000-0000-000000000090"), new DateOnly(2030, 1, 15), new TimeOnly(11, 0)),
        };

        foreach (var offeredSlot in offeredSlots)
        {
            var proposal = SlotProposal.Create(
                Guid.NewGuid(),
                new SlotWindow(offeredSlot.Date, offeredSlot.StartTime),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            var slot = ConfirmedSlot.CreateFrom(offeredSlot.Id, proposal);
            context.SlotProposals.Add(proposal);
            context.ConfirmedSlots.Add(slot);
        }

        var pilots = context.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", $"{Guid.NewGuid():N}@mail.com", pilots);
        candidate.MarkInvited();
        context.Candidates.Add(candidate);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        context.Invites.Add(Invite.CreateInitial(
            inviteId, candidate.Id, issued.TokenHash, DateTimeOffset.UtcNow.AddDays(4),
            offeredSlots.Select(slot => slot.Id).ToList(),
            candidate.RequiredAppointmentTypeIds, 0));

        await context.SaveChangesAsync();

        return new InviteFixture(issued.Token, inviteId);
    }

    private static void AssertNoPropertiesNamed(JsonElement element, params string[] forbiddenNames)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                Assert.DoesNotContain(property.Name, forbiddenNames);
                AssertNoPropertiesNamed(property.Value, forbiddenNames);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                AssertNoPropertiesNamed(item, forbiddenNames);
            }
        }
    }

    internal sealed record InviteFixture(string Token, Guid Id);

    internal sealed record InviteOptionFixture(Guid Id, DateOnly Date, TimeOnly StartTime);

    internal sealed record InviteOptionResponse(
        Guid ConfirmedSlotId,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        string Display);

    internal sealed record InviteResponse(
        Guid InviteId,
        string CandidateName,
        IReadOnlyList<string> AppointmentTypeNames,
        IReadOnlyList<InviteOptionResponse> Options);
}
`````

## tests/EventBooking.Api.Tests/CallerAccessorTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/CallerAccessorTests.cs","encoding":"utf8","sha256":"1322496ba5b1a76bccf5e166ec06bf472903e55467ced26c7f65fb15f3b364e5","parts":1,"part":1} -->

`````csharp
using System.Security.Claims;
using EventBooking.Api.Auth;
using EventBooking.Domain.Access;

namespace EventBooking.Api.Tests;

public class CallerAccessorTests
{
    private static readonly Guid ObjectId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void TheShortObjectIdentifierClaimIsRead()
    {
        var principal = PrincipalWith(HttpContextCallerAccessor.ShortObjectIdClaim, ObjectId.ToString());

        Assert.Equal(ObjectId, HttpContextCallerAccessor.StaffUserIdOf(principal));
    }

    [Fact]
    public void TheLongObjectIdentifierClaimIsRead()
    {
        var principal = PrincipalWith(HttpContextCallerAccessor.ObjectIdClaim, ObjectId.ToString());

        Assert.Equal(ObjectId, HttpContextCallerAccessor.StaffUserIdOf(principal));
    }

    [Fact]
    public void AnUnauthenticatedPrincipalHasNoStaffIdentity()
    {
        Assert.Null(HttpContextCallerAccessor.StaffUserIdOf(null));
        Assert.Null(HttpContextCallerAccessor.StaffUserIdOf(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Fact]
    public void AnUnauthenticatedIdentityObjectIdentifierIsIgnored()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(HttpContextCallerAccessor.ShortObjectIdClaim, ObjectId.ToString())]));

        Assert.Null(HttpContextCallerAccessor.StaffUserIdOf(principal));
    }

    [Fact]
    public void AnObjectIdentifierCannotBeReadFromAnIdentityOtherThanTheAuthenticatedIdentity()
    {
        var principal = new ClaimsPrincipal(
        [
            new ClaimsIdentity([new Claim(HttpContextCallerAccessor.ShortObjectIdClaim, ObjectId.ToString())]),
            new ClaimsIdentity(authenticationType: "test"),
        ]);

        Assert.Null(HttpContextCallerAccessor.StaffUserIdOf(principal));
    }

    [Fact]
    public void AClaimThatIsNotAnIdentifierIsIgnored()
    {
        var principal = PrincipalWith(HttpContextCallerAccessor.ShortObjectIdClaim, "not-a-guid");

        Assert.Null(HttpContextCallerAccessor.StaffUserIdOf(principal));
    }

    private static ClaimsPrincipal PrincipalWith(string type, string value) =>
        new(new ClaimsIdentity([new Claim(type, value)], "test"));

    [Fact]
    public void KnownRoleClaimValuesAreParsed()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(HttpContextCallerAccessor.RolesClaim, "Coordinator"),
                new Claim(HttpContextCallerAccessor.RolesClaim, "Manager"),
            ],
            "test"));

        var roles = HttpContextCallerAccessor.RolesOf(principal);

        Assert.Equal(new HashSet<Role> { Role.Coordinator, Role.Manager }, roles);
    }

    [Fact]
    public void AnUnrecognisedRoleClaimValueIsDroppedNotThrown()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(HttpContextCallerAccessor.RolesClaim, "Coordinator"),
                new Claim(HttpContextCallerAccessor.RolesClaim, "SuperUser"),
            ],
            "test"));

        var rejected = new List<string>();
        var roles = HttpContextCallerAccessor.RolesOf(principal, rejected.Add);

        Assert.Equal(new HashSet<Role> { Role.Coordinator }, roles);
        Assert.Equal(["SuperUser"], rejected);
    }

    [Fact]
    public void AnAbsentRolesClaimYieldsAnEmptySet()
    {
        var principal = PrincipalWith(HttpContextCallerAccessor.ShortObjectIdClaim, ObjectId.ToString());

        Assert.Empty(HttpContextCallerAccessor.RolesOf(principal));
        Assert.Empty(HttpContextCallerAccessor.RolesOf(null));
    }

    [Fact]
    public void GroupsClaimAloneDoesNotCreateAnApplicationRole()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("groups", "Manager")],
            "test"));

        Assert.Empty(HttpContextCallerAccessor.RolesOf(principal));
    }

    [Fact]
    public void RolesAreReadFromALaterAuthenticatedIdentityWhenTheFirstHasNoRolesClaim()
    {
        var principal = new ClaimsPrincipal(
        [
            new ClaimsIdentity(
                [new Claim(HttpContextCallerAccessor.ShortObjectIdClaim, ObjectId.ToString())], "test"),
            new ClaimsIdentity(
                [new Claim(HttpContextCallerAccessor.RolesClaim, "Manager")], "test"),
        ]);

        var roles = HttpContextCallerAccessor.RolesOf(principal);

        Assert.Equal(new HashSet<Role> { Role.Manager }, roles);
    }

    [Fact]
    public void RoleNamesAreCaseSensitiveAndDuplicatesCollapse()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(HttpContextCallerAccessor.RolesClaim, "Manager"),
                new Claim(HttpContextCallerAccessor.RolesClaim, "Manager"),
                new Claim(HttpContextCallerAccessor.RolesClaim, "manager"),
            ],
            "test"));
        var rejected = new List<string>();

        var roles = HttpContextCallerAccessor.RolesOf(principal, rejected.Add);

        Assert.Equal(new HashSet<Role> { Role.Manager }, roles);
        Assert.Equal(["manager"], rejected);
    }

    /// <summary>Verifies a present name claim is surfaced verbatim.</summary>
    [Fact]
    public void DisplayNameReturnsClaimValueWhenPresent()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("name", "Dana Datson"), new Claim("staff_id", "U000002")], "test"));

        Assert.Equal("Dana Datson", HttpContextCallerAccessor.DisplayNameOf(principal));
    }

    /// <summary>Verifies an absent name claim reads as no name rather than an empty string.</summary>
    [Fact]
    public void DisplayNameReturnsNullWhenClaimAbsent()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("staff_id", "U000002")], "test"));

        Assert.Null(HttpContextCallerAccessor.DisplayNameOf(principal));
    }

    /// <summary>Verifies an empty or whitespace name claim reads as no name.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DisplayNameReturnsNullWhenClaimBlank(string value)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("name", value)], "test"));

        Assert.Null(HttpContextCallerAccessor.DisplayNameOf(principal));
    }

    /// <summary>Verifies an unauthenticated identity contributes no name.</summary>
    [Fact]
    public void DisplayNameIgnoresUnauthenticatedIdentity()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("name", "Dana Datson")]));

        Assert.Null(HttpContextCallerAccessor.DisplayNameOf(principal));
    }
}
`````

## tests/EventBooking.Api.Tests/CallerIdentityTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/CallerIdentityTests.cs","encoding":"utf8","sha256":"3d07342c73ea078035748e6fc68b098ab0ab50f80b7bc1598a9459c0652222e2","parts":1,"part":1} -->

`````csharp
using System.Security.Claims;
using EventBooking.Api.Auth;

namespace EventBooking.Api.Tests;

/// <summary>Verifies untrusted staff-number claims are parsed at the request boundary.</summary>
public sealed class CallerIdentityTests
{
    /// <summary>Verifies valid claim casing is normalized into the domain value.</summary>
    [Theory]
    [InlineData("u123456", "U123456")]
    [InlineData("N654321", "N654321")]
    public void ValidClaimIsParsedAndCanonicalised(string claim, string expected)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(HttpContextCallerAccessor.StaffIdClaim, claim)], "test"));

        Assert.Equal(expected, HttpContextCallerAccessor.StaffIdOf(principal)!.Value);
    }

    /// <summary>Verifies absent and malformed claims are indistinguishable and non-throwing.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("X123456")]
    public void MissingOrMalformedClaimReturnsNull(string? claim)
    {
        var claims = claim is null ? [] : new[] { new Claim("staff_id", claim) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        Assert.Null(HttpContextCallerAccessor.StaffIdOf(principal));
    }
}
`````
