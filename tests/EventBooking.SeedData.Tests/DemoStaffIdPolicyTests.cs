using System.Text.RegularExpressions;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>Verifies seed staff numbers satisfy the deployment staff-ID policy.</summary>
[Collection("seed-anchor")]
public sealed class DemoStaffIdPolicyTests
{
    /// <summary>Every demo staff number matches the deployment pattern and is unique.</summary>
    [Fact]
    public void SeedStaffIdsMatchTheDemoDeploymentPattern()
    {
        var staff = DemoSeedSpec.Staff();

        Assert.Equal(8, staff.Count);
        Assert.All(staff, person =>
            Assert.Matches(new Regex("^DEMO[0-9]{3}$"), person.StaffId.Value));
        Assert.Equal(8, staff.Select(person => person.StaffId).Distinct().Count());
    }

    /// <summary>Adopted provider identifiers redirect every derived user key.</summary>
    [Fact]
    public void ProviderIdOverrideRedirectsCoordinatorManagersAndAdmin()
    {
        var adopted = Guid.Parse("00000000-0000-0000-0000-000000000099");
        try
        {
            DemoSeedSpec.OverrideProviderIds(new Dictionary<string, Guid>
            {
                ["coordinator"] = adopted,
            });

            Assert.Equal(adopted, DemoSeedSpec.CoordinatorUserId());
            Assert.Equal(adopted, DemoSeedSpec.ProviderUserId("coordinator"));
            Assert.Equal(
                DemoSeedSpec.Staff().Single(x => x.Username == "admin").UserId,
                DemoSeedSpec.AdminUserId());
        }
        finally
        {
            DemoSeedSpec.OverrideProviderIds(null);
        }
    }
}
