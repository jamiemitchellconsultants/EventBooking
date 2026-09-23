using System.Net;
using System.Text;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>Verifies disabled seed modes cannot parse Keycloak settings or create HTTP transport,
/// and that a --reseed run wires the realm delete-and-recreate step correctly.</summary>
public sealed class KeycloakSeedStepTests
{
    private static readonly Dictionary<string, string?> CompleteSettings = new()
    {
        ["Keycloak__BaseUrl"] = "https://auth.example.test/",
        ["Keycloak__AdminUsername"] = "seed-admin",
        ["Keycloak__AdminPassword"] = "admin-secret",
        ["Keycloak__DemoPassword"] = "demo-secret",
    };

    /// <summary>Verifies absent configuration performs no provider work.</summary>
    [Fact]
    public async Task AbsentConfigurationDoesNotCreateAnHttpClient()
    {
        var clientsCreated = 0;
        var step = new KeycloakSeedStep(
            _ => null,
            () =>
            {
                clientsCreated++;
                return new HttpClient();
            });

        var result = await step.RunAsync(
            skipSeed: false, reseed: false, DemoSeedSpec.Staff(), CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, clientsCreated);
    }

    /// <summary>Verifies --skip-seed bypasses even a partial, otherwise-invalid configuration.</summary>
    [Fact]
    public async Task SkipSeedDoesNotParseSettingsOrCreateAnHttpClient()
    {
        var settingsRead = 0;
        var clientsCreated = 0;
        var step = new KeycloakSeedStep(
            _ =>
            {
                settingsRead++;
                return "partial";
            },
            () =>
            {
                clientsCreated++;
                return new HttpClient();
            });

        var result = await step.RunAsync(
            skipSeed: true, reseed: false, DemoSeedSpec.Staff(), CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, settingsRead);
        Assert.Equal(0, clientsCreated);
    }

    /// <summary>Verifies a reseed without a configured realm export path fails before any HTTP
    /// client or file is touched, naming the missing setting.</summary>
    [Fact]
    public async Task ReseedWithoutRealmExportPathThrowsWithoutCreatingAnHttpClientOrReadingAFile()
    {
        var clientsCreated = 0;
        var filesRead = 0;
        var step = new KeycloakSeedStep(
            name => CompleteSettings.GetValueOrDefault(name),
            () =>
            {
                clientsCreated++;
                return new HttpClient();
            },
            _ =>
            {
                filesRead++;
                return "{}";
            });

        var exception = await Assert.ThrowsAsync<SeedException>(() =>
            step.RunAsync(skipSeed: false, reseed: true, DemoSeedSpec.Staff(), CancellationToken.None));

        Assert.Contains("Keycloak__RealmExportPath", exception.Message);
        Assert.Equal(0, clientsCreated);
        Assert.Equal(0, filesRead);
    }

    /// <summary>Verifies a plain (non-reseed) run never deletes or recreates the realm, even when a
    /// realm export path is configured.</summary>
    [Fact]
    public async Task PlainRunNeverCallsRealmDeleteOrCreate()
    {
        var settings = new Dictionary<string, string?>(CompleteSettings)
        {
            ["Keycloak__RealmExportPath"] = "/config/eventbooking-realm.json",
        };
        var handler = new RecordingHandler();
        handler.Enqueue(HttpStatusCode.OK, "{\"access_token\":\"token\"}");
        handler.Enqueue(HttpStatusCode.NotFound); // client lookup fails fast in this minimal script
        var step = new KeycloakSeedStep(
            name => settings.GetValueOrDefault(name),
            () => new HttpClient(handler),
            _ => throw new InvalidOperationException("A plain run must not read the realm export file."));

        await Assert.ThrowsAsync<SeedException>(() =>
            step.RunAsync(skipSeed: false, reseed: false, DemoSeedSpec.Staff(), CancellationToken.None));

        Assert.DoesNotContain(handler.Requests, request =>
            request.Method == HttpMethod.Delete || request.Path == "/admin/realms");
    }

    private sealed record RequestRecord(HttpMethod Method, string Path);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> responses = new();
        public List<RequestRecord> Requests { get; } = [];

        public void Enqueue(HttpStatusCode status, string body = "") => responses.Enqueue((status, body));

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RequestRecord(request.Method, request.RequestUri!.PathAndQuery));
            var (status, body) = responses.Count > 0
                ? responses.Dequeue()
                : (HttpStatusCode.NotFound, string.Empty);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
