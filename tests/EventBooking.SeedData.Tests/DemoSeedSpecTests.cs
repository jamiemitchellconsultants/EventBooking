using EventBooking.Domain.Access;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>
/// Verifies the shape and relationships of the embedded demo dataset.
/// </summary>
public sealed class DemoSeedSpecTests
{
    /// <summary>
    /// Verifies that the demo dataset provides three distinct agreed slot windows with capacity
    /// for all three appointment types.
    /// </summary>
    [Fact]
    public void AgreedSlots_AreThreeWindowsWithEightTwelveSix()
    {
        var slots = DemoSeedSpec.AgreedSlots();

        Assert.Equal(3, slots.Count);
        Assert.Equal(3, slots.Select(s => (s.Date, s.StartTime)).Distinct().Count());
        foreach (var slot in slots)
        {
            Assert.Equal(8, slot.DatHeadcount);
            Assert.Equal(12, slot.MedHeadcount);
            Assert.Equal(6, slot.UniHeadcount);
        }
    }

    /// <summary>
    /// Verifies that every open slot proposal is after the dataset anchor and initially records
    /// headcount for exactly one appointment type.
    /// </summary>
    [Fact]
    public void OpenProposals_AreFiveWindowsWithOneHeadcountEach()
    {
        var anchor = DemoSeedSpec.AnchorDate();

        var proposals = DemoSeedSpec.OpenProposals();

        Assert.Equal(5, proposals.Count);
        Assert.Equal(5, proposals.Select(p => (p.Date, p.StartTime)).Distinct().Count());
        foreach (var proposal in proposals)
        {
            Assert.True(proposal.Date > anchor);
            var filled = new[] { proposal.DatHeadcount, proposal.MedHeadcount, proposal.UniHeadcount }
                .Count(h => h is not null);
            Assert.Equal(1, filled);
        }
    }

    /// <summary>
    /// Verifies that seeded staff assignments match the users and role scopes in the Keycloak
    /// demo realm.
    /// </summary>
    [Fact]
    public void Staff_MatchesTheKeycloakRealmUsers()
    {
        var staff = DemoSeedSpec.Staff();

        Assert.Equal(6, staff.Count);
        Assert.Single(staff, s => s.Roles.SequenceEqual([Role.Admin]));
        Assert.Equal(3, staff.Count(s => s.Roles.Contains(Role.Manager)));
        Assert.Single(staff, s => s.Roles.SequenceEqual([Role.Coordinator]));
        Assert.Single(staff, s => s.Roles.SequenceEqual([Role.AppointmentStaff]));
        Assert.All(
            staff.Where(s => s.Roles.Contains(Role.Manager)),
            s => Assert.NotNull(s.AppointmentTypeId));
        Assert.Equal(
            staff.Single(s => s.Roles.SequenceEqual([Role.Coordinator])).UserId,
            DemoSeedSpec.CoordinatorUserId());
        Assert.Equal(6, staff.Select(s => s.StaffId).Distinct().Count());
        Assert.All(staff, s => Assert.True(StaffId.TryParse(s.StaffId.Value, out _)));
    }

    /// <summary>
    /// Verifies the deterministic candidate journey mix across all five employee groups.
    /// </summary>
    [Fact]
    public void Candidates_CoverEveryGroupAndJourney()
    {
        var candidates = DemoSeedSpec.Candidates();

        Assert.Equal(100, candidates.Count);
        Assert.Equal(100, candidates.Select(c => c.Email).Distinct().Count());
        Assert.All(candidates, c => Assert.False(string.IsNullOrWhiteSpace(c.Name)));
        Assert.All(
            candidates,
            c => Assert.Contains(
                c.EmployeeGroupCode,
                new[]
                {
                    "CABIN_CREW", "PILOTS", "GROUND_OPERATIONS_AGENT", "ENGINEERING",
                    "GROUND_TRANSPORT_SERVICES",
                }));
        Assert.Equal(20, candidates.Count(c => c.Journey == DemoCandidateJourney.Unbooked));
        Assert.Equal(30, candidates.Count(c => c.Journey == DemoCandidateJourney.Ready));
        Assert.Equal(25, candidates.Count(c => c.Journey == DemoCandidateJourney.Outstanding));
        Assert.Equal(15, candidates.Count(c => c.Journey == DemoCandidateJourney.NoShow));
        Assert.Equal(10, candidates.Count(c => c.Journey == DemoCandidateJourney.RecoveryCompleted));
    }
}
