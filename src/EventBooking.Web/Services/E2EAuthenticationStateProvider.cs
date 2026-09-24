#if EVENTBOOKING_E2E
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace EventBooking.Web.Services;

public sealed class E2EAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly string[] DefaultRoles = ["Coordinator", "Manager", "AppointmentStaff"];
    private readonly NavigationManager _navigation;

    public E2EAuthenticationStateProvider(NavigationManager navigation) => _navigation = navigation;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var query = new Uri(_navigation.Uri).Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .FirstOrDefault(part => part[0] == "e2eRoles");
        var value = query is { Length: 2 } ? Uri.UnescapeDataString(query[1]) : null;
        if (string.Equals(value, "anonymous", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

        var roles = value is null ? DefaultRoles : value.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var claims = new[] { new Claim(ClaimTypes.Name, "E2E staff member") }
            .Concat(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return Task.FromResult(new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity(claims, "E2E"))));
    }
}
#endif
