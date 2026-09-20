# 00d — Retire direct event import, edits 9 (Task 3b)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — src/EventBooking.Web/Pages/EventOperations.razor — 1/1

<!-- retirement-file: {"id":22,"file":"src/EventBooking.Web/Pages/EventOperations.razor","beforeSha":"f7043ae4369a1e0e8124d04d93c96fe033a3afe32efcba9e1477eb5409078487","afterSha":"c2cc5dd945fe9fa1f78184a261016cfd8b8ab57bf7eac70e0fea507a284c3373","side":"after","part":1,"parts":1} -->

`````text
@page "/events/operations"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject EventsClient EventsApi

<PageTitle>Events</PageTitle>

<section class="page events-page" aria-labelledby="events-heading" aria-busy="@(_busy ? "true" : "false")">
    <div class="page-header">
        <div>
            <span class="eyebrow">Operations</span>
            <h1 id="events-heading">Events</h1>
            <p>Review confirmed events and cancel an event when necessary.</p>
        </div>
    </div>

    <div class="card">
        <div class="card-heading">
            <h2>
                Cancel a event
                <span class="tip" tabindex="0" role="note"
                      aria-label="Cancelling a window releases every place it holds. Any attendee booked into it is notified and re-invited."
                      data-tip="Cancelling a window releases every place it holds. Any attendee booked into it is notified and re-invited."></span>
            </h2>
        </div>
        <div class="card-body">
            @if (_eventsLoading)
            {
                <p class="hint" role="status">Loading events…</p>
            }
            else if (_events is null || _events.Count == 0)
            {
                <p class="hint" role="status">No events yet.</p>
            }
            else
            {
                <div class="table-wrap">
                    <table id="event-operations">
                        <thead>
                            <tr>
                                <th scope="col">Date</th>
                                <th scope="col">Window</th>
                                <th scope="col" title="Places left over the total headcount each appointment type accepted.">Capacity by type</th>
                                <th scope="col" title="Attendees currently booked into this window.">Active bookings</th>
                                <th scope="col" class="actions-column">Cancel</th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var eventItem in _events)
                            {
                                <tr @key="eventItem.EventId">
                                    <td data-label="Date">@eventItem.Date.ToString("yyyy-MM-dd")</td>
                                    <td data-label="Window">@eventItem.StartTime.ToString("HH\\:mm")–@eventItem.EndTime.ToString("HH\\:mm")</td>
                                    <td data-label="Capacity by type">
                                        <div class="chip-row">
                                            @foreach (var capacity in eventItem.Capacities)
                                            {
                                                <span class="chip">@capacity.Code @capacity.RemainingCapacity/@capacity.TotalHeadcount</span>
                                            }
                                        </div>
                                    </td>
                                    <td data-label="Active bookings">@eventItem.ActiveBookings</td>
                                    <td data-label="Cancel">
                                        <button class="button button-danger button-small"
                                                @onclick="() => CancelEventAsync(eventItem.EventId)" disabled="@_busy">
                                            @(_cancelAwaitingConfirmation == eventItem.EventId ? "Confirm cancel" : "Cancel event")
                                        </button>
                                    </td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            }

            @if (_eventsError is not null)
            {
                <p class="banner error" role="alert">@_eventsError</p>
            }
        </div>
    </div>
</section>

@code {
    private bool _busy;

    private List<EventOperationDto>? _events;
    private bool _eventsLoading = true;
    private string? _eventsError;
    private Guid? _cancelAwaitingConfirmation;

    protected override Task OnInitializedAsync() => ReloadEventsAsync();

    internal Task ReloadEventsForTestingAsync() => ReloadEventsAsync();

    internal Task CancelEventForTestingAsync(Guid eventId) => CancelEventAsync(eventId);

    private async Task ReloadEventsAsync()
    {
        _eventsLoading = true;
        try
        {
            var outcome = await EventsApi.GetEventOperationsAsync(CancellationToken.None);
            if (!outcome.IsSuccess || outcome.Value is null)
            {
                _eventsError = outcome.ErrorMessage ?? "Something went wrong. Please try again.";
                return;
            }

            _events = outcome.Value.Events.ToList();
            _eventsError = null;
        }
        catch (Exception)
        {
            _eventsError = "Something went wrong. Please try again.";
        }
        finally
        {
            _eventsLoading = false;
            StateHasChanged();
        }
    }

    // Two-stage: the first click asks without authorizing the cascade, so a event holding bookings
    // comes back 409 and the button becomes the confirmation.
    private async Task CancelEventAsync(Guid eventId)
    {
        if (_busy)
        {
            return;
        }

        var confirm = _cancelAwaitingConfirmation == eventId;
        _busy = true;
        try
        {
            var outcome = await EventsApi.CancelEventAsync(eventId, confirm, CancellationToken.None);
            if (!outcome.IsSuccess && outcome.StatusCode == 409 && !confirm)
            {
                _cancelAwaitingConfirmation = eventId;
                _eventsError = $"{outcome.ErrorMessage} Press Confirm cancel to proceed.";
                return;
            }

            _cancelAwaitingConfirmation = null;
            _eventsError = outcome.ErrorMessage;
            if (outcome.IsSuccess)
            {
                await ReloadEventsAsync();
            }
        }
        catch (Exception)
        {
            _eventsError = "Something went wrong. Please try again.";
        }
        finally
        {
            _busy = false;
            StateHasChanged();
        }
    }

}
`````

## before — src/EventBooking.Web/Program.cs — 1/1

<!-- retirement-file: {"id":23,"file":"src/EventBooking.Web/Program.cs","beforeSha":"bfe363f47b4468b4f2fcd06fb4365eb0aa79800f33ba5a94d9352930c85b459c","afterSha":"4dd856e344d8ce43f48b4de58e236c814761933adeed0ffc951b8037fd2bec97","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl is not configured.");
var transitionalLocationTimeZoneId = builder.Configuration["TransitionalLocationTimeZoneId"]
    ?? throw new InvalidOperationException("TransitionalLocationTimeZoneId is not configured.");

string[] tokenScopes;
var authority = builder.Configuration["Auth:Local:Authority"]
    ?? throw new InvalidOperationException("Auth:Local:Authority is not configured.");
var clientId = builder.Configuration["Auth:Local:ClientId"] ?? "eventbooking-web";

builder.Services.AddOidcAuthentication(options =>
{
    options.ProviderOptions.Authority = authority;
    options.ProviderOptions.ClientId = clientId;
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.PostLogoutRedirectUri =
        builder.HostEnvironment.BaseAddress.TrimEnd('/') + "/authentication/logout-callback";
    options.ProviderOptions.DefaultScopes.Clear();
    options.ProviderOptions.DefaultScopes.Add("openid");
    options.ProviderOptions.DefaultScopes.Add("profile");
});

tokenScopes = ["openid", "profile"];

// The authorization message handler attaches the staff access token to every call to the API,
// and only to the API. It is a delegating handler with no transport of its own, so it must
// wrap the browser fetch handler explicitly — without an inner handler every call throws
// net_http_handler_not_assigned before leaving the page.
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthorizationMessageHandler>();
    handler.InnerHandler = new HttpClientHandler();
    handler.ConfigureHandler([apiBaseUrl], tokenScopes);

    return new HttpClient(handler) { BaseAddress = new Uri(apiBaseUrl) };
});

builder.Services.AddScoped<EventBooking.Web.Services.EventsClient>();
builder.Services.AddScoped<EventBooking.Web.Services.AttendeesClient>();
builder.Services.AddScoped<EventBooking.Web.Services.AdminClient>();
builder.Services.AddScoped<EventBooking.Web.Services.StaffAccessClient>();
builder.Services.AddScoped<EventBooking.Web.Services.EventOperationsClient>();
builder.Services.AddScoped<EventBooking.Web.Services.DashboardsClient>();
builder.Services.AddScoped<EventBooking.Web.Services.AuditClient>();
builder.Services.AddScoped<EventBooking.Web.Services.MeClient>();
builder.Services.AddScoped<EventBooking.Web.Services.AppointmentsClient>();
builder.Services.AddSingleton(new EventBooking.Web.Services.TransitionalLocationTimePresentation(transitionalLocationTimeZoneId));
builder.Services.AddSingleton(new EventBooking.Web.Services.TransitionalLocationPageClock(transitionalLocationTimeZoneId));

// Attendees authorise with the single-use token in their URL. This plain named client must never
// use AuthorizationMessageHandler, which would attach a staff access token and start sign-in.
builder.Services.AddHttpClient(EventBooking.Web.Services.BookingClient.ClientName, client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});
builder.Services.AddScoped(sp => new EventBooking.Web.Services.BookingClient(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient(EventBooking.Web.Services.BookingClient.ClientName)));
builder.Services.AddSingleton(new EventBooking.Web.Services.AttendeePageOptions(
    builder.Configuration["CoordinatorContact"] ?? "the recruitment team"));

await builder.Build().RunAsync();
`````

## after — src/EventBooking.Web/Program.cs — 1/1

<!-- retirement-file: {"id":23,"file":"src/EventBooking.Web/Program.cs","beforeSha":"bfe363f47b4468b4f2fcd06fb4365eb0aa79800f33ba5a94d9352930c85b459c","afterSha":"4dd856e344d8ce43f48b4de58e236c814761933adeed0ffc951b8037fd2bec97","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl is not configured.");
var transitionalLocationTimeZoneId = builder.Configuration["TransitionalLocationTimeZoneId"]
    ?? throw new InvalidOperationException("TransitionalLocationTimeZoneId is not configured.");

string[] tokenScopes;
var authority = builder.Configuration["Auth:Local:Authority"]
    ?? throw new InvalidOperationException("Auth:Local:Authority is not configured.");
var clientId = builder.Configuration["Auth:Local:ClientId"] ?? "eventbooking-web";

builder.Services.AddOidcAuthentication(options =>
{
    options.ProviderOptions.Authority = authority;
    options.ProviderOptions.ClientId = clientId;
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.PostLogoutRedirectUri =
        builder.HostEnvironment.BaseAddress.TrimEnd('/') + "/authentication/logout-callback";
    options.ProviderOptions.DefaultScopes.Clear();
    options.ProviderOptions.DefaultScopes.Add("openid");
    options.ProviderOptions.DefaultScopes.Add("profile");
});

tokenScopes = ["openid", "profile"];

// The authorization message handler attaches the staff access token to every call to the API,
// and only to the API. It is a delegating handler with no transport of its own, so it must
// wrap the browser fetch handler explicitly — without an inner handler every call throws
// net_http_handler_not_assigned before leaving the page.
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthorizationMessageHandler>();
    handler.InnerHandler = new HttpClientHandler();
    handler.ConfigureHandler([apiBaseUrl], tokenScopes);

    return new HttpClient(handler) { BaseAddress = new Uri(apiBaseUrl) };
});

builder.Services.AddScoped<EventBooking.Web.Services.EventsClient>();
builder.Services.AddScoped<EventBooking.Web.Services.AttendeesClient>();
builder.Services.AddScoped<EventBooking.Web.Services.AdminClient>();
builder.Services.AddScoped<EventBooking.Web.Services.StaffAccessClient>();
builder.Services.AddScoped<EventBooking.Web.Services.DashboardsClient>();
builder.Services.AddScoped<EventBooking.Web.Services.AuditClient>();
builder.Services.AddScoped<EventBooking.Web.Services.MeClient>();
builder.Services.AddScoped<EventBooking.Web.Services.AppointmentsClient>();
builder.Services.AddSingleton(new EventBooking.Web.Services.TransitionalLocationTimePresentation(transitionalLocationTimeZoneId));
builder.Services.AddSingleton(new EventBooking.Web.Services.TransitionalLocationPageClock(transitionalLocationTimeZoneId));

// Attendees authorise with the single-use token in their URL. This plain named client must never
// use AuthorizationMessageHandler, which would attach a staff access token and start sign-in.
builder.Services.AddHttpClient(EventBooking.Web.Services.BookingClient.ClientName, client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});
builder.Services.AddScoped(sp => new EventBooking.Web.Services.BookingClient(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient(EventBooking.Web.Services.BookingClient.ClientName)));
builder.Services.AddSingleton(new EventBooking.Web.Services.AttendeePageOptions(
    builder.Configuration["CoordinatorContact"] ?? "the recruitment team"));

await builder.Build().RunAsync();
`````

## before — src/EventBooking.Web/Services/EventOperationsClient.cs — 1/1

<!-- retirement-file: {"id":24,"file":"src/EventBooking.Web/Services/EventOperationsClient.cs","beforeSha":"edb776e0de9414fcdf661460c0ce44ea365dee98e2d985d2366289df5c6f5d10","afterSha":null,"side":"before","part":1,"parts":1} -->

`````csharp
using System.Text;

namespace EventBooking.Web.Services;

public sealed record EventImportErrorDto(int LineNumber, string Message);

public sealed record EventImportOutcomeDto(
    bool Accepted,
    int ImportedCount,
    IReadOnlyList<EventImportErrorDto> Errors);

public sealed class EventOperationsClient(HttpClient http)
{
    public async Task<ApiOutcome<EventImportOutcomeDto>> ImportAsync(
        string csv,
        CancellationToken cancellationToken)
    {
        using var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        using var response = await http.PostAsync(
            "/api/events/import", content, cancellationToken);
        return await ApiCall.ReadAsync<EventImportOutcomeDto>(response, cancellationToken);
    }
}
`````

## before — src/EventBooking.Web/Services/StaffNavigation.cs — 1/1

<!-- retirement-file: {"id":25,"file":"src/EventBooking.Web/Services/StaffNavigation.cs","beforeSha":"209160387fc9ae8e479407926e51a903f380b1aea0e8a48acef160e112280cb8","afterSha":"2bd5654b42c287dc4932b7c789ba0d76cbc83be53138b843d6b2f6a56beac71f","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Web.Services;

/// <summary>Describes one staff navigation destination shown for a role combination.</summary>
/// <param name="Href">The link target shown for the permitted role combination.</param>
/// <param name="Label">The visible link text naming the permitted workspace.</param>
/// <param name="Description">The accessible description of what the workspace offers.</param>
public sealed record StaffLink(string Href, string Label, string Description);

/// <summary>Builds the deterministic navigation union permitted by staff roles.</summary>
public static class StaffNavigation
{
    /// <summary>Builds the deterministic union of links permitted by the caller's roles.</summary>
    /// <param name="me">The authenticated staff identity with its assigned roles.</param>
    /// <returns>The ordered links the caller is permitted to open.</returns>
    public static IReadOnlyList<StaffLink> LinksFor(MeDto me)
    {
        var roles = me.Roles.ToHashSet(StringComparer.Ordinal);
        if (roles.Contains("Admin"))
        {
            return
            [
                new("/settings", "System settings", "Configure invitation timing"),
                new("/staff-access", "Staff access", "Set appointment-type scope"),
                new("/events/operations", "Events", "Import already agreed events"),
                new("/audit", "Audit trail", "Search what changed"),
            ];
        }

        var links = new List<StaffLink>();
        if (roles.Contains("Manager"))
        {
            links.Add(new("/events/negotiate", "Event proposals", "Negotiate and confirm shared windows"));
        }

        if (roles.Contains("Manager") || roles.Contains("AppointmentStaff"))
        {
            links.Add(new(
                "/appointments",
                "Appointments",
                "Check attendees in and record appointment outcomes"));
        }

        if (roles.Contains("Coordinator"))
        {
            links.Add(new("/attendees", "Attendees", "Invite and track attendees"));
            links.Add(new("/dashboards", "Dashboards", "Waiting lists and follow-ups"));
            links.Add(new("/events/operations", "Events", "Import already agreed events"));
            links.Add(new("/audit", "Audit trail", "Search what changed"));
        }

        return links;
    }
}
`````

## after — src/EventBooking.Web/Services/StaffNavigation.cs — 1/1

<!-- retirement-file: {"id":25,"file":"src/EventBooking.Web/Services/StaffNavigation.cs","beforeSha":"209160387fc9ae8e479407926e51a903f380b1aea0e8a48acef160e112280cb8","afterSha":"2bd5654b42c287dc4932b7c789ba0d76cbc83be53138b843d6b2f6a56beac71f","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Web.Services;

/// <summary>Describes one staff navigation destination shown for a role combination.</summary>
/// <param name="Href">The link target shown for the permitted role combination.</param>
/// <param name="Label">The visible link text naming the permitted workspace.</param>
/// <param name="Description">The accessible description of what the workspace offers.</param>
public sealed record StaffLink(string Href, string Label, string Description);

/// <summary>Builds the deterministic navigation union permitted by staff roles.</summary>
public static class StaffNavigation
{
    /// <summary>Builds the deterministic union of links permitted by the caller's roles.</summary>
    /// <param name="me">The authenticated staff identity with its assigned roles.</param>
    /// <returns>The ordered links the caller is permitted to open.</returns>
    public static IReadOnlyList<StaffLink> LinksFor(MeDto me)
    {
        var roles = me.Roles.ToHashSet(StringComparer.Ordinal);
        if (roles.Contains("Admin"))
        {
            return
            [
                new("/settings", "System settings", "Configure invitation timing"),
                new("/staff-access", "Staff access", "Set appointment-type scope"),
                new("/events/operations", "Events", "Review and cancel confirmed events"),
                new("/audit", "Audit trail", "Search what changed"),
            ];
        }

        var links = new List<StaffLink>();
        if (roles.Contains("Manager"))
        {
            links.Add(new("/events/negotiate", "Event proposals", "Negotiate and confirm shared windows"));
        }

        if (roles.Contains("Manager") || roles.Contains("AppointmentStaff"))
        {
            links.Add(new(
                "/appointments",
                "Appointments",
                "Check attendees in and record appointment outcomes"));
        }

        if (roles.Contains("Coordinator"))
        {
            links.Add(new("/attendees", "Attendees", "Invite and track attendees"));
            links.Add(new("/dashboards", "Dashboards", "Waiting lists and follow-ups"));
            links.Add(new("/events/operations", "Events", "Review and cancel confirmed events"));
            links.Add(new("/audit", "Audit trail", "Search what changed"));
        }

        return links;
    }
}
`````

## before — tests/EventBooking.Api.Tests/AgentOperationCatalogTests.cs — 1/1

<!-- retirement-file: {"id":26,"file":"tests/EventBooking.Api.Tests/AgentOperationCatalogTests.cs","beforeSha":"ec7ed4adf189ffa7315333024206ba2761abea00739702f2940774faa2c5bd81","afterSha":"1a25ae7f878773bf2762fb013bc8a49d8fbee4709fad4483dbaa879fb296c83b","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Api.Tests/AgentOperationCatalogTests.cs — 1/1

<!-- retirement-file: {"id":26,"file":"tests/EventBooking.Api.Tests/AgentOperationCatalogTests.cs","beforeSha":"ec7ed4adf189ffa7315333024206ba2761abea00739702f2940774faa2c5bd81","afterSha":"1a25ae7f878773bf2762fb013bc8a49d8fbee4709fad4483dbaa879fb296c83b","side":"after","part":1,"parts":1} -->

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
        Assert.Equal(35, staff.Count);
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

## before — tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs — 1/1

<!-- retirement-file: {"id":27,"file":"tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs","beforeSha":"152544c4496439bd6018f1b047ce3c2daad68ebf9fc2ccaa7a672f41bbfd9f1d","afterSha":"833d9d227fb304d275d939d1cee167fe7a6f03b998f9bb49aa3c1804a53f8ae3","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs — 1/1

<!-- retirement-file: {"id":27,"file":"tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs","beforeSha":"152544c4496439bd6018f1b047ce3c2daad68ebf9fc2ccaa7a672f41bbfd9f1d","afterSha":"833d9d227fb304d275d939d1cee167fe7a6f03b998f9bb49aa3c1804a53f8ae3","side":"after","part":1,"parts":1} -->

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
        var eventItem = EventFixture.Create(
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
