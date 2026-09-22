# 05d — Coordinator, attendee and Help flows (Task 27)

[← Phase overview](phase-5-web.md) · [Previous task](phase-5c-manager-and-operations.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task completes the Web layer with Coordinator workflows, anonymous token pages, audit,
role-aware Help and the Phase 5 accessibility gate. It extends the Playwright/axe harness created
in Task 24; it does not create that safety net at the end of the phase.

> Use superpowers:executing-plans. Apply this document after Task 26 on the same phase branch.

**Goal:** Complete every remaining route and state in design 03b, ship the five role guides in the
standalone bundle, prove anonymous booking outcomes are truthful, and close Phase 5 with every
route covered at both viewport widths.

**Architecture:** Coordinator pages use duplicated Web DTOs and cursor envelopes. BookingClient
uses the anonymous named client, never the OIDC handler. Help fetches immutable Markdown assets
from `wwwroot/help`; it chooses guides from the authenticated role claims when signed in and the
attendee guide when anonymous. AuditHistory lazy-loads once per expanded entity.

**Tech Stack:** Blazor WebAssembly, Markdig, bUnit, Playwright 1.62.0, axe-core 4.13.0.

**Spec:** [Master Task 27](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[remaining screens](../design/03b-screens-and-flows.md#attendees-attendees--coordinator-never-admin),
[Help](../design/03a-design-system-and-ia.md#help), and
[definition of done](../design/08-nonfunctional-requirements.md#definition-of-done).

## Global constraints

The [phase constraints](phase-5-web.md#global-constraints) apply. Admin never receives attendee
data. Booking and manage tokens stay in route parameters and must not be logged. The four manage
outcomes are the exact wire values `reinvited`, `reinvitePending`, `noEligibleEvents` and
`cancelled`. `window-started` and `capacity-exhausted` are matched by RFC problem slug. Role guides
are static bundle assets, not embedded files outside the standalone Web project.

## Review focus

STOP AND CHECK: a CSV rejection changes no rows and says “Nothing was imported.”; an invite count
changes with the selected locations; a capacity race removes only the full option; a post-start
manage page contains contact details and no cancel control; anonymous Help exposes no staff guide;
and the phase gate visits every route/state at mobile and desktop widths.

### Task 27: Coordinator screens, attendee pages, Help and the Phase 5 gate

**Files:**

- Modify: src/EventBooking.Web/EventBooking.Web.csproj
- Modify: src/EventBooking.Web/Program.cs
- Modify: src/EventBooking.Web/appsettings.json
- Modify: src/EventBooking.Web/nginx.conf
- Modify: src/EventBooking.Web/Services/AttendeesClient.cs
- Modify: src/EventBooking.Web/Services/DashboardsClient.cs
- Modify: src/EventBooking.Web/Services/AuditClient.cs
- Modify: src/EventBooking.Web/Services/BookingClient.cs
- Modify: src/EventBooking.Web/Services/UserGuideCatalog.cs
- Create: src/EventBooking.Web/Components/AuditHistory.razor
- Modify: src/EventBooking.Web/Pages/Attendees.razor
- Modify: src/EventBooking.Web/Pages/Dashboards.razor
- Modify: src/EventBooking.Web/Pages/Audit.razor
- Modify: src/EventBooking.Web/Pages/Book.razor
- Modify: src/EventBooking.Web/Pages/ManageBooking.razor
- Modify: src/EventBooking.Web/Pages/Help.razor
- Create: src/EventBooking.Web/wwwroot/help/admin.md
- Create: src/EventBooking.Web/wwwroot/help/coordinator.md
- Create: src/EventBooking.Web/wwwroot/help/manager.md
- Create: src/EventBooking.Web/wwwroot/help/appointment-staff.md
- Create: src/EventBooking.Web/wwwroot/help/attendee.md
- Test: tests/EventBooking.Web.Tests/Pages/Coordinator/CoordinatorPageTests.cs
- Test: tests/EventBooking.Web.Tests/Pages/Attendee/AttendeePageTests.cs
- Test: tests/EventBooking.Web.Tests/Pages/Help/HelpPageTests.cs
- Test: tests/EventBooking.Web.Tests/Pages/Help/GuideAssetTests.cs
- Modify: tests/EventBooking.Web.Tests/Contracts/OpenApiClientContractTests.cs
- Modify: tests/EventBooking.Web.E2E/RouteManifest.cs
- Modify: tests/EventBooking.Web.E2E/RouteSetup.cs
- Modify: tests/EventBooking.Web.E2E/E2EApiStub.cs
- Modify: tests/EventBooking.Web.E2E/AccessibilityTests.cs
- Modify: docs/detailed-implementations/HANDOVER.md (replace Phase 5's unexecuted status with the
  executor's observed build, test, axe and bundle evidence)

**Interfaces:**

```csharp
namespace EventBooking.Web.Services;

public sealed record AttendeeDto(
    Guid Id, string Name, string Email, string Status, string GroupCode, string Readiness,
    IReadOnlyList<string> RequiredTypeCodes, string? LatestDeliveryStatus,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record EligibleEventCountDto(int Count, int RequiredOptionCount);
public sealed record ImportErrorDto(int Line, string Field, string Message);
public sealed record ImportOutcomeDto(int ImportedCount, IReadOnlyList<ImportErrorDto> Errors);
public sealed record InviteOutcomeDto(Guid? InviteId, string Status);
public sealed record CoordinatorLocationDto(Guid Id, string Name, string ZoneAbbreviation);
public sealed record CoordinatorGroupDto(Guid Id, string Code, string Name);
public sealed record CoordinatorReferenceData(
    IReadOnlyList<CoordinatorLocationDto> Locations,
    IReadOnlyList<CoordinatorGroupDto> AttendeeGroups,
    IReadOnlyDictionary<string, ApiLink> CollectionLinks);

public interface IAttendeesClient
{
    // Composes /api/me, /api/locations and /api/attendee-groups for forms and collection links.
    Task<ApiOutcome<CoordinatorReferenceData>> GetReferenceDataAsync(CancellationToken ct);
    Task<ApiOutcome<PageDto<AttendeeDto>>> ListAsync(
        string? status, Guid? groupId, string? readiness, string? search, string? cursor,
        CancellationToken ct);
    Task<ApiOutcome<AttendeeDto>> SaveAsync(
        Guid? id, string name, string email, Guid groupId,
        IdempotencySubmission submission, CancellationToken ct);
    Task<ApiOutcome<ImportOutcomeDto>> ImportAsync(
        Stream csv, string fileName, IdempotencySubmission submission, CancellationToken ct);
    Task<ApiOutcome<EligibleEventCountDto>> CountEligibleAsync(
        Guid attendeeId, IReadOnlyList<Guid> locationIds, CancellationToken ct);
    Task<ApiOutcome<InviteOutcomeDto>> InviteAsync(
        Guid attendeeId, IReadOnlyList<Guid> locationIds,
        IdempotencySubmission submission, CancellationToken ct);
    Task<ApiOutcome<object>> RetryEmailAsync(Guid attendeeId, CancellationToken ct);
}

public sealed record DashboardCountedTab<T>(int Count, IReadOnlyList<T> Rows);
public sealed record AwaitingAvailabilityDto(
    Guid AttendeeId, string Name, string Email, IReadOnlyList<string> RequiredCodes,
    DateOnly WaitingSince, int DaysWaiting);
public sealed record NoResponseDto(
    Guid AttendeeId, string Name, string Email, IReadOnlyList<string> RequiredCodes,
    DateOnly GaveUpOn);
public sealed record DashboardCapacityDto(string Code, int TotalHeadcount, int RemainingCapacity);
public sealed record EventOverviewDto(
    Guid EventId, Guid LocationId, string LocationName, EventTimeDto Time,
    IReadOnlyList<DashboardCapacityDto> Capacities, int ActiveBookings);
public sealed record DashboardsDto(
    DashboardCountedTab<AwaitingAvailabilityDto> AwaitingAvailability,
    DashboardCountedTab<NoResponseDto> NoResponse,
    DashboardCountedTab<EventOverviewDto> Events,
    int FailedEmails, int PendingEmails);
public interface IDashboardsClient
{
    Task<ApiOutcome<DashboardsDto>> GetAsync(Guid? locationId, CancellationToken ct);
}

public sealed record AuditRowDto(
    Guid Id, string EntityType, string Action, string ActorType,
    string ActorDisplay, DateTimeOffset OccurredAt, string Details);
public sealed record AuditFilters(
    DateTimeOffset? From, DateTimeOffset? To, string? ActorType, string? Action,
    string? EntityType, Guid? EntityId, Guid? ActorId);
public interface IAuditClient
{
    Task<ApiOutcome<PageDto<AuditRowDto>>> SearchAsync(
        AuditFilters filters, string? cursor, CancellationToken ct);
    Task<ApiOutcome<PageDto<AuditRowDto>>> ForAttendeeAsync(
        Guid attendeeId, string? cursor, CancellationToken ct);
    Task<ApiOutcome<PageDto<AuditRowDto>>> ForEventAsync(
        Guid eventId, string? cursor, CancellationToken ct);
}

public sealed record InviteOptionDto(
    Guid EventId, string LocationName, string Address, EventTimeDto Time);
public sealed record InviteDto(
    Guid InviteId, string AttendeeName, IReadOnlyList<string> AppointmentTypeNames,
    IReadOnlyList<InviteOptionDto> Options, bool IsRecovery,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record ConfirmBookingOutcomeDto(Guid BookingId, string ManageToken);
public sealed record ManagedBookingDto(
    string AttendeeName, string LocationName, string Address, EventTimeDto Time,
    IReadOnlyList<string> AppointmentTypeNames,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record CancelBookingOutcomeDto(string Outcome, Guid? InviteId);

public interface IBookingClient
{
    Task<ApiOutcome<InviteDto>> ViewInviteAsync(string token, CancellationToken ct);
    Task<ApiOutcome<ConfirmBookingOutcomeDto>> ConfirmAsync(
        string token, Guid eventId, IdempotencySubmission submission, CancellationToken ct);
    Task<ApiOutcome<ManagedBookingDto>> ViewManagedAsync(string token, CancellationToken ct);
    Task<ApiOutcome<CancelBookingOutcomeDto>> CancelAsync(
        string token, bool requestNewTime, IdempotencySubmission submission, CancellationToken ct);
}

public sealed record UserGuide(string Role, string Title, string AssetPath);
public interface IUserGuideCatalog
{
    Task<IReadOnlyList<UserGuide>> ForAsync(
        bool isAuthenticated, IReadOnlyCollection<string> heldRoles, CancellationToken ct);
    Task<string> ReadMarkdownAsync(UserGuide guide, CancellationToken ct);
}
```

OpenApiClientContractTests must compare every API transport record below with its explicitly paired
Task 22b OpenAPI schema. CoordinatorLocationDto, CoordinatorGroupDto,
CoordinatorReferenceData, DashboardCountedTab<T> and UserGuide are client-composed records and do
not belong in the contract array. Application-only view names are not a reason to reference server
assemblies. The focused API prerequisite gives the remaining wire contracts the stable schema names
used below. If the snapshot does not supply the location/address/event-time fields, invite confirm
link or manage `_links` required by design 03b, the dashboard's complete event-time
representation, audit actor display/details, or createAttendee/importAttendees in the caller's
collection links when the result is empty, stop and add that prerequisite commit; do not
reconstruct a window, infer cancellation eligibility, or test a role in the browser.

- [ ] **Step 1: Write the failing test**

Create these complete tests first. Shared fake members omitted from one assertion still return a
valid success so a failure describes the missing behaviour, not test setup.

```csharp
// tests/EventBooking.Web.Tests/Pages/Coordinator/CoordinatorPageTests.cs (complete)
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests.Pages.Coordinator;

public sealed class CoordinatorPageTests : BunitContext
{
    [Fact]
    public void EmptyListStillUsesCollectionLinksForCreateAndImport()
    {
        var api = new FakeAttendeesClient();
        api.Rows.Clear();
        Services.AddSingleton<IAttendeesClient>(api);

        var cut = Render<Attendees>();

        Assert.Single(cut.FindAll("[data-action='new-attendee']"));
        Assert.Single(cut.FindAll("[data-action='open-import']"));
    }

    [Fact]
    public async Task InviteCountChangesWithLocationsAndWarnsBelowRequiredCount()
    {
        var api = new FakeAttendeesClient();
        Services.AddSingleton<IAttendeesClient>(api);
        var cut = Render<Attendees>();

        await cut.Find("[data-action='invite']").ClickAsync(new());
        await cut.Find("input[value='10000000-0000-0000-0000-000000000001']").Change(true);
        await cut.Find("input[value='20000000-0000-0000-0000-000000000002']").Change(true);

        Assert.Equal(2, api.LastLocations.Count);
        Assert.Contains("only 2 eligible events", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("need 3", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RejectedImportListsLinesAndStatesNothingWasImported()
    {
        var api = new FakeAttendeesClient
        {
            ImportResult = ApiOutcome<ImportOutcomeDto>.Success(new(0,
                [new(3, "email", "Email is invalid."), new(7, "attendee_group", "Group is unknown.")]))
        };
        Services.AddSingleton<IAttendeesClient>(api);
        var cut = Render<Attendees>();

        cut.FindComponent<Microsoft.AspNetCore.Components.Forms.InputFile>().UploadFiles(
            InputFileContent.CreateFromText(
                "name,email,attendee_group\nA,bad,X", "attendees.csv", "text/csv"));
        await cut.Find("[data-action='upload-csv']").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Contains("Line 3", cut.Markup));
        Assert.Contains("Line 7", cut.Markup);
        Assert.Contains("Nothing was imported.", cut.Markup);
    }

    [Fact]
    public async Task AuditPanelLoadsOnceOnFirstExpand()
    {
        var audit = new FakeAuditClient();
        Services.AddSingleton<IAuditClient>(audit);
        var cut = Render<EventBooking.Web.Components.AuditHistory>(p => p
            .Add(x => x.EntityKind, AuditEntityKind.Event)
            .Add(x => x.EntityId, Guid.NewGuid()));
        await cut.Find("button").ClickAsync(new());
        await cut.Find("button").ClickAsync(new());
        await cut.Find("button").ClickAsync(new());
        Assert.Equal(1, audit.Reads);
    }

    private sealed class FakeAttendeesClient : IAttendeesClient
    {
        public List<AttendeeDto> Rows { get; } = [new(Guid.NewGuid(), "T. Okafor", "t@example.org",
            "AwaitingAvailability", "OFFICE", "AppointmentsOutstanding", ["IND"], "Failed",
            new Dictionary<string, ApiLink> { ["invite"] = new("/invites", "POST", "inviteAttendee") })];
        public IReadOnlyList<Guid> LastLocations { get; private set; } = [];
        public ApiOutcome<ImportOutcomeDto> ImportResult { get; init; } = ApiOutcome<ImportOutcomeDto>.Success(new(1, []));
        public Task<ApiOutcome<CoordinatorReferenceData>> GetReferenceDataAsync(CancellationToken ct) =>
            Task.FromResult(ApiOutcome<CoordinatorReferenceData>.Success(new(
                [new(Guid.Parse("10000000-0000-0000-0000-000000000001"), "London HQ", "BST"),
                 new(Guid.Parse("20000000-0000-0000-0000-000000000002"), "Dublin Centre", "IST")],
                [new(Guid.NewGuid(), "OFFICE", "Office staff")],
                new Dictionary<string, ApiLink>
                {
                    ["createAttendee"] = new("/api/attendees", "POST", "createAttendee"),
                    ["importAttendees"] = new("/api/attendees/import", "POST", "importAttendees"),
                })));
        public Task<ApiOutcome<PageDto<AttendeeDto>>> ListAsync(string? status, Guid? groupId, string? readiness, string? search, string? cursor, CancellationToken ct) => Task.FromResult(ApiOutcome<PageDto<AttendeeDto>>.Success(new(Rows, null)));
        public Task<ApiOutcome<AttendeeDto>> SaveAsync(Guid? id, string name, string email, Guid groupId, IdempotencySubmission submission, CancellationToken ct) => Task.FromResult(ApiOutcome<AttendeeDto>.Success(Rows.First()));
        public Task<ApiOutcome<ImportOutcomeDto>> ImportAsync(Stream csv, string fileName, IdempotencySubmission submission, CancellationToken ct) => Task.FromResult(ImportResult);
        public Task<ApiOutcome<EligibleEventCountDto>> CountEligibleAsync(Guid attendeeId, IReadOnlyList<Guid> locationIds, CancellationToken ct) { LastLocations = locationIds; return Task.FromResult(ApiOutcome<EligibleEventCountDto>.Success(new(locationIds.Count, 3))); }
        public Task<ApiOutcome<InviteOutcomeDto>> InviteAsync(Guid attendeeId, IReadOnlyList<Guid> locationIds, IdempotencySubmission submission, CancellationToken ct) => Task.FromResult(ApiOutcome<InviteOutcomeDto>.Success(new(Guid.NewGuid(), "Invited")));
        public Task<ApiOutcome<object>> RetryEmailAsync(Guid attendeeId, CancellationToken ct) => Task.FromResult(ApiOutcome<object>.Success(new()));
    }

    private sealed class FakeAuditClient : IAuditClient
    {
        public int Reads { get; private set; }
        public Task<ApiOutcome<PageDto<AuditRowDto>>> ForEventAsync(Guid eventId, string? cursor, CancellationToken ct) { Reads++; return Page(); }
        public Task<ApiOutcome<PageDto<AuditRowDto>>> ForAttendeeAsync(Guid attendeeId, string? cursor, CancellationToken ct) { Reads++; return Page(); }
        public Task<ApiOutcome<PageDto<AuditRowDto>>> SearchAsync(AuditFilters filters, string? cursor, CancellationToken ct) => Page();
        private static Task<ApiOutcome<PageDto<AuditRowDto>>> Page() => Task.FromResult(ApiOutcome<PageDto<AuditRowDto>>.Success(new([], null)));
    }
}
```

```csharp
// tests/EventBooking.Web.Tests/Pages/Attendee/AttendeePageTests.cs (complete)
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests.Pages.Attendee;

public sealed class AttendeePageTests : BunitContext
{
    [Fact]
    public async Task CapacityRaceRemovesOnlyFilledOptionAndKeepsTheRest()
    {
        var api = new FakeBookingClient { ConfirmProblem = ApiProblem.FromSlug("capacity-exhausted", "Full.") };
        Services.AddSingleton<IBookingClient>(api);
        var cut = Render<Book>(p => p.Add(x => x.Token, "book-token"));
        cut.Find("input[value='10000000-0000-0000-0000-000000000001']").Change(true);
        await cut.Find("[data-action='confirm-booking']").ClickAsync(new());

        Assert.Contains("That time has just filled up. Please choose another.", cut.Markup);
        Assert.DoesNotContain("London HQ", cut.Markup);
        Assert.Contains("Dublin Centre", cut.Markup);
    }

    [Theory]
    [InlineData("reinvited", "A new invitation is ready")]
    [InlineData("reinvitePending", "A replacement invitation is already being arranged")]
    [InlineData("noEligibleEvents", "We'll be in touch when another time is available")]
    [InlineData("cancelled", "Your booking is cancelled")]
    public async Task ManageRendersEachTruthfulOutcome(string outcome, string expected)
    {
        Services.AddSingleton<IBookingClient>(new FakeBookingClient { Outcome = outcome });
        var cut = Render<ManageBooking>(p => p.Add(x => x.Token, "manage-token"));
        await cut.Find("[data-action='cancel-booking']").ClickAsync(new());
        Assert.Contains(expected, cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StartedBookingShowsContactAndNoCancelControl()
    {
        Services.AddSingleton<IBookingClient>(new FakeBookingClient(includeCancelLink: false));
        Services.AddSingleton(new ProductOptions("EventBooking", null, "events@example.org"));
        var cut = Render<ManageBooking>(p => p.Add(x => x.Token, "manage-token"));
        Assert.Contains("events@example.org", cut.Markup);
        Assert.Contains("already started", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(cut.FindAll("[data-action='cancel-booking']"));
    }

    private sealed class FakeBookingClient(bool includeCancelLink = true) : IBookingClient
    {
        private static readonly Guid London = Guid.Parse("10000000-0000-0000-0000-000000000001");
        private static readonly Guid Dublin = Guid.Parse("20000000-0000-0000-0000-000000000002");
        public ApiProblem? ConfirmProblem { get; init; }
        public string Outcome { get; init; } = "cancelled";
        public Task<ApiOutcome<InviteDto>> ViewInviteAsync(string token, CancellationToken ct) => Task.FromResult(ApiOutcome<InviteDto>.Success(new(Guid.NewGuid(), "Ravi", ["Medical check"], [
            new(London, "London HQ", "1 Example St", TestContractFactory.EventTime("Tue 14 Oct 2026, 09:30–11:00 BST")),
            new(Dublin, "Dublin Centre", "2 Sample Rd", TestContractFactory.EventTime("Wed 15 Oct 2026, 13:00–17:00 IST"))], false,
            new Dictionary<string, ApiLink>
            {
                ["confirm"] = new("/api/booking/book-token/confirm", "POST", "confirmBooking"),
            })));
        public Task<ApiOutcome<ConfirmBookingOutcomeDto>> ConfirmAsync(string token, Guid eventId, IdempotencySubmission submission, CancellationToken ct) => Task.FromResult(ConfirmProblem is null ? ApiOutcome<ConfirmBookingOutcomeDto>.Success(new(Guid.NewGuid(), "manage-token")) : ApiOutcome<ConfirmBookingOutcomeDto>.Failure(ConfirmProblem));
        public Task<ApiOutcome<ManagedBookingDto>> ViewManagedAsync(string token, CancellationToken ct) => Task.FromResult(ApiOutcome<ManagedBookingDto>.Success(new("Ravi", "London HQ", "1 Example St", TestContractFactory.EventTime("Tue 14 Oct 2026, 09:30–11:00 BST"), ["Medical check"], includeCancelLink ? new Dictionary<string,ApiLink> { ["cancel"] = new("/cancel", "POST", "cancelManagedBooking") } : new Dictionary<string,ApiLink>())));
        public Task<ApiOutcome<CancelBookingOutcomeDto>> CancelAsync(string token, bool requestNewTime, IdempotencySubmission submission, CancellationToken ct) => Task.FromResult(ApiOutcome<CancelBookingOutcomeDto>.Success(new(Outcome, Outcome == "reinvited" ? Guid.NewGuid() : null)));
    }
}
```

```csharp
// tests/EventBooking.Web.Tests/Pages/Help/HelpPageTests.cs (complete)
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace EventBooking.Web.Tests.Pages.Help;

public sealed class HelpPageTests : BunitContext
{
    [Fact]
    public void SignedInUserSeesEveryHeldRoleAndContents()
    {
        Services.AddSingleton<IUserGuideCatalog>(new FakeCatalog());
        Services.AddSingleton<AuthenticationStateProvider>(new StateProvider("Coordinator", "Manager"));
        var cut = Render<Help>();
        Assert.Contains("Contents", cut.Markup);
        Assert.Contains("Coordinator guide", cut.Markup);
        Assert.Contains("Manager guide", cut.Markup);
        Assert.DoesNotContain("Admin guide", cut.Markup);
        Assert.DoesNotContain("Attendee guide", cut.Markup);
    }

    [Fact]
    public void AnonymousUserSeesOnlyAttendeeGuide()
    {
        Services.AddSingleton<IUserGuideCatalog>(new FakeCatalog());
        Services.AddSingleton<AuthenticationStateProvider>(new StateProvider());
        var cut = Render<Help>();
        Assert.Contains("Attendee guide", cut.Markup);
        Assert.DoesNotContain("Coordinator guide", cut.Markup);
        Assert.DoesNotContain("Admin guide", cut.Markup);
    }

    private sealed class FakeCatalog : IUserGuideCatalog
    {
        public Task<IReadOnlyList<UserGuide>> ForAsync(bool authenticated, IReadOnlyCollection<string> roles, CancellationToken ct) => Task.FromResult<IReadOnlyList<UserGuide>>(authenticated ? roles.Select(r => new UserGuide(r, $"{r} guide", $"help/{r.ToLowerInvariant()}.md")).ToArray() : [new("Attendee", "Attendee guide", "help/attendee.md")]);
        public Task<string> ReadMarkdownAsync(UserGuide guide, CancellationToken ct) => Task.FromResult($"# {guide.Title}\n\nUse this guide.");
    }

    private sealed class StateProvider(params string[] roles) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = roles.Length == 0 ? new ClaimsIdentity() : new ClaimsIdentity(roles.Select(x => new Claim(ClaimTypes.Role, x)), "test");
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }
}
```

```csharp
// tests/EventBooking.Web.Tests/Pages/Help/GuideAssetTests.cs (complete)
namespace EventBooking.Web.Tests.Pages.Help;

public sealed class GuideAssetTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "../../../../../src/EventBooking.Web/wwwroot/help"));

    [Theory]
    [InlineData("admin.md", "Locations", "Staff access", "Event operations", "Audit search")]
    [InlineData("coordinator.md", "Attendees", "Dashboards", "Event operations", "Audit search")]
    [InlineData("manager.md", "Negotiation", "Appointment workspace", "headcount", "capacity")]
    [InlineData("appointment-staff.md", "Appointment workspace", "Check in", "Complete", "No-show")]
    [InlineData("attendee.md", "Choose a time", "Manage", "cancel", "coordinator")]
    public void GuideContainsRoleTasks(string file, params string[] required)
    {
        var markdown = File.ReadAllText(Path.Combine(Root, file));
        Assert.StartsWith("# ", markdown);
        Assert.All(required, value => Assert.Contains(value, markdown, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("JointBooking", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("candidate", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("slot", markdown, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 2: Run the focused tests and verify the red state**

```bash
dotnet test tests/EventBooking.Web.Tests --filter "FullyQualifiedName~Coordinator|FullyQualifiedName~AttendeePage|FullyQualifiedName~Help"
```

Expected: FAIL because the predecessor services, pages, guide source and response handling remain.

- [ ] **Step 3: Implement Coordinator clients, pages and audit history**

Implement the clients against these exact routes, using the idempotency submission only on the
create routes named by design 05:

```text
GET      /api/me
GET      /api/locations
GET      /api/attendee-groups
GET|POST /api/attendees
POST     /api/attendees/import
GET      /api/attendees/{id}/eligible-event-count?locationIds={id}&locationIds={id}
POST     /api/attendees/{id}/invites
POST     /api/attendees/{id}/email-retry
GET      /api/dashboards?locationId={id}
GET      /api/audit?from={value}&to={value}&action={value}&entityType={value}&entityId={value}&cursor={cursor}
GET      /api/audit/attendees/{id}?cursor={cursor}
GET      /api/audit/events/{id}?cursor={cursor}
```

`Attendees.razor` owns filters, cursor append, edit/import/invite dialogs, and the row actions
present in `_links`. It renders New attendee and Import only from
CoordinatorReferenceData.CollectionLinks, including when Items is empty. Debounce eligible-count
reads and cancel the predecessor request when a
Location selection changes. Do not enable Send invite while count is zero. Render each import
error with its line and field through CsvImportResult; any error forces the all-or-nothing
statement.

`Dashboards.razor` renders the three named tabs and counts, failed/pending email totals, Location
filter and lazy event audit. `Audit.razor` sends only non-empty filters, appends cursor pages, and
renders details as text. `AuditHistory.razor` loads on first expansion, caches that result while
collapsed, walks cursors on demand, and never offers attendee history when the response lacks its
link.

- [ ] **Step 4: Implement anonymous book/manage flows**

Register IBookingClient with the anonymous named HttpClient; bearer tokens must never be sent
to `/api/booking` or `/api/manage`. Keep one idempotency key while a confirm/cancel submission is
retried and discard it only on success or a changed intent.

```csharp
// src/EventBooking.Web/Program.cs — all Task 27 service registrations.
builder.Services.AddScoped<IAttendeesClient, AttendeesClient>();
builder.Services.AddScoped<IDashboardsClient, DashboardsClient>();
builder.Services.AddScoped<IAuditClient, AuditClient>();
builder.Services.AddScoped<IBookingClient>(services => new BookingClient(
    services.GetRequiredService<IHttpClientFactory>().CreateClient("AnonymousApi")));
builder.Services.AddScoped<IUserGuideCatalog, UserGuideCatalog>();
```

`Book.razor` renders location name, address and server-provided EventTimeDto for each option. It
renders confirmation only from InviteDto.Links["confirm"]. On
`capacity-exhausted`, remove the selected option, announce the exact design copy, and retain all
others. Expired/superseded links replace the page; no-options says “We'll be in touch”. Success
shows the event, location and a link to `/manage/{manageToken}` without logging the token.

`ManageBooking.razor` displays its server-provided event and type names. Render cancellation only
when the `cancel` link exists. Map the four outcome strings exhaustively; an unknown value is an
error, not a cheerful generic success. If the link is absent or a submission returns
`window-started`, remove the control and show `ProductOptions.CoordinatorContact`.

No result or test count in this Step 4 is observed: the document remains an authored execution
plan until the executor runs its focused tests and the full gate in Step 6.

- [ ] **Step 5: Move Help wholly into the standalone bundle**

Remove the predecessor embedded-guide items from `EventBooking.Web.csproj`; static Web assets
under `wwwroot/help` need no server project reference. In `nginx.conf`, make `/help` the SPA route
without shadowing `/help/*.md`:

```nginx
location = /help {
    try_files /index.html =404;
}

location / {
    try_files $uri $uri/ /index.html;
}
```

Implement UserGuideCatalog with the exact role-to-asset map below. De-duplicate roles, preserve
Admin/Coordinator/Manager/AppointmentStaff order, and return only attendee when anonymous.

```csharp
private static readonly IReadOnlyDictionary<string, UserGuide> Guides =
    new Dictionary<string, UserGuide>(StringComparer.OrdinalIgnoreCase)
    {
        ["Admin"] = new("Admin", "Administrator guide", "help/admin.md"),
        ["Coordinator"] = new("Coordinator", "Coordinator guide", "help/coordinator.md"),
        ["Manager"] = new("Manager", "Manager guide", "help/manager.md"),
        ["AppointmentStaff"] = new("AppointmentStaff", "Appointment staff guide", "help/appointment-staff.md"),
        ["Attendee"] = new("Attendee", "Attendee guide", "help/attendee.md"),
    };
```

Write the five guide assets as operational guides, not feature lists. Each starts with a single
H1, a short “What you can do” list, numbered procedures matching the current UI labels, the states
the person may encounter, and where to get help. Required subjects are pinned by GuideAssetTests;
also cover these role-specific details:

- Admin: immutable codes, IANA zone impact, group-change confirmation, displaced Manager notice,
  two-step event cancellation, and event-bucket audit visibility.
- Coordinator: multi-location invitation count, all-or-nothing CSV, recovery, delivery retry,
  dashboards, two-step booking/event cancellation, and attendee audit history.
- Manager: N-type proposal, locked own type, privacy of other headcounts, capacity minimum,
  proposal withdrawal, and own-type workspace corrections.
- AppointmentStaff: server-assigned type, grouped event selection, local-date check-in,
  post-window no-show, conflict refresh and CSV roster.
- Attendee: emailed link, local time/location/address, capacity-race message, manage link, all four
  cancellation outcomes, post-start contact, and never sharing either token.

`Help.razor` turns each fetched Markdown file into sanitized HTML with Markdig. With multiple held
roles it renders a contents list linking to stable role section ids. It must not request any staff
guide for an anonymous principal. Remove any page-level authorize attribute: `/help` is deliberately
public, and the authentication state selects staff guides without making sign-in a prerequisite.
Build the Markdig pipeline with `DisableHtml()` so a guide cannot inject raw HTML; generate the
contents links from the known role map rather than accepting arbitrary fragment identifiers.

- [ ] **Step 6: Complete the route manifest and Phase 5 gate**

Add every remaining state to RouteManifest and E2EApiStub: attendee empty/results/import
failure/invite warning; each dashboard tab and empty state; audit empty/paged/error; book
choose/no-options/expired/capacity-race/confirmed; manage normal/four outcomes/window-started;
Help anonymous, one-role and multi-role. Seed route tokens as constants in the stub and never print
them. Keep the Task 24 workflow job; Task 27 extends its manifest rather than introducing a late
E2E project.

The final AccessibilityTests assertion is one data-driven theory over every manifest case and
both viewports. It asserts zero serious or critical axe violations, no horizontal overflow at
390 CSS pixels, one H1, a non-empty title, labelled controls, visible keyboard focus, and no
console errors. Preserve per-case snapshots only on failure.

Use these concrete contract and E2E merge fragments:

```csharp
// tests/EventBooking.Web.Tests/Contracts/OpenApiClientContractTests.cs — append to Contracts.
(typeof(AttendeeDto), "AttendeeResponse"),
(typeof(EligibleEventCountDto), "EligibleEventCountResponse"),
(typeof(ImportErrorDto), "AttendeeImportErrorResponse"),
(typeof(ImportOutcomeDto), "AttendeeImportResponse"),
(typeof(InviteOutcomeDto), "InviteAttendeeResponse"),
(typeof(AwaitingAvailabilityDto), "AwaitingAvailabilityResponse"),
(typeof(NoResponseDto), "NoResponseResponse"),
(typeof(DashboardCapacityDto), "DashboardCapacityResponse"),
(typeof(EventOverviewDto), "DashboardEventResponse"),
(typeof(DashboardsDto), "DashboardsResponse"),
(typeof(AuditRowDto), "AuditRowResponse"),
(typeof(InviteDto), "InviteResponse"),
(typeof(InviteOptionDto), "InviteOptionResponse"),
(typeof(ConfirmBookingOutcomeDto), "ConfirmBookingResponse"),
(typeof(ManagedBookingDto), "ManagedBookingResponse"),
(typeof(CancelBookingOutcomeDto), "AttendeeCancelOutcome"),
```

```csharp
// tests/EventBooking.Web.E2E/RouteManifest.cs — append before the closing collection bracket.
new("attendees-ready", "/attendees"),
new("attendees-empty", "/attendees", "attendees-empty"),
new("attendees-error", "/attendees", "attendees-error"),
new("attendees-import-error", "/attendees", "import-error", "import-invalid-csv"),
new("attendees-invite-warning", "/attendees", "invite-warning", "open-invite"),
new("dashboards-awaiting", "/dashboards", "awaiting-tab"),
new("dashboards-no-response", "/dashboards", "no-response-tab"),
new("dashboards-events", "/dashboards", "events-tab"),
new("dashboards-error", "/dashboards", "dashboards-error"),
new("audit-empty", "/audit", "audit-empty"),
new("audit-paged", "/audit", "audit-paged"),
new("audit-error", "/audit", "audit-error"),
new("book-choose", $"/book/{E2EApiStub.BookToken}"),
new("book-no-options", $"/book/{E2EApiStub.NoOptionsToken}"),
new("book-expired", $"/book/{E2EApiStub.ExpiredToken}"),
new("book-capacity-race", $"/book/{E2EApiStub.BookToken}", "capacity-race", "confirm-booking"),
new("book-confirmed", $"/book/{E2EApiStub.BookToken}", "confirmed", "confirm-booking"),
new("manage-ready", $"/manage/{E2EApiStub.ManageToken}"),
new("manage-reinvited", $"/manage/{E2EApiStub.ManageToken}", "reinvited", "cancel-booking"),
new("manage-reinvite-pending", $"/manage/{E2EApiStub.ManageToken}", "reinvitePending", "cancel-booking"),
new("manage-no-events", $"/manage/{E2EApiStub.ManageToken}", "noEligibleEvents", "cancel-booking"),
new("manage-cancelled", $"/manage/{E2EApiStub.ManageToken}", "cancelled", "cancel-booking"),
new("manage-started", $"/manage/{E2EApiStub.StartedToken}"),
new("help-anonymous", "/help?e2eRoles=anonymous", "anonymous"),
new("help-one-role", "/help?e2eRoles=Coordinator", "one-role"),
new("help-multi-role", "/help?e2eRoles=Coordinator%2CManager", "multi-role"),
```

```csharp
// tests/EventBooking.Web.E2E/RouteSetup.cs — add these entries to the accumulated Actions map.
["import-invalid-csv"] = async page =>
{
    await page.Locator("input[type='file']").SetInputFilesAsync(new FilePayload
    {
        Name = "attendees.csv", MimeType = "text/csv",
        Buffer = System.Text.Encoding.UTF8.GetBytes("name,email,attendee_group\nBad,bad,UNKNOWN"),
    });
    await page.Locator("[data-action='upload-csv']").ClickAsync();
},
["open-invite"] = page => page.Locator("[data-action='invite']").First.ClickAsync(),
["awaiting-tab"] = page => page.Locator("[data-tab='awaiting-availability']").ClickAsync(),
["no-response-tab"] = page => page.Locator("[data-tab='no-response']").ClickAsync(),
["events-tab"] = page => page.Locator("[data-tab='events']").ClickAsync(),
["confirm-booking"] = async page =>
{
    await page.Locator("input[name='eventId']").First.CheckAsync();
    await page.Locator("[data-action='confirm-booking']").ClickAsync();
},
["cancel-booking"] = page => page.Locator("[data-action='cancel-booking']").ClickAsync(),
```

```csharp
// tests/EventBooking.Web.E2E/E2EApiStub.cs — constants stay fake and are never written to output.
public const string BookToken = "e2e-book-ready";
public const string NoOptionsToken = "e2e-book-empty";
public const string ExpiredToken = "e2e-book-expired";
public const string ManageToken = "e2e-manage-ready";
public const string StartedToken = "e2e-manage-started";

private static void MapTask27(WebApplication app)
{
    var attendeeId = Guid.Parse("50000000-0000-0000-0000-000000000005");
    var eventId = Guid.Parse("30000000-0000-0000-0000-000000000003");
    var time = new { date = "2026-10-14", startTime = "09:30:00", durationMinutes = 90,
        startLocal = "2026-10-14T09:30:00+01:00", endLocal = "2026-10-14T11:00:00+01:00",
        startUtc = "2026-10-14T08:30:00Z", endUtc = "2026-10-14T10:00:00Z",
        timeZoneId = "Europe/London", zoneAbbreviation = "BST" };
    app.MapGet("/api/attendees", (HttpContext context) =>
    {
        if (FixtureState(context) == "attendees-error")
            return Results.Problem(statusCode: 500, type: "unexpected");
        object[] items = FixtureState(context) == "attendees-empty" ? [] : [new {
        id = attendeeId, name = "T. Okafor", email = "t@example.org", status = "AwaitingAvailability",
        groupCode = "OFFICE", readiness = "AppointmentsOutstanding", requiredTypeCodes = new[] { "IND" },
        latestDeliveryStatus = "Failed", _links = new Dictionary<string, object> {
            ["invite"] = new { href = $"/api/attendees/{attendeeId}/invites", method = "POST", operationId = "inviteAttendee" } } }];
        return Results.Json(new { items, nextCursor = (string?)null });
    });
    app.MapGet("/api/attendees/{id:guid}/eligible-event-count", () =>
        Results.Json(new { count = 2, requiredOptionCount = 3 }));
    app.MapPost("/api/attendees/import", (HttpContext context) =>
        Results.Json(FixtureState(context) == "import-error"
            ? new { importedCount = 0, errors = new[] { new { line = 2, field = "email", message = "Email is invalid." } } }
            : new { importedCount = 1, errors = Array.Empty<object>() }));
    app.MapGet("/api/dashboards", (HttpContext context) =>
    {
        var state = FixtureState(context);
        if (state == "dashboards-error")
            return Results.Problem(statusCode: 500, type: "unexpected");
        object[] awaiting = state == "awaiting-tab" ? [new { attendeeId, name = "T. Okafor",
            email = "t@example.org", requiredCodes = new[] { "IND" }, waitingSince = "2026-10-01",
            daysWaiting = 13 }] : [];
        object[] noResponse = state == "no-response-tab" ? [new { attendeeId, name = "T. Okafor",
            email = "t@example.org", requiredCodes = new[] { "IND" }, gaveUpOn = "2026-10-10" }] : [];
        object[] events = state == "events-tab" ? [new { eventId, locationId = Guid.NewGuid(),
            locationName = "London HQ", time, capacities = new[] { new { code = "MED",
            totalHeadcount = 6, remainingCapacity = 3 } }, activeBookings = 3 }] : [];
        return Results.Json(new
        {
        awaitingAvailability = new { count = awaiting.Length, rows = awaiting },
        noResponse = new { count = noResponse.Length, rows = noResponse },
        events = new { count = events.Length, rows = events },
        failedEmails = 0,
        pendingEmails = 0,
        });
    });
    app.MapGet("/api/audit", (HttpContext context) =>
    {
        var state = FixtureState(context);
        if (state == "audit-error")
            return Results.Problem(statusCode: 500, type: "unexpected");
        object[] items = state == "audit-paged" ? [new { id = Guid.NewGuid(), entityType = "Attendee",
            action = "Updated", actorType = "Staff", actorDisplay = "E2E staff member",
            occurredAt = "2026-10-14T08:00:00Z", details = "Name changed" }] : [];
        return Results.Json(new { items, nextCursor = state == "audit-paged" ? "next" : null });
    });
    app.MapGet("/api/booking/{token}", (string token) => token == ExpiredToken
        ? Results.Problem(statusCode: 410, type: "token-expired")
        : Results.Json(new { inviteId = Guid.NewGuid(), attendeeName = "Ravi",
            appointmentTypeNames = new[] { "Medical check" }, options = token == NoOptionsToken
                ? Array.Empty<object>() : [new { eventId, locationName = "London HQ",
                    address = "1 Example St", time }], isRecovery = false,
            _links = token == NoOptionsToken ? new Dictionary<string, object>() :
                new Dictionary<string, object> { ["confirm"] = new { href = $"/api/booking/{token}/confirm", method = "POST", operationId = "confirmBooking" } } }));
    app.MapPost("/api/booking/{token}/confirm", (HttpContext context, string token) =>
        FixtureState(context) == "capacity-race"
            ? Results.Problem(statusCode: 409, type: "capacity-exhausted")
            : Results.Created($"/api/manage/{ManageToken}", new { bookingId = Guid.NewGuid(), manageToken = ManageToken }));
    app.MapGet("/api/manage/{token}", (string token) => Results.Json(new
    {
        attendeeName = "Ravi", locationName = "London HQ", address = "1 Example St", time,
        appointmentTypeNames = new[] { "Medical check" },
        _links = token == StartedToken ? new Dictionary<string, object>() :
            new Dictionary<string, object> { ["cancel"] = new { href = $"/api/manage/{token}/cancel", method = "POST", operationId = "cancelManagedBooking" } },
    }));
    app.MapPost("/api/manage/{token}/cancel", (HttpContext context) =>
    {
        var state = FixtureState(context);
        var outcome = state is "reinvited" or "reinvitePending" or "noEligibleEvents" or "cancelled"
            ? state : "cancelled";
        return Results.Json(new { outcome, inviteId = outcome == "reinvited" ? Guid.NewGuid() : (Guid?)null });
    });
}
```

```csharp
// tests/EventBooking.Web.E2E/AccessibilityTests.cs — replace the route theory from Task 24.
public static TheoryData<string, string, string, string?, int, int> Cases()
{
    var data = new TheoryData<string, string, string, string?, int, int>();
    foreach (var route in RouteManifest.All)
    foreach (var viewport in new[] { (Width: 390, Height: 844), (Width: 1440, Height: 900) })
        data.Add(route.Name, route.Path, route.FixtureState, route.SetupAction, viewport.Width, viewport.Height);
    return data;
}

[Theory]
[MemberData(nameof(Cases))]
public async Task RouteIsAccessibleAtViewport(
    string name, string path, string fixtureState, string? setupAction, int width, int height)
{
    await using var context = await _browser!.NewContextAsync(new() { ViewportSize = new() { Width = width, Height = height } });
    var host = new Uri(_host.BaseUrl);
    await context.AddCookiesAsync([new Cookie { Name = E2EApiStub.StateCookie,
        Value = fixtureState, Domain = host.Host, Path = "/", HttpOnly = true,
        SameSite = SameSiteAttribute.Lax }]);
    var page = await context.NewPageAsync();
    var consoleErrors = new List<string>();
    page.Console += (_, message) =>
    {
        if (message.Type == "error") consoleErrors.Add(message.Text);
    };
    await page.GotoAsync(_host.BaseUrl + path, new() { WaitUntil = WaitUntilState.NetworkIdle });
    await RouteSetup.ApplyAsync(page, setupAction);
    Assert.Empty(await AxeRunner.ViolationsAsync(page, _host.AxeScript));
    Assert.Equal(1, await page.Locator("h1").CountAsync());
    Assert.False(string.IsNullOrWhiteSpace(await page.TitleAsync()));
    Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= document.documentElement.clientWidth"));
    var unlabelled = await page.EvaluateAsync<string[]>("""
        [...document.querySelectorAll('input,select,textarea')]
          .filter(e => !e.closest('label') && !e.getAttribute('aria-label') &&
            !(e.id && document.querySelector(`label[for="${e.id}"]`)))
          .map(e => `${e.tagName.toLowerCase()}#${e.id}`)
        """);
    Assert.Empty(unlabelled);
    await page.Keyboard.PressAsync("Tab");
    Assert.True(await page.EvaluateAsync<bool>("document.activeElement !== document.body && document.activeElement.matches(':focus-visible')"));
    Assert.True(consoleErrors.Count == 0, $"{name}: {string.Join(Environment.NewLine, consoleErrors)}");
}
```

Run the guide lint explicitly because new untracked Markdown is not discovered by `git ls-files`:

```bash
node scripts/check-ontology-terms.mjs \
  --also src/EventBooking.Web/wwwroot/help/admin.md \
  --also src/EventBooking.Web/wwwroot/help/coordinator.md \
  --also src/EventBooking.Web/wwwroot/help/manager.md \
  --also src/EventBooking.Web/wwwroot/help/appointment-staff.md \
  --also src/EventBooking.Web/wwwroot/help/attendee.md
```

Then run both section 6a mechanical sweeps and the full gate:

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
dotnet publish src/EventBooking.Web/EventBooking.Web.csproj -c Release -o .artifacts/web
test -f .artifacts/web/wwwroot/_framework/blazor.boot.json
test -n "$(find .artifacts/web/wwwroot -type f \( -name '*.br' -o -name '*.gz' \) -print -quit)"
! rg -n 'EventBooking\.(Api|Application|Domain|Infrastructure)\.wasm' \
  .artifacts/web/wwwroot/_framework/blazor.boot.json
dotnet test tests/EventBooking.Web.E2E -c Release
```

Expected: PASS, zero skipped tests, a trimmed/compressed standalone bundle with no server assembly,
and zero axe violations on every route/state at both widths. Record the executor's real counts and
bundle bytes in `HANDOVER.md`; do not copy Task 11's 1,570-test baseline forward.

- [ ] **Step 7: Draft the Phase 5 pull-request body and stop at the approval boundary**

This is decision-bearing because it realizes D10's neutral theme. Draft
`/tmp/eventbooking-phase-5-pr.md` with all three Narrative headings and a temporary
`AI-Fingerprint: sha256:PENDING` footer. Include the verification commands that will be run above;
replace their results with the executor's observed counts and bundle bytes before the commit.
Do not run `gh pr create` in this task. Opening the pull request is a later user-confirmed action.

The body must include `## Narrative Context`, `## Narrative Decision`,
`## Narrative Consequences`, verification with observed counts, and the fingerprint footer.

After Step 8 has pushed, show the title and fully rendered body to the user and ask before opening
the pull request. Only after explicit confirmation, run this separately (it is not part of Task
27's implementation commit):

```bash
gh pr create \
  --title "feat(web): standalone EventBooking front end" \
  --body-file /tmp/eventbooking-phase-5-pr.md \
  --label narrative-required
```

If any later commit is pushed, recompute the fingerprint and update the open pull-request body.

- [ ] **Step 8: Commit and push**

```bash
git add -A
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
git commit -m "feat(web): coordinator and attendee flows with location selection"
MERGE_BASE=$(git merge-base origin/main HEAD)
FINGERPRINT=$(git diff "$MERGE_BASE" HEAD | shasum -a 256 | cut -c1-12)
sed -i.bak "s/AI-Fingerprint: sha256:PENDING/AI-Fingerprint: sha256:$FINGERPRINT/" \
  /tmp/eventbooking-phase-5-pr.md
rm /tmp/eventbooking-phase-5-pr.md.bak
node scripts/check-ontology-terms.mjs --also /tmp/eventbooking-phase-5-pr.md
git push
```
