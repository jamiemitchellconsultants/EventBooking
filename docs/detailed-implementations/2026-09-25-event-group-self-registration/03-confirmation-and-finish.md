# 03 — Confirmation and finish (Tasks 7–9)

[← Overview](00-overview.md) · [Ontology](../../ontology.md)

Tasks 7–9 complete the public path: confirmation creates the normal booking atomically, the
anonymous Blazor pages present it, and maintenance removes expired personal data. These are the
application, API and web layers on top of the domain and storage contracts from Tasks 1–6.

### Task 7: Confirm one registration atomically

**Files:**
- Create: `src/EventBooking.Application/SelfRegistrations/ViewSelfRegistrationHandler.cs`
- Create: `src/EventBooking.Application/SelfRegistrations/ConfirmSelfRegistrationHandler.cs`
- Modify: `src/EventBooking.Api/Endpoints/PublicEventGroupEndpoints.cs`
- Modify: `src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs`
- Modify: `src/EventBooking.Infrastructure/DependencyInjection.cs`
- Modify: `src/EventBooking.Domain/Audit/AuditAction.cs`
- Test: `tests/EventBooking.Api.Tests/SelfRegistrationConfirmationEndpointTests.cs` (Create)
- Test: `tests/EventBooking.Application.Tests/SelfRegistrations/ConfirmSelfRegistrationTests.cs` (Create)

**Interfaces:**

```csharp
namespace EventBooking.Application.SelfRegistrations;
public sealed record ViewSelfRegistrationQuery(string Token);
public sealed record SelfRegistrationSummary(Guid EventGroupId, Guid EventId,
    string EventGroupTitle, string LocationName, DateOnly Date, TimeOnly StartTime,
    string AttendeeGroupName);
public sealed record ConfirmSelfRegistrationCommand(string Token);
public sealed record ConfirmSelfRegistrationOutcome(Guid BookingId);
public sealed class ViewSelfRegistrationHandler
{
    public Task<Result<SelfRegistrationSummary>> HandleAsync(
        ViewSelfRegistrationQuery query, CancellationToken ct);
}
public sealed class ConfirmSelfRegistrationHandler
{
    public Task<Result<ConfirmSelfRegistrationOutcome>> HandleAsync(
        ConfirmSelfRegistrationCommand command, CancellationToken ct);
}
// GET /api/public/self-registrations/{token}; POST /api/public/self-registrations/{token}/confirm.
// The POST returns booking ID, not a manage token; BookingConfirmation email supplies that link.
```

- [ ] **Step 1: Write the failing test**

Create `tests/EventBooking.Api.Tests/SelfRegistrationConfirmationEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.EventGroups;
using EventBooking.Domain.Events;
using EventBooking.Domain.SelfRegistrations;
using EventBooking.Infrastructure.Persistence;
using EventBooking.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class SelfRegistrationConfirmationEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task TwoEmailConfirmationsForLastPlaceCreateOneBooking()
    {
        var fixture = await SeedTwoRequestsForOnePlaceAsync();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var responses = await Task.WhenAll(fixture.Tokens.Select(token => client.PostAsJsonAsync(
            $"/api/public/self-registrations/{Uri.EscapeDataString(token)}/confirm", new { })));

        Assert.Single(responses.Where(x => x.StatusCode == HttpStatusCode.Created));
        Assert.Single(responses.Where(x => x.StatusCode == HttpStatusCode.Conflict));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.Equal(1, await db.Bookings.CountAsync(x => x.EventId == fixture.EventId));
        Assert.All(await db.EventCapacities.Where(x => x.EventId == fixture.EventId).ToListAsync(), row =>
            Assert.InRange(row.RemainingCapacity, 0, row.TotalHeadcount));
    }

    private async Task<Fixture> SeedTwoRequestsForOnePlaceAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var signer = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var proposal = ProposalFixture.Create(Guid.NewGuid(),
            new EventWindow(new DateOnly(2030, 1, 14), new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 1);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 1);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        db.EventProposals.Add(proposal);
        db.Events.Add(eventItem);
        var requirements = new Dictionary<Guid, IReadOnlyCollection<Guid>>
        {
            [AttendeeGroupIds.CabinCrew] = [
                AppointmentTypeIds.DrugAndAlcoholTesting,
                AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting],
        };
        var group = EventGroup.Create(Guid.NewGuid(), "Open days", null, requirements);
        group.AddEvent(eventItem.Id, eventItem.Capacities.Select(x => x.AppointmentTypeId).ToArray(),
            requirements, true);
        group.SetOpen(true);
        group.SetEventOpen(eventItem.Id, true);
        db.EventGroups.Add(group);
        var requests = new[]
        {
            SelfRegistration.Create(Guid.NewGuid(), group.Id, eventItem.Id,
                AttendeeGroupIds.CabinCrew, "Amara Novak", $"{Guid.NewGuid():N}@example.test",
                DateTimeOffset.UtcNow),
            SelfRegistration.Create(Guid.NewGuid(), group.Id, eventItem.Id,
                AttendeeGroupIds.CabinCrew, "Ivo Chen", $"{Guid.NewGuid():N}@example.test",
                DateTimeOffset.UtcNow),
        };
        db.SelfRegistrations.AddRange(requests);
        await db.SaveChangesAsync();
        return new Fixture(requests.Select(x => signer.Issue(
            TokenPurpose.SelfRegistrationConfirmation, x.Id, x.TokenVersion)).ToArray(), eventItem.Id);
    }

    private sealed record Fixture(string[] Tokens, Guid EventId);
}
```

- [ ] **Step 2: Run the red test**

```bash
dotnet test tests/EventBooking.Api.Tests --filter "FullyQualifiedName~SelfRegistrationConfirmationEndpointTests"
```

Expected: 404 for the missing confirmation route.

- [ ] **Step 3: Implement token view and confirmation transaction**

Reject malformed, wrong-purpose, wrong-version, expired, and already-used tokens without
revealing personal details. To confirm, read the request once to locate its `EventGroup`, then
start a transaction and lock `EventGroup`, `SelfRegistration`, existing Attendee (when any), any
pending Invite, Event, and required `EventCapacity` rows in that order. Recheck IDs, both gates,
selected active `AttendeeGroup`, exact Event types, future Event Window, and spare capacity after
locks. A closed gate or cancelled/started Event maps to a conflict; a bad/expired token maps to
404/410. The private group read path remains 404.

```csharp
var invite = Invite.CreateInitial(Guid.NewGuid(), attendee.Id,
    clock.UtcNow.AddDays(1), [eventItem.LocationId], [eventItem.Id],
    attendee.RequiredAppointmentTypeIds, retryCount: 0, inviteOptionCount: 1);
attendee.MarkInvited(clock.UtcNow);
eventItem.ChargeRequiredTypes(attendee.RequiredAppointmentTypeIds);
var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, clock.UtcNow);
foreach (var typeId in attendee.RequiredAppointmentTypeIds)
    appointments.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
invite.MarkUsed();
attendee.MarkBooked(clock.UtcNow);
registration.Confirm(attendee.Id, booking.Id, clock.UtcNow);
emails.Add(EmailLog.RecordPending(Guid.NewGuid(), attendee.Id,
    EmailTemplate.BookingConfirmation, clock.UtcNow, bookingId: booking.Id));
```

For an existing Attendee, require the same group and no active original Booking, preserve its
name, supersede its pending initial Invite before inserting the one-option Invite. For a new
Attendee, create it from the submitted name/email/group. Catch the unique email and active-booking
database backstops, roll back, and return conflict. Audit only IDs and canonical codes; do not
include submitted personal data. The API returns 201 plus booking ID, without a manage token.

- [ ] **Step 4: Add failure and race tests, then verify green**

In the new application test file, cover token replay; expiry boundary; one gate closing after
request; member Event becoming private, cancelled or started; an existing email in another group;
an existing Attendee already booked; and a reused Attendee with a pending Invite. Assert no
Attendee, Invite, Booking, appointment, outbox or capacity mutation after every failed attempt.
In the API test, verify one created/one conflict under simultaneous confirmation and a booking
confirmation mail row for the winner. Run application/API suites and build.

- [ ] **Step 5: Review, validate, commit and push**

Review intended staged files and the cached diff; run `node scripts/check-ontology-terms.mjs`.

```bash
git add -A
git commit -m "feat(registration): confirm bookings atomically"
git push
```

### Task 8: Anonymous `EventGroup` pages

**Files:**
- Create: `src/EventBooking.Web/Services/PublicEventGroupsClient.cs`
- Create: `src/EventBooking.Web/Pages/PublicEventGroup.razor`
- Create: `src/EventBooking.Web/Pages/PublicEventGroup.razor.css`
- Create: `src/EventBooking.Web/Pages/PublicEventRegistration.razor`
- Create: `src/EventBooking.Web/Pages/PublicEventRegistration.razor.css`
- Create: `src/EventBooking.Web/Pages/ConfirmSelfRegistration.razor`
- Create: `src/EventBooking.Web/Pages/ConfirmSelfRegistration.razor.css`
- Modify: `src/EventBooking.Web/Program.cs`
- Test: `tests/EventBooking.Web.Tests/PublicEventGroupsClientTests.cs` (Create)
- Test: `tests/EventBooking.Web.Tests/Pages/PublicEventGroupPageTests.cs` (Create)
- Test: `tests/EventBooking.Web.Tests/Pages/ConfirmSelfRegistrationPageTests.cs` (Create)

**Interfaces:**

```csharp
namespace EventBooking.Web.Services;
public sealed record PublicAttendeeGroupDto(Guid Id, string Name, string Description);
public sealed record PublicEventDto(Guid Id, string LocationName, string Address,
    DateOnly Date, TimeOnly StartTime, int DurationMinutes,
    IReadOnlyList<string> AppointmentTypeNames,
    IReadOnlyList<Guid> AvailableAttendeeGroupIds);
public sealed record PublicEventGroupDto(Guid Id, string Title, string Description,
    IReadOnlyList<PublicAttendeeGroupDto> AttendeeGroups,
    IReadOnlyList<PublicEventDto> Events);
public sealed record SelfRegistrationSummaryDto(Guid EventGroupId, Guid EventId,
    string EventGroupTitle, string LocationName, DateOnly Date, TimeOnly StartTime,
    string AttendeeGroupName);
public sealed record ConfirmSelfRegistrationOutcomeDto(Guid BookingId);
public interface IPublicEventGroupsClient
{
    Task<ApiOutcome<PublicEventGroupDto>> GetGroupAsync(Guid id, CancellationToken ct);
    Task<ApiOutcome<PublicEventDto>> GetEventAsync(Guid groupId, Guid eventId, CancellationToken ct);
    Task<ApiOutcome<bool>> RequestAsync(Guid groupId, Guid eventId, string name,
        string email, Guid attendeeGroupId, CancellationToken ct);
    Task<ApiOutcome<SelfRegistrationSummaryDto>> ViewConfirmationAsync(string token, CancellationToken ct);
    Task<ApiOutcome<ConfirmSelfRegistrationOutcomeDto>> ConfirmAsync(
        string token, IdempotencySubmission submission, CancellationToken ct);
}
```

- [ ] **Step 1: Write the failing test**

Create `tests/EventBooking.Web.Tests/PublicEventGroupsClientTests.cs`:

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public sealed class PublicEventGroupsClientTests
{
    [Fact]
    public async Task RequestUsesPublicRouteAndOnlySubmittedFields()
    {
        var handler = new SpyHandler();
        var client = new PublicEventGroupsClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example"),
        });
        var groupId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var attendeeGroupId = Guid.NewGuid();

        var result = await client.RequestAsync(groupId, eventId, "Amara Novak",
            "amara@example.test", attendeeGroupId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal($"/api/public/event-groups/{groupId}/events/{eventId}/registrations",
            handler.Request!.RequestUri!.AbsolutePath);
        Assert.False(handler.Request.Headers.Contains("Authorization"));
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("Amara Novak", body.RootElement.GetProperty("name").GetString());
        Assert.Equal("amara@example.test", body.RootElement.GetProperty("email").GetString());
        Assert.Equal(attendeeGroupId.ToString(),
            body.RootElement.GetProperty("attendeeGroupId").GetString());
    }

    private sealed class SpyHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("true", Encoding.UTF8, "application/json"),
            };
        }
    }
}
```

- [ ] **Step 2: Run the red test**

```bash
dotnet test tests/EventBooking.Web.Tests --filter "FullyQualifiedName~PublicEventGroupsClientTests"
```

Expected: PublicEventGroupsClient is missing.

- [ ] **Step 3: Implement client and pages**

Register a named public HttpClient without AuthorizationMessageHandler, like BookingClient.
Routes are /event-groups/{groupId}, /event-groups/{groupId}/events/{eventId}, and
/event-groups/confirm/{token}. Apply AllowAnonymous and AttendeeLayout to each. The group page
shows open Events ordered by local start, and a clear empty state. The event page displays group
descriptions and disables groups without capacity, labels name/email/group controls, and states
that sending the request does not hold a place. On 202 show the same “check your email” copy for
new and existing addresses. The confirmation page shows a summary, runs a single confirm action
with an idempotency key, then tells the user to check the booking-confirmation email for the
manage link. For 404/410/409, show an accessible error and Coordinator contact.

```razor
@page "/event-groups/{GroupId:guid}"
@attribute [Microsoft.AspNetCore.Authorization.AllowAnonymous]
@layout EventBooking.Web.Layout.AttendeeLayout
<h1>@(_group?.Title ?? "Event group")</h1>
```

- [ ] **Step 4: Add rendered-state tests and verify green**

Use the existing bUnit page test setup to cover private 404, no open Events, capacity-disabled
group, invalid form, neutral 202, expired token and success. Assert status/error text has an
accessible role and that no raw token is placed in analytics or logs. Run Web tests and build.

- [ ] **Step 5: Review, validate, commit and push**

Review intended staged files and the cached diff; run `node scripts/check-ontology-terms.mjs`.

```bash
git add -A
git commit -m "feat(web): add anonymous event group registration"
git push
```

### Task 9: Expiry, retention and final integration

**Files:**
- Create: `src/EventBooking.Application/SelfRegistrations/SelfRegistrationMaintenance.cs`
- Modify: `src/EventBooking.Application/Abstractions/ISelfRegistrationRepository.cs`
- Modify: `src/EventBooking.Infrastructure/Persistence/Repositories/SelfRegistrationRepository.cs`
- Modify: `src/EventBooking.Application/Jobs/SweepSteps.cs`
- Modify: `src/EventBooking.Api/InviteSweepService.cs`
- Modify: `src/EventBooking.Infrastructure/DependencyInjection.cs`
- Modify: `src/EventBooking.Domain/Audit/AuditAction.cs`
- Modify: `docs/design/00-overview.md`
- Modify: `docs/design/01-domain-model.md`
- Modify: `docs/design/02-functional-requirements.md`
- Modify: `docs/design/03b-screens-and-flows.md`
- Modify: `docs/design/05-api-design.md`
- Modify: `docs/design/06-security-and-authentication.md`
- Test: `tests/EventBooking.Application.Tests/SelfRegistrations/SelfRegistrationMaintenanceTests.cs` (Create)
- Test: `tests/EventBooking.Api.Tests/SelfRegistrationJourneyTests.cs` (Create)

**Interfaces:**

```csharp
namespace EventBooking.Application.Abstractions;
public interface ISelfRegistrationRepository
{
    Task<SelfRegistration?> GetAsync(Guid id, CancellationToken ct);
    Task<SelfRegistration?> LockForUpdateAsync(Guid id, CancellationToken ct);
    Task LockEmailThrottleAsync(string normalizedEmail, CancellationToken ct);
    Task<bool> HasRecentPendingAsync(string normalizedEmail, DateTimeOffset since, CancellationToken ct);
    void Add(SelfRegistration request);
    Task<IReadOnlyList<SelfRegistration>> ListExpiredPendingAsync(DateTimeOffset now, CancellationToken ct);
    Task<int> DeleteTerminalBeforeAsync(DateTimeOffset cutoff, CancellationToken ct);
}
namespace EventBooking.Application.SelfRegistrations;
public sealed class SelfRegistrationMaintenance
{
    // Expires pending rows and purges terminal rows + related confirmation EmailLogs after 30 days.
    public Task RunAsync(CancellationToken ct);
    public static bool IsDueForDeletion(SelfRegistration request, DateTimeOffset now);
}
```

- [ ] **Step 1: Write the failing test**

Create `tests/EventBooking.Application.Tests/SelfRegistrations/SelfRegistrationMaintenanceTests.cs`:

```csharp
using EventBooking.Application.SelfRegistrations;
using EventBooking.Domain.SelfRegistrations;

namespace EventBooking.Application.Tests.SelfRegistrations;

public sealed class SelfRegistrationMaintenanceTests
{
    [Fact]
    public void RetentionBoundaryIsThirtyDaysAfterTerminalState()
    {
        var terminal = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        var request = SelfRegistration.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "Amara Novak", "amara@example.test", terminal.AddDays(-1));
        request.Expire(terminal);

        Assert.False(SelfRegistrationMaintenance.IsDueForDeletion(
            request, terminal.AddDays(30).AddTicks(-1)));
        Assert.True(SelfRegistrationMaintenance.IsDueForDeletion(
            request, terminal.AddDays(30)));
        var pending = SelfRegistration.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "Ivo Chen", "ivo@example.test", terminal);
        Assert.False(SelfRegistrationMaintenance.IsDueForDeletion(
            pending, terminal.AddDays(40)));
    }
}
```

- [ ] **Step 2: Run the red test**

```bash
dotnet test tests/EventBooking.Application.Tests --filter "FullyQualifiedName~SelfRegistrationMaintenanceTests"
```

Expected: SelfRegistrationMaintenance does not exist.

- [ ] **Step 3: Implement expiry and purge**

Add the maintenance call to the existing periodic sweep. In one transaction, mark pending rows
expired at or after their expiry, then remove confirmation `EmailLog`s for terminal requests with
TerminalAt at most 30 days before now, and delete those requests. Keep the SQL bounded and indexed;
repeat batches until no eligible rows remain. Do not delete an in-flight pending request or an
Attendee/Booking. Audit expiry with request ID and status only. The static policy in the test is:

```csharp
public static bool IsDueForDeletion(SelfRegistration request, DateTimeOffset now) =>
    (request.Status is SelfRegistrationStatus.Confirmed or SelfRegistrationStatus.Expired)
    && request.TerminalAt is { } terminal && terminal.AddDays(30) <= now;
```

- [ ] **Step 4: Add journey and retention integration tests**

In the new API test, use ApiFactory to seed an open group/Event, submit the public form, drive
OutboxDispatcher.DispatchOneAsync for its `EmailLog`, extract the deterministic token through
ITokenService, confirm, and assert exactly one Attendee, Invite, Booking, one appointment per
required type, decremented capacity and a pending `BookingConfirmation` email. Add a PostgreSQL
retention test at just before/after 30 days, including related email-row deletion. Run all .NET
tests, build, ontology generation check and term check. Update current design docs' public-flow
scope and route/security descriptions, preserving the original dated spec as historical evidence.

- [ ] **Step 5: Review, validate, commit and push**

Review intended staged files and the cached diff; run `node scripts/check-ontology-terms.mjs`.

```bash
git add -A
git commit -m "feat(registration): expire requests and document the public journey"
git push
```
