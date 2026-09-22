# 05c — Manager and operations screens (Task 26)

[← Phase overview](phase-5-web.md) · [Previous task](phase-5b-admin-screens.md) · [Ontology](../ontology.md)

This task builds the negotiation, event-operations and appointment-workspace layer. It consumes
the Task 22b API exactly: the browser displays the event times and capability links it receives,
and never reconstructs an event window or authorizes an action from a role name.

> Use superpowers:executing-plans. Apply this document after Task 25 on the same phase branch.

**Goal:** A Manager can negotiate N-type events and work only their assigned type; Admin and
Coordinator can cancel future events; AppointmentStaff and Manager can operate the roster for
their server-assigned type.

**Architecture:** EventsClient and AppointmentsClient duplicate the OpenAPI wire records in
the Web assembly. Page-state classes hold edit values across a failed request. `_links` is the
only source of action availability. The EventTime component renders API-provided local strings; the proposal
dialog alone previews an end time from its not-yet-submitted local start and duration.

**Tech Stack:** Blazor WebAssembly, bUnit, Task 24 components, Playwright/axe.

**Spec:** [Master Task 26](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[negotiation](../design/03b-screens-and-flows.md#negotiation-board-eventsnegotiate--manager),
[event operations](../design/03b-screens-and-flows.md#event-operations-eventsoperations--admin-or-coordinator),
and [workspace](../design/03b-screens-and-flows.md#appointment-workspace-appointments--manager-or-appointmentstaff).

## Global constraints

The [phase constraints](phase-5-web.md#global-constraints) apply. The real page names are
`EventNegotiation.razor`, `EventOperations.razor` and `Appointments.razor`; do not resurrect the
obsolete Negotiate, Slots, ConfirmedSlots or Candidates pages named by the old outline.
All lists consume `{items,nextCursor}`. A changed or closed row is reloaded from the first page;
the UI does not guess its new state. Capacity errors branch on the RFC problem slug.

## Review focus

STOP AND CHECK: no other Manager's headcount appears; capacity input survives a conflict; event
actions disappear when their link is absent; no browser-clock comparison controls check-in,
no-show or cancellation; and every window shown after an API read is the server's EventTimeDto.

### Task 26: Manager negotiation, event operations and appointment workspace

**Files:**

- Modify: src/EventBooking.Web/Program.cs
- Modify: src/EventBooking.Web/Services/EventsClient.cs
- Modify: src/EventBooking.Web/Services/AppointmentsClient.cs
- Modify: src/EventBooking.Web/Pages/EventNegotiation.razor
- Modify: src/EventBooking.Web/Pages/EventOperations.razor
- Modify: src/EventBooking.Web/Pages/Appointments.razor
- Test: tests/EventBooking.Web.Tests/Pages/Negotiation/EventNegotiationPageTests.cs
- Test: tests/EventBooking.Web.Tests/Pages/Operations/EventOperationsPageTests.cs
- Test: tests/EventBooking.Web.Tests/Pages/Appointments/AppointmentsPageTests.cs
- Modify: tests/EventBooking.Web.Tests/Contracts/OpenApiClientContractTests.cs
- Modify: tests/EventBooking.Web.E2E/RouteManifest.cs
- Modify: tests/EventBooking.Web.E2E/RouteSetup.cs
- Modify: tests/EventBooking.Web.E2E/E2EApiStub.cs

**Interfaces:**

```csharp
namespace EventBooking.Web.Services;

// Every API event window is already localized by the server. The UI renders these strings.
public sealed record EventProposalDto(
    Guid Id, Guid LocationId, string LocationCode, string LocationName, EventTimeDto Time,
    string Status, int ListedTypeCount, int AcceptedTypeCount, int? MyAcceptedHeadcount,
    bool AcceptedByMe, bool CreatedByMe, IReadOnlyList<ProposalTypeDto> Types,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record ProposalTypeDto(string Code, string Name);
public sealed record TypeSummaryDto(Guid Id, string Code, string Name, bool IsActive, bool HasManager);
public sealed record LocationSummaryDto(
    Guid Id, string Name, string Address, string TimeZoneId, string ZoneAbbreviation, bool IsActive);
public sealed record NegotiationReferenceData(
    IReadOnlyList<LocationSummaryDto> Locations,
    IReadOnlyList<TypeSummaryDto> AppointmentTypes,
    Guid CallerAppointmentTypeId,
    IReadOnlyDictionary<string, ApiLink> CollectionLinks);
public sealed record EventCapacityDto(
    Guid AppointmentTypeId, string Code, string Name, int TotalHeadcount, int RemainingCapacity,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record EventDto(
    Guid Id, Guid ProposalId, Guid LocationId, string LocationCode, string LocationName,
    EventTimeDto Time, string Status, IReadOnlyList<EventCapacityDto> Capacities,
    int ActiveBookings,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record ProposeEventRequest(
    Guid LocationId, DateOnly Date, TimeOnly StartTime, int DurationMinutes,
    IReadOnlyList<Guid> AppointmentTypeIds, int Headcount);
public sealed record ProposeEventOutcome(Guid ProposalId, string Status, Guid? EventId);
public sealed record RecordAcceptanceOutcome(
    Guid ProposalId, string Status, Guid? EventId, bool Changed);
public sealed record AdjustEventCapacityOutcome(
    Guid EventId, int TotalHeadcount, int RemainingCapacity, bool Changed);
public sealed record CancelEventOutcome(
    int CancelledCount, int ReinvitedCount, int AwaitingAvailabilityCount);

public interface IEventsClient
{
    // Composes /api/me, /api/locations and /api/appointment-types for the proposal form.
    Task<ApiOutcome<NegotiationReferenceData>> GetReferenceDataAsync(CancellationToken ct);
    Task<ApiOutcome<PageDto<EventProposalDto>>> ListProposalsAsync(string? cursor, CancellationToken ct);
    Task<ApiOutcome<ProposeEventOutcome>> ProposeAsync(
        ProposeEventRequest request, IdempotencySubmission submission, CancellationToken ct);
    Task<ApiOutcome<RecordAcceptanceOutcome>> RecordAcceptanceAsync(
        Guid proposalId, int headcount, CancellationToken ct);
    Task<ApiOutcome<object>> WithdrawAcceptanceAsync(Guid proposalId, CancellationToken ct);
    Task<ApiOutcome<object>> WithdrawProposalAsync(Guid proposalId, CancellationToken ct);
    Task<ApiOutcome<PageDto<EventDto>>> ListEventsAsync(
        Guid? locationId, DateOnly? from, DateOnly? to, string? cursor, CancellationToken ct);
    Task<ApiOutcome<AdjustEventCapacityOutcome>> AdjustCapacityAsync(
        Guid eventId, Guid appointmentTypeId, int totalHeadcount, CancellationToken ct);
    Task<ApiOutcome<CancelEventOutcome>> CancelAsync(Guid eventId, bool confirm, CancellationToken ct);
}

public sealed record WorkspaceEventDto(
    Guid EventId, Guid LocationId, string LocationName, EventTimeDto Time, string Status,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record WorkspaceRosterRowDto(
    Guid AppointmentId, string Name, string Email, string ScopeTypeCode,
    string AppointmentStatus, DateTimeOffset? CheckedInAt, long Version,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record WorkspaceContextDto(string ScopeTypeCode, string ScopeTypeName);

public interface IAppointmentsClient
{
    Task<ApiOutcome<WorkspaceContextDto>> GetContextAsync(CancellationToken ct);
    Task<ApiOutcome<PageDto<WorkspaceEventDto>>> ListEventsAsync(Guid? locationId, CancellationToken ct);
    Task<ApiOutcome<PageDto<WorkspaceRosterRowDto>>> GetRosterAsync(Guid eventId, CancellationToken ct);
    Task<ApiOutcome<object>> SetStatusAsync(
        Guid appointmentId, string targetStatus, long expectedVersion, CancellationToken ct);
    Uri RosterCsvUri(Guid eventId);
}
```

The contract test must prove each API transport record below matches its explicitly paired Task 22b
OpenAPI schema, including JSON names and nullability. LocationSummaryDto, TypeSummaryDto,
NegotiationReferenceData and WorkspaceContextDto are client-composed projections and therefore do
not belong in that array. If the committed OpenAPI lacks the proposal's listed types and
state-specific accept/withdraw links, per-capacity adjust links, `appointmentId`, `_links` on
workspace rows, the workspace event's complete event-time representation, the caller's scope type
context in CurrentStaffResponse, or proposeEvent in the caller's collection links,
stop: repair the API contract in a separate prerequisite commit instead of inventing display data,
checking a role, or inferring temporal permissions in Web.

- [ ] **Step 1: Write the failing test**

Create all three test files. These are complete; their fakes implement the public interfaces above
and do not substitute role checks for server links.

```csharp
// tests/EventBooking.Web.Tests/Pages/Negotiation/EventNegotiationPageTests.cs (complete)
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests.Pages.Negotiation;

public sealed class EventNegotiationPageTests : BunitContext
{
    [Fact]
    public async Task DialogPreviewsEndAndFieldErrorsWithoutLosingInputs()
    {
        var api = new FakeEventsClient
        {
            ProposeResult = ApiOutcome<ProposeEventOutcome>.Failure(
                ApiProblem.Validation(new Dictionary<string, string[]>
                {
                    ["date"] = ["Choose a future date."],
                    ["headcount"] = ["Headcount must be at least one."],
                }))
        };
        Services.AddSingleton<IEventsClient>(api);

        var cut = Render<EventNegotiation>();
        await cut.Find("[data-action='propose']").ClickAsync(new());
        cut.Find("input[name='start']").Change("09:30");
        cut.Find("input[name='duration']").Change("90");
        cut.Find("input[name='headcount']").Change("0");
        Assert.Contains("ends 11:00 BST", cut.Markup);

        await cut.Find("[data-action='submit-proposal']").ClickAsync(new());
        Assert.Contains("Choose a future date.", cut.Markup);
        Assert.Contains("Headcount must be at least one.", cut.Markup);
        Assert.Equal("09:30", cut.Find("input[name='start']").GetAttribute("value"));
        Assert.Equal("90", cut.Find("input[name='duration']").GetAttribute("value"));
    }

    [Fact]
    public void BoardShowsAcceptanceCountButNeverAnotherTypesHeadcount()
    {
        var api = new FakeEventsClient();
        api.Proposals.Add(new EventProposalDto(Guid.NewGuid(), Guid.NewGuid(), "LON", "London HQ",
            TestContractFactory.EventTime("Tue 14 Oct 2026, 09:30–11:00 BST"), "Open", 3, 2, 4, true,
            false, [new("MED", "Medical check"), new("FIT", "Equipment fitting"), new("IND", "Induction")],
            Links(("withdraw-acceptance", "DELETE"))));
        Services.AddSingleton<IEventsClient>(api);

        var cut = Render<EventNegotiation>();

        Assert.Contains("2 of 3", cut.Markup);
        Assert.Contains("value=\"4\"", cut.Markup);
        Assert.DoesNotContain("FIT headcount", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IND headcount", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CapacityConflictRetainsInputAndShowsServerMinimum()
    {
        var api = new FakeEventsClient
        {
            CapacityResult = ApiOutcome<AdjustEventCapacityOutcome>.Failure(
                ApiProblem.FromSlug("capacity-below-bookings", "Capacity is too low.",
                    extensions: new() { ["minimum"] = 5, ["current"] = 8 }))
        };
        api.Events.Add(FakeEventsClient.EventWithCapacityLinks(("adjust", "PUT")));
        Services.AddSingleton<IEventsClient>(api);
        var cut = Render<EventNegotiation>();

        cut.Find("input[name='totalHeadcount']").Change("3");
        await cut.Find("[data-action='save-capacity']").ClickAsync(new());

        Assert.Equal("3", cut.Find("input[name='totalHeadcount']").GetAttribute("value"));
        Assert.Contains("minimum is 5", cut.Markup);
        Assert.Contains("server total is 8", cut.Markup);
    }

    private static IReadOnlyDictionary<string, ApiLink> Links(params (string Rel, string Method)[] values) =>
        values.ToDictionary(x => x.Rel, x => new ApiLink("/test", x.Method, x.Rel));

    private sealed class FakeEventsClient : IEventsClient
    {
        private static readonly Guid CallerTypeId = Guid.Parse("90000000-0000-0000-0000-000000000009");
        public List<EventProposalDto> Proposals { get; } = [];
        public List<EventDto> Events { get; } = [];
        public ApiOutcome<ProposeEventOutcome> ProposeResult { get; set; } =
            ApiOutcome<ProposeEventOutcome>.Success(new(Guid.NewGuid(), "Open", null));
        public ApiOutcome<AdjustEventCapacityOutcome> CapacityResult { get; set; } =
            ApiOutcome<AdjustEventCapacityOutcome>.Success(new(Guid.NewGuid(), 4, 4, true));
        public Task<ApiOutcome<NegotiationReferenceData>> GetReferenceDataAsync(CancellationToken ct) =>
            Task.FromResult(ApiOutcome<NegotiationReferenceData>.Success(new(
                [new(Guid.NewGuid(), "London HQ", "1 Example St", "Europe/London", "BST", true)],
                [new(CallerTypeId, "MED", "Medical check", true, true)], CallerTypeId,
                new Dictionary<string, ApiLink>
                {
                    ["proposeEvent"] = new("/api/event-proposals", "POST", "proposeEvent"),
                })));
        public Task<ApiOutcome<PageDto<EventProposalDto>>> ListProposalsAsync(string? cursor, CancellationToken ct) =>
            Task.FromResult(ApiOutcome<PageDto<EventProposalDto>>.Success(new(Proposals, null)));
        public Task<ApiOutcome<ProposeEventOutcome>> ProposeAsync(ProposeEventRequest request, IdempotencySubmission submission, CancellationToken ct) => Task.FromResult(ProposeResult);
        public Task<ApiOutcome<RecordAcceptanceOutcome>> RecordAcceptanceAsync(Guid proposalId, int headcount, CancellationToken ct) =>
            Task.FromResult(ApiOutcome<RecordAcceptanceOutcome>.Success(new(proposalId, "Open", null, true)));
        public Task<ApiOutcome<object>> WithdrawAcceptanceAsync(Guid proposalId, CancellationToken ct) => Task.FromResult(ApiOutcome<object>.Success(new()));
        public Task<ApiOutcome<object>> WithdrawProposalAsync(Guid proposalId, CancellationToken ct) => Task.FromResult(ApiOutcome<object>.Success(new()));
        public Task<ApiOutcome<PageDto<EventDto>>> ListEventsAsync(Guid? locationId, DateOnly? from, DateOnly? to, string? cursor, CancellationToken ct) => Task.FromResult(ApiOutcome<PageDto<EventDto>>.Success(new(Events, null)));
        public Task<ApiOutcome<AdjustEventCapacityOutcome>> AdjustCapacityAsync(Guid eventId, Guid appointmentTypeId, int totalHeadcount, CancellationToken ct) => Task.FromResult(CapacityResult);
        public Task<ApiOutcome<CancelEventOutcome>> CancelAsync(Guid eventId, bool confirm, CancellationToken ct) => Task.FromResult(ApiOutcome<CancelEventOutcome>.Success(new(1, 1, 0)));
        public static EventDto EventWithCapacityLinks(params (string Rel, string Method)[] links) => new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LON", "London HQ",
            TestContractFactory.EventTime("Tue 14 Oct 2026, 09:30–11:00 BST"), "Active",
            [new(Guid.NewGuid(), "MED", "Medical check", 4, 4,
                links.ToDictionary(x => x.Rel, x => new ApiLink("/test", x.Method, x.Rel)))],
            0, new Dictionary<string, ApiLink>());
    }
}
```

```csharp
// tests/EventBooking.Web.Tests/Pages/Operations/EventOperationsPageTests.cs (complete)
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests.Pages.Operations;

public sealed class EventOperationsPageTests : BunitContext
{
    [Fact]
    public async Task CancellationIsTwoStepAndNamesAffectedBookings()
    {
        var api = new FakeEventsClient();
        Services.AddSingleton<IEventsClient>(api);
        var cut = Render<EventOperations>();

        await cut.Find("[data-action='cancel-event']").ClickAsync(new());
        Assert.Contains("Cancel 3 active bookings?", cut.Markup);
        Assert.Empty(api.Confirms);

        await cut.Find("[data-action='confirm-cancel-event']").ClickAsync(new());
        Assert.Equal([true], api.Confirms);
    }

    [Fact]
    public void CancellationControlComesOnlyFromTheLink()
    {
        Services.AddSingleton<IEventsClient>(new FakeEventsClient(includeCancelLink: false));
        var cut = Render<EventOperations>();
        Assert.Empty(cut.FindAll("[data-action='cancel-event']"));
        Assert.Contains("Event already started", cut.Markup);
    }

    private sealed class FakeEventsClient(bool includeCancelLink = true) : IEventsClient
    {
        public List<bool> Confirms { get; } = [];
        private EventDto Event => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LON", "London HQ",
            TestContractFactory.EventTime("Tue 14 Oct 2026, 09:30–11:00 BST"), "Active",
            [new(Guid.NewGuid(), "MED", "Medical check", 6, 3, new Dictionary<string, ApiLink>())], 3,
            includeCancelLink ? new Dictionary<string, ApiLink> { ["cancel"] = new("/cancel", "POST", "cancelEvent") } : new Dictionary<string, ApiLink>());
        public Task<ApiOutcome<PageDto<EventDto>>> ListEventsAsync(Guid? locationId, DateOnly? from, DateOnly? to, string? cursor, CancellationToken ct) => Task.FromResult(ApiOutcome<PageDto<EventDto>>.Success(new([Event], null)));
        public Task<ApiOutcome<CancelEventOutcome>> CancelAsync(Guid eventId, bool confirm, CancellationToken ct) { Confirms.Add(confirm); return Task.FromResult(ApiOutcome<CancelEventOutcome>.Success(new(3, 2, 1))); }
        public Task<ApiOutcome<NegotiationReferenceData>> GetReferenceDataAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<ApiOutcome<PageDto<EventProposalDto>>> ListProposalsAsync(string? cursor, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApiOutcome<ProposeEventOutcome>> ProposeAsync(ProposeEventRequest request, IdempotencySubmission submission, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApiOutcome<RecordAcceptanceOutcome>> RecordAcceptanceAsync(Guid proposalId, int headcount, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApiOutcome<object>> WithdrawAcceptanceAsync(Guid proposalId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApiOutcome<object>> WithdrawProposalAsync(Guid proposalId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApiOutcome<AdjustEventCapacityOutcome>> AdjustCapacityAsync(Guid eventId, Guid appointmentTypeId, int totalHeadcount, CancellationToken ct) => throw new NotSupportedException();
    }
}
```

```csharp
// tests/EventBooking.Web.Tests/Pages/Appointments/AppointmentsPageTests.cs (complete)
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests.Pages.Appointments;

public sealed class AppointmentsPageTests : BunitContext
{
    [Fact]
    public void HeadingUsesServerAssignedTypeAndSelectorGroupsByLocation()
    {
        Services.AddSingleton<IAppointmentsClient>(new FakeAppointmentsClient());
        var cut = Render<Appointments>();
        Assert.Contains("Appointments — Medical check", cut.Markup);
        Assert.Equal(2, cut.FindAll("select[name='event'] optgroup").Count);
        Assert.Empty(cut.FindAll("select[name='appointmentType']"));
    }

    [Fact]
    public void StatusActionsComeOnlyFromRowLinks()
    {
        Services.AddSingleton<IAppointmentsClient>(new FakeAppointmentsClient());
        var cut = Render<Appointments>();
        Assert.Single(cut.FindAll("[data-action='check-in']"));
        Assert.Empty(cut.FindAll("[data-action='no-show']"));
        Assert.Contains("No-show is available after the event ends", cut.Markup);
    }

    [Fact]
    public async Task ConflictReloadsAndAnnouncesOnlyTheAffectedRow()
    {
        var api = new FakeAppointmentsClient { ConflictOnStatus = true };
        Services.AddSingleton<IAppointmentsClient>(api);
        var cut = Render<Appointments>();
        await cut.Find("[data-action='check-in']").ClickAsync(new());
        Assert.Contains("R. Singh changed elsewhere; the row has been refreshed.", cut.Markup);
        Assert.Equal(2, api.RosterReads);
    }

    private sealed class FakeAppointmentsClient : IAppointmentsClient
    {
        private readonly Guid _appointmentId = Guid.NewGuid();
        private readonly Guid _eventId = Guid.NewGuid();
        public bool ConflictOnStatus { get; init; }
        public int RosterReads { get; private set; }
        public Task<ApiOutcome<WorkspaceContextDto>> GetContextAsync(CancellationToken ct) => Task.FromResult(ApiOutcome<WorkspaceContextDto>.Success(new("MED", "Medical check")));
        public Task<ApiOutcome<PageDto<WorkspaceEventDto>>> ListEventsAsync(Guid? locationId, CancellationToken ct) => Task.FromResult(ApiOutcome<PageDto<WorkspaceEventDto>>.Success(new([
            new(_eventId, Guid.NewGuid(), "London HQ", TestContractFactory.EventTime("Tue 14 Oct 2026, 09:30–11:00 BST"), "Active", new Dictionary<string,ApiLink>()),
            new(Guid.NewGuid(), Guid.NewGuid(), "Dublin Centre", TestContractFactory.EventTime("Wed 15 Oct 2026, 13:00–17:00 IST"), "Active", new Dictionary<string,ApiLink>())], null)));
        public Task<ApiOutcome<PageDto<WorkspaceRosterRowDto>>> GetRosterAsync(Guid eventId, CancellationToken ct) { RosterReads++; return Task.FromResult(ApiOutcome<PageDto<WorkspaceRosterRowDto>>.Success(new([
            new(_appointmentId, "R. Singh", "r@example.org", "MED", "Expected", null, RosterReads,
                new Dictionary<string,ApiLink> { ["check-in"] = new("/status", "PUT", "setAppointmentStatus") })], null))); }
        public Task<ApiOutcome<object>> SetStatusAsync(Guid appointmentId, string targetStatus, long expectedVersion, CancellationToken ct) => Task.FromResult(ConflictOnStatus ? ApiOutcome<object>.Failure(ApiProblem.FromSlug("version-conflict", "Changed elsewhere.")) : ApiOutcome<object>.Success(new()));
        public Uri RosterCsvUri(Guid eventId) => new($"https://api.test/api/appointment-workspace/events/{eventId}/roster.csv");
    }
}
```

- [ ] **Step 2: Run the focused tests and verify the red state**

```bash
dotnet test tests/EventBooking.Web.Tests --filter "FullyQualifiedName~Negotiation|FullyQualifiedName~Operations|FullyQualifiedName~Appointments"
```

Expected: FAIL because the three pages still use the ported shapes and behaviour. Do not weaken
the assertions to make the predecessor UI pass.

- [ ] **Step 3: Implement the clients and contract boundary**

Implement the interfaces and register them in `Program.cs`. Use `ApiCall.SendAsync` for every
request, preserve one IdempotencySubmission for a proposal until success or explicit cancel,
and append cursor tokens without synthesising page numbers.

```csharp
// src/EventBooking.Web/Program.cs — replace concrete registrations for these two clients.
builder.Services.AddScoped<IEventsClient, EventsClient>();
builder.Services.AddScoped<IAppointmentsClient, AppointmentsClient>();
```

```csharp
// src/EventBooking.Web/Services/EventsClient.cs — route map; bodies are the interface records.
GET    /api/me
GET    /api/locations
GET    /api/appointment-types
GET    /api/event-proposals?cursor={cursor}
POST   /api/event-proposals                         Idempotency-Key: {submission.Key}
PUT    /api/event-proposals/{id}/acceptance
DELETE /api/event-proposals/{id}/acceptance
POST   /api/event-proposals/{id}/withdraw
GET    /api/events?locationId={id}&from={date}&to={date}&cursor={cursor}
PUT    /api/events/{id}/capacities/{appointmentTypeId}
POST   /api/events/{id}/cancel?confirm={bool}
```

```csharp
// src/EventBooking.Web/Services/AppointmentsClient.cs — route map.
GET /api/me
GET /api/appointment-workspace/events?locationId={id}
GET /api/appointment-workspace/events/{eventId}
PUT /api/appointment-workspace/appointments/{appointmentId}/status
GET /api/appointment-workspace/events/{eventId}/roster.csv
```

The proposal-preview helper accepts a selected location's zone abbreviation, DateOnly,
TimeOnly and a 15-minute-multiple duration. It returns local `start.AddMinutes(duration)` only
for the unsaved preview. Once the API returns a proposal or event, delete the preview and render
EventTimeDto unchanged. Map validation extension members by JSON field name into the matching
form control. Map `capacity-below-bookings`, `proposal-not-open`, `confirmation-required` and
`version-conflict` by ApiProblem.Type.

- [ ] **Step 4: Implement the three routes and extend browser coverage**

`EventNegotiation.razor` owns `/events/negotiate`. Group open proposals before confirmed events.
Render type chips in code order, show AcceptedTypeCount of ListedTypeCount, and expose only
MyAcceptedHeadcount. Lock the caller's type in TypePicker; retain every edit value on error.
Render New proposal only from NegotiationReferenceData.CollectionLinks["proposeEvent"]. Render
accept, withdraw acceptance and withdraw proposal only from that proposal's state-specific links;
never infer them from AcceptedByMe or CreatedByMe. On `proposal-not-open`, announce the change and
refresh the proposal page. Render each capacity editor only from that EventCapacityDto's `adjust`
link, so another type's headcount is visible but never editable.

`EventOperations.razor` owns `/events/operations`. Load active, not-started events through the
server-filtered endpoint, retain Location/date filters while walking cursors, and feed active
booking count into TwoStepButton. If `cancel` is absent, show a reason instead of a disabled
button whose state was decided from the browser clock.

`Appointments.razor` owns `/appointments`. Load server context first, put its type name in the
heading, group the event selector by location, and never offer a type selector. For each roster
row, translate only present links to Check in, Complete, Undo check-in, Correct, or No-show. A
conflict reloads that event's roster and announces the named row. The download link is an ordinary
API URL so the browser receives the CSV response.

Extend RouteManifest with all three routes and states: empty, populated, forbidden, proposal
validation, capacity conflict, cancel-confirmation, no workspace events, empty roster and row
conflict. Extend E2EApiStub with stable response links for each state. Every manifest case runs
at mobile and desktop widths.

Extend OpenApiClientContractTests with every Task 26 API transport record. The
ProposalTypeResponse, WorkspaceEventResponse and WorkspaceRosterRowResponse names are the required
names of the focused API-prerequisite schemas;
if those schemas are absent, take the stop path above before implementing Web. Then run both
section 6a mechanical sweeps and:

```csharp
// tests/EventBooking.Web.Tests/Contracts/OpenApiClientContractTests.cs — append to Contracts.
(typeof(EventProposalDto), "EventProposalResponse"),
(typeof(ProposalTypeDto), "ProposalTypeResponse"),
(typeof(EventCapacityDto), "EventCapacityResponse"),
(typeof(EventDto), "EventResponse"),
(typeof(ProposeEventOutcome), "ProposeEventOutcome"),
(typeof(RecordAcceptanceOutcome), "RecordAcceptanceOutcome"),
(typeof(AdjustEventCapacityOutcome), "AdjustEventCapacityOutcome"),
(typeof(CancelEventOutcome), "CancelEventOutcome"),
(typeof(WorkspaceEventDto), "WorkspaceEventResponse"),
(typeof(WorkspaceRosterRowDto), "WorkspaceRosterRowResponse"),
```

```csharp
// tests/EventBooking.Web.E2E/RouteManifest.cs — append before the closing collection bracket.
new("negotiation-ready", "/events/negotiate"),
new("negotiation-forbidden", "/events/negotiate", "negotiation-forbidden"),
new("negotiation-validation", "/events/negotiate", "proposal-validation", "submit-proposal"),
new("negotiation-capacity-conflict", "/events/negotiate", "capacity-conflict", "save-capacity"),
new("event-operations-ready", "/events/operations"),
new("event-operations-confirm", "/events/operations", "cancel-confirmation", "begin-event-cancel"),
new("appointments-ready", "/appointments"),
new("appointments-empty", "/appointments", "workspace-empty"),
new("appointments-conflict", "/appointments", "workspace-conflict", "check-in-conflict"),
```

```csharp
// tests/EventBooking.Web.E2E/RouteSetup.cs — add these entries to the Task 25 Actions initializer.
["submit-proposal"] = async page =>
{
    await page.Locator("[data-action='propose']").ClickAsync();
    await page.Locator("[data-action='submit-proposal']").ClickAsync();
},
["save-capacity"] = async page =>
{
    await page.Locator("input[name='totalHeadcount']").First.FillAsync("1");
    await page.Locator("[data-action='save-capacity']").First.ClickAsync();
},
["begin-event-cancel"] = page => page.Locator("[data-action='cancel-event']").First.ClickAsync(),
["check-in-conflict"] = page => page.Locator("[data-action='check-in']").First.ClickAsync(),
```

```csharp
// tests/EventBooking.Web.E2E/E2EApiStub.cs — call from Map and add at class scope.
private static void MapTask26(WebApplication app)
{
    var eventId = Guid.Parse("30000000-0000-0000-0000-000000000003");
    var appointmentId = Guid.Parse("40000000-0000-0000-0000-000000000004");
    var time = new { date = "2026-10-14", startTime = "09:30:00", durationMinutes = 90,
        startLocal = "2026-10-14T09:30:00+01:00", endLocal = "2026-10-14T11:00:00+01:00",
        startUtc = "2026-10-14T08:30:00Z", endUtc = "2026-10-14T10:00:00Z",
        timeZoneId = "Europe/London", zoneAbbreviation = "BST" };
    app.MapGet("/api/event-proposals", (HttpContext context) =>
        FixtureState(context) == "negotiation-forbidden"
        ? Results.Problem(statusCode: 403, type: "forbidden")
        : Results.Json(new { items = new[] { new {
        id = Guid.NewGuid(), locationId = Guid.NewGuid(), locationCode = "LON", locationName = "London HQ",
        time, status = "Open", listedTypeCount = 3, acceptedTypeCount = 2, myAcceptedHeadcount = (int?)null,
        acceptedByMe = false, createdByMe = true, types = new[] { new { code = "MED", name = "Medical check" } },
        _links = new Dictionary<string, object> { ["accept"] = new { href = "/acceptance", method = "PUT", operationId = "recordAcceptance" } } }, nextCursor = (string?)null }));
    app.MapPost("/api/event-proposals", (HttpContext context) =>
        FixtureState(context) == "proposal-validation"
            ? Results.ValidationProblem(new Dictionary<string, string[]> { ["headcount"] = ["Headcount must be at least one."] })
            : Results.Created("/api/event-proposals/1", new { proposalId = Guid.NewGuid(), status = "Open", eventId = (Guid?)null }));
    app.MapGet("/api/events", () => Results.Json(new { items = new[] { new { id = eventId,
        proposalId = Guid.NewGuid(), locationId = Guid.NewGuid(), locationCode = "LON", locationName = "London HQ",
        time, status = "Active", capacities = new[] { new { appointmentTypeId = Guid.NewGuid(), code = "MED", name = "Medical check", totalHeadcount = 6, remainingCapacity = 3,
            _links = new Dictionary<string, object> { ["adjust"] = new { href = "/capacity", method = "PUT", operationId = "adjustEventCapacity" } } } },
        activeBookings = 3, _links = new Dictionary<string, object> { ["cancel"] = new { href = "/cancel", method = "POST", operationId = "cancelEvent" } } }, nextCursor = (string?)null }));
    app.MapPut("/api/events/{id:guid}/capacities/{typeId:guid}", (HttpContext context) =>
        FixtureState(context) == "capacity-conflict"
            ? Results.Problem(statusCode: 409, type: "capacity-below-bookings", extensions: new Dictionary<string, object?> { ["minimum"] = 3, ["current"] = 6 })
            : Results.Ok(new { eventId, totalHeadcount = 6, remainingCapacity = 3, changed = true }));
    app.MapGet("/api/appointment-workspace/events", (HttpContext context) => Results.Json(new {
        items = FixtureState(context) == "workspace-empty" ? Array.Empty<object>() : [new { eventId,
            locationId = Guid.NewGuid(), locationName = "London HQ", time, status = "Active",
            _links = new Dictionary<string, object>() }], nextCursor = (string?)null }));
    app.MapGet("/api/appointment-workspace/events/{id:guid}", (Guid id) => Results.Json(new { items = new[] { new {
        appointmentId, name = "R. Singh", email = "r@example.org", scopeTypeCode = "MED",
        appointmentStatus = "Expected", checkedInAt = (string?)null, version = 1,
        _links = new Dictionary<string, object> { ["check-in"] = new { href = $"/api/appointment-workspace/appointments/{appointmentId}/status", method = "PUT", operationId = "setAppointmentStatus" } } } }, nextCursor = (string?)null }));
    app.MapPut("/api/appointment-workspace/appointments/{id:guid}/status", (HttpContext context) =>
        FixtureState(context) == "workspace-conflict"
            ? Results.Problem(statusCode: 409, type: "version-conflict")
            : Results.NoContent());
}
```

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test tests/EventBooking.Web.Tests --filter "FullyQualifiedName~Negotiation|FullyQualifiedName~Operations|FullyQualifiedName~Appointments|FullyQualifiedName~OpenApiClientContract"
dotnet test tests/EventBooking.Web.E2E -c Release
dotnet test EventBooking.sln
```

Expected: PASS, zero skipped tests and zero axe violations for the Task 24–26 manifest. Record the
executor's actual counts; no Phase 5 count is claimed by this authored plan.

- [ ] **Step 5: Commit and push**

```bash
git add -A
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
git commit -m "feat(web): N-type negotiation board and workspace"
git push
```
