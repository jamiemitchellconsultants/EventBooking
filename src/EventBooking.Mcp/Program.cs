using EventBooking.Api;
using EventBooking.Api.Auth;
using EventBooking.Application;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using EventBooking.Mcp.Tools;

var builder = WebApplication.CreateBuilder(args);


var (connectionString, headOffice, tokens, email, portal) =
    EventBookingConfiguration.Read(builder.Configuration);

builder.Services.AddEventBookingInfrastructure(connectionString, headOffice, tokens);

var smtpHost = builder.Configuration["Email:Smtp:Host"]
    ?? throw new InvalidOperationException(
        "Email:Smtp:Host is required when Email:Provider is Smtp.");
var smtpPort = int.TryParse(builder.Configuration["Email:Smtp:Port"], out var port)
    ? port
    : throw new InvalidOperationException(
        "Email:Smtp:Port must be a valid integer when Email:Provider is Smtp.");

builder.Services.AddLocalEmailTransport(email, new SmtpOptions(smtpHost, smtpPort));

builder.Services.AddEventBookingApplication(portal);
builder.Services.AddEventBookingAuthentication(builder.Configuration);

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<SlotTools>()
    .WithTools<CandidateTools>()
    .WithTools<AdminTools>()
    .WithTools<OperationsTools>();

var app = builder.Build();

app.UseAuthentication();
app.UseMiddleware<StaffIdentityRecorder>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

// Same bearer tokens and the same staff policy as the API: every tool call runs
// as the signed-in staff identity, and each handler re-checks its capability.
app.MapMcp("/mcp").RequireAuthorization(AuthenticationExtensions.StaffPolicy);

app.Run();

/// <summary>Named so the integration test factory can start this host.</summary>
public partial class Program;
