using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>
/// Verifies seed staff numbers are validated against the deployment's staff-ID policy
/// rather than only the default format.
/// </summary>
[Collection("seed-anchor")]
public sealed class ConfiguredStaffIdSeedTests
{
    /// <summary>
    /// Verifies a deployment policy the seed data violates fails the seed with the staff entry.
    /// </summary>
    [Fact]
    public void RestrictivePolicy_RejectsSeedStaffIds()
    {
        try
        {
            DemoSeedSpec.ConfigureStaffIdPattern("^Z[0-9]+$");

            var exception = Assert.Throws<SeedException>(() => DemoSeedSpec.Staff());

            Assert.Contains("admin.user", exception.Message);
            Assert.Contains("is invalid", exception.Message);
        }
        finally
        {
            DemoSeedSpec.ConfigureStaffIdPattern(null);
        }
    }

    /// <summary>
    /// Verifies restoring the default policy loads the seed staff numbers again.
    /// </summary>
    [Fact]
    public void DefaultPolicy_AcceptsSeedStaffIds()
    {
        try
        {
            DemoSeedSpec.ConfigureStaffIdPattern(null);

            Assert.Equal(6, DemoSeedSpec.Staff().Count);
        }
        finally
        {
            DemoSeedSpec.ConfigureStaffIdPattern(null);
        }
    }
}
