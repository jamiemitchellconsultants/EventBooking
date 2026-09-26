namespace EventBooking.Web.Services;

/// <summary>Describes one staff navigation destination.</summary>
/// <param name="Href">The route.</param>
/// <param name="Label">The visible label.</param>
/// <param name="Description">The accessible description.</param>
public sealed record StaffLink(string Href, string Label, string Description);

/// <summary>Builds the deterministic union of links permitted by the caller's roles.</summary>
public static class StaffNavigation
{
    /// <summary>Returns the complete ordered link union for one staff identity.</summary>
    /// <param name="me">The authenticated staff identity.</param>
    /// <returns>The role union with no duplicate route.</returns>
    public static IReadOnlyList<StaffLink> LinksFor(MeDto me)
    {
        var roles = me.Roles.ToHashSet(StringComparer.Ordinal);
        if (roles.Contains("Admin"))
        {
            return
            [
                new("/admin/locations", "Locations", "Manage event sites and time zones"),
                new("/admin/appointment-types", "Appointment types", "Manage the types events may offer"),
                new("/admin/attendee-groups", "Attendee groups", "Manage requirement mappings"),
                new("/admin/settings", "System settings", "Configure future invitations"),
                new("/admin/staff-access", "Staff access", "Set appointment-type scope"),
                new("/event-groups", "Event groups", "Publish compatible events for self-registration"),
                new("/events/operations", "Event operations", "Review and cancel future events"),
                new("/audit", "Audit search", "Search event and administration history"),
            ];
        }

        var links = new List<StaffLink>();
        if (roles.Contains("Manager"))
            links.Add(new("/events/negotiate", "Negotiation board", "Propose and agree event windows"));
        if (roles.Contains("Manager") || roles.Contains("AppointmentStaff"))
            links.Add(new("/appointments", "Appointment workspace", "Run your appointment roster"));
        if (roles.Contains("Coordinator"))
        {
            links.Add(new("/attendees", "Attendees", "Invite and track attendees"));
            links.Add(new("/dashboards", "Dashboards", "Review waiting lists and follow-ups"));
            links.Add(new("/events/operations", "Event operations", "Review and cancel future events"));
            links.Add(new("/event-groups", "Event groups", "Publish compatible events for self-registration"));
            links.Add(new("/audit", "Audit search", "Search event and administration history"));
        }

        if (roles.Contains("Coordinator") || roles.Contains("Manager"))
        {
            links.Add(new("/admin/locations", "Locations", "Read event sites and time zones"));
            links.Add(new("/admin/appointment-types", "Appointment types", "Read the types events may offer"));
            links.Add(new("/admin/attendee-groups", "Attendee groups", "Read requirement mappings"));
        }

        return links;
    }
}
