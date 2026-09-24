using System.Net;
using System.Threading.RateLimiting;
using EventBooking.Api;
using EventBooking.Api.Auth;
using EventBooking.Api.Endpoints;
using EventBooking.Api.Idempotency;
using EventBooking.Api.Observability;
using EventBooking.Api.OpenApi;
using EventBooking.Api.Pagination;
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

const string WebClientCorsPolicy = "web-client";
var settings = EventBookingConfiguration.Read(builder.Configuration);

builder.Logging.ClearProviders();
// Scopes are not rendered, and the hosting category is raised to Warning. Both are about the
// same leak: ASP.NET Core logs "Request starting … /api/booking/<token>" at Information and
// puts that same raw path on its request scope, and design 08 forbids a token in a log line.
// The application cannot redact a message the framework formats, so the message is silenced
// and the scope is not printed; the correlation line below carries the route template, the
// status and the correlation identifier, which is what the Information level was for.
builder.Logging.AddJsonConsole(options => options.IncludeScopes = false);
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);

builder.Services.AddEventBookingInfrastructure(
    settings.ConnectionString, settings.Clock, settings.Tokens);
builder.Services.AddLocalEmailTransport(settings.Email, settings.Smtp);
builder.Services.AddEventBookingApplication(
    settings.Portal,
    new EventBooking.Application.Access.StaffIdPolicy(builder.Configuration["Identity:StaffIdPattern"]));
builder.Services.AddEventBookingAuth(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddEventBookingOpenApi();

builder.Services.AddSingleton(settings);
builder.Services.Configure<RateLimitSettings>(options =>
{
    options.AttendeePerMinute = settings.RateLimits.AttendeePerMinute;
    options.TokenPerMinute = settings.RateLimits.TokenPerMinute;
    options.StaffPerMinute = settings.RateLimits.StaffPerMinute;
});
builder.Services.AddSingleton<ICorrelationContext, AsyncLocalCorrelationContext>();
builder.Services.AddSingleton<EventBookingMetrics>();
builder.Services.AddSingleton<PrometheusText>();
builder.Services.AddSingleton(new PageCursor(Encoding.UTF8.GetBytes(settings.Tokens.SigningKey)));
// The session advisory lock needs a dedicated pooled connection that lives through the handler;
// registering the data source also lets the container dispose that pool on shutdown.
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(settings.ConnectionString));
builder.Services.AddSingleton<IdempotencyKeyLock>();
builder.Services.AddScoped<IIdempotencyStore,
    EventBooking.Infrastructure.Persistence.Idempotency.IdempotencyStore>();
builder.Services.AddScoped<EventBooking.Api.Contracts.CallerCapabilities>();

builder.Services.AddCors(options => options.AddPolicy(WebClientCorsPolicy, policy => policy
    .WithOrigins([.. settings.AllowedOrigins])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Content-Disposition", CorrelationMiddleware.HeaderName)));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // The global limiter has no policy of its own, so without this its rejections would be
    // a bare 429 with no body and no Retry-After. Endpoint policies keep their own writer.
    options.OnRejected = (context, ct) => RateLimitRejection.WriteAsync(
        context, RemoteIpRateLimiterPolicy.PolicyName, ct);
    // Global and endpoint limiters are composed by ASP.NET. The address policy is global only
    // for attendee-token routes; their endpoint metadata selects the token-prefix policy.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        AttendeeRouteRateLimiter.Partition(context, settings.RateLimits));
    options.AddPolicy<string, RemoteIpRateLimiterPolicy>(RemoteIpRateLimiterPolicy.PolicyName);
    options.AddPolicy<string, TokenPrefixRateLimiterPolicy>(TokenPrefixRateLimiterPolicy.PolicyName);
    options.AddPolicy<string, StaffRateLimiterPolicy>(StaffRateLimiterPolicy.PolicyName);
});

builder.Services.AddHostedService<InviteSweepService>();
builder.Services.AddSingleton<OutboxDispatcher>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<OutboxDispatcher>());

var app = builder.Build();

// The scrape listener must exist before the first request it should observe: a lazily
// created singleton would miss every measurement taken before the first /metrics call,
// and the scrape request's own measurement only records after the endpoint renders.
app.Services.GetRequiredService<PrometheusText>();

// Forwarded headers are opt-in. Clearing both trust lists with no replacement makes ASP.NET
// accept a header from every peer, so a deployment without a proxy keeps its socket address.
if (settings.ProxyNetworks.Count > 0)
{
    var forwarded = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        ForwardLimit = 1,
    };
    forwarded.KnownIPNetworks.Clear();
    forwarded.KnownProxies.Clear();
    foreach (var network in settings.ProxyNetworks)
    {
        var parts = network.Split('/');
        if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var prefix) &&
            int.TryParse(parts[1], out var length))
        {
            forwarded.KnownIPNetworks.Add(new System.Net.IPNetwork(prefix, length));
        }
        else if (IPAddress.TryParse(network, out var proxy))
        {
            forwarded.KnownProxies.Add(proxy);
        }
        else
        {
            throw new InvalidOperationException($"ProxyNetworks contains invalid entry '{network}'.");
        }
    }

    app.UseForwardedHeaders(forwarded);
}
app.UseMiddleware<CorrelationMiddleware>();

app.UseMiddleware<RequestMetricsMiddleware>();

app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous().DisableRateLimiting();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "EventBooking API v1";
    options.SwaggerEndpoint("/openapi/v1.json", "EventBooking API v1");
    options.DisplayOperationId();
    options.HeadContent = "<link rel=\"alternate\" type=\"application/json\" href=\"/openapi/v1.json\" />";
});

app.UseCors(WebClientCorsPolicy);
app.UseMiddleware<ForbiddenResponseMiddleware>();
app.UseAuthentication();
app.UseMiddleware<StaffIdentityRecorder>();
app.UseAuthorization();
app.UseRateLimiter();

// After authentication, because a key is scoped to the caller it was issued under.
app.UseMiddleware<IdempotencyMiddleware>();

app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous().DisableRateLimiting().WithAgentMetadata("getLiveness");

app.MapGet("/health/ready", async (EventBookingDbContext context, CancellationToken ct) =>
    await context.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "ok", checks = new[] { new { name = "database", status = "ok" } } })
        : Results.Json(
            new { status = "unavailable", checks = new[] { new { name = "database", status = "failed" } } },
            statusCode: StatusCodes.Status503ServiceUnavailable))
    .AllowAnonymous().DisableRateLimiting().WithAgentMetadata("getReadiness");

app.MapGet("/metrics", (PrometheusText metrics) =>
    Results.Text(metrics.Render(), "text/plain"))
    .AllowAnonymous().DisableRateLimiting().WithAgentMetadata("getMetrics");

app.MapApiDiscoveryEndpoints();
app.MapMeEndpoints();

app.MapLocationEndpoints();
app.MapAppointmentTypeEndpoints();
app.MapAttendeeGroupEndpoints();
app.MapSettingsEndpoints();
app.MapStaffAccessEndpoints();

app.MapEventProposalEndpoints();
app.MapEventEndpoints();

app.MapAttendeeEndpoints();
app.MapDashboardEndpoints();
app.MapAuditEndpoints();
app.MapAppointmentWorkspaceEndpoints();

app.MapBookingEndpoints();
app.MapManageEndpoints();

app.Run();

/// <summary>Named so the integration test factory can start this host.</summary>
public partial class Program;
