using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the role-to-guide mapping bundled from docs/user-guides.</summary>
public class UserGuideCatalogTests
{
    [Fact]
    public void AnyRecognisedRoleGetsEveryGuideIncludingAttendee()
    {
        var guides = UserGuideCatalog.GuidesFor(["Admin"]);

        Assert.Equal(
            ["Admin", "Manager", "AppointmentStaff", "Coordinator", "Attendee"],
            guides.Select(g => g.RoleKey));
    }

    [Fact]
    public void CombinedRolesStillGetEveryGuideNotJustTheirOwn()
    {
        var guides = UserGuideCatalog.GuidesFor(["Coordinator", "Manager", "AppointmentStaff"]);

        Assert.Equal(
            ["Admin", "Manager", "AppointmentStaff", "Coordinator", "Attendee"],
            guides.Select(g => g.RoleKey));
    }

    [Fact]
    public void SingleRoleGetsEveryGuideNotJustItsOwn()
    {
        var guides = UserGuideCatalog.GuidesFor(["Coordinator"]);

        Assert.Equal(
            ["Admin", "Manager", "AppointmentStaff", "Coordinator", "Attendee"],
            guides.Select(g => g.RoleKey));
        Assert.Contains(guides, g => g.RoleKey == "Coordinator" && g.Html.Contains("Attendees"));
    }

    [Fact]
    public void MissingOrUnrecognisedRolesGetNoGuide()
    {
        Assert.Empty(UserGuideCatalog.GuidesFor([]));
        Assert.Empty(UserGuideCatalog.GuidesFor(["SomeFutureRole"]));
    }

    [Fact]
    public void EachGuideRendersMarkdownHeadingsAsHtml()
    {
        var guide = UserGuideCatalog.GuidesFor(["Admin"]).Single(g => g.RoleKey == "Admin");

        Assert.Contains("<h1", guide.Html);
        Assert.Contains("<h2", guide.Html);
    }

    [Fact]
    public void CrossGuideLinksAreRewrittenAwayFromBareMarkdownFilenames()
    {
        var manager = UserGuideCatalog.GuidesFor(["Manager"]).Single(g => g.RoleKey == "Manager");

        Assert.DoesNotContain("appointment-staff-guide.md", manager.Html);
        Assert.DoesNotContain("README.md", manager.Html);
        Assert.DoesNotContain("All user guides", manager.Html);
    }

    [Fact]
    public void CrossGuideAnchorLinksCarryTheHelpPathSoTheyDoNotResolveAgainstBaseHref()
    {
        // Blazor's <base href="/"> resolves a bare "#anchor" against "/" (Home), not against the
        // current route, so every in-page cross-link must be an absolute "/help#..." path.
        var manager = UserGuideCatalog.GuidesFor(["Manager"]).Single(g => g.RoleKey == "Manager");

        Assert.Contains("href=\"/help#appointmentstaff\"", manager.Html);
    }

    [Fact]
    public void AttendeeGuideIsAvailableOutsideTheRoleMap()
    {
        var guide = UserGuideCatalog.AttendeeGuide();

        Assert.Equal("Attendee", guide.RoleKey);
        Assert.Contains("<h1", guide.Html);
        Assert.DoesNotContain("README.md", guide.Html);
    }
}
