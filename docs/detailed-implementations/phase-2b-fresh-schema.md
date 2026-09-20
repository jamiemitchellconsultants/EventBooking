# 02b — One fresh schema, and the roles that keep the audit trail append-only (Task 9b)

[← Phase overview](phase-2-persistence.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task completes master Task 9 and settles decision D12. The predecessor's 34 migrations describe a database EventBooking never had: a three-type, single-site schema patched 34 times towards one it was never going to reach. They go, and one initial migration takes their place — the schema the generalised domain actually wants, with the constraints the design names and two database roles that make the audit trail append-only for the application.

> Use superpowers:executing-plans. Complete changed types and exact before/after files are embedded
> in the numbered companion volumes; apply them with the script in Step 3, never by hand.

**Goal:** `Persistence/Migrations/` holds exactly one migration. It creates every table from the current model, seeds the reference rows the predecessor seeded from its chain, carries the eligibility index and the token version columns from the start, and grants an application role DML everywhere except `AuditLog`, where it may only insert and read.

**Architecture:** Three things about a squash are worth stating, because none is automatic. The seed rows the old chain inserted from raw SQL move into the model's own seed data, so the model snapshot owns them and the migration is regenerable rather than hand-patched. The roles live in `Persistence/Sql/roles.sql` as an embedded resource applied before the migration, because the migration grants to roles it deliberately does not create — a deployment owns who logs in, and a database migrated without the script is still valid, so the grants are skipped rather than fatal. And the guard tests that pinned named migrations in the chain retire with the chain: what they protected is now a constraint the schema tests assert directly against a real PostgreSQL 16.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers, bUnit.

**Spec:** [Master Task 9](../superpowers/plans/2026-09-19-eventbooking-implementation.md), [domain model](../design/01-domain-model.md), [functional requirements](../design/02-functional-requirements.md), [ontology](../ontology.md).

## Global constraints

Nothing in this repository has shipped, so there is no data to preserve and no down-migration path worth keeping: this is a squash, not an upgrade. Every figure in the schema tests comes from Testcontainers against PostgreSQL 16 — never an in-memory provider, which has none of these constraints. `start_utc` stays **nullable** here: Task 11's repository is what computes it, and a non-nullable column would take a silent 0001-01-01 for every row nothing has computed yet, which the eligibility query would read as an event in the past.

## Review focus

STOP AND CHECK five things. There is exactly one migration and it applies to an empty database. A capacity row cannot be written with a negative remainder, a remainder above its total, or a total of zero — all three fail on the same named check. Two events cannot share a proposal, and two Manager profiles cannot share an appointment type. As the application role, an insert into `AuditLog` succeeds while an update and a delete are refused with SQLSTATE 42501. And a stale version on a `Location` is a refused write, not a silent last-writer-wins.

### Task 9b: The fresh initial schema, its constraints and its database roles

**Files:**

- Modify: src/EventBooking.Domain/Notifications/EmailLog.cs
- Modify: src/EventBooking.Infrastructure/EventBooking.Infrastructure.csproj
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeConfiguration.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupConfiguration.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupRequirementConfiguration.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/EmailLogConfiguration.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/EventCapacityConfiguration.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/LocationConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/DatabaseRoles.cs
- Modify: src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260905060413_InitialSchema.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260905060413_InitialSchema.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260905163200_AttendeeStatusChangedAt.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260905163200_AttendeeStatusChangedAt.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260905192057_MakeEventProposalIdNullable.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260905192057_MakeEventProposalIdNullable.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260906120000_ScopedMultiRoleAuthorization.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260906120000_ScopedMultiRoleAuthorization.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260907125759_RepairCConcurrencyBackstops.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260907125759_RepairCConcurrencyBackstops.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260907180547_DurableEmailDelivery.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260907180547_DurableEmailDelivery.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260907204442_AddBookingAppointments.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260907204442_AddBookingAppointments.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260908211939_AddStaffIdentity.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260908211939_AddStaffIdentity.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260909100000_AddAttendeeGroupsAndAttendeeAssociation.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260909100000_AddAttendeeGroupsAndAttendeeAssociation.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260909110000_AddInviteRequirementSnapshots.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260909110000_AddInviteRequirementSnapshots.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260909120000_AddRecoveryBookings.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260909120000_AddRecoveryBookings.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260909130000_RequireAttendeeAttendeeGroup.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260909130000_RequireAttendeeAttendeeGroup.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260911120000_RelaxStaffAccessProfileScopeConstraint.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260911120000_RelaxStaffAccessProfileScopeConstraint.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260916045451_AddAuditLogTimestampIndex.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260916045451_AddAuditLogTimestampIndex.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260916052707_AddStaffIdentityDisplayName.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260916052707_AddStaffIdentityDisplayName.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260920060813_GeneralizeStaffIdentifiers.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260920060813_GeneralizeStaffIdentifiers.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260920061416_RequireEventProposal.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260920061416_RequireEventProposal.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260920071155_VariableEventWindowDuration.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260920071155_VariableEventWindowDuration.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260920072159_ManagedReferenceData.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260920072159_ManagedReferenceData.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260920073816_ListedAppointmentTypes.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260920073816_ListedAppointmentTypes.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260920095319_InviteLocations.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260920095319_InviteLocations.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260920102733_DeterministicAttendeeTokens.Designer.cs (delete)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260920102733_DeterministicAttendeeTokens.cs (delete)
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260920120000_InitialSchema.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260920120000_InitialSchema.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs
- Create: src/EventBooking.Infrastructure/Persistence/Sql/roles.sql
- Modify: src/EventBooking.SeedData/Program.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AttendeeGroupRequiredMigrationTests.cs (delete)
- Modify: tests/EventBooking.Infrastructure.Tests/BookingAppointmentPersistenceTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs
- Modify: tests/EventBooking.Infrastructure.Tests/SchemaTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/StaffAccessMigrationTests.cs (delete)
- Modify: tests/EventBooking.Infrastructure.Tests/StaffAccessProfileMigrationRegressionTests.cs (delete)

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them with the after files at Step 3, not before the failing test.

```csharp
-- Database roles for EventBooking.
--
-- Applied by the SeedData CLI before migrations, because the initial migration grants to the
-- application role and the role has to exist first. Both roles are NOLOGIN: a deployment attaches
-- its own login role to them, and a test reaches them with SET ROLE.
--
-- Re-running this script is safe.

DO $$
BEGIN
    -- Owns every object. Nothing the application connects as can drop a table.
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'eventbooking_migrator') THEN
        CREATE ROLE eventbooking_migrator NOLOGIN;
    END IF;

    -- Reads and writes rows, and nothing else. DDL is the migrator's alone.
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'eventbooking_app') THEN
        CREATE ROLE eventbooking_app NOLOGIN;
    END IF;
END
$$;

GRANT USAGE ON SCHEMA public TO eventbooking_app;

-- Tables that already exist, for a re-run against a migrated database. The initial migration
-- repeats these grants for the tables it creates.
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO eventbooking_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO eventbooking_app;

-- The audit trail is append-only for the application. Only the migrator may correct it, and that
-- correction is itself a deployment event rather than something a request can do.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'public' AND tablename = 'audit_log') THEN
        REVOKE UPDATE, DELETE, TRUNCATE ON audit_log FROM eventbooking_app;
    END IF;
END
$$;
```

```csharp
using System.Reflection;

namespace EventBooking.Infrastructure.Persistence;

/// <summary>
/// The role script that has to run before migrations, because the initial migration grants to a
/// role it does not create. Shipped as an embedded resource so a deployment cannot apply a copy
/// that has drifted from the schema it guards.
/// </summary>
public static class DatabaseRoles
{
    private const string ResourceName = "EventBooking.Infrastructure.Persistence.Sql.roles.sql";

    private static readonly Lazy<string> Contents = new(() =>
    {
        using var stream = typeof(DatabaseRoles).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource {ResourceName} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    /// <summary>Gets the role script, verbatim.</summary>
    public static string Script => Contents.Value;
}
```

```csharp
using EventBooking.Application.Events;
using EventBooking.Domain.Locations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("location");
        builder.HasKey(location => location.Id);

        builder.Property(location => location.Id).HasColumnName("id");
        builder.Property(location => location.Code)
            .HasColumnName("code")
            .HasMaxLength(Location.MaximumCodeLength)
            .IsRequired();
        builder.Property(location => location.Name)
            .HasColumnName("name")
            .HasMaxLength(Location.MaximumNameLength)
            .IsRequired();
        builder.Property(location => location.Address)
            .HasColumnName("address")
            .HasMaxLength(Location.MaximumAddressLength)
            .IsRequired();

        // The IANA identifier, not an offset: an offset cannot answer "was this in summer time".
        builder.Property(location => location.TimeZoneId)
            .HasColumnName("time_zone_id")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(location => location.IsActive).HasColumnName("is_active");
        builder.Property(location => location.Version).HasColumnName("version").IsConcurrencyToken();

        builder.HasIndex(location => location.Code).IsUnique();

        // The single site every proposal, event and invite is made at until Phase 3, matching the
        // transitional clock's zone. It is seeded here for the same reason the three appointment
        // types are: the rest of the schema references it, and nothing manages locations yet.
        builder.HasData(new
        {
            Id = TransitionalLocation.Id,
            Code = "TRANSITIONAL",
            Name = "Transitional location",
            Address = "Recorded against the transitional site until Phase 3.",
            TimeZoneId = TransitionalLocation.TimeZoneId,
            IsActive = true,
            Version = 1L,
        });
    }
}
```

```csharp
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("event");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.ProposalId).HasColumnName("proposal_id").IsRequired();
        builder.Property(s => s.LocationId).HasColumnName("location_id");
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>();

        // A derived persistence column, not domain data (design 04). It exists to index and order
        // the eligibility query, and the domain never reads it as the source of truth; PostgreSQL
        // cannot evaluate IANA rules in a generated column, so the application computes it.
        //
        // Nullable until Task 11, which is where the repository writes it in the same transaction
        // as the insert and makes the column required. A non-nullable column here would take EF's
        // default of 0001-01-01 for every row nothing has computed yet, and the eligibility query
        // filters on start_utc: a wrong instant would quietly hide the event rather than fail.
        builder.Property<DateTimeOffset?>("StartUtc").HasColumnName("start_utc");

        builder.OwnsOne(s => s.Window, window =>
        {
            window.Property(w => w.Date).HasColumnName("date");
            window.Property(w => w.StartTime).HasColumnName("start_time");
            window.Property(w => w.DurationMinutes).HasColumnName("duration_minutes");
            window.Ignore(w => w.EndTime);
        });
        builder.Navigation(s => s.Window).IsRequired();

        builder
            .HasMany(s => s.Capacities)
            .WithOne()
            .HasForeignKey(c => c.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Capacities).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => s.ProposalId).IsUnique();
        builder.HasIndex(s => s.Status);
        builder
            .HasIndex(nameof(Event.Status), nameof(Event.LocationId), "StartUtc")
            .HasDatabaseName("ix_event_eligibility");
    }
}
```

```csharp
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class EventCapacityConfiguration : IEntityTypeConfiguration<EventCapacity>
{
    public void Configure(EntityTypeBuilder<EventCapacity> builder)
    {
        // The table and column names here are written out in raw SQL in Task 47. Changing either
        // one means changing that query in the same commit.
        builder.ToTable("event_capacity");

        builder.HasKey(c => new { c.EventId, c.AppointmentTypeId });

        builder.Property(c => c.EventId).HasColumnName("event_id");
        builder.Property(c => c.AppointmentTypeId).HasColumnName("appointment_type_id");
        builder.Property(c => c.TotalHeadcount).HasColumnName("total_headcount");
        builder.Property(c => c.RemainingCapacity).HasColumnName("remaining_capacity");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_event_capacity_bounds",
            "remaining_capacity >= 0 AND remaining_capacity <= total_headcount AND total_headcount > 0"));
    }
}
```

**Context you need**

- Master plan Task 9: delete every ported migration; one new initial migration; configurations for `Location`, `EventProposalAppointmentType` and `InviteLocation`; a roles script in `Persistence/Sql/roles.sql` applied by the SeedData CLI before migrating.
- Master plan Task 9 (constraints): `event_capacity` keyed on (event id, appointment type id) with the bounds check; a unique non-null proposal on `event`; composite keys on the two join tables; unique codes on the three reference tables; a unique lower-cased email on `attendee`; a unique staff id; a partial unique index for one Manager per appointment type; version tokens on six tables; `claimed_at` and a claim count on `email_log`.
- Master plan Task 9 (roles): a migration owner role, and an application role with DML on every table except `AuditLog`, where it has only INSERT and SELECT.
- Design 04 (invite selection): `start_utc` is a derived persistence column, not domain data. The application writes it in the same transaction that inserts the `Event`, computing it from the date, the start time and the location's zone, because PostgreSQL cannot evaluate IANA rules in a generated column deterministically. An index on (status, location id, start utc) supports the query.
- Design 01 (time): the start and end instants are computed from the location's zone whenever they are needed and are not domain data — the derived `start_utc` column is the one exception, kept only for indexing.
- The `Location` aggregate has existed in the domain since Task 5 and has never been persisted. Task 8's first invite-location migration invented a second table by relating to it while it was unmapped; this task is where it gains a real one.
- The predecessor seeded three appointment types and five attendee groups with their requirement mappings from migrations. Those rows are change-controlled reference data and are still required; they move into the model's seed data here and retire with the fixed identifiers in Phase 3.
- The transitional location is the single fixed site every proposal, event and invite is made at until Phase 3. The fresh schema seeds that one row, for the same reason it seeds the appointment types.
- Task 9a added a short-lived migration for the two token version columns. This task deletes it with the rest of the chain and writes both columns into the initial migration.

- [ ] **Step 1: Write the failing tests**

Create these complete files before applying production changes.

tests/EventBooking.Infrastructure.Tests/SchemaTests.cs

```csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Domain.Locations;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

/// <summary>
/// The schema is one migration against a real PostgreSQL 16. Every constraint here is one the
/// application must not be able to violate even with a direct connection.
/// </summary>
[Collection("postgres")]
public class SchemaTests(PostgresFixture fixture)
{
    private const string CapacityBounds = "ck_event_capacity_bounds";

    [Fact]
    public async Task TheSchemaIsOneMigrationThatAppliesToAnEmptyDatabase()
    {
        var databaseName = $"eventbooking_fresh_{Guid.NewGuid():N}";
        var connectionString = ConnectionTo(databaseName);

        try
        {
            await CreateDatabaseAsync(databaseName);
            await ApplyRolesScriptAsync(connectionString);

            await using var context = NewContext(connectionString);
            Assert.Equal(
                ["20260920120000_InitialSchema"],
                (await context.Database.GetPendingMigrationsAsync()).ToArray());

            await context.Database.MigrateAsync();

            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
            var types = await context.AppointmentTypes.OrderBy(t => t.Code).ToListAsync();
            Assert.Equal(["DAT", "MED", "UNI"], types.Select(t => t.Code));
            Assert.Equal(1, (await context.SystemSettings.SingleAsync()).Id);
        }
        finally
        {
            await DropDatabaseAsync(databaseName);
        }
    }

    [Theory]
    [InlineData(5, -1)]
    [InlineData(5, 6)]
    [InlineData(0, 0)]
    public async Task TheDatabaseRefusesACapacityRowOutsideItsBounds(int total, int remaining)
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var eventId = await CreateEventWithoutCapacitiesAsync(context);

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO event_capacity
                    (event_id, appointment_type_id, total_headcount, remaining_capacity)
                VALUES ({0}, {1}, {2}, {3});
                """,
                [eventId, AppointmentTypeIds.DrugAndAlcoholTesting, total, remaining]));

        Assert.Contains(CapacityBounds, ex.ToString());
    }

    [Fact]
    public async Task TheDatabaseRefusesASecondEventForOneProposal()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var eventId = await CreateEventWithoutCapacitiesAsync(context);
        var proposalId = await context.Events.Where(e => e.Id == eventId)
            .Select(e => e.ProposalId).SingleAsync();

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO event
                    (id, proposal_id, location_id, status, date, start_time, duration_minutes, start_utc)
                SELECT {0}, proposal_id, location_id, status, date, start_time, duration_minutes, start_utc
                  FROM event WHERE id = {1};
                """,
                [Guid.NewGuid(), eventId]));

        Assert.Contains("proposal_id", ex.ToString());
    }

    [Fact]
    public async Task TheDatabaseRefusesASecondManagerForOneAppointmentType()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var typeId = AppointmentTypeIds.MedicalCheckUp;
        await InsertManagerProfileAsync(context, typeId);

        var ex = await Assert.ThrowsAnyAsync<Exception>(
            () => InsertManagerProfileAsync(context, typeId));

        Assert.Contains("ux_staff_access_profile_manager_appointment_type", ex.ToString());
    }

    [Fact]
    public async Task TheApplicationRoleMayAppendToTheAuditTrailButNeverRewriteIt()
    {
        await fixture.ResetAsync();

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, "SET ROLE eventbooking_app;");

        await ExecuteAsync(
            connection,
            """
            INSERT INTO audit_log (id, entity_type, entity_id, action, actor_type, actor_id, timestamp)
            VALUES (gen_random_uuid(), 'Event', gen_random_uuid(), 0, 2, 'schema-test', now());
            """);
        Assert.Equal(1L, await ScalarAsync(connection, "SELECT count(*) FROM audit_log;"));

        var update = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync(connection, "UPDATE audit_log SET actor_id = 'rewritten';"));
        Assert.Equal("42501", update.SqlState);

        var delete = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync(connection, "DELETE FROM audit_log;"));
        Assert.Equal("42501", delete.SqlState);
    }

    [Fact]
    public async Task TheApplicationRoleKeepsFullDmlOnEveryOtherTable()
    {
        await fixture.ResetAsync();

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, "SET ROLE eventbooking_app;");

        await ExecuteAsync(
            connection,
            """
            INSERT INTO location (id, code, name, address, time_zone_id, is_active, version)
            VALUES (gen_random_uuid(), 'ROLE', 'Role check', 'Somewhere', 'Europe/London', true, 1);
            UPDATE location SET name = 'Renamed' WHERE code = 'ROLE';
            DELETE FROM location WHERE code = 'ROLE';
            """);

        Assert.Equal(0L, await ScalarAsync(connection, "SELECT count(*) FROM location WHERE code = 'ROLE';"));
    }

    [Fact]
    public async Task AStaleVersionOnALocationIsARefusedWrite()
    {
        await fixture.ResetAsync();

        var location = Location.Create(
            Guid.NewGuid(), "STALE", "Stale check", "Somewhere", "Europe/London", new NodaTimeEventWindowZones());
        await using (var seed = fixture.NewContext())
        {
            seed.Add(location);
            await seed.SaveChangesAsync();
        }

        await using var first = fixture.NewContext();
        await using var second = fixture.NewContext();
        var readByFirst = await first.Set<Location>().SingleAsync(l => l.Id == location.Id);
        var readBySecond = await second.Set<Location>().SingleAsync(l => l.Id == location.Id);

        readByFirst.Rename("Renamed first");
        await first.SaveChangesAsync();

        readBySecond.Rename("Renamed second");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Theory]
    [InlineData("location", "code")]
    [InlineData("appointment_type", "code")]
    [InlineData("attendee_group", "code")]
    public async Task ACodeIsUniqueWithinItsReferenceTable(string table, string column)
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var indexes = await IndexDefinitionsAsync(context, table);

        Assert.Contains(
            indexes,
            definition => definition.Contains("UNIQUE", StringComparison.Ordinal)
                && definition.Contains($"({column})", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnAttendeeEmailIsUniqueIgnoringCase()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var indexes = await IndexDefinitionsAsync(context, "attendee");

        Assert.Contains(
            indexes,
            definition => definition.Contains("UNIQUE", StringComparison.Ordinal)
                && definition.Contains("lower(", StringComparison.Ordinal)
                && definition.Contains("email", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheEligibilityQueryHasItsCoveringIndex()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var indexes = await IndexDefinitionsAsync(context, "event");

        Assert.Contains(
            indexes,
            definition => definition.Contains("(status, location_id, start_utc)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ADeliveryAttemptCarriesItsClaimAndItsClaimCount()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var columns = await ColumnNamesAsync(context, "email_log");

        Assert.Contains("claimed_at", columns);
        Assert.Contains("claim_count", columns);
    }

    private static async Task InsertManagerProfileAsync(EventBookingDbContext context, Guid typeId) =>
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO staff_access_profile
                (staff_user_id, is_admin, is_coordinator, is_manager, is_appointment_staff,
                 appointment_type_id, version)
            VALUES ({0}, false, false, true, false, {1}, 1);
            """,
            [Guid.NewGuid(), typeId]);

    private static async Task<IReadOnlyList<string>> IndexDefinitionsAsync(
        EventBookingDbContext context, string table)
    {
        var rows = new List<string>();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        await context.Database.OpenConnectionAsync();
        command.CommandText =
            $"SELECT indexdef FROM pg_indexes WHERE schemaname = 'public' AND tablename = '{table}';";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<string>> ColumnNamesAsync(
        EventBookingDbContext context, string table)
    {
        var rows = new List<string>();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        await context.Database.OpenConnectionAsync();
        command.CommandText =
            $"""
            SELECT column_name FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = '{table}';
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private async Task ApplyRolesScriptAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, DatabaseRoles.Script);
    }

    private static async Task<Guid> CreateEventWithoutCapacitiesAsync(EventBookingDbContext context)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 1);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 1);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 1);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        context.EventProposals.Add(proposal);
        context.Events.Add(eventItem);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM event_capacity WHERE event_id = {eventItem.Id}");

        return eventItem.Id;
    }

    private string ConnectionTo(string databaseName) =>
        new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        }.ConnectionString;

    private static EventBookingDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(connectionString)
            .Options);

    private async Task CreateDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
            {
                Database = "postgres",
                Pooling = false,
            }.ConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, $"CREATE DATABASE {databaseName};");
    }

    private async Task DropDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
            {
                Database = "postgres",
                Pooling = false,
            }.ConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, $"DROP DATABASE IF EXISTS {databaseName};");
    }
}
```

- [ ] **Step 2: Verify the tests fail**

```bash
dotnet test tests/EventBooking.Infrastructure.Tests --filter FullyQualifiedName~SchemaTests
```

Expected: The suite does not compile: the roles script and the accessor that reads it do not exist, and `Location` has no table to write to. Once it does compile, the schema tests fail on the inherited chain: there is no single initial migration, no eligibility index, no claim count, and no role to refuse an update to the audit trail. A Docker startup failure is not the intended failure.

- [ ] **Step 3: Apply the exact implementation and regression edits**

The 26 phase-2b-edits-NNN.md files supply 66 complete before/after changes. The script validates every payload and current file before writing. It accepts an already-applied after state, refuses unrelated edits, and only deletes explicitly listed files whose before hash matches.

```bash
node --input-type=module <<'TASK_PAYLOAD'
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const root=fs.realpathSync('.'), plan='docs/detailed-implementations';
const sha=s=>crypto.createHash('sha256').update(s).digest('hex');
const names=fs.readdirSync(plan).filter(n=>n.startsWith('phase-2b-edits-')&&n.endsWith('.md')).sort();
if(names.length!==26)throw Error('Incomplete edit volumes.');
const entries=new Map();
for(const name of names){
 const text=fs.readFileSync(path.join(plan,name),'utf8');
 const pattern=/<!-- retirement-file: (.+) -->\n\n`{5}[^\n]*\n([\s\S]*?)\n`{5}/g;
 for(const match of text.matchAll(pattern)){
  const m=JSON.parse(match[1]);
  if(path.isAbsolute(m.file)||m.file.split('/').includes('..'))throw Error('Unsafe path.');
  const e=entries.get(m.id)??{...m,before:new Map(),after:new Map(),counts:{}};
  if(e.file!==m.file||e.beforeSha!==m.beforeSha||e.afterSha!==m.afterSha||e[m.side].has(m.part))throw Error('Conflicting metadata.');
  e[m.side].set(m.part,match[2]+'\n');e.counts[m.side]=m.parts;entries.set(m.id,e);
 }
}
if(entries.size!==66)throw Error('Incomplete operation set.');
const actions=[];
for(const e of entries.values()){
 for(const side of ['before','after']){
  if(e[side+'Sha']===null)continue;
  if(e[side].size!==e.counts[side])throw Error('Missing parts.');
  const parts=Array.from({length:e.counts[side]},(_,i)=>e[side].get(i+1));
  if(parts.some(p=>p===undefined))throw Error('Missing part number.');
  e[side+'Text']=parts.join('');
  if(sha(e[side+'Text'])!==e[side+'Sha'])throw Error('Payload checksum mismatch.');
 }
 const target=path.join(root,e.file);
 let parent=path.dirname(target);while(!fs.existsSync(parent))parent=path.dirname(parent);
 const resolved=fs.realpathSync(parent);
 if(resolved!==root&&!resolved.startsWith(root+path.sep))throw Error('Parent escapes checkout.');
 if(fs.existsSync(target)&&fs.lstatSync(target).isSymbolicLink())throw Error('Symlink target.');
 const actual=fs.existsSync(target)?sha(fs.readFileSync(target)):null;
 if(actual!==e.beforeSha&&actual!==e.afterSha)throw Error('Unrelated edit: '+e.file);
 actions.push({target,body:e.afterText,remove:e.afterSha===null});
}
for(const action of actions){
 if(action.remove){if(fs.existsSync(action.target))fs.unlinkSync(action.target);}
 else{fs.mkdirSync(path.dirname(action.target),{recursive:true});fs.writeFileSync(action.target,action.body);}
}
console.log('Applied '+actions.length+' verified file changes.');
TASK_PAYLOAD
```

The included migration, designer and model snapshot were generated with this exact command, and are already represented in the supplied after files. Do not generate a duplicate migration:

```bash
dotnet ef migrations add InitialSchema --project src/EventBooking.Infrastructure --startup-project src/EventBooking.Api
```

Review the complete migration in the edit volumes before running database-dependent tests.

- [ ] **Step 4: Verify the targeted tests pass**

```bash
dotnet test tests/EventBooking.Infrastructure.Tests --filter FullyQualifiedName~SchemaTests
```

Expected: all targeted cases pass, with zero skipped tests.

- [ ] **Step 5: Build and validate the complete solution**

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
```

Expected: zero warnings, zero errors and zero failed or skipped tests. The verified checkpoint contains 1543 tests: Domain 360, Application 424, Infrastructure 176, API 232, MCP 35, Web 241 and SeedData 75.

- [ ] **Step 6: Commit and push**

No ontology change belongs to this task. It gives existing concepts — `Location`, `EventCapacity`, `AuditLog` and the rest — the tables and constraints the design already describes. If you find a concept that is genuinely missing, edit `docs/ontology.ttl` and regenerate before committing.

```bash
git add -- \
  'src/EventBooking.Domain/Notifications/EmailLog.cs' \
  'src/EventBooking.Infrastructure/EventBooking.Infrastructure.csproj' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupRequirementConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/EmailLogConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/EventCapacityConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/LocationConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/DatabaseRoles.cs' \
  'src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905060413_InitialSchema.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905060413_InitialSchema.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905163200_AttendeeStatusChangedAt.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905163200_AttendeeStatusChangedAt.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905192057_MakeEventProposalIdNullable.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905192057_MakeEventProposalIdNullable.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260906120000_ScopedMultiRoleAuthorization.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260906120000_ScopedMultiRoleAuthorization.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260907125759_RepairCConcurrencyBackstops.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260907125759_RepairCConcurrencyBackstops.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260907180547_DurableEmailDelivery.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260907180547_DurableEmailDelivery.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260907204442_AddBookingAppointments.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260907204442_AddBookingAppointments.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260908211939_AddStaffIdentity.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260908211939_AddStaffIdentity.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909100000_AddAttendeeGroupsAndAttendeeAssociation.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909100000_AddAttendeeGroupsAndAttendeeAssociation.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909110000_AddInviteRequirementSnapshots.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909110000_AddInviteRequirementSnapshots.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909120000_AddRecoveryBookings.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909120000_AddRecoveryBookings.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909130000_RequireAttendeeAttendeeGroup.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909130000_RequireAttendeeAttendeeGroup.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260911120000_RelaxStaffAccessProfileScopeConstraint.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260911120000_RelaxStaffAccessProfileScopeConstraint.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260916045451_AddAuditLogTimestampIndex.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260916045451_AddAuditLogTimestampIndex.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260916052707_AddStaffIdentityDisplayName.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260916052707_AddStaffIdentityDisplayName.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920060813_GeneralizeStaffIdentifiers.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920060813_GeneralizeStaffIdentifiers.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920061416_RequireEventProposal.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920061416_RequireEventProposal.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920071155_VariableEventWindowDuration.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920071155_VariableEventWindowDuration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920072159_ManagedReferenceData.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920072159_ManagedReferenceData.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920073816_ListedAppointmentTypes.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920073816_ListedAppointmentTypes.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920095319_InviteLocations.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920095319_InviteLocations.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920102733_DeterministicAttendeeTokens.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920102733_DeterministicAttendeeTokens.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920120000_InitialSchema.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920120000_InitialSchema.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs' \
  'src/EventBooking.Infrastructure/Persistence/Sql/roles.sql' \
  'src/EventBooking.SeedData/Program.cs' \
  'tests/EventBooking.Infrastructure.Tests/AttendeeGroupRequiredMigrationTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/BookingAppointmentPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs' \
  'tests/EventBooking.Infrastructure.Tests/SchemaTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/StaffAccessMigrationTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/StaffAccessProfileMigrationRegressionTests.cs'
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
: "${EXECUTOR_COAUTHOR:?Set the executing harness co-author identity}"
git commit -m "feat(persistence): fresh initial schema with capacity and role constraints" -m "Co-authored-by: $EXECUTOR_COAUTHOR"
git push -u origin HEAD
```

Go to Task 10, which adds the unit-of-work lock helpers and the concurrency harness on top of this schema.
