# 00a — Port source 37 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.Web/Services/UserGuideCatalog.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Services/UserGuideCatalog.cs","encoding":"utf8","sha256":"4d9370cba912f4abe828aab76593606b0b148e66e3ece1efb18b69fdefafffce","parts":1,"part":1} -->

`````csharp
using System.Reflection;

namespace EventBooking.Web.Services;

/// <summary>One role's rendered guide, ready to drop into the Help page.</summary>
/// <param name="RoleKey">The role this guide covers, or "Candidate" for the anonymous guide.</param>
/// <param name="AnchorId">The in-page section id other guides' cross-links target.</param>
/// <param name="Title">The guide's display heading.</param>
/// <param name="Html">The guide's markdown, already rendered to HTML.</param>
public sealed record UserGuide(string RoleKey, string AnchorId, string Title, string Html);

/// <summary>
/// Serves the docs/user-guides markdown files bundled into the assembly as embedded resources,
/// rendered to HTML for the in-app Help page (Pages/Help.razor). Bundling keeps the guides working
/// offline and versioned with the code that matches them, at the cost of needing a rebuild to
/// publish a wording change.
/// </summary>
public static class UserGuideCatalog
{
    private const string ResourcePrefix = "UserGuides/";

    // Ordered to match the workspace-link union StaffNavigation.LinksFor builds for a combined
    // profile, so a coordinator-manager sees Manager guidance before Coordinator guidance.
    private static readonly (string RoleKey, string ResourceName, string Title)[] RoleGuides =
    [
        ("Manager", "manager-guide.md", "Manager guide"),
        ("AppointmentStaff", "appointment-staff-guide.md", "Appointment staff guide"),
        ("Coordinator", "coordinator-guide.md", "Coordinator guide"),
    ];

    private static readonly (string RoleKey, string ResourceName, string Title) AdminGuide =
        ("Admin", "admin-guide.md", "Admin guide");

    private static readonly (string RoleKey, string ResourceName, string Title) CandidateGuideEntry =
        ("Candidate", "candidate-guide.md", "Candidate guide");

    // Every signed-in staff member can read every guide, including the candidate guide — a
    // coordinator fielding a candidate's question, or a manager covering another type, needs the
    // same reference the other roles see, not just their own. Only a signed-out visitor is
    // restricted to the candidate guide alone (the <NotAuthorized> branch in Help.razor).
    private static readonly (string RoleKey, string ResourceName, string Title)[] AllGuides =
        [AdminGuide, .. RoleGuides, CandidateGuideEntry];

    // GuidesFor unlocks the full catalog for anyone holding at least one of these — an account
    // whose only role isn't one EventBooking recognises still gets no guide, matching Home.razor's
    // "not assigned a role yet" message rather than dumping the whole catalog on a role typo.
    private static readonly HashSet<string> RecognisedStaffRoles = new(
        RoleGuides.Select(guide => guide.RoleKey).Append(AdminGuide.RoleKey),
        StringComparer.Ordinal);

    // The screenshots the guides embed as "screenshots/foo.png" (relative to docs/user-guides/) are
    // published to wwwroot/help-assets/screenshots by the .csproj. Blazor's <base href="/"> resolves
    // relative URLs against "/" regardless of the current route, not against "/help", so the bare
    // relative path 404s (rendering as a broken-image placeholder) unless rewritten to an absolute
    // one — and that path deliberately isn't under /help/, which would collide with the client-side
    // /help route (see the .csproj comment on the Content item for the failure mode).
    private const string ScreenshotMarkdownPrefix = "(screenshots/";
    private const string ScreenshotUrlPrefix = "(/help-assets/screenshots/";

    // Every guide opens with this exact line pointing back to the standalone docs/user-guides
    // index — useful when the file is read on its own (e.g. on GitHub), but redundant on the Help
    // page now that every guide sits on the one page under its own jump-link in the index above
    // them (see Help.razor). Dropped along with the blank line that follows it, so removing it
    // doesn't leave a gap between the heading and the guide's first paragraph.
    private const string BackToAllGuidesLine = "[← All user guides](README.md)\n\n";

    // manager-guide.md links on to "appointment-staff-guide.md". Rendered guides sit inline on one
    // page rather than as browsable files, so that bare filename is rewritten to an in-page anchor
    // to avoid producing a dead link.
    private static readonly Dictionary<string, string> AnchorsByResourceName = RoleGuides
        .Append(AdminGuide)
        .Append(CandidateGuideEntry)
        .ToDictionary(guide => guide.ResourceName, guide => guide.RoleKey.ToLowerInvariant());

    private static readonly Dictionary<string, string> RenderedHtmlByResourceName = new(StringComparer.Ordinal);
    private static readonly Lock RenderLock = new();

    /// <summary>
    /// Builds every guide for a caller holding at least one recognised staff role — the full
    /// catalog, not just the guides matching their own roles, since staff routinely need to
    /// understand what other roles (and candidates) see. A caller with no recognised role gets
    /// none, so Help.razor can show its "not assigned a role yet" message.
    /// </summary>
    public static IReadOnlyList<UserGuide> GuidesFor(IReadOnlyList<string> roles) =>
        roles.Any(RecognisedStaffRoles.Contains) ? AllGuides.Select(BuildGuide).ToList() : [];

    /// <summary>Builds the candidate guide shown to signed-out visitors of the Help page.</summary>
    public static UserGuide CandidateGuide() => BuildGuide(CandidateGuideEntry);

    private static UserGuide BuildGuide((string RoleKey, string ResourceName, string Title) guide) =>
        new(guide.RoleKey, AnchorsByResourceName[guide.ResourceName], guide.Title, RenderedHtmlFor(guide.ResourceName));

    private static string RenderedHtmlFor(string resourceName)
    {
        lock (RenderLock)
        {
            if (RenderedHtmlByResourceName.TryGetValue(resourceName, out var cached))
            {
                return cached;
            }

            var markdown = RewriteCrossGuideLinks(ReadEmbeddedMarkdown(resourceName));
            var html = Markdig.Markdown.ToHtml(markdown);
            RenderedHtmlByResourceName[resourceName] = html;
            return html;
        }
    }

    private static string ReadEmbeddedMarkdown(string resourceName)
    {
        var assembly = typeof(UserGuideCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded user guide '{resourceName}' is missing. Check the EmbeddedResource glob in EventBooking.Web.csproj.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string RewriteCrossGuideLinks(string markdown)
    {
        var rewritten = markdown
            .Replace(BackToAllGuidesLine, string.Empty, StringComparison.Ordinal)
            .Replace(ScreenshotMarkdownPrefix, ScreenshotUrlPrefix, StringComparison.Ordinal);
        foreach (var (resourceName, anchor) in AnchorsByResourceName)
        {
            // Blazor's <base href="/"> resolves a bare "#anchor" against "/", not against the
            // current route — the same failure mode as the screenshot paths above — so a fragment
            // link needs the /help prefix to land back on this page instead of Home.
            rewritten = rewritten.Replace($"({resourceName})", $"(/help#{anchor})", StringComparison.Ordinal);
        }

        return rewritten;
    }
}
`````

## src/EventBooking.Web/Shared/AuditHistory.razor — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Shared/AuditHistory.razor","encoding":"utf8","sha256":"a638e20addc6e766bc940f2911c70dfb115e88fcb813fa2a9d29e80ce60971d9","parts":1,"part":1} -->

`````razor
@using EventBooking.Web.Services
@inject AuditClient Audit
@inject HeadOfficeTimePresentation TimePresentation

<details class="audit-history" @ontoggle="OnToggleAsync">
    <summary title="Every recorded change, newest first, with who made it. Loaded when you expand it.">History</summary>

    @if (_error is not null)
    {
        <p class="error" role="alert">@_error</p>
    }
    else if (!_requested)
    {
        <p>Loading…</p>
    }
    else if (_rows is { Count: 0 })
    {
        <p>Nothing recorded yet.</p>
    }
    else if (_rows is not null)
    {
        <div class="table-wrap">
            <table>
                <thead>
                    <tr><th scope="col">When</th><th scope="col">What</th><th scope="col">Who</th><th scope="col">Details</th></tr>
                </thead>
                <tbody>
                    @foreach (var row in _rows)
                    {
                        <tr>
                            <td data-label="When">@TimePresentation.Format(row.Timestamp)</td>
                            <td data-label="What">@row.Action</td>
                            <td data-label="Who">@row.ActorType @row.ActorId</td>
                            <td data-label="Details">@row.Details</td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    }
</details>

@code {
    /// <summary>Either a confirmed slot or a candidate. Set exactly one.</summary>
    [Parameter]
    public Guid? SlotId { get; set; }

    [Parameter]
    public Guid? CandidateId { get; set; }

    private List<AuditRowDto>? _rows;
    private string? _error;
    private bool _requested;

    // Loaded on first expand rather than on render: a table of 20 slots should not fire 20 requests.
    private async Task OnToggleAsync()
    {
        if (_requested)
        {
            return;
        }

        _requested = true;

        try
        {
            var outcome = SlotId is not null
                ? await Audit.ForSlotAsync(SlotId.Value, CancellationToken.None)
                : await Audit.ForCandidateAsync(CandidateId!.Value, CancellationToken.None);

            _rows = outcome.Value;
            _error = outcome.ErrorMessage;
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
    }
}
`````

## src/EventBooking.Web/Shared/BrandMark.razor — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Shared/BrandMark.razor","encoding":"utf8","sha256":"8ba13590a6eda114ef915affe6d86006e80fb673e2c95d2cbb54f861a0c53ecd","parts":1,"part":1} -->

`````razor
@* The real British Airways Speedmarque, extracted from the internal
   "British Airways_External ASSET DECK.pptx" asset deck (Logos slide) — it carries
   its own fixed red/blue colouring and needs no light/dark variant. *@

<img class="brand-mark" src="speedmarque.png" alt="British Airways" />
`````

## src/EventBooking.Web/wwwroot/appsettings.Development.json — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/wwwroot/appsettings.Development.json","encoding":"utf8","sha256":"61fc38eb3b98ae7362f00e362b3a3fd09bad5768b16e1b9deecd0732b3fc6591","parts":1,"part":1} -->

`````text
{
  "ApiBaseUrl": "http://localhost:5000",
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://localhost:8081/realms/eventbooking",
      "ClientId": "eventbooking-web"
    }
  }
}
`````

## src/EventBooking.Web/wwwroot/appsettings.json — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/wwwroot/appsettings.json","encoding":"utf8","sha256":"4f2f350c862a8ade3be4cc8da7b09571e3af9e712c6f3538d49b69d2e863485d","parts":1,"part":1} -->

`````text
{
  "ApiBaseUrl": "https://localhost:5001",
  "CoordinatorContact": "recruitment@example.com",
  "HeadOfficeTimeZoneId": "Europe/London",
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://localhost:8081/realms/eventbooking",
      "ClientId": "eventbooking-web"
    }
  }
}
`````

## src/EventBooking.Web/wwwroot/css/app.css — 1/2

<!-- port-file: {"path":"src/EventBooking.Web/wwwroot/css/app.css","encoding":"utf8","sha256":"4f6b728919404d0294a19cea3227b24930789630d249ccda16617f25a26bfad6","parts":2,"part":1} -->

`````text
/* Design system for the EventBooking staff and candidate interfaces.
   Every component class here is global on purpose: Blazor scoped CSS cannot reach a
   child component, so shell, table, button and banner rules held per page left shared
   components (AuditHistory, CandidateLayout) unstyled. Page .razor.css files now carry
   only layout that is genuinely local to one page. */

:root {
    color-scheme: light;

    /* BAgel primitive palette (britishairways.design/design/colour) — the 500 shade is
       each family's base. Kept under the existing --ba-* names so nothing downstream
       needs to change; only the hex values move to the real tokens. */
    --ba-blue-700: #234776;
    --ba-blue-600: #2c5791;
    --ba-blue-500: #3468ad;
    --ba-blue-400: #9eb7d8;
    --ba-blue-300: #becfe5;
    --ba-blue-200: #dfe7f2;

    --ba-red-700: #8c160a;
    --ba-red-600: #ad1c0d;
    --ba-red-500: #ce210f;
    --ba-red-200: #f7dbd9;

    --ba-midnight-700: #01122c;
    --ba-midnight-600: #021737;
    --ba-midnight-500: #021b41;

    --ba-green-700: #00573c;
    --ba-green-500: #008058;
    --ba-green-200: #d6ebe4;

    --ba-grey-700: #70758f;
    --ba-grey-600: #989cae;
    --ba-grey-500: #b7b9c6;
    --ba-grey-300: #e7e8ec;
    --ba-grey-200: #f9f9fa;

    /* Aliases so the rest of this file (and page stylesheets) keep working unchanged. */
    --ba-midnight: var(--ba-midnight-500);
    --ba-chatham-blue: var(--ba-blue-500);
    --ba-chatham-blue-hover: var(--ba-blue-600);
    --ba-sky: var(--ba-blue-400);
    --ba-sky-tint: var(--ba-blue-200);
    --ba-speedmarque-red: var(--ba-red-500);
    --ba-red-ink: var(--ba-red-700);
    --ba-mist: var(--ba-grey-200);
    --ba-cloud: #ffffff;

    --ink: var(--ba-midnight-500);
    --ink-strong: var(--ba-midnight);
    --sub: var(--ba-grey-700);
    --line: var(--ba-grey-300);
    --line-strong: var(--ba-grey-500);
    --surface: var(--ba-cloud);
    --surface-sunken: var(--ba-grey-200);
    --page-bg: var(--ba-mist);

    --accent: var(--ba-chatham-blue);
    --accent-hover: var(--ba-chatham-blue-hover);
    --accent-soft: var(--ba-sky-tint);

    --success-bg: var(--ba-green-200);
    --success-line: var(--ba-green-500);
    --success-ink: var(--ba-green-700);
    /* BAgel's public colour page has no primitive "warning" family (only Blue, Red,
       Midnight, Green, Club-tier and Grey) — kept as-is until the real ba-message
       component (which carries its own warning styling) is wired in. */
    --warning-bg: #fdf3e3;
    --warning-line: #b57611;
    --warning-ink: #7a4e06;
    --error-bg: var(--ba-red-200);
    --error-line: var(--ba-red-600);
    --error-ink: var(--ba-red-700);

    /* Aliases kept because earlier page styles referenced these names. */
    --border: var(--line);
    --error: var(--error-ink);
    --success: var(--success-ink);
    --focus: rgb(35 71 118 / 32%);

    --radius-sm: 4px;
    --radius: 8px;
    --radius-lg: 12px;
    --shadow-sm: 0 1px 2px rgb(2 27 65 / 6%);
    --shadow: 0 4px 14px rgb(2 27 65 / 10%);
    --shadow-lg: 0 12px 32px rgb(2 27 65 / 16%);

    /* Mylius Modern is BA's licensed display face (headings, light weight); Open Sans
       is the licensed-free body face (also light weight) — see
       britishairways.design/design/typography. Mylius' actual font files ship inside
       the BAgel package itself, so the named face falls back to system sans until
       that package is wired in. */
    --font-heading: "Mylius Modern", "BA Mylius", -apple-system, BlinkMacSystemFont,
        "Segoe UI", "Helvetica Neue", Arial, sans-serif;
    --font-body: "Open Sans", -apple-system, BlinkMacSystemFont, "Segoe UI",
        "Helvetica Neue", Arial, sans-serif;
    --font-sans: var(--font-body);
    --font-mono: ui-monospace, "SF Mono", Menlo, Consolas, monospace;

    --page-max: 1280px;
    --focus-ring: 3px solid var(--focus);
}

* {
    box-sizing: border-box;
}

html,
body {
    background: var(--page-bg);
    color: var(--ink);
    font-family: var(--font-body);
    font-size: 16px;
    font-weight: 300;
    line-height: 1.5;
    margin: 0;
    -webkit-font-smoothing: antialiased;
}

h1,
h2,
h3 {
    color: var(--ink-strong);
    font-family: var(--font-heading);
    font-weight: 300;
    letter-spacing: -0.01em;
}

h1:focus {
    outline: none;
}

a {
    color: var(--accent);
    text-underline-offset: 2px;
}

a:hover {
    color: var(--accent-hover);
}

:where(a, button, input, select, textarea, summary, [tabindex]):focus-visible {
    outline: var(--focus-ring);
    outline-offset: 2px;
}

code {
    background: var(--surface-sunken);
    border: 1px solid var(--line);
    border-radius: var(--radius-sm);
    font-family: var(--font-mono);
    font-size: 0.8125em;
    overflow-wrap: anywhere;
    padding: 1px 5px;
}

/* ---------- Application shell ---------- */

.app-shell {
    display: flex;
    flex-direction: column;
    min-height: 100vh;
}

.topbar {
    align-items: center;
    background: var(--ba-chatham-blue);
    box-shadow: var(--shadow-sm);
    display: flex;
    gap: 18px;
    justify-content: space-between;
    min-height: 64px;
    padding: 10px 28px;
    position: sticky;
    top: 0;
    z-index: 100;
}

/* The Speedmarque cue: a red-into-blue rule under the whole masthead. */
.topbar::after {
    background: linear-gradient(
        90deg,
        var(--ba-speedmarque-red) 0%,
        var(--ba-speedmarque-red) 22%,
        var(--ba-sky) 60%,
        var(--ba-chatham-blue) 100%);
    bottom: 0;
    content: "";
    height: 3px;
    left: 0;
    position: absolute;
    right: 0;
}

.brand-lockup {
    align-items: center;
    display: flex;
    gap: 12px;
    min-width: 0;
}

/* Real artwork now, with its own fixed colouring and ~4:1 aspect ratio — height-only
   sizing lets width follow naturally rather than stretching/cropping it. */
.brand-mark {
    flex: 0 0 auto;
    height: 22px;
    width: auto;
}

.brand-name {
    color: rgb(255 255 255 / 78%);
    display: block;
    font-size: 0.6875rem;
    font-weight: 600;
    letter-spacing: 0.14em;
    line-height: 1.2;
    text-transform: uppercase;
}

.brand {
    color: #fff;
    display: block;
    font-size: 1.0625rem;
    font-weight: 700;
    letter-spacing: -0.01em;
    line-height: 1.2;
    text-decoration: none;
}

.brand:hover,
.brand:focus-visible {
    color: #fff;
    text-decoration: underline;
}

.identity {
    align-items: flex-end;
    color: rgb(255 255 255 / 72%);
    display: flex;
    flex-direction: column;
    font-size: 0.75rem;
    line-height: 1.35;
    text-align: right;
}

.identity strong {
    color: #fff;
    font-size: 0.8125rem;
    font-weight: 600;
}

.identity button {
    background: transparent;
    border: 1.5px solid rgb(255 255 255 / 45%);
    border-radius: var(--radius-sm);
    color: #fff;
    cursor: pointer;
    font: inherit;
    font-size: 0.75rem;
    font-weight: 600;
    margin-top: 6px;
    padding: 5px 12px;
}

.identity button:hover {
    background: rgb(255 255 255 / 12%);
    border-color: #fff;
}

.staff-nav {
    background: var(--surface);
    border-bottom: 1px solid var(--line);
    display: flex;
    flex-wrap: wrap;
    gap: 4px;
    padding: 0 28px;
}

.staff-nav-link {
    border-bottom: 3px solid transparent;
    color: var(--sub);
    display: inline-block;
    font-size: 0.8125rem;
    font-weight: 600;
    padding: 12px 4px;
    text-decoration: none;
}

.staff-nav-link:hover {
    color: var(--accent);
}

.staff-nav-link.active {
    border-bottom-color: var(--accent);
    color: var(--accent);
}

.main-content {
    flex: 1 1 auto;
    margin: 0 auto;
    max-width: var(--page-max);
    width: 100%;
}

.app-footer {
    border-top: 1px solid var(--line);
    color: var(--sub);
    display: flex;
    flex-wrap: wrap;
    font-size: 0.75rem;
    gap: 6px 18px;
    justify-content: space-between;
    margin: 0 auto;
    max-width: var(--page-max);
    padding: 18px 28px 24px;
    width: 100%;
}

/* ---------- Page furniture ---------- */

.page {
    display: flex;
    flex-direction: column;
    gap: 20px;
    padding: 28px 28px 60px;
}

.page-header {
    align-items: flex-end;
    display: flex;
    flex-wrap: wrap;
    gap: 18px;
    justify-content: space-between;
}

.page-header h1 {
    font-size: 1.5rem;
    margin: 0;
}

.page-header p {
    color: var(--sub);
    font-size: 0.8125rem;
    line-height: 1.5;
    margin: 4px 0 0;
    max-width: 68ch;
}

.eyebrow {
    color: var(--accent);
    font-size: 0.6875rem;
    font-weight: 700;
    letter-spacing: 0.12em;
    text-transform: uppercase;
}

.card {
    background: var(--surface);
    border: 1px solid var(--line);
    border-radius: 8px;
    /* ba-card's own drop-shadow variant — a soft brand-blue glow rather than a neutral
       grey shadow, see src/components/card/card.scss. */
    box-shadow: 0 2px 12px 0 rgb(46 92 153 / 10%);
}

.card-heading {
    align-items: baseline;
    border-bottom: 1px solid var(--line);
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
    justify-content: space-between;
    padding: 16px 24px;
}

.card-heading h2 {
    align-items: center;
    color: var(--sub);
    display: flex;
    font-size: 0.75rem;
    font-weight: 700;
    letter-spacing: 0.08em;
    margin: 0;
    text-transform: uppercase;
}

.card-body {
    padding: 24px;
}

/* ---------- Help page guide index ---------- */

.guide-toc {
    padding: 16px 20px;
}

.guide-toc ul {
    display: flex;
    flex-wrap: wrap;
    gap: 8px 20px;
    list-style: none;
    margin: 0;
    padding: 0;
}

.guide-toc a {
    color: var(--accent);
    font-size: 0.8125rem;
    font-weight: 600;
    text-decoration: none;
}

.guide-toc a:hover {
    text-decoration: underline;
}

/* ---------- Text-tips ---------- */

.tip {
    background: var(--surface-sunken);
    border: 1px solid var(--line-strong);
    border-radius: 999px;
    color: var(--sub);
    cursor: help;
    display: inline-grid;
    flex: 0 0 auto;
    font-size: 0.625rem;
    font-weight: 700;
    height: 16px;
    margin-left: 6px;
    place-items: center;
    position: relative;
    vertical-align: 1px;
    width: 16px;
}

.tip::before {
    content: "i";
    font-family: Georgia, "Times New Roman", serif;
    font-style: italic;
    line-height: 1;
}

.tip::after {
    background: var(--ba-midnight);
    border-radius: var(--radius-sm);
    bottom: calc(100% + 8px);
    box-shadow: var(--shadow-lg);
    color: #fff;
    content: attr(data-tip);
    font-family: var(--font-sans);
    font-size: 0.75rem;
    font-style: normal;
    font-weight: 400;
    left: 50%;
    letter-spacing: 0;
    line-height: 1.4;
    max-width: 260px;
    opacity: 0;
    padding: 8px 11px;
    pointer-events: none;
    position: absolute;
    text-align: left;
    text-transform: none;
    transform: translate(-50%, 4px);
    transition: opacity 120ms ease, transform 120ms ease;
    visibility: hidden;
    white-space: normal;
    width: max-content;
    z-index: 200;
}

.tip:hover,
.tip:focus-visible {
    background: var(--accent-soft);
    border-color: var(--accent);
    color: var(--accent);
}

.tip:hover::after,
.tip:focus-visible::after {
    opacity: 1;
    transform: translate(-50%, 0);
    visibility: visible;
}

/* A tip close to the right edge would otherwise push its bubble off-screen. */
.tip.tip-end::after {
    left: auto;
    right: 0;
    transform: translate(0, 4px);
}

.tip.tip-end:hover::after,
.tip.tip-end:focus-visible::after {
    transform: translate(0, 0);
}

/* BAgel's hint text is full ink colour, not muted — size alone carries the
   de-emphasis (src/tokens/form-field.tokens.js: hint.text = typography.smallPrint). */
.hint {
    color: var(--ink);
    display: block;
    font-size: 0.75rem;
    line-height: 1.45;
    margin: 0;
}

/* ---------- Forms ---------- */
/* Sized and typeset per src/tokens/form-field.tokens.js: 52px-tall fields, 9px
   radius, and label/input text set in the heading face at body size rather than a
   small bold caption — a deliberately generous, touch-friendly BAgel signature. */

.field {
    display: flex;
    flex-direction: column;
    gap: 5px;
}

.field > label,
.field-label {
    align-items: center;
    color: var(--ink);
    display: flex;
    font-family: var(--font-heading);
    font-size: 1rem;
    font-weight: 300;
    letter-spacing: 0.012em;
}

input:not([type="checkbox"], [type="radio"], [type="file"]),
select,
textarea {
    background: var(--surface);
    border: 1px solid var(--ba-grey-600);
    border-radius: 9px;
    color: var(--ink);
    font-family: var(--font-heading);
    font-size: 1rem;
    font-weight: 300;
    letter-spacing: 0.012em;
    min-height: 52px;
    padding: 0 12px;
    transition: border-color 120ms ease, box-shadow 120ms ease;
}

textarea {
    min-height: 96px;
    padding: 12px;
}

input:not([type="checkbox"], [type="radio"], [type="file"]):hover:not(:disabled),
select:hover:not(:disabled) {
    border-color: var(--accent);
}

input:not([type="checkbox"], [type="radio"], [type="file"]):focus-visible,
select:focus-visible,
textarea:focus-visible {
    border-color: var(--accent);
    box-shadow: 0 0 0 3px var(--focus);
    outline: none;
}

input:not([type="checkbox"], [type="radio"], [type="file"])[aria-invalid="true"],
select[aria-invalid="true"],
textarea[aria-invalid="true"] {
    background: var(--ba-red-200);
    border-color: var(--ba-red-500);
}

input::placeholder {
    color: #8695ab;
}

input[type="checkbox"],
input[type="radio"] {
    accent-color: var(--accent);
    block-size: 17px;
    inline-size: 17px;
    margin: 0;
}

input:disabled,
select:disabled,
textarea:disabled {
    background: var(--surface-sunken);
    color: var(--sub);
    cursor: not-allowed;
}

.number-input {
    max-width: 110px;
    text-align: center;
}

fieldset {
    border: 0;
    margin: 0;
    padding: 0;
}

legend {
    color: var(--ink);
    font-size: 0.75rem;
    font-weight: 600;
    padding: 0;
}

/* ---------- Buttons ---------- */

/* Secondary variant (ba-button default): transparent with a coloured outline, filling
   solid on hover/focus rather than tinting — see src/tokens/theme-*.tokens.js baButton. */
.button {
    align-items: center;
    background: transparent;
    border: 1.5px solid var(--accent);
    border-radius: var(--radius-sm);
    color: var(--accent);
    cursor: pointer;
    display: inline-flex;
    font: inherit;
    font-size: 0.8125rem;
    font-weight: 600;
    gap: 6px;
    justify-content: center;
    min-height: 38px;
    padding: 8px 14px;
    text-decoration: none;
    transition: background-color 120ms ease, border-color 120ms ease, box-shadow 120ms ease;
    white-space: nowrap;
}

.button:hover:not(:disabled, [aria-disabled="true"]) {
    background: var(--accent);
    border-color: var(--accent);
    color: #fff;
}

.button:disabled {
    cursor: wait;
    opacity: 0.55;
}

.button[aria-disabled="true"] {
    cursor: not-allowed;
    opacity: 0.55;
}

.button-primary {
    background: var(--accent);
    border-color: var(--accent);
    box-shadow: var(--shadow-sm);
    color: #fff;
}

.button-primary:hover:not(:disabled, [aria-disabled="true"]) {
    background: var(--accent-hover);
    border-color: var(--accent-hover);
    color: #fff;
}

.button-primary:active:not(:disabled, [aria-disabled="true"]) {
    background: var(--ba-blue-700);
    border-color: var(--ba-blue-700);
}

.button-quiet {
    background: transparent;
    border-color: var(--line);
    color: var(--sub);
}

.button-quiet:hover:not(:disabled, [aria-disabled="true"]) {
    background: var(--surface-sunken);
    border-color: var(--line-strong);
    color: var(--ink);
}

.button-danger {
    background: transparent;
    border-color: var(--ba-red-ink);
    color: var(--ba-red-ink);
}

.button-danger:hover:not(:disabled, [aria-disabled="true"]) {
    background: var(--ba-red-ink);
    border-color: var(--ba-red-ink);
    color: #fff;
}

.button-small {
    font-size: 0.75rem;
    min-height: 32px;
    padding: 5px 10px;
}

/* Real British Airways artwork, extracted from the internal "British Airways_External
   ASSET DECK.pptx" icon library (not BAgel's own bagel-icons package, still blocked). */
.button-icon-search::before,
.button-icon-edit::before,
.button-icon-delete::before {
    -webkit-mask-image: var(--btn-icon);
    -webkit-mask-position: center;
    -webkit-mask-repeat: no-repeat;
    -webkit-mask-size: contain;
    background-color: currentColor;
    content: "";
    flex-shrink: 0;
    height: 14px;
    mask-image: var(--btn-icon);
    mask-position: center;
    mask-repeat: no-repeat;
    mask-size: contain;
    width: 14px;
}

.button-icon-search {
    --btn-icon: url("../icons/search.svg");
}

.button-icon-edit {
    --btn-icon: url("../icons/edit.svg");
}

.button-icon-delete {
    --btn-icon: url("../icons/delete.svg");
}

.row-actions,
.chip-row,
.toolbar {
    align-items: center;
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
}

/* ---------- Tables ---------- */

.table-wrap {
    overflow-x: auto;
}

/* Outer rounded, coloured-border box + blue700 header rule matches ba-table's default
   theme exactly (border.color = blue700 — see src/tokens/theme-info.tokens.js and
   src/styles/_ba-table.scss); the light grey dividers between body rows are our own
   addition for scanning long lists, which plain ba-table doesn't need. */
table {
    border: 1px solid var(--ba-blue-700);
    border-collapse: separate;
    border-radius: 8px;
    border-spacing: 0;
    min-width: 680px;
    overflow: hidden;
    width: 100%;
}

th {
    background: var(--surface-sunken);
    border-bottom: 1px solid var(--ba-blue-700);
    color: var(--sub);
    font-size: 0.6875rem;
    font-weight: 700;
    letter-spacing: 0.06em;
    padding: 10px;
    text-align: left;
    text-transform: uppercase;
    white-space: nowrap;
}

thead tr:first-child th:first-child {
    border-top-left-radius: 7px;
}

thead tr:first-child th:last-child {
    border-top-right-radius: 7px;
}

td {
    border-bottom: 1px solid var(--line);
    font-size: 0.8125rem;
    padding: 12px 10px;
    vertical-align: middle;
}

tbody tr:hover td {
    background: rgb(52 104 173 / 5%);
}

tbody tr:last-child td {
    border-bottom: 0;
}

.actions-column {
    min-width: 230px;
}

.awaiting-confirmation,
.awaiting-confirmation td {
    background: var(--warning-bg);
}

/* ---------- Chips, pills and statuses ---------- */
/* Shape and sizing match ba-tag exactly (8px radius, 24px min-height, 8px inside
   padding — not a full pill): src/components/tag/tag.tokens.js. Category chips use
   the tinted "cabinClassEconomy" tag variant (blue200/blue700); status chips use the
   bold solid variants (info/success/error) — BAgel has no "warning" tag, so that one
   keeps its own bridging colour with the same shape. */

.chip {
    align-items: center;
    background: var(--ba-blue-200);
    border: 1px solid var(--ba-blue-300);
    border-radius: 8px;
    color: var(--ba-blue-700);
    display: inline-flex;
    font-family: var(--font-heading);
    font-size: 0.6875rem;
    font-weight: 600;
    letter-spacing: 0.02em;
    min-height: 24px;
    padding: 0 8px;
    white-space: nowrap;
}

.count-chips {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
    list-style: none;
    margin: 0;
    padding: 0;
}

.count-chips .chip {
    font-size: 0.75rem;
    min-height: 28px;
    padding: 0 10px;
}

.candidate-status,
.status-expected,
.status-checkedin,
.status-completed,
.status-noshow {
    align-items: center;
    background: var(--surface);
    border: 1px solid var(--line);
    border-radius: 8px;
    color: var(--ink);
    display: inline-flex;
    font-family: var(--font-heading);
    font-size: 0.75rem;
    font-weight: 600;
    min-height: 24px;
    padding: 0 8px;
    white-space: nowrap;
}

.status-new,
.status-expected {
    background: var(--accent);
    border-color: var(--accent);
    color: #fff;
}

.status-warning,
.status-checkedin {
    background: var(--warning-line);
    border-color: var(--warning-line);
    color: #fff;
}

.status-success,
.status-completed {
    background: var(--ba-green-500);
    border-color: var(--ba-green-500);
    color: #fff;
}

.status-noshow {
    background: var(--ba-red-500);
    border-color: var(--ba-red-500);
    color: #fff;
}

.status-neutral {
    background: var(--surface);
    border-color: var(--line);
    color: var(--ink);
}

.role-chip {
    align-items: center;
    background: var(--ba-blue-200);
    border: 1px solid var(--ba-blue-300);
    border-radius: 8px;
    color: var(--ba-blue-700);
    display: inline-flex;
    font-family: var(--font-heading);
    font-size: 0.6875rem;
    font-weight: 600;
    margin: 2px 4px 2px 0;
    min-height: 24px;
    padding: 0 8px;
}

/* ---------- Banners ---------- */

/* Matches ba-message: an even 1px outline + 8px radius + 16px padding, not a filled
   tint — see src/components/message/message.tokens.js. BAgel only defines error, info
   and success variants (no warning), so "warning" here keeps its own bridging colour.

   Icons for alert/tick are real British Airways artwork, extracted from the
   internal "British Airways_External ASSET DECK.pptx" icon library (not from BAgel's
   own bagel-icons package, which is still blocked — this is BA's separate general
   asset-deck icon set, confirmed as the closest visual match). "info" has no
   equivalent in that deck, so it keeps our own placeholder shape. */
:root {
    --icon-info: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'%3E%3Ccircle cx='12' cy='12' r='9' fill='none' stroke='black' stroke-width='2'/%3E%3Ccircle cx='12' cy='7.5' r='1.25' fill='black'/%3E%3Crect x='11' y='10.5' width='2' height='7' rx='1' fill='black'/%3E%3C/svg%3E");
    --icon-alert: url("../icons/alert.svg");
    --icon-tick: url("../icons/tick.svg");
    --icon-tick: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'%3E%3Ccircle cx='12' cy='12' r='9' fill='none' stroke='black' stroke-width='2'/%3E%3Cpath d='M7.5 12.5 L10.5 15.5 L16.5 8.5' fill='none' stroke='black' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'/%3E%3C/svg%3E");
}

.banner {
    background: var(--surface);
    border: 1px solid var(--line-strong);
    border-radius: 8px;
    font-size: 0.8125rem;
    line-height: 1.5;
    margin: 0;
    padding: 16px 16px 16px 48px;
    position: relative;
}

.banner::before {
    -webkit-mask-image: var(--banner-icon, none);
    -webkit-mask-position: center;
    -webkit-mask-repeat: no-repeat;
    -webkit-mask-size: contain;
    background-color: var(--banner-icon-color, currentColor);
    content: "";
    height: 20px;
    left: 16px;
    mask-image: var(--banner-icon, none);
    mask-position: center;
    mask-repeat: no-repeat;
    mask-size: contain;
    position: absolute;
    top: 16px;
    width: 20px;
}

.banner strong {
    margin-right: 4px;
}

.banner.error {
    border-color: var(--ba-red-700);
    color: var(--error-ink);
    --banner-icon: var(--icon-alert);
    --banner-icon-color: var(--ba-red-700);
}

.banner.warning {
    background: var(--warning-bg);
    border-color: var(--warning-line);
    color: var(--warning-ink);
    --banner-icon: var(--icon-alert);
    --banner-icon-color: var(--warning-line);
}

.banner.saved,
.banner.notice {
    border-color: var(--ba-green-700);
    color: var(--success-ink);
    --banner-icon: var(--icon-tick);
    --banner-icon-color: var(--ba-green-700);
}

.banner.info {
    background: var(--surface-sunken);
    border-color: var(--ba-blue-700);
    color: var(--ink);
    --banner-icon: var(--icon-info);
    --banner-icon-color: var(--ba-blue-700);
}

.callout {
    background: var(--accent-soft);
    border: 1px solid var(--accent);
    border-radius: var(--radius-sm);
    color: var(--accent);
    font-size: 0.75rem;
    line-height: 1.5;
    max-width: 72ch;
    padding: 10px 14px;
}

/* ---------- Empty and loading states ---------- */

.empty-state {
    align-items: center;
    color: var(--sub);
    display: flex;
    flex-direction: column;
    font-size: 0.8125rem;
    gap: 8px;
    padding: 34px 22px;
    text-align: center;
}

.empty-state strong {
    color: var(--ink-strong);
    font-size: 0.9375rem;
}

.empty-state p {
    margin: 0;
    max-width: 46ch;
}

.loading-block {
    display: flex;
    flex-direction: column;
    gap: 12px;
    padding: 22px;
}

.loading-line {
    animation: skeleton-pulse 1.3s ease-in-out infinite;
    background: linear-gradient(90deg, #d7dee9, #e9eef5 55%, #d7dee9);
    border-radius: var(--radius-sm);
    display: block;
    height: 12px;
    width: 82%;
}

.loading-line-short {
    width: 32%;
}

.loading-line-medium {
    width: 57%;
}

@keyframes skeleton-pulse {
    50% {
        opacity: 0.5;
    }
}

/* ---------- Audit history disclosure ---------- */

.audit-history summary {
    color: var(--accent);
    cursor: pointer;
    font-size: 0.75rem;
    font-weight: 600;
    list-style: none;
}

.audit-history summary::-webkit-details-marker {
    display: none;
}

.audit-history summary::before {
    content: "▸ ";
}

.audit-history[open] summary::before {
    content: "▾ ";
}

.audit-history[open] {
    background: var(--surface-sunken);
    border: 1px solid var(--line);
    border-radius: var(--radius-sm);
    margin: 4px 0;
    padding: 10px 12px;
}

.audit-history table {
    min-width: 460px;
}

.audit-history th,
.audit-history td {
    font-size: 0.75rem;
    padding: 6px 8px;
}

/* ---------- Landing page ---------- */

.landing {
    margin: 0 auto;
    max-width: 720px;
`````
