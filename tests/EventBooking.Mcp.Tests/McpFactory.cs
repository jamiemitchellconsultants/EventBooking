using System.Security.Claims;
using System.Text.Encodings.Web;
using EventBooking.Domain.Access;
using EventBooking.Infrastructure.Email;
using EventBooking.Mcp.Tests.Fakes;
using EventBooking.Mcp.Tools;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace EventBooking.Mcp.Tests;

/// <summary>
/// Starts the real MCP host against a throwaway PostgreSQL container, with the bearer
/// token validation replaced by a test scheme so a test can say who is calling by
/// setting <see cref="SignedInAs"/>.
/// </summary>
public sealed class McpFactory : WebApplicationFactory<EventTools>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();
    private int _staffIdSequence;

    /// <summary>The Entra object identifier every request is made as. Null means anonymous.</summary>
    public Guid? SignedInAs { get; set; }

    /// <summary>The provider-issued staff number claim, or null to omit the claim.</summary>
    public string? StaffIdClaim { get; set; } = "U999999";

    /// <summary>The untrusted <c>roles</c> claim values attached to authenticated test requests.</summary>
    public IReadOnlyCollection<string> RolesClaim { get; set; } = [];

    /// <summary>Stands in for AWS SES so MCP tools that send email don't need an AWS environment.</summary>
    public RecordingEmailTransport EmailTransport { get; } = new();

    /// <summary>Creates a staff access profile for a fresh identity.</summary>
    /// <param name="roles">The roles to grant.</param>
    /// <param name="appointmentTypeId">The shared scope, if any.</param>
    /// <param name="staffIdClaim">The fixed staff number for the request, or null to allocate one.</param>
    /// <returns>The new staff identity.</returns>
    public async Task<Guid> GivenStaffAsync(
        IReadOnlyCollection<Role> roles,
        Guid? appointmentTypeId,
        string? staffIdClaim = null)
    {
        StaffIdClaim = staffIdClaim ?? $"U{Interlocked.Increment(ref _staffIdSequence):D6}";
        RolesClaim = roles.Select(role => role.ToString()).ToList();
        var staffUserId = Guid.NewGuid();
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EventBooking.Infrastructure.Persistence.EventBookingDbContext>();
        if (roles.Contains(Role.Manager) && appointmentTypeId is not null)
        {
            var previous = await context.StaffAccessProfiles.SingleOrDefaultAsync(profile =>
                profile.IsManager && profile.AppointmentTypeId == appointmentTypeId);
            if (previous is not null)
            {
                context.StaffAccessProfiles.Remove(previous);
            }
        }

        context.StaffAccessProfiles.Add(
            StaffAccessProfile.Create(staffUserId, roles, appointmentTypeId));
        await context.SaveChangesAsync();
        return staffUserId;
    }

    /// <summary>Returns the recorded identity for the provider key, when one exists.</summary>
    /// <param name="staffUserId">The provider key to find.</param>
    /// <returns>The recorded identity, or null.</returns>
    public async Task<StaffIdentity?> FindIdentityAsync(Guid staffUserId)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EventBooking.Infrastructure.Persistence.EventBookingDbContext>();
        return await context.StaffIdentities.AsNoTracking().SingleOrDefaultAsync(
            identity => identity.StaffUserId == staffUserId);
    }

    /// <summary>Returns the access profile for the provider key, when one exists.</summary>
    /// <param name="staffUserId">The provider key to find.</param>
    /// <returns>The access profile, or null.</returns>
    public async Task<StaffAccessProfile?> FindProfileAsync(Guid staffUserId)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EventBooking.Infrastructure.Persistence.EventBookingDbContext>();
        return await context.StaffAccessProfiles.AsNoTracking().SingleOrDefaultAsync(
            profile => profile.StaffUserId == staffUserId);
    }

    /// <summary>Starts the container and migrates the throwaway database.</summary>
    /// <returns>A task tracking initialization.</returns>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EventBooking.Infrastructure.Persistence.EventBookingDbContext>();
        await context.Database.MigrateAsync();
    }

    /// <summary>Disposes the container and the host.</summary>
    /// <returns>A task tracking disposal.</returns>
    public new async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:EventBooking", _container.GetConnectionString());
        builder.UseSetting("Tokens:SigningKey", "a-test-signing-key-that-is-long-enough-here");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(this);
            services
                .AddAuthentication(TestAuthenticationHandler.Scheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.Scheme, _ => { });

            // The real transport constructs an AWS SES client that throws immediately outside an
            // AWS environment (no RegionEndpoint or ServiceURL configured) — exactly where CI runs.
            services.AddSingleton<IEmailTransport>(EmailTransport);
        });
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        McpFactory factory) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "Test";

        /// <inheritdoc />
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (factory.SignedInAs is null)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new("oid", factory.SignedInAs.Value.ToString()),
            };
            if (factory.StaffIdClaim is not null)
            {
                claims.Add(new Claim("staff_id", factory.StaffIdClaim));
            }
            foreach (var role in factory.RolesClaim)
            {
                claims.Add(new Claim("roles", role));
            }

            var identity = new ClaimsIdentity(claims, Scheme);

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}

/// <summary>Shares one MCP host across the endpoint tests.</summary>
[CollectionDefinition("mcp")]
public sealed class McpCollection : ICollectionFixture<McpFactory>;
