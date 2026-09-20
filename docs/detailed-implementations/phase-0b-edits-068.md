# 00b — Vocabulary edits 68 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — src/EventBooking.Web/wwwroot/css/app.css — 2/2

<!-- vocabulary-file: {"id":218,"oldPath":"src/EventBooking.Web/wwwroot/css/app.css","newPath":"src/EventBooking.Web/wwwroot/css/app.css","beforeSha":"4f6b728919404d0294a19cea3227b24930789630d249ccda16617f25a26bfad6","afterSha":"0c04e877642811b234a57684ad0a7f3967fe67136da1008a08345cb9d976f957","side":"after","part":2,"parts":2} -->

`````text
    padding: 56px 28px 60px;
    text-align: center;
}

.landing-intro {
    align-items: center;
    display: flex;
    flex-direction: column;
    gap: 10px;
}

.landing h1 {
    font-size: 1.75rem;
    line-height: 1.2;
    margin: 0;
}

.landing p {
    color: var(--sub);
    font-size: 0.9375rem;
    line-height: 1.55;
    margin: 0;
    max-width: 52ch;
}

.landing-links {
    display: grid;
    gap: 12px;
    grid-template-columns: repeat(auto-fit, minmax(258px, 1fr));
    margin: 32px auto 0;
    text-align: left;
}

.link-card {
    align-items: center;
    background: var(--surface);
    border: 1px solid var(--line);
    border-radius: var(--radius);
    box-shadow: var(--shadow-sm);
    color: var(--ink-strong);
    display: flex;
    font-size: 0.9375rem;
    font-weight: 600;
    gap: 12px;
    min-height: 72px;
    padding: 16px 18px;
    text-decoration: none;
    transition: border-color 120ms ease, box-shadow 120ms ease, transform 120ms ease;
}

.link-card::before {
    align-self: stretch;
    background: linear-gradient(180deg, var(--ba-speedmarque-red), var(--ba-chatham-blue));
    border-radius: 999px;
    content: "";
    flex: 0 0 3px;
}

.link-card:hover {
    border-color: var(--accent);
    box-shadow: var(--shadow);
    color: var(--ink-strong);
    transform: translateY(-2px);
}

.link-card > span:not(.arrow) {
    flex: 1 1 auto;
}

.arrow {
    color: var(--accent);
    font-size: 1.125rem;
    font-weight: 700;
    margin-left: auto;
}

.landing-sub {
    color: var(--sub);
    display: block;
    font-size: 0.75rem;
    font-weight: 400;
    margin-top: 3px;
}

.landing-hint {
    background: var(--surface);
    border: 1px solid var(--line);
    border-radius: var(--radius);
    color: var(--sub);
    font-size: 0.8125rem !important;
    margin: 28px auto 0 !important;
    max-width: 520px !important;
    padding: 18px 20px;
}

.landing-signed-out {
    min-height: calc(100vh - 220px);
    padding-top: 76px;
}

.access-summary {
    color: var(--sub);
    font-size: 0.8125rem;
    margin-top: 14px;
}

.access-summary p {
    margin: 0 auto;
}

.sign-in-button {
    background: var(--accent);
    border: 1.5px solid var(--accent);
    border-radius: var(--radius-sm);
    box-shadow: var(--shadow-sm);
    color: #fff;
    display: inline-block;
    font-size: 0.875rem;
    font-weight: 600;
    margin-top: 16px;
    padding: 11px 22px;
    text-decoration: none;
}

.sign-in-button:hover {
    background: var(--accent-hover);
    border-color: var(--accent-hover);
    color: #fff;
}

.topbar .sign-in-button {
    background: #fff;
    border-color: #fff;
    box-shadow: none;
    color: var(--accent);
    font-size: 0.8125rem;
    margin-top: 0;
    padding: 8px 16px;
}

.topbar .sign-in-button:hover {
    background: var(--accent-soft);
    border-color: var(--accent-soft);
    color: var(--accent-hover);
}

/* ---------- Utilities ---------- */

.muted {
    color: var(--sub);
}

.error {
    color: var(--error-ink);
    font-weight: 600;
}

.warning {
    color: var(--warning-ink);
    font-weight: 600;
}

.saved {
    color: var(--success-ink);
    font-weight: 600;
}

.visually-hidden {
    border: 0;
    clip: rect(0 0 0 0);
    clip-path: inset(50%);
    height: 1px;
    margin: -1px;
    overflow: hidden;
    padding: 0;
    position: absolute;
    white-space: nowrap;
    width: 1px;
}

/* ---------- Startup and error chrome ---------- */

#app {
    align-items: center;
    display: flex;
    justify-content: center;
    min-height: 100vh;
}

.loading-splash {
    align-items: center;
    display: flex;
    flex-direction: column;
    gap: 16px;
    text-align: center;
}

.loading-splash .brand-mark {
    height: 40px;
    width: auto;
}

.loading-splash-title {
    color: var(--ink-strong);
    font-size: 1.0625rem;
    font-weight: 700;
}

.loading-splash-title span {
    color: var(--sub);
    display: block;
    font-size: 0.6875rem;
    font-weight: 600;
    letter-spacing: 0.14em;
    text-transform: uppercase;
}

.loading-progress {
    display: block;
    height: 5.5rem;
    margin: 0 auto;
    position: relative;
    width: 5.5rem;
}

.loading-progress circle {
    fill: none;
    stroke: var(--line);
    stroke-width: 0.45rem;
    transform: rotate(-90deg);
    transform-origin: 50% 50%;
}

.loading-progress circle:last-child {
    stroke: var(--accent);
    stroke-dasharray: calc(3.141592653589793 * var(--blazor-load-percentage, 0%) * 0.8) 500%;
    transition: stroke-dasharray 80ms linear;
}

.loading-progress-text {
    color: var(--sub);
    font-size: 0.8125rem;
    font-weight: 600;
    letter-spacing: 0.04em;
}

.loading-progress-text::after {
    content: var(--blazor-load-percentage-text, "Loading");
}

#blazor-error-ui {
    background: var(--warning-bg);
    border-top: 3px solid var(--warning-line);
    bottom: 0;
    box-shadow: 0 -2px 10px rgb(0 27 68 / 18%);
    color: var(--warning-ink);
    display: none;
    font-size: 0.8125rem;
    left: 0;
    padding: 0.7rem 1.25rem 0.8rem;
    position: fixed;
    width: 100%;
    z-index: 1000;
}

#blazor-error-ui .reload {
    color: var(--warning-ink);
    font-weight: 600;
}

#blazor-error-ui .dismiss {
    cursor: pointer;
    position: absolute;
    right: 0.75rem;
    top: 0.5rem;
}

/* ---------- Responsive ---------- */

@media (max-width: 760px) {
    .topbar {
        gap: 12px;
        padding: 10px 18px;
    }

    .staff-nav {
        flex-wrap: nowrap;
        overflow-x: auto;
        padding: 0 18px;
    }

    .brand-name {
        font-size: 0.625rem;
        letter-spacing: 0.1em;
    }

    .page {
        gap: 16px;
        padding: 20px 16px 44px;
    }

    .page-header {
        align-items: stretch;
        flex-direction: column;
    }

    .app-footer {
        padding: 16px 18px 22px;
    }

    .landing {
        padding: 44px 18px 42px;
    }

    .landing-signed-out {
        padding-top: 56px;
    }

    .landing-links {
        grid-template-columns: 1fr;
    }

    /* Tables become stacked cards; each cell names its column through data-label. */
    .table-wrap {
        padding: 6px 16px;
    }

    table {
        min-width: 0;
    }

    thead {
        clip: rect(0 0 0 0);
        clip-path: inset(50%);
        height: 1px;
        overflow: hidden;
        position: absolute;
        white-space: nowrap;
        width: 1px;
    }

    tbody tr {
        border-bottom: 1px solid var(--line);
        display: block;
        padding: 8px 0;
    }

    tbody tr:last-child {
        border-bottom: 0;
    }

    tbody tr:hover td {
        background: none;
    }

    td {
        border: 0;
        display: flex;
        gap: 14px;
        justify-content: space-between;
        padding: 6px 4px;
        text-align: right;
    }

    td::before {
        color: var(--sub);
        content: attr(data-label);
        flex: 0 0 auto;
        font-size: 0.6875rem;
        font-weight: 700;
        letter-spacing: 0.06em;
        text-align: left;
        text-transform: uppercase;
    }

    td:not([data-label])::before {
        content: none;
    }

    .chip-row,
    .row-actions {
        justify-content: flex-end;
    }

    .actions-column {
        min-width: 0;
    }
}

@media (prefers-reduced-motion: reduce) {
    *,
    *::before,
    *::after {
        animation-duration: 0.001ms !important;
        animation-iteration-count: 1 !important;
        transition-duration: 0.001ms !important;
    }
}
`````

## before — src/EventBooking.Web/wwwroot/index.html — 1/1

<!-- vocabulary-file: {"id":219,"oldPath":"src/EventBooking.Web/wwwroot/index.html","newPath":"src/EventBooking.Web/wwwroot/index.html","beforeSha":"11378e4d05de103247003d41cc6c7a2cbf7de6d3e35127808d46cf5022a94ba8","afterSha":"aaaef2f40cf2a3b4e51385b9a72e05cb9324d74422c74301435ccea482f5c034","side":"before","part":1,"parts":1} -->

`````text
<!DOCTYPE html>
<html lang="en-GB">

<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <meta name="theme-color" content="#3468ad" />
    <meta name="description" content="British Airways EventBooking — coordinate candidate appointments across drug and alcohol testing, medical check-ups and uniform fittings." />
    <title>EventBooking · British Airways</title>
    <base href="/" />
    <link rel="preload" id="webassembly" />
    <link rel="stylesheet" href="css/fonts.css" />
    <link rel="stylesheet" href="lib/bootstrap/dist/css/bootstrap.min.css" />
    <link rel="stylesheet" href="css/app.css" />
    <link rel="icon" type="image/png" href="favicon.png" />
    <link href="EventBooking.Web.styles.css" rel="stylesheet" />
    <script type="importmap"></script>
</head>

<body>
    <div id="app">
        <div class="loading-splash">
            <img class="brand-mark" src="speedmarque.png" alt="British Airways" />
            <div class="loading-splash-title">
                <span>British Airways</span>
                EventBooking
            </div>
            <svg class="loading-progress">
                <circle r="40%" cx="50%" cy="50%" />
                <circle r="40%" cx="50%" cy="50%" />
            </svg>
            <div class="loading-progress-text"></div>
        </div>
    </div>

    <div id="blazor-error-ui">
        An unhandled error has occurred.
        <a href="." class="reload">Reload</a>
        <span class="dismiss">🗙</span>
    </div>
    <script src="js/download.js"></script>
    <script src="_framework/blazor.webassembly#[.{fingerprint}].js" autostart="false"></script>
    <script src="_content/Microsoft.AspNetCore.Components.WebAssembly.Authentication/AuthenticationService.js"></script>
    <script>Blazor.start();</script>
</body>

</html>
`````

## after — src/EventBooking.Web/wwwroot/index.html — 1/1

<!-- vocabulary-file: {"id":219,"oldPath":"src/EventBooking.Web/wwwroot/index.html","newPath":"src/EventBooking.Web/wwwroot/index.html","beforeSha":"11378e4d05de103247003d41cc6c7a2cbf7de6d3e35127808d46cf5022a94ba8","afterSha":"aaaef2f40cf2a3b4e51385b9a72e05cb9324d74422c74301435ccea482f5c034","side":"after","part":1,"parts":1} -->

`````text
<!DOCTYPE html>
<html lang="en-GB">

<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <meta name="theme-color" content="#3468ad" />
    <meta name="description" content="British Airways EventBooking — coordinate attendee appointments across drug and alcohol testing, medical check-ups and uniform fittings." />
    <title>EventBooking · British Airways</title>
    <base href="/" />
    <link rel="preload" id="webassembly" />
    <link rel="stylesheet" href="css/fonts.css" />
    <link rel="stylesheet" href="lib/bootstrap/dist/css/bootstrap.min.css" />
    <link rel="stylesheet" href="css/app.css" />
    <link rel="icon" type="image/png" href="favicon.png" />
    <link href="EventBooking.Web.styles.css" rel="stylesheet" />
    <script type="importmap"></script>
</head>

<body>
    <div id="app">
        <div class="loading-splash">
            <img class="brand-mark" src="speedmarque.png" alt="British Airways" />
            <div class="loading-splash-title">
                <span>British Airways</span>
                EventBooking
            </div>
            <svg class="loading-progress">
                <circle r="40%" cx="50%" cy="50%" />
                <circle r="40%" cx="50%" cy="50%" />
            </svg>
            <div class="loading-progress-text"></div>
        </div>
    </div>

    <div id="blazor-error-ui">
        An unhandled error has occurred.
        <a href="." class="reload">Reload</a>
        <span class="dismiss">🗙</span>
    </div>
    <script src="js/download.js"></script>
    <script src="_framework/blazor.webassembly#[.{fingerprint}].js" autostart="false"></script>
    <script src="_content/Microsoft.AspNetCore.Components.WebAssembly.Authentication/AuthenticationService.js"></script>
    <script>Blazor.start();</script>
</body>

</html>
`````

## before — tests/EventBooking.Api.Tests/AgentOperationCatalogTests.cs — 1/1

<!-- vocabulary-file: {"id":220,"oldPath":"tests/EventBooking.Api.Tests/AgentOperationCatalogTests.cs","newPath":"tests/EventBooking.Api.Tests/AgentOperationCatalogTests.cs","beforeSha":"f4e96fb3879143d4dcc6c1cd2e14d75745e6a929d2fd86e6d3e34ce89fd699c4","afterSha":"ec7ed4adf189ffa7315333024206ba2761abea00739702f2940774faa2c5bd81","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Api.OpenApi;

namespace EventBooking.Api.Tests;

/// <summary>Locks operation identity and MCP parity invariants before endpoint decoration.</summary>
public sealed class AgentOperationCatalogTests
{
    [Fact]
    public void CatalogHasUniqueCompleteEntries()
    {
        var operations = AgentOperationCatalog.All.Values.ToList();
        Assert.NotEmpty(operations);
        Assert.Equal(operations.Count, operations.Select(x => x.OperationId).Distinct().Count());
        Assert.Equal(operations.Count, operations.Select(x => $"{x.Method} {x.Route}").Distinct().Count());
        Assert.All(operations, operation =>
        {
            Assert.Matches("^[a-z][A-Za-z0-9]+$", operation.OperationId);
            Assert.True((operation.McpTool is null) ^ (operation.ExclusionReason is null));
            Assert.False(operation.Hints.OpenWorld);
        });
        var staff = operations.Where(x => x.McpTool is not null).ToList();
        Assert.Equal(36, staff.Count);
        Assert.Equal(staff.Count, staff.Select(x => x.McpTool).Distinct().Count());
    }

    [Fact]
    public void CatalogContainsTheEightNewParityMappings()
    {
        var names = AgentOperationCatalog.All.Values.Select(x => x.McpTool).ToHashSet();
        Assert.Contains("get_slot_operations", names);
        Assert.Contains("start_recovery_invite", names);
        Assert.Contains("cancel_recovery_invite", names);
        Assert.Contains("list_candidate_bookings", names);
        Assert.Contains("cancel_candidate_booking", names);
        Assert.Contains("get_candidate_readiness", names);
        Assert.Contains("search_audit", names);
        Assert.Contains("export_appointment_roster", names);
    }
}
`````

## after — tests/EventBooking.Api.Tests/AgentOperationCatalogTests.cs — 1/1

<!-- vocabulary-file: {"id":220,"oldPath":"tests/EventBooking.Api.Tests/AgentOperationCatalogTests.cs","newPath":"tests/EventBooking.Api.Tests/AgentOperationCatalogTests.cs","beforeSha":"f4e96fb3879143d4dcc6c1cd2e14d75745e6a929d2fd86e6d3e34ce89fd699c4","afterSha":"ec7ed4adf189ffa7315333024206ba2761abea00739702f2940774faa2c5bd81","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Api.OpenApi;

namespace EventBooking.Api.Tests;

/// <summary>Locks operation identity and MCP parity invariants before endpoint decoration.</summary>
public sealed class AgentOperationCatalogTests
{
    [Fact]
    public void CatalogHasUniqueCompleteEntries()
    {
        var operations = AgentOperationCatalog.All.Values.ToList();
        Assert.NotEmpty(operations);
        Assert.Equal(operations.Count, operations.Select(x => x.OperationId).Distinct().Count());
        Assert.Equal(operations.Count, operations.Select(x => $"{x.Method} {x.Route}").Distinct().Count());
        Assert.All(operations, operation =>
        {
            Assert.Matches("^[a-z][A-Za-z0-9]+$", operation.OperationId);
            Assert.True((operation.McpTool is null) ^ (operation.ExclusionReason is null));
            Assert.False(operation.Hints.OpenWorld);
        });
        var staff = operations.Where(x => x.McpTool is not null).ToList();
        Assert.Equal(36, staff.Count);
        Assert.Equal(staff.Count, staff.Select(x => x.McpTool).Distinct().Count());
    }

    [Fact]
    public void CatalogContainsTheEightNewParityMappings()
    {
        var names = AgentOperationCatalog.All.Values.Select(x => x.McpTool).ToHashSet();
        Assert.Contains("get_event_operations", names);
        Assert.Contains("start_recovery_invite", names);
        Assert.Contains("cancel_recovery_invite", names);
        Assert.Contains("list_attendee_bookings", names);
        Assert.Contains("cancel_attendee_booking", names);
        Assert.Contains("get_attendee_readiness", names);
        Assert.Contains("search_audit", names);
        Assert.Contains("export_appointment_roster", names);
    }
}
`````

## before — tests/EventBooking.Api.Tests/ApiDiscoveryTests.cs — 1/1

<!-- vocabulary-file: {"id":221,"oldPath":"tests/EventBooking.Api.Tests/ApiDiscoveryTests.cs","newPath":"tests/EventBooking.Api.Tests/ApiDiscoveryTests.cs","beforeSha":"02c7c533a81b2ecd20ea6b3bbfbca943eb9f3a66a96f6652b6b27ff414bb08fc","afterSha":"e7952b683ef3aec80a30a298dab781755bb7830a1f38192d28163030a32fcac2","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Text.Json;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class ApiDiscoveryTests(ApiFactory factory)
{
    [Fact]
    public async Task ApiRootIsAnonymousAndCarriesStableLinks()
    {
        factory.SignedInAs = null;
        using var response = await factory.CreateClient().GetAsync("/api");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("EventBooking API", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("v1", json.RootElement.GetProperty("version").GetString());
        var links = json.RootElement.GetProperty("_links");
        AssertLink(links, "self", "/api", "GET", "getApiIndex");
        AssertLink(links, "openapi", "/openapi/v1.json", "GET", "getOpenApiDocument");
        AssertLink(links, "swagger", "/swagger", "GET", "getSwaggerUi");
        AssertLink(links, "me", "/api/me", "GET", "getMyAccess");
        AssertLink(links, "candidates", "/api/candidates", "GET", "listCandidates");
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

## after — tests/EventBooking.Api.Tests/ApiDiscoveryTests.cs — 1/1

<!-- vocabulary-file: {"id":221,"oldPath":"tests/EventBooking.Api.Tests/ApiDiscoveryTests.cs","newPath":"tests/EventBooking.Api.Tests/ApiDiscoveryTests.cs","beforeSha":"02c7c533a81b2ecd20ea6b3bbfbca943eb9f3a66a96f6652b6b27ff414bb08fc","afterSha":"e7952b683ef3aec80a30a298dab781755bb7830a1f38192d28163030a32fcac2","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Text.Json;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class ApiDiscoveryTests(ApiFactory factory)
{
    [Fact]
    public async Task ApiRootIsAnonymousAndCarriesStableLinks()
    {
        factory.SignedInAs = null;
        using var response = await factory.CreateClient().GetAsync("/api");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("EventBooking API", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("v1", json.RootElement.GetProperty("version").GetString());
        var links = json.RootElement.GetProperty("_links");
        AssertLink(links, "self", "/api", "GET", "getApiIndex");
        AssertLink(links, "openapi", "/openapi/v1.json", "GET", "getOpenApiDocument");
        AssertLink(links, "swagger", "/swagger", "GET", "getSwaggerUi");
        AssertLink(links, "me", "/api/me", "GET", "getMyAccess");
        AssertLink(links, "attendees", "/api/attendees", "GET", "listAttendees");
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

## before — tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":222,"oldPath":"tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs","beforeSha":"3d92c46770ca227dc36f8145e31d97d47a38e5998a6df1648b0235343422f4f8","afterSha":"152544c4496439bd6018f1b047ce3c2daad68ebf9fc2ccaa7a672f41bbfd9f1d","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":222,"oldPath":"tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs","beforeSha":"3d92c46770ca227dc36f8145e31d97d47a38e5998a6df1648b0235343422f4f8","afterSha":"152544c4496439bd6018f1b047ce3c2daad68ebf9fc2ccaa7a672f41bbfd9f1d","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
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
    public async Task RoleMatrixProtectsTheEventList(Role[] roles, HttpStatusCode expected)
    {
        Guid? scope = roles.Any(role => role is Role.Manager or Role.AppointmentStaff)
            ? AppointmentTypeIds.DrugAndAlcoholTesting
            : null;
        factory.SignedInAs = await factory.GivenStaffAsync(roles, scope);

        using var response = await factory.CreateClient()
            .GetAsync("/api/appointment-workspace/events");

        Assert.Equal(expected, response.StatusCode);
    }

    /// <summary>Verifies anonymous and unassigned identities receive no workspace data.</summary>
    [Fact]
    public async Task AnonymousAndUnassignedCallersAreRejected()
    {
        factory.SignedInAs = null;
        using var anonymous = await factory.CreateClient()
            .GetAsync("/api/appointment-workspace/events");
        factory.SignedInAs = Guid.NewGuid();
        using var unassigned = await factory.CreateClient()
            .GetAsync("/api/appointment-workspace/events");

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
            "/api/appointment-workspace/events"));
        AssertKeys(list.RootElement, "appointmentTypeName", "events", "_links");
        var eventItem = Assert.Single(
            list.RootElement.GetProperty("events").EnumerateArray(),
            item => item.GetProperty("eventId").GetString() == data.EventId.ToString());
        AssertKeys(eventItem, "eventId", "date", "startTime", "endTime", "counts", "_links");
        AssertKeys(eventItem.GetProperty("counts"), "expected", "checkedIn", "completed", "noShow");

        using var detail = JsonDocument.Parse(await client.GetStringAsync(
            $"/api/appointment-workspace/events/{data.EventId}"));
        AssertKeys(detail.RootElement,
            "appointmentTypeName", "eventId", "date", "startTime", "endTime", "appointments", "_links");
        var row = Assert.Single(detail.RootElement.GetProperty("appointments").EnumerateArray());
        AssertKeys(row, "bookingAppointmentId", "attendeeName", "attendeeEmail",
            "status", "checkedInAt", "outcomeAt", "version", "_links");
        Assert.Equal("Expected", row.GetProperty("status").GetString());
        Assert.DoesNotContain("attendeeId", detail.RootElement.GetRawText());
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

    /// <summary>Seeds one active eventItem, attendee, booking, and scoped appointment row.</summary>

    /// <summary>Verifies the roster route is protected by the same role matrix as the event list.</summary>
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
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster");

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
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster");

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
            "Attendee Name,Attendee Email,Appointment Type,Status,Checked In At,Outcome At",
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
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster");

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(data.AppointmentId.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Version", body, StringComparison.Ordinal);
    }

    /// <summary>Verifies a caller scoped to another appointment type cannot learn the event exists.</summary>
    [Fact]
    public async Task RosterCrossTypeRequestReturnsNotFound()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.UniformFitting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies an unknown eventItem is refused the same way the JSON detail route refuses it.</summary>
    [Fact]
    public async Task RosterMissingEventReturnsNotFound()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{Guid.NewGuid()}/roster");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies an anonymous caller is challenged before any attendee data is read.</summary>
    [Fact]
    public async Task RosterAnonymousCallerIsUnauthorized()
    {
        factory.SignedInAs = null;

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{Guid.NewGuid()}/roster");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Verifies a free-text attendee name with a comma and a quote survives the CSV.</summary>
    [Fact]
    public async Task RosterEscapesCommaAndQuoteInAttendeeName()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, "Okafor, Ada \"Bisi\"");
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster");

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
            .GetAsync($"/api/appointment-workspace/events/{inScope.EventId}/roster");
        using var refused = await client
            .GetAsync($"/api/appointment-workspace/events/{outOfScope.EventId}/roster");

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
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<(Guid EventId, Guid AppointmentId)> GivenWorkspaceAsync(
        Guid appointmentTypeId,
        string attendeeName = "Alex Morgan")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;
        var eventItem = Event.CreateImported(
            Guid.NewGuid(), new EventWindow(today, new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var groupId = appointmentTypeId == AppointmentTypeIds.MedicalCheckUp
            ? AttendeeGroupIds.GroundOperationsAgent
            : AttendeeGroupIds.Pilots;
        var group = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == groupId);
        var attendee = Attendee.Create(
            Guid.NewGuid(), attendeeName, $"alex-{Guid.NewGuid():N}@example.com", group);
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
