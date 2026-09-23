using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>Verifies demo data explicitly assigns groups and covers readiness/recovery journeys.</summary>
public sealed class EmployeeGroupJourneySeedTests
{
    /// <summary>Every group and every approved demo journey appears without requirement input.</summary>
    [Fact]
    public void CandidateSpecsUseExplicitGroupsAndCoverJourneys()
    {
        var candidates = DemoSeedSpec.Candidates();

        Assert.Superset(
            new HashSet<string>
            {
                "CABIN_CREW",
                "PILOTS",
                "GROUND_OPERATIONS_AGENT",
                "ENGINEERING",
                "GROUND_TRANSPORT_SERVICES",
            },
            candidates.Select(candidate => candidate.EmployeeGroupCode).ToHashSet());
        Assert.Contains(candidates, candidate => candidate.Journey == DemoCandidateJourney.Ready);
        Assert.Contains(candidates, candidate => candidate.Journey == DemoCandidateJourney.Outstanding);
        Assert.Contains(candidates, candidate => candidate.Journey == DemoCandidateJourney.NoShow);
        Assert.Contains(candidates, candidate => candidate.Journey == DemoCandidateJourney.RecoveryCompleted);
        Assert.DoesNotContain(
            typeof(CandidateSpec).GetProperties(),
            property => property.Name.Contains("Requirement", StringComparison.Ordinal));
    }
}
