# 02 — Staff management and registration requests (Tasks 4–6)

[← Overview](00-overview.md) · [Ontology](../../ontology.md)

Tasks 4–6 build on the persisted `EventGroup`: first staff authorization and API, then its staff
screen, then anonymous discovery and pending email-confirmation requests. Booking confirmation is
deliberately in Task 7, after every public gate and outbox contract exists.

### Task 4: Staff `EventGroup` management API

**Files:**
- Modify: `src/EventBooking.Application/Access/StaffCapability.cs`
- Modify: `src/EventBooking.Application/Access/CapabilityMatrix.cs`
- Create: `src/EventBooking.Application/EventGroups/EventGroupCommands.cs`
- Create: `src/EventBooking.Application/EventGroups/EventGroupReadModels.cs`
- Create: `src/EventBooking.Application/EventGroups/ManageEventGroupHandler.cs`
- Create: `src/EventBooking.Application/EventGroups/ListEventGroupsHandler.cs`
- Create: `src/EventBooking.Api/Endpoints/EventGroupEndpoints.cs`
- Modify: `src/EventBooking.Api/Program.cs`
- Modify: `src/EventBooking.Api/Contracts/ApiResponses.cs`
- Modify: `src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs`
- Modify: `src/EventBooking.Infrastructure/DependencyInjection.cs`
- Modify: `src/EventBooking.Domain/Audit/AuditAction.cs`
- Modify: `src/EventBooking.Domain/Audit/ActorType.cs`
- Modify: `src/EventBooking.Domain/Audit/AuditEntityTypes.cs`
- Modify: `docs/design/06-security-and-authentication.md`
- Test: `tests/EventBooking.Api.Tests/EventGroupStaffEndpointTests.cs` (Create)
- Test: `tests/EventBooking.Application.Tests/Access/AuthorizationMatrixTests.cs` (Modify)

**Interfaces:**

```csharp
namespace EventBooking.Application.EventGroups;
public sealed record CreateEventGroupCommand(Guid StaffUserId, string? Title,
    string? Description, IReadOnlyList<Guid> AttendeeGroupIds);
public sealed record UpdateEventGroupCommand(Guid StaffUserId, Guid EventGroupId,
    string? Title, string? Description, IReadOnlyList<Guid> AttendeeGroupIds,
    bool IsOpen, long ExpectedVersion);
public sealed record ChangeEventGroupEventCommand(Guid StaffUserId, Guid EventGroupId,
    Guid EventId, bool? IsOpen, bool Remove, long ExpectedVersion);
public sealed record EventGroupResult(Guid Id, string Title, string Description, bool IsOpen,
    long Version, IReadOnlyList<Guid> AttendeeGroupIds,
    IReadOnlyList<Guid> AppointmentTypeIds, IReadOnlyList<EventGroupEventResult> Events);
public sealed record EventGroupEventResult(Guid EventId, bool IsOpen);
public sealed class ManageEventGroupHandler
{
    public Task<Result<EventGroupResult>> CreateAsync(CreateEventGroupCommand command, CancellationToken ct);
    public Task<Result<EventGroupResult>> UpdateAsync(UpdateEventGroupCommand command, CancellationToken ct);
    public Task<Result<EventGroupResult>> ChangeEventAsync(ChangeEventGroupEventCommand command, CancellationToken ct);
}
public sealed class ListEventGroupsHandler
{
    public Task<Result<IReadOnlyList<EventGroupResult>>> ListAsync(Guid staffUserId, CancellationToken ct);
    public Task<Result<EventGroupResult>> GetAsync(Guid staffUserId, Guid id, CancellationToken ct);
}
// Produces GET/POST/PUT /api/event-groups; PUT adds Event membership, PATCH changes its
// isOpen gate, and DELETE removes it at /api/event-groups/{id}/events/{eventId}.
```

- [ ] **Step 1: Write the failing test**

Create `tests/EventBooking.Api.Tests/EventGroupStaffEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class EventGroupStaffEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task AdminAndCoordinatorMayCreateButManagerMayNot()
    {
        var client = factory.CreateClient();
        var body = new
        {
            title = "Autumn intake",
            description = "Choose a date",
            attendeeGroupIds = new[] { AttendeeGroupIds.CabinCrew },
        };

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var admin = await client.PostAsJsonAsync("/api/event-groups", body);
        Assert.Equal(HttpStatusCode.Created, admin.StatusCode);
        var json = await admin.Content.ReadAsStringAsync();
        Assert.DoesNotContain("attendeeEmail", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("memberCount", json, StringComparison.OrdinalIgnoreCase);

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var coordinator = await client.PostAsJsonAsync("/api/event-groups", body);
        Assert.Equal(HttpStatusCode.Created, coordinator.StatusCode);

        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.MedicalCheckUp);
        var manager = await client.PostAsJsonAsync("/api/event-groups", body);
        Assert.Equal(HttpStatusCode.Forbidden, manager.StatusCode);
    }
}
```

- [ ] **Step 2: Run the red test**

```bash
dotnet test tests/EventBooking.Api.Tests --filter "FullyQualifiedName~EventGroupStaffEndpointTests"
```

Expected: 404 for the missing route.

- [ ] **Step 3: Implement commands, authorization and routes**

Add `ManageEventGroups` to the enum and matrix for Admin and Coordinator only, and add its row to
the design 06 authorization table in the same task. Every write
handler calls the capability authorizer before loading or changing the aggregate. Staff GETs
also require the capability. Use the repository's parent-row lock before updating; compare
ExpectedVersion and return a version conflict on mismatch. Reject repeated selected group IDs,
then resolve the `AttendeeGroup`s as active and construct their requirement map. Resolve member Event capacity type IDs and future
window in its Location zone. Call the domain methods from Task 2, then save/audit inside one
transaction. Audit only IDs, canonical codes, and old/new publication states.

```csharp
var authorized = await access.AuthorizeAsync(command.StaffUserId,
    StaffCapability.ManageEventGroups, null, ct);
if (authorized.IsFailure) return Result<EventGroupResult>.Failure(authorized.Error);
var group = await repository.LockForUpdateAsync(command.EventGroupId, ct);
if (group is null) return Result<EventGroupResult>.Failure(Error.NotFound("No such event group."));
if (group.Version != command.ExpectedVersion)
    return Result<EventGroupResult>.Failure(Error.VersionConflict(
        "The event group changed under you.", group.Version));
```

Map routes with StaffPolicy, StaffRateLimiterPolicy, operation metadata and 403/404/409/422
response declarations. Expose link relations only for held capabilities. Update the capability
matrix test's expected row set and the API operation catalog.

- [ ] **Step 4: Add behavior tests and verify green**

Add API cases for incompatible/started Events, missing/extra types, add/remove membership,
independent gates when one Event belongs to two groups, and stale version 409. Run application
access tests and API tests. The group list/detail response must contain no attendee identity or
count, including for Admin.

- [ ] **Step 5: Review, validate, commit and push**

Review intended staged files and the cached diff; run `node scripts/check-ontology-terms.mjs`.

```bash
git add -A
git commit -m "feat(event-groups): expose staff management"
git push
```

### Task 5: Staff `EventGroup`s screen

**Files:**
- Create: `src/EventBooking.Web/Services/EventGroupsClient.cs`
- Create: `src/EventBooking.Web/Pages/EventGroups.razor`
- Create: `src/EventBooking.Web/Pages/EventGroups.razor.css`
- Modify: `src/EventBooking.Web/Services/StaffNavigation.cs`
- Modify: `src/EventBooking.Web/Program.cs`
- Test: `tests/EventBooking.Web.Tests/EventGroupsClientTests.cs` (Create)
- Test: `tests/EventBooking.Web.Tests/Pages/EventGroupsPageTests.cs` (Create)

**Interfaces:**

```csharp
namespace EventBooking.Web.Services;
public sealed record EventGroupDto(Guid Id, string Title, string Description,
    bool IsOpen, long Version, IReadOnlyList<Guid> AttendeeGroupIds,
    IReadOnlyList<Guid> AppointmentTypeIds, IReadOnlyList<EventGroupEventDto> Events);
public sealed record EventGroupEventDto(Guid EventId, bool IsOpen);
public interface IEventGroupsClient
{
    Task<ApiOutcome<PageDto<EventGroupDto>>> ListAsync(CancellationToken ct);
    Task<ApiOutcome<EventGroupDto>> CreateAsync(string title, string description,
        IReadOnlyList<Guid> groupIds, IdempotencySubmission submission, CancellationToken ct);
    Task<ApiOutcome<EventGroupDto>> UpdateAsync(EventGroupDto value, CancellationToken ct);
    Task<ApiOutcome<EventGroupDto>> SetEventOpenAsync(Guid groupId, Guid eventId,
        bool open, long expectedVersion, CancellationToken ct);
    Task<ApiOutcome<EventGroupDto>> AddEventAsync(Guid groupId, Guid eventId,
        long expectedVersion, CancellationToken ct);
    Task<ApiOutcome<EventGroupDto>> RemoveEventAsync(Guid groupId, Guid eventId,
        long expectedVersion, CancellationToken ct);
}
```

- [ ] **Step 1: Write the failing test**

Create `tests/EventBooking.Web.Tests/EventGroupsClientTests.cs`:

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public sealed class EventGroupsClientTests
{
    [Fact]
    public async Task CreateSendsSelectedGroupsAndIdempotencyKey()
    {
        var handler = new SpyHandler(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent("""{"id":"10000000-0000-0000-0000-000000000001","title":"Autumn intake","description":"Choose a date","isOpen":false,"version":1,"attendeeGroupIds":[],"appointmentTypeIds":[],"events":[]}""",
                Encoding.UTF8, "application/json"),
        });
        var client = new EventGroupsClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example"),
        });
        var groupId = Guid.NewGuid();
        var submission = IdempotencySubmission.Start();

        var result = await client.CreateAsync("Autumn intake", "Choose a date",
            [groupId], submission, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("/api/event-groups", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal(submission.Key, handler.Request.Headers.GetValues("Idempotency-Key").Single());
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal(groupId.ToString(), body.RootElement.GetProperty("attendeeGroupIds")[0].GetString());
    }

    private sealed class SpyHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return response;
        }
    }
}
```

- [ ] **Step 2: Run the red test**

```bash
dotnet test tests/EventBooking.Web.Tests --filter "FullyQualifiedName~EventGroupsClientTests"
```

Expected: EventGroupsClient is missing.

- [ ] **Step 3: Implement client, navigation and page**

Register the client with the authenticated/staff HTTP pipeline in Web/Program.cs. Follow
AdminClient's `ApiCall.ReadAsync` and Idempotency-Key pattern. Add the `EventGroup`s navigation
link to both Admin and Coordinator branches. On the page load the groups, `AttendeeGroup` options,
and future Events; display the derived type list. Disable add for incompatible Events and show
the exact expected/actual type names. Preserve editor values on 409, update the version shown by
the server, and require review before retry. Show the group's shareable public URL. Label both
publication switches separately and announce errors with the existing Banner component.

```csharp
// StaffNavigation: add to Admin and Coordinator lists, nowhere else.
new("/event-groups", "Event groups", "Publish compatible events for self-registration")
```

- [ ] **Step 4: Add page tests and verify green**

Add a complete bUnit page test file following `AttendeeGroupsPageTests.cs`: create group, edit
description, add an Event, toggle group and member independently, and preserve submitted values
after a 409. Verify Admin view contains no attendee list or count. Run Web tests and build.

- [ ] **Step 5: Review, validate, commit and push**

Review intended staged files and the cached diff; run `node scripts/check-ontology-terms.mjs`.

```bash
git add -A
git commit -m "feat(web): manage event groups and publication"
git push
```

### Task 6: Pending registration, public reads and submission

**Files:**
- Create: `src/EventBooking.Domain/SelfRegistrations/SelfRegistration.cs`
- Create: `src/EventBooking.Domain/SelfRegistrations/SelfRegistrationStatus.cs`
- Modify: `src/EventBooking.Domain/Notifications/EmailLog.cs`
- Modify: `src/EventBooking.Domain/Notifications/EmailTemplate.cs`
- Modify: `src/EventBooking.Domain/Audit/AuditAction.cs`
- Modify: `src/EventBooking.Domain/Audit/AuditEntityTypes.cs`
- Modify: `src/EventBooking.Application/Abstractions/ITokenService.cs`
- Create: `src/EventBooking.Application/Abstractions/ISelfRegistrationRepository.cs`
- Create: `src/EventBooking.Application/SelfRegistrations/PublicEventGroupHandlers.cs`
- Create: `src/EventBooking.Application/SelfRegistrations/RequestSelfRegistrationHandler.cs`
- Modify: `src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs`
- Create: `src/EventBooking.Infrastructure/Persistence/Configurations/SelfRegistrationConfiguration.cs`
- Create: `src/EventBooking.Infrastructure/Persistence/Repositories/SelfRegistrationRepository.cs`
- Modify: `src/EventBooking.Infrastructure/Persistence/Configurations/EmailLogConfiguration.cs`
- Create: `src/EventBooking.Infrastructure/Persistence/Migrations/20260925130000_SelfRegistrations.cs`
- Create: `src/EventBooking.Infrastructure/Persistence/Migrations/20260925130000_SelfRegistrations.Designer.cs`
- Modify: `src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs`
- Modify: `src/EventBooking.Infrastructure/Tokens/HmacTokenService.cs`
- Modify: `src/EventBooking.Infrastructure/Email/OutboxDispatcher.cs`
- Modify: `src/EventBooking.Infrastructure/Email/EmailComposer.cs`
- Modify: `src/EventBooking.Application/Notifications/RetryEmailHandler.cs`
- Modify: `src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs`
- Modify: `src/EventBooking.Infrastructure/DependencyInjection.cs`
- Create: `src/EventBooking.Api/Endpoints/PublicEventGroupEndpoints.cs`
- Create: `src/EventBooking.Api/Auth/SelfRegistrationRateLimiterPolicy.cs`
- Modify: `src/EventBooking.Api/Program.cs`
- Modify: `src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs`
- Test: `tests/EventBooking.Domain.Tests/SelfRegistrations/SelfRegistrationTests.cs` (Create)
- Test: `tests/EventBooking.Api.Tests/PublicEventGroupEndpointTests.cs` (Create)
- Test: `tests/EventBooking.Infrastructure.Tests/Email/SelfRegistrationOutboxTests.cs` (Create)

**Interfaces:**

```csharp
namespace EventBooking.Domain.SelfRegistrations;
public enum SelfRegistrationStatus { Pending, Confirmed, Expired }
public sealed class SelfRegistration
{
    public Guid Id { get; private set; }
    public Guid EventGroupId { get; private set; }
    public Guid EventId { get; private set; }
    public Guid AttendeeGroupId { get; private set; }
    public string Name { get; private set; }
    public string Email { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? TerminalAt { get; private set; }
    public SelfRegistrationStatus Status { get; private set; }
    public int TokenVersion { get; private set; }
    public Guid? AttendeeId { get; private set; }
    public Guid? BookingId { get; private set; }
    public static SelfRegistration Create(Guid id, Guid eventGroupId, Guid eventId,
        Guid attendeeGroupId, string? name, string? email, DateTimeOffset now);
    public bool IsConfirmableAt(DateTimeOffset now);
    public void Confirm(Guid attendeeId, Guid bookingId, DateTimeOffset now);
    public void Expire(DateTimeOffset now);
}
namespace EventBooking.Application.Abstractions;
public interface ISelfRegistrationRepository
{
    Task<SelfRegistration?> GetAsync(Guid id, CancellationToken ct);
    Task<SelfRegistration?> LockForUpdateAsync(Guid id, CancellationToken ct);
    Task LockEmailThrottleAsync(string normalizedEmail, CancellationToken ct);
    Task<bool> HasRecentPendingAsync(string normalizedEmail, DateTimeOffset since, CancellationToken ct);
    void Add(SelfRegistration request);
}
namespace EventBooking.Application.SelfRegistrations;
public sealed record PublicAttendeeGroup(Guid Id, string Name, string Description);
public sealed record PublicEvent(Guid Id, string LocationName, string Address,
    DateOnly Date, TimeOnly StartTime, int DurationMinutes,
    IReadOnlyList<string> AppointmentTypeNames,
    IReadOnlyList<Guid> AvailableAttendeeGroupIds); // capacity is event + selected-group specific
public sealed record PublicEventGroup(Guid Id, string Title, string Description,
    IReadOnlyList<PublicAttendeeGroup> AttendeeGroups, IReadOnlyList<PublicEvent> Events);
public sealed record RequestSelfRegistrationCommand(Guid EventGroupId, Guid EventId,
    Guid AttendeeGroupId, string? Name, string? Email);
public sealed class PublicEventGroupHandlers
{
    public Task<Result<PublicEventGroup>> GetGroupAsync(Guid id, CancellationToken ct);
    public Task<Result<PublicEvent>> GetEventAsync(Guid groupId, Guid eventId, CancellationToken ct);
}
public sealed class RequestSelfRegistrationHandler
{
    public Task<Result> HandleAsync(RequestSelfRegistrationCommand command, CancellationToken ct);
}
// TokenPurpose.SelfRegistrationConfirmation = 3; HMAC payload is existing ID/version format.
// EmailLog.RecordSelfRegistrationPending(id, requestId, now) has attendeeId null.
// GET /api/public/event-groups/{id}; GET /{id}/events/{eventId}; POST /{id}/events/{eventId}/registrations.
```

- [ ] **Step 1: Write the failing test**

Create `tests/EventBooking.Domain.Tests/SelfRegistrations/SelfRegistrationTests.cs`:

```csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.SelfRegistrations;

namespace EventBooking.Domain.Tests.SelfRegistrations;

public sealed class SelfRegistrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static SelfRegistration New(string? email = " AMARA@example.test ") =>
        SelfRegistration.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), " Amara Novak ", email, Now);

    [Fact]
    public void RequestNormalizesIdentityAndDoesNotConfirmBeforeEmail()
    {
        var request = New();
        Assert.Equal("Amara Novak", request.Name);
        Assert.Equal("amara@example.test", request.Email);
        Assert.Equal(SelfRegistrationStatus.Pending, request.Status);
        Assert.Equal(Now.AddHours(24), request.ExpiresAt);
        Assert.Null(request.BookingId);
        Assert.True(request.IsConfirmableAt(Now.AddHours(23)));
        Assert.False(request.IsConfirmableAt(Now.AddHours(24)));
    }

    [Fact]
    public void ExpiredOrConfirmedRequestCannotBeConfirmedAgain()
    {
        var request = New();
        request.Confirm(Guid.NewGuid(), Guid.NewGuid(), Now.AddMinutes(1));
        Assert.False(request.IsConfirmableAt(Now.AddMinutes(2)));
        Assert.Throws<DomainException>(() => request.Confirm(
            Guid.NewGuid(), Guid.NewGuid(), Now.AddMinutes(3)));
        var expired = New();
        expired.Expire(Now.AddHours(24));
        Assert.Equal(SelfRegistrationStatus.Expired, expired.Status);
    }
}
```

- [ ] **Step 2: Run the red test**

```bash
dotnet test tests/EventBooking.Domain.Tests --filter "FullyQualifiedName~SelfRegistrationTests"
```

Expected: SelfRegistration types are missing.

- [ ] **Step 3: Implement pending state, persistence, token and outbox**

Validate nonempty IDs, name 1–200, email at most 320 using the same parsing/normalization rule
as Attendee. Expire precisely at 24 hours; stamp TerminalAt only on Confirm/Expire. Add the
`SelfRegistration` table and indexes on `(email, status, expires_at)` and terminal time. Email
Log's AttendeeId becomes nullable; enforce exactly one recipient context ID by a database check.
Keep attendee-scoped retry and dashboard queries restricted to non-null AttendeeId; the staff
retry endpoint does not retry anonymous registration mail. Update their nullable projections and
RecordPending calls explicitly rather than suppressing compiler warnings.
Add a purpose value to HmacTokenService's purpose validation. Compose the confirmation mail from
the row and deterministic token, suppressing a send when it is no longer pending/unexpired.

```csharp
public static EmailLog RecordSelfRegistrationPending(Guid id, Guid requestId, DateTimeOffset now)
    => PendingOrRecorded(id, null, EmailTemplate.SelfRegistrationConfirmation, now,
        EmailStatus.Pending, null, null, null, requestId);
```

- [ ] **Step 4: Implement public reads and submit handler**

Public reads return 404 for closed/unknown group, never include private, cancelled or started
members, and sort by local Event start. Return only names/descriptions, Event/Location/window,
type names and group-specific availability; no capacity counts. The submit handler checks name,
email, chosen active `AttendeeGroup`, both gates, exact type compatibility and current capacity.
It begins a transaction, locks the `EventGroup` row, rechecks both publication gates, takes a
transaction-scoped advisory lock on the normalized email, checks the 60-second cooldown, enqueues
`SelfRegistrationConfirmation` with the new row, and commits.
Audit the request by ID and canonical codes only. Do not query or report whether the email is already an Attendee. A recent pending request for the
same normalized email returns the same accepted response without enqueuing another email for 60
seconds. Use IP rate limiting at the route and the repository query for the email cooldown.

```csharp
// Request handler's successful path; no Event.ChargeRequiredTypes call here.
var request = SelfRegistration.Create(Guid.NewGuid(), group.Id, eventItem.Id,
    selectedGroup.Id, command.Name, command.Email, clock.UtcNow);
registrations.Add(request);
emails.Add(EmailLog.RecordSelfRegistrationPending(Guid.NewGuid(), request.Id, clock.UtcNow));
audit.Record(AuditEntityTypes.SelfRegistration, request.Id,
    AuditAction.SelfRegistrationRequested, ActorType.Anonymous, null,
    $"event {eventItem.Id}; group {selectedGroup.Code}");
await unitOfWork.SaveChangesAsync(ct);
await transaction.CommitAsync(ct);
```

Map public endpoints with AllowAnonymous and 404/409/422/429 declarations; no staff bearer token.
Add API tests for closed/unknown indistinguishability, one Event open in only one group, a full
group, neutral existing-email submit and unchanged capacity. Add an outbox dispatch test that
inspects the email link and confirms no token/body is persisted. Run domain/infrastructure/API
tests and build.

- [ ] **Step 5: Review, validate, commit and push**

Review intended staged files and the cached diff; run `node scripts/check-ontology-terms.mjs`.

```bash
git add -A
git commit -m "feat(registration): accept requests and email confirmation links"
git push
```
