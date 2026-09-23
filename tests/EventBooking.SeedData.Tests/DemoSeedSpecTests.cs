using EventBooking.Domain.Access;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>
/// Verifies the shape and relationships of the embedded demo dataset.
/// </summary>
public sealed class DemoSeedSpecTests
{
    /// <summary>
    /// Verifies that the demo dataset provides three distinct agreed event windows with capacity
    /// for all three appointment types.
    /// </summary>
    [Fact]
    public void AgreedEvents_AreThreeWindowsWithEightTwelveSix()
    {
        var events = DemoSeedSpec.AgreedEvents();

        Assert.Equal(3, events.Count);
        Assert.Equal(3, events.Select(s => (s.Date, s.StartTime)).Distinct().Count());
        foreach (var eventItem in events)
        {
            Assert.Equal(8, eventItem.DatHeadcount);
            Assert.Equal(12, eventItem.MedHeadcount);
            Assert.Equal(6, eventItem.UniHeadcount);
        }
    }

    /// <summary>
    /// Verifies that every open event proposal is after the dataset anchor and initially records
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
    /// Verifies the deterministic attendee journey mix across all five attendee groups.
    /// </summary>
    [Fact]
    public void Attendees_CoverEveryGroupAndJourney()
    {
        var attendees = DemoSeedSpec.Attendees();

        Assert.Equal(100, attendees.Count);
        Assert.Equal(100, attendees.Select(c => c.Email).Distinct().Count());
        Assert.All(attendees, c => Assert.False(string.IsNullOrWhiteSpace(c.Name)));
        Assert.All(
            attendees,
            c => Assert.Contains(
                c.AttendeeGroupCode,
                new[]
                {
                    "CABIN_CREW", "PILOTS", "GROUND_OPERATIONS_AGENT", "ENGINEERING",
                    "GROUND_TRANSPORT_SERVICES",
                }));
        Assert.Equal(20, attendees.Count(c => c.Journey == DemoAttendeeJourney.Unbooked));
        Assert.Equal(30, attendees.Count(c => c.Journey == DemoAttendeeJourney.Ready));
        Assert.Equal(25, attendees.Count(c => c.Journey == DemoAttendeeJourney.Outstanding));
        Assert.Equal(15, attendees.Count(c => c.Journey == DemoAttendeeJourney.NoShow));
        Assert.Equal(10, attendees.Count(c => c.Journey == DemoAttendeeJourney.RecoveryCompleted));
    }
}
