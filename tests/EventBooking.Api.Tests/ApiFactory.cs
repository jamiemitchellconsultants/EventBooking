using System.Security.Claims;
using System.Text.Encodings.Web;
using EventBooking.Api.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace EventBooking.Api.Tests;

/// <summary>
/// Starts the real host against a throwaway PostgreSQL container, with Entra ID replaced by a test
/// authentication scheme so a test can say who is calling by setting <see cref="SignedInAs"/>.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    /// <summary>The Entra object identifier every request is made as. Null means anonymous.</summary>
    public Guid? SignedInAs { get; set; }

    /// <summary>The untrusted <c>staff_id</c> claim attached to authenticated test requests.</summary>
    public string? StaffIdClaim { get; set; } = "U999999";

    /// <summary>The untrusted <c>roles</c> claim values attached to authenticated test requests.</summary>
    public IReadOnlyCollection<string> RolesClaim { get; set; } = [];

    /// <summary>The untrusted <c>name</c> claim attached to authenticated test requests.</summary>
    public string? NameClaim { get; set; }

    /// <summary>Stands in for AWS SES; a test can inspect what would have been sent.</summary>
    public RecordingEmailTransport EmailTransport { get; } = new();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        await context.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    public async Task<Guid> GivenStaffAsync(Role role, Guid? appointmentTypeId = null) =>
        await GivenStaffAsync([role], appointmentTypeId);

    public async Task<Guid> GivenStaffAsync(
        IReadOnlyCollection<Role> roles,
        Guid? appointmentTypeId) =>
        await GivenStaffWithIdAsync(Guid.NewGuid(), roles, appointmentTypeId);

    public async Task<Guid> GivenStaffWithIdAsync(
        Guid staffUserId,
        IReadOnlyCollection<Role> roles,
        Guid? appointmentTypeId)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
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

        // Staff requests reconcile the stored profile with the token's roles before
        // authorizing, so a seeded profile must arrive with matching token roles —
        // exactly as a production token carries the identity-provider roles the
        // profile mirrors. Tests asserting a mismatch assign RolesClaim afterwards.
        RolesClaim = roles.Select(role => role.ToString()).ToList();
        return staffUserId;
    }

    /// <summary>Stores a staff-number/provider-key pair as though the identity had signed in.</summary>
    public async Task GivenIdentityAsync(Guid staffUserId, string staffId, string? displayName = null)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        context.StaffIdentities.Add(StaffIdentity.Create(
            staffUserId,
            new StaffId(staffId),
            displayName,
            DateTimeOffset.Parse("2026-09-08T10:00:00Z")));
        await context.SaveChangesAsync();
    }

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

            // The dispatcher loop would race test assertions on staged rows. Tests drive
            // passes explicitly through the registered OutboxDispatcher singleton, so only
            // the hosted loop (the factory descriptor) is removed here.
            var loop = services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationFactory is not null)
                .ToList();
            foreach (var descriptor in loop)
            {
                services.Remove(descriptor);
            }
        });
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ApiFactory factory) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "Test";

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
            if (factory.NameClaim is not null)
            {
                claims.Add(new Claim("name", factory.NameClaim));
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

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
