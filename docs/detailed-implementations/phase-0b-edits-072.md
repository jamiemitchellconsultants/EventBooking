# 00b — Vocabulary edits 72 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — tests/EventBooking.Api.Tests/HealthTests.cs — 1/1

<!-- vocabulary-file: {"id":233,"oldPath":"tests/EventBooking.Api.Tests/HealthTests.cs","newPath":"tests/EventBooking.Api.Tests/HealthTests.cs","beforeSha":"d84a47c60ec5b73b03fa62c08484a9e487bb745f1c10c680d37ec76c70ca8afd","afterSha":"8dea831ce12a68db79ae37c9e070e4e94ecb81f7c01fe9e356a398a149b7ce9a","side":"after","part":1,"parts":1} -->

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
                ["TransitionalLocation:TimeZoneId"] = "Europe/London",
                ["TransitionalLocation:Address"] = "1 Example Street",
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

## before — tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":234,"oldPath":"tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs","beforeSha":"d86348552f5297c6d626a52aeb7623ae047b66ae6e5176258f007b620a2adace","afterSha":"54908c6f32741b4592523812ceb5852722131d49badaa63eed8e3ff093fdbf33","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":234,"oldPath":"tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs","beforeSha":"d86348552f5297c6d626a52aeb7623ae047b66ae6e5176258f007b620a2adace","afterSha":"54908c6f32741b4592523812ceb5852722131d49badaa63eed8e3ff093fdbf33","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
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
        Assert.Equal("Amara Novak", view!.AttendeeName);
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
        Assert.Equal(AttendeeStatus.NotYetInvited, persisted.AttendeeStatus);
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
        Assert.Equal(AttendeeStatus.Invited, persisted.AttendeeStatus);
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
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");

        var eventId = view!.Options[0].EventId;
        var confirmed = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm",
            new { EventId = eventId });

        var outcome = await confirmed.Content.ReadFromJsonAsync<ConfirmResponse>();
        return new BookingFixture(
            outcome!.BookingId,
            outcome.ManageToken,
            invite.AttendeeId,
            invite.Id,
            eventId);
    }

    private async Task<AttendeeInviteFixture> GivenAnInvitedAttendee()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var eventIds = new List<Guid>();
        foreach (var (date, startTime) in new[]
                 {
                     (new DateOnly(2030, 1, 14), new TimeOnly(9, 0)),
                     (new DateOnly(2030, 1, 15), new TimeOnly(11, 0)),
                     (new DateOnly(2030, 1, 16), new TimeOnly(13, 0)),
                 })
        {
            var proposal = EventProposal.Create(
                Guid.NewGuid(), new EventWindow(date, startTime), Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            var eventId = Guid.NewGuid();
            context.EventProposals.Add(proposal);
            context.Events.Add(Event.CreateFrom(eventId, proposal));
            eventIds.Add(eventId);
        }

        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", $"{Guid.NewGuid():N}@mail.com",
            AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        attendee.MarkInvited();
        context.Attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        context.Invites.Add(Invite.CreateInitial(
            inviteId, attendee.Id, issued.TokenHash, DateTimeOffset.UtcNow.AddDays(4),
            eventIds,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0));
        await context.SaveChangesAsync();

        return new AttendeeInviteFixture(issued.Token, attendee.Id, inviteId);
    }

    private async Task<CancellationState> ReadCancellationStateAsync(BookingFixture booking)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

        var persistedBooking = await context.Bookings.SingleAsync(b => b.Id == booking.BookingId);
        var attendee = await context.Attendees.SingleAsync(c => c.Id == booking.AttendeeId);
        var capacity = await context.EventCapacities.SingleAsync(c =>
            c.EventId == booking.EventId &&
            c.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting);
        var pendingInviteIds = await context.Invites
            .Where(i => i.AttendeeId == booking.AttendeeId && i.Status == InviteStatus.Pending)
            .Select(i => i.Id)
            .ToListAsync();

        return new CancellationState(
            persistedBooking.Status,
            attendee.Status,
            capacity.TotalHeadcount,
            capacity.RemainingCapacity,
            pendingInviteIds);
    }

    private sealed record ConfirmResponse(Guid BookingId, string ManageToken);

    private sealed record AttendeeInviteFixture(string Token, Guid AttendeeId, Guid Id);

    private sealed record BookingFixture(
        Guid BookingId,
        string ManageToken,
        Guid AttendeeId,
        Guid OriginalInviteId,
        Guid EventId);

    private sealed record CancellationState(
        BookingStatus BookingStatus,
        AttendeeStatus AttendeeStatus,
        int TotalHeadcount,
        int RemainingCapacity,
        IReadOnlyList<Guid> PendingInviteIds);

    private sealed record BookingResponse(DateOnly Date, string Display, string AttendeeName);

    private sealed record CancelResponse(bool Reinvited);
}
`````

## before — tests/EventBooking.Api.Tests/OpenApiContractTests.cs — 1/1

<!-- vocabulary-file: {"id":235,"oldPath":"tests/EventBooking.Api.Tests/OpenApiContractTests.cs","newPath":"tests/EventBooking.Api.Tests/OpenApiContractTests.cs","beforeSha":"6b19ec037cc277618ff3db1519629157ec0c9c3cfc28f631aea6ec554cc8f7db","afterSha":"e9eebcc623c797d40ed91b12d7ba9ba3c5cb7cea19f9a09a4fd2b630ce3dfff6","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Api.Tests/OpenApiContractTests.cs — 1/1

<!-- vocabulary-file: {"id":235,"oldPath":"tests/EventBooking.Api.Tests/OpenApiContractTests.cs","newPath":"tests/EventBooking.Api.Tests/OpenApiContractTests.cs","beforeSha":"6b19ec037cc277618ff3db1519629157ec0c9c3cfc28f631aea6ec554cc8f7db","afterSha":"e9eebcc623c797d40ed91b12d7ba9ba3c5cb7cea19f9a09a4fd2b630ce3dfff6","side":"after","part":1,"parts":1} -->

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
        var attendeeImport = paths.GetProperty("/api/attendees/import").GetProperty("post");
        Assert.True(attendeeImport.GetProperty("requestBody").GetProperty("content").TryGetProperty("text/csv", out _));
        var roster = paths.GetProperty("/api/appointment-workspace/events/{eventId}/roster").GetProperty("get");
        Assert.True(roster.GetProperty("responses").GetProperty("200").GetProperty("content").TryGetProperty("text/csv", out _));
        var withdraw = paths.GetProperty("/api/event-proposals/{id}/acceptance").GetProperty("delete");
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

## before — tests/EventBooking.Api.Tests/ProposalAcceptanceRevisionEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":236,"oldPath":"tests/EventBooking.Api.Tests/ProposalAcceptanceRevisionEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/ProposalAcceptanceRevisionEndpointTests.cs","beforeSha":"8e0b3a5567c997568e75e4c0c711f39cc9e0c010656d8daee5dfba9230c3324a","afterSha":"cb241c0eca76c391481264e2c911021048ff3af597082c3196298c7be40056fb","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Api.Tests/ProposalAcceptanceRevisionEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":236,"oldPath":"tests/EventBooking.Api.Tests/ProposalAcceptanceRevisionEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/ProposalAcceptanceRevisionEndpointTests.cs","beforeSha":"8e0b3a5567c997568e75e4c0c711f39cc9e0c010656d8daee5dfba9230c3324a","afterSha":"cb241c0eca76c391481264e2c911021048ff3af597082c3196298c7be40056fb","side":"after","part":1,"parts":1} -->

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
            "/api/event-proposals",
            new
            {
                Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60),
                StartTime = new TimeOnly(9, 0),
            });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var proposalId = await created.Content.ReadFromJsonAsync<Guid>();

        var accepted = await client.PostAsJsonAsync(
            $"/api/event-proposals/{proposalId}/acceptance",
            new { Headcount = 10 });
        var revised = await client.PostAsJsonAsync(
            $"/api/event-proposals/{proposalId}/acceptance",
            new { Headcount = 12 });

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);

        var board = await client.GetFromJsonAsync<BoardResponse>("/api/events/board");
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

## before — tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs — 1/1

<!-- vocabulary-file: {"id":237,"oldPath":"tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs","newPath":"tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs","beforeSha":"50b57161712650895d4e9dcdc3f8cd8ebd549c1075ab4e8b822138704ab3a18a","afterSha":"c7e3b0e57198b14b4badcdaca7266ee8eb718b6dfe8e13473e0bb94e8783c21b","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs — 1/1

<!-- vocabulary-file: {"id":237,"oldPath":"tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs","newPath":"tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs","beforeSha":"50b57161712650895d4e9dcdc3f8cd8ebd549c1075ab4e8b822138704ab3a18a","afterSha":"c7e3b0e57198b14b4badcdaca7266ee8eb718b6dfe8e13473e0bb94e8783c21b","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

/// <summary>Verifies recently past events keep the scoped, minimum-data workspace boundary.</summary>
[Collection("api")]
public sealed class RecentPastWorkspaceBoundaryTests(ApiFactory factory)
{
    /// <summary>Verifies Manager and AppointmentStaff both reach a recently past eventItem.</summary>
    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.AppointmentStaff)]
    public async Task ScopedRolesReachRecentlyPastEvents(Role role)
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [role], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var list = await client.GetAsync("/api/appointment-workspace/events");
        using var detail = await client.GetAsync(
            $"/api/appointment-workspace/events/{data.EventId}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var body = await list.Content.ReadAsStringAsync();
        Assert.Contains(data.EventId.ToString(), body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies a recently past event outside trusted scope is indistinguishable from missing.</summary>
    [Fact]
    public async Task CrossTypeRecentlyPastEventIsNotFound()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.UniformFitting);

        using var response = await factory.CreateClient().GetAsync(
            $"/api/appointment-workspace/events/{data.EventId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies unscoped callers are denied recently past events without data.</summary>
    [Fact]
    public async Task UnassignedCallerIsForbiddenRecentlyPastEvent()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = Guid.NewGuid();

        using var response = await factory.CreateClient().GetAsync(
            $"/api/appointment-workspace/events/{data.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Verifies a recently past event keeps the exact approved minimum-data shape.</summary>
    [Fact]
    public async Task RecentlyPastResponsesKeepTheApprovedPropertySets()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var detail = JsonDocument.Parse(await client.GetStringAsync(
            $"/api/appointment-workspace/events/{data.EventId}"));
        AssertKeys(detail.RootElement,
            "appointmentTypeName", "eventId", "date", "startTime", "endTime", "appointments", "_links");
        var row = Assert.Single(detail.RootElement.GetProperty("appointments").EnumerateArray());
        AssertKeys(row, "bookingAppointmentId", "attendeeName", "attendeeEmail",
            "status", "checkedInAt", "outcomeAt", "version", "_links");
        Assert.DoesNotContain("attendeeId", detail.RootElement.GetRawText());
        Assert.DoesNotContain("bookingId", detail.RootElement.GetRawText());
    }

    private async Task<(Guid EventId, Guid AppointmentId)> GivenWorkspaceAsync(
        Guid appointmentTypeId,
        int daysBeforeToday)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;
        var eventItem = Event.CreateImported(
            Guid.NewGuid(), new EventWindow(today.AddDays(-daysBeforeToday), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var groupId = appointmentTypeId == AppointmentTypeIds.MedicalCheckUp
            ? AttendeeGroupIds.GroundOperationsAgent
            : AttendeeGroupIds.Pilots;
        var group = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == groupId);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);
        context.AddRange(eventItem, attendee, booking, appointment);
        await context.SaveChangesAsync();
        return (eventItem.Id, appointment.Id);
    }

    /// <summary>Asserts a JSON object carries exactly the approved property names.</summary>
    private static void AssertKeys(JsonElement value, params string[] expected) =>
        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
}
`````

## before — tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":238,"oldPath":"tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs","beforeSha":"52d14a745dbf3c166f4d9517214af3dd14cb60b53b630ebf37fc3dbe888eca99","afterSha":"f40a87937723d0cc58c8ad4a0c62f959ce1d5173fea3b1f00e05f6d78e94e4d1","side":"before","part":1,"parts":1} -->

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
