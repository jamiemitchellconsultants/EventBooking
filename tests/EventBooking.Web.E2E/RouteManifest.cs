namespace EventBooking.Web.E2E;

public sealed record RouteCase(
    string Name, string Path, string FixtureState = "ready", string? SetupAction = null);

public static class RouteManifest
{
    public static IReadOnlyList<RouteCase> All { get; } =
    [
        new("home-ready", "/"),
        new("help-ready", "/help"),
        new("not-found", "/route-that-does-not-exist"),
        new("admin-locations-ready", "/admin/locations"),
        new("admin-locations-empty", "/admin/locations", "locations-empty"),
        new("admin-locations-edit", "/admin/locations", "ready", "locations-edit"),
        new("admin-locations-conflict", "/admin/locations", "locations-conflict", "locations-save"),
        new("admin-appointment-types-ready", "/admin/appointment-types"),
        new("admin-attendee-groups-ready", "/admin/attendee-groups"),
        new("admin-attendee-groups-confirm", "/admin/attendee-groups", "ready", "group-confirm"),
        new("admin-settings-ready", "/admin/settings"),
        new("admin-staff-access-ready", "/admin/staff-access"),
        new("reference-data-read-only", "/admin/locations", "reference-read-only"),
    ];
}
