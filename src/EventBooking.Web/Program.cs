using EventBooking.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl is not configured.");
var transitionalLocationTimeZoneId = builder.Configuration["TransitionalLocationTimeZoneId"]
    ?? throw new InvalidOperationException("TransitionalLocationTimeZoneId is not configured.");

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

builder.Services.AddScoped<EventBooking.Web.Services.EventsClient>();
builder.Services.AddScoped<EventBooking.Web.Services.AttendeesClient>();
builder.Services.AddScoped<EventBooking.Web.Services.AdminClient>();
builder.Services.AddScoped<EventBooking.Web.Services.StaffAccessClient>();
builder.Services.AddScoped<EventBooking.Web.Services.DashboardsClient>();
builder.Services.AddScoped<EventBooking.Web.Services.AuditClient>();
builder.Services.AddScoped<EventBooking.Web.Services.MeClient>();
builder.Services.AddScoped<EventBooking.Web.Services.AppointmentsClient>();
builder.Services.AddSingleton(new EventBooking.Web.Services.TransitionalLocationTimePresentation(transitionalLocationTimeZoneId));
builder.Services.AddSingleton(new EventBooking.Web.Services.TransitionalLocationPageClock(transitionalLocationTimeZoneId));

// Attendees authorise with the single-use token in their URL. This plain named client must never
// use AuthorizationMessageHandler, which would attach a staff access token and start sign-in.
builder.Services.AddHttpClient(EventBooking.Web.Services.BookingClient.ClientName, client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});
builder.Services.AddScoped(sp => new EventBooking.Web.Services.BookingClient(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient(EventBooking.Web.Services.BookingClient.ClientName)));
builder.Services.AddSingleton(new EventBooking.Web.Services.AttendeePageOptions(
    builder.Configuration["CoordinatorContact"] ?? "the recruitment team"));

await builder.Build().RunAsync();
