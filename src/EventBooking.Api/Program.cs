using System.Threading.RateLimiting;
using EventBooking.Api;
using EventBooking.Api.Auth;
using EventBooking.Api.Endpoints;
using EventBooking.Application;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using EventBooking.Api.OpenApi;

var builder = WebApplication.CreateBuilder(args);


const string WebClientCorsPolicy = "web-client";
var allowedWebOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

var (connectionString, transitionalLocation, tokens, email, portal) =
    EventBookingConfiguration.Read(builder.Configuration);

builder.Services.AddEventBookingInfrastructure(connectionString, transitionalLocation, tokens);

var smtpHost = builder.Configuration["Email:Smtp:Host"]
    ?? throw new InvalidOperationException(
        "Email:Smtp:Host is required when Email:Provider is Smtp.");
var smtpPort = int.TryParse(builder.Configuration["Email:Smtp:Port"], out var port)
    ? port
    : throw new InvalidOperationException(
        "Email:Smtp:Port must be a valid integer when Email:Provider is Smtp.");

builder.Services.AddLocalEmailTransport(email, new SmtpOptions(smtpHost, smtpPort));
builder.Services.AddEventBookingApplication(portal,
    new EventBooking.Application.Access.StaffIdPolicy(builder.Configuration["Identity:StaffIdPattern"]));
builder.Services.AddEventBookingAuthentication(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddEventBookingOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy(WebClientCorsPolicy, policy =>
    {
        if (allowedWebOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedWebOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders("Content-Disposition");
        }
    });
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Anonymous, token-addressed routes: generous for a real attendee, unattractive for a script.
    // Partitioned by client address so one client's burst (or script) cannot consume the
    // allowance of every other attendee. ForwardedHeadersMiddleware (below) restores the real
    // client address when the app runs behind a proxy or load balancer.
    options.AddPolicy<string, RemoteIpRateLimiterPolicy>(BookingEndpoints.RateLimiterPolicy);
});

builder.Services.AddHostedService<InviteSweepService>();

var app = builder.Build();

app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "EventBooking API v1";
    options.SwaggerEndpoint("/openapi/v1.json", "EventBooking API v1");
    options.DisplayOperationId();
    options.HeadContent = "<link rel=\"alternate\" type=\"application/json\" href=\"/openapi/v1.json\" />";
});
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
});
app.UseCors(WebClientCorsPolicy);
app.UseAuthentication();
app.UseMiddleware<StaffIdentityRecorder>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous().WithAgentMetadata("getHealth");
app.MapApiDiscoveryEndpoints();

app.MapEventEndpoints();
app.MapAttendeeEndpoints();
app.MapAttendeeGroupEndpoints();
app.MapAdminEndpoints();
app.MapStaffAccessEndpoints();
app.MapMeEndpoints();
app.MapBookingEndpoints();
app.MapDashboardEndpoints();
app.MapAuditEndpoints();
app.MapAppointmentWorkspaceEndpoints();

app.Run();

/// <summary>Named so the integration test factory can start this host.</summary>
public partial class Program;
