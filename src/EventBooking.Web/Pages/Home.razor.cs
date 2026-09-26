using Microsoft.AspNetCore.Components;
using EventBooking.Web.Services;

namespace EventBooking.Web.Pages;

public partial class Home
{
    // Fetched once by MainLayout and cascaded here, rather than fetched again by this page —
    // two concurrent calls to the same token-protected endpoint on first load raced the WASM
    // auth token acquisition and intermittently failed one of them.
    [CascadingParameter]
    private ApiOutcome<MeDto>? MeOutcome { get; set; }

    private bool Loading => MeOutcome is null;
    private string? Error => MeOutcome is { IsSuccess: false } ? MeOutcome.ErrorMessage : null;
    private MeDto? Me => MeOutcome?.Value;

    private IReadOnlyList<StaffLink> Links => Me is null ? [] : StaffNavigation.LinksFor(Me);
    private IReadOnlyList<StaffLink> WorkLinks => Links.Where(x => !IsReference(x)).ToList();
    private IReadOnlyList<StaffLink> ReferenceLinks => Links.Where(IsReference).ToList();
    private bool IsAdmin => Me?.Roles.Contains("Admin") == true;

    // Admin screens are settings for an Admin and read-only reference lists for everyone else.
    private static bool IsReference(StaffLink link) => link.Href.StartsWith("/admin/", StringComparison.Ordinal);
}
