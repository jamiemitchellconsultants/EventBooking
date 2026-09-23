using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace EventBooking.Infrastructure.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("eventbooking")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();
    private NpgsqlDataSource? _dataSource;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _dataSource = NpgsqlDataSource.Create(ConnectionString);

        await using var context = NewContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    public EventBookingDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new EventBookingDbContext(options);
    }

    /// <summary>
    /// Empties every table and re-seeds the fixed rows. Faster and far less flaky than dropping
    /// and re-creating the database between tests.
    /// </summary>
    public async Task ResetAsync()
    {
        var dataSource = _dataSource
            ?? throw new InvalidOperationException("The PostgreSQL fixture has not been initialized.");
        await using var connection = await dataSource.OpenConnectionAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                DO $$
                DECLARE statements CURSOR FOR
                    SELECT tablename FROM pg_tables
                    WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'
                    -- Change-controlled Employee Group rows stay seeded by the migration.
                    AND tablename NOT IN ('employee_group', 'employee_group_requirement');
                BEGIN
                    FOR statement IN statements LOOP
                        EXECUTE 'TRUNCATE TABLE ' || quote_ident(statement.tablename) || ' CASCADE;';
                    END LOOP;
                END $$;
                """;
            await command.ExecuteNonQueryAsync();
        }

        await using var context = NewContext();
        context.AppointmentTypes.AddRange(
            Domain.AppointmentTypes.AppointmentType.CreateFixedSet());
        context.SystemSettings.Add(Domain.Settings.SystemSettings.CreateDefault());
        await EnsureEmployeeGroupsSeededAsync(context);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Repairs change-controlled Employee Group rows after truncation. Truncating
    /// appointment_type cascades into the mapping table, so migration-seeded mappings are
    /// re-inserted when missing. Production databases rely on the migration alone.
    /// </summary>
    private static async Task EnsureEmployeeGroupsSeededAsync(EventBookingDbContext context)
    {
        var fixedGroups = new[]
        {
            EmployeeGroup.Define(
                EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting]),
            EmployeeGroup.Define(
                EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
            EmployeeGroup.Define(
                EmployeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT", "Ground Operations Agent", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            EmployeeGroup.Define(
                EmployeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            EmployeeGroup.Define(
                EmployeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES", "Ground Transport Services", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting]),
        };

        foreach (var group in fixedGroups)
        {
            var existing = await context.EmployeeGroups
                .Include(persisted => persisted.Requirements)
                .SingleOrDefaultAsync(persisted => persisted.Id == group.Id);
            if (existing is null)
            {
                context.EmployeeGroups.Add(group);
                continue;
            }

            foreach (var requirement in group.Requirements)
            {
                if (existing.Requirements.All(persisted => persisted.AppointmentTypeId != requirement.AppointmentTypeId))
                {
                    context.Add(requirement);
                }
            }
        }
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
