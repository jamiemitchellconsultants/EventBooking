# 00c — Configurable staff identity, edits 1 (Task 3a)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — src/EventBooking.Api/Auth/HttpContextCallerAccessor.cs — 1/1

<!-- retirement-file: {"id":0,"file":"src/EventBooking.Api/Auth/HttpContextCallerAccessor.cs","beforeSha":"1dbf86a42b8773cb5b6e7dbf5009c2baa7fbedbff674fb6fde2457a5f733720a","afterSha":"638b21745c9bd4a58de33e7948a608292f20953e3ffcb5779388ea8d62a88576","side":"before","part":1,"parts":1} -->

`````csharp
using System.Security.Claims;
using EventBooking.Domain.Access;

namespace EventBooking.Api.Auth;

/// <summary>Reads provider and enterprise staff identifiers from authenticated HTTP claims.</summary>
public sealed class HttpContextCallerAccessor(IHttpContextAccessor accessor, ILogger<HttpContextCallerAccessor> logger) : ICallerAccessor
{
    /// <summary>The claim type a v1 Entra ID token uses.</summary>
    public const string ObjectIdClaim = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    /// <summary>The claim type a v2 Entra ID token uses.</summary>
    public const string ShortObjectIdClaim = "oid";

    /// <summary>The shared Keycloak and Entra ID claim containing the enterprise staff number.</summary>
    public const string StaffIdClaim = "staff_id";

    /// <summary>The shared Keycloak and Entra ID claim carrying identity-provider-assigned roles.</summary>
    public const string RolesClaim = "roles";

    /// <summary>The shared Keycloak and Entra ID claim carrying the caller's full name.</summary>
    public const string NameClaim = "name";

    /// <summary>Gets the provider identifier from the current authenticated principal.</summary>
    public Guid? StaffUserId => StaffUserIdOf(accessor.HttpContext?.User);

    /// <summary>Gets the validated enterprise staff number from the current principal.</summary>
    public StaffId? StaffId => StaffIdOf(accessor.HttpContext?.User);

    /// <summary>Gets the human-readable name from the current authenticated principal.</summary>
    public string? DisplayName => DisplayNameOf(accessor.HttpContext?.User);

    /// <summary>Gets the recognised roles the current authenticated principal's token carries.</summary>
    public IReadOnlySet<Role> Roles => RolesOf(
        accessor.HttpContext?.User,
        value => logger.LogWarning("Ignoring unknown identity-provider role {Role}.", value));

    /// <inheritdoc />
    public Guid RequireStaffUserId() =>
        StaffUserId ?? throw new InvalidOperationException("The request has no staff identity.");

    /// <inheritdoc />
    public StaffId RequireStaffId() =>
        StaffId ?? throw new InvalidOperationException("The request has no valid staff number.");

    /// <summary>Reads a provider identifier only from an authenticated identity.</summary>
    /// <param name="principal">The request principal.</param>
    /// <returns>The provider identifier, or null when absent or malformed.</returns>
    public static Guid? StaffUserIdOf(ClaimsPrincipal? principal)
    {
        foreach (var identity in principal?.Identities ?? [])
        {
            if (!identity.IsAuthenticated)
            {
                continue;
            }

            var value =
                identity.FindFirst(ShortObjectIdClaim)?.Value
                ?? identity.FindFirst(ObjectIdClaim)?.Value;

            if (Guid.TryParse(value, out var id))
            {
                return id;
            }
        }

        return null;
    }

    /// <summary>Reads and validates a staff number only from an authenticated identity.</summary>
    /// <param name="principal">The request principal.</param>
    /// <returns>The canonical staff number, or null when absent or malformed.</returns>
    public static StaffId? StaffIdOf(ClaimsPrincipal? principal)
    {
        foreach (var identity in principal?.Identities ?? [])
        {
            if (!identity.IsAuthenticated)
            {
                continue;
            }

            if (StaffId.TryParse(identity.FindFirst(StaffIdClaim)?.Value, out var staffId))
            {
                return staffId;
            }
        }

        return null;
    }

    /// <summary>Reads a human-readable name only from an authenticated identity.</summary>
    /// <param name="principal">The request principal.</param>
    /// <returns>The name, or null when absent, empty, or whitespace.</returns>
    public static string? DisplayNameOf(ClaimsPrincipal? principal)
    {
        foreach (var identity in principal?.Identities ?? [])
        {
            if (!identity.IsAuthenticated)
            {
                continue;
            }

            var value = identity.FindFirst(NameClaim)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>Reads and parses every recognised role claim value from an authenticated identity.</summary>
    /// <param name="principal">The request principal.</param>
    /// <param name="onRejected">Receives each rejected claim value for warning-level logging.</param>
    /// <returns>The parsed role set; empty when the claim is absent or unauthenticated.</returns>
    public static IReadOnlySet<Role> RolesOf(
        ClaimsPrincipal? principal,
        Action<string>? onRejected = null)
    {
        foreach (var identity in principal?.Identities ?? [])
        {
            if (!identity.IsAuthenticated)
            {
                continue;
            }

            var claims = identity.FindAll(RolesClaim).ToList();
            if (claims.Count == 0)
            {
                continue;
            }

            var roles = new HashSet<Role>();
            foreach (var claim in claims)
            {
                if (Enum.TryParse<Role>(claim.Value, ignoreCase: false, out var role) && Enum.IsDefined(role))
                {
                    roles.Add(role);
                }
                else
                {
                    onRejected?.Invoke(claim.Value);
                }
            }

            return roles;
        }

        return new HashSet<Role>();
    }
}
`````

## after — src/EventBooking.Api/Auth/HttpContextCallerAccessor.cs — 1/1

<!-- retirement-file: {"id":0,"file":"src/EventBooking.Api/Auth/HttpContextCallerAccessor.cs","beforeSha":"1dbf86a42b8773cb5b6e7dbf5009c2baa7fbedbff674fb6fde2457a5f733720a","afterSha":"638b21745c9bd4a58de33e7948a608292f20953e3ffcb5779388ea8d62a88576","side":"after","part":1,"parts":1} -->

`````csharp
using System.Security.Claims;
using EventBooking.Application.Access;
using EventBooking.Domain.Access;

namespace EventBooking.Api.Auth;

/// <summary>Reads provider and enterprise staff identifiers from authenticated HTTP claims.</summary>
public sealed class HttpContextCallerAccessor(IHttpContextAccessor accessor, ILogger<HttpContextCallerAccessor> logger, StaffIdPolicy? staffIdPolicy = null) : ICallerAccessor
{
    /// <summary>The claim type a v1 Entra ID token uses.</summary>
    public const string ObjectIdClaim = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    /// <summary>The claim type a v2 Entra ID token uses.</summary>
    public const string ShortObjectIdClaim = "oid";

    /// <summary>The shared Keycloak and Entra ID claim containing the enterprise staff number.</summary>
    public const string StaffIdClaim = "staff_id";

    /// <summary>The shared Keycloak and Entra ID claim carrying identity-provider-assigned roles.</summary>
    public const string RolesClaim = "roles";

    /// <summary>The shared Keycloak and Entra ID claim carrying the caller's full name.</summary>
    public const string NameClaim = "name";

    /// <summary>Gets the provider identifier from the current authenticated principal.</summary>
    public Guid? StaffUserId => StaffUserIdOf(accessor.HttpContext?.User);

    /// <summary>Gets the validated enterprise staff number from the current principal.</summary>
    public StaffId? StaffId => StaffIdOf(accessor.HttpContext?.User, staffIdPolicy?.Pattern ?? EventBooking.Domain.Access.StaffId.DefaultPattern);

    /// <summary>Gets the human-readable name from the current authenticated principal.</summary>
    public string? DisplayName => DisplayNameOf(accessor.HttpContext?.User);

    /// <summary>Gets the recognised roles the current authenticated principal's token carries.</summary>
    public IReadOnlySet<Role> Roles => RolesOf(
        accessor.HttpContext?.User,
        value => logger.LogWarning("Ignoring unknown identity-provider role {Role}.", value));

    /// <inheritdoc />
    public Guid RequireStaffUserId() =>
        StaffUserId ?? throw new InvalidOperationException("The request has no staff identity.");

    /// <inheritdoc />
    public StaffId RequireStaffId() =>
        StaffId ?? throw new InvalidOperationException("The request has no valid staff number.");

    /// <summary>Reads a provider identifier only from an authenticated identity.</summary>
    /// <param name="principal">The request principal.</param>
    /// <returns>The provider identifier, or null when absent or malformed.</returns>
    public static Guid? StaffUserIdOf(ClaimsPrincipal? principal)
    {
        foreach (var identity in principal?.Identities ?? [])
        {
            if (!identity.IsAuthenticated)
            {
                continue;
            }

            var value =
                identity.FindFirst(ShortObjectIdClaim)?.Value
                ?? identity.FindFirst(ObjectIdClaim)?.Value;

            if (Guid.TryParse(value, out var id))
            {
                return id;
            }
        }

        return null;
    }

    /// <summary>Reads and validates a staff number only from an authenticated identity.</summary>
    /// <param name="principal">The request principal.</param>
    /// <returns>The canonical staff number, or null when absent or malformed.</returns>
    public static StaffId? StaffIdOf(ClaimsPrincipal? principal, string pattern = EventBooking.Domain.Access.StaffId.DefaultPattern)
    {
        foreach (var identity in principal?.Identities ?? [])
        {
            if (!identity.IsAuthenticated)
            {
                continue;
            }

            if (StaffId.TryParse(identity.FindFirst(StaffIdClaim)?.Value, out var staffId, pattern))
            {
                return staffId;
            }
        }

        return null;
    }

    /// <summary>Reads a human-readable name only from an authenticated identity.</summary>
    /// <param name="principal">The request principal.</param>
    /// <returns>The name, or null when absent, empty, or whitespace.</returns>
    public static string? DisplayNameOf(ClaimsPrincipal? principal)
    {
        foreach (var identity in principal?.Identities ?? [])
        {
            if (!identity.IsAuthenticated)
            {
                continue;
            }

            var value = identity.FindFirst(NameClaim)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>Reads and parses every recognised role claim value from an authenticated identity.</summary>
    /// <param name="principal">The request principal.</param>
    /// <param name="onRejected">Receives each rejected claim value for warning-level logging.</param>
    /// <returns>The parsed role set; empty when the claim is absent or unauthenticated.</returns>
    public static IReadOnlySet<Role> RolesOf(
        ClaimsPrincipal? principal,
        Action<string>? onRejected = null)
    {
        foreach (var identity in principal?.Identities ?? [])
        {
            if (!identity.IsAuthenticated)
            {
                continue;
            }

            var claims = identity.FindAll(RolesClaim).ToList();
            if (claims.Count == 0)
            {
                continue;
            }

            var roles = new HashSet<Role>();
            foreach (var claim in claims)
            {
                if (Enum.TryParse<Role>(claim.Value, ignoreCase: false, out var role) && Enum.IsDefined(role))
                {
                    roles.Add(role);
                }
                else
                {
                    onRejected?.Invoke(claim.Value);
                }
            }

            return roles;
        }

        return new HashSet<Role>();
    }
}
`````

## before — src/EventBooking.Api/EventBooking.Api.csproj — 1/1

<!-- retirement-file: {"id":1,"file":"src/EventBooking.Api/EventBooking.Api.csproj","beforeSha":"8a2e1db19753f37e7564ad99d448be0fdd54309c09f17c730eea852ab1e95d1e","afterSha":"e84b112e16ee01f686ecd99586c5748c04d67a63169bdf75c2d0f5810401a632","side":"before","part":1,"parts":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk.Web">

  <ItemGroup>
    <ProjectReference Include="..\EventBooking.Application\EventBooking.Application.csproj" />
    <ProjectReference Include="..\EventBooking.Infrastructure\EventBooking.Infrastructure.csproj" />
    <ProjectReference Include="..\EventBooking.Api.Auth\EventBooking.Api.Auth.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
    <PackageReference Include="Swashbuckle.AspNetCore.SwaggerUI" />
  </ItemGroup>

</Project>
`````

## after — src/EventBooking.Api/EventBooking.Api.csproj — 1/1

<!-- retirement-file: {"id":1,"file":"src/EventBooking.Api/EventBooking.Api.csproj","beforeSha":"8a2e1db19753f37e7564ad99d448be0fdd54309c09f17c730eea852ab1e95d1e","afterSha":"e84b112e16ee01f686ecd99586c5748c04d67a63169bdf75c2d0f5810401a632","side":"after","part":1,"parts":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk.Web">

  <ItemGroup>
    <ProjectReference Include="..\EventBooking.Application\EventBooking.Application.csproj" />
    <ProjectReference Include="..\EventBooking.Infrastructure\EventBooking.Infrastructure.csproj" />
    <ProjectReference Include="..\EventBooking.Api.Auth\EventBooking.Api.Auth.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
    <PackageReference Include="Swashbuckle.AspNetCore.SwaggerUI" />
  </ItemGroup>

</Project>
`````

## before — src/EventBooking.Api/Program.cs — 1/1

<!-- retirement-file: {"id":2,"file":"src/EventBooking.Api/Program.cs","beforeSha":"7ea712cb8cac6276f9eb4a8cc7b69dcd6b7707f5cda9366cad7d8aec4de954c2","afterSha":"018cbfa354c29c56ede31548a8d82745a606170bab8c5d254a70f3bdc9343078","side":"before","part":1,"parts":1} -->

`````csharp
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
builder.Services.AddEventBookingApplication(portal);
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
`````

## after — src/EventBooking.Api/Program.cs — 1/1

<!-- retirement-file: {"id":2,"file":"src/EventBooking.Api/Program.cs","beforeSha":"7ea712cb8cac6276f9eb4a8cc7b69dcd6b7707f5cda9366cad7d8aec4de954c2","afterSha":"018cbfa354c29c56ede31548a8d82745a606170bab8c5d254a70f3bdc9343078","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## before — src/EventBooking.Application/Access/StaffAccessHandler.cs — 1/1

<!-- retirement-file: {"id":3,"file":"src/EventBooking.Application/Access/StaffAccessHandler.cs","beforeSha":"b5eeafba038f82d84e0d3f2e69bcc7939cc76e9b2a183b6cabf9ac36986e12d5","afterSha":"0e8c31421aa20813fae9ad19913c9ad233a4b9b9c8b686a0804b2dc59a8bfc28","side":"before","part":1,"parts":1} -->

`````csharp
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;

namespace EventBooking.Application.Access;

/// <summary>Projects an access profile with its optional enterprise staff number.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="StaffId">The staff id.</param>
/// <param name="Roles">The roles.</param>
/// <param name="AppointmentTypeId">The appointment type id.</param>
/// <param name="AppointmentTypeName">The appointment type name.</param>
/// <param name="Version">The version.</param>
/// <param name="DisplayName">The display name.</param>
public sealed record StaffAccessProfileView(
    Guid StaffUserId,
    StaffId? StaffId,
    IReadOnlyList<Role> Roles,
    Guid? AppointmentTypeId,
    string? AppointmentTypeName,
    long Version,
    // <summary>
    // The human-readable name mirrored from the identity provider, or null when the identity
    // carries none. Presentation data only; never authorization-relevant.
    // </summary>
    string? DisplayName = null);

/// <summary>Requests atomic replacement of one existing profile's appointment-type scope.</summary>
/// <param name="ActorStaffUserId">The actor staff user id.</param>
/// <param name="TargetStaffUserId">The target staff user id.</param>
/// <param name="AppointmentTypeId">The appointment type id.</param>
/// <param name="ExpectedVersion">The expected version.</param>
public sealed record ReplaceStaffAccessProfileScopeCommand(
    Guid ActorStaffUserId,
    Guid TargetStaffUserId,
    Guid? AppointmentTypeId,
    long ExpectedVersion);

/// <summary>Requests clearing one existing profile's Admin-owned scope.</summary>
/// <param name="ActorStaffUserId">The actor staff user id.</param>
/// <param name="TargetStaffUserId">The target staff user id.</param>
/// <param name="ExpectedVersion">The expected version.</param>
public sealed record ClearStaffAccessProfileScopeCommand(
    Guid ActorStaffUserId,
    Guid TargetStaffUserId,
    long ExpectedVersion);

/// <summary>Returns a profile mutation and any displaced manager identity.</summary>
/// <param name="Profile">The profile.</param>
/// <param name="FormerManagerStaffUserId">The former manager staff user id.</param>
public sealed record StaffAccessMutationView(
    StaffAccessProfileView Profile,
    Guid? FormerManagerStaffUserId);

/// <summary>Authorizes and applies complete staff-access administration operations.</summary>
/// <param name="profiles">The profiles.</param>
/// <param name="identities">The identities.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
public sealed class StaffAccessHandler(
    IStaffAccessProfileRepository profiles,
    IStaffIdentityRepository identities,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Defines list async for the current use case.</summary>
    /// <param name="actorStaffUserId">The actor staff user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<StaffAccessProfileView>>> ListAsync(
        Guid actorStaffUserId,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            actorStaffUserId, StaffCapability.ManageStaffAccess, null, cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<IReadOnlyList<StaffAccessProfileView>>.Failure(authorized.Error);
        }

        var current = await profiles.ListAsync(cancellationToken);
        // One listing serves both the staff number and the name; no extra query.
        var identityByUserId = (await identities.ListAsync(cancellationToken))
            .ToDictionary(identity => identity.StaffUserId);
        return Result<IReadOnlyList<StaffAccessProfileView>>.Success(
            current.Select(profile =>
            {
                var identity = identityByUserId.GetValueOrDefault(profile.StaffUserId);
                return ToView(profile, identity?.StaffId, identity?.DisplayName);
            }).ToList());
    }

    /// <summary>Resolves an observed staff number after checking administration capability.</summary>
    /// <param name="actorStaffUserId">The actor staff user id.</param>
    /// <param name="staffIdText">The staff id text.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<Guid>> ResolveIdentityAsync(
        Guid actorStaffUserId,
        string staffIdText,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            actorStaffUserId, StaffCapability.ManageStaffAccess, null, cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<Guid>.Failure(authorized.Error);
        }

        if (!StaffId.TryParse(staffIdText, out var staffId))
        {
            return Result<Guid>.Failure(Error.Validation("Enter a valid staff number."));
        }

        var identity = await identities.GetByStaffIdAsync(staffId!, cancellationToken);
        return identity is null
            ? Result<Guid>.Failure(Error.NotFound(
                "No one with that staff number has signed in yet."))
            : Result<Guid>.Success(identity.StaffUserId);
    }

    /// <summary>Replaces scope without accepting or changing identity-provider roles.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<StaffAccessMutationView>> ReplaceScopeAsync(
        ReplaceStaffAccessProfileScopeCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ActorStaffUserId, StaffCapability.ManageStaffAccess, null, cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<StaffAccessMutationView>.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var locked = (await profiles.LockAllAsync(cancellationToken)).ToList();
        if (!locked.Any(profile =>
                profile.StaffUserId == command.ActorStaffUserId
                && profile.IsAdmin
                && profile.IsValid()))
        {
            return Result<StaffAccessMutationView>.Failure(
                Error.Forbidden("Only an Admin can manage staff access."));
        }

        var current = locked.SingleOrDefault(
            profile => profile.StaffUserId == command.TargetStaffUserId);
        if (current is null)
        {
            return Result<StaffAccessMutationView>.Failure(Error.NotFound("No such staff profile."));
        }

        if (current.Version != command.ExpectedVersion)
        {
            return Conflict<StaffAccessMutationView>(
                "The staff profile was changed by another administrator.");
        }

        // Blocks moving an already-scoped Manager to a different type directly — matching the former
        // AbandonsManagedType guard from Issue #71, since roles no longer change here so the only way
        // this handler can abandon a managed type is by moving its Manager's scope away from it.
        if (current.IsManager
            && current.AppointmentTypeId is not null
            && command.AppointmentTypeId != current.AppointmentTypeId)
        {
            return Conflict<StaffAccessMutationView>(
                "Assign a replacement Manager for the current appointment type first.");
        }

        var previous = AuditStateOf(current);
        Guid? formerManagerId = null;
        if (current.IsManager && command.AppointmentTypeId is not null)
        {
            var former = locked.SingleOrDefault(profile =>
                profile.StaffUserId != current.StaffUserId
                && profile.IsManager
                && profile.AppointmentTypeId == command.AppointmentTypeId);

            if (former is not null)
            {
                formerManagerId = former.StaffUserId;
                var formerBefore = AuditStateOf(former);
                // Roles are identity-provider-owned and cannot be cleared here, so scope is nulled
                // unconditionally: leaving it set would let the displaced Manager keep passing
                // StaffAccessAuthorizer's IsManager + AppointmentTypeId match for this type, silently
                // un-displacing them and recreating two Managers for the same appointment type.
                former.Replace(former.Roles, null);
                Record(command.ActorStaffUserId, former.StaffUserId,
                    AuditAction.StaffAccessChanged, formerBefore, AuditStateOf(former));
            }
        }

        try
        {
            current.Replace(current.Roles, command.AppointmentTypeId);
        }
        catch (DomainException exception)
        {
            return Result<StaffAccessMutationView>.Failure(Error.Validation(exception.Message));
        }

        Record(command.ActorStaffUserId, current.StaffUserId,
            AuditAction.StaffAccessChanged, previous, AuditStateOf(current));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<StaffAccessMutationView>.Success(
            new StaffAccessMutationView(ToView(current, null), formerManagerId));
    }

    /// <summary>Clears scope without deleting or changing identity-provider roles.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> ClearScopeAsync(
        ClearStaffAccessProfileScopeCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ActorStaffUserId, StaffCapability.ManageStaffAccess, null, cancellationToken);
        if (authorized.IsFailure)
        {
            return Result.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var locked = await profiles.LockAllAsync(cancellationToken);
        if (!locked.Any(profile =>
                profile.StaffUserId == command.ActorStaffUserId
                && profile.IsAdmin
                && profile.IsValid()))
        {
            return Result.Failure(Error.Forbidden("Only an Admin can manage staff access."));
        }

        var current = locked.SingleOrDefault(
            profile => profile.StaffUserId == command.TargetStaffUserId);
        if (current is null)
        {
            return Result.Failure(Error.NotFound("No such staff profile."));
        }

        if (current.Version != command.ExpectedVersion)
        {
            return Result.Failure(
                Error.Conflict("The staff profile was changed by another administrator."));
        }

        if (current.AppointmentTypeId is null)
        {
            return Result.Failure(
                Error.Validation("This profile has no appointment-type scope to clear."));
        }

        if (current.IsManager)
        {
            return Result.Failure(Error.Conflict(
                "Assign a replacement Manager for the current appointment type first."));
        }

        var previous = AuditStateOf(current);
        current.Replace(current.Roles, null);
        Record(command.ActorStaffUserId, current.StaffUserId,
            AuditAction.StaffAccessChanged, previous, AuditStateOf(current));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private static Result<T> Conflict<T>(string message) =>
        Result<T>.Failure(Error.Conflict(message));

    private static StaffAccessProfileView ToView(
        StaffAccessProfile profile,
        StaffId? staffId,
        string? displayName = null) => new(
        profile.StaffUserId,
        staffId,
        profile.Roles.OrderBy(role => role).ToList(),
        profile.AppointmentTypeId,
        profile.AppointmentTypeId is null
            ? null
            : AppointmentTypeIds.NameOf(profile.AppointmentTypeId.Value),
        profile.Version,
        displayName);

    private void Record(
        Guid actorStaffUserId,
        Guid targetStaffUserId,
        AuditAction action,
        AuditState? previous,
        AuditState? current) =>
        audit.Record(
            AuditEntityTypes.StaffAccessProfile,
            targetStaffUserId,
            action,
            ActorType.Staff,
            actorStaffUserId.ToString(),
            JsonSerializer.Serialize(new { previous, current }));

    private static AuditState AuditStateOf(StaffAccessProfile profile) => new(
        profile.Roles.OrderBy(role => role).Select(role => role.ToString()).ToList(),
        profile.AppointmentTypeId,
        profile.Version);

    private sealed record AuditState(
        IReadOnlyList<string> Roles,
        Guid? AppointmentTypeId,
        long Version);
}
`````

## after — src/EventBooking.Application/Access/StaffAccessHandler.cs — 1/1

<!-- retirement-file: {"id":3,"file":"src/EventBooking.Application/Access/StaffAccessHandler.cs","beforeSha":"b5eeafba038f82d84e0d3f2e69bcc7939cc76e9b2a183b6cabf9ac36986e12d5","afterSha":"0e8c31421aa20813fae9ad19913c9ad233a4b9b9c8b686a0804b2dc59a8bfc28","side":"after","part":1,"parts":1} -->

`````csharp
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;

namespace EventBooking.Application.Access;

/// <summary>Projects an access profile with its optional enterprise staff number.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="StaffId">The staff id.</param>
/// <param name="Roles">The roles.</param>
/// <param name="AppointmentTypeId">The appointment type id.</param>
/// <param name="AppointmentTypeName">The appointment type name.</param>
/// <param name="Version">The version.</param>
/// <param name="DisplayName">The display name.</param>
public sealed record StaffAccessProfileView(
    Guid StaffUserId,
    StaffId? StaffId,
    IReadOnlyList<Role> Roles,
    Guid? AppointmentTypeId,
    string? AppointmentTypeName,
    long Version,
    // <summary>
    // The human-readable name mirrored from the identity provider, or null when the identity
    // carries none. Presentation data only; never authorization-relevant.
    // </summary>
    string? DisplayName = null);

/// <summary>Requests atomic replacement of one existing profile's appointment-type scope.</summary>
/// <param name="ActorStaffUserId">The actor staff user id.</param>
/// <param name="TargetStaffUserId">The target staff user id.</param>
/// <param name="AppointmentTypeId">The appointment type id.</param>
/// <param name="ExpectedVersion">The expected version.</param>
public sealed record ReplaceStaffAccessProfileScopeCommand(
    Guid ActorStaffUserId,
    Guid TargetStaffUserId,
    Guid? AppointmentTypeId,
    long ExpectedVersion);

/// <summary>Requests clearing one existing profile's Admin-owned scope.</summary>
/// <param name="ActorStaffUserId">The actor staff user id.</param>
/// <param name="TargetStaffUserId">The target staff user id.</param>
/// <param name="ExpectedVersion">The expected version.</param>
public sealed record ClearStaffAccessProfileScopeCommand(
    Guid ActorStaffUserId,
    Guid TargetStaffUserId,
    long ExpectedVersion);

/// <summary>Returns a profile mutation and any displaced manager identity.</summary>
/// <param name="Profile">The profile.</param>
/// <param name="FormerManagerStaffUserId">The former manager staff user id.</param>
public sealed record StaffAccessMutationView(
    StaffAccessProfileView Profile,
    Guid? FormerManagerStaffUserId);

/// <summary>Authorizes and applies complete staff-access administration operations.</summary>
/// <param name="profiles">The profiles.</param>
/// <param name="identities">The identities.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="staffIdPolicy">The configured staff-number validation expression.</param>
public sealed class StaffAccessHandler(
    IStaffAccessProfileRepository profiles,
    IStaffIdentityRepository identities,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    StaffIdPolicy? staffIdPolicy = null)
{
    /// <summary>Defines list async for the current use case.</summary>
    /// <param name="actorStaffUserId">The actor staff user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<StaffAccessProfileView>>> ListAsync(
        Guid actorStaffUserId,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            actorStaffUserId, StaffCapability.ManageStaffAccess, null, cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<IReadOnlyList<StaffAccessProfileView>>.Failure(authorized.Error);
        }

        var current = await profiles.ListAsync(cancellationToken);
        // One listing serves both the staff number and the name; no extra query.
        var identityByUserId = (await identities.ListAsync(cancellationToken))
            .ToDictionary(identity => identity.StaffUserId);
        return Result<IReadOnlyList<StaffAccessProfileView>>.Success(
            current.Select(profile =>
            {
                var identity = identityByUserId.GetValueOrDefault(profile.StaffUserId);
                return ToView(profile, identity?.StaffId, identity?.DisplayName);
            }).ToList());
    }

    /// <summary>Resolves an observed staff number after checking administration capability.</summary>
    /// <param name="actorStaffUserId">The actor staff user id.</param>
    /// <param name="staffIdText">The staff id text.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<Guid>> ResolveIdentityAsync(
        Guid actorStaffUserId,
        string staffIdText,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            actorStaffUserId, StaffCapability.ManageStaffAccess, null, cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<Guid>.Failure(authorized.Error);
        }

        if (!StaffId.TryParse(staffIdText, out var staffId, staffIdPolicy?.Pattern ?? StaffId.DefaultPattern))
        {
            return Result<Guid>.Failure(Error.Validation("Enter a valid staff number."));
        }

        var identity = await identities.GetByStaffIdAsync(staffId!, cancellationToken);
        return identity is null
            ? Result<Guid>.Failure(Error.NotFound(
                "No one with that staff number has signed in yet."))
            : Result<Guid>.Success(identity.StaffUserId);
    }

    /// <summary>Replaces scope without accepting or changing identity-provider roles.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<StaffAccessMutationView>> ReplaceScopeAsync(
        ReplaceStaffAccessProfileScopeCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ActorStaffUserId, StaffCapability.ManageStaffAccess, null, cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<StaffAccessMutationView>.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var locked = (await profiles.LockAllAsync(cancellationToken)).ToList();
        if (!locked.Any(profile =>
                profile.StaffUserId == command.ActorStaffUserId
                && profile.IsAdmin
                && profile.IsValid()))
        {
            return Result<StaffAccessMutationView>.Failure(
                Error.Forbidden("Only an Admin can manage staff access."));
        }

        var current = locked.SingleOrDefault(
            profile => profile.StaffUserId == command.TargetStaffUserId);
        if (current is null)
        {
            return Result<StaffAccessMutationView>.Failure(Error.NotFound("No such staff profile."));
        }

        if (current.Version != command.ExpectedVersion)
        {
            return Conflict<StaffAccessMutationView>(
                "The staff profile was changed by another administrator.");
        }

        // Blocks moving an already-scoped Manager to a different type directly — matching the former
        // AbandonsManagedType guard from Issue #71, since roles no longer change here so the only way
        // this handler can abandon a managed type is by moving its Manager's scope away from it.
        if (current.IsManager
            && current.AppointmentTypeId is not null
            && command.AppointmentTypeId != current.AppointmentTypeId)
        {
            return Conflict<StaffAccessMutationView>(
                "Assign a replacement Manager for the current appointment type first.");
        }

        var previous = AuditStateOf(current);
        Guid? formerManagerId = null;
        if (current.IsManager && command.AppointmentTypeId is not null)
        {
            var former = locked.SingleOrDefault(profile =>
                profile.StaffUserId != current.StaffUserId
                && profile.IsManager
                && profile.AppointmentTypeId == command.AppointmentTypeId);

            if (former is not null)
            {
                formerManagerId = former.StaffUserId;
                var formerBefore = AuditStateOf(former);
                // Roles are identity-provider-owned and cannot be cleared here, so scope is nulled
                // unconditionally: leaving it set would let the displaced Manager keep passing
                // StaffAccessAuthorizer's IsManager + AppointmentTypeId match for this type, silently
                // un-displacing them and recreating two Managers for the same appointment type.
                former.Replace(former.Roles, null);
                Record(command.ActorStaffUserId, former.StaffUserId,
                    AuditAction.StaffAccessChanged, formerBefore, AuditStateOf(former));
            }
        }

        try
        {
            current.Replace(current.Roles, command.AppointmentTypeId);
        }
        catch (DomainException exception)
        {
            return Result<StaffAccessMutationView>.Failure(Error.Validation(exception.Message));
        }

        Record(command.ActorStaffUserId, current.StaffUserId,
            AuditAction.StaffAccessChanged, previous, AuditStateOf(current));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<StaffAccessMutationView>.Success(
            new StaffAccessMutationView(ToView(current, null), formerManagerId));
    }

    /// <summary>Clears scope without deleting or changing identity-provider roles.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> ClearScopeAsync(
        ClearStaffAccessProfileScopeCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ActorStaffUserId, StaffCapability.ManageStaffAccess, null, cancellationToken);
        if (authorized.IsFailure)
        {
            return Result.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var locked = await profiles.LockAllAsync(cancellationToken);
        if (!locked.Any(profile =>
                profile.StaffUserId == command.ActorStaffUserId
                && profile.IsAdmin
                && profile.IsValid()))
        {
            return Result.Failure(Error.Forbidden("Only an Admin can manage staff access."));
        }

        var current = locked.SingleOrDefault(
            profile => profile.StaffUserId == command.TargetStaffUserId);
        if (current is null)
        {
            return Result.Failure(Error.NotFound("No such staff profile."));
        }

        if (current.Version != command.ExpectedVersion)
        {
            return Result.Failure(
                Error.Conflict("The staff profile was changed by another administrator."));
        }

        if (current.AppointmentTypeId is null)
        {
            return Result.Failure(
                Error.Validation("This profile has no appointment-type scope to clear."));
        }

        if (current.IsManager)
        {
            return Result.Failure(Error.Conflict(
                "Assign a replacement Manager for the current appointment type first."));
        }

        var previous = AuditStateOf(current);
        current.Replace(current.Roles, null);
        Record(command.ActorStaffUserId, current.StaffUserId,
            AuditAction.StaffAccessChanged, previous, AuditStateOf(current));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private static Result<T> Conflict<T>(string message) =>
        Result<T>.Failure(Error.Conflict(message));

    private static StaffAccessProfileView ToView(
        StaffAccessProfile profile,
        StaffId? staffId,
        string? displayName = null) => new(
        profile.StaffUserId,
        staffId,
        profile.Roles.OrderBy(role => role).ToList(),
        profile.AppointmentTypeId,
        profile.AppointmentTypeId is null
            ? null
            : AppointmentTypeIds.NameOf(profile.AppointmentTypeId.Value),
        profile.Version,
        displayName);

    private void Record(
        Guid actorStaffUserId,
        Guid targetStaffUserId,
        AuditAction action,
        AuditState? previous,
        AuditState? current) =>
        audit.Record(
            AuditEntityTypes.StaffAccessProfile,
            targetStaffUserId,
            action,
            ActorType.Staff,
            actorStaffUserId.ToString(),
            JsonSerializer.Serialize(new { previous, current }));

    private static AuditState AuditStateOf(StaffAccessProfile profile) => new(
        profile.Roles.OrderBy(role => role).Select(role => role.ToString()).ToList(),
        profile.AppointmentTypeId,
        profile.Version);

    private sealed record AuditState(
        IReadOnlyList<string> Roles,
        Guid? AppointmentTypeId,
        long Version);
}
`````

## after — src/EventBooking.Application/Access/StaffIdPolicy.cs — 1/1

<!-- retirement-file: {"id":4,"file":"src/EventBooking.Application/Access/StaffIdPolicy.cs","beforeSha":null,"afterSha":"2c8aba15b3eeccb38e674ba317bb35be2912d9fe14d88fc3476cb5b012beddaa","side":"after","part":1,"parts":1} -->

`````csharp
using System.Text.RegularExpressions;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Access;

/// <summary>Immutable deployment configuration shared by every staff-number input boundary.</summary>
public sealed class StaffIdPolicy
{
    /// <summary>Validates the deployment expression when the host starts.</summary>
    /// <param name="pattern">The configured regular expression, or the default when absent.</param>
    public StaffIdPolicy(string? pattern = null)
    {
        Pattern = pattern ?? StaffId.DefaultPattern;
        _ = new Regex(Pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }

    /// <summary>Gets the expression applied after trimming and uppercasing staff identifiers.</summary>
    public string Pattern { get; }
}
`````

## before — src/EventBooking.Application/DependencyInjection.cs — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Application/DependencyInjection.cs","beforeSha":"086f5950790fdcd0e286802031d2097a72038d0e9fca76bf26b271f375bff76e","afterSha":"956c5c0f41c77a77e31b33cbe5c91c2dd16625a97b9626569e320358048d2389","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Settings;
using EventBooking.Application.Events;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Application;

/// <summary>Defines application service collection extensions for the current use case.</summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Defines add event booking application for the current use case.</summary>
    /// <param name="services">The services.</param>
    /// <param name="portal">The portal.</param>
    public static IServiceCollection AddEventBookingApplication(
        this IServiceCollection services,
        AttendeePortalOptions portal)
    {
        services.AddSingleton(portal);

        // Shared services.
        services.AddScoped<EligibleEventFinder>();
        services.AddScoped<EmailDeliveryService>();
        services.AddScoped<InviteIssuer>();
        services.AddScoped<BookingCanceller>();
        services.AddScoped<IStaffAccessAuthorizer, StaffAccessAuthorizer>();
        services.AddScoped<StaffAccessHandler>();

        // Event negotiation.
        services.AddScoped<ProposeEventHandler>();
        services.AddScoped<AcceptProposalHandler>();
        services.AddScoped<ImportEventsHandler>();
        services.AddScoped<WithdrawAcceptanceHandler>();
        services.AddScoped<WithdrawProposalHandler>();
        services.AddScoped<GetManagerEventBoardHandler>();
        services.AddScoped<CancelEventHandler>();
        services.AddScoped<AdjustEventCapacityHandler>();

        // Attendees.
        services.AddScoped<ImportAttendeesHandler>();
        services.AddScoped<SaveAttendeeHandler>();
        services.AddScoped<DeleteAttendeeHandler>();
        services.AddScoped<ListAttendeesHandler>();
        services.AddScoped<ListAttendeeGroupsHandler>();
        services.AddScoped<AttendeeReadinessCalculator>();
        services.AddScoped<GetAttendeeReadinessHandler>();
        services.AddScoped<GetAttendeeBookingsHandler>();
        services.AddScoped<GetDashboardsHandler>();
        services.AddScoped<GetEventOperationsHandler>();
        services.AddScoped<GetAuditHistoryHandler>();
        services.AddScoped<GetAuditSearchHandler>();

        // Administration.
        services.AddScoped<AdminSettingsHandler>();
        services.AddScoped<MeHandler>();
        services.AddScoped<SyncStaffAccessProfileRolesHandler>();

        // Appointments.
        services.AddScoped<GetAppointmentWorkspaceHandler>();
        services.AddScoped<AppointmentRosterCsvFormatter>();
        services.AddScoped<RecoveryBookingOutcomeCoordinator>();
        services.AddScoped<UpdateBookingAppointmentStatusHandler>();

        // Invites and bookings.
        services.AddScoped<TriggerInviteHandler>();
        services.AddScoped<StartRecoveryHandler>();
        services.AddScoped<CancelRecoveryInviteHandler>();
        services.AddScoped<RetryEmailHandler>();
        services.AddScoped<ExpireInvitesHandler>();
        services.AddScoped<ViewInviteHandler>();
        services.AddScoped<ViewBookingHandler>();
        services.AddScoped<ConfirmBookingHandler>();
        services.AddScoped<CancelBookingHandler>();
        services.AddScoped<CancelAttendeeBookingHandler>();

        return services;
    }
}
`````

## after — src/EventBooking.Application/DependencyInjection.cs — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Application/DependencyInjection.cs","beforeSha":"086f5950790fdcd0e286802031d2097a72038d0e9fca76bf26b271f375bff76e","afterSha":"956c5c0f41c77a77e31b33cbe5c91c2dd16625a97b9626569e320358048d2389","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Settings;
using EventBooking.Application.Events;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Application;

/// <summary>Defines application service collection extensions for the current use case.</summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Defines add event booking application for the current use case.</summary>
    /// <param name="services">The services.</param>
    /// <param name="portal">The portal.</param>
    /// <param name="staffIdPolicy">The deployment staff-number validation policy.</param>
    public static IServiceCollection AddEventBookingApplication(
        this IServiceCollection services,
        AttendeePortalOptions portal,
        StaffIdPolicy? staffIdPolicy = null)
    {
        services.AddSingleton(portal);
        services.AddSingleton(staffIdPolicy ?? new StaffIdPolicy());

        // Shared services.
        services.AddScoped<EligibleEventFinder>();
        services.AddScoped<EmailDeliveryService>();
        services.AddScoped<InviteIssuer>();
        services.AddScoped<BookingCanceller>();
        services.AddScoped<IStaffAccessAuthorizer, StaffAccessAuthorizer>();
        services.AddScoped<StaffAccessHandler>();

        // Event negotiation.
        services.AddScoped<ProposeEventHandler>();
        services.AddScoped<AcceptProposalHandler>();
        services.AddScoped<ImportEventsHandler>();
        services.AddScoped<WithdrawAcceptanceHandler>();
        services.AddScoped<WithdrawProposalHandler>();
        services.AddScoped<GetManagerEventBoardHandler>();
        services.AddScoped<CancelEventHandler>();
        services.AddScoped<AdjustEventCapacityHandler>();

        // Attendees.
        services.AddScoped<ImportAttendeesHandler>();
        services.AddScoped<SaveAttendeeHandler>();
        services.AddScoped<DeleteAttendeeHandler>();
        services.AddScoped<ListAttendeesHandler>();
        services.AddScoped<ListAttendeeGroupsHandler>();
        services.AddScoped<AttendeeReadinessCalculator>();
        services.AddScoped<GetAttendeeReadinessHandler>();
        services.AddScoped<GetAttendeeBookingsHandler>();
        services.AddScoped<GetDashboardsHandler>();
        services.AddScoped<GetEventOperationsHandler>();
        services.AddScoped<GetAuditHistoryHandler>();
        services.AddScoped<GetAuditSearchHandler>();

        // Administration.
        services.AddScoped<AdminSettingsHandler>();
        services.AddScoped<MeHandler>();
        services.AddScoped<SyncStaffAccessProfileRolesHandler>();

        // Appointments.
        services.AddScoped<GetAppointmentWorkspaceHandler>();
        services.AddScoped<AppointmentRosterCsvFormatter>();
        services.AddScoped<RecoveryBookingOutcomeCoordinator>();
        services.AddScoped<UpdateBookingAppointmentStatusHandler>();

        // Invites and bookings.
        services.AddScoped<TriggerInviteHandler>();
        services.AddScoped<StartRecoveryHandler>();
        services.AddScoped<CancelRecoveryInviteHandler>();
        services.AddScoped<RetryEmailHandler>();
        services.AddScoped<ExpireInvitesHandler>();
        services.AddScoped<ViewInviteHandler>();
        services.AddScoped<ViewBookingHandler>();
        services.AddScoped<ConfirmBookingHandler>();
        services.AddScoped<CancelBookingHandler>();
        services.AddScoped<CancelAttendeeBookingHandler>();

        return services;
    }
}
`````
