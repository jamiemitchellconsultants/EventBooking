# 05b — Admin screens (Task 25)

[← Phase overview](phase-5-web.md) · [Previous task](phase-5a-design-system.md) · [Ontology](../ontology.md)

This task builds the reference-data, settings and staff-scope screens over Task 24's design system.
It is the administration layer of the Web phase: Admin edits, Coordinator and Manager can read the
three reference-data screens, and the API remains the authorization boundary.

> Use superpowers:executing-plans. Apply this document after Task 24 on the same phase branch.

**Goal:** Five Admin routes with every state named in design 03b, three read-only reference-data
views for other staff, navigation that matches design 03a, optimistic-conflict forms that retain
unsaved values, and E2E accessibility coverage at both widths.

**Architecture:** AdminClient owns the duplicated Task 22b request and response records for
reference data and settings. StaffAccessClient owns only scope assignment. Pages render action
controls from each response's `_links`, while Task 24's IMeClient supplies the
collection-level create links that still exist when a list is empty. Read-only use therefore needs
no role branch. On
`version-conflict`, a page stores the server's `current` body separately, preserves the edit model,
and asks the user to review and resubmit.

The focused API prerequisite must expose createLocation, createAppointmentType and
createAttendeeGroup in `/api/me`'s caller-specific `_links`. Task 22b's two-field page envelope
stays unchanged. If any link is absent from the OpenAPI snapshot and an authorized response, stop
and complete that prerequisite; never make an empty list hide create for Admin or show it by
checking the Admin role.

**Tech Stack:** Blazor WebAssembly, bUnit, Task 24 design-system components, Playwright/axe.

**Spec:** [Master Task 25](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[reference-data screens](../design/03b-screens-and-flows.md#locations-adminlocations--admin-edits-others-read-only),
[settings](../design/03b-screens-and-flows.md#settings-adminsettings--admin),
[staff access](../design/03b-screens-and-flows.md#staff-access-adminstaff-access--admin), and
[reference-data API](../design/05-api-design.md#reference-data-and-settings).

## Global constraints

The [phase constraints](phase-5-web.md#global-constraints) apply. Codes are immutable after
creation. The three list endpoints return page envelopes even though their cursor is null. The
zone list is an explicit client asset built from IANA identifiers; Web validates only selection,
while the API is authoritative. Staff roles are displayed and never edited.

## Review focus

STOP AND CHECK: an Admin with a stale version keeps every typed value; a non-Admin sees rows but no
create, update or activation control; a zone change blocked by `in-use` shows the blocking counts;
a group requirement warning names the exact member count before save; and a scope displacement
confirmation uses the returned display name, never a GUID.

### Task 25: Admin reference-data, settings and staff-access screens

**Files:**

- Modify: src/EventBooking.Web/Services/AdminClient.cs
- Modify: src/EventBooking.Web/Services/StaffAccessClient.cs
- Modify: src/EventBooking.Web/Services/StaffNavigation.cs
- Create: src/EventBooking.Web/Pages/Admin/Locations.razor
- Create: src/EventBooking.Web/Pages/Admin/AppointmentTypes.razor
- Create: src/EventBooking.Web/Pages/Admin/AttendeeGroups.razor
- Modify: src/EventBooking.Web/Pages/Settings.razor (route becomes `/admin/settings`; add inviteOptionCount)
- Modify: src/EventBooking.Web/Pages/StaffAccess.razor (route becomes `/admin/staff-access`)
- Modify: src/EventBooking.Web/Pages/Home.razor
- Test: tests/EventBooking.Web.Tests/Pages/Admin/AdminClientTests.cs
- Test: tests/EventBooking.Web.Tests/Pages/Admin/AdminPageFixture.cs
- Test: tests/EventBooking.Web.Tests/Pages/Admin/LocationsPageTests.cs
- Test: tests/EventBooking.Web.Tests/Pages/Admin/AppointmentTypesPageTests.cs
- Test: tests/EventBooking.Web.Tests/Pages/Admin/AttendeeGroupsPageTests.cs
- Test: tests/EventBooking.Web.Tests/Pages/Admin/SettingsPageTests.cs
- Test: tests/EventBooking.Web.Tests/Pages/Admin/StaffAccessPageTests.cs
- Modify: tests/EventBooking.Web.Tests/Contracts/OpenApiClientContractTests.cs (include every Task 25 DTO)
- Modify: tests/EventBooking.Web.E2E/RouteManifest.cs
- Modify: tests/EventBooking.Web.E2E/RouteSetup.cs
- Modify: tests/EventBooking.Web.E2E/E2EApiStub.cs

**Interfaces:**

```csharp
namespace EventBooking.Web.Services;

public sealed record LocationDto(
    Guid Id, string Code, string Name, string Address, string TimeZoneId, bool IsActive,
    long Version,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record AppointmentTypeDto(
    Guid Id, string Code, string Name, bool IsActive, long Version,
    string? ManagerDisplayName,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record AttendeeGroupDto(
    Guid Id, string Code, string Name, bool IsActive, long Version,
    IReadOnlyList<Guid> RequirementTypeIds, int MemberCount,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record SettingsDto(
    int InviteExpiryDays, int MaxAutoRetryCount, int InviteOptionCount, long Version,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record StaffAccessProfileDto(
    Guid StaffUserId, string? StaffId, string? DisplayName, IReadOnlyList<string> Roles,
    Guid? AppointmentTypeId, long Version,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record SetStaffScopeOutcome(
    Guid TargetStaffUserId, Guid? AppointmentTypeId, string? DisplacedManagerDisplayName);

public sealed class AdminClient
{
    public Task<ApiOutcome<PageDto<LocationDto>>> ListLocationsAsync(bool includeInactive, CancellationToken ct);
    public Task<ApiOutcome<LocationDto>> CreateLocationAsync(
        string code, string name, string address, string timeZoneId,
        IdempotencySubmission submission, CancellationToken ct);
    public Task<ApiOutcome<LocationDto>> UpdateLocationAsync(LocationDto value, CancellationToken ct);
    public Task<ApiOutcome<PageDto<AppointmentTypeDto>>> ListAppointmentTypesAsync(bool includeInactive, CancellationToken ct);
    public Task<ApiOutcome<AppointmentTypeDto>> CreateAppointmentTypeAsync(
        string code, string name, IdempotencySubmission submission, CancellationToken ct);
    public Task<ApiOutcome<AppointmentTypeDto>> UpdateAppointmentTypeAsync(AppointmentTypeDto value, CancellationToken ct);
    public Task<ApiOutcome<PageDto<AttendeeGroupDto>>> ListAttendeeGroupsAsync(bool includeInactive, CancellationToken ct);
    public Task<ApiOutcome<AttendeeGroupDto>> CreateAttendeeGroupAsync(
        string code, string name, IReadOnlyList<Guid> typeIds,
        IdempotencySubmission submission, CancellationToken ct);
    public Task<ApiOutcome<AttendeeGroupDto>> UpdateAttendeeGroupAsync(AttendeeGroupDto value, CancellationToken ct);
    public Task<ApiOutcome<SettingsDto>> GetSettingsAsync(CancellationToken ct);
    public Task<ApiOutcome<SettingsDto>> UpdateSettingsAsync(SettingsDto value, CancellationToken ct);
}

public sealed class StaffAccessClient
{
    public Task<ApiOutcome<PageDto<StaffAccessProfileDto>>> ListAsync(CancellationToken ct);
    public Task<ApiOutcome<SetStaffScopeOutcome>> SetScopeAsync(
        Guid staffUserId, Guid? appointmentTypeId, long expectedVersion, CancellationToken ct);
}
```

- [ ] **Step 1: Write the failing test**

Create a reusable HTTP spy inside AdminClientTests and pin routes, envelopes, JSON names and
idempotency:

```csharp
// tests/EventBooking.Web.Tests/Pages/Admin/AdminClientTests.cs (complete)
using System.Net;
using System.Text;
using System.Text.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests.Pages.Admin;

public sealed class AdminClientTests
{
    [Fact]
    public async Task CreateLocationUsesOneIdempotencyKeyAndTask22bBody()
    {
        var handler = new SpyHandler(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = Json("""{"id":"10000000-0000-0000-0000-000000000001","code":"LONDON_HQ","name":"London HQ","address":"1 Example St","timeZoneId":"Europe/London","isActive":true,"version":1,"_links":{}}"""),
        });
        var client = new AdminClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example") });
        var submission = IdempotencySubmission.Start();

        var result = await client.CreateLocationAsync(
            "LONDON_HQ", "London HQ", "1 Example St", "Europe/London", submission,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/api/locations", handler.Request.RequestUri!.AbsolutePath);
        Assert.Equal(submission.Key, handler.Request.Headers.GetValues("Idempotency-Key").Single());
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("Europe/London", body.RootElement.GetProperty("timeZoneId").GetString());
    }

    [Fact]
    public async Task EveryReferenceListReadsThePageEnvelope()
    {
        var handler = new SpyHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = Json("""{"items":[],"nextCursor":null}"""),
        });
        var client = new AdminClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example") });

        var result = await client.ListLocationsAsync(true, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
        Assert.Contains("includeInactive=true", handler.Request!.RequestUri!.Query);
    }

    private static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");

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

```csharp
// tests/EventBooking.Web.Tests/Pages/Admin/AdminPageFixture.cs (complete)
using System.Net;
using System.Text;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests.Pages.Admin;

internal static class AdminPageFixture
{
    internal static void Register(IServiceCollection services, params HttpResponseMessage[] responses)
    {
        services.AddSingleton(new AdminClient(new HttpClient(new QueueHandler(responses))
            { BaseAddress = new Uri("https://api.example") }));
        RegisterContext(services, "createLocation", "createAppointmentType", "createAttendeeGroup");
    }

    internal static void RegisterReadOnly(
        IServiceCollection services, params HttpResponseMessage[] responses)
    {
        services.AddSingleton(new AdminClient(new HttpClient(new QueueHandler(responses))
            { BaseAddress = new Uri("https://api.example") }));
        RegisterContext(services);
    }

    internal static void RegisterContext(IServiceCollection services, params string[] relations) =>
        services.AddSingleton<IMeClient>(new FakeMeClient(relations));

    internal static void RegisterStaff(
        IServiceCollection services, params HttpResponseMessage[] responses) =>
        services.AddSingleton(new StaffAccessClient(new HttpClient(new QueueHandler(responses))
            { BaseAddress = new Uri("https://api.example") }));

    internal static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8,
            (int)status >= 400 ? "application/problem+json" : "application/json"),
    };

    private sealed class QueueHandler(IEnumerable<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct) => Task.FromResult(_responses.Dequeue());
    }

    private sealed class FakeMeClient(IEnumerable<string> relations) : IMeClient
    {
        private readonly IReadOnlyDictionary<string, ApiLink> _links = relations.ToDictionary(
            relation => relation,
            relation => new ApiLink("/test", "POST", relation));

        public Task<ApiOutcome<MeDto>> GetAsync(CancellationToken ct) => Task.FromResult(
            ApiOutcome<MeDto>.Success(new(
                "Admin User", "A1", ["Admin"], null, null, null,
                ["ManageReferenceData"], null, _links)));
    }
}
```

Create the page tests. Each file uses a queued handler returning the JSON shown and registers one
AdminClient. The complete Locations suite establishes the shared state contract; the remaining
files exercise their unique rules rather than repeating identical loading assertions:

```csharp
// tests/EventBooking.Web.Tests/Pages/Admin/LocationsPageTests.cs (complete)
using System.Net;
using System.Text;
using Bunit;
using EventBooking.Web.Pages.Admin;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests.Pages.Admin;

public sealed class LocationsPageTests : BunitContext
{
    [Fact]
    public void EmptyStateUsesTheDesignCopy()
    {
        Register(HttpStatusCode.OK, """{"items":[],"nextCursor":null}""");
        var cut = Render<Locations>();
        cut.WaitForAssertion(() => Assert.Contains(
            "No locations yet. Add one before Managers can propose events.", cut.Markup));
        Assert.Contains("New location", cut.Markup);
    }

    [Fact]
    public void ReadOnlyResponseHasNoMutationControls()
    {
        AdminPageFixture.RegisterReadOnly(Services,
            Response(HttpStatusCode.OK, Page("{}")));
        var cut = Render<Locations>();
        cut.WaitForAssertion(() => Assert.DoesNotContain("New location", cut.Markup));
        Assert.Empty(cut.FindAll("button[data-action='edit']"));
    }

    [Fact]
    public void VersionConflictKeepsTypedValuesAndShowsCurrentState()
    {
        var handler = new QueueHandler(
            Response(HttpStatusCode.OK, Page("{\"update\":{\"href\":\"/api/locations/10000000-0000-0000-0000-000000000001\",\"method\":\"PUT\",\"operationId\":\"updateLocation\"}}")),
            Response(HttpStatusCode.Conflict, """{"type":"version-conflict","title":"Changed","status":409,"detail":"Another Admin changed this location.","current":{"name":"Server name","version":2}}"""));
        Services.AddSingleton(new AdminClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example") }));
        AdminPageFixture.RegisterContext(Services, "createLocation");
        var cut = Render<Locations>();
        cut.WaitForElement("button[data-action='edit']").Click();
        cut.Find("input[name='name']").Change("My unsaved name");
        cut.Find("button[data-action='save']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Another Admin changed this location.", cut.Markup));
        Assert.Equal("My unsaved name", cut.Find("input[name='name']").GetAttribute("value"));
        Assert.Contains("Server name", cut.Markup);
    }

    [Fact]
    public void InUseZoneChangeIsDisabledWithBlockingCounts()
    {
        var handler = new QueueHandler(
            Response(HttpStatusCode.OK, Page("{\"update\":{\"href\":\"/api/locations/10000000-0000-0000-0000-000000000001\",\"method\":\"PUT\",\"operationId\":\"updateLocation\"}}")),
            Response(HttpStatusCode.Conflict, """{"type":"in-use","title":"In use","status":409,"detail":"The zone is in use.","blocking":{"openProposals":2,"futureEvents":3}}"""));
        Services.AddSingleton(new AdminClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example") }));
        AdminPageFixture.RegisterContext(Services, "createLocation");
        var cut = Render<Locations>();
        cut.WaitForElement("button[data-action='edit']").Click();
        cut.Find("select[name='timeZoneId']").Change("Asia/Tokyo");
        cut.Find("button[data-action='save']").Click();
        cut.WaitForAssertion(() => Assert.True(cut.Find("select[name='timeZoneId']").HasAttribute("disabled")));
        cut.WaitForAssertion(() => Assert.Contains("2 open proposals and 3 future events", cut.Markup));
    }

    private void Register(HttpStatusCode status, string body) =>
        AdminPageFixture.Register(Services, Response(status, body));
    private static string Page(string links) => $"""{{"items":[{{"id":"10000000-0000-0000-0000-000000000001","code":"LONDON_HQ","name":"London HQ","address":"1 Example St","timeZoneId":"Europe/London","isActive":true,"version":1,"_links":{links}}}],"nextCursor":null}}""";
    private static HttpResponseMessage Response(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body, Encoding.UTF8, status == HttpStatusCode.OK ? "application/json" : "application/problem+json") };
    private sealed class QueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(_responses.Dequeue());
    }
}
```

```csharp
// tests/EventBooking.Web.Tests/Pages/Admin/AppointmentTypesPageTests.cs (complete)
using System.Net;
using Bunit;
using EventBooking.Web.Pages.Admin;

namespace EventBooking.Web.Tests.Pages.Admin;

public sealed class AppointmentTypesPageTests : BunitContext
{
    [Fact]
    public void ManagerlessTypeHasVisibleAssignmentGuidance()
    {
        AdminPageFixture.Register(Services, AdminPageFixture.Json(HttpStatusCode.OK,
            """{"items":[{"id":"10000000-0000-0000-0000-000000000001","code":"ESC","name":"Escort briefing","isActive":true,"version":1,"managerDisplayName":null,"_links":{}}],"nextCursor":null}"""));
        var cut = Render<AppointmentTypes>();
        cut.WaitForAssertion(() => Assert.Contains("No Manager assigned", cut.Markup));
        Assert.Empty(cut.FindAll("[data-action='edit']"));
    }
}
```

```csharp
// tests/EventBooking.Web.Tests/Pages/Admin/AttendeeGroupsPageTests.cs (complete)
using System.Net;
using Bunit;
using EventBooking.Web.Pages.Admin;

namespace EventBooking.Web.Tests.Pages.Admin;

public sealed class AttendeeGroupsPageTests : BunitContext
{
    [Fact]
    public void RequirementsChangeNamesAffectedMembersBeforeSave()
    {
        AdminPageFixture.Register(Services,
            AdminPageFixture.Json(HttpStatusCode.OK,
                """{"items":[{"id":"10000000-0000-0000-0000-000000000001","code":"FIELD","name":"Field staff","isActive":true,"version":1,"requirementTypeIds":[],"memberCount":17,"_links":{"update":{"href":"/api/attendee-groups/10000000-0000-0000-0000-000000000001","method":"PUT","operationId":"updateAttendeeGroup"}}}],"nextCursor":null}"""),
            AdminPageFixture.Json(HttpStatusCode.OK,
                """{"items":[{"id":"20000000-0000-0000-0000-000000000002","code":"FIT","name":"Fitting","isActive":true,"version":1,"managerDisplayName":"F. Manager","_links":{}}],"nextCursor":null}"""));
        var cut = Render<AttendeeGroups>();
        cut.WaitForElement("button[data-action='edit']");
        cut.Find("button[data-action='edit']").Click();
        cut.Find("input[type='checkbox']:not([disabled])").Change(true);
        cut.Find("button[data-action='save']").Click();
        Assert.Contains("This changes requirements for 17 attendees and replaces their pending invitations.", cut.Markup);
    }
}
```

```csharp
// tests/EventBooking.Web.Tests/Pages/Admin/SettingsPageTests.cs (complete)
using System.Net;
using Bunit;
using EventBooking.Web.Pages;

namespace EventBooking.Web.Tests.Pages.Admin;

public sealed class SettingsPageTests : BunitContext
{
    [Fact]
    public void AllThreeFutureInviteSettingsAreBounded()
    {
        AdminPageFixture.Register(Services, AdminPageFixture.Json(HttpStatusCode.OK,
            """{"inviteExpiryDays":7,"maxAutoRetryCount":2,"inviteOptionCount":3,"version":1,"_links":{"update":{"href":"/api/settings","method":"PUT","operationId":"updateSettings"}}}"""));
        var cut = Render<Settings>();
        cut.WaitForElement("#invite-expiry-days");
        Assert.Equal("1", cut.Find("#invite-expiry-days").GetAttribute("min"));
        Assert.Equal("60", cut.Find("#invite-expiry-days").GetAttribute("max"));
        Assert.Equal("0", cut.Find("#max-auto-retries").GetAttribute("min"));
        Assert.Equal("10", cut.Find("#max-auto-retries").GetAttribute("max"));
        Assert.Equal("1", cut.Find("#invite-option-count").GetAttribute("min"));
        Assert.Equal("5", cut.Find("#invite-option-count").GetAttribute("max"));
        Assert.Contains("future invitations only", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }
}
```

```csharp
// tests/EventBooking.Web.Tests/Pages/Admin/StaffAccessPageTests.cs (complete)
using System.Net;
using Bunit;
using EventBooking.Web.Pages;

namespace EventBooking.Web.Tests.Pages.Admin;

public sealed class StaffAccessPageTests : BunitContext
{
    [Fact]
    public void SavedConfirmationNamesDisplacedManager()
    {
        AdminPageFixture.Register(Services, AdminPageFixture.Json(HttpStatusCode.OK,
            """{"items":[{"id":"20000000-0000-0000-0000-000000000002","code":"MED","name":"Medical check","isActive":true,"version":1,"managerDisplayName":"Sam Patel","_links":{}}],"nextCursor":null}"""));
        AdminPageFixture.RegisterStaff(Services,
            AdminPageFixture.Json(HttpStatusCode.OK,
                """{"items":[{"staffUserId":"10000000-0000-0000-0000-000000000001","staffId":"M1","displayName":"Morgan Lee","roles":["Manager"],"appointmentTypeId":null,"version":1,"_links":{"set-scope":{"href":"/api/staff-access/10000000-0000-0000-0000-000000000001/scope","method":"PUT","operationId":"setStaffScope"}}}],"nextCursor":null}"""),
            AdminPageFixture.Json(HttpStatusCode.OK,
                """{"targetStaffUserId":"10000000-0000-0000-0000-000000000001","appointmentTypeId":"20000000-0000-0000-0000-000000000002","displacedManagerDisplayName":"Sam Patel"}"""));
        var cut = Render<StaffAccess>();
        cut.WaitForElement("button[data-action='save-scope']");
        cut.Find("button[data-action='save-scope']").Click();
        cut.WaitForAssertion(() => Assert.Contains("Sam Patel is no longer the Manager", cut.Markup));
    }
}
```

- [ ] **Step 2: Run the focused tests and verify the red state**

```bash
dotnet test tests/EventBooking.Web.Tests --filter "FullyQualifiedName~Pages.Admin|FullyQualifiedName~AdminClientTests"
```

Expected: FAIL because the three reference-data pages and Task 22b client shapes do not exist, the
ported settings omit inviteOptionCount, and staff access calls the retired `/api/admin` routes.

- [ ] **Step 3: Implement the Task 22b clients**

Replace AdminClient completely:

```csharp
// src/EventBooking.Web/Services/AdminClient.cs (complete)
using System.Net.Http.Json;

namespace EventBooking.Web.Services;

public sealed record LocationDto(Guid Id, string Code, string Name, string Address, string TimeZoneId,
    bool IsActive, long Version, [property: System.Text.Json.Serialization.JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);
public sealed record AppointmentTypeDto(Guid Id, string Code, string Name, bool IsActive, long Version,
    string? ManagerDisplayName, [property: System.Text.Json.Serialization.JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);
public sealed record AttendeeGroupDto(Guid Id, string Code, string Name, bool IsActive, long Version,
    IReadOnlyList<Guid> RequirementTypeIds, int MemberCount,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);
public sealed record SettingsDto(int InviteExpiryDays, int MaxAutoRetryCount, int InviteOptionCount,
    long Version, [property: System.Text.Json.Serialization.JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

public sealed class AdminClient(HttpClient http)
{
    public Task<ApiOutcome<PageDto<LocationDto>>> ListLocationsAsync(bool inactive, CancellationToken ct) => Get<PageDto<LocationDto>>($"/api/locations?includeInactive={inactive.ToString().ToLowerInvariant()}", ct);
    public Task<ApiOutcome<PageDto<AppointmentTypeDto>>> ListAppointmentTypesAsync(bool inactive, CancellationToken ct) => Get<PageDto<AppointmentTypeDto>>($"/api/appointment-types?includeInactive={inactive.ToString().ToLowerInvariant()}", ct);
    public Task<ApiOutcome<PageDto<AttendeeGroupDto>>> ListAttendeeGroupsAsync(bool inactive, CancellationToken ct) => Get<PageDto<AttendeeGroupDto>>($"/api/attendee-groups?includeInactive={inactive.ToString().ToLowerInvariant()}", ct);
    public Task<ApiOutcome<SettingsDto>> GetSettingsAsync(CancellationToken ct) => Get<SettingsDto>("/api/settings", ct);

    public Task<ApiOutcome<LocationDto>> CreateLocationAsync(string code, string name, string address, string zone, IdempotencySubmission submission, CancellationToken ct) => Send<LocationDto>(HttpMethod.Post, "/api/locations", new { code, name, address, timeZoneId = zone }, submission, ct);
    public Task<ApiOutcome<LocationDto>> UpdateLocationAsync(LocationDto value, CancellationToken ct) => Send<LocationDto>(HttpMethod.Put, $"/api/locations/{value.Id}", new { value.Name, value.Address, value.TimeZoneId, value.IsActive, expectedVersion = value.Version }, null, ct);
    public Task<ApiOutcome<AppointmentTypeDto>> CreateAppointmentTypeAsync(string code, string name, IdempotencySubmission submission, CancellationToken ct) => Send<AppointmentTypeDto>(HttpMethod.Post, "/api/appointment-types", new { code, name }, submission, ct);
    public Task<ApiOutcome<AppointmentTypeDto>> UpdateAppointmentTypeAsync(AppointmentTypeDto value, CancellationToken ct) => Send<AppointmentTypeDto>(HttpMethod.Put, $"/api/appointment-types/{value.Id}", new { value.Name, value.IsActive, expectedVersion = value.Version }, null, ct);
    public Task<ApiOutcome<AttendeeGroupDto>> CreateAttendeeGroupAsync(string code, string name, IReadOnlyList<Guid> ids, IdempotencySubmission submission, CancellationToken ct) => Send<AttendeeGroupDto>(HttpMethod.Post, "/api/attendee-groups", new { code, name, appointmentTypeIds = ids }, submission, ct);
    public Task<ApiOutcome<AttendeeGroupDto>> UpdateAttendeeGroupAsync(AttendeeGroupDto value, CancellationToken ct) => Send<AttendeeGroupDto>(HttpMethod.Put, $"/api/attendee-groups/{value.Id}", new { value.Name, appointmentTypeIds = value.RequirementTypeIds, value.IsActive, expectedVersion = value.Version }, null, ct);
    public Task<ApiOutcome<SettingsDto>> UpdateSettingsAsync(SettingsDto value, CancellationToken ct) => Send<SettingsDto>(HttpMethod.Put, "/api/settings", new { value.InviteExpiryDays, value.MaxAutoRetryCount, value.InviteOptionCount, expectedVersion = value.Version }, null, ct);

    private async Task<ApiOutcome<T>> Get<T>(string path, CancellationToken ct) { using var response = await http.GetAsync(path, ct); return await ApiCall.ReadAsync<T>(response, ct); }
    private async Task<ApiOutcome<T>> Send<T>(HttpMethod method, string path, object body, IdempotencySubmission? submission, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        if (submission is not null) request.Headers.Add("Idempotency-Key", submission.Key);
        using var response = await http.SendAsync(request, ct);
        return await ApiCall.ReadAsync<T>(response, ct);
    }
}
```

```csharp
// src/EventBooking.Web/Services/StaffAccessClient.cs (complete)
using System.Net.Http.Json;

namespace EventBooking.Web.Services;

public sealed record StaffAccessProfileDto(Guid StaffUserId, string? StaffId, string? DisplayName,
    IReadOnlyList<string> Roles, Guid? AppointmentTypeId, long Version,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);
public sealed record SetStaffScopeOutcome(Guid TargetStaffUserId, Guid? AppointmentTypeId, string? DisplacedManagerDisplayName);

public sealed class StaffAccessClient(HttpClient http)
{
    public async Task<ApiOutcome<PageDto<StaffAccessProfileDto>>> ListAsync(CancellationToken ct)
    { using var response = await http.GetAsync("/api/staff-access", ct); return await ApiCall.ReadAsync<PageDto<StaffAccessProfileDto>>(response, ct); }
    public async Task<ApiOutcome<SetStaffScopeOutcome>> SetScopeAsync(Guid staffUserId, Guid? typeId, long version, CancellationToken ct)
    { using var response = await http.PutAsJsonAsync($"/api/staff-access/{staffUserId}/scope", new { appointmentTypeId = typeId, expectedVersion = version }, ct); return await ApiCall.ReadAsync<SetStaffScopeOutcome>(response, ct); }
}
```

- [ ] **Step 4: Implement the five routes and navigation**

Create Locations.razor as the full state machine below. AppointmentTypes.razor and
AttendeeGroups.razor use the same loading/empty/read-only/edit/conflict/busy/error structure with
the field sets and exact copy from design 03b; the group page inserts TypePicker and the explicit
member-impact confirmation before sending an update.

```razor
@* src/EventBooking.Web/Pages/Admin/Locations.razor (complete) *@
@page "/admin/locations"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@inject AdminClient Api
@inject IMeClient CurrentStaff
<PageTitle>Locations</PageTitle>
<section class="page" aria-labelledby="locations-heading" aria-busy="@(_loading || _busy)">
<header class="page-header"><div><span class="eyebrow">Administration</span><h1 id="locations-heading">Locations</h1></div>
@if (_canCreate) { <button class="button button-primary" @onclick="New">New location</button> }</header>
<Banner Variant="BannerVariant.Error" Message="@_error" Retry="ReloadAsync" />
@if (_conflictCurrent is not null) { <Banner Variant="BannerVariant.Warning" Message="@($"Another Admin saved '{_conflictCurrent}'. Your values are still here; review them and save again.")" /> }
<DataTable TItem="LocationDto" Items="@_rows" RowKey="x => x.Id" Loading="@_loading" EmptyTitle="No locations yet. Add one before Managers can propose events." EmptyAction="Create the first active location." Columns="@Columns" />
@if (_edit is not null)
{
<section class="card" aria-labelledby="location-editor"><h2 id="location-editor">@(_creating ? "New location" : $"Edit {_edit.Code}")</h2>
@if (_creating) { <label class="field">Code<input name="code" @bind="_edit.Code" /></label> }
<label class="field">Name<input name="name" @bind="_edit.Name" /></label><label class="field">Address<textarea name="address" @bind="_edit.Address"></textarea></label>
<label class="field">Time zone<select name="timeZoneId" @bind="_edit.TimeZoneId" disabled="@(_zoneBlocking is not null)">@foreach (var zone in Zones) { <option>@zone</option> }</select></label>
@if (_zoneBlocking is not null) { <p class="hint">The zone cannot change while this location has @_zoneBlocking.Value.Proposals open proposals and @_zoneBlocking.Value.Events future events.</p> }
<label><input type="checkbox" @bind="_edit.IsActive" /> Active</label>
<div class="row-actions"><button class="button" @onclick="Cancel">Cancel</button><button data-action="save" class="button button-primary" disabled="@_busy" @onclick="SaveAsync">Save</button></div></section>
}
</section>
@code {
    private static readonly string[] Zones = ["Europe/London", "Europe/Dublin", "Asia/Tokyo", "Etc/UTC"];
    private readonly List<LocationDto> _rows = []; private LocationForm? _edit; private string? _error; private string? _conflictCurrent; private bool _creating; private bool _loading = true; private bool _busy; private bool _canCreate; private (int Proposals, int Events)? _zoneBlocking; private IdempotencySubmission? _submission;
    private IReadOnlyList<TableColumn<LocationDto>> Columns => [new("Code", x => b => b.AddContent(0, x.Code)), new("Name", x => b => b.AddContent(0, x.Name)), new("Address", x => b => b.AddContent(0, x.Address)), new("Zone", x => b => b.AddContent(0, x.TimeZoneId)), new("Status", x => b => { b.OpenComponent<StatusBadge>(0); b.AddAttribute(1, "Value", x.IsActive ? "Active" : "Inactive"); b.AddAttribute(2, "Display", x.IsActive ? "Active" : "Inactive"); b.CloseComponent(); }), new("Actions", x => b => { if (x.Links.Allows("update")) { b.OpenElement(0, "button"); b.AddAttribute(1, "data-action", "edit"); b.AddAttribute(2, "class", "button"); b.AddAttribute(3, "onclick", EventCallback.Factory.Create(this, () => Edit(x))); b.AddContent(4, "Edit"); b.CloseElement(); } })];
    protected override Task OnInitializedAsync() => ReloadAsync();
    private async Task ReloadAsync() { _loading = true; var context = await CurrentStaff.GetAsync(CancellationToken.None); var result = await Api.ListLocationsAsync(true, CancellationToken.None); _loading = false; _canCreate = context.IsSuccess && context.Value!.Links.Allows("createLocation"); if (result.IsSuccess) { _rows.Clear(); _rows.AddRange(result.Value!.Items); _error = null; } else _error = result.ErrorMessage; }
    private void New() { _creating = true; _submission = IdempotencySubmission.Start(); _edit = new() { TimeZoneId = Zones[0], IsActive = true }; }
    private void Edit(LocationDto value) { _creating = false; _submission = null; _edit = LocationForm.From(value); _conflictCurrent = null; }
    private void Cancel() { _edit = null; _conflictCurrent = null; _zoneBlocking = null; }
    private async Task SaveAsync() { if (_edit is null) return; _busy = true; var result = _creating ? await Api.CreateLocationAsync(_edit.Code, _edit.Name, _edit.Address, _edit.TimeZoneId, _submission!, CancellationToken.None) : await Api.UpdateLocationAsync(_edit.ToDto(), CancellationToken.None); _busy = false; if (result.IsSuccess) { Cancel(); await ReloadAsync(); return; } _error = result.ErrorMessage; if (result.ErrorCode == "version-conflict" && result.Problem?.Current is { } current) { _conflictCurrent = current.GetProperty("name").GetString(); _edit.Version = current.GetProperty("version").GetInt64(); } if (result.ErrorCode == "in-use" && result.Problem?.Blocking is { } blocking) _zoneBlocking = (blocking.GetProperty("openProposals").GetInt32(), blocking.GetProperty("futureEvents").GetInt32()); }
    private sealed class LocationForm
    {
        public Guid Id { get; init; } public string Code { get; set; } = ""; public string Name { get; set; } = "";
        public string Address { get; set; } = ""; public string TimeZoneId { get; set; } = "";
        public bool IsActive { get; set; } public long Version { get; set; }
        public static LocationForm From(LocationDto value) => new() { Id=value.Id,Code=value.Code,Name=value.Name,Address=value.Address,TimeZoneId=value.TimeZoneId,IsActive=value.IsActive,Version=value.Version };
        public LocationDto ToDto() => new(Id,Code,Name,Address,TimeZoneId,IsActive,Version,new Dictionary<string,ApiLink>());
    }
}
```

```razor
@* src/EventBooking.Web/Pages/Admin/AppointmentTypes.razor (complete) *@
@page "/admin/appointment-types"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@inject AdminClient Api
@inject IMeClient CurrentStaff
<PageTitle>Appointment types</PageTitle><section class="page"><header class="page-header"><h1>Appointment types</h1>@if (_canCreate) { <button class="button button-primary" @onclick="New">New appointment type</button> }</header><Banner Variant="BannerVariant.Error" Message="@_error" Retry="LoadAsync" />
<DataTable TItem="AppointmentTypeDto" Items="@_rows" RowKey="x => x.Id" Loading="@_loading" EmptyTitle="No appointment types yet." EmptyAction="Add a type before creating groups or proposals." Columns="@Columns" />
@if (_edit is not null) { <section class="card"><h2>@(_creating ? "New appointment type" : $"Edit {_edit.Code}")</h2>@if (_creating) { <label class="field">Code<input @bind="_edit.Code" /></label> }<label class="field">Name<input @bind="_edit.Name" /></label><label><input type="checkbox" @bind="_edit.IsActive" /> Active</label><div class="row-actions"><button class="button" @onclick="() => _edit = null">Cancel</button><button class="button button-primary" @onclick="SaveAsync">Save</button></div></section> }</section>
@code { private readonly List<AppointmentTypeDto> _rows=[]; private AppointmentTypeForm? _edit; private bool _loading=true,_creating,_canCreate; private string? _error; private IdempotencySubmission? _submission; private IReadOnlyList<TableColumn<AppointmentTypeDto>> Columns => [new("Code",x=>b=>b.AddContent(0,x.Code)),new("Name",x=>b=>{b.OpenComponent<TypeChip>(0);b.AddAttribute(1,"Code",x.Code);b.AddAttribute(2,"Name",x.Name);b.CloseComponent();}),new("Manager",x=>b=>b.AddContent(0,x.ManagerDisplayName??"No Manager assigned")),new("Status",x=>b=>b.AddContent(0,x.IsActive?"Active":"Inactive")),new("Actions",x=>b=>{if(x.Links.Allows("update")){b.OpenElement(0,"button");b.AddAttribute(1,"data-action","edit");b.AddAttribute(2,"onclick",EventCallback.Factory.Create(this,()=>{_creating=false;_edit=AppointmentTypeForm.From(x);}));b.AddContent(3,"Edit");b.CloseElement();}})]; protected override Task OnInitializedAsync()=>LoadAsync(); private async Task LoadAsync(){_loading=true;var context=await CurrentStaff.GetAsync(CancellationToken.None);var r=await Api.ListAppointmentTypesAsync(true,CancellationToken.None);_loading=false;_canCreate=context.IsSuccess&&context.Value!.Links.Allows("createAppointmentType");if(r.IsSuccess){_rows.Clear();_rows.AddRange(r.Value!.Items);}else _error=r.ErrorMessage;} private void New(){_creating=true;_submission=IdempotencySubmission.Start();_edit=new(){IsActive=true};} private async Task SaveAsync(){if(_edit is null)return;var r=_creating?await Api.CreateAppointmentTypeAsync(_edit.Code,_edit.Name,_submission!,CancellationToken.None):await Api.UpdateAppointmentTypeAsync(_edit.ToDto(),CancellationToken.None);if(r.IsSuccess){_edit=null;_creating=false;await LoadAsync();}else _error=r.ErrorMessage;} private sealed class AppointmentTypeForm { public Guid Id{get;init;} public string Code{get;set;}=""; public string Name{get;set;}=""; public bool IsActive{get;set;} public long Version{get;set;} public string? ManagerDisplayName{get;init;} public static AppointmentTypeForm From(AppointmentTypeDto x)=>new(){Id=x.Id,Code=x.Code,Name=x.Name,IsActive=x.IsActive,Version=x.Version,ManagerDisplayName=x.ManagerDisplayName}; public AppointmentTypeDto ToDto()=>new(Id,Code,Name,IsActive,Version,ManagerDisplayName,new Dictionary<string,ApiLink>()); } }
```

```razor
@* src/EventBooking.Web/Pages/Admin/AttendeeGroups.razor (complete) *@
@page "/admin/attendee-groups"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@inject AdminClient Api
@inject IMeClient CurrentStaff
<PageTitle>Attendee groups</PageTitle><section class="page"><header class="page-header"><h1>Attendee groups</h1>@if(_canCreate){<button class="button button-primary" @onclick="New">New attendee group</button>}</header><Banner Variant="BannerVariant.Error" Message="@_error" />
<DataTable TItem="AttendeeGroupDto" Items="@Rows" RowKey="x=>x.Id" EmptyTitle="No attendee groups yet." EmptyAction="Add a group and at least one required type." Columns="@Columns" />
@if (_edit is not null){<section class="card"><h2>@(_creating ? "New attendee group" : $"Edit {_edit.Code}")</h2>@if(_creating){<label class="field">Code<input @bind="_edit.Code" /></label>}<label class="field">Name<input @bind="_edit.Name" /></label><TypePicker Options="@TypeOptions" SelectedIds="@_edit.RequirementTypeIds" SelectedIdsChanged="ids=>_edit.RequirementTypeIds=ids" />@if(_confirm){<Banner Variant="BannerVariant.Warning" Message="@($"This changes requirements for {_edit.MemberCount} attendees and replaces their pending invitations.")" /><button class="button button-primary" @onclick="SaveConfirmedAsync">Confirm and save</button>}else{<button data-action="save" class="button button-primary" @onclick="RequestSaveAsync">Save</button>}</section>}</section>
@code { private List<AttendeeGroupDto> Rows {get;}=[]; private AttendeeGroupForm? _edit; private bool _confirm,_creating,_canCreate; private string? _error; private IdempotencySubmission? _submission; private IReadOnlyList<TypeOption> TypeOptions=[]; private IReadOnlyList<TableColumn<AttendeeGroupDto>> Columns=>[new("Code",x=>b=>b.AddContent(0,x.Code)),new("Name",x=>b=>b.AddContent(0,x.Name)),new("Members",x=>b=>b.AddContent(0,x.MemberCount)),new("Actions",x=>b=>{if(x.Links.Allows("update")){b.OpenElement(0,"button");b.AddAttribute(1,"data-action","edit");b.AddAttribute(2,"onclick",EventCallback.Factory.Create(this,()=>{_creating=false;_edit=AttendeeGroupForm.From(x);}));b.AddContent(3,"Edit");b.CloseElement();}})]; protected override async Task OnInitializedAsync(){var context=await CurrentStaff.GetAsync(CancellationToken.None);var groups=await Api.ListAttendeeGroupsAsync(true,CancellationToken.None);var types=await Api.ListAppointmentTypesAsync(true,CancellationToken.None);_canCreate=context.IsSuccess&&context.Value!.Links.Allows("createAttendeeGroup");if(groups.IsSuccess)Rows.AddRange(groups.Value!.Items);else _error=groups.ErrorMessage;if(types.IsSuccess)TypeOptions=types.Value!.Items.Select(x=>new TypeOption(x.Id,x.Code,x.Name,x.IsActive,x.ManagerDisplayName is not null)).ToArray();} private void New(){_creating=true;_submission=IdempotencySubmission.Start();_edit=new(){IsActive=true};} private async Task RequestSaveAsync(){if(_edit is null)return;if(!_creating&&_edit.MemberCount>0){_confirm=true;return;}await SaveConfirmedAsync();} private async Task SaveConfirmedAsync(){if(_edit is null)return;var r=_creating?await Api.CreateAttendeeGroupAsync(_edit.Code,_edit.Name,_edit.RequirementTypeIds,_submission!,CancellationToken.None):await Api.UpdateAttendeeGroupAsync(_edit.ToDto(),CancellationToken.None);if(r.IsSuccess){_edit=null;_confirm=false;_creating=false;}else _error=r.ErrorMessage;} private sealed class AttendeeGroupForm { public Guid Id{get;init;} public string Code{get;set;}=""; public string Name{get;set;}=""; public bool IsActive{get;set;} public long Version{get;set;} public IReadOnlyList<Guid> RequirementTypeIds{get;set;}=[]; public int MemberCount{get;init;} public static AttendeeGroupForm From(AttendeeGroupDto x)=>new(){Id=x.Id,Code=x.Code,Name=x.Name,IsActive=x.IsActive,Version=x.Version,RequirementTypeIds=x.RequirementTypeIds,MemberCount=x.MemberCount}; public AttendeeGroupDto ToDto()=>new(Id,Code,Name,IsActive,Version,RequirementTypeIds,MemberCount,new Dictionary<string,ApiLink>()); } }
```

Rewrite Settings.razor against GetSettingsAsync/UpdateSettingsAsync with three bounded inputs, the
future-invitations hint, and the same conflict-preserving pattern. Rewrite StaffAccess.razor
against the page envelope and `PUT /api/staff-access/{id}/scope`; load active type options through
AdminClient, one null option clears scope, roles remain chips, and success uses
DisplacedManagerDisplayName. Update Home and
StaffNavigation to these exact Admin routes and reference-data read routes:

```csharp
// src/EventBooking.Web/Services/StaffNavigation.cs — LinksFor returns these Admin links.
new("/admin/locations", "Locations", "Manage event sites and time zones"),
new("/admin/appointment-types", "Appointment types", "Manage the types events may offer"),
new("/admin/attendee-groups", "Attendee groups", "Manage requirement mappings"),
new("/admin/settings", "System settings", "Configure future invitations"),
new("/admin/staff-access", "Staff access", "Set appointment-type scope"),
new("/events/operations", "Event operations", "Review and cancel future events"),
new("/audit", "Audit search", "Search event and administration history"),
```

For Coordinator and Manager, append the three reference-data routes as read-only links once each;
AppointmentStaff gets none. Remove every predecessor organisation string from Home.

Extend the OpenAPI contract array with the exact client-type/schema-name pairs below. This is
deliberately not a type-name convention: the Web-owned names differ from several API schema names,
and a guessed lookup would either miss drift or fail for the wrong reason. Extend E2EApiStub with
the five GET responses and stable update links, and RouteManifest with the five Task 25 routes.

Use these concrete merge fragments rather than leaving those three modifications implicit:

```csharp
// tests/EventBooking.Web.Tests/Contracts/OpenApiClientContractTests.cs — append to Contracts.
(typeof(LocationDto), "LocationResponse"),
(typeof(AppointmentTypeDto), "AppointmentTypeResponse"),
(typeof(AttendeeGroupDto), "AttendeeGroupResponse"),
(typeof(SettingsDto), "SettingsResponse"),
(typeof(StaffAccessProfileDto), "StaffAccessResponse"),
(typeof(SetStaffScopeOutcome), "SetStaffScopeOutcome"),
```

```csharp
// tests/EventBooking.Web.E2E/RouteManifest.cs — append before the closing collection bracket.
new("admin-locations-ready", "/admin/locations"),
new("admin-locations-empty", "/admin/locations", "locations-empty"),
new("admin-locations-edit", "/admin/locations", "ready", "locations-edit"),
new("admin-locations-conflict", "/admin/locations", "locations-conflict", "locations-save"),
new("admin-appointment-types-ready", "/admin/appointment-types"),
new("admin-attendee-groups-ready", "/admin/attendee-groups"),
new("admin-attendee-groups-confirm", "/admin/attendee-groups", "ready", "group-confirm"),
new("admin-settings-ready", "/admin/settings"),
new("admin-staff-access-ready", "/admin/staff-access"),
new("reference-data-read-only", "/admin/locations", "reference-read-only"),
```

```csharp
// tests/EventBooking.Web.E2E/RouteSetup.cs — replace Actions with the accumulated map.
private static readonly IReadOnlyDictionary<string, Func<IPage, Task>> Actions =
    new Dictionary<string, Func<IPage, Task>>(StringComparer.Ordinal)
    {
        ["locations-edit"] = page => page.Locator("[data-action='edit']").First.ClickAsync(),
        ["locations-save"] = async page =>
        {
            await page.Locator("[data-action='edit']").First.ClickAsync();
            await page.Locator("input[name='name']").FillAsync("Unsaved London name");
            await page.Locator("[data-action='save']").ClickAsync();
        },
        ["group-confirm"] = async page =>
        {
            await page.Locator("[data-action='edit']").First.ClickAsync();
            await page.Locator("[data-action='save']").ClickAsync();
        },
    };
```

```csharp
// tests/EventBooking.Web.E2E/E2EApiStub.cs — call from Map and add at class scope.
private static void MapTask25(WebApplication app)
{
    app.MapGet("/api/locations", (HttpContext context) =>
    {
        var state = FixtureState(context);
        object[] rows = state == "locations-empty" ? [] : [new
        {
            id = Guid.Parse("10000000-0000-0000-0000-000000000001"), code = "LON",
            name = "London HQ", address = "1 Example St", timeZoneId = "Europe/London",
            isActive = true, version = 1,
            _links = state == "reference-read-only" ? new Dictionary<string, object>() :
                new Dictionary<string, object> { ["update"] = new { href = "/api/locations/10000000-0000-0000-0000-000000000001", method = "PUT", operationId = "updateLocation" } },
        }];
        return Results.Json(new { items = rows, nextCursor = (string?)null });
    });
    app.MapGet("/api/appointment-types", (HttpContext context) => Results.Json(new { items = new[] { new { id = Guid.NewGuid(), code = "MED", name = "Medical check", isActive = true, version = 1, managerDisplayName = "M. Manager",
        _links = FixtureState(context) == "reference-read-only" ? new Dictionary<string, object>() :
            new Dictionary<string, object> { ["update"] = new { href = "/api/appointment-types/1", method = "PUT", operationId = "updateAppointmentType" } } } }, nextCursor = (string?)null }));
    app.MapGet("/api/attendee-groups", (HttpContext context) => Results.Json(new { items = new[] { new { id = Guid.NewGuid(), code = "FIELD", name = "Field staff", isActive = true, version = 1, requirementTypeIds = Array.Empty<Guid>(), memberCount = 17,
        _links = FixtureState(context) == "reference-read-only" ? new Dictionary<string, object>() :
            new Dictionary<string, object> { ["update"] = new { href = "/api/attendee-groups/1", method = "PUT", operationId = "updateAttendeeGroup" } } } }, nextCursor = (string?)null }));
    app.MapGet("/api/settings", () => Results.Json(new { inviteExpiryDays = 7, maxAutoRetryCount = 2, inviteOptionCount = 3, version = 1, _links = new { update = new { href = "/api/settings", method = "PUT", operationId = "updateSettings" } } }));
    app.MapGet("/api/staff-access", () => Results.Json(new { items = new[] { new { staffUserId = Guid.NewGuid(), staffId = "A1", displayName = "A. Admin", roles = new[] { "Admin" }, appointmentTypeId = (Guid?)null, version = 1, _links = new Dictionary<string, object>() } }, nextCursor = (string?)null }));
    app.MapPut("/api/locations/{id:guid}", (HttpContext context, Guid id) =>
        FixtureState(context) == "locations-conflict"
            ? Results.Problem(statusCode: 409, type: "version-conflict",
                extensions: new Dictionary<string, object?> { ["current"] = new { name = "London HQ", version = 2 } })
            : Results.NoContent());
}
```

Run both section 6a mechanical sweeps. Then run:

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test tests/EventBooking.Web.Tests --filter "FullyQualifiedName~Pages.Admin|FullyQualifiedName~StaffNavigation"
dotnet test tests/EventBooking.Web.E2E -c Release
dotnet test EventBooking.sln
```

Expected: PASS, zero skipped tests and zero axe violations for the Task 24–25 manifest at both
widths. No figure is observed here; record the executor's real Web and E2E counts.

- [ ] **Step 5: Commit and push**

```bash
git add -A
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
git commit -m "feat(web): Admin reference-data screens"
git push
```
