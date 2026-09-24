using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public sealed class StaffNavigationTests
{
    public static TheoryData<string[], string[]> EveryValidShape => new()
    {
        { ["Admin"], ["/admin/locations", "/admin/appointment-types", "/admin/attendee-groups", "/admin/settings", "/admin/staff-access", "/events/operations", "/audit"] },
        { ["Coordinator"], ["/attendees", "/dashboards", "/events/operations", "/audit", "/admin/locations", "/admin/appointment-types", "/admin/attendee-groups"] },
        { ["Manager"], ["/events/negotiate", "/appointments", "/admin/locations", "/admin/appointment-types", "/admin/attendee-groups"] },
        { ["AppointmentStaff"], ["/appointments"] },
        { ["Manager", "Coordinator"], ["/events/negotiate", "/appointments", "/attendees", "/dashboards", "/events/operations", "/audit", "/admin/locations", "/admin/appointment-types", "/admin/attendee-groups"] },
        { ["Coordinator", "AppointmentStaff"], ["/appointments", "/attendees", "/dashboards", "/events/operations", "/audit", "/admin/locations", "/admin/appointment-types", "/admin/attendee-groups"] },
        { ["Manager", "AppointmentStaff"], ["/events/negotiate", "/appointments", "/admin/locations", "/admin/appointment-types", "/admin/attendee-groups"] },
        { ["Manager", "Coordinator", "AppointmentStaff"], ["/events/negotiate", "/appointments", "/attendees", "/dashboards", "/events/operations", "/audit", "/admin/locations", "/admin/appointment-types", "/admin/attendee-groups"] },
    };

    [Theory]
    [MemberData(nameof(EveryValidShape))]
    public void EveryValidProfileShapeGetsTheCompleteRouteUnion(
        string[] roles,
        string[] expectedRoutes)
    {
        var links = StaffNavigation.LinksFor(new MeDto(
            roles,
            roles.Any(role => role is "Manager" or "AppointmentStaff") ? Guid.NewGuid() : null,
            null));

        Assert.Equal(expectedRoutes, links.Select(link => link.Href));
        Assert.Equal(links.Count, links.Select(link => link.Href).Distinct().Count());
    }

    [Fact]
    public void EmptyRolesHaveNoLinks() =>
        Assert.Empty(StaffNavigation.LinksFor(new MeDto([], null, null)));
}
