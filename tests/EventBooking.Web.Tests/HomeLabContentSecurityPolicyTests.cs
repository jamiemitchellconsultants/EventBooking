namespace EventBooking.Web.Tests;

// The Blazor sign-in library checks for an existing Keycloak session in a hidden iframe at every
// page load. If the policy blocks that iframe the app waits for the library's 10 second timeout
// before it renders anything, so the framing rules below are load-time behaviour, not hardening.
public sealed class HomeLabContentSecurityPolicyTests
{
    private static readonly string Policy = ReadPolicy();

    [Fact]
    public void KeycloakMayBeFramedForSilentSignIn()
    {
        Assert.Contains("frame-src 'self' {$EVENTBOOKING_KEYCLOAK_URL}", Policy, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSilentSignInCallbackMayBeFramedBySameOriginPagesOnly()
    {
        Assert.Contains("frame-ancestors 'self'", Policy, StringComparison.Ordinal);
        Assert.DoesNotContain("frame-ancestors *", Policy, StringComparison.Ordinal);
    }

    [Fact]
    public void ScriptsRemainRestrictedToSelfAndTheImportMapHash()
    {
        Assert.Contains("script-src 'self' 'wasm-unsafe-eval' 'sha256-__IMPORTMAP_HASH__'", Policy, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-inline", Policy.Split("script-src", 2)[1].Split(';', 2)[0], StringComparison.Ordinal);
    }

    private static string ReadPolicy()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "EventBooking.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var caddyfile = File.ReadAllText(
            Path.Combine(directory!.FullName, "deploy", "home-lab", "web", "Caddyfile"));
        var line = caddyfile.Split('\n').Single(x => x.Contains("Content-Security-Policy", StringComparison.Ordinal));
        return line;
    }
}
