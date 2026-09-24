using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>Verifies the demo AppointmentStaff identity and application profile stay aligned.</summary>
[Collection("seed-anchor")]
public sealed class AppointmentStaffDemoSeedTests
{
    /// <summary>Verifies the scoped profile carries MED and the unscoped one carries no scope.</summary>
    [Fact]
    public void SeedContainsScopedAndUnscopedAppointmentStaffProfiles()
    {
        var scoped = Assert.Single(DemoSeedSpec.Staff(), value =>
            value.Username == "appointment.med");
        Assert.Equal([Role.AppointmentStaff], scoped.Roles);
        var medical = DemoSeedSpec.Build().AppointmentTypes.Single(type => type.Code == "MED");
        Assert.Equal(medical.Id, scoped.AppointmentTypeId);

        var unscoped = Assert.Single(DemoSeedSpec.Staff(), value =>
            value.Username == "appointment.unscoped");
        Assert.Equal([Role.AppointmentStaff], unscoped.Roles);
        Assert.Null(unscoped.AppointmentTypeId);
    }

    /// <summary>Verifies the local realm declares no demo users; the seed converges those.</summary>
    [Fact]
    public void LocalRealmContainsNoDemoUsers()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            RepoFile("deploy/keycloak/realm-export.json")));

        Assert.False(document.RootElement.TryGetProperty("users", out _));
        Assert.Contains(
            Role.AppointmentStaff.ToString(),
            document.RootElement.GetProperty("roles").GetProperty("realm").EnumerateArray()
                .Select(value => value.GetProperty("name").GetString()!));
    }

    /// <summary>Verifies all seed identities still have unique ids, usernames and staff numbers.</summary>
    [Fact]
    public void LocalRealmIdentityKeysRemainUnique()
    {
        var staff = DemoSeedSpec.Staff();

        Assert.Equal(8, staff.Count);
        Assert.Equal(8, staff.Select(person => person.UserId).Distinct().Count());
        Assert.Equal(8, staff.Select(person => person.Username).Distinct().Count());
        Assert.Equal(8, staff.Select(person => person.StaffId).Distinct().Count());
    }

    private static string RepoFile(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "EventBooking.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, relativePath);
    }
}
