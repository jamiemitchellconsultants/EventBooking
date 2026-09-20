# 00a — Port source 32 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.SeedData/Program.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.SeedData/Program.cs","encoding":"utf8","sha256":"beab2ba7e0d35297ab9ee41f035ffdfccd47235446dd91f1891a4495383c41fd","parts":1,"part":1} -->

`````csharp
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
using EventBooking.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var reseed = args.Contains("--reseed");
var skipSeed = args.Contains("--skip-seed");
var verbose = args.Contains("--verbose");
var reanchorIndex = args.ToList().IndexOf("--reanchor");
var reanchorRequested = reanchorIndex >= 0;
DateOnly? reanchorDate = null;
if (reanchorRequested
    && reanchorIndex + 1 < args.Length
    && DateOnly.TryParse(args[reanchorIndex + 1], out var parsedDate))
{
    reanchorDate = parsedDate;
}
var connectionString = args.FirstOrDefault(a => !a.StartsWith('-'))
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__EventBooking");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project src/EventBooking.SeedData -- \"<postgres connection string>\" [--reseed] [--skip-seed] [--verbose]");
    Console.Error.WriteLine(
        "   or set the ConnectionStrings__EventBooking environment variable.");
    Console.Error.WriteLine(
        "   Pending migrations are always applied first, whichever mode runs.");
    Console.Error.WriteLine(
        "   --reseed wipes every domain table first, then seeds fresh.");
    Console.Error.WriteLine(
        "   --skip-seed applies migrations only and seeds nothing.");
    Console.Error.WriteLine(
        "   --verbose reports per-step progress; failures print the full exception.");
    Console.Error.WriteLine(
        "   --reanchor [yyyy-MM-dd] resolves every day offset against the given date");
    Console.Error.WriteLine(
        "   (default: today at head office) instead of the file anchor, without editing");
    Console.Error.WriteLine(
        "   demo-seed.json. Use it when the file anchor has gone stale and proposals");
    Console.Error.WriteLine(
        "   land on today or earlier.");
    Console.Error.WriteLine(
        "   Keycloak demo users are converged when Keycloak__BaseUrl, Keycloak__Realm,");
    Console.Error.WriteLine(
        "   Keycloak__AdminRealm, Keycloak__AdminUsername, Keycloak__AdminPassword, and");
    Console.Error.WriteLine(
        "   Keycloak__DemoPassword are set; otherwise only the database is seeded.");
    Console.Error.WriteLine(
        "   --reseed also deletes and recreates the Keycloak realm from the file named by");
    Console.Error.WriteLine(
        "   Keycloak__RealmExportPath, when Keycloak settings are configured.");
    Console.Error.WriteLine("   Normal seed/reseed sends five demo invitations through Mailpit SMTP.");
    Console.Error.WriteLine("   Local defaults: Portal__BaseUrl=http://localhost:5002, Email__Smtp__Host=localhost,");
    Console.Error.WriteLine("   Email__Smtp__Port=1025 and the local API's development token key.");
    Console.Error.WriteLine("   Non-local portals require explicit Tokens__SigningKey and Email__Smtp__Host.");
    Console.Error.WriteLine("   Match Tokens__SigningKey and Portal__BaseUrl to the running API.");
    Console.Error.WriteLine("   --skip-seed does not read email settings or send any messages.");
    return 2;
}

try
{
    var services = new ServiceCollection();
    services.AddLogging();
    if (skipSeed)
    {
        // The persistence interceptor still needs IClock, even for migration-only context creation.
        services.AddEventBookingPersistence(connectionString);
        services.AddSingleton(new HeadOfficeOptions("Europe/London"));
        services.AddSingleton<IClock, SystemClock>();
    }
    else
    {
        var email = DemoEmailOptions.From(Environment.GetEnvironmentVariable);
        services.AddEventBookingInfrastructure(connectionString, email.HeadOffice, email.Tokens);
        services.AddEventBookingApplication(email.Portal);
        services.AddLocalEmailTransport(email.Sender, email.Smtp);
        services.AddScoped<DemoSeeder>();
        services.AddScoped<DemoInvitationSeeder>();
    }
    using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
    if (verbose)
    {
        Console.WriteLine("[seed] Applying pending migrations...");
    }

    await database.Database.MigrateAsync();
    if (verbose)
    {
        Console.WriteLine("[seed] Migrations applied.");
    }

    if (skipSeed)
    {
        Console.WriteLine("Migrations applied. Skipping seed data (--skip-seed).");
        return 0;
    }

    var keycloakStep = new KeycloakSeedStep(
        Environment.GetEnvironmentVariable,
        static () => new HttpClient());
    if (reseed && verbose)
    {
        Console.WriteLine("[seed] Reseed requested: the Keycloak realm will be deleted and recreated first, if configured.");
    }

    var keycloakSummary = await keycloakStep.RunAsync(
        skipSeed, reseed, DemoSeedSpec.Staff(), CancellationToken.None);
    if (keycloakSummary is not null)
    {
        if (verbose)
        {
            Console.WriteLine(reseed
                ? "[seed] Keycloak realm reset and convergence complete."
                : "[seed] Keycloak convergence complete.");
        }

        Console.WriteLine(
            $"Keycloak seed complete: {keycloakSummary.RolesCreated} roles created, " +
            $"{keycloakSummary.MapperWrites} mapper writes, " +
            $"{keycloakSummary.UsersCreated} users created, " +
            $"{keycloakSummary.RoleMappingWrites} role-mapping writes.");
    }
    else if (verbose)
    {
        Console.WriteLine("[seed] Keycloak provider seed skipped.");
    }

    if (reanchorRequested)
    {
        reanchorDate ??= scope.ServiceProvider
            .GetRequiredService<IClock>()
            .TodayAtHeadOffice;
        DemoSeedSpec.OverrideAnchor(reanchorDate.Value);
        Console.WriteLine($"[seed] Reanchored to {reanchorDate:yyyy-MM-dd}.");
    }

    var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
    if (verbose)
    {
        seeder.Progress = Console.Out;
    }

    var summary = reseed
        ? await seeder.ReseedAsync(CancellationToken.None)
        : await seeder.RunAsync(CancellationToken.None);

    var invitations = scope.ServiceProvider.GetRequiredService<DemoInvitationSeeder>();
    if (verbose) invitations.Progress = Console.Out;
    var invitationEmailsSent = await invitations.RunAsync(CancellationToken.None);

    Console.WriteLine(
        $"Migrations applied. " +
        $"{(reseed ? "Reseed complete (database was cleared): " : "Seed complete: ")}" +
        $"{summary.IdentitiesEnsured} identities, " +
        $"{summary.ProfilesEnsured} profiles, " +
        $"{summary.AgreedSlotsImported} agreed slots, " +
        $"{summary.ProposalsEnsured} proposals, " +
        $"{summary.AcceptancesApplied} acceptances, " +
        $"{summary.CandidatesCreated} candidates; " +
        $"{invitationEmailsSent} invitation emails sent or retried.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(verbose ? $"Seed failed: {ex}" : $"Seed failed: {ex.Message}");
    return 2;
}
`````

## src/EventBooking.Web/_Imports.razor — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/_Imports.razor","encoding":"utf8","sha256":"b5dae9beb162220a9cd501814874ba016fbaaec3fd22ee093d757fbea3882cc4","parts":1,"part":1} -->

`````razor
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Web.Virtualization
@using Microsoft.AspNetCore.Components.WebAssembly.Http
@using Microsoft.JSInterop
@using EventBooking.Web
@using EventBooking.Web.Layout
@using EventBooking.Web.Shared
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.AspNetCore.Components.WebAssembly.Authentication
`````

## src/EventBooking.Web/.npmrc — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/.npmrc","encoding":"utf8","sha256":"2491809e5df181ef4b8ef82bcef63850365b7484e3c5df8b3a2e6fd71a483e10","parts":1,"part":1} -->

`````text
@britishairways-ent:registry=https://npm.pkg.github.com
`````

## src/EventBooking.Web/App.razor — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/App.razor","encoding":"utf8","sha256":"e7942ec3b1a8bb0ff3db881ee991abe4764c469bc5ac77f877b26f84c0c1946b","parts":1,"part":1} -->

`````razor
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.AspNetCore.Components.WebAssembly.Authentication

<CascadingAuthenticationState>
    <Router AppAssembly="@typeof(App).Assembly">
        <Found Context="routeData">
            <AuthorizeRouteView RouteData="@routeData" DefaultLayout="@typeof(Layout.MainLayout)">
                <NotAuthorized>
                    @if (context.User.Identity?.IsAuthenticated != true)
                    {
                        <RedirectToLogin />
                    }
                    else
                    {
                        <div class="page">
                            <div class="card">
                                <div class="empty-state">
                                    <strong>You do not have access to this page.</strong>
                                    <p>
                                        Your roles do not cover this workspace. Ask an administrator to
                                        grant the role you need, then sign in again.
                                    </p>
                                    <a class="button" href="/">Back to your workspace</a>
                                </div>
                            </div>
                        </div>
                    }
                </NotAuthorized>
            </AuthorizeRouteView>
        </Found>
        <NotFound>
            <LayoutView Layout="@typeof(Layout.MainLayout)">
                <div class="page">
                    <div class="card">
                        <div class="empty-state">
                            <strong>Page not found.</strong>
                            <p>The address you followed does not match anything in EventBooking.</p>
                            <a class="button" href="/">Back to your workspace</a>
                        </div>
                    </div>
                </div>
            </LayoutView>
        </NotFound>
    </Router>
</CascadingAuthenticationState>
`````

## src/EventBooking.Web/Dockerfile — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Dockerfile","encoding":"utf8","sha256":"efc001737b25efd8f29ddde0e6d4f862c637a6dca2bc52d667f7dc4028d9a2ee","parts":1,"part":1} -->

`````text
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/EventBooking.Web/EventBooking.Web.csproj -c Release -o /app

FROM nginxinc/nginx-unprivileged:alpine AS runtime
COPY --from=build /app/wwwroot /usr/share/nginx/html
COPY src/EventBooking.Web/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 8080
`````

## src/EventBooking.Web/EventBooking.Web.csproj — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/EventBooking.Web.csproj","encoding":"utf8","sha256":"bbfbf57b773b4ed17cbe19ff30bf09b0dfc7e5eb252396c8a470eb62d4788885","parts":1,"part":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">

  <PropertyGroup>
    <OverrideHtmlAssetPlaceholders>true</OverrideHtmlAssetPlaceholders>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" PrivateAssets="all" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.Authentication" />
    <PackageReference Include="Microsoft.Extensions.Http" />
    <PackageReference Include="Markdig" />
  </ItemGroup>

  <ItemGroup>
    <!-- Bundled so the in-app Help page (Pages/Help.razor) renders the same guides that live in
         docs/user-guides — one source of truth, no runtime fetch. -->
    <EmbeddedResource Include="../../docs/user-guides/*.md" Exclude="../../docs/user-guides/README.md"
                       LogicalName="UserGuides/%(Filename)%(Extension)" />

    <!-- The guides' markdown images reference these by path (see UserGuideCatalog's rewrite of
         "screenshots/" to "/help-assets/screenshots/"); serving them means copying the files into
         wwwroot, since a Blazor WASM app has no backend to stream embedded resources from at
         request time. Deliberately not wwwroot/help/... — that would create a real /help directory
         that collides with the client-side /help route: nginx's try_files matches the directory
         before falling back to index.html and 301-redirects to a trailing slash instead of serving
         the SPA. -->
    <Content Include="../../docs/user-guides/screenshots/*.png">
      <Link>wwwroot/help-assets/screenshots/%(Filename)%(Extension)</Link>
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
    </Content>
  </ItemGroup>

</Project>
`````

## src/EventBooking.Web/Layout/CandidateLayout.razor — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Layout/CandidateLayout.razor","encoding":"utf8","sha256":"0ef2874bea3185589b681ff310c511d2a00a39db71a395a2457826ff5a2a9378","parts":1,"part":1} -->

`````razor
@inherits LayoutComponentBase

<div class="app-shell">
    <header class="topbar">
        <div class="brand-lockup">
            <BrandMark />
            <div>
                <span class="brand-name">British Airways</span>
                <a class="brand" href="/" aria-label="EventBooking home">EventBooking</a>
            </div>
        </div>
    </header>

    <main class="main-content">
        @Body
    </main>

    <footer class="app-footer">
        <span>British Airways · Recruitment appointments</span>
        <span>Your booking link is personal to you — please do not forward it.</span>
        <a href="/help">Need help?</a>
    </footer>
</div>
`````

## src/EventBooking.Web/Layout/MainLayout.razor — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Layout/MainLayout.razor","encoding":"utf8","sha256":"572ce555c47ed10edbb7969a4973bf395e24dd054b7530ec6abb7007bc612605","parts":1,"part":1} -->

`````razor
@inherits LayoutComponentBase
@using Microsoft.AspNetCore.Components.WebAssembly.Authentication
@using Microsoft.AspNetCore.Components.Authorization
@using EventBooking.Web.Services
@implements IDisposable
@inject NavigationManager Navigation
@inject MeClient Me
@inject AuthenticationStateProvider AuthState

<div class="app-shell">
    <header class="topbar">
        <div class="brand-lockup">
            <BrandMark />
            <div>
                <span class="brand-name">British Airways</span>
                <a class="brand" href="/" aria-label="EventBooking home">EventBooking</a>
            </div>
        </div>
        <AuthorizeView>
            <Authorized>
                <div class="identity" aria-label="Signed-in staff member">
                    <strong>@context.User.Identity?.Name</strong>
                    <span>Staff workspace</span>
                    <button type="button" @onclick="SignOut"
                            title="End this session on this device. Any unsaved changes on the page are discarded.">
                        Sign out
                    </button>
                </div>
            </Authorized>
            <NotAuthorized>
                <a class="sign-in-button" href="authentication/login">Sign in</a>
            </NotAuthorized>
        </AuthorizeView>
    </header>

    @if (_navLinks.Count > 0)
    {
        <nav class="staff-nav" aria-label="Workspace sections">
            @foreach (var link in _navLinks)
            {
                <a class="staff-nav-link @(IsActive(link.Href) ? "active" : null)"
                   href="@link.Href" title="@link.Description">
                    @link.Label
                </a>
            }
        </nav>
    }

    <main class="main-content">
        <CascadingValue Value="_meOutcome">
            @Body
        </CascadingValue>
    </main>

    <footer class="app-footer">
        <span>British Airways · Recruitment appointment coordination</span>
        <span>Times shown at head office local time</span>
    </footer>
</div>

@code {
    private IReadOnlyList<StaffLink> _navLinks = [];

    // Cascaded so pages needing the caller's roles (e.g. Home) reuse this single fetch instead
    // of racing it with a second concurrent call to the same token-protected endpoint.
    private ApiOutcome<MeDto>? _meOutcome;

    protected override async Task OnInitializedAsync()
    {
        Navigation.LocationChanged += OnLocationChanged;
        AuthState.AuthenticationStateChanged += OnAuthenticationStateChanged;

        await LoadMeAsync();
    }

    // Right after the OIDC login redirect completes, the first GetAuthenticationStateAsync call
    // here can still race the provider and see an unauthenticated user even though the header's
    // AuthorizeView (subscribed to this same event via CascadingAuthenticationState) goes on to
    // render as signed in. Without this subscription that miss was permanent: OnInitializedAsync
    // runs once, so _meOutcome stayed null and the workspace tiles never left "Loading…".
    private async void OnAuthenticationStateChanged(Task<AuthenticationState> _)
    {
        await LoadMeAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadMeAsync()
    {
        if (_meOutcome is not null)
        {
            return;
        }

        var state = await AuthState.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        _meOutcome = await Me.GetAsync(CancellationToken.None);
        if (_meOutcome.IsSuccess && _meOutcome.Value is not null)
        {
            var links = StaffNavigation.LinksFor(_meOutcome.Value);
            if (links.Count > 0)
            {
                links = [.. links, new StaffLink("/help", "Help", "Read the guide for your role")];
            }

            _navLinks = links;
        }
    }

    private bool IsActive(string href) =>
        string.Equals(
            Navigation.ToBaseRelativePath(Navigation.Uri).TrimEnd('/'),
            href.TrimStart('/').TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase);

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => StateHasChanged();

    // Sign-out must start as in-page navigation: landing on authentication/logout
    // any other way makes the framework reject it as not initiated from the page.
    private void SignOut() =>
        Navigation.NavigateToLogout("authentication/logout");

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        AuthState.AuthenticationStateChanged -= OnAuthenticationStateChanged;
    }
}
`````

## src/EventBooking.Web/Layout/RedirectToLogin.razor — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Layout/RedirectToLogin.razor","encoding":"utf8","sha256":"fcc5fc7a21db10975fdce0a805a58bb512d0b34382d363b9029d3f35c6517f04","parts":1,"part":1} -->

`````razor
@inject NavigationManager Navigation

@code {
    protected override void OnInitialized()
    {
        Navigation.NavigateToLogin("authentication/login");
    }
}
`````

## src/EventBooking.Web/nginx.conf — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/nginx.conf","encoding":"utf8","sha256":"b3bfa9e553281048ceca0433bd7048988910b16ea541e72228bf474c14f63e01","parts":1,"part":1} -->

`````text
server {
    listen 8080;

    root /usr/share/nginx/html;

    # Single-page app: every route that is not a real file falls back to the
    # Blazor host page, which owns client-side routes such as
    # /authentication/login-callback and /book/<token>. The original query
    # string survives the internal redirect, so the login code still arrives.
    location / {
        try_files $uri $uri/ /index.html;
    }
}
`````

## src/EventBooking.Web/Pages/Appointments.razor — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/Appointments.razor","encoding":"utf8","sha256":"d6c275de7493dd7cdeec0909ac9d8f18bf2d5cfe03377b3c08b9f39c7726fa06","parts":1,"part":1} -->

`````razor
@page "/appointments"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject AppointmentsClient AppointmentsApi
@inject Microsoft.JSInterop.IJSRuntime JS
@inject HeadOfficePageClock PageClock

<PageTitle>Appointments</PageTitle>

<div class="page appointments-page">
    <div class="page-header">
        <div>
            <span class="eyebrow">Appointment workspace</span>
            <h1>@(_slotList?.AppointmentTypeName is not null ? $"{_slotList.AppointmentTypeName} appointments" : "Appointments")</h1>
            <p>Check candidates in and record appointment outcomes for your appointment type.</p>
        </div>
        @if (_slotList is not null && _slotList.Slots.Count > 0)
        {
            <div class="field">
                <label for="slot-selector">
                    Slot
                    <span class="tip tip-end" tabindex="0" role="note"
                          aria-label="Recently past, current, and upcoming confirmed windows for your appointment type are listed."
                          data-tip="Recently past, current, and upcoming confirmed windows for your appointment type are listed."></span>
                </label>
                <select id="slot-selector" name="confirmed-slot" value="@_selectedSlotId" @onchange="OnSlotChanged">
                    @if (RecentPastSlots(_slotList).Count > 0)
                    {
                        <optgroup label="Recent past">
                            @foreach (var slot in RecentPastSlots(_slotList))
                            {
                                <option value="@slot.ConfirmedSlotId">
                                    @slot.Date.ToString("yyyy-MM-dd") @slot.StartTime.ToString("HH\\:mm")-@slot.EndTime.ToString("HH\\:mm")
                                </option>
                            }
                        </optgroup>
                    }
                    @if (CurrentAndUpcomingSlots(_slotList).Count > 0)
                    {
                        <optgroup label="Current and upcoming">
                            @foreach (var slot in CurrentAndUpcomingSlots(_slotList))
                            {
                                <option value="@slot.ConfirmedSlotId">
                                    @slot.Date.ToString("yyyy-MM-dd") @slot.StartTime.ToString("HH\\:mm")-@slot.EndTime.ToString("HH\\:mm")
                                </option>
                            }
                        </optgroup>
                    }
                </select>
            </div>
        }
    </div>

    @if (_error is not null)
    {
        <p class="banner error" role="alert">@_error</p>
    }

    @if (_announcement is not null)
    {
        <p class="banner notice" aria-live="polite">@_announcement</p>
    }

    @if (_loadingSlots && _slotList is null)
    {
        <div class="card loading-block" role="status">
            <span class="loading-line loading-line-medium"></span>
            <span class="loading-line"></span>
            <span class="visually-hidden">Loading appointment slots…</span>
        </div>
    }
    else if (_slotList is not null && _slotList.Slots.Count == 0)
    {
        <div class="card">
            <div class="empty-state">
                <strong>No current or upcoming appointment slots.</strong>
                <p>Slots appear here once every appointment type has accepted a proposed window.</p>
            </div>
        </div>
    }
    else if (_loadingDetail && _detail is null)
    {
        <div class="card loading-block" role="status">
            <span class="loading-line loading-line-short"></span>
            <span class="loading-line"></span>
            <span class="visually-hidden">Loading appointments…</span>
        </div>
    }
    else if (_detail is not null)
    {
        <section class="card" aria-label="Selected slot">
            <div class="card-heading">
                <h2 class="slot-window">
                    @(_detail.Date.ToString("dddd, dd MMM yyyy"))
                    · @(_detail.StartTime.ToString("HH\\:mm"))-@(_detail.EndTime.ToString("HH\\:mm"))
                </h2>
                <ul class="count-chips">
                    <li class="chip">Expected @_detailCounts?.Expected</li>
                    <li class="chip status-checkedin">Checked in @_detailCounts?.CheckedIn</li>
                    <li class="chip status-completed">Completed @_detailCounts?.Completed</li>
                    <li class="chip status-noshow">No-show @_detailCounts?.NoShow</li>
                </ul>
                <button class="button" data-testid="download-roster" type="button"
                        @onclick="DownloadRosterAsync" disabled="@_downloadingRoster"
                        title="Downloads this slot's roster as a CSV file to work from offline.">
                    @(_downloadingRoster ? "Preparing…" : "Download roster")
                </button>
            </div>

            @if (_detail.Appointments.Count == 0)
            {
                <div class="empty-state">
                    <strong>Nobody to see in this window.</strong>
                    <p>No candidates require this appointment in the selected slot.</p>
                </div>
            }
            else
            {
                <div class="table-wrap">
                    <table>
                        <thead>
                            <tr>
                                <th scope="col">Candidate</th>
                                <th scope="col" title="Expected, checked in, completed, or no-show.">Status</th>
                                <th scope="col" title="When check-in was recorded, at head office local time.">Check-in recorded</th>
                                <th scope="col" title="When completion or no-show was recorded, at head office local time.">Outcome recorded</th>
                                <th scope="col" class="actions-column"><span class="muted">Actions</span></th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var row in _detail.Appointments)
                            {
                                var busy = _busyRows.Contains(row.BookingAppointmentId);
                                <tr>
                                    <td data-label="Candidate">
                                        <span class="candidate-name">@row.CandidateName</span>
                                        <span class="candidate-email">@row.CandidateEmail</span>
                                    </td>
                                    <td data-label="Status"><span class="status-@row.Status.ToLowerInvariant()">@row.Status</span></td>
                                    <td data-label="Check-in recorded">@(row.CheckedInAt?.ToString("yyyy-MM-dd HH:mm zzz") ?? "—")</td>
                                    <td data-label="Outcome recorded">@(row.OutcomeAt?.ToString("yyyy-MM-dd HH:mm zzz") ?? "—")</td>
                                    <td data-label="Actions">
                                        <span class="row-actions">
                                            @foreach (var action in ActionsFor(row))
                                            {
                                                <button class="button" data-action="@action.Action"
                                                        title="@action.Tip"
                                                        @onclick="() => OnActionAsync(row, action.TargetStatus, action.Label, action.RequiresConfirmation)"
                                                        disabled="@(busy || !action.Enabled)">
                                                    @action.Label
                                                </button>
                                            }
                                        </span>
                                        @if (row.Status == "Expected")
                                        {
                                            @if (!IsToday(_detail))
                                            {
                                                <p class="row-hint">Check-in opens on the confirmed-slot date.</p>
                                            }
                                            else if (!WindowHasEnded(_detail))
                                            {
                                                <p class="row-hint">No-show is available after the slot window ends.</p>
                                            }
                                        }
                                    </td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            }
        </section>
    }

    @if (_pending is not null)
    {
        <div class="dialog-overlay" @onclick="CancelPending">
            <div role="alertdialog" aria-labelledby="confirm-heading" aria-describedby="confirm-description" @onclick:stopPropagation="true">
                <h2 id="confirm-heading">@_pending.Label</h2>
                <p id="confirm-description">@_pending.Label for @_pending.CandidateName. This change is recorded immediately.</p>
                <span class="row-actions">
                    <button class="button button-primary" data-confirm="yes" @onclick="ConfirmPendingAsync"
                            title="Records the change and writes it to the appointment history.">Confirm</button>
                    <button class="button button-quiet" data-confirm="no" @onclick="CancelPending">Cancel</button>
                </span>
            </div>
        </div>
    }
</div>

@code {
    private AppointmentWorkspaceSlotListDto? _slotList;
    private AppointmentSlotDetailDto? _detail;
    private Guid? _selectedSlotId;
    private readonly HashSet<Guid> _busyRows = [];
    private PendingAction? _pending;
    private int _detailRequestVersion;
    private bool _loadingSlots;
    private bool _loadingDetail;
    private bool _downloadingRoster;
    private string? _error;
    private string? _announcement;

    private sealed record PendingAction(
        Guid BookingAppointmentId, string CandidateName, string TargetStatus, string Label);

    private sealed record RowAction(
        string Action, string Label, string TargetStatus, bool Enabled, bool RequiresConfirmation, string Tip);

    private AppointmentStatusCountsDto? _detailCounts =>
        _slotList?.Slots.FirstOrDefault(slot => slot.ConfirmedSlotId == _selectedSlotId)?.Counts;

    protected override async Task OnInitializedAsync()
    {
        _loadingSlots = true;
        try
        {
            var outcome = await AppointmentsApi.ListSlotsAsync(CancellationToken.None);
            if (!outcome.IsSuccess || outcome.Value is null)
            {
                _error = outcome.ErrorMessage;
                return;
            }

            _slotList = outcome.Value;
            var first = CurrentAndUpcomingSlots(_slotList).FirstOrDefault()
                ?? RecentPastSlots(_slotList).LastOrDefault();
            if (first is not null)
            {
                await LoadDetailAsync(first.ConfirmedSlotId);
            }
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        finally
        {
            _loadingSlots = false;
        }
    }

    // Fetched through the authenticated client and handed to the interop helper: a plain anchor
    // to the API route would not carry the caller's bearer token.
    private async Task DownloadRosterAsync()
    {
        if (_selectedSlotId is null || _downloadingRoster)
        {
            return;
        }

        _downloadingRoster = true;
        _error = null;
        try
        {
            var outcome = await AppointmentsApi.GetRosterAsync(
                _selectedSlotId.Value, CancellationToken.None);
            if (outcome is not { IsSuccess: true, Value: not null })
            {
                _error = outcome.ErrorMessage ?? "Something went wrong. Please try again.";
                return;
            }

            await JS.InvokeVoidAsync(
                "saveTextFile", outcome.Value.FileName, outcome.Value.Content);
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        finally
        {
            _downloadingRoster = false;
            StateHasChanged();
        }
    }

    private async Task OnSlotChanged(ChangeEventArgs args)
    {
        if (Guid.TryParse(args.Value?.ToString(), out var slotId) && slotId != _selectedSlotId)
        {
            await LoadDetailAsync(slotId);
        }
    }

    private async Task<bool> LoadDetailAsync(Guid slotId)
    {
        var requestVersion = ++_detailRequestVersion;
        _loadingDetail = true;
        _selectedSlotId = slotId;
        _detail = null;
        _pending = null;
        _error = null;
        _announcement = null;
        try
        {
            var outcome = await AppointmentsApi.GetSlotAsync(slotId, CancellationToken.None);
            if (requestVersion != _detailRequestVersion)
            {
                return false;
            }

            if (!outcome.IsSuccess || outcome.Value is null)
            {
                _error = outcome.ErrorMessage;
                return false;
            }

            _detail = outcome.Value;
            _error = null;
            return true;
        }
        catch (Exception)
        {
            if (requestVersion == _detailRequestVersion)
            {
                _error = "Something went wrong. Please try again.";
            }

            return false;
        }
        finally
        {
            if (requestVersion == _detailRequestVersion)
            {
                _loadingDetail = false;
            }
        }
    }

    private bool IsToday(AppointmentSlotDetailDto detail) =>
        detail.Date == DateOnly.FromDateTime(PageClock.NowAtHeadOffice.DateTime);

    private DateOnly TodayAtHeadOffice() =>
        DateOnly.FromDateTime(PageClock.NowAtHeadOffice.DateTime);

    private IReadOnlyList<AppointmentSlotSummaryDto> RecentPastSlots(
        AppointmentWorkspaceSlotListDto slotList) =>
        slotList.Slots.Where(slot => slot.Date < TodayAtHeadOffice()).ToList();

    private IReadOnlyList<AppointmentSlotSummaryDto> CurrentAndUpcomingSlots(
        AppointmentWorkspaceSlotListDto slotList) =>
        slotList.Slots.Where(slot => slot.Date >= TodayAtHeadOffice()).ToList();

    private bool WindowHasEnded(AppointmentSlotDetailDto detail)
    {
        var now = PageClock.NowAtHeadOffice;
        var end = new DateTimeOffset(detail.Date.ToDateTime(detail.EndTime), now.Offset);
        return now >= end;
    }

    private IReadOnlyList<RowAction> ActionsFor(BookingAppointmentRowDto row)
    {
        return row.Status switch
        {
            "Expected" when _detail is not null => new RowAction[]
            {
                new("check-in", "Check in", "CheckedIn", IsToday(_detail), false,
                    "Marks the candidate as arrived. Only available on the slot date."),
                new("no-show", "No-show", "NoShow", WindowHasEnded(_detail), true,
                    "Records that the candidate never arrived. Only available once the window has ended."),
            },
            "CheckedIn" => new RowAction[]
            {
                new("complete", "Complete", "Completed", true, false,
                    "Records the appointment as delivered."),
                new("correct-expected", "Correct to expected", "Expected", true, true,
                    "Undoes the check-in if it was recorded against the wrong candidate."),
            },
            "Completed" => new RowAction[]
            {
                new("correct-checked-in", "Correct to checked in", "CheckedIn", true, true,
                    "Undoes the completion and puts the candidate back to checked in."),
            },
            "NoShow" => new RowAction[]
            {
                new("correct-expected", "Correct to expected", "Expected", true, true,
                    "Clears the no-show so the candidate is expected again."),
            },
            _ => [],
        };
    }

    private Task OnActionAsync(
        BookingAppointmentRowDto row, string targetStatus, string label, bool requiresConfirmation)
    {
        if (requiresConfirmation)
        {
            _pending = new PendingAction(row.BookingAppointmentId, row.CandidateName, targetStatus, label);
            return Task.CompletedTask;
        }

        return UpdateAsync(row.BookingAppointmentId, row.CandidateName, targetStatus);
    }

    private void CancelPending() => _pending = null;

    private async Task ConfirmPendingAsync()
    {
        if (_pending is null)
        {
            return;
        }

        var pending = _pending;
        _pending = null;
        await UpdateAsync(pending.BookingAppointmentId, pending.CandidateName, pending.TargetStatus);
    }

    private async Task UpdateAsync(Guid bookingAppointmentId, string candidateName, string targetStatus)
    {
        var sourceDetail = _detail;
        if (sourceDetail is null || !_busyRows.Add(bookingAppointmentId))
        {
            return;
        }

        var sourceSlotId = sourceDetail.ConfirmedSlotId;
        var sourceRequestVersion = _detailRequestVersion;
        var row = sourceDetail.Appointments.FirstOrDefault(
            item => item.BookingAppointmentId == bookingAppointmentId);
        if (row is null)
        {
            _busyRows.Remove(bookingAppointmentId);
            return;
        }

        try
        {
            var outcome = await AppointmentsApi.UpdateStatusAsync(
                bookingAppointmentId, targetStatus, row.Version, CancellationToken.None);
            var appliesToCurrentDetail = IsCurrentDetail(sourceSlotId, sourceRequestVersion);
            if (outcome.IsSuccess && outcome.Value is not null)
            {
                ApplyUpdate(sourceSlotId, row, outcome.Value, appliesToCurrentDetail);
                if (appliesToCurrentDetail)
                {
                    _error = null;
                    _announcement = AnnouncementFor(targetStatus, candidateName);
                }

                return;
            }

            if (!appliesToCurrentDetail)
            {
                return;
            }

            if (outcome.ErrorCode == AppointmentsClient.VersionConflictErrorCode)
            {
                if (await LoadDetailAsync(sourceSlotId))
                {
                    _error = "Another staff member changed this appointment. The row has been refreshed; review it before trying again.";
                }

                return;
            }

            _error = outcome.ErrorMessage;
        }
        catch (Exception)
        {
            if (IsCurrentDetail(sourceSlotId, sourceRequestVersion))
            {
                _error = "Something went wrong. Please try again.";
            }
        }
        finally
        {
            _busyRows.Remove(bookingAppointmentId);
        }
    }

    private bool IsCurrentDetail(Guid slotId, int requestVersion) =>
        requestVersion == _detailRequestVersion
        && _selectedSlotId == slotId
        && _detail?.ConfirmedSlotId == slotId;

    private void ApplyUpdate(
        Guid sourceSlotId,
        BookingAppointmentRowDto row,
        BookingAppointmentUpdateDto update,
        bool applyToCurrentDetail)
    {
        if (_slotList is null)
        {
            return;
        }

        var oldStatus = row.Status;
        if (applyToCurrentDetail && _detail is not null)
        {
            _detail = _detail with
            {
                Appointments = _detail.Appointments
                    .Select(item => item.BookingAppointmentId == row.BookingAppointmentId
                        ? item with
                        {
                            Status = update.Status,
                            CheckedInAt = update.CheckedInAt,
                            OutcomeAt = update.OutcomeAt,
                            Version = update.Version,
                        }
                        : item)
                    .ToList(),
            };
        }

        _slotList = _slotList with
        {
            Slots = _slotList.Slots
                .Select(slot => slot.ConfirmedSlotId == sourceSlotId
                    ? slot with { Counts = MoveCount(slot.Counts, oldStatus, update.Status) }
                    : slot)
                .ToList(),
        };
    }

    private static AppointmentStatusCountsDto MoveCount(
        AppointmentStatusCountsDto counts, string oldStatus, string newStatus)
    {
        var buckets = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Expected"] = counts.Expected,
            ["CheckedIn"] = counts.CheckedIn,
            ["Completed"] = counts.Completed,
            ["NoShow"] = counts.NoShow,
        };
        if (buckets.ContainsKey(oldStatus))
        {
            buckets[oldStatus] = Math.Max(0, buckets[oldStatus] - 1);
        }

        if (buckets.ContainsKey(newStatus))
        {
            buckets[newStatus] += 1;
        }

        return new AppointmentStatusCountsDto
        {
            Expected = buckets["Expected"],
            CheckedIn = buckets["CheckedIn"],
            Completed = buckets["Completed"],
            NoShow = buckets["NoShow"],
        };
    }

    private static string AnnouncementFor(string targetStatus, string candidateName) =>
        targetStatus switch
        {
            "CheckedIn" => $"Check-in recorded for {candidateName}.",
            "Completed" => $"Completion recorded for {candidateName}.",
            "NoShow" => $"No-show recorded for {candidateName}.",
            _ => $"Correction recorded for {candidateName}.",
        };
}
`````

## src/EventBooking.Web/Pages/Appointments.razor.css — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/Appointments.razor.css","encoding":"utf8","sha256":"05efdf9bb471e02b5fdaf57c63e9cb6f4d40f51b1dd3c1c911db2fdfc4c1b6c4","parts":1,"part":1} -->

`````text
.slot-window {
    color: var(--ink-strong);
    font-size: 0.9375rem;
    font-weight: 600;
    letter-spacing: 0;
    margin: 0;
    text-transform: none;
}

.row-hint {
    color: var(--sub);
    font-size: 0.75rem;
    font-style: italic;
    margin: 6px 0 0;
}

.candidate-name {
    display: block;
    font-weight: 600;
}

.candidate-email {
    color: var(--sub);
    display: block;
    font-size: 0.75rem;
}

.table-wrap {
    max-height: calc(100vh - 360px);
    min-height: 200px;
    overflow-y: auto;
}

.table-wrap thead th {
    position: sticky;
    top: 0;
    z-index: 1;
}

.dialog-overlay {
    align-items: center;
    background: rgba(0, 0, 0, 0.45);
    display: flex;
    inset: 0;
    justify-content: center;
    padding: 20px;
    position: fixed;
    z-index: 100;
}

[role="alertdialog"] {
    background: var(--surface);
    border: 1px solid var(--accent);
    border-left: 5px solid var(--accent);
    border-radius: var(--radius);
    box-shadow: var(--shadow);
    display: flex;
    flex-direction: column;
    gap: 10px;
    max-width: 480px;
    padding: 18px 20px;
    width: 100%;
}

[role="alertdialog"] h2 {
    font-size: 1rem;
    margin: 0;
}

[role="alertdialog"] p {
    color: var(--sub);
    font-size: 0.8125rem;
    margin: 0;
    max-width: 60ch;
}

@media (max-width: 760px) {
    .candidate-email,
    .candidate-name {
        text-align: right;
    }
}
`````

## src/EventBooking.Web/Pages/Audit.razor — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/Audit.razor","encoding":"utf8","sha256":"76f1b8f337f0bbe4317174f7456cde24ee0ed29059ed2d4f37e1c86a5342272e","parts":1,"part":1} -->

`````razor
@page "/audit"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject AuditClient Audits
@inject MeClient Me
@inject HeadOfficeTimePresentation TimePresentation

<PageTitle>Audit trail</PageTitle>

<section class="page audit-page" aria-labelledby="audit-heading">
    <div class="page-header">
        <div>
            <span class="eyebrow">Assurance</span>
            <h1 id="audit-heading">Audit trail</h1>
            <p>Search what changed, who changed it, and when.</p>
        </div>
    </div>

    <div class="card">
        <div class="card-body audit-filters" aria-label="Audit search filters">
            <div class="field">
                <label class="field-label" for="audit-from">From</label>
                <input id="audit-from" type="date" @bind="_from"
                       title="Only shows changes recorded on or after this date." />
            </div>
            <div class="field">
                <label class="field-label" for="audit-to">To</label>
                <input id="audit-to" type="date" @bind="_to"
                       title="Only shows changes recorded on or before this date." />
            </div>
            <div class="field">
                <label class="field-label" for="audit-actor-type">Actor</label>
                <select id="audit-actor-type" @bind="_actorType"
                        title="Who caused the change: a staff member, a candidate's link, or the system.">
                    <option value="">Any actor</option>
                    @foreach (var actorType in ActorTypes)
                    {
                        <option value="@actorType">@actorType</option>
                    }
                </select>
            </div>
            <div class="field">
                <label class="field-label" for="audit-action">Action</label>
                <select id="audit-action" @bind="_action"
                        title="The recorded change to look for.">
                    <option value="">Any action</option>
                    @foreach (var action in Actions)
                    {
                        <option value="@action">@action</option>
                    }
                </select>
            </div>
            <div class="field">
                <label class="field-label" for="audit-identifier">Identifier</label>
                <input id="audit-identifier" @bind="_identifier" placeholder="Entity or actor id"
                       title="Matches an audited entity identifier or an actor identifier exactly." />
            </div>
            @if (_showEntityType)
            {
                <div class="field">
                    <label class="field-label" for="audit-entity-type">Entity</label>
                    <select id="audit-entity-type" @bind="_entityType"
                            title="Narrows the search to one kind of audited entity.">
                        <option value="">All entities</option>
                        @foreach (var entityType in EntityTypes)
                        {
                            <option value="@entityType">@entityType</option>
                        }
                    </select>
                </div>
            }
            <button id="audit-search" type="button" class="button button-primary" @onclick="SearchAsync" disabled="@_busy">
                Search
            </button>
        </div>
    </div>

    @if (_error is not null)
    {
        <p class="error" role="alert">@_error</p>
    }
    else if (_rows.Count == 0 && !_busy)
    {
        <p>Nothing matches these filters.</p>
    }

    @if (_rows.Count > 0)
    {
        <div class="table-wrap">
            <table>
                <thead>
                    <tr>
                        <th scope="col">When</th>
                        <th scope="col">What</th>
                        <th scope="col">Who</th>
                        <th scope="col">Details</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var row in _rows)
                    {
                        <tr>
                            <td data-label="When">@TimePresentation.Format(row.Timestamp)</td>
                            <td data-label="What">@row.EntityType @row.Action</td>
                            <td data-label="Who">@row.ActorType @row.ActorId</td>
                            <td data-label="Details">@row.Details</td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    }

    @if (_nextCursor is not null)
    {
        <button id="audit-load-more" type="button" class="button" @onclick="LoadMoreAsync" disabled="@_busy">
            Load more
        </button>
    }
</section>

@code {
    // The web app deliberately does not reference the domain assembly, so the server's enum names
    // are repeated here as plain strings; the API rejects anything it does not recognise.
    private static readonly string[] ActorTypes = ["Staff", "CandidateToken", "System"];

    private static readonly string[] Actions =
    [
        "ProposalCreated", "ProposalWithdrawn", "AcceptanceRecorded", "AcceptanceWithdrawn",
        "SlotConfirmed", "SlotCancelled", "CapacityDecremented", "CapacityIncremented",
        "InviteCreated", "InviteSent", "InviteExpired", "InviteOptionReplaced",
        "BookingCreated", "BookingCancelled", "CapacityAdjusted", "SlotImported",
        "StaffAccessChanged", "StaffAccessRemoved", "AppointmentCheckedIn", "AppointmentCompleted",
        "AppointmentMarkedNoShow", "AppointmentStatusCorrected", "EmployeeGroupAssigned",
        "EmployeeGroupChanged", "RecoveryInviteCreated", "RecoveryInviteCancelled",
        "RecoveryBookingCreated", "RecoveryBookingConcluded", "StaffRolesSynced",
    ];

    private static readonly string[] EntityTypes =
    [
        "SlotProposal", "ConfirmedSlot", "Invite", "Booking",
        "StaffAccessProfile", "BookingAppointment", "Candidate",
    ];

    private const int PageSize = 50;

    private readonly List<AuditRowDto> _rows = [];
    private DateTime? _from;
    private DateTime? _to;
    private string? _actorType;
    private string? _action;
    private string? _identifier;
    private string? _entityType;
    private string? _nextCursor;
    private string? _error;
    private bool _busy;
    private bool _showEntityType;

    protected override async Task OnInitializedAsync()
    {
        var me = await Me.GetAsync(CancellationToken.None);
        _showEntityType = me.Value?.Roles.Contains("Coordinator") == true;
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        _rows.Clear();
        _nextCursor = null;
        await LoadMoreAsync();
    }

    private async Task LoadMoreAsync()
    {
        _busy = true;
        _error = null;
        try
        {
            var outcome = await Audits.SearchAsync(
                new AuditSearchFilterDto(
                    ToOffset(_from),
                    ToOffset(_to),
                    EmptyToNull(_actorType),
                    EmptyToNull(_action),
                    EmptyToNull(_identifier),
                    EmptyToNull(_entityType),
                    _nextCursor,
                    PageSize),
                CancellationToken.None);

            if (outcome is { IsSuccess: true, Value: not null })
            {
                _rows.AddRange(outcome.Value.Rows);
                _nextCursor = outcome.Value.NextCursor;
            }
            else
            {
                _error = outcome.ErrorMessage ?? "Something went wrong. Please try again.";
            }
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        finally
        {
            _busy = false;
        }
    }

    private static DateTimeOffset? ToOffset(DateTime? value) =>
        value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
`````

## src/EventBooking.Web/Pages/Audit.razor.css — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/Audit.razor.css","encoding":"utf8","sha256":"d456110984455a7943e583de66d3196709757efbc04d7606bd5597787b597fe7","parts":1,"part":1} -->

`````text
.audit-filters {
    align-items: flex-end;
    display: flex;
    flex-wrap: wrap;
    gap: 12px 16px;
}

.audit-filters .field {
    flex: 1 1 160px;
    min-width: 0;
}

.audit-filters .field input,
.audit-filters .field select {
    width: 100%;
}

@media (max-width: 760px) {
    .audit-filters {
        align-items: stretch;
        flex-direction: column;
    }

    .audit-filters .field {
        flex-basis: auto;
    }
}

/* Details carry unspaced JSON and identifiers, which would otherwise widen the page on phones. */
.audit-page td {
    overflow-wrap: anywhere;
}
`````
