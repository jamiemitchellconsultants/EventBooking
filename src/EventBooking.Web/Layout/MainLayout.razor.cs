using Microsoft.AspNetCore.Components;
using EventBooking.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace EventBooking.Web.Layout;

public partial class MainLayout
{
    [Inject] private IMeClient Me { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private AuthenticationStateProvider Auth { get; set; } = default!;
    [Inject] private ProductOptions Product { get; set; } = default!;

    private ApiOutcome<MeDto>? _me;

    protected override async Task OnInitializedAsync()
    {
        Auth.AuthenticationStateChanged += OnAuthenticationStateChanged;
        await LoadMeAsync();
    }

    // Right after the OIDC login redirect completes, the first fetch here can still race
    // the provider and see an unauthenticated user. Without this subscription that miss is
    // permanent: OnInitializedAsync runs once, so the staff nav would never appear.
    private async void OnAuthenticationStateChanged(Task<AuthenticationState> task)
    {
        var state = await task;
        if (state.User.Identity?.IsAuthenticated == true && _me is not { IsSuccess: true })
        {
            await LoadMeAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task LoadMeAsync()
    {
        var state = await Auth.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        // The authorization handler throws rather than returning a response when no token is
        // available yet, and the network can throw too. Either must not escape: this runs from
        // an async void handler, where an exception would take down the whole app.
        try
        {
            _me = await Me.GetAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            _me = ApiOutcome<MeDto>.Failure("Something went wrong. Please try again.");
        }
    }

    private void SignOut() => Navigation.NavigateToLogout("authentication/logout");

    public void Dispose() => Auth.AuthenticationStateChanged -= OnAuthenticationStateChanged;
}
