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
