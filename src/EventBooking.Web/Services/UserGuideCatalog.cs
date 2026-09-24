namespace EventBooking.Web.Services;

public sealed record UserGuide(string RoleKey, string Title, string AssetPath);

public interface IUserGuideCatalog
{
    Task<IReadOnlyList<UserGuide>> ForAsync(
        bool authenticated, IReadOnlyCollection<string> roles, CancellationToken ct);
    Task<string> ReadMarkdownAsync(UserGuide guide, CancellationToken ct);
}

/// <summary>
/// Serves the role guides as static bundle assets under wwwroot/help. A signed-in user sees the
/// guide for every role they hold; an anonymous visitor sees only the attendee guide.
/// </summary>
public sealed class UserGuideCatalog(HttpClient http, Microsoft.AspNetCore.Components.NavigationManager navigation)
    : IUserGuideCatalog
{
    private static readonly IReadOnlyDictionary<string, UserGuide> Guides =
        new Dictionary<string, UserGuide>(StringComparer.OrdinalIgnoreCase)
        {
            ["Admin"] = new("Admin", "Administrator guide", "help/admin.md"),
            ["Coordinator"] = new("Coordinator", "Coordinator guide", "help/coordinator.md"),
            ["Manager"] = new("Manager", "Manager guide", "help/manager.md"),
            ["AppointmentStaff"] = new("AppointmentStaff", "Appointment staff guide", "help/appointment-staff.md"),
            ["Attendee"] = new("Attendee", "Attendee guide", "help/attendee.md"),
        };

    private static readonly string[] RoleOrder =
        ["Admin", "Coordinator", "Manager", "AppointmentStaff"];

    public Task<IReadOnlyList<UserGuide>> ForAsync(
        bool authenticated, IReadOnlyCollection<string> roles, CancellationToken ct)
    {
        if (!authenticated)
            return Task.FromResult<IReadOnlyList<UserGuide>>([Guides["Attendee"]]);
        var held = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);
        var guides = RoleOrder
            .Where(held.Contains)
            .Select(role => Guides[role])
            .ToArray();
        return Task.FromResult<IReadOnlyList<UserGuide>>(guides);
    }

    // The guides ship with the app bundle, not the API: the path is resolved against
    // the app's own origin so the API base address on the shared client is ignored.
    public Task<string> ReadMarkdownAsync(UserGuide guide, CancellationToken ct) =>
        http.GetStringAsync(new Uri(new Uri(navigation.BaseUri), guide.AssetPath), ct);
}
