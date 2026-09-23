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
