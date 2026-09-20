# 00a — Port source 57 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Api.Tests/DashboardEndpointTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/DashboardEndpointTests.cs","encoding":"utf8","sha256":"77ca7463440b0a9cdb9479098ebb7a3b00bfa9b3554eaa70bb32229d5909777d","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class DashboardEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task ACoordinatorGetsAllThreeViewsInOneResponse()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboards");
        var dashboards = await response.Content.ReadFromJsonAsync<DashboardsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(dashboards);
        Assert.NotNull(dashboards!.AwaitingAvailability);
        Assert.NotNull(dashboards.NoResponse);
        Assert.NotNull(dashboards.Slots);
    }

    [Fact]
    public async Task AnAdminIsForbiddenTheDashboards()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboards");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AManagerIsForbidden()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.MedicalCheckUp);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboards");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnUnauthenticatedCallerIsChallenged()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboards");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The Dashboards page binds to Email, RequiredCodes, WaitingSince/DaysWaiting, GaveUpOn and the
    /// slot Capacities list. Earlier coverage only asserted the row lists were non-null, so a DTO
    /// field could be renamed or dropped without failing a test — the page would simply render blank
    /// cells. This seeds one row of each kind and checks every field the page actually reads.
    /// </summary>
    [Fact]
    public async Task TheResponseCarriesEveryFieldThePageBindsTo()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var awaitingId = Guid.NewGuid();
        var noResponseId = Guid.NewGuid();
        var slotId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

            var cabinCrew = context.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.CabinCrew);
            var awaiting = Candidate.Create(
                awaitingId, "A. Waiting", "a.waiting@mail.com", cabinCrew);
            awaiting.MarkAwaitingAvailability();
            context.Candidates.Add(awaiting);

            var groundOps = context.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.GroundOperationsAgent);
            var noResponse = Candidate.Create(
                noResponseId, "B. Stuck", "b.stuck@mail.com", groundOps);
            noResponse.MarkInvited();
            noResponse.MarkNoResponse();
            context.Candidates.Add(noResponse);

            var proposal = SlotProposal.Create(
                Guid.NewGuid(),
                new SlotWindow(today.AddDays(30), new TimeOnly(9, 0)),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
            var slot = ConfirmedSlot.CreateFrom(slotId, proposal);
            slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
            context.SlotProposals.Add(proposal);
            context.ConfirmedSlots.Add(slot);

            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var dashboards = await client.GetFromJsonAsync<DashboardsResponse>("/api/dashboards");

        Assert.NotNull(dashboards);

        var awaitingRow = Assert.Single(dashboards!.AwaitingAvailability, r => r.CandidateId == awaitingId);
        Assert.Equal("A. Waiting", awaitingRow.Name);
        Assert.Equal("a.waiting@mail.com", awaitingRow.Email);
        Assert.Equal(new[] { "DAT", "MED", "UNI" }, awaitingRow.RequiredCodes);
        Assert.Equal(today, awaitingRow.WaitingSince);
        Assert.Equal(0, awaitingRow.DaysWaiting);

        var noResponseRow = Assert.Single(dashboards.NoResponse, r => r.CandidateId == noResponseId);
        Assert.Equal("B. Stuck", noResponseRow.Name);
        Assert.Equal("b.stuck@mail.com", noResponseRow.Email);
        Assert.Equal(new[] { "MED" }, noResponseRow.RequiredCodes);
        Assert.Equal(today, noResponseRow.GaveUpOn);

        var slotRow = Assert.Single(dashboards.Slots, s => s.ConfirmedSlotId == slotId);
        Assert.Equal(today.AddDays(30), slotRow.Date);
        Assert.Equal(new TimeOnly(9, 0), slotRow.StartTime);
        Assert.Equal(new TimeOnly(13, 0), slotRow.EndTime);
        Assert.Equal(0, slotRow.ActiveBookings);
        Assert.Equal(new[] { "DAT", "MED", "UNI" }, slotRow.Capacities.Select(c => c.Code));
        var drugAndAlcohol = slotRow.Capacities.Single(c => c.Code == "DAT");
        Assert.Equal(10, drugAndAlcohol.TotalHeadcount);
        Assert.Equal(9, drugAndAlcohol.RemainingCapacity);
    }

    private sealed record RowResponse(
        Guid CandidateId,
        string Name,
        string Email,
        IReadOnlyList<string> RequiredCodes,
        DateOnly? WaitingSince,
        int? DaysWaiting,
        DateOnly? GaveUpOn);

    private sealed record SlotCapacityResponse(string Code, int TotalHeadcount, int RemainingCapacity);

    private sealed record SlotResponse(
        Guid ConfirmedSlotId,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        IReadOnlyList<SlotCapacityResponse> Capacities,
        int ActiveBookings);

    private sealed record DashboardsResponse(
        IReadOnlyList<RowResponse> AwaitingAvailability,
        IReadOnlyList<RowResponse> NoResponse,
        IReadOnlyList<SlotResponse> Slots);
}
`````

## tests/EventBooking.Api.Tests/DiscoveryDocumentationTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/DiscoveryDocumentationTests.cs","encoding":"utf8","sha256":"b51d241f1a812ac31411e48ac68c051b8f2cda26dc2b51fd16a35e04084997f1","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Api.Tests;

/// <summary>Prevents the public discovery routes from becoming undocumented.</summary>
public sealed class DiscoveryDocumentationTests
{
    [Fact]
    public void RootReadmeDocumentsRestAndMcpDiscovery()
    {
        var root = FindRepositoryRoot();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        Assert.Contains("/api", readme, StringComparison.Ordinal);
        Assert.Contains("/openapi/v1.json", readme, StringComparison.Ordinal);
        Assert.Contains("/swagger", readme, StringComparison.Ordinal);
        Assert.Contains("/mcp", readme, StringComparison.Ordinal);
        Assert.Contains("bearer", readme, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HomeLabReadmeDocumentsDeployedSwagger()
    {
        var root = FindRepositoryRoot();
        var readme = File.ReadAllText(Path.Combine(root, "deploy", "home-lab", "README.md"));
        Assert.Contains("/openapi/v1.json", readme, StringComparison.Ordinal);
        Assert.Contains("/swagger", readme, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EventBooking.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
`````

## tests/EventBooking.Api.Tests/EventBooking.Api.Tests.csproj — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/EventBooking.Api.Tests.csproj","encoding":"utf8","sha256":"b9519e80014b47db54d5c00cc3a29ece29f2078bc9e0d7a1216838f387c49a64","parts":1,"part":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="Microsoft.Extensions.Configuration" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Testcontainers.PostgreSql" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\EventBooking.Api\EventBooking.Api.csproj" />
    <ProjectReference Include="..\..\src\EventBooking.Domain\EventBooking.Domain.csproj" />
    <ProjectReference Include="..\..\src\EventBooking.Application\EventBooking.Application.csproj" />
    <ProjectReference Include="..\..\src\EventBooking.Infrastructure\EventBooking.Infrastructure.csproj" />
    <ProjectReference Include="..\..\src\EventBooking.Api.Auth\EventBooking.Api.Auth.csproj" />
  </ItemGroup>

</Project>
`````

## tests/EventBooking.Api.Tests/Fakes/RecordingEmailTransport.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/Fakes/RecordingEmailTransport.cs","encoding":"utf8","sha256":"39f3791453dfc1dd302466f068b39656c1f71828da02a86043c1a3c916e0a391","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure.Email;

namespace EventBooking.Api.Tests.Fakes;

/// <summary>
/// Stands in for AWS SES in every API test. Constructing the real
/// <c>AmazonSimpleEmailServiceV2Client</c> throws immediately outside an AWS environment (no
/// RegionEndpoint or ServiceURL configured), which is exactly where CI runs — so no test may
/// depend on it, directly or through a handler that happens to send an email.
/// </summary>
public sealed class RecordingEmailTransport : IEmailTransport
{
    /// <summary>Messages accepted by this fake provider.</summary>
    public List<EmailMessage> Sent { get; } = [];

    /// <summary>Makes the next provider call fail, modelling an SES rejection.</summary>
    public bool FailNextSend { get; set; }

    /// <inheritdoc />
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (FailNextSend)
        {
            FailNextSend = false;
            throw new InvalidOperationException("simulated provider failure");
        }

        Sent.Add(message);
        return Task.CompletedTask;
    }
}
`````

## tests/EventBooking.Api.Tests/HealthTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/HealthTests.cs","encoding":"utf8","sha256":"d84a47c60ec5b73b03fa62c08484a9e487bb745f1c10c680d37ec76c70ca8afd","parts":1,"part":1} -->

`````csharp
using System.Net;
using Microsoft.Extensions.Configuration;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class HealthTests(ApiFactory factory)
{
    [Fact]
    public async Task TheHostStartsAndAnswersHealthAnonymously()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void MissingConfigurationNamesEverySettingThatIsAbsent()
    {
        var empty = new ConfigurationBuilder().Build();

        var ex = Assert.Throws<InvalidOperationException>(() => EventBookingConfiguration.Read(empty));

        Assert.Contains("ConnectionStrings:EventBooking", ex.Message);
        Assert.Contains("Tokens:SigningKey", ex.Message);
        Assert.Contains("Portal:BaseUrl", ex.Message);
        Assert.Contains("Auth:Provider", ex.Message);
        Assert.Contains("Email:Provider", ex.Message);
    }

    /// <summary>Ensures only the documented exact email-provider literals are accepted.</summary>
    [Theory]
    [InlineData("Fax")]
    [InlineData("999")]
    [InlineData("ses")]
    [InlineData("smtp")]
    public void AnInvalidEmailProviderIsRejectedWithAReadableMessage(string emailProvider)
    {
        var configuration = ConfigurationWith(emailProvider: emailProvider);

        var ex = Assert.Throws<InvalidOperationException>(
            () => EventBookingConfiguration.Read(configuration));

        Assert.Contains("Email:Provider", ex.Message);
    }

    [Fact]
    public void AnInvalidAuthProviderIsRejectedWithAReadableMessage()
    {
        var configuration = ConfigurationWith(authProvider: "Auth0");

        var ex = Assert.Throws<InvalidOperationException>(
            () => EventBookingConfiguration.Read(configuration));

        Assert.Contains("Auth:Provider", ex.Message);
    }

    /// <summary>Every required key present and valid, except the one override under test.</summary>
    private static IConfiguration ConfigurationWith(
        string emailProvider = "Smtp", string authProvider = "Local") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:EventBooking"] = "Host=localhost;Database=x;Username=x;Password=x",
                ["HeadOffice:TimeZoneId"] = "Europe/London",
                ["HeadOffice:Address"] = "1 Example Street",
                ["Tokens:SigningKey"] = "a-signing-key-that-is-long-enough-to-be-safe",
                ["Email:FromAddress"] = "recruitment@example.com",
                ["Email:FromName"] = "Recruitment Team",
                ["Email:Provider"] = emailProvider,
                ["Auth:Provider"] = authProvider,
                ["Portal:BaseUrl"] = "https://localhost:5001",
                ["Portal:CoordinatorContact"] = "recruitment@example.com",
            })
            .Build();
}
`````

## tests/EventBooking.Api.Tests/LocalAuthenticationExtensionsTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/LocalAuthenticationExtensionsTests.cs","encoding":"utf8","sha256":"2d6aaf7ae0c57a1beeda3f8ec2daf1253f609d1ffc0cedd5d948fba9ffd57bef","parts":1,"part":1} -->

`````csharp
using EventBooking.Api.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EventBooking.Api.Tests;

public class LocalAuthenticationExtensionsTests
{
    [Fact]
    public void AddLocalAuthenticationSetsJwtBearerAsTheDefaultSchemeAndReadsAuthorityAndAudience()
    {
        var services = new ServiceCollection();
        var authLocalSection = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authority"] = "http://keycloak.local:8081/realms/eventbooking",
                ["Audience"] = "eventbooking-web",
            })
            .Build();

        services.AddLocalAuthentication(authLocalSection);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        var jwtOptions = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, options.DefaultScheme);
        Assert.Equal("http://keycloak.local:8081/realms/eventbooking", jwtOptions.Authority);
        Assert.Equal("eventbooking-web", jwtOptions.Audience);
    }
}
`````

## tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs","encoding":"utf8","sha256":"d86348552f5297c6d626a52aeb7623ae047b66ae6e5176258f007b620a2adace","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Application.Abstractions;
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
public class ManageBookingEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task TheManageLinkShowsTheBookedTime()
    {
        var booking = await GivenABooking();
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingResponse>(
            $"/api/booking/manage/{booking.ManageToken}");

        Assert.NotNull(view);
        Assert.Equal("Amara Novak", view!.CandidateName);
        Assert.Contains("-", view.Display);

        using var document = System.Text.Json.JsonDocument.Parse(
            await client.GetStringAsync($"/api/booking/manage/{booking.ManageToken}"));
        var links = document.RootElement.GetProperty("_links");
        var cancel = links.GetProperty("cancel");
        Assert.Equal(
            $"/api/booking/manage/{Uri.EscapeDataString(booking.ManageToken)}/cancel",
            cancel.GetProperty("href").GetString());
        Assert.Equal("POST", cancel.GetProperty("method").GetString());
        Assert.Equal("cancelManagedBooking", cancel.GetProperty("operationId").GetString());
    }

    [Fact]
    public async Task CancellingWithoutRebookingReleasesTheBooking()
    {
        var booking = await GivenABooking();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/manage/{booking.ManageToken}/cancel", new { Rebook = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.False(outcome!.Reinvited);

        var afterwards = await client.GetAsync($"/api/booking/manage/{booking.ManageToken}");
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);

        var persisted = await ReadCancellationStateAsync(booking);
        Assert.Equal(BookingStatus.Cancelled, persisted.BookingStatus);
        Assert.Equal(CandidateStatus.NotYetInvited, persisted.CandidateStatus);
        Assert.Equal(persisted.TotalHeadcount, persisted.RemainingCapacity);
        Assert.Empty(persisted.PendingInviteIds);
    }

    [Fact]
    public async Task CancelAndRebookIssuesAFreshInvite()
    {
        var booking = await GivenABooking();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/manage/{booking.ManageToken}/cancel", new { Rebook = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.True(outcome!.Reinvited);

        var persisted = await ReadCancellationStateAsync(booking);
        Assert.Equal(BookingStatus.Cancelled, persisted.BookingStatus);
        Assert.Equal(CandidateStatus.Invited, persisted.CandidateStatus);
        Assert.Equal(persisted.TotalHeadcount, persisted.RemainingCapacity);
        var inviteId = Assert.Single(persisted.PendingInviteIds);
        Assert.NotEqual(booking.OriginalInviteId, inviteId);
    }

    [Fact]
    public async Task AnUnknownManageTokenIsNotFound()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/booking/manage/nonsense");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<BookingFixture> GivenABooking()
    {
        var invite = await GivenAnInvitedCandidate();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");

        var confirmedSlotId = view!.Options[0].ConfirmedSlotId;
        var confirmed = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm",
            new { ConfirmedSlotId = confirmedSlotId });

        var outcome = await confirmed.Content.ReadFromJsonAsync<ConfirmResponse>();
        return new BookingFixture(
            outcome!.BookingId,
            outcome.ManageToken,
            invite.CandidateId,
            invite.Id,
            confirmedSlotId);
    }

    private async Task<CandidateInviteFixture> GivenAnInvitedCandidate()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var confirmedSlotIds = new List<Guid>();
        foreach (var (date, startTime) in new[]
                 {
                     (new DateOnly(2030, 1, 14), new TimeOnly(9, 0)),
                     (new DateOnly(2030, 1, 15), new TimeOnly(11, 0)),
                     (new DateOnly(2030, 1, 16), new TimeOnly(13, 0)),
                 })
        {
            var proposal = SlotProposal.Create(
                Guid.NewGuid(), new SlotWindow(date, startTime), Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            var confirmedSlotId = Guid.NewGuid();
            context.SlotProposals.Add(proposal);
            context.ConfirmedSlots.Add(ConfirmedSlot.CreateFrom(confirmedSlotId, proposal));
            confirmedSlotIds.Add(confirmedSlotId);
        }

        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", $"{Guid.NewGuid():N}@mail.com",
            EmployeeGroup.Define(
                EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        candidate.MarkInvited();
        context.Candidates.Add(candidate);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        context.Invites.Add(Invite.CreateInitial(
            inviteId, candidate.Id, issued.TokenHash, DateTimeOffset.UtcNow.AddDays(4),
            confirmedSlotIds,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0));
        await context.SaveChangesAsync();

        return new CandidateInviteFixture(issued.Token, candidate.Id, inviteId);
    }

    private async Task<CancellationState> ReadCancellationStateAsync(BookingFixture booking)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

        var persistedBooking = await context.Bookings.SingleAsync(b => b.Id == booking.BookingId);
        var candidate = await context.Candidates.SingleAsync(c => c.Id == booking.CandidateId);
        var capacity = await context.SlotCapacities.SingleAsync(c =>
            c.ConfirmedSlotId == booking.ConfirmedSlotId &&
            c.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting);
        var pendingInviteIds = await context.Invites
            .Where(i => i.CandidateId == booking.CandidateId && i.Status == InviteStatus.Pending)
            .Select(i => i.Id)
            .ToListAsync();

        return new CancellationState(
            persistedBooking.Status,
            candidate.Status,
            capacity.TotalHeadcount,
            capacity.RemainingCapacity,
            pendingInviteIds);
    }

    private sealed record ConfirmResponse(Guid BookingId, string ManageToken);

    private sealed record CandidateInviteFixture(string Token, Guid CandidateId, Guid Id);

    private sealed record BookingFixture(
        Guid BookingId,
        string ManageToken,
        Guid CandidateId,
        Guid OriginalInviteId,
        Guid ConfirmedSlotId);

    private sealed record CancellationState(
        BookingStatus BookingStatus,
        CandidateStatus CandidateStatus,
        int TotalHeadcount,
        int RemainingCapacity,
        IReadOnlyList<Guid> PendingInviteIds);

    private sealed record BookingResponse(DateOnly Date, string Display, string CandidateName);

    private sealed record CancelResponse(bool Reinvited);
}
`````

## tests/EventBooking.Api.Tests/MeEndpointTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/MeEndpointTests.cs","encoding":"utf8","sha256":"02b2e29d1cb6713f275dc9a775069d2f90dcf0853a8f7d79cb4bcaf8bfbfbb4e","parts":1,"part":1} -->

`````csharp
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
`````

## tests/EventBooking.Api.Tests/OpenApiContractTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/OpenApiContractTests.cs","encoding":"utf8","sha256":"6b19ec037cc277618ff3db1519629157ec0c9c3cfc28f631aea6ec554cc8f7db","parts":1,"part":1} -->

`````csharp
using System.Text.Json;
using EventBooking.Api.OpenApi;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class OpenApiContractTests(ApiFactory factory)
{
    [Fact]
    public async Task EveryCataloguedApplicationRouteHasItsOperationId()
    {
        using var document = await GetDocumentAsync();
        var paths = document.RootElement.GetProperty("paths");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var expected in AgentOperationCatalog.All.Values.Where(IsGeneratedOperation))
        {
            var path = paths.GetProperty(expected.Route);
            var operation = path.GetProperty(expected.Method.ToLowerInvariant());
            var operationId = operation.GetProperty("operationId").GetString();
            Assert.Equal(expected.OperationId, operationId);
            Assert.True(seen.Add(operationId!));
            Assert.Equal(expected.Tag, operation.GetProperty("tags")[0].GetString());
            Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("summary").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("description").GetString()));
        }
    }

    [Fact]
    public async Task ProtectedOperationsCarryBearerAndMcpExtensions()
    {
        using var document = await GetDocumentAsync();
        var root = document.RootElement;
        Assert.True(root.GetProperty("components").GetProperty("securitySchemes")
            .TryGetProperty("bearer", out var bearer));
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());

        foreach (var expected in AgentOperationCatalog.All.Values.Where(x => x.McpTool is not null))
        {
            var operation = root.GetProperty("paths").GetProperty(expected.Route)
                .GetProperty(expected.Method.ToLowerInvariant());
            Assert.Equal(expected.McpTool, operation.GetProperty("x-mcp-tool").GetString());
            var hints = operation.GetProperty("x-agent-hints");
            Assert.Equal(expected.Hints.ReadOnly, hints.GetProperty("readOnly").GetBoolean());
            Assert.Equal(expected.Hints.Destructive, hints.GetProperty("destructive").GetBoolean());
            Assert.Equal(expected.Hints.Idempotent, hints.GetProperty("idempotent").GetBoolean());
            Assert.False(hints.GetProperty("openWorld").GetBoolean());
            Assert.True(operation.TryGetProperty("security", out _));
        }
    }

    [Fact]
    public async Task ContractDeclaresProblemJsonCsvAndNoContent()
    {
        using var document = await GetDocumentAsync();
        var paths = document.RootElement.GetProperty("paths");
        var candidateImport = paths.GetProperty("/api/candidates/import").GetProperty("post");
        Assert.True(candidateImport.GetProperty("requestBody").GetProperty("content").TryGetProperty("text/csv", out _));
        var roster = paths.GetProperty("/api/appointment-workspace/slots/{confirmedSlotId}/roster").GetProperty("get");
        Assert.True(roster.GetProperty("responses").GetProperty("200").GetProperty("content").TryGetProperty("text/csv", out _));
        var withdraw = paths.GetProperty("/api/slots/proposals/{id}/acceptance").GetProperty("delete");
        Assert.True(withdraw.GetProperty("responses").TryGetProperty("204", out _));
        Assert.True(withdraw.GetProperty("responses").GetProperty("404").GetProperty("content")
            .TryGetProperty("application/problem+json", out _));
    }

    private async Task<JsonDocument> GetDocumentAsync() => JsonDocument.Parse(
        await factory.CreateClient().GetStringAsync("/openapi/v1.json"));

    private static bool IsGeneratedOperation(AgentOperation operation) =>
        operation.OperationId is not "getOpenApiDocument" and not "getSwaggerUi";
}
`````

## tests/EventBooking.Api.Tests/OpenApiHostingTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/OpenApiHostingTests.cs","encoding":"utf8","sha256":"23de4c6ed7d370f2dd8d20c69dfc22cfc48d28a3de3ea857173da4daac74807a","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Text.Json;

namespace EventBooking.Api.Tests;

/// <summary>Verifies production-available OpenAPI and Swagger hosting.</summary>
[Collection("api")]
public sealed class OpenApiHostingTests(ApiFactory factory)
{
    /// <summary>The anonymous machine document is available under the stable v1 URL.</summary>
    [Fact]
    public async Task OpenApiDocumentIsAvailableAnonymously()
    {
        factory.SignedInAs = null;
        using var response = await factory.CreateClient().GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("EventBooking API", json.RootElement.GetProperty("info").GetProperty("title").GetString());
        Assert.Equal("v1", json.RootElement.GetProperty("info").GetProperty("version").GetString());
        Assert.True(json.RootElement.GetProperty("paths").TryGetProperty("/health", out _));
    }

    /// <summary>The browser documentation is available outside Development-only conditionals.</summary>
    [Fact]
    public async Task SwaggerUiLoadsTheFirstPartyDocument()
    {
        factory.SignedInAs = null;
        using var response = await factory.CreateClient().GetAsync("/swagger/index.html");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("EventBooking API v1", html, StringComparison.Ordinal);
        Assert.Contains("/openapi/v1.json", html, StringComparison.Ordinal);
    }

    /// <summary>The MCP host is not accidentally given REST documentation middleware.</summary>
    [Fact]
    public async Task ApiDocumentDoesNotDescribeMcpTransport()
    {
        using var json = JsonDocument.Parse(
            await factory.CreateClient().GetStringAsync("/openapi/v1.json"));
        Assert.False(json.RootElement.GetProperty("paths").TryGetProperty("/mcp", out _));
    }
}
`````

## tests/EventBooking.Api.Tests/PortArchitectureTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/PortArchitectureTests.cs","encoding":"utf8","sha256":"d1f919e490249bdd3d9d072b247228a13491f7848b1055777c1d6917b70c9414","parts":1,"part":1} -->

`````csharp
using System.Reflection;
using Xunit;
using System.Xml.Linq;

namespace EventBooking.Api.Tests;

public sealed class PortArchitectureTests
{
    [Fact]
    public void Api_dependency_graph_has_no_retired_provider()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        Visit(Assembly.Load("EventBooking.Api"), seen);
        Assert.DoesNotContain(seen, IsRetired);
    }

    [Fact]
    public void Source_projects_have_no_retired_provider_reference()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EventBooking.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var references = Directory.EnumerateFiles(Path.Combine(directory.FullName, "src"), "*.csproj", SearchOption.AllDirectories)
            .SelectMany(path => XDocument.Load(path).Descendants())
            .Where(element => element.Name.LocalName is "ProjectReference" or "PackageReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty);
        Assert.DoesNotContain(references, IsRetired);
    }

    private static bool IsRetired(string name) =>
        name.Contains("Aws", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Amazon.Lambda", StringComparison.OrdinalIgnoreCase)
        || name.Contains("EntraId", StringComparison.OrdinalIgnoreCase);

    private static void Visit(Assembly assembly, HashSet<string> seen)
    {
        if (!seen.Add(assembly.GetName().Name!)) return;
        foreach (var reference in assembly.GetReferencedAssemblies())
        {
            if (IsRetired(reference.Name!)) seen.Add(reference.Name!);
            else if (reference.Name!.StartsWith("EventBooking", StringComparison.Ordinal))
                Visit(Assembly.Load(reference), seen);
        }
    }
}
`````

## tests/EventBooking.Api.Tests/ProposalAcceptanceRevisionEndpointTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/ProposalAcceptanceRevisionEndpointTests.cs","encoding":"utf8","sha256":"8e0b3a5567c997568e75e4c0c711f39cc9e0c010656d8daee5dfba9230c3324a","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class ProposalAcceptanceRevisionEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task RepostingAnAcceptanceUpdatesTheHeadcountReturnedByTheBoard()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        var created = await client.PostAsJsonAsync(
            "/api/slots/proposals",
            new
            {
                Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60),
                StartTime = new TimeOnly(9, 0),
            });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var proposalId = await created.Content.ReadFromJsonAsync<Guid>();

        var accepted = await client.PostAsJsonAsync(
            $"/api/slots/proposals/{proposalId}/acceptance",
            new { Headcount = 10 });
        var revised = await client.PostAsJsonAsync(
            $"/api/slots/proposals/{proposalId}/acceptance",
            new { Headcount = 12 });

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);

        var board = await client.GetFromJsonAsync<BoardResponse>("/api/slots/board");
        var proposal = Assert.Single(
            board!.OpenProposals,
            item => item.ProposalId == proposalId);
        Assert.True(proposal.AcceptedByMe);
        Assert.Equal(12, proposal.MyAcceptedHeadcount);
    }

    private sealed record BoardResponse(
        IReadOnlyList<OpenProposalResponse> OpenProposals);

    private sealed record OpenProposalResponse(
        Guid ProposalId,
        bool AcceptedByMe,
        int? MyAcceptedHeadcount);
}
`````

## tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs","encoding":"utf8","sha256":"50b57161712650895d4e9dcdc3f8cd8ebd549c1075ab4e8b822138704ab3a18a","parts":1,"part":1} -->

`````csharp
using System.Net;
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

/// <summary>Verifies recently past slots keep the scoped, minimum-data workspace boundary.</summary>
[Collection("api")]
public sealed class RecentPastWorkspaceBoundaryTests(ApiFactory factory)
{
    /// <summary>Verifies Manager and AppointmentStaff both reach a recently past slot.</summary>
    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.AppointmentStaff)]
    public async Task ScopedRolesReachRecentlyPastSlots(Role role)
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [role], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var list = await client.GetAsync("/api/appointment-workspace/slots");
        using var detail = await client.GetAsync(
            $"/api/appointment-workspace/slots/{data.SlotId}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var body = await list.Content.ReadAsStringAsync();
        Assert.Contains(data.SlotId.ToString(), body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies a recently past slot outside trusted scope is indistinguishable from missing.</summary>
    [Fact]
    public async Task CrossTypeRecentlyPastSlotIsNotFound()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.UniformFitting);

        using var response = await factory.CreateClient().GetAsync(
            $"/api/appointment-workspace/slots/{data.SlotId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies unscoped callers are denied recently past slots without data.</summary>
    [Fact]
    public async Task UnassignedCallerIsForbiddenRecentlyPastSlot()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = Guid.NewGuid();

        using var response = await factory.CreateClient().GetAsync(
            $"/api/appointment-workspace/slots/{data.SlotId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Verifies a recently past slot keeps the exact approved minimum-data shape.</summary>
    [Fact]
    public async Task RecentlyPastResponsesKeepTheApprovedPropertySets()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var detail = JsonDocument.Parse(await client.GetStringAsync(
            $"/api/appointment-workspace/slots/{data.SlotId}"));
        AssertKeys(detail.RootElement,
            "appointmentTypeName", "confirmedSlotId", "date", "startTime", "endTime", "appointments", "_links");
        var row = Assert.Single(detail.RootElement.GetProperty("appointments").EnumerateArray());
        AssertKeys(row, "bookingAppointmentId", "candidateName", "candidateEmail",
            "status", "checkedInAt", "outcomeAt", "version", "_links");
        Assert.DoesNotContain("candidateId", detail.RootElement.GetRawText());
        Assert.DoesNotContain("bookingId", detail.RootElement.GetRawText());
    }

    private async Task<(Guid SlotId, Guid AppointmentId)> GivenWorkspaceAsync(
        Guid appointmentTypeId,
        int daysBeforeToday)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtHeadOffice;
        var slot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(today.AddDays(-daysBeforeToday), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var groupId = appointmentTypeId == AppointmentTypeIds.MedicalCheckUp
            ? EmployeeGroupIds.GroundOperationsAgent
            : EmployeeGroupIds.Pilots;
        var group = context.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == groupId);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
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

## tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs","encoding":"utf8","sha256":"52d14a745dbf3c166f4d9517214af3dd14cb60b53b630ebf37fc3dbe888eca99","parts":1,"part":1} -->

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

/// <summary>Verifies recovery-invite start/cancel routes, auth, and the delivery-outcome projection.</summary>
[Collection("api")]
public sealed class RecoveryInviteEndpointTests(ApiFactory factory)
{
    private sealed record DeliveryOutcomeResponse(
        Guid InviteId,
        IReadOnlyList<Guid> AppointmentTypeIds,
        bool EmailSent);

    /// <summary>A Coordinator starts recovery for a missed appointment and gets the outcome.</summary>
    [Fact]
    public async Task CoordinatorStartsRecoveryAndReceivesDeliveryOutcome()
    {
        var candidateId = await GivenCandidateWithNoShowAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsync(
            $"/api/candidates/{candidateId}/recovery-invites", null);
        var body = await response.Content.ReadFromJsonAsync<DeliveryOutcomeResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(Guid.Empty, body!.InviteId);
        Assert.Contains(AppointmentTypeIds.MedicalCheckUp, body.AppointmentTypeIds);
    }

    /// <summary>A Coordinator learns nothing is recoverable through the stable error code.</summary>
    [Fact]
    public async Task CoordinatorReceivesConflictWhenNothingRecoverable()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/candidates", new
        {
            Name = "Amara Novak",
            Email = $"{Guid.NewGuid():N}@example.com",
            EmployeeGroupId = EmployeeGroupIds.CabinCrew,
        });
        var candidateId = await created.Content.ReadFromJsonAsync<Guid>();

        using var response = await client.PostAsync(
            $"/api/candidates/{candidateId}/recovery-invites", null);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "recovery_not_available",
            problem.RootElement.GetProperty("title").GetString());
    }

    /// <summary>Only Coordinators may start or cancel a recovery invite.</summary>
    [Fact]
    public async Task AdminCannotStartRecovery()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);

        using var response = await factory.CreateClient().PostAsync(
            $"/api/candidates/{Guid.NewGuid()}/recovery-invites", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>A scoped Manager still lacks the Coordinator-only recovery capability.</summary>
    [Fact]
    public async Task ManagerCannotStartRecovery()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient().PostAsync(
            $"/api/candidates/{Guid.NewGuid()}/recovery-invites", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Anonymous callers cannot start a recovery invite.</summary>
    [Fact]
    public async Task AnonymousCannotStartRecovery()
    {
        factory.SignedInAs = null;

        using var response = await factory.CreateClient().PostAsync(
            $"/api/candidates/{Guid.NewGuid()}/recovery-invites", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>A Coordinator cancels a pending recovery, and a second cancel conflicts.</summary>
    [Fact]
    public async Task CoordinatorCancelsPendingRecovery()
    {
        var candidateId = await GivenCandidateWithNoShowAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        using var started = await client.PostAsync(
            $"/api/candidates/{candidateId}/recovery-invites", null);
        var outcome = await started.Content.ReadFromJsonAsync<DeliveryOutcomeResponse>();

        using var cancelled = await client.DeleteAsync(
            $"/api/candidates/{candidateId}/recovery-invites/{outcome!.InviteId}");
        using var again = await client.DeleteAsync(
            $"/api/candidates/{candidateId}/recovery-invites/{outcome.InviteId}");

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    /// <summary>Cancelling an unknown recovery invite is not found.</summary>
    [Fact]
    public async Task CancellingUnknownRecoveryReturnsNotFound()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/candidates", new
        {
            Name = "Amara Novak",
            Email = $"{Guid.NewGuid():N}@example.com",
            EmployeeGroupId = EmployeeGroupIds.CabinCrew,
        });
        var candidateId = await created.Content.ReadFromJsonAsync<Guid>();

        using var response = await client.DeleteAsync(
            $"/api/candidates/{candidateId}/recovery-invites/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Only Coordinators may cancel a recovery invite.</summary>
    [Fact]
    public async Task AdminCannotCancelRecovery()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);

        using var response = await factory.CreateClient().DeleteAsync(
            $"/api/candidates/{Guid.NewGuid()}/recovery-invites/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>A scoped Manager still lacks the Coordinator-only recovery capability.</summary>
    [Fact]
    public async Task ManagerCannotCancelRecovery()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient().DeleteAsync(
            $"/api/candidates/{Guid.NewGuid()}/recovery-invites/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Seeds a booked candidate with one Medical Check-up no-show and a spare slot.</summary>
    private async Task<Guid> GivenCandidateWithNoShowAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtHeadOffice;
        var bookedSlot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(today.AddDays(-1), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var spareSlots = new[]
        {
            new TimeOnly(11, 0),
            new TimeOnly(13, 0),
            new TimeOnly(15, 0),
        }
        .Select(start => ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(today.AddDays(2), start),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20)))
        .ToList();
        var group = context.EmployeeGroups
            .Include(g => g.Requirements)
            .Single(g => g.Id == EmployeeGroupIds.GroundOperationsAgent);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [bookedSlot.Id, Guid.NewGuid(), Guid.NewGuid()],
            candidate.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, bookedSlot.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp);
        context.AddRange(bookedSlot);
        context.AddRange(spareSlots);
        context.AddRange(candidate, booking, appointment);
        await context.SaveChangesAsync();

        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.MedicalCheckUp);
        using var marked = await factory.CreateClient().PutAsJsonAsync(
            $"/api/appointment-workspace/appointments/{appointment.Id}/status",
            new { status = "NoShow", expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        return candidate.Id;
    }
}
`````
