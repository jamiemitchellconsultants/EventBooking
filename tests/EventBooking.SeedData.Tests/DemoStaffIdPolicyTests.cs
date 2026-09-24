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
}
