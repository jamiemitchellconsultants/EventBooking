using EventBooking.Api;
using EventBooking.Api.Auth;
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using EventBooking.Mcp.Tools;

var builder = WebApplication.CreateBuilder(args);


var settings = EventBookingConfiguration.Read(builder.Configuration);

builder.Services.AddEventBookingInfrastructure(settings.ConnectionString, settings.Tokens);
builder.Services.AddLocalEmailTransport(settings.Email, settings.Smtp);
builder.Services.AddEventBookingApplication(
    settings.Portal,
    new EventBooking.Application.Access.StaffIdPolicy(builder.Configuration["Identity:StaffIdPattern"]));
builder.Services.AddEventBookingAuth(builder.Configuration);
builder.Services.AddSingleton<ICorrelationContext, AsyncLocalCorrelationContext>();
// The API's cursor signer, from the same key: a cursor from either surface reads on the other.
builder.Services.AddSingleton(new EventBooking.Api.Pagination.PageCursor(
    System.Text.Encoding.UTF8.GetBytes(settings.Tokens.SigningKey)));

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<IdentityTools>()
    .WithTools<ReferenceDataTools>()
    .WithTools<AdministrationTools>()
    .WithTools<NegotiationTools>()
    .WithTools<EventTools>()
    .WithTools<EventGroupTools>()
    .WithTools<AttendeeTools>()
    .WithTools<DashboardTools>()
    .WithTools<AuditTools>()
    .WithTools<WorkspaceTools>();

var app = builder.Build();

app.UseAuthentication();
app.UseMiddleware<StaffIdentityRecorder>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.MapGet("/health/ready", async (
        EventBooking.Infrastructure.Persistence.EventBookingDbContext context,
        CancellationToken ct) =>
    await context.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "ok" })
        : Results.Json(new { status = "unavailable" },
            statusCode: StatusCodes.Status503ServiceUnavailable))
    .AllowAnonymous();

// Same bearer tokens and the same staff policy as the API: every tool call runs
// as the signed-in staff identity, and each handler re-checks its capability.
app.MapMcp("/mcp").RequireAuthorization(AuthenticationExtensions.StaffPolicy);

app.Run();

/// <summary>Named so the integration test factory can start this host.</summary>
public partial class Program;
