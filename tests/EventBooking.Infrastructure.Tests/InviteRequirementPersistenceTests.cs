using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies Invite requirement snapshots round-trip with restrictive constraints.</summary>
[Collection("postgres")]
public sealed class InviteRequirementPersistenceTests(PostgresFixture fixture)
{
    /// <summary>An Invite reloads options, requirements, and recovery linkage together.</summary>
    [Fact]
    public async Task SnapshotRoundTrips()
    {
        await fixture.ResetAsync();
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group, ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            "hash",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);

        await using (var write = fixture.NewContext())
        {
            write.Attendees.Add(attendee);
            write.Invites.Add(invite);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var actual = await read.Invites
            .Include(value => value.Options)
            .Include(value => value.Requirements)
            .SingleAsync(value => value.Id == invite.Id);
        Assert.Equal(3, actual.Options.Count);
        Assert.Equal(attendee.RequiredAppointmentTypeIds, actual.RequiredAppointmentTypeIds);
    }

    /// <summary>An Invite reloads the location set every later offer must be drawn from.</summary>
    [Fact]
    public async Task TheLocationSetRoundTrips()
    {
        await fixture.ResetAsync();
        var second = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Bo", "bo@example.com", group, ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            "hash-of-a-two-location-invite",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId, second],
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);

        await using (var write = fixture.NewContext())
        {
            write.Attendees.Add(attendee);
            write.Invites.Add(invite);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var actual = await read.Invites
            .Include(value => value.Locations)
            .SingleAsync(value => value.Id == invite.Id);

        Assert.Equal([ProposalFixture.LocationId, second], actual.LocationIds.Order());
        Assert.All(actual.Locations, location => Assert.Equal(invite.Id, location.InviteId));
    }

    /// <summary>The attendee's status stamp is the aggregate's own, and survives a round trip.</summary>
    [Fact]
    public async Task TheAttendeeStatusStampRoundTrips()
    {
        await fixture.ResetAsync();
        var stampedAt = new DateTimeOffset(2026, 9, 4, 11, 0, 0, TimeSpan.Zero);
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Cass", "cass@example.com", group, ProposalFixture.Now);
        attendee.MarkAwaitingAvailability(stampedAt);

        await using (var write = fixture.NewContext())
        {
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var actual = await read.Attendees.SingleAsync(value => value.Id == attendee.Id);

        Assert.Equal(AttendeeStatus.AwaitingAvailability, actual.Status);
        Assert.Equal(stampedAt, actual.StatusChangedAt);
    }
}
