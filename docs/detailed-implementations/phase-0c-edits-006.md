# 00c — Configurable staff identity, edits 6 (Task 3a)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Infrastructure.Tests/StaffIdentityPersistenceTests.cs — 1/1

<!-- retirement-file: {"id":20,"file":"tests/EventBooking.Infrastructure.Tests/StaffIdentityPersistenceTests.cs","beforeSha":"b47ceace9ff92c36152f48bcd17d3e2dbd10e1a25f6915548c239bb9df2479aa","afterSha":"3406c42e5e68cef44a575d95f72ff6da9dfa9f5d2dacaf5a25921044bc354da4","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies the database contract for the provider identity mirror.</summary>
[Collection("postgres")]
public sealed class StaffIdentityPersistenceTests(PostgresFixture fixture)
{
    /// <summary>Verifies a canonical staff number resolves its provider identity.</summary>
    [Fact]
    public async Task IdentityRoundTripsAndIsFoundByCanonicalStaffId()
    {
        await fixture.ResetAsync();
        var userId = Guid.NewGuid();
        await using (var write = fixture.NewContext())
        {
            await new StaffIdentityRepository(write).UpsertAsync(
                userId,
                new StaffId("u123456"),
                null,
                DateTimeOffset.Parse("2026-09-08T10:00:00Z"),
                CancellationToken.None);
        }

        await using var read = fixture.NewContext();
        var actual = await new StaffIdentityRepository(read)
            .GetByStaffIdAsync(new StaffId("U123456"), CancellationToken.None);

        Assert.Equal(userId, actual!.StaffUserId);
        Assert.Equal("U123456", actual.StaffId.Value);
    }

    /// <summary>Verifies a repeated provider key atomically refreshes the mirrored observation.</summary>
    [Fact]
    public async Task UpsertRefreshesTheExistingProviderIdentity()
    {
        await fixture.ResetAsync();
        var userId = Guid.NewGuid();
        await using var write = fixture.NewContext();
        var repository = new StaffIdentityRepository(write);
        var first = DateTimeOffset.Parse("2026-09-08T10:00:00Z");

        await repository.UpsertAsync(
            userId, new StaffId("U123456"), null, first, CancellationToken.None);
        await repository.UpsertAsync(
            userId, new StaffId("U123456"), null, first.AddHours(1), CancellationToken.None);

        await using var read = fixture.NewContext();
        var identities = await new StaffIdentityRepository(read).ListAsync(CancellationToken.None);
        var identity = Assert.Single(identities);
        Assert.Equal(first.AddHours(1), identity.LastSeenAt);
    }

    /// <summary>Verifies the database backstops accept lowercase shape but reject bad or reused values.</summary>
    [Fact]
    public async Task DatabaseAcceptsLowercaseButRejectsMalformedAndDuplicateStaffIds()
    {
        await fixture.ResetAsync();
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using (var valid = connection.CreateCommand())
        {
            valid.CommandText = "INSERT INTO staff_identity (staff_user_id, staff_id, last_seen_at) VALUES (@user, 'u123456', now())";
            valid.Parameters.AddWithValue("user", Guid.NewGuid());
            await valid.ExecuteNonQueryAsync();
        }

        var invalid = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO staff_identity (staff_user_id, staff_id, last_seen_at) VALUES (@user, ' ', now())";
            command.Parameters.AddWithValue("user", Guid.NewGuid());
            await command.ExecuteNonQueryAsync();
        });
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalid.SqlState);

        var duplicate = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO staff_identity (staff_user_id, staff_id, last_seen_at) VALUES (@user, 'u123456', now())";
            command.Parameters.AddWithValue("user", Guid.NewGuid());
            await command.ExecuteNonQueryAsync();
        });
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
    }

    /// <summary>Verifies an observed name round-trips through the upsert and the listing.</summary>
    [Fact]
    public async Task UpsertWritesAndReadsBackDisplayName()
    {
        await fixture.ResetAsync();
        var userId = Guid.NewGuid();

        await using (var write = fixture.NewContext())
        {
            await new StaffIdentityRepository(write).UpsertAsync(
                userId,
                new StaffId("U000002"),
                "Dana Datson",
                DateTimeOffset.Parse("2026-09-08T10:00:00Z"),
                CancellationToken.None);
        }

        await using var read = fixture.NewContext();
        var identities = await new StaffIdentityRepository(read).ListAsync(CancellationToken.None);

        var identity = Assert.Single(identities);
        Assert.Equal("Dana Datson", identity.DisplayName);
    }

    /// <summary>Verifies a later token without a name clears the stored value.</summary>
    [Fact]
    public async Task UpsertOverwritesDisplayNameBackToNull()
    {
        await fixture.ResetAsync();
        var userId = Guid.NewGuid();
        var first = DateTimeOffset.Parse("2026-09-08T10:00:00Z");

        await using (var write = fixture.NewContext())
        {
            var repository = new StaffIdentityRepository(write);
            await repository.UpsertAsync(
                userId, new StaffId("U000003"), "Dana Datson", first, CancellationToken.None);
            await repository.UpsertAsync(
                userId, new StaffId("U000003"), null, first.AddHours(1), CancellationToken.None);
        }

        await using var read = fixture.NewContext();
        var identity = await new StaffIdentityRepository(read)
            .GetByStaffIdAsync(new StaffId("U000003"), CancellationToken.None);

        Assert.NotNull(identity);
        Assert.Null(identity!.DisplayName);
        Assert.Equal("U000003", identity.StaffId.Value);
        Assert.Equal(first.AddHours(1), identity.LastSeenAt);
    }

    /// <summary>Verifies an identity observed without a name stores and reads back a null column.</summary>
    [Fact]
    public async Task UpsertNullDisplayNameRoundTrips()
    {
        await fixture.ResetAsync();
        var userId = Guid.NewGuid();

        await using (var write = fixture.NewContext())
        {
            await new StaffIdentityRepository(write).UpsertAsync(
                userId,
                new StaffId("U000004"),
                null,
                DateTimeOffset.Parse("2026-09-08T10:00:00Z"),
                CancellationToken.None);
        }

        await using var read = fixture.NewContext();
        var identity = await new StaffIdentityRepository(read)
            .GetByStaffIdAsync(new StaffId("U000004"), CancellationToken.None);

        Assert.NotNull(identity);
        Assert.Equal(userId, identity!.StaffUserId);
        Assert.Null(identity.DisplayName);
    }

    /// <summary>Verifies a rename observed at the next refresh replaces the stored value.</summary>
    [Fact]
    public async Task UpsertReplacesAnEarlierDisplayName()
    {
        await fixture.ResetAsync();
        var userId = Guid.NewGuid();
        var first = DateTimeOffset.Parse("2026-09-08T10:00:00Z");

        await using (var write = fixture.NewContext())
        {
            var repository = new StaffIdentityRepository(write);
            await repository.UpsertAsync(
                userId, new StaffId("U000005"), "Old Name", first, CancellationToken.None);
            await repository.UpsertAsync(
                userId, new StaffId("U000005"), "New Name", first.AddHours(1), CancellationToken.None);
        }

        await using var read = fixture.NewContext();
        var identity = await new StaffIdentityRepository(read)
            .GetByStaffIdAsync(new StaffId("U000005"), CancellationToken.None);

        Assert.Equal("New Name", identity!.DisplayName);
    }
}
`````
