using EventBooking.Web;
using EventBooking.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl is not configured.");

var product = new ProductOptions(
    builder.Configuration["ProductName"] ?? "EventBooking",
    builder.Configuration["LogoPath"],
    builder.Configuration["CoordinatorContact"] ?? "events@example.org");
builder.Services.AddSingleton(product);
builder.Services.AddScoped<IMeClient, MeClient>();

#if EVENTBOOKING_E2E
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, E2EAuthenticationStateProvider>();
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddHttpClient("AuthenticatedApi", client =>
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddHttpClient("AnonymousApi", client =>
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
#else
string[] tokenScopes;
var authority = builder.Configuration["Auth:Local:Authority"]
    ?? throw new InvalidOperationException("Auth:Local:Authority is not configured.");
var clientId = builder.Configuration["Auth:Local:ClientId"] ?? "eventbooking-web";

builder.Services.AddOidcAuthentication(options =>
{
    options.ProviderOptions.Authority = authority;
    options.ProviderOptions.ClientId = clientId;
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.PostLogoutRedirectUri =
        builder.HostEnvironment.BaseAddress.TrimEnd('/') + "/authentication/logout-callback";
    options.ProviderOptions.DefaultScopes.Clear();
    options.ProviderOptions.DefaultScopes.Add("openid");
    options.ProviderOptions.DefaultScopes.Add("profile");
});

tokenScopes = ["openid", "profile"];

// The authorization message handler attaches the staff access token to every call to the API,
// and only to the API. It is a delegating handler with no transport of its own, so it must
// wrap the browser fetch handler explicitly — without an inner handler every call throws
// net_http_handler_not_assigned before leaving the page.
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthorizationMessageHandler>();
    handler.InnerHandler = new HttpClientHandler();
    handler.ConfigureHandler([apiBaseUrl], tokenScopes);

    return new HttpClient(handler) { BaseAddress = new Uri(apiBaseUrl) };
});
#endif

builder.Services.AddScoped<IEventsClient, EventsClient>();
builder.Services.AddScoped<IEventGroupsClient, EventGroupsClient>();
builder.Services.AddScoped<IAttendeesClient, AttendeesClient>();
builder.Services.AddScoped<AdminClient>();
builder.Services.AddScoped<StaffAccessClient>();
builder.Services.AddScoped<IDashboardsClient, DashboardsClient>();
builder.Services.AddScoped<IAuditClient, AuditClient>();
builder.Services.AddScoped<IAppointmentsClient, AppointmentsClient>();
builder.Services.AddScoped<IUserGuideCatalog, UserGuideCatalog>();

// Attendees authorise with the single-use token in their URL. This plain named client must never
// use AuthorizationMessageHandler, which would attach a staff access token and start sign-in.
#if EVENTBOOKING_E2E
builder.Services.AddHttpClient(BookingClient.ClientName, client =>
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
#else
builder.Services.AddHttpClient(BookingClient.ClientName, client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});
#endif
builder.Services.AddScoped<IBookingClient>(services => new BookingClient(
    services.GetRequiredService<IHttpClientFactory>().CreateClient(BookingClient.ClientName)));
builder.Services.AddSingleton(new AttendeePageOptions(
    builder.Configuration["CoordinatorContact"] ?? "the recruitment team"));

await builder.Build().RunAsync();
