using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>Verifies demo data explicitly assigns groups and covers readiness/recovery journeys.</summary>
public sealed class AttendeeGroupJourneySeedTests
{
    /// <summary>Every group and every approved demo journey appears without requirement input.</summary>
    [Fact]
    public void AttendeeSpecsUseExplicitGroupsAndCoverJourneys()
    {
        var attendees = DemoSeedSpec.Attendees();

        Assert.Superset(
            new HashSet<string>
            {
                "CABIN_CREW",
                "PILOTS",
                "GROUND_OPERATIONS_AGENT",
                "ENGINEERING",
                "GROUND_TRANSPORT_SERVICES",
            },
            attendees.Select(attendee => attendee.AttendeeGroupCode).ToHashSet());
        Assert.Contains(attendees, attendee => attendee.Journey == DemoAttendeeJourney.Ready);
        Assert.Contains(attendees, attendee => attendee.Journey == DemoAttendeeJourney.Outstanding);
        Assert.Contains(attendees, attendee => attendee.Journey == DemoAttendeeJourney.NoShow);
        Assert.Contains(attendees, attendee => attendee.Journey == DemoAttendeeJourney.RecoveryCompleted);
        Assert.DoesNotContain(
            typeof(AttendeeSpec).GetProperties(),
            property => property.Name.Contains("Requirement", StringComparison.Ordinal));
    }
}
