using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the deterministic union of staff navigation links per role combination.</summary>
public class StaffNavigationTests
{
    /// <summary>Gets every valid role shape with its exact expected link union.</summary>
    public static TheoryData<string[], string[]> EveryValidShape => new()
    {
        { ["Admin"], ["/settings", "/staff-access", "/events/operations", "/audit"] },
        { ["Coordinator"], ["/attendees", "/dashboards", "/events/operations", "/audit"] },
        { ["Manager"], ["/events/negotiate", "/appointments"] },
        { ["AppointmentStaff"], ["/appointments"] },
        { ["Manager", "Coordinator"], ["/events/negotiate", "/appointments", "/attendees", "/dashboards", "/events/operations", "/audit"] },
        { ["Coordinator", "AppointmentStaff"], ["/appointments", "/attendees", "/dashboards", "/events/operations", "/audit"] },
        { ["Manager", "AppointmentStaff"], ["/events/negotiate", "/appointments"] },
        { ["Manager", "Coordinator", "AppointmentStaff"], ["/events/negotiate", "/appointments", "/attendees", "/dashboards", "/events/operations", "/audit"] },
    };

    /// <summary>Verifies every valid profile shape receives its exact link union.</summary>
    [Theory]
    [MemberData(nameof(EveryValidShape))]
    public void EveryValidProfileShapeGetsItsExactLinkUnion(
        string[] roles,
        string[] expectedRoutes)
    {
        var links = StaffNavigation.LinksFor(new MeDto(
            roles,
            roles.Contains("Manager") || roles.Contains("AppointmentStaff")
                ? Guid.NewGuid()
                : null,
            null));

        Assert.Equal(expectedRoutes, links.Select(link => link.Href));
    }

    /// <summary>Verifies administrators keep only administrative and event-only links.</summary>
    [Fact]
    public void AdminHasOnlyAdministrativeAndEventOnlyLinks()
    {
        var links = StaffNavigation.LinksFor(new MeDto(["Admin"], null, null));

        Assert.Equal(
            ["/settings", "/staff-access", "/events/operations", "/audit"],
            links.Select(link => link.Href));
        Assert.DoesNotContain(links, link => link.Href is "/attendees" or "/dashboards");
    }

    /// <summary>Verifies the Staff access link describes scope assignment, since roles come from the identity provider.</summary>
    [Fact]
    public void StaffAccessLinkDescribesScopeAssignmentNotRoleAssignment()
    {
        var staffAccess = StaffNavigation.LinksFor(new MeDto(["Admin"], null, null))
            .Single(link => link.Href == "/staff-access");

        Assert.Equal("Set appointment-type scope", staffAccess.Description);
    }

    /// <summary>Verifies a coordinator-manager keeps the union without duplicates.</summary>
    [Fact]
    public void CoordinatorManagerGetsTheUnionWithoutDuplicates()
    {
        var links = StaffNavigation.LinksFor(new MeDto(
            ["Manager", "Coordinator"],
            Guid.NewGuid(),
            "Medical Check-up"));

        Assert.Equal(
            ["/events/negotiate", "/appointments", "/attendees", "/dashboards", "/events/operations", "/audit"],
            links.Select(link => link.Href));
        Assert.Equal(links.Count, links.Select(link => link.Href).Distinct().Count());
    }

    /// <summary>Verifies appointment-only staff see only the appointment workspace.</summary>
    [Fact]
    public void AppointmentStaffHasOnlyTheAppointmentWorkspace()
    {
        var links = StaffNavigation.LinksFor(new MeDto(
            ["AppointmentStaff"], Guid.NewGuid(), "Uniform Fitting"));

        Assert.Equal(["/appointments"], links.Select(link => link.Href));
    }

    /// <summary>Verifies empty roles receive no links.</summary>
    [Fact]
    public void EmptyRolesHaveNoLinks()
    {
        Assert.Empty(StaffNavigation.LinksFor(new MeDto([], null, null)));
    }

    /// <summary>Verifies the audit trail is offered only to the roles that may search it.</summary>
    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Coordinator", true)]
    [InlineData("Manager", false)]
    [InlineData("AppointmentStaff", false)]
    public void TheAuditTrailIsOfferedOnlyToRolesThatMaySearchIt(string role, bool expected)
    {
        var links = StaffNavigation.LinksFor(new MeDto(
            [role],
            role is "Manager" or "AppointmentStaff" ? Guid.NewGuid() : null,
            null));

        Assert.Equal(expected, links.Any(link => link.Href == "/audit"));
    }
}
