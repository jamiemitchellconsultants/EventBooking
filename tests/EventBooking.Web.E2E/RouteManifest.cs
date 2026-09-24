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
    ];
}
