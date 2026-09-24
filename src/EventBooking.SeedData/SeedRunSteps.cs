// src/EventBooking.SeedData/SeedRunSteps.cs (complete)
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.SeedData;

public sealed class SeedRunSteps : ISeedRunSteps
{
    private readonly SeedCliOptions _options;
    private readonly Func<string, string?> _environment;
    private readonly ServiceProvider _provider;

    private SeedRunSteps(SeedCliOptions options, Func<string, string?> environment, ServiceProvider provider)
    {
        _options = options;
        _environment = environment;
        _provider = provider;
    }

    public static SeedRunSteps Create(SeedCliOptions options, Func<string, string?> environment)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (options.Demo)
        {
            var email = DemoEmailOptions.From(environment);
            services.AddEventBookingInfrastructure(options.ConnectionString, email.Tokens);
            services.AddEventBookingApplication(email.Portal,
                new EventBooking.Application.Access.StaffIdPolicy(environment("Identity__StaffIdPattern")));
            services.AddLocalEmailTransport(email.Sender, email.Smtp);
            services.AddScoped<DemoSeeder>();
            services.AddScoped<DemoInvitationSeeder>();
            services.AddSingleton<OutboxDispatcher>();
        }
        else
        {
            services.AddEventBookingPersistence(options.ConnectionString);
            services.AddSingleton<IClock, SystemClock>();
            services.AddSingleton<EventBooking.Domain.Time.IEventWindowZones, NodaTimeEventWindowZones>();
        }

        return new SeedRunSteps(options, environment, services.BuildServiceProvider());
    }

    private AsyncServiceScope Scope() => _provider.CreateAsyncScope();

    public async Task ApplyDatabaseRolesAsync(CancellationToken ct)
    {
        await using var scope = Scope();
        await scope.ServiceProvider.GetRequiredService<EventBookingDbContext>()
            .Database.ExecuteSqlRawAsync(DatabaseRoles.Script, ct);
    }

    public async Task ApplyMigrationsAsync(CancellationToken ct)
    {
        await using var scope = Scope();
        await scope.ServiceProvider.GetRequiredService<EventBookingDbContext>().Database.MigrateAsync(ct);
    }

    public async Task ReanchorToTodayAsync(CancellationToken ct)
    {
        await using var scope = Scope();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var zones = scope.ServiceProvider.GetRequiredService<EventBooking.Domain.Time.IEventWindowZones>();
        DemoSeedSpec.OverrideAnchor(zones.LocalDateOf(clock.UtcNow, "Europe/London"));
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>()
            .ReanchorAsync(DemoSeedSpec.Build(), ct);
    }

    public async Task<KeycloakSeedSummary?> ConvergeKeycloakAsync(bool recreateRealm, CancellationToken ct)
    {
        var step = new KeycloakSeedStep(_environment, static () => new HttpClient());
        return await step.RunAsync(false, recreateRealm, DemoSeedSpec.Staff(), ct);
    }

    public async Task<SeedSummary> SeedDemoAsync(bool wipeFirst, CancellationToken ct)
    {
        await using var scope = Scope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
        if (_options.Verbose) seeder.Progress = Console.Out;
        return wipeFirst ? await seeder.ReseedAsync(ct) : await seeder.RunAsync(ct);
    }

    public async Task<int> SendDemoInvitationsAsync(CancellationToken ct)
    {
        await using var scope = Scope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoInvitationSeeder>();
        if (_options.Verbose) seeder.Progress = Console.Out;
        return await seeder.RunAsync(ct);
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
