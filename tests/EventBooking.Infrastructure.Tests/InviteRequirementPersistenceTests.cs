using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
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
        var group = EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var candidate = Candidate.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, "hash", DateTimeOffset.UtcNow.AddDays(1),
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()], candidate.RequiredAppointmentTypeIds, 0);

        await using (var write = fixture.NewContext())
        {
            write.Candidates.Add(candidate);
            write.Invites.Add(invite);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var actual = await read.Invites
            .Include(value => value.Options)
            .Include(value => value.Requirements)
            .SingleAsync(value => value.Id == invite.Id);
        Assert.Equal(3, actual.Options.Count);
        Assert.Equal(candidate.RequiredAppointmentTypeIds, actual.RequiredAppointmentTypeIds);
    }
}
