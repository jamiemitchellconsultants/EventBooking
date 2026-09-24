using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>Verifies the demo AppointmentStaff identity and application profile stay aligned.</summary>
[Collection("seed-anchor")]
public sealed class AppointmentStaffDemoSeedTests
{
    private static readonly Guid AppointmentStaffId =
        Guid.Parse("dc5f9a90-7f54-46d1-8603-d4c317f47226");

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

    /// <summary>Verifies Keycloak supplies the fixed identity and AppointmentStaff role.</summary>
    [Fact]
    public void LocalRealmContainsTheIdentityWithAppointmentStaffRole()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            RepoFile("deploy/keycloak/realm-export.json")));
        var user = Assert.Single(
            document.RootElement.GetProperty("users").EnumerateArray(),
            value => value.GetProperty("username").GetString() == "appointment.staff");

        Assert.Equal(AppointmentStaffId.ToString(), user.GetProperty("id").GetString());
        Assert.Equal(
            [Role.AppointmentStaff.ToString()],
            user.GetProperty("realmRoles").EnumerateArray()
                .Select(value => value.GetString()!).ToArray());
    }

    /// <summary>Verifies all local identities still have unique ids and usernames.</summary>
    [Fact]
    public void LocalRealmIdentityKeysRemainUnique()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            RepoFile("deploy/keycloak/realm-export.json")));
        var users = document.RootElement.GetProperty("users").EnumerateArray().ToList();

        Assert.Equal(users.Count, users.Select(user => user.GetProperty("id").GetString()).Distinct().Count());
        Assert.Equal(users.Count, users.Select(user => user.GetProperty("username").GetString()).Distinct().Count());
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
