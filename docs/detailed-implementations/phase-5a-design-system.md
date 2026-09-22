# 05a — Neutral design system and rendered-page gate (Task 24)

[← Phase overview](phase-5-web.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task opens Phase 5 at the presentation boundary. It removes the predecessor's branding,
introduces the reusable component contracts from design 03a, updates the standalone Web client's
shared HTTP contract, and creates the Playwright/axe project and workflow job that every later Web
task extends.

> Use superpowers:executing-plans. This task is hand-authored: compile and test-drive the complete
> tests and implementation below. The counts are expectations, not observations.

**Goal:** A neutral token-only theme; nine accessible design-system components; RFC 9457, cursor,
hypermedia and idempotency primitives for the standalone client; and an E2E harness that publishes
and runs the real WASM bundle, with zero axe violations on its initial route set at mobile and
desktop widths.

**Architecture:** CSS variables are the only source of colour, spacing, radius and typography.
Components accept client-owned DTOs and callbacks and make no HTTP calls. The authenticated and
anonymous clients remain separate. A compile-time E2E authentication provider exists only in a
publish carrying `EVENTBOOKING_E2E`; the test host serves that bundle and deterministic HTTP
fixtures without referencing a server project.

**Tech Stack:** .NET 10, Blazor WebAssembly, bUnit, xUnit, Microsoft.Playwright 1.62.0, axe-core
4.13.0.

**Spec:** [Master Task 24](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[component contracts](../design/03a-design-system-and-ia.md#component-contracts),
[accessibility baseline](../design/03a-design-system-and-ia.md#accessibility-baseline),
[API conventions](../design/05-api-design.md#conventions), and
[NFR accessibility](../design/08-nonfunctional-requirements.md#accessibility).

## Global constraints

The [phase constraints](phase-5-web.md#global-constraints) apply. In particular, the E2E build
symbol must be absent from every normal publish, TypeChip colours come only from the AA-safe
palette in `theme.css`, TwoStepButton never calls native `confirm()`, and EventTime never derives
an end or performs a zone conversion.

## Review focus

STOP AND CHECK five things. A six-type row gets one Capacity column rather than six columns. A
single-select LocationPicker emits exactly one identifier and a multi-select one emits a stable
set. TwoStepButton resets both after ten seconds and after navigation. `type` — not `title` — is
the error slug preserved by ApiCall. The colour scan checks Razor and every CSS file except
`theme.css`, including scoped CSS inherited from the port.

### Task 24: Neutral theme, design-system components, and early axe gate

**Files:**

- Modify: Directory.Packages.props (pin Microsoft.Playwright 1.62.0)
- Modify: EventBooking.sln (add the E2E project)
- Delete: src/EventBooking.Web/.npmrc
- Delete: src/EventBooking.Web/wwwroot/css/fonts.css
- Delete: src/EventBooking.Web/wwwroot/fonts/mylius-Modern-bd.woff2
- Delete: src/EventBooking.Web/wwwroot/fonts/mylius-Modern-extlig.woff2
- Delete: src/EventBooking.Web/wwwroot/fonts/mylius-Modern-lt.woff2
- Delete: src/EventBooking.Web/wwwroot/fonts/mylius-Modern-reg.woff2
- Delete: src/EventBooking.Web/wwwroot/speedmarque.png
- Create: src/EventBooking.Web/wwwroot/theme.css
- Modify: src/EventBooking.Web/wwwroot/css/app.css
- Modify: src/EventBooking.Web/wwwroot/index.html
- Modify: src/EventBooking.Web/wwwroot/appsettings.json
- Modify: src/EventBooking.Web/EventBooking.Web.csproj
- Modify: src/EventBooking.Web/_Imports.razor
- Modify: src/EventBooking.Web/App.razor
- Modify: src/EventBooking.Web/Program.cs
- Modify: src/EventBooking.Web/Layout/MainLayout.razor
- Modify: src/EventBooking.Web/Layout/AttendeeLayout.razor
- Modify: src/EventBooking.Web/Shared/BrandMark.razor
- Create: src/EventBooking.Web/Services/ProductOptions.cs
- Modify: src/EventBooking.Web/Services/ApiOutcome.cs
- Modify: src/EventBooking.Web/Services/ApiCall.cs
- Create: src/EventBooking.Web/Services/ClientContracts.cs
- Modify: src/EventBooking.Web/Services/MeClient.cs
- Create: src/EventBooking.Web/Services/IdempotencySubmission.cs
- Create: src/EventBooking.Web/Services/E2EAuthenticationStateProvider.cs
- Create: src/EventBooking.Web/Components/ComponentModels.cs
- Create: src/EventBooking.Web/Components/StatusBadge.razor
- Create: src/EventBooking.Web/Components/TypeChip.razor
- Create: src/EventBooking.Web/Components/DataTable.razor
- Create: src/EventBooking.Web/Components/TypePicker.razor
- Create: src/EventBooking.Web/Components/LocationPicker.razor
- Create: src/EventBooking.Web/Components/TwoStepButton.razor
- Create: src/EventBooking.Web/Components/Banner.razor
- Create: src/EventBooking.Web/Components/CsvImportResult.razor
- Create: src/EventBooking.Web/Components/EventTime.razor
- Create: src/EventBooking.Web/Components/components.css
- Test: tests/EventBooking.Web.Tests/Components/DesignSystemComponentTests.cs
- Test: tests/EventBooking.Web.Tests/Contracts/ApiCallContractTests.cs
- Test: tests/EventBooking.Web.Tests/Contracts/OpenApiClientContractTests.cs
- Test: tests/EventBooking.Web.Tests/MeClientTests.cs
- Test: tests/EventBooking.Web.Tests/Contracts/TestContractFactory.cs
- Test: tests/EventBooking.Web.Tests/Theme/ThemeIsolationTests.cs
- Create: tests/EventBooking.Web.Tests/Contracts/openapi-v1.json (generated and reviewed from Task 22b's `/openapi/v1.json`)
- Create: tests/EventBooking.Web.E2E/EventBooking.Web.E2E.csproj
- Create: tests/EventBooking.Web.E2E/package.json
- Create: tests/EventBooking.Web.E2E/package-lock.json (generated by `npm install --package-lock-only` and committed)
- Create: tests/EventBooking.Web.E2E/WebHostFixture.cs
- Create: tests/EventBooking.Web.E2E/E2EApiStub.cs
- Create: tests/EventBooking.Web.E2E/AxeRunner.cs
- Create: tests/EventBooking.Web.E2E/RouteManifest.cs
- Create: tests/EventBooking.Web.E2E/RouteSetup.cs
- Create: tests/EventBooking.Web.E2E/AccessibilityTests.cs
- Modify: .github/workflows/dotnet-build.yml (separate axe job for Web changes)

**Interfaces:**

```csharp
namespace EventBooking.Web.Services;

// Client-owned wire primitives. These types deliberately duplicate the server's JSON contract;
// Web has no project reference to any server assembly.
public sealed record ApiLink(string Href, string Method, string OperationId);
public sealed record PageDto<T>(IReadOnlyList<T> Items, string? NextCursor);
public sealed record FieldProblem(string? Field, int? Line, string Code, string? Message);
public sealed record ApiProblem(
    string Type, string? Title, int Status, string? Detail,
    IReadOnlyList<FieldProblem> Errors,
    JsonElement? Current, JsonElement? Consequence, int? Minimum, JsonElement? Blocking)
{
    public static ApiProblem Validation(IReadOnlyDictionary<string, string[]> errors);
    public static ApiProblem FromSlug(
        string slug, string detail,
        IReadOnlyDictionary<string, object?>? extensions = null);
}

public sealed record EventTimeDto(
    DateOnly Date, TimeOnly StartTime, int DurationMinutes,
    DateTimeOffset StartLocal, DateTimeOffset EndLocal,
    DateTimeOffset StartUtc, DateTimeOffset EndUtc,
    string TimeZoneId, string ZoneAbbreviation);

// /api/me is also the caller-specific collection-affordance resource. Row actions remain on
// their rows; create/import/propose links live here so an empty page never needs a role check.
[method: System.Text.Json.Serialization.JsonConstructor]
public sealed record MeDto(
    string? DisplayName, string? StaffId, IReadOnlyList<string> Roles,
    Guid? ScopeAppointmentTypeId, string? ScopeAppointmentTypeCode,
    string? ScopeAppointmentTypeName, IReadOnlyList<string> Capabilities, string? Problem,
    IReadOnlyDictionary<string, ApiLink> Links)
{
    public MeDto(
        IReadOnlyList<string> roles, Guid? appointmentTypeId, string? appointmentTypeName)
        : this(null, null, roles, appointmentTypeId, null, appointmentTypeName, [], null,
            new Dictionary<string, ApiLink>()) { }

    // Transitional aliases keep the Task 11 pages compiling until Tasks 25–27 replace them.
    [JsonIgnore] public Guid? AppointmentTypeId => ScopeAppointmentTypeId;
    [JsonIgnore] public string? AppointmentTypeName => ScopeAppointmentTypeName;
}

public interface IMeClient
{
    Task<ApiOutcome<MeDto>> GetAsync(CancellationToken ct);
}

public sealed record ProductOptions(
    string ProductName, string? LogoPath, string CoordinatorContact);

public sealed class IdempotencySubmission
{
    public string Key { get; }
    public static IdempotencySubmission Start();
}

public sealed record ApiOutcome<T>(
    bool IsSuccess, T? Value, int StatusCode, ApiProblem? Problem)
{
    public string? ErrorCode => Problem?.Type;
    public string? ErrorMessage => Problem?.Detail ?? Problem?.Title;
}
```

```csharp
namespace EventBooking.Web.Components;

public enum BannerVariant { Info, Success, Warning, Error }
public sealed record TypeOption(
    Guid Id, string Code, string Name, bool IsActive, bool HasManager);
public sealed record LocationOption(
    Guid Id, string Name, string Address, string ZoneAbbreviation, bool IsActive);
public sealed record TypeCapacity(
    Guid AppointmentTypeId, string Code, string Name, int Remaining, int Total);
public sealed record TableColumn<TItem>(string Heading, RenderFragment<TItem> Cell);

// DataTable computes the distinct type columns in ordinal code order. At six or more it emits
// one Capacity column containing `code: remaining/total` and an expandable full breakdown.
// RowKey is mandatory so per-row busy state and expansion survive an append-page render.
// TypePicker locks CallerTypeId and disables !IsActive or !HasManager options with visible text.
// LocationPicker emits one id in Single mode and a de-duplicated stable list in Multi mode.
```

- [ ] **Step 1: Write the failing test**

Create the component suite in full:

```csharp
// tests/EventBooking.Web.Tests/Components/DesignSystemComponentTests.cs (complete)
using Bunit;
using EventBooking.Web.Components;
using EventBooking.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests.Components;

public sealed class DesignSystemComponentTests : BunitContext
{
    [Fact]
    public void StatusBadgeAlwaysCarriesDisplayText()
    {
        var cut = Render<StatusBadge>(p => p
            .Add(x => x.Value, "NoShow")
            .Add(x => x.Display, "No-show"));

        Assert.Equal("No-show", cut.Find(".status-badge").TextContent.Trim());
        Assert.Contains("status-noshow", cut.Find(".status-badge").ClassList);
    }

    [Fact]
    public void TypeChipUsesCodeForAStablePaletteClassAndNameForText()
    {
        var first = Render<TypeChip>(p => p
            .Add(x => x.Code, "MED")
            .Add(x => x.Name, "Medical check"));
        var second = Render<TypeChip>(p => p
            .Add(x => x.Code, "MED")
            .Add(x => x.Name, "Renamed medical check"));

        Assert.Equal("Medical check", first.Find(".type-chip").TextContent.Trim());
        Assert.Equal("MED", first.Find(".type-chip").GetAttribute("title"));
        Assert.Equal(
            first.Find(".type-chip").ClassList.Single(x => x.StartsWith("type-palette-")),
            second.Find(".type-chip").ClassList.Single(x => x.StartsWith("type-palette-")));
    }

    [Theory]
    [InlineData(5, 5, false)]
    [InlineData(6, 0, true)]
    public void DataTableUsesDynamicColumnsUntilTheSixthType(
        int typeCount, int expectedTypeHeaders, bool expectedCapacityHeader)
    {
        var capacities = Enumerable.Range(0, typeCount)
            .Select(i => new TypeCapacity(Guid.NewGuid(), $"T{i:00}", $"Type {i}", i + 1, i + 2))
            .ToArray();
        var row = new Row(Guid.NewGuid(), capacities);
        RenderFragment<Row> name = item => builder => builder.AddContent(0, item.Id);

        var cut = Render<DataTable<Row>>(p => p
            .Add(x => x.Items, [row])
            .Add(x => x.RowKey, x => x.Id)
            .Add(x => x.Columns, [new TableColumn<Row>("Row", name)])
            .Add(x => x.Capacities, x => x.Capacities)
            .Add(x => x.EmptyTitle, "No rows")
            .Add(x => x.EmptyAction, "Create one"));

        Assert.Equal(expectedTypeHeaders, cut.FindAll("th[data-type-code]").Count);
        Assert.Equal(expectedCapacityHeader, cut.Markup.Contains(">Capacity<"));
        Assert.Equal(typeCount, cut.FindAll(".capacity-breakdown li").Count);
    }

    [Fact]
    public void TypePickerLocksCallerAndExplainsManagerlessType()
    {
        var caller = Guid.NewGuid();
        var managerless = Guid.NewGuid();
        var cut = Render<TypePicker>(p => p
            .Add(x => x.Options,
            [
                new(caller, "MED", "Medical", true, true),
                new(managerless, "ESC", "Escort briefing", true, false),
            ])
            .Add(x => x.CallerTypeId, caller)
            .Add(x => x.SelectedIds, [caller]));

        Assert.True(cut.Find($"input[value='{caller}']").HasAttribute("disabled"));
        Assert.True(cut.Find($"input[value='{managerless}']").HasAttribute("disabled"));
        Assert.Contains("No Manager assigned", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void LocationPickerShowsNameAddressAndZoneAndEmitsStableMultiSelection()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        IReadOnlyList<Guid>? changed = null;
        var cut = Render<LocationPicker>(p => p
            .Add(x => x.Mode, LocationPickerMode.Multi)
            .Add(x => x.Options,
            [
                new(first, "London HQ", "1 Example St", "BST", true),
                new(second, "Dublin", "2 Sample Rd", "IST", true),
            ])
            .Add(x => x.SelectedIds, [first])
            .Add(x => x.SelectedIdsChanged,
                EventCallback.Factory.Create<IReadOnlyList<Guid>>(this, value => changed = value)));

        Assert.Contains("London HQ", cut.Markup);
        Assert.Contains("1 Example St", cut.Markup);
        Assert.Contains("BST", cut.Markup);
        cut.Find($"input[value='{second}']").Change(true);
        Assert.Equal([first, second], changed);
    }

    [Fact]
    public void BannerAnnouncesItsAppearanceAndOffersRetry()
    {
        var retried = false;
        var cut = Render<Banner>(p => p
            .Add(x => x.Variant, BannerVariant.Error)
            .Add(x => x.Message, "The list could not load.")
            .Add(x => x.Retry, EventCallback.Factory.Create(this, () => retried = true)));

        Assert.Equal("assertive", cut.Find("[aria-live]").GetAttribute("aria-live"));
        cut.Find("button").Click();
        Assert.True(retried);
    }

    [Fact]
    public void CsvRejectionListsLinesAndSaysNothingWasImported()
    {
        var cut = Render<CsvImportResult>(p => p
            .Add(x => x.Accepted, false)
            .Add(x => x.Errors,
            [
                new FieldProblem("email", 4, "duplicate", "Email is duplicated."),
                new FieldProblem("attendee_group", 7, "unknown", "Group is unknown."),
            ]));

        Assert.Contains("Nothing was imported.", cut.Markup);
        Assert.Contains("Line 4", cut.Markup);
        Assert.Contains("Line 7", cut.Markup);
    }

    [Fact]
    public void EventTimeRendersApiValuesWithoutDerivingAnEnd()
    {
        var time = new EventTimeDto(
            new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 90,
            new DateTimeOffset(2026, 10, 14, 9, 30, 0, TimeSpan.FromHours(1)),
            new DateTimeOffset(2026, 10, 14, 11, 0, 0, TimeSpan.FromHours(1)),
            new DateTimeOffset(2026, 10, 14, 8, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero),
            "Europe/London", "BST");

        var cut = Render<EventTime>(p => p
            .Add(x => x.Value, time)
            .Add(x => x.LocationName, "London HQ")
            .Add(x => x.ShowLocation, true));

        Assert.Contains("Wed 14 Oct 2026, 09:30–11:00 BST", cut.Markup);
        Assert.Contains("London HQ", cut.Markup);
    }

    [Fact]
    public void TwoStepButtonRequiresASecondActivation()
    {
        var confirmed = 0;
        var cut = Render<TwoStepButton>(parameters => parameters
            .Add(component => component.Label, "Cancel event")
            .Add(component => component.ConfirmLabel, "Confirm cancellation")
            .Add(component => component.Consequence, "Cancels 3 bookings")
            .Add(component => component.Confirmed,
                EventCallback.Factory.Create(this, () => confirmed++)));

        cut.Find("button").Click();

        Assert.Equal(0, confirmed);
        Assert.Equal("Confirm cancellation", cut.Find("button").TextContent.Trim());
        Assert.Contains("Cancels 3 bookings", cut.Find("[role='status']").TextContent);

        cut.Find("button").Click();

        Assert.Equal(1, confirmed);
        Assert.Equal("Cancel event", cut.Find("button").TextContent.Trim());
        Assert.Empty(cut.FindAll("[role='status']"));
    }

    [Fact]
    public void TwoStepButtonResetsAfterItsTenSecondWindow()
    {
        var time = new ManualTimeProvider();
        var cut = Render<TwoStepButton>(parameters => parameters
            .Add(component => component.Label, "Cancel event")
            .Add(component => component.ConfirmLabel, "Confirm cancellation")
            .Add(component => component.Consequence, "Cancels 3 bookings")
            .Add(component => component.TimeProvider, time));

        cut.Find("button").Click();
        Assert.Equal(TimeSpan.FromSeconds(10), time.LastDueTime);

        time.Elapse();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Cancel event", cut.Find("button").TextContent.Trim());
            Assert.Empty(cut.FindAll("[role='status']"));
        });
    }

    [Fact]
    public void TwoStepButtonResetsWhenNavigationChanges()
    {
        var cut = Render<TwoStepButton>(parameters => parameters
            .Add(component => component.Label, "Cancel event")
            .Add(component => component.ConfirmLabel, "Confirm cancellation")
            .Add(component => component.Consequence, "Cancels 3 bookings"));
        cut.Find("button").Click();

        Services.GetRequiredService<NavigationManager>().NavigateTo("/another-page");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Cancel event", cut.Find("button").TextContent.Trim());
            Assert.Empty(cut.FindAll("[role='status']"));
        });
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private ManualTimer? _timer;

        public TimeSpan? LastDueTime { get; private set; }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            LastDueTime = dueTime;
            _timer = new ManualTimer(callback, state);
            return _timer;
        }

        public void Elapse() => _timer?.Fire();

        private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
        {
            private bool _active = true;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                _active = true;
                return true;
            }

            public void Dispose() => _active = false;

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            public void Fire()
            {
                if (!_active)
                    return;

                _active = false;
                callback(state);
            }
        }
    }

    private sealed record Row(Guid Id, IReadOnlyList<TypeCapacity> Capacities);
}
```

Create the transport tests in full. They pin the slug, extensions, 403 distinction, cursor and
idempotency behaviour before any page consumes them:

```csharp
// tests/EventBooking.Web.Tests/Contracts/ApiCallContractTests.cs (complete)
using System.Net;
using System.Text;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests.Contracts;

public sealed class ApiCallContractTests
{
    [Fact]
    public async Task Rfc9457TypeAndExtensionsSurviveParsing()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(
                """{"type":"capacity-below-bookings","title":"Capacity conflict","status":409,"detail":"Keep the value.","minimum":4,"current":{"totalHeadcount":6},"errors":[{"field":"totalHeadcount","code":"too-low","message":"Minimum is 4."}]}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

        var result = await ApiCall.ReadAsync<object>(response, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("capacity-below-bookings", result.ErrorCode);
        Assert.Equal(4, result.Problem!.Minimum);
        Assert.Equal("too-low", Assert.Single(result.Problem.Errors).Code);
        Assert.Equal(6, result.Problem.Current!.Value.GetProperty("totalHeadcount").GetInt32());
    }

    [Fact]
    public async Task ForbiddenIsNotRewrittenAsUnauthenticated()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent(
                """{"type":"forbidden","title":"Forbidden","status":403,"detail":"Scope is required."}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

        var result = await ApiCall.ReadAsync<object>(response, CancellationToken.None);

        Assert.Equal(403, result.StatusCode);
        Assert.Equal("forbidden", result.ErrorCode);
    }

    [Fact]
    public void SubmissionKeyIsStableForOneAttemptAndDifferentForTheNext()
    {
        var first = IdempotencySubmission.Start();
        var retryKey = first.Key;
        var second = IdempotencySubmission.Start();

        Assert.Equal(retryKey, first.Key);
        Assert.NotEqual(first.Key, second.Key);
    }
}
```

```csharp
// tests/EventBooking.Web.Tests/Contracts/OpenApiClientContractTests.cs (complete)
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests.Contracts;

public sealed class OpenApiClientContractTests
{
    private static readonly (Type Client, string Schema)[] Contracts =
    [
        (typeof(ApiLink), "ApiLink"),
        (typeof(EventTimeDto), "EventTimeResponse"),
        (typeof(MeDto), "CurrentStaffResponse"),
    ];

    [Fact]
    public void ClientContractsExactlyMatchTheirOpenApiSchemas()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(SnapshotPath()));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var nullability = new NullabilityInfoContext();

        foreach (var (client, schemaName) in Contracts)
        {
            var schema = schemas.GetProperty(schemaName);
            var properties = schema.GetProperty("properties");
            var clientProperties = client.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(x => x.GetCustomAttribute<JsonIgnoreAttribute>() is null)
                .ToArray();
            var clientNames = clientProperties.Select(JsonName).Order().ToArray();
            var schemaNames = properties.EnumerateObject().Select(x => x.Name).Order().ToArray();
            Assert.Equal(schemaNames, clientNames);

            var required = schema.TryGetProperty("required", out var requiredElement)
                ? requiredElement.EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal)
                : [];
            foreach (var property in clientProperties)
            {
                var name = JsonName(property);
                var clientAllowsNull = property.PropertyType.IsValueType
                    ? Nullable.GetUnderlyingType(property.PropertyType) is not null
                    : nullability.Create(property).ReadState != NullabilityState.NotNull;
                Assert.Equal(clientAllowsNull,
                    AllowsNull(document.RootElement, properties.GetProperty(name)));
                if (!clientAllowsNull) Assert.Contains(name, required);
            }
        }
    }

    [Fact]
    public void EveryListOperationReturnsItemsAndNextCursor()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(SnapshotPath()));
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            if (!path.Value.TryGetProperty("get", out var get)
                || !get.TryGetProperty("x-eventbooking-list", out var marker)
                || !marker.GetBoolean())
            {
                continue;
            }

            var schema = get.GetProperty("responses").GetProperty("200")
                .GetProperty("content").GetProperty("application/json").GetProperty("schema");
            var properties = ResolveSchema(document.RootElement, schema).GetProperty("properties");
            Assert.True(properties.TryGetProperty("items", out _), path.Name);
            Assert.True(properties.TryGetProperty("nextCursor", out _), path.Name);
        }
    }

    private static JsonElement ResolveSchema(JsonElement root, JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out var reference)) return schema;
        return reference.GetString()!.Split('/').Skip(1).Aggregate(root, (value, part) => value.GetProperty(part));
    }

    private static bool AllowsNull(JsonElement root, JsonElement schema)
    {
        if (schema.TryGetProperty("nullable", out var nullable) && nullable.GetBoolean()) return true;
        if (schema.TryGetProperty("type", out var type))
        {
            if (type.ValueKind == JsonValueKind.String) return type.GetString() == "null";
            if (type.ValueKind == JsonValueKind.Array)
                return type.EnumerateArray().Any(x => x.GetString() == "null");
        }
        if (schema.TryGetProperty("anyOf", out var anyOf))
            return anyOf.EnumerateArray().Any(item => AllowsNull(root, item));
        return schema.TryGetProperty("$ref", out var reference)
            && AllowsNull(root, ResolveSchema(root, schema));
    }

    private static string JsonName(PropertyInfo property) =>
        property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
        ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name);

    private static string SnapshotPath() => Path.Combine(
        AppContext.BaseDirectory, "Contracts", "openapi-v1.json");
}
```

```csharp
// tests/EventBooking.Web.Tests/MeClientTests.cs (complete; replace the predecessor file)
using System.Net;
using System.Text;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public sealed class MeClientTests
{
    [Fact]
    public async Task ReadsCallerSpecificCollectionAffordancesFromMe()
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"displayName":"Alex","staffId":"A1","roles":["Admin"],"scopeAppointmentTypeId":null,"scopeAppointmentTypeCode":null,"scopeAppointmentTypeName":null,"capabilities":["ManageReferenceData"],"problem":null,"_links":{"createLocation":{"href":"/api/locations","method":"POST","operationId":"createLocation"}}}""",
                Encoding.UTF8, "application/json"),
        });
        var client = new MeClient(new HttpClient(handler)
            { BaseAddress = new Uri("https://api.example") });

        var result = await client.GetAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var link = Assert.Single(result.Value!.Links).Value;
        Assert.Equal("createLocation", link.OperationId);
        Assert.Equal("/api/me", handler.Path);
    }

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public string? Path { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            Path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(response);
        }
    }
}
```

```csharp
// tests/EventBooking.Web.Tests/Theme/ThemeIsolationTests.cs (complete)
using System.Text.RegularExpressions;

namespace EventBooking.Web.Tests.Theme;

public sealed partial class ThemeIsolationTests
{
    [Fact]
    public void ColourLiteralsExistOnlyInThemeCss()
    {
        var root = RepositoryRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "src", "EventBooking.Web"), "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".razor", StringComparison.Ordinal)
                || path.EndsWith(".css", StringComparison.Ordinal))
            .Where(path => !path.EndsWith(Path.Combine("wwwroot", "theme.css"), StringComparison.Ordinal));

        var offenders = files
            .SelectMany(path => ColourLiteral().Matches(File.ReadAllText(path))
                .Select(match => $"{Path.GetRelativePath(root, path)}: {match.Value}"))
            .ToArray();

        Assert.Empty(offenders);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EventBooking.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b|\brgba?\s*\(|\bhsla?\s*\(")]
    private static partial Regex ColourLiteral();
}
```

- [ ] **Step 2: Run the focused tests and verify the red state**

```bash
dotnet test tests/EventBooking.Web.Tests --filter \
  "FullyQualifiedName~DesignSystemComponentTests|FullyQualifiedName~ApiCallContractTests|FullyQualifiedName~OpenApiClientContractTests|FullyQualifiedName~MeClientTests|FullyQualifiedName~ThemeIsolationTests"
```

Expected: FAIL because the components and client contracts do not exist, the old parser reads a
problem title as its code, the OpenAPI snapshot is absent, and branded colour literals remain.

- [ ] **Step 3: Implement the neutral shell and client primitives**

Pin the browser library and add the test project:

```xml
<!-- Directory.Packages.props — add inside the existing ItemGroup -->
<PackageVersion Include="Microsoft.Playwright" Version="1.62.0" />
```

```bash
dotnet sln EventBooking.sln add tests/EventBooking.Web.E2E/EventBooking.Web.E2E.csproj
```

Replace appsettings with organisation-neutral values:

```json
{
  "ApiBaseUrl": "https://localhost:5001",
  "ProductName": "EventBooking",
  "LogoPath": null,
  "CoordinatorContact": "events@example.com",
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://localhost:8081/realms/eventbooking",
      "ClientId": "eventbooking-web"
    }
  }
}
```

Create the token file. This is the only CSS file in which literal colours are legal:

```css
/* src/EventBooking.Web/wwwroot/theme.css (complete) */
:root {
  --color-primary: #1f4e79;
  --color-on-primary: #ffffff;
  --color-accent: #6b4fa3;
  --color-danger: #b42318;
  --color-warning: #b54708;
  --color-success: #067647;
  --color-neutral-50: #f8fafc;
  --color-neutral-100: #f1f5f9;
  --color-neutral-200: #e2e8f0;
  --color-neutral-500: #64748b;
  --color-neutral-700: #334155;
  --color-neutral-900: #0f172a;
  --color-type-0-bg: #dbeafe; --color-type-0-fg: #173b63;
  --color-type-1-bg: #ede9fe; --color-type-1-fg: #4c2a85;
  --color-type-2-bg: #dcfce7; --color-type-2-fg: #14532d;
  --color-type-3-bg: #ffedd5; --color-type-3-fg: #7c2d12;
  --color-type-4-bg: #fce7f3; --color-type-4-fg: #831843;
  --color-type-5-bg: #cffafe; --color-type-5-fg: #164e63;
  --color-type-6-bg: #fef9c3; --color-type-6-fg: #713f12;
  --color-type-7-bg: #e0e7ff; --color-type-7-fg: #312e81;
  --space-1: 0.25rem; --space-2: 0.5rem; --space-3: 0.75rem;
  --space-4: 1rem; --space-6: 1.5rem; --space-8: 2rem; --space-12: 3rem;
  --radius-control: 0.25rem; --radius-card: 0.5rem;
  --font-body: system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
  --font-size-body: 0.875rem; --font-size-label: 0.75rem;
  --focus-ring: 0 0 0 0.2rem color-mix(in srgb, var(--color-primary) 35%, transparent);
}
```

Replace app.css with this complete neutral structural sheet and create the shared component sheet:

```css
/* src/EventBooking.Web/wwwroot/css/app.css (complete) */
@import url("../theme.css");
* { box-sizing: border-box; }
html { font-family: var(--font-body); color: var(--color-neutral-900); background: var(--color-neutral-50); }
body { margin: 0; font-size: var(--font-size-body); line-height: 1.5; }
a { color: var(--color-primary); }
button, input, select, textarea { font: inherit; }
button:focus-visible, a:focus-visible, input:focus-visible, select:focus-visible, textarea:focus-visible {
  outline: 2px solid var(--color-primary); outline-offset: 2px; box-shadow: var(--focus-ring);
}
.app-shell { min-height: 100vh; display: grid; grid-template-rows: auto auto 1fr auto; }
.topbar { display: flex; align-items: center; justify-content: space-between; gap: var(--space-4); padding: var(--space-4) var(--space-6); background: var(--color-primary); color: var(--color-on-primary); }
.brand-lockup { display: flex; align-items: center; gap: var(--space-3); }
.brand, .topbar a { color: var(--color-on-primary); font-weight: 700; text-decoration: none; }
.brand-mark { max-height: 2.5rem; max-width: 10rem; }
.staff-nav { display: flex; flex-wrap: wrap; gap: var(--space-2); padding: var(--space-2) var(--space-6); background: var(--color-neutral-100); }
.staff-nav-link { padding: var(--space-2) var(--space-3); border-radius: var(--radius-control); }
.staff-nav-link.active { background: var(--color-primary); color: var(--color-on-primary); }
.main-content { width: min(100%, 90rem); margin-inline: auto; padding: var(--space-6); }
.page-header { display: flex; justify-content: space-between; gap: var(--space-4); align-items: start; }
.card { background: var(--color-on-primary); border: 1px solid var(--color-neutral-200); border-radius: var(--radius-card); margin-block: var(--space-4); padding: var(--space-4); }
.button { min-height: 2.75rem; border: 1px solid var(--color-primary); border-radius: var(--radius-control); padding: var(--space-2) var(--space-4); background: var(--color-on-primary); color: var(--color-primary); }
.button-primary { background: var(--color-primary); color: var(--color-on-primary); }
.button-danger { border-color: var(--color-danger); color: var(--color-danger); }
.field { display: grid; gap: var(--space-1); margin-block: var(--space-3); }
.field > label, .field-label { font-size: var(--font-size-label); font-weight: 600; }
.hint, .muted { color: var(--color-neutral-700); }
.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; }
th, td { padding: var(--space-3); text-align: left; border-bottom: 1px solid var(--color-neutral-200); vertical-align: top; }
.row-actions, .chip-row { display: flex; flex-wrap: wrap; gap: var(--space-2); }
.visually-hidden { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0,0,0,0); white-space: nowrap; border: 0; }
.app-footer { display: flex; flex-wrap: wrap; gap: var(--space-4); justify-content: space-between; padding: var(--space-4) var(--space-6); background: var(--color-neutral-100); }
@media (max-width: 64rem) { .main-content { padding: var(--space-4); } table, thead, tbody, tr, th, td { display: block; } thead { position: absolute; left: -10000px; } td::before { content: attr(data-label); display: block; font-weight: 600; } }
@media (max-width: 40rem) { .topbar, .page-header { align-items: stretch; flex-direction: column; } .main-content { padding: var(--space-3); } .button { width: 100%; } }
```

```css
/* src/EventBooking.Web/Components/components.css (complete) */
.status-badge, .type-chip { display: inline-flex; align-items: center; border-radius: 999px; padding: var(--space-1) var(--space-2); font-weight: 600; }
.status-success { color: var(--color-success); } .status-warning { color: var(--color-warning); }
.status-error, .status-failed, .status-noshow { color: var(--color-danger); }
.status-info, .status-open, .status-active { color: var(--color-primary); }
.type-palette-0 { background: var(--color-type-0-bg); color: var(--color-type-0-fg); }
.type-palette-1 { background: var(--color-type-1-bg); color: var(--color-type-1-fg); }
.type-palette-2 { background: var(--color-type-2-bg); color: var(--color-type-2-fg); }
.type-palette-3 { background: var(--color-type-3-bg); color: var(--color-type-3-fg); }
.type-palette-4 { background: var(--color-type-4-bg); color: var(--color-type-4-fg); }
.type-palette-5 { background: var(--color-type-5-bg); color: var(--color-type-5-fg); }
.type-palette-6 { background: var(--color-type-6-bg); color: var(--color-type-6-fg); }
.type-palette-7 { background: var(--color-type-7-bg); color: var(--color-type-7-fg); }
.banner { border-inline-start: 0.25rem solid currentColor; padding: var(--space-3); margin-block: var(--space-3); background: var(--color-neutral-50); }
.banner-info { color: var(--color-primary); } .banner-success { color: var(--color-success); }
.banner-warning { color: var(--color-warning); } .banner-error { color: var(--color-danger); }
.picker-list, .capacity-breakdown { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--space-2); }
.loading-skeleton { min-height: 6rem; background: var(--color-neutral-100); border-radius: var(--radius-card); }
```

Update index.html to link only `theme.css`, Bootstrap if still required by framework auth, app.css
and scoped styles; set the title, description and loading splash to EventBooking; remove the font
stylesheet, predecessor logo and every predecessor name. ProductOptions owns optional branding:

```csharp
// src/EventBooking.Web/Services/ProductOptions.cs (complete)
namespace EventBooking.Web.Services;

public sealed record ProductOptions(
    string ProductName, string? LogoPath, string CoordinatorContact);
```

```csharp
// src/EventBooking.Web/Services/ClientContracts.cs (complete)
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventBooking.Web.Services;

public sealed record ApiLink(string Href, string Method, string OperationId);
public sealed record PageDto<T>(IReadOnlyList<T> Items, string? NextCursor);
public sealed record FieldProblem(string? Field, int? Line, string Code, string? Message);
public sealed record ApiProblem(
    string Type, string? Title, int Status, string? Detail,
    IReadOnlyList<FieldProblem> Errors,
    JsonElement? Current, JsonElement? Consequence, int? Minimum, JsonElement? Blocking)
{
    public static ApiProblem Validation(IReadOnlyDictionary<string, string[]> errors) => new(
        "validation-failed", "Validation failed", 422, null,
        errors.SelectMany(pair => pair.Value.Select(message =>
            new FieldProblem(pair.Key, null, "invalid", message))).ToArray(),
        null, null, null, null);

    public static ApiProblem FromSlug(
        string slug, string detail,
        IReadOnlyDictionary<string, object?>? extensions = null)
    {
        extensions ??= new Dictionary<string, object?>();
        extensions.TryGetValue("current", out var current);
        extensions.TryGetValue("consequence", out var consequence);
        extensions.TryGetValue("blocking", out var blocking);
        var minimum = extensions.TryGetValue("minimum", out var rawMinimum)
            ? Convert.ToInt32(rawMinimum, System.Globalization.CultureInfo.InvariantCulture)
            : (int?)null;
        return new(slug, "Request refused", 409, detail, [],
            Element(current), Element(consequence), minimum, Element(blocking));
    }

    private static JsonElement? Element(object? value) =>
        value is null ? null : JsonSerializer.SerializeToElement(value);
}
public sealed record EventTimeDto(
    DateOnly Date, TimeOnly StartTime, int DurationMinutes,
    DateTimeOffset StartLocal, DateTimeOffset EndLocal,
    DateTimeOffset StartUtc, DateTimeOffset EndUtc,
    string TimeZoneId, string ZoneAbbreviation);

[method: JsonConstructor]
public sealed record MeDto(
    string? DisplayName, string? StaffId, IReadOnlyList<string> Roles,
    Guid? ScopeAppointmentTypeId, string? ScopeAppointmentTypeCode,
    string? ScopeAppointmentTypeName, IReadOnlyList<string> Capabilities, string? Problem,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    public MeDto(
        IReadOnlyList<string> roles, Guid? appointmentTypeId, string? appointmentTypeName)
        : this(null, null, roles, appointmentTypeId, null, appointmentTypeName, [], null,
            new Dictionary<string, ApiLink>()) { }

    // Compatibility aliases are client-only and remain excluded from the wire contract.
    [JsonIgnore] public Guid? AppointmentTypeId => ScopeAppointmentTypeId;
    [JsonIgnore] public string? AppointmentTypeName => ScopeAppointmentTypeName;
}

public static class LinkRelations
{
    public static bool Allows(this IReadOnlyDictionary<string, ApiLink> links, string relation) =>
        links.ContainsKey(relation);
}
```

```csharp
// src/EventBooking.Web/Services/MeClient.cs (complete; replace the predecessor file)
namespace EventBooking.Web.Services;

public interface IMeClient
{
    Task<ApiOutcome<MeDto>> GetAsync(CancellationToken ct);
}

public sealed class MeClient(HttpClient http) : IMeClient
{
    public async Task<ApiOutcome<MeDto>> GetAsync(CancellationToken ct)
    {
        using var response = await http.GetAsync("/api/me", ct);
        return await ApiCall.ReadAsync<MeDto>(response, ct);
    }
}
```

```csharp
// src/EventBooking.Web/Services/IdempotencySubmission.cs (complete)
namespace EventBooking.Web.Services;

public sealed class IdempotencySubmission
{
    private IdempotencySubmission(string key) => Key = key;
    public string Key { get; }
    public static IdempotencySubmission Start() => new(Guid.NewGuid().ToString("N"));
}
```

```csharp
// src/EventBooking.Web/Services/ApiOutcome.cs (complete)
namespace EventBooking.Web.Services;

public sealed record ApiOutcome<T>(bool IsSuccess, T? Value, int StatusCode, ApiProblem? Problem)
{
    public string? ErrorCode => Problem?.Type;
    public string? ErrorMessage => Problem?.Detail ?? Problem?.Title;
    public static ApiOutcome<T> Success(T value) => Success(value, 200);
    public static ApiOutcome<T> Success(T value, int status) => new(true, value, status, null);
    public static ApiOutcome<T> Failure(ApiProblem problem) => new(false, default, problem.Status, problem);
}
```

```csharp
// tests/EventBooking.Web.Tests/Contracts/TestContractFactory.cs (complete)
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public static class TestContractFactory
{
    public static EventTimeDto EventTime(string _)
    {
        var start = new DateTimeOffset(2026, 10, 14, 9, 30, 0, TimeSpan.FromHours(1));
        return new EventTimeDto(
            new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 90,
            start, start.AddMinutes(90), start.ToUniversalTime(),
            start.AddMinutes(90).ToUniversalTime(), "Europe/London", "BST");
    }
}
```

```csharp
// src/EventBooking.Web/Services/ApiCall.cs (complete)
using System.Net.Http.Json;
using System.Text.Json;

namespace EventBooking.Web.Services;

public static class ApiCall
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<ApiOutcome<T>> ReadAsync<T>(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
            if (value is not null) return ApiOutcome<T>.Success(value, (int)response.StatusCode);
            return ApiOutcome<T>.Failure(Generic((int)response.StatusCode));
        }

        return ApiOutcome<T>.Failure(await ReadProblemAsync(response, cancellationToken));
    }

    public static async Task<ApiOutcome<bool>> ReadNoContentAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return ApiOutcome<bool>.Success(true, (int)response.StatusCode);
        return ApiOutcome<bool>.Failure(await ReadProblemAsync(response, cancellationToken));
    }

    public static async Task<ApiOutcome<string>> ReadTextAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return ApiOutcome<string>.Success(
                await response.Content.ReadAsStringAsync(cancellationToken),
                (int)response.StatusCode);
        return ApiOutcome<string>.Failure(await ReadProblemAsync(response, cancellationToken));
    }

    private static async Task<ApiProblem> ReadProblemAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            var errors = root.TryGetProperty("errors", out var array)
                ? array.EnumerateArray().Select(x => new FieldProblem(
                    Text(x, "field"), Number(x, "line"), Text(x, "code") ?? "unknown", Text(x, "message"))).ToArray()
                : [];
            return new ApiProblem(
                Text(root, "type") ?? "unexpected",
                Text(root, "title"),
                Number(root, "status") ?? (int)response.StatusCode,
                Text(root, "detail"),
                errors,
                Clone(root, "current"), Clone(root, "consequence"), Number(root, "minimum"), Clone(root, "blocking"));
        }
        catch (JsonException)
        {
            return Generic((int)response.StatusCode);
        }
    }

    private static ApiProblem Generic(int status) => new(
        "unexpected", "Something went wrong", status,
        "Something went wrong. Please try again.", [], null, null, null, null);
    private static string? Text(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() : null;
    private static int? Number(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.TryGetInt32(out var number)
            ? number : null;
    private static JsonElement? Clone(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) ? property.Clone() : null;
}
```

Create the component model and components exactly as follows:

```csharp
// src/EventBooking.Web/Components/ComponentModels.cs (complete)
using Microsoft.AspNetCore.Components;

namespace EventBooking.Web.Components;

public enum BannerVariant { Info, Success, Warning, Error }
public enum LocationPickerMode { Single, Multi }
public sealed record TypeOption(Guid Id, string Code, string Name, bool IsActive, bool HasManager);
public sealed record LocationOption(Guid Id, string Name, string Address, string ZoneAbbreviation, bool IsActive);
public sealed record TypeCapacity(Guid AppointmentTypeId, string Code, string Name, int Remaining, int Total);
public sealed record TableColumn<TItem>(string Heading, RenderFragment<TItem> Cell);
```

```razor
@* src/EventBooking.Web/Components/StatusBadge.razor (complete) *@
<span class="status-badge status-@Value.ToLowerInvariant()">@Display</span>
@code { [Parameter, EditorRequired] public string Value { get; set; } = "info"; [Parameter, EditorRequired] public string Display { get; set; } = "Information"; }
```

```razor
@* src/EventBooking.Web/Components/TypeChip.razor (complete) *@
<span class="type-chip type-palette-@PaletteIndex(Code)" title="@Code">@Name</span>
@code {
    [Parameter, EditorRequired] public string Code { get; set; } = string.Empty;
    [Parameter, EditorRequired] public string Name { get; set; } = string.Empty;
    private static int PaletteIndex(string code)
    {
        unchecked { var hash = 17; foreach (var character in code) hash = (hash * 31) + character; return (hash & int.MaxValue) % 8; }
    }
}
```

```razor
@* src/EventBooking.Web/Components/Banner.razor (complete) *@
@if (!string.IsNullOrWhiteSpace(Message))
{
    <div class="banner banner-@Variant.ToString().ToLowerInvariant()" role="@(Variant == BannerVariant.Error ? "alert" : "status")" aria-live="@(Variant == BannerVariant.Error ? "assertive" : "polite")">
        <span>@Message</span>
        @if (Retry.HasDelegate) { <button type="button" class="button" @onclick="Retry">Retry</button> }
    </div>
}
@code { [Parameter] public BannerVariant Variant { get; set; } [Parameter] public string? Message { get; set; } [Parameter] public EventCallback Retry { get; set; } }
```

```razor
@* src/EventBooking.Web/Components/CsvImportResult.razor (complete) *@
@using EventBooking.Web.Services
@if (Accepted) { <Banner Variant="BannerVariant.Success" Message="@($"{ImportedCount} attendees imported.")" /> }
else if (Errors.Count > 0)
{
    <div class="banner banner-error" role="alert" aria-live="assertive">
        <strong>Nothing was imported.</strong>
        <ul>@foreach (var error in Errors) { <li>Line @(error.Line ?? 0): @error.Message</li> }</ul>
    </div>
}
@code { [Parameter] public bool Accepted { get; set; } [Parameter] public int ImportedCount { get; set; } [Parameter] public IReadOnlyList<FieldProblem> Errors { get; set; } = []; }
```

```razor
@* src/EventBooking.Web/Components/EventTime.razor (complete) *@
@using System.Globalization
@using EventBooking.Web.Services
<time datetime="@Value.StartUtc.ToString("O")">@Value.StartLocal.ToString("ddd dd MMM yyyy, HH:mm", CultureInfo.GetCultureInfo("en-GB"))–@Value.EndLocal.ToString("HH:mm", CultureInfo.InvariantCulture) @Value.ZoneAbbreviation</time>
@if (ShowLocation && !string.IsNullOrWhiteSpace(LocationName)) { <span class="event-location"> · @LocationName</span> }
@code { [Parameter, EditorRequired] public EventTimeDto Value { get; set; } = default!; [Parameter] public string? LocationName { get; set; } [Parameter] public bool ShowLocation { get; set; } }
```

```razor
@* src/EventBooking.Web/Components/TypePicker.razor (complete) *@
<fieldset><legend>@Legend</legend><input type="search" aria-label="Search appointment types" @bind="_search" @bind:event="oninput" />
<ul class="picker-list">@foreach (var option in Visible)
{
    var locked = option.Id == CallerTypeId; var disabled = locked || !option.IsActive || !option.HasManager;
    <li><label><input type="checkbox" value="@option.Id" checked="@SelectedIds.Contains(option.Id)" disabled="@disabled" @onchange="e => Change(option.Id, (bool?)e.Value == true)" /> <TypeChip Code="@option.Code" Name="@option.Name" />
    @if (locked) { <span> You, required</span> } else if (!option.IsActive) { <span> Inactive</span> } else if (!option.HasManager) { <span> No Manager assigned</span> }</label></li>
}</ul></fieldset>
@code {
    [Parameter] public string Legend { get; set; } = "Appointment types";
    [Parameter] public IReadOnlyList<TypeOption> Options { get; set; } = [];
    [Parameter] public IReadOnlyList<Guid> SelectedIds { get; set; } = [];
    [Parameter] public EventCallback<IReadOnlyList<Guid>> SelectedIdsChanged { get; set; }
    [Parameter] public Guid? CallerTypeId { get; set; }
    private string _search = string.Empty;
    private IEnumerable<TypeOption> Visible => Options.Where(x => string.IsNullOrWhiteSpace(_search) || x.Code.Contains(_search, StringComparison.OrdinalIgnoreCase) || x.Name.Contains(_search, StringComparison.OrdinalIgnoreCase));
    private Task Change(Guid id, bool selected) { var values = SelectedIds.Where(x => x != id).ToList(); if (selected) values.Add(id); if (CallerTypeId is { } own && !values.Contains(own)) values.Insert(0, own); return SelectedIdsChanged.InvokeAsync(values.Distinct().ToArray()); }
}
```

```razor
@* src/EventBooking.Web/Components/LocationPicker.razor (complete) *@
<fieldset><legend>@Legend</legend><ul class="picker-list">@foreach (var option in Options.Where(x => x.IsActive))
{
    <li><label><input type="@(Mode == LocationPickerMode.Single ? "radio" : "checkbox")" name="@_name" value="@option.Id" checked="@SelectedIds.Contains(option.Id)" @onchange="e => Change(option.Id, (bool?)e.Value == true)" /> <strong>@option.Name</strong> <span>@option.Address · @option.ZoneAbbreviation</span></label></li>
}</ul></fieldset>
@code {
    [Parameter] public string Legend { get; set; } = "Locations";
    [Parameter] public LocationPickerMode Mode { get; set; }
    [Parameter] public IReadOnlyList<LocationOption> Options { get; set; } = [];
    [Parameter] public IReadOnlyList<Guid> SelectedIds { get; set; } = [];
    [Parameter] public EventCallback<IReadOnlyList<Guid>> SelectedIdsChanged { get; set; }
    private readonly string _name = $"location-{Guid.NewGuid():N}";
    private Task Change(Guid id, bool selected) { IReadOnlyList<Guid> values = Mode == LocationPickerMode.Single ? [id] : selected ? SelectedIds.Append(id).Distinct().ToArray() : SelectedIds.Where(x => x != id).ToArray(); return SelectedIdsChanged.InvokeAsync(values); }
}
```

```razor
@* src/EventBooking.Web/Components/DataTable.razor (complete) *@
@typeparam TItem
@if (Loading) { <div class="loading-skeleton" role="status"><span class="visually-hidden">@LoadingText</span></div> }
else if (Items.Count == 0) { <div class="empty-state"><strong>@EmptyTitle</strong><p>@EmptyAction</p></div> }
else
{
<div class="table-wrap"><table><thead><tr>@foreach (var column in Columns) { <th scope="col">@column.Heading</th> }
@if (_typeCodes.Count is > 0 and <= 5) { @foreach (var code in _typeCodes) { <th scope="col" data-type-code="@code">@code</th> } }
else if (_typeCodes.Count > 5) { <th scope="col">Capacity</th> }</tr></thead><tbody>
@foreach (var item in Items) { var cells = Capacities?.Invoke(item) ?? []; <tr @key="RowKey(item)" aria-busy="@(BusyKeys.Contains(RowKey(item)) ? "true" : "false")">@foreach (var column in Columns) { <td>@column.Cell(item)</td> }
@if (_typeCodes.Count is > 0 and <= 5) { @foreach (var code in _typeCodes) { var value = cells.SingleOrDefault(x => x.Code == code); <td data-label="@code">@(value is null ? "—" : $"{value.Remaining}/{value.Total}")</td> } }
else if (_typeCodes.Count > 5) { <td data-label="Capacity"><ul class="capacity-breakdown">@foreach (var value in cells.OrderBy(x => x.Code, StringComparer.Ordinal)) { <li>@value.Code: @value.Remaining/@value.Total</li> }</ul><details><summary>Full breakdown</summary>@foreach (var value in cells.OrderBy(x => x.Code, StringComparer.Ordinal)) { <TypeChip Code="@value.Code" Name="@value.Name" /> }</details></td> }</tr> }
</tbody></table></div>
}
@code {
    [Parameter] public IReadOnlyList<TItem> Items { get; set; } = [];
    [Parameter, EditorRequired] public Func<TItem, object> RowKey { get; set; } = default!;
    [Parameter] public IReadOnlyList<TableColumn<TItem>> Columns { get; set; } = [];
    [Parameter] public Func<TItem, IReadOnlyList<TypeCapacity>>? Capacities { get; set; }
    [Parameter] public IReadOnlySet<object> BusyKeys { get; set; } = new HashSet<object>();
    [Parameter] public bool Loading { get; set; }
    [Parameter] public string LoadingText { get; set; } = "Loading…";
    [Parameter, EditorRequired] public string EmptyTitle { get; set; } = string.Empty;
    [Parameter, EditorRequired] public string EmptyAction { get; set; } = string.Empty;
    private IReadOnlyList<string> _typeCodes = [];
    protected override void OnParametersSet() => _typeCodes = Capacities is null ? [] : Items.SelectMany(x => Capacities(x)).Select(x => x.Code).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
}
```

```razor
@* src/EventBooking.Web/Components/TwoStepButton.razor (complete) *@
@implements IDisposable
@inject NavigationManager Navigation
<button type="button" class="button button-danger" disabled="@Disabled" @onclick="ActivateAsync" aria-describedby="@(_armed ? _descriptionId : null)">@(_armed ? ConfirmLabel : Label)</button>
@if (_armed) { <span id="@_descriptionId" role="status" aria-live="polite">@Consequence</span> }
@code {
    [Parameter] public string Label { get; set; } = "Delete"; [Parameter] public string ConfirmLabel { get; set; } = "Confirm"; [Parameter, EditorRequired] public string Consequence { get; set; } = string.Empty; [Parameter] public bool Disabled { get; set; } [Parameter] public EventCallback Confirmed { get; set; } [Parameter] public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
    private readonly string _descriptionId = $"confirm-{Guid.NewGuid():N}"; private CancellationTokenSource? _reset; private bool _armed;
    protected override void OnInitialized() => Navigation.LocationChanged += ResetOnNavigation;
    private async Task ActivateAsync() { if (!_armed) { _armed = true; _reset?.Cancel(); _reset = new(); _ = ResetAfterDelayAsync(_reset.Token); return; } _armed = false; _reset?.Cancel(); await Confirmed.InvokeAsync(); }
    private async Task ResetAfterDelayAsync(CancellationToken token) { try { await Task.Delay(TimeSpan.FromSeconds(10), TimeProvider, token); _armed = false; await InvokeAsync(StateHasChanged); } catch (OperationCanceledException) { } }
    private void ResetOnNavigation(object? sender, LocationChangedEventArgs args) { _armed = false; _reset?.Cancel(); _ = InvokeAsync(StateHasChanged); }
    public void Dispose() { Navigation.LocationChanged -= ResetOnNavigation; _reset?.Cancel(); _reset?.Dispose(); }
}
```

Update `_Imports.razor` with `@using EventBooking.Web.Components` and
`@using EventBooking.Web.Services`. Replace BrandMark with an optional image and text fallback;
replace both layouts so every predecessor organisation string is gone and the footer uses the
configured product name. Update App.razor's not-found branch to set a PageTitle and render one H1
with a route back home. Program registers ProductOptions and, only inside the compile-time block
below, the E2E provider:

```csharp
// src/EventBooking.Web/Program.cs — replace the transitional-zone binding and add these registrations.
var product = new EventBooking.Web.Services.ProductOptions(
    builder.Configuration["ProductName"] ?? "EventBooking",
    builder.Configuration["LogoPath"],
    builder.Configuration["CoordinatorContact"] ?? "events@example.org");
builder.Services.AddSingleton(product);
builder.Services.AddScoped<MeClient>();
builder.Services.AddScoped<IMeClient>(services => services.GetRequiredService<MeClient>());

#if EVENTBOOKING_E2E
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, EventBooking.Web.Services.E2EAuthenticationStateProvider>();
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddHttpClient("AuthenticatedApi", client =>
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddHttpClient("AnonymousApi", client =>
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
#else
// Keep the existing OIDC authorization-code-with-PKCE registration and its authenticated client.
#endif
```

```csharp
// src/EventBooking.Web/Services/E2EAuthenticationStateProvider.cs (complete)
#if EVENTBOOKING_E2E
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace EventBooking.Web.Services;

public sealed class E2EAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly string[] DefaultRoles = ["Coordinator", "Manager", "AppointmentStaff"];
    private readonly NavigationManager _navigation;

    public E2EAuthenticationStateProvider(NavigationManager navigation) => _navigation = navigation;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var query = new Uri(_navigation.Uri).Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .FirstOrDefault(part => part[0] == "e2eRoles");
        var value = query is { Length: 2 } ? Uri.UnescapeDataString(query[1]) : null;
        if (string.Equals(value, "anonymous", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

        var roles = value is null ? DefaultRoles : value.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var claims = new[] { new Claim(ClaimTypes.Name, "E2E staff member") }
            .Concat(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return Task.FromResult(new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity(claims, "E2E"))));
    }
}
#endif
```

Keep the Web project server-reference-free and add the component stylesheet as content. Delete its
embedded `docs/user-guides` ItemGroup only in Task 27, when Help moves to static role-guide assets.
Generate and review the OpenAPI snapshot while Task 22b's API is running:

```bash
curl --fail --silent --show-error http://localhost:5001/openapi/v1.json \
  --output tests/EventBooking.Web.Tests/Contracts/openapi-v1.json
```

- [ ] **Step 4: Create the Playwright/axe project and make the initial routes green**

```xml
<!-- tests/EventBooking.Web.E2E/EventBooking.Web.E2E.csproj (complete) -->
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup><IsPackable>false</IsPackable><IsTestProject>true</IsTestProject></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Microsoft.Playwright" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
  <ItemGroup><Using Include="Xunit" /></ItemGroup>
  <ItemGroup>
    <None Include="package.json" CopyToOutputDirectory="PreserveNewest" />
    <None Include="package-lock.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

```json
{
  "name": "eventbooking-web-e2e",
  "private": true,
  "devDependencies": { "axe-core": "4.13.0" }
}
```

Generate the committed lockfile from that exact manifest; do not hand-author it:

```bash
npm install --package-lock-only --prefix tests/EventBooking.Web.E2E
```

```csharp
// tests/EventBooking.Web.E2E/RouteManifest.cs (complete)
namespace EventBooking.Web.E2E;

public sealed record RouteCase(
    string Name, string Path, string FixtureState = "ready", string? SetupAction = null);

public static class RouteManifest
{
    public static IReadOnlyList<RouteCase> All { get; } =
    [
        new("home-ready", "/"),
        new("help-ready", "/help"),
        new("not-found", "/route-that-does-not-exist"),
    ];
}
```

```csharp
// tests/EventBooking.Web.E2E/RouteSetup.cs (complete)
using Microsoft.Playwright;

namespace EventBooking.Web.E2E;

public static class RouteSetup
{
    private static readonly IReadOnlyDictionary<string, Func<IPage, Task>> Actions =
        new Dictionary<string, Func<IPage, Task>>(StringComparer.Ordinal);

    public static async Task ApplyAsync(IPage page, string? action)
    {
        if (action is null) return;
        if (!Actions.TryGetValue(action, out var apply))
            throw new InvalidOperationException($"Unknown E2E setup action '{action}'.");
        await apply(page);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
```

```csharp
// tests/EventBooking.Web.E2E/E2EApiStub.cs (complete)
namespace EventBooking.Web.E2E;

public static class E2EApiStub
{
    public const string StateCookie = "eventbooking-e2e-state";

    public static string FixtureState(HttpContext context) =>
        context.Request.Cookies.TryGetValue(StateCookie, out var value) ? value : "ready";

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/me", (HttpContext context) => Results.Json(new
        {
            displayName = "E2E staff member", staffId = "E2E1",
            roles = new[] { "Coordinator", "Manager", "AppointmentStaff" },
            scopeAppointmentTypeId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            scopeAppointmentTypeCode = "MED",
            scopeAppointmentTypeName = "Medical check",
            capabilities = new[] { "ManageAttendees", "ViewAttendeeDashboards", "ViewAttendeeAudit", "ViewEventAudit", "ManageEventNegotiation", "CancelEvent", "ConductAppointments" },
            problem = (string?)null,
            _links = FixtureState(context) == "reference-read-only"
                ? new Dictionary<string, object>()
                : CollectionLinks(),
        }));
    }

    private static Dictionary<string, object> CollectionLinks() => new()
    {
        ["createLocation"] = new { href = "/api/locations", method = "POST", operationId = "createLocation" },
        ["createAppointmentType"] = new { href = "/api/appointment-types", method = "POST", operationId = "createAppointmentType" },
        ["createAttendeeGroup"] = new { href = "/api/attendee-groups", method = "POST", operationId = "createAttendeeGroup" },
        ["proposeEvent"] = new { href = "/api/event-proposals", method = "POST", operationId = "proposeEvent" },
        ["createAttendee"] = new { href = "/api/attendees", method = "POST", operationId = "createAttendee" },
        ["importAttendees"] = new { href = "/api/attendees/import", method = "POST", operationId = "importAttendees" },
    };
}
```

```csharp
// tests/EventBooking.Web.E2E/WebHostFixture.cs (complete)
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace EventBooking.Web.E2E;

public sealed class WebHostFixture : IAsyncLifetime
{
    private string? _publish;
    private WebApplication? _app;
    public string BaseUrl { get; private set; } = string.Empty;
    public string AxeScript { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var root = RepositoryRoot();
        _publish = Path.Combine(Path.GetTempPath(), "eventbooking-web-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_publish);
        await RunAsync("dotnet", ["publish", "src/EventBooking.Web/EventBooking.Web.csproj", "-c", "Release", "-o", _publish, "-p:DefineConstants=EVENTBOOKING_E2E"], root);
        await RunAsync("npm", ["ci", "--prefix", "tests/EventBooking.Web.E2E"], root);
        AxeScript = Path.Combine(root, "tests", "EventBooking.Web.E2E", "node_modules", "axe-core", "axe.min.js");

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseWebRoot(Path.Combine(_publish, "wwwroot")).UseUrls("http://127.0.0.1:0");
        _app = builder.Build();
        E2EApiStub.Map(_app);
        _app.UseStaticFiles();
        _app.MapFallbackToFile("index.html");
        await _app.StartAsync();
        BaseUrl = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null) await _app.DisposeAsync();
        if (_publish is not null && Directory.Exists(_publish)) Directory.Delete(_publish, true);
    }

    private static async Task RunAsync(string file, IReadOnlyList<string> arguments, string directory)
    {
        var start = new ProcessStartInfo(file)
        {
            WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {file}.");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException((await process.StandardError.ReadToEndAsync()) + (await process.StandardOutput.ReadToEndAsync()));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EventBooking.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
```

```csharp
// tests/EventBooking.Web.E2E/AxeRunner.cs (complete)
using System.Text.Json;
using Microsoft.Playwright;

namespace EventBooking.Web.E2E;

public static class AxeRunner
{
    public static async Task<IReadOnlyList<string>> ViolationsAsync(IPage page, string script)
    {
        await page.AddScriptTagAsync(new PageAddScriptTagOptions { Path = script });
        var result = await page.EvaluateAsync<JsonElement>(
            "async () => await axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a','wcag2aa','wcag21a','wcag21aa'] } })");
        return result.GetProperty("violations").EnumerateArray()
            .Select(v => $"{v.GetProperty("id").GetString()}: {v.GetProperty("help").GetString()}")
            .ToArray();
    }
}
```

```csharp
// tests/EventBooking.Web.E2E/AccessibilityTests.cs (complete)
using Microsoft.Playwright;

namespace EventBooking.Web.E2E;

public sealed class AccessibilityTests : IClassFixture<WebHostFixture>, IAsyncLifetime
{
    private readonly WebHostFixture _host;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    public AccessibilityTests(WebHostFixture host) => _host = host;

    public async ValueTask InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
    }

    public static TheoryData<string, string, string, string?, int, int> Routes()
    {
        var data = new TheoryData<string, string, string, string?, int, int>();
        foreach (var route in RouteManifest.All)
        {
            data.Add(route.Name, route.Path, route.FixtureState, route.SetupAction, 390, 844);
            data.Add(route.Name, route.Path, route.FixtureState, route.SetupAction, 1440, 900);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task RouteHasNoAxeViolations(
        string name, string path, string fixtureState, string? setupAction, int width, int height)
    {
        await using var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { ViewportSize = new ViewportSize { Width = width, Height = height } });
        var host = new Uri(_host.BaseUrl);
        await context.AddCookiesAsync([new Cookie
        {
            Name = E2EApiStub.StateCookie, Value = fixtureState,
            Domain = host.Host, Path = "/", HttpOnly = true, SameSite = SameSiteAttribute.Lax,
        }]);
        var page = await context.NewPageAsync();
        await page.GotoAsync(_host.BaseUrl + path, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator("#app").WaitForAsync();
        await RouteSetup.ApplyAsync(page, setupAction);
        var violations = await AxeRunner.ViolationsAsync(page, _host.AxeScript);
        Assert.True(violations.Count == 0, $"{name} at {width}x{height}: {string.Join(Environment.NewLine, violations)}");
    }
}
```

Add an `axe` job to the workflow. It is not path-filtered independently from a required check;
the workflow's existing top-level filter remains the one described in AGENTS.md:

```yaml
  axe:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"
      - uses: actions/setup-node@v4
        with:
          node-version: "20"
          cache: npm
          cache-dependency-path: tests/EventBooking.Web.E2E/package-lock.json
      - name: Build browser test project
        run: dotnet build tests/EventBooking.Web.E2E -c Release
      - name: Install Chromium
        run: pwsh tests/EventBooking.Web.E2E/bin/Release/net10.0/playwright.ps1 install --with-deps chromium
      - name: Run route accessibility matrix
        run: dotnet test tests/EventBooking.Web.E2E -c Release --no-build --logger "console;verbosity=normal"
```

Run both mechanical sweeps from HANDOVER section 6a. Print the match count for the async sweep.
Then run:

```bash
npm ci --prefix tests/EventBooking.Web.E2E
dotnet build tests/EventBooking.Web.E2E -c Release
pwsh tests/EventBooking.Web.E2E/bin/Release/net10.0/playwright.ps1 install chromium
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
dotnet test tests/EventBooking.Web.E2E -c Release --no-build
```

Expected: PASS, zero skipped tests and zero axe violations. No count is observed in this authored
document; record the executor's real counts. Confirm a normal publish contains no E2E principal by
running `strings` or `rg` over its generated boot resources for `E2E staff member` and expecting no
match.

- [ ] **Step 5: Commit and push**

```bash
git add -A
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
git commit -m "feat(web): neutral theme and design-system components"
git push
```
